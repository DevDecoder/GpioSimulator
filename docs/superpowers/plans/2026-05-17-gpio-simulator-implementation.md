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

- [x] **Step 1: Create the PinMode enum**
- [x] **Step 2: Create the PinValue struct**
- [x] **Step 3: Verify the build**

---

### Task 2: Cross-Platform Browser Opener
**Files:**
* Create: `src/System.Device.Gpio/BrowserLauncher.cs`

- [x] **Step 1: Write the BrowserLauncher class**
- [x] **Step 2: Verify the build**

---

### Task 3: GpioController Skeleton
**Files:**
* Create: `src/System.Device.Gpio/GpioController.cs`

- [x] **Step 1: Implement the base controller interface**
- [x] **Step 2: Verify the build**

---

### Task 4: ASP.NET Core WebSocket Hub Server
**Files:**
* Modify: `src/DevDecoder.GpioSimulator.Web/Program.cs`

- [x] **Step 1: Replace Program.cs content**
- [x] **Step 2: Verify the Web app builds**

---

### Task 5: Microcontroller JSON Board Schemas
**Files:**
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/board_schemas/raspberry_pi_5.json`
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/board_schemas/arduino_uno.json`

- [x] **Step 1: Write Raspberry Pi 5 Schema**
- [x] **Step 2: Write Arduino Uno Schema**

---

### Task 6: Visual Web UI Frontend
**Files:**
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/index.html`
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css`
* Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`

- [x] **Step 1: Create index.html**
- [x] **Step 2: Create style.css**
- [x] **Step 3: Create main.js**

---

### Task 7: Full GpioController WebSocket Synchronization
**Files:**
* Modify: `src/System.Device.Gpio/GpioController.cs`

- [x] **Step 1: Inject WebSocket Client and Process Spawning logic**
- [x] **Step 2: Verify both projects compile successfully**

---

### Task 8: Interactive Sample Program and Unit Testing
**Files:**
* Create: `src/DevDecoder.GpioSimulator.Sample/Program.cs`
* Create: `src/DevDecoder.GpioSimulator.Sample/DevDecoder.GpioSimulator.Sample.csproj`
* Create: `src/DevDecoder.GpioSimulator.Tests/PinValueTests.cs`

- [x] **Step 1: Create DevDecoder.GpioSimulator.Sample.csproj**
- [x] **Step 2: Write rich, interactive CLI sample program**
- [x] **Step 3: Create DevDecoder.GpioSimulator.Tests xUnit project**
- [x] **Step 4: Write robust PinValue unit tests and verify they pass**

---

### Task 9: Official System.Device.Gpio API Parity & Callback Simulation
**Files:**
* Create: `src/System.Device.Gpio/PinNumberingScheme.cs`
* Create: `src/System.Device.Gpio/PinValuePair.cs`
* Create: `src/System.Device.Gpio/PinEventTypes.cs`
* Create: `src/System.Device.Gpio/PinValueChangedEventArgs.cs`
* Modify: `src/System.Device.Gpio/GpioController.cs`
* Modify: `src/DevDecoder.GpioSimulator.Tests/PinValueTests.cs`

- [ ] **Step 1: Implement supporting types**
  Create the missing structs, classes, and enums (`PinNumberingScheme.cs`, `PinValuePair.cs`, `PinEventTypes.cs`, and `PinValueChangedEventArgs.cs`) under `src/System.Device.Gpio/`.
  
- [ ] **Step 2: Add GpioController constructor overloads and missing methods**
  Update `GpioController.cs` with `PinCount`, `NumberingScheme`, new constructors, and missing methods (`IsPinOpen`, `IsPinModeSupported`, `GetPinMode`, `OpenPin` overloads with default arguments and initial output values).

- [ ] **Step 3: Add event registration and edge detection**
  Implement `RegisterCallbackForPinValueChangedEvent` and `UnregisterCallbackForPinValueChangedEvent`. Add state edge transition detection in `ReceiveWebSocketMessages` (Low -> High = Rising, High -> Low = Falling) and fire callbacks.

- [ ] **Step 4: Implement synchronous event wait**
  Add `WaitForEvent` blocking overloads utilizing `TaskCompletionSource` or thread signaling that blocks/unblocks appropriately when input transitions occur.

- [ ] **Step 5: Verify via unit tests**
  Add comprehensive event callback unit tests inside `DevDecoder.GpioSimulator.Tests` to verify interrupt handling, and run the test suite to verify success.
