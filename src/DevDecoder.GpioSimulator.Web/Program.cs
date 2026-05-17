using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

var clients = new ConcurrentDictionary<Guid, WebSocket>();
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
                app.Lifetime.StopApplication();
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
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            clients.TryAdd(clientId, webSocket);
            CancelShutdownTimer();
            
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
                        if (client.Value.State == WebSocketState.Open && client.Value != webSocket)
                        {
                            await client.Value.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
            }
            catch
            {
                // Handle disconnected socket
            }
            finally
            {
                clients.TryRemove(clientId, out _);
                if (clients.IsEmpty)
                {
                    StartShutdownTimer(10); // Shutdown after 10 seconds of idleness
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
