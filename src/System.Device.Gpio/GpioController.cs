using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Device.Gpio
{
    public class GpioController : IDisposable
    {
        private readonly ConcurrentDictionary<int, PinMode> _openPins = new ConcurrentDictionary<int, PinMode>();
        private readonly ConcurrentDictionary<int, PinValue> _pinValues = new ConcurrentDictionary<int, PinValue>();
        
        private ClientWebSocket _wsClient;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private static readonly HttpClient _httpClient = new HttpClient();

        public GpioController()
        {
            EnsureServerStartedAndConnected().GetAwaiter().GetResult();
        }

        private async Task EnsureServerStartedAndConnected()
        {
            string serverUrl = "http://127.0.0.1:5050";
            bool serverActive = false;

            try
            {
                var response = await _httpClient.GetAsync($"{serverUrl}/api/status");
                serverActive = response.IsSuccessStatusCode;
            }
            catch
            {
                // Offline
            }

            if (!serverActive)
            {
                // Try to find the Web App DLL relative to build directory
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dllPath = Path.Combine(baseDir, "simulator", "DevDecoder.GpioSimulator.Web.dll");
                
                if (!File.Exists(dllPath))
                {
                    // Fallback searching up for dev execution
                    dllPath = Path.Combine(baseDir, "..", "..", "..", "..", "DevDecoder.GpioSimulator.Web", "bin", "Debug", "net8.0", "DevDecoder.GpioSimulator.Web.dll");
                }

                if (File.Exists(dllPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"\"{dllPath}\" --urls {serverUrl}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    
                    // Allow server spin up time
                    await Task.Delay(2000);
                }
                
                BrowserLauncher.Open(serverUrl);
            }

            // Establish WebSocket client connection
            try
            {
                _wsClient = new ClientWebSocket();
                await _wsClient.ConnectAsync(new Uri("ws://127.0.0.1:5050/ws"), CancellationToken.None);
                
                // Start background listener
                _ = Task.Run(ReceiveWebSocketMessages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Pi Simulator] Client failed to connect to WebSocket: {ex.Message}");
            }
        }

        private async Task ReceiveWebSocketMessages()
        {
            var buffer = new byte[1024 * 4];
            try
            {
                while (_wsClient.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _wsClient.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    // Manual JSON Parsing to avoid any library dependency on .NET Standard 2.0
                    if (msg.Contains("\"action\":\"read\""))
                    {
                        int pinIndex = msg.IndexOf("\"pin\":") + 6;
                        int commaIndex = msg.IndexOf(",", pinIndex);
                        if (commaIndex > pinIndex && int.TryParse(msg.Substring(pinIndex, commaIndex - pinIndex).Trim(), out int pin))
                        {
                            int valIndex = msg.IndexOf("\"value\":\"") + 9;
                            int endValIndex = msg.IndexOf("\"", valIndex);
                            if (endValIndex > valIndex)
                            {
                                string valStr = msg.Substring(valIndex, endValIndex - valIndex);
                                PinValue val = valStr == "High" ? PinValue.High : PinValue.Low;
                                _pinValues[pin] = val;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently absorb socket closing
            }
        }

        public void OpenPin(int pinNumber, PinMode mode)
        {
            _openPins[pinNumber] = mode;
            _pinValues[pinNumber] = PinValue.Low;
            NotifyPinChange(pinNumber, "mode", mode.ToString());
        }

        public void ClosePin(int pinNumber)
        {
            _openPins.TryRemove(pinNumber, out _);
            _pinValues.TryRemove(pinNumber, out _);
        }

        public void Write(int pinNumber, PinValue value)
        {
            if (!_openPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");
            _pinValues[pinNumber] = value;
            NotifyPinChange(pinNumber, "write", value.ToString());
        }

        public PinValue Read(int pinNumber)
        {
            if (!_openPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");
            return _pinValues.TryGetValue(pinNumber, out var val) ? val : PinValue.Low;
        }

        public void SetPinMode(int pinNumber, PinMode mode)
        {
            if (!_openPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");
            _openPins[pinNumber] = mode;
            NotifyPinChange(pinNumber, "mode", mode.ToString());
        }

        private void NotifyPinChange(int pin, string action, string data)
        {
            if (_wsClient != null && _wsClient.State == WebSocketState.Open)
            {
                // Manual JSON building to avoid any library dependency on .NET Standard 2.0
                var payload = $"{{\"action\":\"{action}\",\"pin\":{pin},\"value\":\"{data}\",\"mode\":\"{data}\"}}";
                var bytes = Encoding.UTF8.GetBytes(payload);
                
                // Run synchronously as GpioController writes are synchronous
                _wsClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                         .GetAwaiter().GetResult();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _wsClient?.Dispose();
            _openPins.Clear();
            _pinValues.Clear();
        }
    }
}
