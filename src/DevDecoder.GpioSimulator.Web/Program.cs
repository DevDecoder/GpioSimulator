using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

var clients = new ConcurrentBag<WebSocket>();
var pinStates = new ConcurrentDictionary<int, string>(); // Stores mode:value

app.MapGet("/api/status", () => Results.Json(new { status = "online" }));

app.MapGet("/api/pins", () => Results.Json(pinStates));

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            clients.Add(webSocket);
            
            // Send initial states to freshly opened client
            foreach (var kvp in pinStates)
            {
                var parts = kvp.Value.Split(':');
                var initMsg = JsonSerializer.Serialize(new { action = "state_change", pin = kvp.Key, mode = parts[0], value = parts[1] });
                var bytes = Encoding.UTF8.GetBytes(initMsg);
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            var buffer = new byte[1024 * 4];
            try
            {
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
                        if (client.State == WebSocketState.Open && client != webSocket)
                        {
                            await client.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
            }
            catch
            {
                // Handle disconnected socket
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

app.Run();
