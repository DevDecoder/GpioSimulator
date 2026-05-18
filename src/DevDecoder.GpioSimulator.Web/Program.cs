using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DevDecoder.GpioSimulator.Common;

var contentRoot = AppContext.BaseDirectory;
var dir = new DirectoryInfo(contentRoot);
while (dir != null)
{
    if (Directory.Exists(Path.Combine(dir.FullName, "wwwroot")))
    {
        contentRoot = dir.FullName;
        break;
    }
    var candidate = Path.Combine(dir.FullName, "src", "DevDecoder.GpioSimulator.Web");
    if (Directory.Exists(Path.Combine(candidate, "wwwroot")))
    {
        contentRoot = candidate;
        break;
    }
    dir = dir.Parent;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});
var app = builder.Build();

var assemblyVersion = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion ?? typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
var bustVersion = Uri.EscapeDataString(assemblyVersion);

app.MapGet("/", async (HttpContext context) =>
{
    var htmlPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
    if (!File.Exists(htmlPath))
    {
        return Results.NotFound();
    }
    
    var html = await File.ReadAllTextAsync(htmlPath);
    html = ApplyCacheBusting(html, bustVersion);
    
    return Results.Content(html, "text/html", Encoding.UTF8);
});

app.MapGet("/index.html", async (HttpContext context) =>
{
    var htmlPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
    if (!File.Exists(htmlPath))
    {
        return Results.NotFound();
    }
    
    var html = await File.ReadAllTextAsync(htmlPath);
    html = ApplyCacheBusting(html, bustVersion);
    
    return Results.Content(html, "text/html", Encoding.UTF8);
});

app.UseDefaultFiles();
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".gsc"] = "application/json";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
app.UseWebSockets();

var clients = new ConcurrentDictionary<Guid, ClientConnection>();
var engine = new GpioSimulatorEngine();

void Log(string message)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss} GPIO Simulator] {message}");
}

void LoadBoardMapping(string boardId)
{
    try
    {
        string gscPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "components", $"{boardId}.gsc");
        if (!File.Exists(gscPath))
        {
            var dir = new DirectoryInfo(app.Environment.ContentRootPath);
            while (dir != null)
            {
                var candidate1 = Path.Combine(dir.FullName, "src", "DevDecoder.GpioSimulator.Web", "wwwroot", "components", $"{boardId}.gsc");
                if (File.Exists(candidate1))
                {
                    gscPath = candidate1;
                    break;
                }
                var candidate2 = Path.Combine(dir.FullName, "wwwroot", "components", $"{boardId}.gsc");
                if (File.Exists(candidate2))
                {
                    gscPath = candidate2;
                    break;
                }
                dir = dir.Parent;
            }
        }

        if (!File.Exists(gscPath))
        {
            Log($"GSC file not found: {gscPath}");
            return;
        }

        var json = File.ReadAllText(gscPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        var tempPhysToLog = new Dictionary<int, int>();

        if (root.TryGetProperty("pins", out var pinsProp))
        {
            foreach (var pin in pinsProp.EnumerateArray())
            {
                if (pin.TryGetProperty("physical", out var physProp) && 
                    pin.TryGetProperty("logical", out var logProp) && 
                    logProp.ValueKind == JsonValueKind.Number)
                {
                    int phys = physProp.GetInt32();
                    int log = logProp.GetInt32();
                    tempPhysToLog[phys] = log;
                }
            }
        }

        engine.LoadBoardMapping(boardId, tempPhysToLog);
        Log($"Loaded board mapping for: {boardId} ({tempPhysToLog.Count} pins)");
    }
    catch (Exception ex)
    {
        Log($"Error loading mapping for {boardId}: {ex.Message}");
    }
}

engine.BoardChanged += (boardId, mapping) =>
{
    _ = BroadcastMessage(JsonSerializer.Serialize(new
    {
        action = "board_change",
        boardId = boardId,
        pins = mapping
    }));
};

async Task BroadcastMessage(string msg)
{
    var bytes = Encoding.UTF8.GetBytes(msg);
    foreach (var c in clients.Values)
    {
        if (c.Socket.State == WebSocketState.Open)
        {
            try
            {
                await c.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch { }
        }
    }
}

LoadBoardMapping("raspberry_pi_5_breakout");

CancellationTokenSource? shutdownCts = null;
var shutdownLock = new object();

void StartShutdownTimer(int delaySeconds)
{
    lock (shutdownLock)
    {
        shutdownCts?.Cancel();
        shutdownCts = new CancellationTokenSource();
        var token = shutdownCts.Token;

        Task.Delay(TimeSpan.FromSeconds(delaySeconds), token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                Log("No active connections. Shutting down server...");
                Environment.Exit(0);
            }
        }, TaskContinuationOptions.ExecuteSynchronously);
    }
}

void CancelShutdownTimer()
{
    lock (shutdownLock)
    {
        shutdownCts?.Cancel();
        shutdownCts = null;
    }
}

app.MapGet("/api/status", () => Results.Json(new { status = "online" }));

app.MapGet("/api/pins", () => Results.Json(engine.GetAllPinStates().ToDictionary(
    kvp => kvp.Key,
    kvp => $"{kvp.Value.Mode}:{kvp.Value.Value}:{kvp.Value.OwnerId}:{kvp.Value.OwnerType}"
)));

app.MapPost("/api/board/active", (HttpContext context) =>
{
    var boardId = context.Request.Query["boardId"].ToString();
    if (string.IsNullOrEmpty(boardId))
    {
        return Results.BadRequest("Missing boardId parameter");
    }
    Log($"Setting active board layout to: {boardId}");
    LoadBoardMapping(boardId);
    return Results.Ok();
});

app.MapGet("/api/board/active", () => Results.Json(new
{
    boardId = engine.ActiveBoardId,
    pins = engine.ActivePhysToLog
}));

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var clientId = Guid.NewGuid();
            var clientType = context.Request.Query["client"].ToString();
            if (string.IsNullOrEmpty(clientType)) clientType = "ui";

            var clientScheme = context.Request.Query["scheme"].ToString();
            if (string.IsNullOrEmpty(clientScheme)) clientScheme = "Board";

            Log($"Client connected: {clientId} (Type: {clientType}, Scheme: {clientScheme})");

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var connection = new ClientConnection
            {
                ClientId = clientId,
                Socket = webSocket,
                Type = clientType,
                Scheme = clientScheme
            };
            clients.TryAdd(clientId, connection);

            if (clientType == "controller")
            {
                CancelShutdownTimer();
            }
            
            // Set up local websocket event bridges
            Action<int, string, string, string, string> stateHandler = async (physicalPin, mode, value, owner, type) =>
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    int targetPin = physicalPin;
                    if (clientScheme == "Logical")
                    {
                        targetPin = engine.ConvertPhysicalToLogical(physicalPin);
                    }
                    if (targetPin >= 0)
                    {
                        var msg = JsonSerializer.Serialize(new {
                            action = "state_change",
                            pin = targetPin,
                            mode = mode,
                            value = value,
                            ownerId = owner,
                            ownerType = type
                        });
                        try
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch { }
                    }
                }
            };

            Action<int> closeHandler = async (physicalPin) =>
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    int targetPin = physicalPin;
                    if (clientScheme == "Logical")
                    {
                        targetPin = engine.ConvertPhysicalToLogical(physicalPin);
                    }
                    if (targetPin >= 0)
                    {
                        var msg = JsonSerializer.Serialize(new {
                            action = "close",
                            pin = targetPin
                        });
                        try
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch { }
                    }
                }
            };

            Action<string, Dictionary<int, int>> boardHandler = async (boardId, pins) =>
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    var msg = JsonSerializer.Serialize(new {
                        action = "board_change",
                        boardId = boardId,
                        pins = pins
                    });
                    try
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch { }
                }
            };

            engine.PinStateChanged += stateHandler;
            engine.PinClosed += closeHandler;
            engine.BoardChanged += boardHandler;

            try
            {
                // Send connection and initial board state
                var connMsg = JsonSerializer.Serialize(new { action = "connected", clientId = clientId.ToString() });
                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(connMsg)), WebSocketMessageType.Text, true, CancellationToken.None);

                var initBoard = JsonSerializer.Serialize(new { action = "board_change", boardId = engine.ActiveBoardId, pins = engine.ActivePhysToLog });
                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(initBoard)), WebSocketMessageType.Text, true, CancellationToken.None);

                // Sync currently active states
                var initialStates = engine.GetAllPinStates();
                foreach (var kvp in initialStates)
                {
                    int targetPin = kvp.Key;
                    if (clientScheme == "Logical")
                    {
                        targetPin = engine.ConvertPhysicalToLogical(kvp.Key);
                    }
                    if (targetPin >= 0)
                    {
                        var msg = JsonSerializer.Serialize(new { 
                            action = "state_change", 
                            pin = targetPin, 
                            mode = kvp.Value.Mode, 
                            value = kvp.Value.Value,
                            ownerId = kvp.Value.OwnerId,
                            ownerType = kvp.Value.OwnerType
                        });
                        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }

                var buffer = new byte[1024 * 4];
                
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    using var json = JsonDocument.Parse(message);
                    var root = json.RootElement;
                    if (root.TryGetProperty("action", out var actionProp))
                    {
                        var action = actionProp.GetString();
                        
                        if (action == "log")
                        {
                            var val = root.GetProperty("value").GetString() ?? "";
                            Log(val);
                            continue;
                        }
                        
                        var pin = root.GetProperty("pin").GetInt32();
                        string? requestId = null;
                        if (root.TryGetProperty("requestId", out var rProp))
                        {
                            requestId = rProp.GetString();
                        }
                        
                        int physPin = pin;
                        bool isValid = true;
                        
                        if (clientScheme == "Logical")
                        {
                            physPin = engine.ConvertLogicalToPhysical(pin);
                            isValid = physPin >= 0;
                        }
                        else
                        {
                            isValid = engine.IsValidPhysicalPin(pin);
                        }
                        
                        if (!isValid)
                        {
                            if (action == "open" && requestId != null)
                            {
                                var resp = JsonSerializer.Serialize(new {
                                    action = "open_response",
                                    requestId = requestId,
                                    status = "error",
                                    errorType = "ArgumentException",
                                    errorMessage = $"Pin {pin} is not valid for this board layout."
                                });
                                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(resp)), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            continue;
                        }

                        if (action == "open")
                        {
                            var mode = root.GetProperty("mode").GetString() ?? "Input";
                            
                            if (engine.TryOpenPin(physPin, mode, clientId.ToString(), clientType, out var errorType, out var errorMessage))
                            {
                                if (requestId != null)
                                {
                                    var resp = JsonSerializer.Serialize(new { action = "open_response", requestId = requestId, status = "success" });
                                    await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(resp)), WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                                Log($"Physical Pin {physPin} opened in {mode} mode by client {clientId} ({clientType})");
                            }
                            else if (requestId != null)
                            {
                                var resp = JsonSerializer.Serialize(new {
                                    action = "open_response",
                                    requestId = requestId,
                                    status = "error",
                                    errorType = errorType,
                                    errorMessage = errorMessage
                                });
                                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(resp)), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                        }
                        else if (action == "write" || action == "read")
                        {
                            var val = root.GetProperty("value").GetString() ?? "Low";
                            engine.WritePin(physPin, val, clientId.ToString(), clientType);
                        }
                        else if (action == "mode")
                        {
                            var mode = root.GetProperty("mode").GetString() ?? "Input";
                            engine.SetPinMode(physPin, mode);
                        }
                        else if (action == "close")
                        {
                            engine.ClosePin(physPin);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error in client {clientId} loop: {ex.Message}");
            }
            finally
            {
                engine.PinStateChanged -= stateHandler;
                engine.PinClosed -= closeHandler;
                engine.BoardChanged -= boardHandler;

                clients.TryRemove(clientId, out _);
                int activeControllers = clients.Values.Count(c => c.Type == "controller");
                Log($"Client disconnected: {clientId} (Type: {clientType}). Remaining controllers: {activeControllers}");
                if (activeControllers == 0)
                {
                    StartShutdownTimer(3);
                }
            }
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
    else
    {
        await next(context);
    }
});

StartShutdownTimer(15);
app.Run();

public class ClientConnection
{
    public Guid ClientId { get; set; }
    public WebSocket Socket { get; set; } = null!;
    public string Type { get; set; } = "ui";
    public string Scheme { get; set; } = "Board";
}

public partial class Program
{
    private static readonly Regex CacheBustRegex = new Regex(@"\{\{CACHE_BUST_VERSION\}\}", RegexOptions.Compiled);

    private static string ApplyCacheBusting(string html, string version)
    {
        return CacheBustRegex.Replace(html, version);
    }
}
