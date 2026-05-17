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
    public class WebAPIDriver : SimulatorDriverBase
    {
        private ClientWebSocket? _wsClient;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private static readonly HttpClient _httpClient = new HttpClient();
        
        private readonly ConcurrentDictionary<int, int> _activePhysToLog = new ConcurrentDictionary<int, int>();
        private readonly ConcurrentDictionary<int, int> _activeLogToPhys = new ConcurrentDictionary<int, int>();
        private readonly object _mappingLock = new object();
        private readonly ManualResetEventSlim _initialMappingReceived = new ManualResetEventSlim(false);

        private class OpenPinResult
        {
            public bool Success { get; set; }
            public string? ErrorType { get; set; }
            public string? ErrorMessage { get; set; }
        }

        private readonly ConcurrentDictionary<string, TaskCompletionSource<OpenPinResult>> _pendingRequests = 
            new ConcurrentDictionary<string, TaskCompletionSource<OpenPinResult>>();

        public WebAPIDriver(PinNumberingScheme numberingScheme = PinNumberingScheme.Logical) : base(numberingScheme)
        {
            EnsureServerStartedAndConnected().GetAwaiter().GetResult();
        }

        protected override int ConvertPin(int pinNumber)
        {
            lock (_mappingLock)
            {
                if (NumberingScheme == PinNumberingScheme.Logical)
                {
                    if (!_activeLogToPhys.TryGetValue(pinNumber, out int phys))
                        throw new ArgumentException($"Pin {pinNumber} is not a valid logical pin on the current board mapping.");
                    return phys;
                }
                else
                {
                    if (!_activePhysToLog.ContainsKey(pinNumber))
                        throw new ArgumentException($"Pin {pinNumber} is not a valid physical pin on the current board mapping.");
                    return pinNumber;
                }
            }
        }

        private void Log(string level, string message)
        {
            string formatted = $"[Driver] [{level}] {message}";
            if (_wsClient != null && _wsClient.State == WebSocketState.Open)
            {
                try
                {
                    string escapedMessage = formatted.Replace("\"", "\\\"");
                    var payload = $"{{\"action\":\"log\",\"pin\":0,\"value\":\"{escapedMessage}\",\"mode\":\"\"}}";
                    var bytes = Encoding.UTF8.GetBytes(payload);
                    _wsClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                             .GetAwaiter().GetResult();
                    return;
                }
                catch { }
            }
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss} GPIO Simulator] {formatted}");
        }

        private static void ReadStream(StreamReader reader)
        {
            try
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line != null)
                    {
                        System.Diagnostics.Debug.WriteLine(line);
                    }
                }
            }
            catch { }
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
            catch { }

            if (!serverActive)
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (TryFindDevWebDll(baseDir, out string dllPath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"--roll-forward Major \"{dllPath}\" --urls {serverUrl}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(dllPath),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
                    
                    var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        _ = Task.Run(() => ReadStream(process.StandardOutput));
                        _ = Task.Run(() => ReadStream(process.StandardError));
                    }
                    
                    await Task.Delay(2000);
                }
                
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                {
                    BrowserLauncher.Open(serverUrl);
                }
            }

            try
            {
                _wsClient = new ClientWebSocket();
                using (var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    await _wsClient.ConnectAsync(new Uri($"ws://127.0.0.1:5050/ws?client=controller&scheme={NumberingScheme}"), connectCts.Token);
                }
                
                _ = Task.Run(ReceiveWebSocketMessages);

                if (!_initialMappingReceived.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new InvalidOperationException("Failed to receive initial board mapping from simulator server within the timeout period.");
                }
            }
            catch (Exception ex)
            {
                _wsClient?.Dispose();
                _wsClient = null;
                throw new InvalidOperationException("Failed to connect to the simulator server over WebSocket.", ex);
            }
        }

        private static string? GetJsonValue(string msg, string key)
        {
            int index = msg.IndexOf($"\"{key}\":");
            if (index == -1) return null;
            index += key.Length + 3;
            
            while (index < msg.Length && (msg[index] == ' ' || msg[index] == '"'))
            {
                index++;
            }
            
            int end = index;
            while (end < msg.Length && msg[end] != '"' && msg[end] != ',' && msg[end] != '}' && msg[end] != '\r' && msg[end] != '\n')
            {
                end++;
            }
            
            if (end > index)
            {
                return msg.Substring(index, end - index).Trim();
            }
            return null;
        }

        private async Task ReceiveWebSocketMessages()
        {
            var buffer = new byte[1024 * 4];
            try
            {
                while (_wsClient != null && _wsClient.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _wsClient.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    if (msg.Contains("\"action\":\"open_response\""))
                    {
                        var requestId = GetJsonValue(msg, "requestId");
                        var status = GetJsonValue(msg, "status");
                        var errorType = GetJsonValue(msg, "errorType");
                        var errorMessage = GetJsonValue(msg, "errorMessage");
                        
                        if (requestId != null && _pendingRequests.TryGetValue(requestId, out var tcs))
                        {
                            tcs.TrySetResult(new OpenPinResult
                            {
                                Success = status == "success",
                                ErrorType = errorType,
                                ErrorMessage = errorMessage
                            });
                        }
                    }
                    else if (msg.Contains("\"action\":\"state_change\"") || msg.Contains("\"action\":\"write\""))
                    {
                        var pinStr = GetJsonValue(msg, "pin");
                        var valStr = GetJsonValue(msg, "value");
                        
                        if (int.TryParse(pinStr, out int pin) && valStr != null)
                        {
                            PinValue val = valStr == "High" ? PinValue.High : PinValue.Low;
                            
                            PinValue oldVal = PinValues.GetOrAdd(pin, PinValue.Low);
                            if (oldVal != val)
                            {
                                UpdatePinValue(pin, val);
                                PinEventTypes occurredType = val == PinValue.High ? PinEventTypes.Rising : PinEventTypes.Falling;
                                FireCallbacks(pin, occurredType);
                            }
                        }
                    }
                    else if (msg.Contains("\"action\":\"close\""))
                    {
                        var pinStr = GetJsonValue(msg, "pin");
                        if (int.TryParse(pinStr, out int pin))
                        {
                            ClearPinCache(pin);
                        }
                    }
                    else if (msg.Contains("\"action\":\"board_change\""))
                    {
                        int pinsIndex = msg.IndexOf("\"pins\":");
                        if (pinsIndex != -1)
                        {
                            int startIndex = msg.IndexOf('{', pinsIndex);
                            int endIndex = msg.IndexOf('}', startIndex);
                            if (startIndex != -1 && endIndex != -1)
                            {
                                var pinsContent = msg.Substring(startIndex + 1, endIndex - startIndex - 1);
                                var pairs = pinsContent.Split(',');
                                var tempPhysToLog = new System.Collections.Generic.Dictionary<int, int>();
                                var tempLogToPhys = new System.Collections.Generic.Dictionary<int, int>();
                                foreach (var pair in pairs)
                                {
                                    var parts = pair.Split(':');
                                    if (parts.Length == 2)
                                    {
                                        var physStr = parts[0].Trim('"', ' ', '\r', '\n');
                                        var logStr = parts[1].Trim('"', ' ', '\r', '\n');
                                        if (int.TryParse(physStr, out int phys) && int.TryParse(logStr, out int log))
                                        {
                                            tempPhysToLog[phys] = log;
                                            tempLogToPhys[log] = phys;
                                        }
                                    }
                                }
                                lock (_mappingLock)
                                {
                                    _activePhysToLog.Clear();
                                    _activeLogToPhys.Clear();
                                    foreach (var kvp in tempPhysToLog) _activePhysToLog[kvp.Key] = kvp.Value;
                                    foreach (var kvp in tempLogToPhys) _activeLogToPhys[kvp.Key] = kvp.Value;
                                }
                                _initialMappingReceived.Set();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        protected override void OpenPinInternal(int pinNumber, PinMode mode)
        {
            if (_wsClient != null && _wsClient.State == WebSocketState.Open)
            {
                var requestId = Guid.NewGuid().ToString();
                var tcs = new TaskCompletionSource<OpenPinResult>();
                _pendingRequests[requestId] = tcs;
                
                var payload = $"{{\"action\":\"open\",\"pin\":{pinNumber},\"mode\":\"{mode}\",\"requestId\":\"{requestId}\"}}";
                var bytes = Encoding.UTF8.GetBytes(payload);
                _wsClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();
                
                if (!tcs.Task.Wait(TimeSpan.FromSeconds(5)))
                {
                    _pendingRequests.TryRemove(requestId, out _);
                    throw new TimeoutException($"Timeout waiting for simulator server to open pin {pinNumber}.");
                }
                
                _pendingRequests.TryRemove(requestId, out _);
                
                var result = tcs.Task.Result;
                if (!result.Success)
                {
                    if (result.ErrorType == "ArgumentException") throw new ArgumentException(result.ErrorMessage);
                    if (result.ErrorType == "InvalidOperationException") throw new InvalidOperationException(result.ErrorMessage);
                    throw new Exception(result.ErrorMessage);
                }
            }
            else
            {
                throw new InvalidOperationException("Failed to open pin: The driver is not connected to the simulator server.");
            }
        }

        protected override void ClosePinInternal(int pinNumber)
        {
            SendPinMessage(pinNumber, "close", "");
        }

        protected override void WriteInternal(int pinNumber, PinValue value)
        {
            SendPinMessage(pinNumber, "write", value.ToString());
        }

        protected override PinValue ReadInternal(int pinNumber)
        {
            SendPinMessage(pinNumber, "read", "");
            return PinValues.TryGetValue(pinNumber, out var val) ? val : PinValue.Low;
        }

        protected override void SetPinModeInternal(int pinNumber, PinMode mode)
        {
            SendPinMessage(pinNumber, "mode", mode.ToString());
        }

        protected override PinMode GetPinModeInternal(int pinNumber)
        {
            return OpenPins.TryGetValue(pinNumber, out var mode) ? mode : PinMode.Input;
        }

        private void SendPinMessage(int pinNumber, string action, string data)
        {
            if (_wsClient != null && _wsClient.State == WebSocketState.Open)
            {
                var payload = $"{{\"action\":\"{action}\",\"pin\":{pinNumber},\"value\":\"{data}\",\"mode\":\"{data}\"}}";
                var bytes = Encoding.UTF8.GetBytes(payload);
                _wsClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        private static bool TryFindDevWebDll(string baseDir, out string dllPath)
        {
            dllPath = Path.Combine(baseDir, "simulator", "DevDecoder.GpioSimulator.Web.dll");
            if (File.Exists(dllPath)) return true;

            dllPath = null!;
            var dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                var webAppDir = Path.Combine(dir.FullName, "src", "DevDecoder.GpioSimulator.Web");
                if (!Directory.Exists(webAppDir)) webAppDir = Path.Combine(dir.FullName, "DevDecoder.GpioSimulator.Web");

                if (Directory.Exists(webAppDir))
                {
                    var binPath = Path.Combine(webAppDir, "bin");
                    if (Directory.Exists(binPath))
                    {
                        var configurations = new[] { "Debug", "Release" };
                        foreach (var config in configurations)
                        {
                            var configPath = Path.Combine(binPath, config);
                            if (Directory.Exists(configPath))
                            {
                                foreach (var frameworkDir in Directory.GetDirectories(configPath, "net*"))
                                {
                                    var targetDll = Path.Combine(frameworkDir, "DevDecoder.GpioSimulator.Web.dll");
                                    if (File.Exists(targetDll))
                                    {
                                        dllPath = targetDll;
                                        return true;
                                    }
                                }
                            }
                        }
                        try
                        {
                            var files = Directory.GetFiles(binPath, "DevDecoder.GpioSimulator.Web.dll", SearchOption.AllDirectories);
                            if (files.Length > 0)
                            {
                                dllPath = files[0];
                                return true;
                            }
                        }
                        catch { }
                    }
                }
                dir = dir.Parent;
            }
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _wsClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
