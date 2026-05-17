# UI & API Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement UI tweaks (hiding ownership, controller representation, click-and-hold momentary pressed interaction) and API validations (enforce OpenPin re-entry rejection, real-time WebSocket active board remapping, and IsPinModeSupported) in the simulator.

**Architecture:** Use reactive real-time active board sync: the server pushes the initial schema to connecting sockets via a `"board_change"` message, and broadcasts board changes dynamically. The client `GpioController` dynamically caches these mappings in memory, enabling instant, zero-HTTP-overhead checks inside `IsValidPin` and `IsPinModeSupported`.

**Tech Stack:** HTML/Vanilla JS, .NET (C#), ASP.NET WebSockets.

---

### Task 1: Sample Project Scheme Default

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Sample/Program.cs:31`

- [ ] **Step 1: Write minimal code modification**
  
  Update line 31 of [Program.cs](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Sample/Program.cs#L31):
  ```csharp
  PinNumberingScheme defaultScheme = PinNumberingScheme.Board;
  ```

- [ ] **Step 2: Build and verify compilation**
  
  Run: `dotnet build src/DevDecoder.GpioSimulator.Sample`
  Expected: Successful compilation.

- [ ] **Step 3: Commit**
  
  ```bash
  git add src/DevDecoder.GpioSimulator.Sample/Program.cs
  git commit -m "sample: default dynamic scheme to physical Board numbering"
  ```

---

### Task 2: Server-Side WebSocket Board Mappings and Validation

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/Program.cs`

- [ ] **Step 1: Enforce Strict OpenPin Re-entry Exception**
  
  Update the `"open"` message processor in [Program.cs](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/Program.cs#L400-L435) to strictly reject open requests if the pin is already open on the server:
  ```csharp
  // Check if pin is already open
  if (pinStates.TryGetValue(physPin, out var existingState))
  {
      bool ownerActive = existingState.OwnerId.HasValue && clients.ContainsKey(existingState.OwnerId.Value);
      if (ownerActive)
      {
          if (requestId != null)
          {
              var resp = JsonSerializer.Serialize(new {
                  action = "open_response",
                  requestId = requestId,
                  status = "error",
                  errorType = "InvalidOperationException",
                  errorMessage = $"Pin {physPin} is already open."
              });
              await webSocket.SendAsync(new ArraySegment<byte>(resp)), WebSocketMessageType.Text, true, CancellationToken.None);
          }
          Log($"Rejecting open for pin {physPin}: Pin is already open (owner: {existingState.OwnerId})");
          continue;
      }
  }
  ```

- [ ] **Step 2: Implement BroadcastBoardChange and dynamic push on connect**
  
  Add `BroadcastBoardChange()` helper inside [Program.cs](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/Program.cs):
  ```csharp
  async Task BroadcastBoardChange()
  {
      string msg;
      lock (mappingLock)
      {
          msg = JsonSerializer.Serialize(new
          {
              action = "board_change",
              boardId = activeBoardId,
              pins = activePhysToLog
          });
      }
      var bytes = Encoding.UTF8.GetBytes(msg);
      foreach (var c in clients.Values)
      {
          if (c.Socket.State == WebSocketState.Open)
          {
              try
              {
                  await c.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
              }
              catch { }
          }
      }
  }
  ```
  
  Invoke this broadcast at the end of `LoadBoardMapping(boardId)` dynamically:
  ```csharp
  _ = BroadcastBoardChange();
  ```

  Push the initial `"board_change"` packet immediately after client connection accepts around lines 220-229:
  ```csharp
  // Send initial board mapping info
  string boardMsg;
  lock (mappingLock)
  {
      boardMsg = JsonSerializer.Serialize(new
      {
          action = "board_change",
          boardId = activeBoardId,
          pins = activePhysToLog
      });
  }
  var boardBytes = Encoding.UTF8.GetBytes(boardMsg);
  await webSocket.SendAsync(new ArraySegment<byte>(boardBytes), WebSocketMessageType.Text, true, CancellationToken.None);
  ```

- [ ] **Step 3: Run server test suite**
  
  Run: `dotnet test`
  Expected: All server and client connection tests pass.

- [ ] **Step 4: Commit**
  
  ```bash
  git add src/DevDecoder.GpioSimulator.Web/Program.cs
  git commit -m "server: enforce strict open re-entry and broadcast board_change on mapping updates"
  ```

---

### Task 3: Client-Side Dynamic Mappings Cache & Verification

**Files:**
- Modify: `src/System.Device.Gpio/GpioController.cs`

- [ ] **Step 1: Declare Mapping Cache Dictionaries**
  
  Add active mapping fields in [GpioController.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Gpio/GpioController.cs):
  ```csharp
  private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _activePhysToLog = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();
  private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _activeLogToPhys = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();
  ```

- [ ] **Step 2: Parse board_change inside ReceiveWebSocketMessages**
  
  In [GpioController.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Gpio/GpioController.cs#L220-L270), parse `"board_change"` events to dynamically synchronize mappings in real-time:
  ```csharp
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
              _activePhysToLog.Clear();
              _activeLogToPhys.Clear();
              foreach (var pair in pairs)
              {
                  var parts = pair.Split(':');
                  if (parts.Length == 2)
                  {
                      var physStr = parts[0].Trim('"', ' ', '\r', '\n');
                      var logStr = parts[1].Trim('"', ' ', '\r', '\n');
                      if (int.TryParse(physStr, out int phys) && int.TryParse(logStr, out int log))
                      {
                          _activePhysToLog[phys] = log;
                          _activeLogToPhys[log] = phys;
                      }
                  }
              }
          }
      }
  }
  ```

- [ ] **Step 3: Implement Reactive IsValidPin**
  
  In [GpioController.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Gpio/GpioController.cs#L90-L112), update `IsValidPin` to check the cached server-pushed active mappings dynamically:
  ```csharp
  public bool IsValidPin(int pinNumber)
  {
      if (NumberingScheme == PinNumberingScheme.Logical)
      {
          if (_activeLogToPhys.Count > 0)
          {
              return _activeLogToPhys.ContainsKey(pinNumber);
          }
          return pinNumber >= 0 && pinNumber <= 27; // fallback
      }
      else
      {
          if (_activePhysToLog.Count > 0)
          {
              return _activePhysToLog.ContainsKey(pinNumber);
          }
          var validPhysPins = new System.Collections.Generic.HashSet<int> 
          { 
              3, 5, 7, 8, 10, 11, 12, 13, 15, 16, 18, 19, 21, 22, 23, 24, 26, 27, 28, 29, 31, 32, 33, 35, 36, 37, 38, 40 
          };
          return validPhysPins.Contains(pinNumber); // fallback
      }
  }
  ```

- [ ] **Step 4: Implement IsPinModeSupported**
  
  In [GpioController.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Gpio/GpioController.cs#L441-L444), update `IsPinModeSupported`:
  ```csharp
  public virtual bool IsPinModeSupported(int pinNumber, PinMode mode)
  {
      return IsValidPin(pinNumber) && (
          mode == PinMode.Input || 
          mode == PinMode.Output || 
          mode == PinMode.InputPullUp || 
          mode == PinMode.InputPullDown
      );
  }
  ```

- [ ] **Step 5: Run unit tests**
  
  Run: `dotnet test`
  Expected: All tests pass.

- [ ] **Step 6: Commit**
  
  ```bash
  git add src/System.Device.Gpio/GpioController.cs
  git commit -m "client: add dynamic server-backed IsValidPin and IsPinModeSupported"
  ```

---

### Task 4: Expand Sample CLI Commands

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Sample/Program.cs`

- [ ] **Step 1: Add command parsers to Program.cs**
  
  Update the main interactive command switch block in [Program.cs](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Sample/Program.cs#L105-L295):
  ```csharp
  case "isopen":
      if (parts.Length < 2 || !int.TryParse(parts[1], out int isOpenPin))
      {
          Console.WriteLine("Error: Please specify pin number. Syntax: isopen <pin>");
          break;
      }
      bool isPinOpen = controller.IsPinOpen(isOpenPin);
      Console.WriteLine($"Pin {isOpenPin} is {(isPinOpen ? "OPEN" : "CLOSED")}.");
      break;
  case "issupported":
      if (parts.Length < 3)
      {
          Console.WriteLine("Error: Please specify pin and mode. Syntax: issupported <pin> <in|out|pullup|pulldown>");
          break;
      }
      if (int.TryParse(parts[1], out int supportPin))
      {
          string modeStr = parts[2].ToLower();
          PinMode mode = PinMode.Input;
          bool validMode = true;
          if (modeStr == "out" || modeStr == "output") mode = PinMode.Output;
          else if (modeStr == "pullup" || modeStr == "inputpullup" || modeStr == "pu") mode = PinMode.InputPullUp;
          else if (modeStr == "pulldown" || modeStr == "inputpulldown" || modeStr == "pd") mode = PinMode.InputPullDown;
          else if (modeStr == "in" || modeStr == "input") mode = PinMode.Input;
          else validMode = false;

          if (!validMode)
          {
              Console.WriteLine($"Error: Invalid mode '{parts[2]}'. Use 'in', 'out', 'pullup', or 'pulldown'.");
              break;
          }
          bool isSupported = controller.IsPinModeSupported(supportPin, mode);
          Console.WriteLine($"Pin {supportPin} supports mode {mode}: {isSupported}");
      }
      else
      {
          Console.WriteLine("Error: Invalid pin number.");
      }
      break;
  case "setmode":
      if (parts.Length < 3)
      {
          Console.WriteLine("Error: Please specify pin and mode. Syntax: setmode <pin> <in|out|pullup|pulldown>");
          break;
      }
      if (int.TryParse(parts[1], out int setModePin))
      {
          string modeStr = parts[2].ToLower();
          PinMode mode = PinMode.Input;
          bool validMode = true;
          if (modeStr == "out" || modeStr == "output") mode = PinMode.Output;
          else if (modeStr == "pullup" || modeStr == "inputpullup" || modeStr == "pu") mode = PinMode.InputPullUp;
          else if (modeStr == "pulldown" || modeStr == "inputpulldown" || modeStr == "pd") mode = PinMode.InputPullDown;
          else if (modeStr == "in" || modeStr == "input") mode = PinMode.Input;
          else validMode = false;

          if (!validMode)
          {
              Console.WriteLine($"Error: Invalid mode '{parts[2]}'. Use 'in', 'out', 'pullup', or 'pulldown'.");
              break;
          }
          controller.SetPinMode(setModePin, mode);
          lock (_pins)
          {
              _pins[setModePin] = mode;
          }
          lock (_lastValues)
          {
              _lastValues[setModePin] = controller.Read(setModePin);
          }
          Console.WriteLine($"Successfully set Pin {setModePin} mode to {mode}.");
      }
      else
      {
          Console.WriteLine("Error: Invalid pin number.");
      }
      break;
  ```

- [ ] **Step 2: Update PrintHelp**
  
  In [Program.cs](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Sample/Program.cs#L297-L312):
  ```csharp
  private static void PrintHelp()
  {
      Console.WriteLine("\nAvailable Interactive Commands:");
      Console.WriteLine("  open <pin> <in|out|pullup|pulldown>  - Open a pin with specified mode");
      Console.WriteLine("  setmode <pin> <in|out|pullup|pulldown> - Change the mode of an already open pin");
      Console.WriteLine("  isopen <pin>                         - Check if a pin is open");
      Console.WriteLine("  issupported <pin> <mode>             - Check if a pin supports a mode");
      Console.WriteLine("  close <pin>                          - Close an open pin");
      Console.WriteLine("  write <pin> <1|0|h|l>                - Write High/Low to an output pin (e.g. write 3 1)");
      Console.WriteLine("  read <pin>                           - Read the current value of an open pin");
      Console.WriteLine("  scheme <logical|board>               - Switch dynamic pin numbering scheme (e.g. scheme board)");
      Console.WriteLine("  schema <logical|board>               - Alias for scheme command");
      Console.WriteLine("  status                               - Display status of all currently opened pins");
      Console.WriteLine("  help                                 - Show this guide");
      Console.WriteLine("  exit | quit | q                      - Terminate the simulation program");
  }
  ```

- [ ] **Step 3: Build sample**
  
  Run: `dotnet build src/DevDecoder.GpioSimulator.Sample`
  Expected: Successful compilation.

- [ ] **Step 4: Commit**
  
  ```bash
  git add src/DevDecoder.GpioSimulator.Sample/Program.cs
  git commit -m "sample: add setmode, isopen, and issupported commands to interactive CLI"
  ```

---

### Task 5: UI Tooltip and Sync Mappings Tweaks

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`

- [ ] **Step 1: Hide Ownership for non-configurable pins**
  
  In [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js#L304-L444), update `refreshTooltipContent` rendering:
  ```javascript
  const ownershipRow = pin.logical !== null ? `
      <div class="tooltip-info-row">
          <span class="info-label">Ownership:</span>
          <span class="info-val">${state.ownerType ? `${state.ownerType} (${state.ownerId?.substring(0, 4) || 'None'})` : 'None'}</span>
      </div>
  ` : '';
  ```

- [ ] **Step 2: Represent owned pin modes as static text**
  
  In `refreshTooltipContent`, if `canEdit` is false, change the `modeSelector` markup:
  ```javascript
  const modeLabels = {
      "Input": "Input (Standard)",
      "Output": "Output",
      "InputPullUp": "Input Pull-Up",
      "InputPullDown": "Input Pull-Down"
  };
  const modeSelector = canEdit ? `
      <select id="tooltip-mode-selector" class="tooltip-select">
          <option value="Input" ${state.mode === 'Input' ? 'selected' : ''}>Input (Standard)</option>
          <option value="Output" ${state.mode === 'Output' ? 'selected' : ''}>Output</option>
          <option value="InputPullUp" ${state.mode === 'InputPullUp' ? 'selected' : ''}>Input Pull-Up</option>
          <option value="InputPullDown" ${state.mode === 'InputPullDown' ? 'selected' : ''}>Input Pull-Down</option>
      </select>
  ` : `<span class="info-val">${modeLabels[state.mode || 'Input']}</span>`;
  ```

- [ ] **Step 3: Handle skipPostNotification in loadBoard**
  
  Update `loadBoard(boardId, skipPostNotification = false)`:
  ```javascript
  // Notify the server of the active board layout if standard board
  if (!customBoard && !skipPostNotification) {
      await fetch(`/api/board/active?boardId=${boardId}`, { method: 'POST' });
  }
  ```

- [ ] **Step 4: Intercept board_change on WebSocket**
  
  In [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js) incoming `ws.onmessage` switch:
  ```javascript
  else if (msg.action === "board_change") {
      const currentBoardId = activeSchema?.boardId || activeSchema?.id;
      if (currentBoardId !== msg.boardId) {
          log(`Board layout changed on server to: ${msg.boardId}. Reloading UI...`);
          loadBoard(msg.boardId, true);
      }
  }
  ```

- [ ] **Step 5: Commit**
  
  ```bash
  git add src/DevDecoder.GpioSimulator.Web/wwwroot/main.js
  git commit -m "ui: hide ownership, display owned modes as text, and sync board_change events"
  ```

---

### Task 6: Click-and-Hold Interactive Button Momentary Trigger

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`

- [ ] **Step 1: Implement timed click-and-hold hotspot bindings**
  
  In [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js#L170-L198), remove standard `click` event listeners on SVG hotspot groups and replace with timed pointer listeners:
  ```javascript
  let pressStartTime = 0;
  let activePressPin = null;
  let isHoldTriggered = false;
  let initialValue = null;

  function handlePressStart(pinId, e) {
      if (e.button !== 0 && e.type !== 'touchstart') return; // Left click or touch only
      
      const pin = schema.pins.find(p => p.physical === pinId);
      if (!pin || pin.logical === null) return;
      
      const state = pinStates[pinId] || { mode: "Input", value: "Low", ownerId: null };
      const canEdit = !state.ownerId || state.ownerId === myClientId;
      const isInputMode = state.mode === "Input" || state.mode === "InputPullUp" || state.mode === "InputPullDown";

      activePressPin = pinId;
      pressStartTime = Date.now();
      isHoldTriggered = false;
      initialValue = state.value;

      if (isInputMode && canEdit) {
          // Send momentary drive command on hold trigger after brief delay
          setTimeout(() => {
              if (activePressPin === pinId && (Date.now() - pressStartTime) >= 250) {
                  isHoldTriggered = true;
                  // Determine opposite pressed value based on mode
                  let targetValue = "High";
                  if (state.mode === "InputPullUp") targetValue = "Low";
                  else if (state.mode === "InputPullDown") targetValue = "High";
                  else targetValue = (initialValue === "High") ? "Low" : "High";

                  send({
                      action: "value",
                      pin: pinId,
                      value: targetValue
                  });
              }
          }, 250);
      }
  }

  function handlePressEnd(pinId, e) {
      if (activePressPin !== pinId) return;

      const duration = Date.now() - pressStartTime;
      const pin = schema.pins.find(p => p.physical === pinId);
      const state = pinStates[pinId] || { mode: "Input", value: "Low", ownerId: null };

      if (isHoldTriggered) {
          // Revert back to default state upon release
          let defaultValue = "Low";
          if (state.mode === "InputPullUp") defaultValue = "High";
          else if (state.mode === "InputPullDown") defaultValue = "Low";
          else defaultValue = initialValue;

          send({
              action: "value",
              pin: pinId,
              value: defaultValue
          });
      } else if (duration < 250) {
          // Open tooltip if brief tap
          showTooltip(pinId);
      }

      activePressPin = null;
      isHoldTriggered = false;
  }

  // Bind to hotspots
  group.addEventListener('mousedown', (e) => handlePressStart(pin.physical, e));
  group.addEventListener('touchstart', (e) => handlePressStart(pin.physical, e), { passive: true });
  group.addEventListener('mouseup', (e) => handlePressEnd(pin.physical, e));
  group.addEventListener('touchend', (e) => handlePressEnd(pin.physical, e));
  group.addEventListener('mouseleave', (e) => {
      if (activePressPin === pin.physical) {
          handlePressEnd(pin.physical, e);
      }
  });
  group.addEventListener('touchcancel', (e) => {
      if (activePressPin === pin.physical) {
          handlePressEnd(pin.physical, e);
      }
  });
  ```

- [ ] **Step 2: Commit**
  
  ```bash
  git add src/DevDecoder.GpioSimulator.Web/wwwroot/main.js
  git commit -m "ui: implement click-and-hold button input driving for interactive pins"
  ```
