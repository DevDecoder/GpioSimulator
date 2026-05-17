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
                    // Fallback searching up for dev execution - check Release first, then Debug
                    dllPath = Path.Combine(baseDir, "..", "..", "..", "..", "DevDecoder.GpioSimulator.Web", "bin", "Release", "net8.0", "DevDecoder.GpioSimulator.Web.dll");
                    if (!File.Exists(dllPath))
                    {
                        dllPath = Path.Combine(baseDir, "..", "..", "..", "..", "DevDecoder.GpioSimulator.Web", "bin", "Debug", "net8.0", "DevDecoder.GpioSimulator.Web.dll");
                    }
                }

                if (File.Exists(dllPath))
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
                await _wsClient.ConnectAsync(new Uri($"ws://127.0.0.1:5050/ws?client=controller&scheme={NumberingScheme}"), CancellationToken.None);
                
                // Start background listener
                _ = Task.Run(ReceiveWebSocketMessages);
            }
            catch (Exception ex)
            {
                Log("Error", $"Client failed to connect to WebSocket: {ex.Message}");
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
                    if (msg.Contains("\"pin\":") && msg.Contains("\"value\":\""))
                    {
                        int pinIndex = msg.IndexOf("\"pin\":") + 6;
                        int commaIndex = msg.IndexOf(",", pinIndex);
                        if (commaIndex == -1)
                        {
                            commaIndex = msg.IndexOf("}", pinIndex);
                        }

                        if (commaIndex > pinIndex && int.TryParse(msg.Substring(pinIndex, commaIndex - pinIndex).Trim(), out int pin))
                        {
                            int valIndex = msg.IndexOf("\"value\":\"") + 9;
                            int endValIndex = msg.IndexOf("\"", valIndex);
                            if (endValIndex > valIndex)
                            {
                                string valStr = msg.Substring(valIndex, endValIndex - valIndex);
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

        public virtual void OpenPin(int pinNumber)
        {
            OpenPin(pinNumber, PinMode.Input);
        }

        public virtual void OpenPin(int pinNumber, PinMode mode)
        {
            _openPins[pinNumber] = mode;
            _pinValues[pinNumber] = PinValue.Low;
            NotifyPinChange(pinNumber, "mode", mode.ToString());
        }

        public virtual void OpenPin(int pinNumber, PinMode mode, PinValue initialValue)
        {
            _openPins[pinNumber] = mode;
            _pinValues[pinNumber] = initialValue;
            NotifyPinChange(pinNumber, "mode", mode.ToString());
            NotifyPinChange(pinNumber, "write", initialValue.ToString());
        }

        public virtual void ClosePin(int pinNumber)
        {
            if (_openPins.TryRemove(pinNumber, out _))
            {
                NotifyPinChange(pinNumber, "close", "");
            }
            _pinValues.TryRemove(pinNumber, out _);
        }

        public virtual void Write(int pinNumber, PinValue value)
        {
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
            if (!_openPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");
            _openPins[pinNumber] = mode;
            NotifyPinChange(pinNumber, "mode", mode.ToString());
        }

        public virtual PinMode GetPinMode(int pinNumber)
        {
            if (!_openPins.TryGetValue(pinNumber, out var mode))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");
            return mode;
        }

        public virtual bool IsPinOpen(int pinNumber)
        {
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
