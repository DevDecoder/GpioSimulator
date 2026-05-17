using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

app.UseDefaultFiles();
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".gsc"] = "application/json";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
app.UseWebSockets();

var clients = new ConcurrentDictionary<Guid, ClientConnection>();
var pinStates = new ConcurrentDictionary<int, PinState>();

var mappingLock = new object();
var activeBoardId = "raspberry_pi_5_breakout";
var activePhysToLog = new Dictionary<int, int>();
var activeLogToPhys = new Dictionary<int, int>();

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
            // Fallback for bin/Debug or publish directories
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
        var tempLogToPhys = new Dictionary<int, int>();

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
                    tempLogToPhys[log] = phys;
                }
            }
        }

        lock (mappingLock)
        {
            activeBoardId = boardId;
            activePhysToLog = tempPhysToLog;
            activeLogToPhys = tempLogToPhys;
        }
        Log($"Loaded board mapping for: {boardId} ({tempPhysToLog.Count} pins)");
    }
    catch (Exception ex)
    {
        Log($"Error loading mapping for {boardId}: {ex.Message}");
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

app.MapGet("/api/pins", () => Results.Json(pinStates.ToDictionary(
    kvp => kvp.Key,
    kvp => $"{kvp.Value.Mode}:{kvp.Value.Value}:{kvp.Value.OwnerId?.ToString() ?? ""}:{kvp.Value.OwnerType}"
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

app.MapGet("/api/board/active", () =>
{
    lock (mappingLock)
    {
        return Results.Json(new
        {
            boardId = activeBoardId,
            pins = activePhysToLog
        });
    }
});

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
            
            try
            {
                // Send connection info
                var connMsg = JsonSerializer.Serialize(new { 
                    action = "connected", 
                    clientId = clientId.ToString() 
                });
                var connBytes = Encoding.UTF8.GetBytes(connMsg);
                await webSocket.SendAsync(new ArraySegment<byte>(connBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // Send initial states to freshly opened client
                foreach (var kvp in pinStates)
                {
                    int targetPin = kvp.Key;
                    if (clientScheme == "Logical")
                    {
                        lock (mappingLock)
                        {
                            if (activePhysToLog.TryGetValue(kvp.Key, out int log))
                            {
                                targetPin = log;
                            }
                        }
                    }
                    var initMsg = JsonSerializer.Serialize(new { 
                        action = "state_change", 
                        pin = targetPin, 
                        mode = kvp.Value.Mode, 
                        value = kvp.Value.Value,
                        ownerId = kvp.Value.OwnerId?.ToString() ?? "",
                        ownerType = kvp.Value.OwnerType
                    });
                    var bytes = Encoding.UTF8.GetBytes(initMsg);
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }

                var buffer = new byte[1024 * 4];
                
                async Task BroadcastPinState(int pPin, PinState state)
                {
                    foreach (var c in clients.Values)
                    {
                        if (c.Socket.State == WebSocketState.Open)
                        {
                            int targetPin = pPin;
                            if (c.Scheme == "Logical")
                            {
                                lock (mappingLock)
                                {
                                    if (activePhysToLog.TryGetValue(pPin, out int log))
                                    {
                                        targetPin = log;
                                    }
                                }
                            }
                            var msg = JsonSerializer.Serialize(new {
                                action = "state_change",
                                pin = targetPin,
                                mode = state.Mode,
                                value = state.Value,
                                ownerId = state.OwnerId?.ToString() ?? "",
                                ownerType = state.OwnerType
                            });
                            var bytes = Encoding.UTF8.GetBytes(msg);
                            try
                            {
                                await c.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            catch { }
                        }
                    }
                }

                async Task BroadcastPinClose(int pPin)
                {
                    foreach (var c in clients.Values)
                    {
                        if (c.Socket.State == WebSocketState.Open)
                        {
                            int targetPin = pPin;
                            if (c.Scheme == "Logical")
                            {
                                lock (mappingLock)
                                {
                                    if (activePhysToLog.TryGetValue(pPin, out int log))
                                    {
                                        targetPin = log;
                                    }
                                }
                            }
                            var msg = JsonSerializer.Serialize(new {
                                action = "close",
                                pin = targetPin
                            });
                            var bytes = Encoding.UTF8.GetBytes(msg);
                            try
                            {
                                await c.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            catch { }
                        }
                    }
                }

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    
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
                            lock (mappingLock)
                            {
                                if (activeLogToPhys.TryGetValue(pin, out int phys))
                                {
                                    physPin = phys;
                                }
                                else
                                {
                                    isValid = false;
                                }
                            }
                        }
                        else
                        {
                            lock (mappingLock)
                            {
                                if (!activePhysToLog.ContainsKey(pin))
                                {
                                    isValid = false;
                                }
                            }
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
                                    errorMessage = $"Pin {pin} is not valid for this board or scheme."
                                });
                                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(resp)), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            else
                            {
                                Log($"Rejecting action '{action}' for invalid pin {pin}.");
                            }
                            continue;
                        }

                        if (action == "open")
                        {
                            var mode = root.GetProperty("mode").GetString() ?? "Input";
                            
                            // Check if pin is already open
                            if (pinStates.TryGetValue(physPin, out var existingState))
                            {
                                // If already open by same client, succeed immediately
                                if (existingState.OwnerId == clientId)
                                {
                                    existingState.Mode = mode;
                                    var defaultVal = mode == "InputPullUp" ? "High" : "Low";
                                    existingState.Value = defaultVal;
                                    
                                    if (requestId != null)
                                    {
                                        var resp = JsonSerializer.Serialize(new {
                                            action = "open_response",
                                            requestId = requestId,
                                            status = "success"
                                        });
                                        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(resp)), WebSocketMessageType.Text, true, CancellationToken.None);
                                    }
                                    
                                    await BroadcastPinState(physPin, existingState);
                                    Log($"Physical Pin {physPin} reconfigured to {mode} by its owner {clientId} ({clientType})");
                                    continue;
                                }
                                
                                // Check if owner is active
                                bool ownerActive = existingState.OwnerId.HasValue && clients.ContainsKey(existingState.OwnerId.Value);
                                if (ownerActive)
                                {
                                    if (requestId != null)
                                    {
                                        var resp = JsonSerializer.Serialize(new {
                                            action = "open_response",
                                            requestId = requestId,
                                            status = "error",
                                            errorType = "InvalidOperationException",
                                            errorMessage = $"Pin {physPin} is already opened by another controller ({existingState.OwnerType})."
                                        });
                                        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(resp)), WebSocketMessageType.Text, true, CancellationToken.None);
                                    }
                                    Log($"Rejecting open for pin {physPin}: Owned by active client {existingState.OwnerId} ({existingState.OwnerType})");
                                    continue;
                                }
                            }
                            
                            // Pin is not open, or owner is dead -> Open and claim it!
                            var defaultValStr = mode == "InputPullUp" ? "High" : "Low";
                            var state = new PinState
                            {
                                Mode = mode,
                                Value = defaultValStr,
                                OwnerId = clientId,
                                OwnerType = clientType
                            };
                            pinStates[physPin] = state;
                            
                            if (requestId != null)
                            {
                                var resp = JsonSerializer.Serialize(new {
                                    action = "open_response",
                                    requestId = requestId,
                                    status = "success"
                                });
                                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(resp)), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            
                            await BroadcastPinState(physPin, state);
                            Log($"Physical Pin {physPin} opened in {mode} mode by client {clientId} ({clientType})");
                        }
                        else if (action == "write" || action == "read")
                        {
                            var val = root.GetProperty("value").GetString() ?? "Low";
                            
                            if (pinStates.TryGetValue(physPin, out var state))
                            {
                                if (state.OwnerId != clientId)
                                {
                                    Log($"Unauthorized write/read for pin {physPin} by client {clientId} ({clientType}). Owned by {state.OwnerId} ({state.OwnerType})");
                                    continue;
                                }
                                state.Value = val;
                                Log($"Physical Pin {physPin} state set to: {val} by owner {clientId}");
                                await BroadcastPinState(physPin, state);
                            }
                            else
                            {
                                Log($"Write/read ignored: Pin {physPin} is not open.");
                            }
                        }
                        else if (action == "mode")
                        {
                            var mode = root.GetProperty("mode").GetString() ?? "Input";
                            var val = mode == "InputPullUp" ? "High" : "Low";
                            
                            if (pinStates.TryGetValue(physPin, out var state))
                            {
                                if (state.OwnerId != clientId)
                                {
                                    Log($"Unauthorized mode change for pin {physPin} by client {clientId} ({clientType}). Owned by {state.OwnerId} ({state.OwnerType})");
                                    continue;
                                }
                                state.Mode = mode;
                                state.Value = val;
                                Log($"Physical Pin {physPin} mode configured to: {mode} by owner {clientId}");
                                await BroadcastPinState(physPin, state);
                            }
                            else
                            {
                                Log($"Mode change ignored: Pin {physPin} is not open.");
                            }
                        }
                        else if (action == "close")
                        {
                            if (pinStates.TryGetValue(physPin, out var state))
                            {
                                if (state.OwnerId != clientId)
                                {
                                    Log($"Unauthorized close for pin {physPin} by client {clientId} ({clientType}). Owned by {state.OwnerId} ({state.OwnerType})");
                                    continue;
                                }
                                pinStates.TryRemove(physPin, out _);
                                Log($"Physical Pin {physPin} closed by owner {clientId}");
                                await BroadcastPinClose(physPin);
                            }
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
                clients.TryRemove(clientId, out _);
                int activeControllers = clients.Values.Count(c => c.Type == "controller");
                Log($"Client disconnected: {clientId} (Type: {clientType}). Remaining controllers: {activeControllers}");
                if (activeControllers == 0)
                {
                    StartShutdownTimer(3); // Shutdown after 3 seconds of no active controller
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

// Start a 15-second grace period for the first client to connect upon booting
StartShutdownTimer(15);

app.Run();

public class ClientConnection
{
    public Guid ClientId { get; set; }
    public WebSocket Socket { get; set; } = null!;
    public string Type { get; set; } = "ui";
    public string Scheme { get; set; } = "Board";
}

public class PinState
{
    public string Mode { get; set; } = "None";
    public string Value { get; set; } = "Low";
    public Guid? OwnerId { get; set; }
    public string OwnerType { get; set; } = "";
}
