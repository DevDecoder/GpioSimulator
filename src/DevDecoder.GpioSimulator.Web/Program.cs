using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".gsc"] = "application/json";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
app.UseWebSockets();

var clients = new ConcurrentDictionary<Guid, (WebSocket Socket, string Type)>();
var pinStates = new ConcurrentDictionary<int, string>(); // Stores mode:value

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

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var clientId = Guid.NewGuid();
            var clientType = context.Request.Query["client"].ToString();
            if (string.IsNullOrEmpty(clientType)) clientType = "ui";

            Console.WriteLine($"[Pi Simulator Web] Client connected: {clientId} (Type: {clientType})");

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            clients.TryAdd(clientId, (webSocket, clientType));

            if (clientType == "controller")
            {
                CancelShutdownTimer();
            }
            
            try
            {
                // Send initial states to freshly opened client
                foreach (var kvp in pinStates)
                {
                    var parts = kvp.Value.Split(':');
                    var initMsg = JsonSerializer.Serialize(new { action = "state_change", pin = kvp.Key, mode = parts[0], value = parts[1] });
                    var bytes = Encoding.UTF8.GetBytes(initMsg);
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }

                var buffer = new byte[1024 * 4];
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    // Message from UI or App Client -> Broadcast to other clients
                    var json = JsonDocument.Parse(message);
                    var root = json.RootElement;
                    if (root.TryGetProperty("action", out var actionProp))
                    {
                        var action = actionProp.GetString();
                        var pin = root.GetProperty("pin").GetInt32();
                        
                        if (action == "write" || action == "read")
                        {
                            var val = root.GetProperty("value").GetString() ?? "Low";
                            pinStates.AddOrUpdate(pin, $"Unknown:{val}", (k, old) => $"{old.Split(':')[0]}:{val}");
                        }
                        else if (action == "mode")
                        {
                            var mode = root.GetProperty("mode").GetString() ?? "Input";
                            pinStates.AddOrUpdate(pin, $"{mode}:Low", (k, old) => $"{mode}:{old.Split(':')[1]}");
                        }
                    }

                    // Broadcast message to all other connected sockets
                    foreach (var client in clients)
                    {
                        if (client.Value.Socket.State == WebSocketState.Open && client.Value.Socket != webSocket)
                        {
                            await client.Value.Socket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), WebSocketMessageType.Text, true, CancellationToken.None);
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
