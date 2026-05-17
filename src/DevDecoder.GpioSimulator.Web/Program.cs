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

app.MapGet("/api/pins", () => Results.Json(pinStates));

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
                            
                            // Map incoming pin to a physical pin (Board numbering scheme)
                            int physPin = origPin;
                            if (clientScheme == "Logical")
                            {
                                lock (mappingLock)
                                {
                                    if (activeLogToPhys.TryGetValue(origPin, out int phys))
                                    {
                                        physPin = phys;
                                    }
                                }
                            }
                            
                            // Map physical pin to destination scheme
                            int targetPin = physPin;
                            if (destScheme == "Logical")
                            {
                                lock (mappingLock)
                                {
                                    if (activePhysToLog.TryGetValue(physPin, out int log))
                                    {
                                        targetPin = log;
                                    }
                                }
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
                        
                        int physPin = pin;
                        if (clientScheme == "Logical")
                        {
                            lock (mappingLock)
                            {
                                if (activeLogToPhys.TryGetValue(pin, out int phys))
                                {
                                    physPin = phys;
                                }
                            }
                        }

                        if (action == "log")
                        {
                            var val = root.GetProperty("value").GetString() ?? "";
                            Log(val);
                        }
                        else if (action == "write" || action == "read")
                        {
                            var val = root.GetProperty("value").GetString() ?? "Low";
                            pinStates.AddOrUpdate(physPin, $"Unknown:{val}", (k, old) => $"{old.Split(':')[0]}:{val}");
                            Log($"Physical Pin {physPin} state set to: {val}");
                        }
                        else if (action == "mode")
                        {
                            var mode = root.GetProperty("mode").GetString() ?? "Input";
                            var val = mode == "InputPullUp" ? "High" : "Low";
                            pinStates.AddOrUpdate(physPin, $"{mode}:{val}", (k, old) => $"{mode}:{val}");
                            Log($"Physical Pin {physPin} mode configured: {mode} (Default value: {val})");

                            // Respond back to the sender socket with the updated pin value
                            var responseMsg = JsonSerializer.Serialize(new { action = "write", pin = pin, value = val });
                            var responseBytes = Encoding.UTF8.GetBytes(responseMsg);
                            await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                            // Also broadcast the value change to all other connected clients
                            foreach (var client in clients)
                            {
                                if (client.Value.Socket.State == WebSocketState.Open && client.Value.Socket != webSocket)
                                {
                                    int targetPin = physPin;
                                    if (client.Value.Scheme == "Logical")
                                    {
                                        lock (mappingLock)
                                        {
                                            if (activePhysToLog.TryGetValue(physPin, out int log))
                                            {
                                                targetPin = log;
                                            }
                                        }
                                    }
                                    var broadcastMsg = JsonSerializer.Serialize(new { action = "write", pin = targetPin, value = val });
                                    var broadcastBytes = Encoding.UTF8.GetBytes(broadcastMsg);
                                    await client.Value.Socket.SendAsync(new ArraySegment<byte>(broadcastBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                            }
                        }
                        else if (action == "close")
                        {
                            pinStates.TryRemove(physPin, out _);
                            Log($"Physical Pin {physPin} closed");
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
