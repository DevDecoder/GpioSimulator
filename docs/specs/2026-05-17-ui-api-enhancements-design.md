# Design Specification: UI & API Enhancements

This design document specifies the architecture, components, and behavior for the simulator's UI tweaks and API verification improvements.

## 1. Requirements

### 🎨 UI & Interactivity
* **Hide Ownership for Non-Configurable Pins:** Do not display owner ID or owner type information for pins that are Ground (GND), Power (3.3V, 5V), or non-configurable in the UI tooltip.
* **Controller Ownership Mode Representation:** When a controller has ownership of a pin, display the current mode as static text in the UI rather than in a disabled dropdown element.
* **Click-and-Hold Momentary Button Press:** When clicking and holding a pin in the UI (specifically for input modes `Input`, `InputPullUp`, and `InputPullDown`), make it act as a button press rather than opening a tooltip. In standard `Input` mode, this toggles the state for the duration of the hold. A short click (under 250ms) still opens the tooltip.

### 💻 Client/Server Validation & API
* **Dynamic Pin Verification & Real-Time Active Board Sync:**
  * Avoid heavy synchronous HTTP overhead. Instead, keep client GpioControllers and UI clients fully reactive.
  * When a new WebSocket connection is established, the server pushes a `"board_change"` packet containing the initial `boardId` and `pins` mapping to the client immediately.
  * Whenever the board mapping is changed dynamically (e.g. via UI selection or a POST to `/api/board/active`), the server broadcasts a `"board_change"` packet to all connected clients.
  * The C# client `GpioController` receives this packet, parses it, and dynamically updates its cached physical/logical pin dictionaries in memory.
  * The UI client (`main.js`) receives this packet and automatically re-renders the visual SVG board layout if a board ID mismatch is detected.
* **Enforce OpenPin Re-entry Rejection:** If `OpenPin` is called on a pin that is already open (even by the same client), the server must respond with an error. The client `GpioController` throws an identical `.NET` exception (`InvalidOperationException`) on receipt.
* **IsPinModeSupported:** Fully support standard `IsPinModeSupported(int pinNumber, PinMode mode)` backed by API verification.

### 🔌 Sample Console Application
* **Default Scheme:** Default the sample application to connect to the simulator using the physical `Board` scheme instead of `Logical`.
* **Expand Commands:** Add interactive commands `setmode`, `isopen`, and `issupported` to the CLI shell.

---

## 2. Component Design & Changes

### 2.1 UI Layer (`wwwroot/main.js` and `style.css`)
* **Tooltip Rendering:**
  * In `refreshTooltipContent()`, wrap the Ownership row in a conditional check: `pin.logical !== null`.
  * If `canEdit` is `false`, render `modeSelector` as a plain text string representing the current mode rather than an HTML `<select>` tag.
* **Momentary Hold / Toggle Behavior:**
  * Replace the existing `click` event listener on pin hotspots with `mousedown`, `touchstart`, `mouseup`, `touchend`, and `mouseleave` event handlers.
  * Maintain press timing via `Date.now()`.
  * On press down, if the pin is in an input mode and editable, transition state immediately to its active level (High for Pull-down, Low for Pull-up, opposite of current value for standard Input) and send a WebSocket frame to update the simulator.
  * On release, drive the value back to its default/original value.
  * If the press duration is `< 250ms`, invoke `showTooltip()`. Otherwise, suppress the tooltip.
* **WebSocket Reactive Board Reloading:**
  * In `main.js` WebSocket listener: on receiving `board_change` message, if `msg.boardId` is different from the active schema's board ID, invoke `loadBoard(msg.boardId, true)` (skipping the server POST feedback loop).

### 2.2 Server Layer (`Program.cs`)
* **Strict OpenPin Semantics:**
  * In the `open` WebSocket message handler:
    * If `pinStates.TryGetValue(physPin, out var existingState)` is true and the owner client is active, return an `open_response` with `status = "error"`, `errorType = "InvalidOperationException"`, and `errorMessage = "Pin is already open."`.
    * Remove the exception for the same client ID dynamically re-opening its own pin under a new mode. Mode updates must go through the standard `mode` command.
* **WebSocket Initializer & Board Change Broadcasts:**
  * When a socket connects, send the initial `"board_change"` packet with `activeBoardId` and the active physical-to-logical mapping.
  * Add a helper method `BroadcastBoardChange()` that sends the current board mapping to all connected sockets. Call it in `LoadBoardMapping` or when switching layouts via HTTP POST.

### 2.3 Client Layer (`GpioController.cs`)
* **Reactive Board Mapping Cache:**
  * Declare `ConcurrentDictionary<int, int> _activePhysToLog` and `_activeLogToPhys` fields.
  * In `ReceiveWebSocketMessages`, intercept `"action": "board_change"` packets and dynamically parse the pins mapping to refresh the dictionaries in real-time.
* **Pin Verification:**
  * Inside `IsValidPin(int pinNumber)`:
    * If the scheme is `Logical`, check if the number exists in `_activeLogToPhys.Keys` (or fall back to `0-27` range check if maps are empty).
    * If the scheme is `Board`, check if the number exists in `_activePhysToLog.Keys` (or fall back to standard hardcoded set).
  * Implement `IsPinModeSupported` based on `IsValidPin` and standard mode boundaries.

### 2.4 Sample Layer (`Program.cs`)
* Initialize the CLI program with `PinNumberingScheme.Board` by default.
* Integrate command parsers for `setmode`, `isopen`, and `issupported` mapping directly to the underlying `GpioController` API methods.
