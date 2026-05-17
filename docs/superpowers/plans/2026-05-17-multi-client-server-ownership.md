# Multi-Client Server-Controlled Pin Ownership Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish the web server as the authoritative controller for pin ownership, validating pin number ranges, tracking ownership client IDs across multiple CLI and UI instances, throwing exact .NET exceptions client-side, and disabling Web UI controls for pins owned by other clients.

**Architecture:** Implement a request-response RPC model over WebSocket inside the simulated `GpioController` driver. When opening a pin, block the calling thread synchronously until the server responds with success or exception information. On the server side, validate bounds and track owner `clientId` and `clientType`. In the Web UI, assign a unique `clientId` to each browser session and render real-time owner info while dynamically gating input controls.

**Tech Stack:** C#, ASP.NET Core, JavaScript, WebSockets, Vanilla CSS.

---

## 1. File Structure & Responsibilities

- **Modify:** `src/System.Device.Gpio/GpioController.cs`
  - Implement dynamic synchronous request-response waiting dictionary (`_pendingRequests`).
  - Add `IsValidPin` and `ValidatePin` helpers for logical BCM (0–27) and valid physical board pins.
  - Implement server-controlled `OpenPin` overloads that wait for the server's `"open_response"`, throwing `ArgumentException` or `InvalidOperationException` matching real hardware exact types.
  - Fall back to standard local offline behavior if the WebSocket connection is not active (ensures existing tests continue passing).
- **Modify:** `src/DevDecoder.GpioSimulator.Web/Program.cs`
  - Define `ClientConnection` class mapping connections to unique IDs, schemes, and types (`ui` vs. `controller`).
  - Define `PinState` class storing pin levels, modes, and owner connection metadata.
  - Clean up client connection tracking and implement a unified, centralized `BroadcastStateChangeAsync` to serialize state changes and translate pin numbers dynamically per client scheme.
  - Implement authoritative `"open"` action checks, rejecting invalid physical pins and duplicate ownership.
- **Modify:** `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`
  - Receive and register the unique browser `clientId` from the server's `"welcome"` event.
  - Revamp the tooltip UI to display status dynamically based on pin state:
    - **Closed:** Show `[Open Pin]` button.
    - **Open & Owned by Us:** Show mode `<select>` dropdown, driver toggles/buttons, and a `[Close Pin]` button.
    - **Open & Owned by Others:** Show read-only mode badge, logic level dot, and ownership metadata: `Opened by [Client Type] (ID: [ID])`.
- **Modify:** `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css`
  - Add beautiful responsive styles for premium buttons (`open-btn`, `close-btn`), translucent dropdown lists, and ownership badge overlays.

---

## Task 1: Autoritative Validations & Client Sync in `GpioController.cs`

**Files:**
- Modify: `src/System.Device.Gpio/GpioController.cs`

- [ ] **Step 1: Declare request tracking collections and helpers**
  Update the fields section of `GpioController.cs` (lines 14-25) to declare pending requests, client ID, and local validation lists.

  ```csharp
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
  ```

- [ ] **Step 2: Add Pin Range & Type Validation Helpers**
  Add the validation routines below into `GpioController.cs` near the constructor.

  ```csharp
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
  ```

- [ ] **Step 3: Revamp WebSocket Message Processing with JSON parser helper**
  Replace `ReceiveWebSocketMessages` (lines 147-196) and implement `GetJsonValue` to parse both `open_response` updates and manual client updates.

  ```csharp
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
                          PinEventTypes occurredType = val == PinValue.High ? PinEventTypes.Rising : PinEventTypes.Falling;
                          FireCallbacks(pin, occurredType);
                      }
                  }
              }
          }
      }
      catch
      {
          // Absorb socket exceptions on close
      }
  }
  ```

- [ ] **Step 4: Update GpioController mutators with strict validations and wait loop**
  Update the synchronous `OpenPin`, `SetPinMode`, `Write`, and `Read` implementations in `GpioController.cs` (lines 220-300).

  ```csharp
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
          // Local offline fallback validation for unit testing compatibility
          if (_openPins.ContainsKey(pinNumber))
          {
              throw new InvalidOperationException($"Pin {pinNumber} is already open.");
          }
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

  public virtual PinValue Read(int pinNumber)
  {
      ValidatePin(pinNumber);
      if (!_openPins.ContainsKey(pinNumber))
          throw new InvalidOperationException($"Pin {pinNumber} is not open.");
      return _pinValues.TryGetValue(pinNumber, out var val) ? val : PinValue.Low;
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
  ```

- [ ] **Step 5: Run offline GpioController tests to verify correctness**
  Run tests: `dotnet test src/DevDecoder.GpioSimulator.Tests/DevDecoder.GpioSimulator.Tests.csproj`
  Expected: PASS

---

## Task 2: Server-Authoritative Ownership Tracking & Cleanup in `Program.cs`

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/Program.cs`

- [ ] **Step 1: Declare Connection & State Models**
  Update the structures section of `Program.cs` (lines 43-50) to declare custom types.

  ```csharp
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

  var clients = new ConcurrentDictionary<Guid, ClientConnection>();
  var pinStates = new ConcurrentDictionary<int, PinState>();
  ```

- [ ] **Step 2: Add Unified State Broadcasting Helper**
  Add `BroadcastStateChangeAsync` to `Program.cs` to handle translating pin schemes and multicasting.

  ```csharp
  async Task BroadcastStateChangeAsync(int physPin, PinState state, WebSocket? senderSocket = null)
  {
      foreach (var client in clients)
      {
          if (client.Value.Socket.State == WebSocketState.Open)
          {
              int targetPin = physPin;
              if (client.Value.Scheme == "Logical")
              {
                  lock (mappingLock)
                  {
                      if (!activePhysToLog.TryGetValue(physPin, out int log))
                      {
                          // If not present in active mapping under Logical Scheme, skip
                          continue;
                      }
                      targetPin = log;
                  }
              }
              
              var msg = JsonSerializer.Serialize(new
              {
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
                  await client.Value.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
              }
              catch
              {
                  // Silently ignore broken connections (will be Tri-Removed in receive loops)
              }
          }
      }
  }
  ```

- [ ] **Step 3: Implement Web API Updates**
  Update the status endpoints `/api/pins` and others to read the revised strongly-typed `pinStates`.

  ```csharp
  app.MapGet("/api/pins", () => Results.Json(pinStates.ToDictionary(
      kvp => kvp.Key,
      kvp => $"{kvp.Value.Mode}:{kvp.Value.Value}:{kvp.Value.OwnerId?.ToString() ?? ""}:{kvp.Value.OwnerType}"
  )));
  ```

- [ ] **Step 4: Update WebSocket Core receive loop with Ownership Validation Gates**
  Replace the WebSocket handle routing block (lines 180-365) to enforce client registration, welcome broadcasts, and command-level authorization.

  ```csharp
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
      // Send unique assigned Client ID welcome message
      var welcomeMsg = JsonSerializer.Serialize(new { action = "welcome", clientId = clientId.ToString() });
      await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(welcomeMsg)), WebSocketMessageType.Text, true, CancellationToken.None);

      // Send initial states
      foreach (var kvp in pinStates)
      {
          int targetPin = kvp.Key;
          if (clientScheme == "Logical")
          {
              lock (mappingLock)
              {
                  if (!activePhysToLog.TryGetValue(kvp.Key, out int log)) continue;
                  targetPin = log;
              }
          }
          var initMsg = JsonSerializer.Serialize(new 
          { 
              action = "state_change", 
              pin = targetPin, 
              mode = kvp.Value.Mode, 
              value = kvp.Value.Value,
              ownerId = kvp.Value.OwnerId?.ToString() ?? "",
              ownerType = kvp.Value.OwnerType
          });
          await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(initMsg)), WebSocketMessageType.Text, true, CancellationToken.None);
      }

      var buffer = new byte[1024 * 4];
      while (webSocket.State == WebSocketState.Open)
      {
          var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
          if (result.MessageType == WebSocketMessageType.Close) break;

          var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
          using var json = JsonDocument.Parse(message);
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
              else if (action == "open")
              {
                  var mode = root.TryGetProperty("mode", out var mProp) ? mProp.GetString() : "Input";
                  var requestId = root.TryGetProperty("requestId", out var rProp) ? rProp.GetString() : "";
                  
                  bool validPhys = false;
                  var validPhysPins = new HashSet<int> { 3, 5, 7, 8, 10, 11, 12, 13, 15, 16, 18, 19, 21, 22, 23, 24, 26, 27, 28, 29, 31, 32, 33, 35, 36, 37, 38, 40 };
                  validPhys = validPhysPins.Contains(physPin);

                  if (!validPhys)
                  {
                      var errResponse = JsonSerializer.Serialize(new
                      {
                          action = "open_response",
                          pin = pin,
                          requestId = requestId,
                          status = "error",
                          errorType = "ArgumentException",
                          errorMessage = $"Pin {pin} is not a valid GPIO pin under scheme {clientScheme}."
                      });
                      await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(errResponse)), WebSocketMessageType.Text, true, CancellationToken.None);
                  }
                  else if (pinStates.TryGetValue(physPin, out var existingState))
                  {
                      var errResponse = JsonSerializer.Serialize(new
                      {
                          action = "open_response",
                          pin = pin,
                          requestId = requestId,
                          status = "error",
                          errorType = "InvalidOperationException",
                          errorMessage = existingState.OwnerId == clientId 
                              ? $"Pin {pin} is already open." 
                              : $"Pin {pin} is already open by client {existingState.OwnerId} (type: {existingState.OwnerType})."
                      });
                      await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(errResponse)), WebSocketMessageType.Text, true, CancellationToken.None);
                  }
                  else
                  {
                      var state = new PinState
                      {
                          Mode = mode,
                          Value = mode == "InputPullUp" ? "High" : "Low",
                          OwnerId = clientId,
                          OwnerType = clientType
                      };
                      pinStates.TryAdd(physPin, state);

                      var okResponse = JsonSerializer.Serialize(new
                      {
                          action = "open_response",
                          pin = pin,
                          requestId = requestId,
                          status = "success"
                      });
                      await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(okResponse)), WebSocketMessageType.Text, true, CancellationToken.None);
                      await BroadcastStateChangeAsync(physPin, state);
                  }
              }
              else
              {
                  // Ownership validations for state changes (Write, Mode, Close)
                  if (pinStates.TryGetValue(physPin, out var state))
                  {
                      if (state.OwnerId != clientId)
                      {
                          Log($"[SECURITY WARN] Unauthorized pin {physPin} write/mode attempt rejected from client {clientId}");
                          continue;
                      }

                      if (action == "write" || action == "read")
                      {
                          var val = root.GetProperty("value").GetString() ?? "Low";
                          state.Value = val;
                          await BroadcastStateChangeAsync(physPin, state);
                      }
                      else if (action == "mode")
                      {
                          var mode = root.GetProperty("mode").GetString() ?? "Input";
                          state.Mode = mode;
                          state.Value = mode == "InputPullUp" ? "High" : "Low";
                          await BroadcastStateChangeAsync(physPin, state);
                      }
                      else if (action == "close")
                      {
                          pinStates.TryRemove(physPin, out _);
                          
                          // Broadcast closing action to update UI maps
                          foreach (var cl in clients)
                          {
                              if (cl.Value.Socket.State == WebSocketState.Open)
                              {
                                  int targetPin = physPin;
                                  if (cl.Value.Scheme == "Logical")
                                  {
                                      lock (mappingLock)
                                      {
                                          if (!activePhysToLog.TryGetValue(physPin, out int log)) continue;
                                          targetPin = log;
                                      }
                                  }
                                  var closeMsg = JsonSerializer.Serialize(new { action = "close", pin = targetPin });
                                  await cl.Value.Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(closeMsg)), WebSocketMessageType.Text, true, CancellationToken.None);
                              }
                          }
                      }
                  }
              }
          }
      }
  }
  ```

---

## Task 3: Interactive Multi-Client UI & Tooltip Controls in `main.js`

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`

- [ ] **Step 1: Setup unique client identifier storage**
  Declare browser connection variable `myClientId` in `main.js` (lines 4-8).

  ```javascript
  let activeSchema = null;
  let ws = null;
  let myClientId = null;
  const pinsStateMap = {};
  let activeTooltipPin = null;
  ```

- [ ] **Step 2: Parse Welcome, State-Changes and Ownership in `setupWebSocket`**
  Update `setupWebSocket`'s `ws.onmessage` handler (lines 446-492) to process state change variables.

  ```javascript
  ws.onmessage = (event) => {
      try {
          const msg = JSON.parse(event.data);
          
          if (msg.action === "welcome") {
              myClientId = msg.clientId;
              log(`Registered unique client session ID: ${myClientId}`);
          }
          else if (msg.action === "reset") {
              for (const key in pinsStateMap) {
                  delete pinsStateMap[key];
              }
              if (activeSchema && activeSchema.pins) {
                  activeSchema.pins.forEach(pin => {
                      updatePinVisuals(pin.physical);
                  });
              }
              log("Simulator state reset.");
          }
          else if (msg.action === "close") {
              delete pinsStateMap[msg.pin];
              updatePinVisuals(msg.pin);
              log(`Pin ${msg.pin} closed`);
          }
          else if (msg.action === "log") {
              log(msg.value);
          }
          else if (msg.action === "write") {
              pinsStateMap[msg.pin] = pinsStateMap[msg.pin] || { mode: "Output", value: "Low", ownerId: "", ownerType: "" };
              pinsStateMap[msg.pin].value = msg.value;
              updatePinVisuals(msg.pin);
          }
          else if (msg.action === "mode") {
              pinsStateMap[msg.pin] = pinsStateMap[msg.pin] || { mode: "Input", value: "Low", ownerId: "", ownerType: "" };
              pinsStateMap[msg.pin].mode = msg.mode;
              updatePinVisuals(msg.pin);
          }
          else if (msg.action === "state_change") {
              pinsStateMap[msg.pin] = {
                  mode: msg.mode,
                  value: msg.value,
                  ownerId: msg.ownerId,
                  ownerType: msg.ownerType
              };
              updatePinVisuals(msg.pin);
              log(`Pin ${msg.pin} updated: Mode=${msg.mode}, Level=${msg.value}, Owner=${msg.ownerType}`);
          }
      } catch (err) {
          console.error("Error parsing WebSocket packet", err);
      }
  };
  ```

- [ ] **Step 3: Update `syncPinStatesFromServer` Parser**
  Modify API parse loop in `main.js` to structure detailed properties.

  ```javascript
  async function syncPinStatesFromServer() {
      try {
          const res = await fetch('/api/pins');
          if (res.ok) {
              const serverStates = await res.json();
              Object.entries(serverStates).forEach(([pinStr, stateStr]) => {
                  const pin = parseInt(pinStr);
                  const parts = stateStr.split(':');
                  pinsStateMap[pin] = {
                      mode: parts[0],
                      value: parts[1],
                      ownerId: parts[2] || "",
                      ownerType: parts[3] || ""
                  };
                  updatePinVisuals(pin);
              });
              log("Synchronized all pin states from server.");
          }
      } catch (err) {
          log(`Failed to sync states from server: ${err}`);
      }
  }
  ```

- [ ] **Step 4: Update `refreshTooltipContent` with ownership views**
  Rewrite `refreshTooltipContent` in `main.js` (lines 301-383) to present Open/Close buttons and read-only details dynamically.

  ```javascript
  function refreshTooltipContent(pin, anchorEl) {
      const state = pinsStateMap[pin.physical];
      let logicalStr = "N/A (Power / GND)";
      let bodySection = "";
      
      if (pin.logical === null) {
          // Closed non-GPIO Pins
          bodySection = `
              <div class="tooltip-info-row">
                  <span class="info-label">Physical Pin:</span>
                  <span class="info-val font-mon">${pin.physical}</span>
              </div>
              <div class="tooltip-info-row">
                  <span class="info-label">Type:</span>
                  <span class="info-val">${pin.name}</span>
              </div>
          `;
      }
      else if (!state) {
          // GPIO Pin is Closed (un-owned)
          logicalStr = `GPIO ${pin.logical}`;
          bodySection = `
              <div class="tooltip-info-row">
                  <span class="info-label">Physical Pin:</span>
                  <span class="info-val font-mon">${pin.physical}</span>
              </div>
              <div class="tooltip-info-row">
                  <span class="info-label">Logical ID:</span>
                  <span class="info-val font-mon">${logicalStr}</span>
              </div>
              <div class="tooltip-info-row">
                  <span class="info-label">Status:</span>
                  <span class="info-val badge mode-none">Closed</span>
              </div>
              <div class="tooltip-control">
                  <button class="action-btn open-btn" id="tooltip-pin-open">Open Pin</button>
              </div>
          `;
      }
      else {
          // GPIO Pin is Open
          logicalStr = `GPIO ${pin.logical}`;
          const isOwner = state.ownerId === myClientId;
          const ownerLabel = state.ownerType === "controller" ? "Console Driver" : "Other Tab";
          
          let controlSection = "";
          let modeSelect = "";
          
          if (isOwner) {
              // Owner interactive dropdown list
              modeSelect = `
                  <select id="tooltip-mode-select" class="tooltip-mode-select">
                      <option value="Input" ${state.mode === "Input" ? "selected" : ""}>Input</option>
                      <option value="Output" ${state.mode === "Output" ? "selected" : ""}>Output</option>
                      <option value="InputPullUp" ${state.mode === "InputPullUp" ? "selected" : ""}>InputPullUp</option>
                      <option value="InputPullDown" ${state.mode === "InputPullDown" ? "selected" : ""}>InputPullDown</option>
                  </select>
              `;
              
              const modeLower = state.mode.toLowerCase();
              if (modeLower === "input") {
                  const checkedAttr = state.value === "High" ? "checked" : "";
                  controlSection = `
                      <div class="tooltip-control">
                          <label class="switch">
                              <input type="checkbox" id="tooltip-state-toggle" ${checkedAttr}>
                              <span class="slider round"></span>
                          </label>
                          <div class="control-text">
                              <span class="control-label">Manual Input Driver</span>
                              <span class="control-sub">Toggle HIGH/LOW</span>
                          </div>
                      </div>
                  `;
              } else if (modeLower === "inputpulldown") {
                  controlSection = `
                      <div class="tooltip-control">
                          <button class="push-btn" id="tooltip-state-push">Drive HIGH</button>
                          <div class="control-text">
                              <span class="control-label">Pull-Down Push Button</span>
                              <span class="control-sub">Hold to drive HIGH</span>
                          </div>
                      </div>
                  `;
              } else if (modeLower === "inputpullup") {
                  controlSection = `
                      <div class="tooltip-control">
                          <button class="push-btn" id="tooltip-state-push">Drive LOW</button>
                          <div class="control-text">
                              <span class="control-label">Pull-Up Push Button</span>
                              <span class="control-sub">Hold to drive LOW</span>
                          </div>
                      </div>
                  `;
              } else {
                  controlSection = `
                      <div class="tooltip-control disabled">
                          <div class="control-text">
                              <span class="control-label">Governed by Code</span>
                              <span class="control-sub">Output governed by owner logic</span>
                          </div>
                      </div>
                  `;
              }
              
              controlSection += `
                  <div class="tooltip-control border-top">
                      <button class="action-btn close-btn" id="tooltip-pin-close">Close Pin</button>
                  </div>
              `;
          }
          else {
              // Read-only info row for other owners
              modeSelect = `<span class="info-val badge mode-${state.mode.toLowerCase()}">${state.mode}</span>`;
              controlSection = `
                  <div class="tooltip-control disabled ownership-alert">
                      <div class="control-text">
                          <span class="control-label">Owned by ${ownerLabel}</span>
                          <span class="control-sub">Session: ${state.ownerId.substring(0, 8)}...</span>
                      </div>
                  </div>
              `;
          }
          
          bodySection = `
              <div class="tooltip-info-row">
                  <span class="info-label">Physical Pin:</span>
                  <span class="info-val font-mon">${pin.physical}</span>
              </div>
              <div class="tooltip-info-row">
                  <span class="info-label">Logical ID:</span>
                  <span class="info-val font-mon">${logicalStr}</span>
              </div>
              <div class="tooltip-info-row">
                  <span class="info-label">Current Mode:</span>
                  ${modeSelect}
              </div>
              <div class="tooltip-info-row">
                  <span class="info-label">Logic Level:</span>
                  <span class="info-val state-indicator ${state.value.toLowerCase()}">
                      <span class="state-dot"></span>
                      ${state.value}
                  </span>
              </div>
              ${controlSection}
          `;
      }
      
      tooltip.innerHTML = `
          <div class="tooltip-header">
              <h4>${pin.name}</h4>
              <span class="close-btn" id="tooltip-close-x">&times;</span>
          </div>
          <div class="tooltip-body">
              ${bodySection}
          </div>
      `;
      
      // Wire Close buttons
      const closeX = tooltip.querySelector('#tooltip-close-x');
      if (closeX) closeX.onclick = closeTooltip;
      
      // Wire Open Pin button
      const openBtn = tooltip.querySelector('#tooltip-pin-open');
      if (openBtn) {
          openBtn.onclick = () => {
              log(`UI client requesting to open Pin ${pin.physical}`);
              if (ws && ws.readyState === WebSocket.OPEN) {
                  ws.send(JSON.stringify({ action: "open", pin: pin.physical, mode: "Input" }));
              }
          };
      }
      
      // Wire Close Pin button
      const closeBtn = tooltip.querySelector('#tooltip-pin-close');
      if (closeBtn) {
          closeBtn.onclick = () => {
              log(`UI client closing Pin ${pin.physical}`);
              if (ws && ws.readyState === WebSocket.OPEN) {
                  ws.send(JSON.stringify({ action: "close", pin: pin.physical }));
              }
          };
      }
      
      // Wire Mode Change select dropdown list
      const modeSelectEl = tooltip.querySelector('#tooltip-mode-select');
      if (modeSelectEl) {
          modeSelectEl.onchange = (e) => {
              const selectedMode = e.target.value;
              log(`UI client changing Pin ${pin.physical} mode to ${selectedMode}`);
              if (ws && ws.readyState === WebSocket.OPEN) {
                  ws.send(JSON.stringify({ action: "mode", pin: pin.physical, mode: selectedMode }));
              }
          };
      }
      
      if (pin.logical !== null && state && state.ownerId === myClientId) {
          const toggle = tooltip.querySelector('#tooltip-state-toggle');
          if (toggle) {
              toggle.onchange = (e) => {
                  const newState = e.target.checked ? "High" : "Low";
                  pinsStateMap[pin.physical].value = newState;
                  sendPinState(pin.physical, "read", newState);
                  updatePinVisuals(pin.physical);
              };
          }
          
          const pushBtn = tooltip.querySelector('#tooltip-state-push');
          if (pushBtn) {
              const modeLower = state.mode.toLowerCase();
              const defaultState = modeLower === "inputpullup" ? "High" : "Low";
              const pressedState = modeLower === "inputpullup" ? "Low" : "High";
              
              const handlePress = (e) => {
                  e.preventDefault();
                  pinsStateMap[pin.physical].value = pressedState;
                  sendPinState(pin.physical, "read", pressedState);
                  updatePinVisuals(pin.physical);
              };
              
              const handleRelease = (e) => {
                  e.preventDefault();
                  pinsStateMap[pin.physical].value = defaultState;
                  sendPinState(pin.physical, "read", defaultState);
                  updatePinVisuals(pin.physical);
              };
              
              pushBtn.addEventListener('mousedown', handlePress);
              pushBtn.addEventListener('touchstart', handlePress);
              pushBtn.addEventListener('mouseup', handleRelease);
              pushBtn.addEventListener('mouseleave', handleRelease);
              pushBtn.addEventListener('touchend', handleRelease);
          }
      }
  }
  ```

---

## Task 4: UI Dropdowns & Buttons styling in `style.css`

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css`

- [ ] **Step 1: Add Translucent Custom Select Dropdown Styles**
  Append custom class definitions into `style.css` near lines 750-800.

  ```css
  /* Premium Glassmorphic Dropdowns & Selects */
  .tooltip-mode-select {
      background: rgba(30, 30, 40, 0.65);
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 6px;
      color: #f3f3f3;
      font-family: 'Outfit', sans-serif;
      font-size: 0.85rem;
      padding: 4px 8px;
      outline: none;
      transition: all 0.25s ease;
      cursor: pointer;
  }
  .tooltip-mode-select:hover {
      background: rgba(45, 45, 60, 0.8);
      border-color: rgba(255, 255, 255, 0.3);
  }
  .tooltip-mode-select:focus {
      border-color: var(--accent-color, #a855f7);
      box-shadow: 0 0 8px rgba(168, 85, 247, 0.4);
  }

  /* Action Buttons for Pins */
  .action-btn {
      width: 100%;
      padding: 8px 12px;
      border: none;
      border-radius: 6px;
      font-family: 'Outfit', sans-serif;
      font-weight: 600;
      font-size: 0.85rem;
      cursor: pointer;
      transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  }
  .open-btn {
      background: linear-gradient(135deg, #10b981, #059669);
      color: #ffffff;
      box-shadow: 0 4px 12px rgba(16, 185, 129, 0.25);
  }
  .open-btn:hover {
      transform: translateY(-1px);
      box-shadow: 0 6px 16px rgba(16, 185, 129, 0.4);
  }
  .close-btn {
      background: linear-gradient(135deg, #ef4444, #dc2626);
      color: #ffffff;
      box-shadow: 0 4px 12px rgba(239, 68, 68, 0.25);
  }
  .close-btn:hover {
      transform: translateY(-1px);
      box-shadow: 0 6px 16px rgba(239, 68, 68, 0.4);
  }

  .border-top {
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      padding-top: 12px;
      margin-top: 8px;
  }

  /* Ownership Alert details */
  .ownership-alert {
      background: rgba(234, 179, 8, 0.08) !important;
      border: 1px solid rgba(234, 179, 8, 0.25) !important;
      border-radius: 6px;
      padding: 8px 12px;
  }
  .ownership-alert .control-label {
      color: #fbbf24 !important;
      font-weight: 600;
  }
  ```

---

## 5. Verification & Validation Checklist

- [ ] **Run standard build verification**
  Run: `dotnet build`
  Expected: Successful compilation without warnings or errors.

- [ ] **Verify multi-client isolated tests**
  Run: `dotnet test`
  Expected: All unit tests execute and pass correctly in isolated offline conditions.

- [ ] **Launch real-time UI validation**
  Run local instance: `dotnet run --project src/DevDecoder.GpioSimulator.Sample/DevDecoder.GpioSimulator.Sample.csproj`
  Load in web browser and verify:
  1. Un-owned pins are displayed as closed with an `[Open Pin]` button.
  2. Pin can be successfully opened from UI, instantly unlocking the translucent Select Mode list.
  3. Interactive input state checkbox and push-buttons respond instantly.
  4. Run sample console driver side-by-side: verify pins opened by console cannot be written or closed from browser, showing detailed `Console Driver (ID: ...)` text inside tooltip.
