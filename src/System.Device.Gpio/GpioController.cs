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
        private readonly ConcurrentDictionary<int, ConcurrentBag<(PinEventTypes Types, PinChangeEventHandler Handler)>> _pinCallbacks = 
            new ConcurrentDictionary<int, ConcurrentBag<(PinEventTypes Types, PinChangeEventHandler Handler)>>();
        
        private class OpenPinResult
        {
            public bool Success { get; set; }
            public string ErrorType { get; set; }
            public string ErrorMessage { get; set; }
        }

        private readonly ConcurrentDictionary<string, TaskCompletionSource<OpenPinResult>> _pendingRequests = 
            new ConcurrentDictionary<string, TaskCompletionSource<OpenPinResult>>();

        private ClientWebSocket _wsClient;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private static readonly HttpClient _httpClient = new HttpClient();

        private void Log(string level, string message)
        {
            string formatted = $"[Controller] [{level}] {message}";
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
                catch
                {
                    // Fallback
                }
            }

            // Fallback: Write directly to console standard error/out
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
            catch
            {
                // Process exited or stream closed
            }
        }

        public virtual PinNumberingScheme NumberingScheme { get; }
        public virtual int PinCount => 40;

        public GpioController() : this(PinNumberingScheme.Logical)
        {
        }

        public GpioController(PinNumberingScheme numberingScheme)
        {
            NumberingScheme = numberingScheme;
            EnsureServerStartedAndConnected().GetAwaiter().GetResult();
        }

        public bool IsValidPin(int pinNumber)
        {
            if (NumberingScheme == PinNumberingScheme.Logical)
            {
                return pinNumber >= 0 && pinNumber <= 27;
            }
            else
            {
                var validPhysPins = new System.Collections.Generic.HashSet<int> 
                { 
                    3, 5, 7, 8, 10, 11, 12, 13, 15, 16, 18, 19, 21, 22, 23, 24, 26, 27, 28, 29, 31, 32, 33, 35, 36, 37, 38, 40 
                };
                return validPhysPins.Contains(pinNumber);
            }
        }

        private void ValidatePin(int pinNumber)
        {
            if (!IsValidPin(pinNumber))
            {
                throw new ArgumentException($"Pin {pinNumber} is not a valid GPIO pin under scheme {NumberingScheme}.");
            }
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
                // Try to find the Web App DLL
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
                    
                    // Allow server spin up time
                    await Task.Delay(2000);
                }
                
                // Only open the browser if not running in a CI environment (headless cloud run)
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                {
                    BrowserLauncher.Open(serverUrl);
                }
            }

            // Establish WebSocket client connection
            try
            {
                _wsClient = new ClientWebSocket();
                
                // Connection attempt with 5-second timeout
                using (var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    await _wsClient.ConnectAsync(new Uri($"ws://127.0.0.1:5050/ws?client=controller&scheme={NumberingScheme}"), connectCts.Token);
                }
                
                // Start background listener
                _ = Task.Run(ReceiveWebSocketMessages);
            }
            catch (Exception ex)
            {
                _wsClient?.Dispose();
                _wsClient = null;
                throw new InvalidOperationException("Failed to connect to the simulator server driver over WebSocket.", ex);
            }
        }

        private static string GetJsonValue(string msg, string key)
        {
            int index = msg.IndexOf($"\"{key}\":");
            if (index == -1) return null;
            index += key.Length + 3; // skip "key":
            
            // Trim leading space/quotes
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
                while (_wsClient.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
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
                            
                            PinValue oldVal = _pinValues.GetOrAdd(pin, PinValue.Low);
                            if (oldVal != val)
                            {
                                _pinValues[pin] = val;
                                
                                // Trigger callbacks on edge transition
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
                            _openPins.TryRemove(pin, out _);
                            _pinValues.TryRemove(pin, out _);
                        }
                    }
                }
            }
            catch
            {
                // Silently absorb socket closing
            }
        }

        private void FireCallbacks(int pinNumber, PinEventTypes occurredType)
        {
            if (_pinCallbacks.TryGetValue(pinNumber, out var list))
            {
                foreach (var item in list)
                {
                    if ((item.Types & occurredType) != PinEventTypes.None)
                    {
                        try
                        {
                            // Invoke callback asynchronously on worker thread pool to prevent blocking receive loop
                            Task.Run(() => item.Handler(this, new PinValueChangedEventArgs(occurredType, pinNumber)));
                        }
                        catch
                        {
                            // Absorb subscriber exceptions to keep receive loop stable
                        }
                    }
                }
            }
        }

        private void NotifyPinOpen(int pin, PinMode mode, string requestId)
        {
            if (_wsClient != null && _wsClient.State == WebSocketState.Open)
            {
                var payload = $"{{\"action\":\"open\",\"pin\":{pin},\"mode\":\"{mode}\",\"requestId\":\"{requestId}\"}}";
                var bytes = Encoding.UTF8.GetBytes(payload);
                _wsClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                         .GetAwaiter().GetResult();
            }
        }

        public virtual void OpenPin(int pinNumber)
        {
            OpenPin(pinNumber, PinMode.Input);
        }

        public virtual void OpenPin(int pinNumber, PinMode mode)
        {
            ValidatePin(pinNumber);
            
            if (_wsClient != null && _wsClient.State == WebSocketState.Open)
            {
                var requestId = Guid.NewGuid().ToString();
                var tcs = new TaskCompletionSource<OpenPinResult>();
                _pendingRequests[requestId] = tcs;
                
                NotifyPinOpen(pinNumber, mode, requestId);
                
                if (!tcs.Task.Wait(TimeSpan.FromSeconds(5)))
                {
                    _pendingRequests.TryRemove(requestId, out _);
                    throw new TimeoutException($"Timeout waiting for simulator server to open pin {pinNumber}.");
                }
                
                _pendingRequests.TryRemove(requestId, out _);
                
                var result = tcs.Task.Result;
                if (!result.Success)
                {
                    if (result.ErrorType == "ArgumentException")
                    {
                        throw new ArgumentException(result.ErrorMessage);
                    }
                    else if (result.ErrorType == "InvalidOperationException")
                    {
                        throw new InvalidOperationException(result.ErrorMessage);
                    }
                    else
                    {
                        throw new Exception(result.ErrorMessage);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Failed to open pin: The GpioController is not connected to the simulator driver.");
            }

            _openPins[pinNumber] = mode;
            var defaultVal = mode == PinMode.InputPullUp ? PinValue.High : PinValue.Low;
            _pinValues[pinNumber] = defaultVal;
        }

        public virtual void OpenPin(int pinNumber, PinMode mode, PinValue initialValue)
        {
            OpenPin(pinNumber, mode);
            Write(pinNumber, initialValue);
        }

        public virtual void ClosePin(int pinNumber)
        {
            ValidatePin(pinNumber);
            if (_openPins.TryRemove(pinNumber, out _))
            {
                NotifyPinChange(pinNumber, "close", "");
            }
            _pinValues.TryRemove(pinNumber, out _);
        }

        public virtual void Write(int pinNumber, PinValue value)
        {
            ValidatePin(pinNumber);
            if (!_openPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");
            _pinValues[pinNumber] = value;
            NotifyPinChange(pinNumber, "write", value.ToString());
        }

        public virtual void Write(ReadOnlySpan<PinValuePair> pinValuePairs)
        {
            foreach (var pair in pinValuePairs)
            {
                Write(pair.PinNumber, pair.PinValue);
            }
        }

        public virtual PinValue Read(int pinNumber)
        {
            ValidatePin(pinNumber);
            if (!_openPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");
            return _pinValues.TryGetValue(pinNumber, out var val) ? val : PinValue.Low;
        }

        public virtual void Read(Span<PinValuePair> pinValuePairs)
        {
            for (int i = 0; i < pinValuePairs.Length; i++)
            {
                var pair = pinValuePairs[i];
                var val = Read(pair.PinNumber);
                pinValuePairs[i] = new PinValuePair(pair.PinNumber, val);
            }
        }

        public virtual void SetPinMode(int pinNumber, PinMode mode)
        {
            ValidatePin(pinNumber);
            if (!_openPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");
            _openPins[pinNumber] = mode;
            NotifyPinChange(pinNumber, "mode", mode.ToString());
            
            var defaultVal = mode == PinMode.InputPullUp ? PinValue.High : PinValue.Low;
            _pinValues[pinNumber] = defaultVal;
        }

        public virtual PinMode GetPinMode(int pinNumber)
        {
            ValidatePin(pinNumber);
            if (!_openPins.TryGetValue(pinNumber, out var mode))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");
            return mode;
        }

        public virtual bool IsPinOpen(int pinNumber)
        {
            ValidatePin(pinNumber);
            return _openPins.ContainsKey(pinNumber);
        }

        public virtual bool IsPinModeSupported(int pinNumber, PinMode mode)
        {
            return true;
        }

        public virtual void RegisterCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
        {
            var list = _pinCallbacks.GetOrAdd(pinNumber, _ => new ConcurrentBag<(PinEventTypes, PinChangeEventHandler)>());
            list.Add((eventTypes, callback));
        }

        public virtual void UnregisterCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
        {
            if (_pinCallbacks.TryGetValue(pinNumber, out var list))
            {
                var items = list.ToArray();
                var remaining = new ConcurrentBag<(PinEventTypes, PinChangeEventHandler)>();
                foreach (var item in items)
                {
                    if (item.Handler != callback)
                    {
                        remaining.Add(item);
                    }
                }
                _pinCallbacks[pinNumber] = remaining;
            }
        }

        public virtual WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                return WaitForEvent(pinNumber, eventTypes, cts.Token);
            }
        }

        public virtual WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<PinEventTypes>();
            
            PinChangeEventHandler tempHandler = (sender, args) =>
            {
                tcs.TrySetResult(args.ChangeType);
            };

            RegisterCallbackForPinValueChangedEvent(pinNumber, eventTypes, tempHandler);

            try
            {
                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    tcs.Task.Wait(cancellationToken);
                    
                    return new WaitForEventResult
                    {
                        TimedOut = false,
                        EventTypes = tcs.Task.Result
                    };
                }
            }
            catch (OperationCanceledException)
            {
                return new WaitForEventResult
                {
                    TimedOut = true,
                    EventTypes = PinEventTypes.None
                };
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                return new WaitForEventResult
                {
                    TimedOut = true,
                    EventTypes = PinEventTypes.None
                };
            }
            finally
            {
                UnregisterCallbackForPinValueChangedEvent(pinNumber, tempHandler);
            }
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

        private static bool TryFindDevWebDll(string baseDir, out string dllPath)
        {
            // First check the simulator sub-folder in the base directory
            dllPath = Path.Combine(baseDir, "simulator", "DevDecoder.GpioSimulator.Web.dll");
            if (File.Exists(dllPath))
            {
                return true;
            }

            dllPath = null;
            var dir = new DirectoryInfo(baseDir);
            
            while (dir != null)
            {
                var webAppDir = Path.Combine(dir.FullName, "src", "DevDecoder.GpioSimulator.Web");
                if (!Directory.Exists(webAppDir))
                {
                    webAppDir = Path.Combine(dir.FullName, "DevDecoder.GpioSimulator.Web");
                }

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
                        catch
                        {
                            // Ignore IO errors
                        }
                    }
                }
                dir = dir.Parent;
            }
            return false;
        }

        public void Dispose()
        {
            // Cleanly close all currently open pins to release them on the server
            foreach (var pin in _openPins.Keys)
            {
                try
                {
                    ClosePin(pin);
                }
                catch { }
            }

            _cts.Cancel();
            _wsClient?.Dispose();
            _openPins.Clear();
            _pinValues.Clear();
            _pinCallbacks.Clear();
        }
    }
}
