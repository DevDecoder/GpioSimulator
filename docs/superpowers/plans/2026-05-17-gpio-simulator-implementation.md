# Extensible GPIO Simulator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a drop-in C# NuGet replacement for `System.Device.Gpio` (targeting .NET Standard 2.0) that automatically launches a local, metadata-driven Web Simulator interface (targeting .NET 8.0) to display and interact with digital/analog pin states in the browser without local firewall blocks.

**Architecture:** 
1. `System.Device.Gpio` shim containing identical API classes (`GpioController`, `PinMode`, `PinValue`) syncing state over local WebSockets.
2. `DevDecoder.GpioSimulator.Web` minimal ASP.NET Core web host serving a dynamic single-page visual SVG representation of loaded microcontroller boards (based on dynamic JSON schemas).
3. The shim automatically checks if the server is active on `127.0.0.1:5050` and spawns it using the system's trusted `dotnet` CLI, opening the browser automatically.

**Tech Stack:** C#, .NET Standard 2.0, .NET 8.0 (ASP.NET Core, Minimal APIs, WebSockets), HTML5, CSS3, JavaScript.

---

## File Structure Map
* `src/System.Device.Gpio/PinMode.cs` - Enums for GPIO pin configuration.
* `src/System.Device.Gpio/PinValue.cs` - Implicitly convertible value representation.
* `src/System.Device.Gpio/BrowserLauncher.cs` - OS-aware default browser opener.
* `src/System.Device.Gpio/GpioController.cs` - Primary client interface that manages WebSocket sync and child process spawning.
* `src/DevDecoder.GpioSimulator.Web/Program.cs` - Web server, static file host, and WebSocket hub.
* `src/DevDecoder.GpioSimulator.Web/wwwroot/board_schemas/` - Folder containing metadata-driven board schema definitions.
* `src/DevDecoder.GpioSimulator.Web/wwwroot/index.html` - Visual simulator interface structure.
* `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css` - Visual board and terminal styling.
* `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js` - WebSocket logic, dynamic SVG drawing, and interactive controls.

---

## Implementation Tasks

### Task 1: System.Device.Gpio Core Enums & Structs
**Files:**
* Create: `src/System.Device.Gpio/PinMode.cs`
* Create: `src/System.Device.Gpio/PinValue.cs`

- [ ] **Step 1: Create the PinMode enum**
  Write this enum in `src/System.Device.Gpio/PinMode.cs`:
  ```csharp
  namespace System.Device.Gpio
  {
      public enum PinMode
      {
          Input = 0,
          Output = 1,
          InputPullUp = 2,
          InputPullDown = 3
      }
  }
  ```

- [ ] **Step 2: Create the PinValue struct**
  Write this struct with implicit conversion operators and equality interfaces in `src/System.Device.Gpio/PinValue.cs`:
  ```csharp
  using System;

  namespace System.Device.Gpio
  {
      public struct PinValue : IEquatable<PinValue>
      {
          private readonly byte _value;

          private PinValue(byte value) => _value = value;

          public static PinValue Low => new PinValue(0);
          public static PinValue High => new PinValue(1);

          public static implicit operator PinValue(bool value) => value ? High : Low;
          public static implicit operator bool(PinValue value) => value._value != 0;
          public static implicit operator PinValue(int value) => value == 0 ? Low : High;
          public static implicit operator int(PinValue value) => value._value;

          public bool Equals(PinValue other) => _value == other._value;
          public override bool Equals(object obj) => obj is PinValue other && Equals(other);
          public override int GetHashCode() => _value.GetHashCode();
          public override string ToString() => _value == 0 ? "Low" : "High";

          public static bool operator ==(PinValue left, PinValue right) => left.Equals(right);
          public static bool operator !=(PinValue left, PinValue right) => !left.Equals(right);
      }
  }
  ```

- [ ] **Step 3: Verify the build**
  Run: `dotnet build`
  Expected: Compile PASS without errors.

---

### Task 2: Cross-Platform Browser Opener
**Files:**
* Create: `src/System.Device.Gpio/BrowserLauncher.cs`

- [ ] **Step 1: Write the BrowserLauncher class**
  Implement cross-platform shell launch and robust cmd.exe start fallbacks for locked-down Windows machines in `src/System.Device.Gpio/BrowserLauncher.cs`:
  ```csharp
  using System;
  using System.Diagnostics;
  using System.Runtime.InteropServices;

  namespace System.Device.Gpio
  {
      public static class BrowserLauncher
      {
          public static void Open(string url)
          {
              try
              {
                  if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                  {
                      Process.Start(new ProcessStartInfo
                      {
                          FileName = url,
                          UseShellExecute = true
                      });
                  }
                  else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                  {
                      Process.Start("open", url);
                  }
                  else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                  {
                      Process.Start("xdg-open", url);
                  }
              }
              catch
              {
                  if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                  {
                      try
                      {
                          Process.Start("cmd", $"/c start {url.Replace("&", "^&")}");
                      }
                      catch
                      {
                          Console.WriteLine($"[Pi Simulator] Please open your browser and navigate to: {url}");
                      }
                  }
                  else
                  {
                      Console.WriteLine($"[Pi Simulator] Please open your browser and navigate to: {url}");
                  }
              }
          }
      }
  }
  ```

- [ ] **Step 2: Verify the build**
  Run: `dotnet build`
  Expected: Compile PASS.

---

### Task 3: GpioController Skeleton
**Files:**
* Create: `src/System.Device.Gpio/GpioController.cs`

- [ ] **Step 1: Implement the base controller interface**
  Write the core `GpioController` shape inside `src/System.Device.Gpio/GpioController.cs`:
  ```csharp
  using System;
  using System.Collections.Concurrent;

  namespace System.Device.Gpio
  {
      public class GpioController : IDisposable
      {
          private readonly ConcurrentDictionary<int, PinMode> _openPins = new ConcurrentDictionary<int, PinMode>();
          private readonly ConcurrentDictionary<int, PinValue> _pinValues = new ConcurrentDictionary<int, PinValue>();

          public GpioController()
          {
              // Startup checks & WebSocket instantiation will go here
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
              NotifyPinChange(pinNumber, "close", "");
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
              // WebSocket push implementation in later task
          }

          public void Dispose()
          {
              _openPins.Clear();
              _pinValues.Clear();
          }
      }
  }
  ```

- [ ] **Step 2: Verify the build**
  Run: `dotnet build`
  Expected: Compile PASS.

---

### Task 4: ASP.NET Core WebSocket Hub Server
**Files:**
* Modify: `src/DevDecoder.GpioSimulator.Web/Program.cs`

- [ ] **Step 1: Replace Program.cs content**
  Implement the minimal WebSocket and REST server capable of broadcasting real-time pin adjustments, serving static assets, and reporting online status in `src/DevDecoder.GpioSimulator.Web/Program.cs`:
  ```csharp
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

  app.MapUse("/ws", async (context, next) =>
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
          finally
          {
              // Clean up client collection
          }
      }
      else
      {
          await next();
      }
  });

  app.Run();
  ```

- [ ] **Step 2: Verify the Web app builds**
  Run: `dotnet build`
  Expected: Compile PASS.

---

### Task 5: Microcontroller JSON Board Schemas
**Files:**
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/board_schemas/raspberry_pi_5.json`
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/board_schemas/arduino_uno.json`

- [ ] **Step 1: Write Raspberry Pi 5 Schema**
  Write this file at `src/DevDecoder.GpioSimulator.Web/wwwroot/board_schemas/raspberry_pi_5.json`:
  ```json
  {
    "boardId": "raspberry_pi_5",
    "displayName": "Raspberry Pi 5",
    "layoutType": "dual_row_header",
    "visuals": {
      "boardColor": "#0d5c34",
      "pinColumns": 2,
      "totalPins": 40
    },
    "pins": [
      { "physical": 1, "logical": null, "name": "3.3V Power", "supportedModes": [] },
      { "physical": 2, "logical": null, "name": "5V Power", "supportedModes": [] },
      { "physical": 3, "logical": 2, "name": "GPIO 2 (SDA)", "supportedModes": ["Input", "Output"] },
      { "physical": 4, "logical": null, "name": "5V Power", "supportedModes": [] },
      { "physical": 5, "logical": 3, "name": "GPIO 3 (SCL)", "supportedModes": ["Input", "Output"] },
      { "physical": 6, "logical": null, "name": "GND", "supportedModes": [] },
      { "physical": 7, "logical": 4, "name": "GPIO 4 (GPCLK0)", "supportedModes": ["Input", "Output"] },
      { "physical": 8, "logical": 14, "name": "GPIO 14 (TXD)", "supportedModes": ["Input", "Output"] },
      { "physical": 9, "logical": null, "name": "GND", "supportedModes": [] },
      { "physical": 10, "logical": 15, "name": "GPIO 15 (RXD)", "supportedModes": ["Input", "Output"] },
      { "physical": 11, "logical": 17, "name": "GPIO 17", "supportedModes": ["Input", "Output"] },
      { "physical": 12, "logical": 18, "name": "GPIO 18", "supportedModes": ["Input", "Output"] }
    ]
  }
  ```

- [ ] **Step 2: Write Arduino Uno Schema**
  Write this file at `src/DevDecoder.GpioSimulator.Web/wwwroot/board_schemas/arduino_uno.json`:
  ```json
  {
    "boardId": "arduino_uno",
    "displayName": "Arduino Uno R3",
    "layoutType": "split_headers",
    "visuals": {
      "boardColor": "#006699"
    },
    "pins": [
      { "physical": 1, "logical": 0, "name": "D0 (RX)", "supportedModes": ["Input", "Output"] },
      { "physical": 2, "logical": 1, "name": "D1 (TX)", "supportedModes": ["Input", "Output"] },
      { "physical": 3, "logical": 2, "name": "D2", "supportedModes": ["Input", "Output"] },
      { "physical": 4, "logical": 3, "name": "D3 (~)", "supportedModes": ["Input", "Output"] },
      { "physical": 14, "logical": 14, "name": "Analog A0", "supportedModes": ["Input", "AnalogInput"] },
      { "physical": 15, "logical": 15, "name": "Analog A1", "supportedModes": ["Input", "AnalogInput"] }
    ]
  }
  ```

---

### Task 6: Visual Web UI Frontend
**Files:**
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/index.html`
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css`
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`

- [ ] **Step 1: Create index.html**
  Create the basic layout with a board wrapper and a logs terminal in `src/DevDecoder.GpioSimulator.Web/wwwroot/index.html`:
  ```html
  <!DOCTYPE html>
  <html lang="en">
  <head>
      <meta charset="UTF-8">
      <meta name="viewport" content="width=device-width, initial-scale=1.0">
      <title>DevDecoder GPIO Simulator</title>
      <link rel="stylesheet" href="style.css">
  </head>
  <body>
      <header>
          <h1>DevDecoder GPIO Board Simulator</h1>
          <div class="control-panel">
              <label for="board-select">Select Active Board: </label>
              <select id="board-select">
                  <option value="raspberry_pi_5">Raspberry Pi 5</option>
                  <option value="arduino_uno">Arduino Uno R3</option>
              </select>
          </div>
      </header>
      
      <main>
          <div id="board-container">
              <div id="visual-board"></div>
          </div>
          <div id="terminal-panel">
              <h3>Real-Time Activity Log</h3>
              <div id="terminal-log"></div>
          </div>
      </main>

      <script src="main.js"></script>
  </body>
  </html>
  ```

- [ ] **Step 2: Create style.css**
  Implement beautiful premium aesthetics (dark grid pattern, realistic LEDs, clear terminal lines) in `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css`:
  ```css
  :root {
      --bg-color: #121214;
      --card-bg: #1a1a1e;
      --text-color: #e1e1e6;
      --accent: #4fc3f7;
      --led-off: #2e2e34;
      --led-on: #4caf50;
  }

  body {
      background-color: var(--bg-color);
      color: var(--text-color);
      font-family: 'Segoe UI', system-ui, sans-serif;
      margin: 0;
      padding: 20px;
      display: flex;
      flex-direction: column;
      height: 95vh;
  }

  header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 2px solid var(--card-bg);
      padding-bottom: 10px;
  }

  select {
      background-color: var(--card-bg);
      color: var(--text-color);
      border: 1px solid var(--accent);
      padding: 8px;
      border-radius: 4px;
      cursor: pointer;
  }

  main {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: 20px;
      margin-top: 20px;
      flex-grow: 1;
      height: 75vh;
  }

  #board-container {
      background-color: var(--card-bg);
      border-radius: 8px;
      padding: 20px;
      display: flex;
      justify-content: center;
      align-items: center;
      border: 1px solid #29292e;
  }

  #visual-board {
      width: 100%;
      height: 100%;
      display: flex;
      flex-direction: column;
      justify-content: center;
      align-items: center;
  }

  /* Grid styles for board pins */
  .pin-header {
      background-color: #222;
      border-radius: 4px;
      padding: 10px;
      display: grid;
      gap: 8px;
      border: 2px solid #555;
  }

  .pin-row {
      display: flex;
      gap: 15px;
      align-items: center;
  }

  .pin-node {
      width: 24px;
      height: 24px;
      background-color: #888;
      border-radius: 50%;
      display: flex;
      justify-content: center;
      align-items: center;
      cursor: pointer;
      font-size: 10px;
      font-weight: bold;
      color: #000;
  }

  .pin-node.gnd { background-color: #333; color: #fff; }
  .pin-node.v5 { background-color: #e53935; color: #fff; }
  .pin-node.v3 { background-color: #fb8c00; color: #fff; }

  .led-indicator {
      width: 12px;
      height: 12px;
      border-radius: 50%;
      background-color: var(--led-off);
      transition: background-color 0.2s, box-shadow 0.2s;
  }

  .led-indicator.active {
      background-color: var(--led-on);
      box-shadow: 0 0 10px var(--led-on);
  }

  #terminal-panel {
      background-color: #0a0a0c;
      border-radius: 8px;
      border: 1px solid #1a1a1e;
      padding: 15px;
      display: flex;
      flex-direction: column;
  }

  #terminal-log {
      flex-grow: 1;
      font-family: 'Consolas', monospace;
      font-size: 13px;
      overflow-y: auto;
      background-color: #000;
      padding: 10px;
      border-radius: 4px;
      color: #00ff00;
  }
  ```

- [ ] **Step 3: Create main.js**
  Write the dynamic rendering script that loads the active schema, creates the SVG/HTML layout, updates indicators when receiving WebSocket events, and sends input clicks back in `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`:
  ```javascript
  let activeSchema = null;
  let ws = null;
  const pinsStateMap = {};

  const boardSelect = document.getElementById('board-select');
  const boardVisual = document.getElementById('visual-board');
  const logTerminal = document.getElementById('terminal-log');

  function log(message) {
      const time = new Date().toLocaleTimeString();
      const div = document.createElement('div');
      div.textContent = `[${time}] ${message}`;
      logTerminal.appendChild(div);
      logTerminal.scrollTop = logTerminal.scrollHeight;
  }

  async function loadBoard(boardId) {
      log(`Loading board schema for: ${boardId}...`);
      try {
          const res = await fetch(`board_schemas/${boardId}.json`);
          activeSchema = await res.json();
          renderBoard();
      } catch (err) {
          log(`Error loading schema: ${err}`);
      }
  }

  function renderBoard() {
      boardVisual.innerHTML = "";
      
      const headerDiv = document.createElement('div');
      headerDiv.className = "pin-header";
      headerDiv.style.backgroundColor = activeSchema.visuals.boardColor || "#1b3a24";
      
      if (activeSchema.layoutType === "dual_row_header") {
          headerDiv.style.gridTemplateColumns = `repeat(${activeSchema.visuals.pinColumns}, 1fr)`;
          
          for (let i = 0; i < activeSchema.pins.length; i += 2) {
              const rowDiv = document.createElement('div');
              rowDiv.className = "pin-row";
              
              const pinLeft = activeSchema.pins[i];
              const pinRight = activeSchema.pins[i+1];
              
              if (pinLeft) rowDiv.appendChild(createPinNode(pinLeft));
              if (pinRight) rowDiv.appendChild(createPinNode(pinRight));
              
              headerDiv.appendChild(rowDiv);
          }
      } else {
          // Fallback single line or split layout
          activeSchema.pins.forEach(pin => {
              const row = document.createElement('div');
              row.className = "pin-row";
              row.appendChild(createPinNode(pin));
              headerDiv.appendChild(row);
          });
      }
      
      boardVisual.appendChild(headerDiv);
      log(`Rendered ${activeSchema.displayName} header visual successfully.`);
  }

  function createPinNode(pin) {
      const div = document.createElement('div');
      div.className = "pin-node-wrapper";
      div.style.display = "flex";
      div.style.alignItems = "center";
      div.style.gap = "10px";
      
      const node = document.createElement('div');
      node.className = "pin-node";
      node.textContent = pin.physical;
      
      if (pin.name.includes("GND")) node.classList.add("gnd");
      else if (pin.name.includes("5V")) node.classList.add("v5");
      else if (pin.name.includes("3.3V")) node.classList.add("v3");
      
      const label = document.createElement('span');
      label.textContent = pin.name;
      label.style.fontSize = "12px";
      
      const led = document.createElement('div');
      led.className = "led-indicator";
      led.id = `led-pin-${pin.physical}`;
      
      div.appendChild(node);
      div.appendChild(led);
      div.appendChild(label);
      
      if (pin.logical !== null) {
          // If input, make it interactive/clickable
          node.style.cursor = "pointer";
          node.onclick = () => {
              const currentState = pinsStateMap[pin.logical] === "High" ? "Low" : "High";
              pinsStateMap[pin.logical] = currentState;
              led.classList.toggle('active', currentState === "High");
              
              log(`Input Triggered on Pin ${pin.logical}: ${currentState}`);
              sendPinState(pin.logical, "read", currentState);
          };
      }
      
      return div;
  }

  function setupWebSocket() {
      const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
      ws = new WebSocket(`${protocol}//${window.location.host}/ws`);
      
      ws.onopen = () => log("WebSocket Connected to Simulator Server.");
      
      ws.onmessage = (event) => {
          const msg = JSON.parse(event.data);
          if (msg.action === "write") {
              pinsStateMap[msg.pin] = msg.value;
              const physicalPin = activeSchema?.pins.find(p => p.logical === msg.pin)?.physical;
              if (physicalPin) {
                  const led = document.getElementById(`led-pin-${physicalPin}`);
                  if (led) {
                      led.classList.toggle('active', msg.value === "High");
                  }
              }
              log(`Pin ${msg.pin} state set to: ${msg.value}`);
          }
      };
      
      ws.onclose = () => {
          log("WebSocket closed. Attempting reconnect...");
          setTimeout(setupWebSocket, 3000);
      };
  }

  function sendPinState(pin, action, val) {
      if (ws && ws.readyState === WebSocket.OPEN) {
          ws.send(JSON.stringify({ action: action, pin: pin, value: val }));
      }
  }

  boardSelect.onchange = (e) => loadBoard(e.target.value);

  // Initialize
  loadBoard('raspberry_pi_5').then(setupWebSocket);
  ```

---

### Task 7: Full GpioController WebSocket Synchronization
**Files:**
* Modify: `src/System.Device.Gpio/GpioController.cs`

- [ ] **Step 1: Inject WebSocket Client and Process Spawning logic**
  Update `src/System.Device.Gpio/GpioController.cs` to automatically ensure the server is active, open the browser, connect to the WebSocket server, and synchronize states:
  ```csharp
  using System;
  using System.Collections.Concurrent;
  using System.Diagnostics;
  using System.IO;
  using System.Net.Http;
  using System.Net.WebSockets;
  using System.Text;
  using System.Text.Json;
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
                      using var doc = JsonDocument.Parse(msg);
                      var root = doc.RootElement;
                      if (root.TryGetProperty("action", out var actionProp) && actionProp.GetString() == "read")
                      {
                          int pin = root.GetProperty("pin").GetInt32();
                          string valStr = root.GetProperty("value").GetString() ?? "Low";
                          PinValue val = valStr == "High" ? PinValue.High : PinValue.Low;
                          
                          _pinValues[pin] = val;
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
                  var payload = JsonSerializer.Serialize(new { action = action, pin = pin, value = data, mode = data });
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
  ```

- [ ] **Step 2: Verify both projects compile successfully**
  Run: `dotnet build`
  Expected: PASS.

---

### Task 8: End-to-End Test Program
**Files:**
* Create: `src/GpioSimulator.Test/Program.cs`
* Create: `src/GpioSimulator.Test/GpioSimulator.Test.csproj`

- [ ] **Step 1: Create GpioSimulator.Test.csproj**
  Create a Console application that references our new local `System.Device.Gpio` shim project in `src/GpioSimulator.Test/GpioSimulator.Test.csproj`:
  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
      <OutputType>Exe</OutputType>
      <TargetFramework>net8.0</TargetFramework>
      <ImplicitUsings>enable</ImplicitUsings>
      <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
      <ProjectReference Include="..\System.Device.Gpio\System.Device.Gpio.csproj" />
    </ItemGroup>

  </Project>
  ```

- [ ] **Step 2: Write test program**
  Write a simple LED toggling and button reading loop inside `src/GpioSimulator.Test/Program.cs`:
  ```csharp
  using System;
  using System.Device.Gpio;
  using System.Threading;

  namespace GpioSimulator.Test
  {
      class Program
      {
          static void Main(string[] args)
          {
              Console.WriteLine("=== Starting DevDecoder Gpio Simulator Test ===");
              using var controller = new GpioController();
              
              // Open Pin 3 as Output (LED)
              controller.OpenPin(3, PinMode.Output);
              
              // Open Pin 5 as Input (momentary button)
              controller.OpenPin(5, PinMode.Input);

              Console.WriteLine("Pin 3 opened as Output, Pin 5 opened as Input.");
              Console.WriteLine("Observe the Web UI: dynamic board and LEDs should appear.");
              Console.WriteLine("Press Ctrl+C to terminate test.");

              bool ledState = false;

              while (true)
              {
                  // 1. Read Button State from UI
                  PinValue buttonState = controller.Read(5);
                  
                  // 2. Output to console on press
                  if (buttonState == PinValue.High)
                  {
                      Console.WriteLine($"[Console Log] Button (Pin 5) is ACTIVE!");
                  }

                  // 3. Toggle LED
                  ledState = !ledState;
                  controller.Write(3, ledState);
                  
                  Thread.Sleep(1000);
              }
          }
      }
  }
  ```

- [ ] **Step 3: Register test project in solution**
  Run: `dotnet sln add src/GpioSimulator.Test/GpioSimulator.Test.csproj`
  Expected: Success.

- [ ] **Step 4: Build and Execute Test**
  Run: `dotnet run --project src/GpioSimulator.Test/GpioSimulator.Test.csproj`
  Expected:
  1. The ASP.NET Web server starts in the background.
  2. The browser automatically pops open to `http://127.0.0.1:5050`.
  3. The visual LED next to physical Pin 3 blinks on and off once per second in real time on the screen!
  4. Clicking on Pin 5 on the webpage outputs `[Console Log] Button (Pin 5) is ACTIVE!` in the terminal.
