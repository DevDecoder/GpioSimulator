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

var clients = new ConcurrentDictionary<Guid, (WebSocket Socket, string Type, string Scheme)>();
var pinStates = new ConcurrentDictionary<int, string>(); // Stores mode:value

var mappingLock = new object();
var activeBoardId = "raspberry_pi_5";
var activePhysToLog = new Dictionary<int, int>();
var activeLogToPhys = new Dictionary<int, int>();

void LoadBoardMapping(string boardId)
{
    try
    {
        string gscPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "components", $"{boardId}.gsc");
        if (!File.Exists(gscPath))
        {
            Console.WriteLine($"[Pi Simulator Web] GSC file not found: {gscPath}");
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
        Console.WriteLine($"[Pi Simulator Web] Loaded board mapping for: {boardId} ({tempPhysToLog.Count} pins)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Pi Simulator Web] Error loading mapping for {boardId}: {ex.Message}");
    }
}

LoadBoardMapping("raspberry_pi_5");

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
                Console.WriteLine("[Pi Simulator Web] No active connections. Shutting down server...");
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

app.MapGet("/api/pins", () => Results.Json(pinStates));

app.MapPost("/api/board/active", (HttpContext context) =>
{
    var boardId = context.Request.Query["boardId"].ToString();
    if (string.IsNullOrEmpty(boardId))
    {
        return Results.BadRequest("Missing boardId parameter");
    }
    Console.WriteLine($"[Pi Simulator Web] Setting active board layout to: {boardId}");
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
            if (string.IsNullOrEmpty(clientScheme)) clientScheme = "Logical";

            Console.WriteLine($"[Pi Simulator Web] Client connected: {clientId} (Type: {clientType}, Scheme: {clientScheme})");

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            clients.TryAdd(clientId, (webSocket, clientType, clientScheme));

            if (clientType == "controller")
            {
                CancelShutdownTimer();
            }
            
            try
            {
                // Send initial states to freshly opened client
                foreach (var kvp in pinStates)
                {
                    int targetPin = kvp.Key;
                    if (clientType == "controller" && clientScheme == "Board")
                    {
                        lock (mappingLock)
                        {
                            if (activeLogToPhys.TryGetValue(kvp.Key, out int phys))
                            {
                                targetPin = phys;
                            }
                        }
                    }
                    var parts = kvp.Value.Split(':');
                    var initMsg = JsonSerializer.Serialize(new { action = "state_change", pin = targetPin, mode = parts[0], value = parts[1] });
                    var bytes = Encoding.UTF8.GetBytes(initMsg);
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }

                var buffer = new byte[1024 * 4];
                
                string SerializeMessageForClient(string rawJson, string destType, string destScheme)
                {
                    try
                    {
                        var node = JsonNode.Parse(rawJson);
                        if (node != null && node["pin"] != null)
                        {
                            int origPin = node["pin"]!.GetValue<int>();
                            int targetPin = origPin;
                            
                            int logPin = origPin;
                            if (clientType == "controller" && clientScheme == "Board")
                            {
                                lock (mappingLock)
                                {
                                    if (activePhysToLog.TryGetValue(origPin, out int log))
                                    {
                                        logPin = log;
                                    }
                                }
                            }
                            
                            if (destType == "controller" && destScheme == "Board")
                            {
                                lock (mappingLock)
                                {
                                    if (activeLogToPhys.TryGetValue(logPin, out int phys))
                                    {
                                        targetPin = phys;
                                    }
                                }
                            }
                            else
                            {
                                targetPin = logPin;
                            }
                            
                            node["pin"] = targetPin;
                            return node.ToJsonString();
                        }
                    }
                    catch
                    {
                        // Fallback to original JSON if parsing/processing fails
                    }
                    return rawJson;
                }

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var json = JsonDocument.Parse(message);
                    var root = json.RootElement;
                    if (root.TryGetProperty("action", out var actionProp))
                    {
                        var action = actionProp.GetString();
                        var pin = root.GetProperty("pin").GetInt32();
                        
                        int logicalPin = pin;
                        if (clientType == "controller" && clientScheme == "Board")
                        {
                            lock (mappingLock)
                            {
                                if (activePhysToLog.TryGetValue(pin, out int log))
                                {
                                    logicalPin = log;
                                }
                            }
                        }

                        if (action == "write" || action == "read")
                        {
                            var val = root.GetProperty("value").GetString() ?? "Low";
                            pinStates.AddOrUpdate(logicalPin, $"Unknown:{val}", (k, old) => $"{old.Split(':')[0]}:{val}");
                        }
                        else if (action == "mode")
                        {
                            var mode = root.GetProperty("mode").GetString() ?? "Input";
                            pinStates.AddOrUpdate(logicalPin, $"{mode}:Low", (k, old) => $"{mode}:{old.Split(':')[1]}");
                        }
                    }

                    // Broadcast message to all other connected sockets
                    foreach (var client in clients)
                    {
                        if (client.Value.Socket.State == WebSocketState.Open && client.Value.Socket != webSocket)
                        {
                            string translatedMsg = SerializeMessageForClient(message, client.Value.Type, client.Value.Scheme);
                            var transBytes = Encoding.UTF8.GetBytes(translatedMsg);
                            await client.Value.Socket.SendAsync(new ArraySegment<byte>(transBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Pi Simulator Web] Error in client {clientId} loop: {ex.Message}");
            }
            finally
            {
                clients.TryRemove(clientId, out _);
                int activeControllers = clients.Values.Count(c => c.Type == "controller");
                Console.WriteLine($"[Pi Simulator Web] Client disconnected: {clientId} (Type: {clientType}). Remaining controllers: {activeControllers}");
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
