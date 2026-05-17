# Implementation Plan: GPIO Pull Modes UI & Validation Refinement

This plan outlines the enhancements to fully support, validate, and dynamically configure all standard GPIO pin modes (`Input`, `Output`, `InputPullUp`, and `InputPullDown`) across the simulator driver, the web server, the Web UI, and the CLI sample application.

---

## 1. Research Findings & Answers

### A. Validating Physical & Logical Pins (e.g., Physical Pin 30 GND)
* **Question:** *What does the original `System.Device.Gpio` do if we try to open a pin that has no logic ID (e.g., Pin 30 GND)?*
* **Answer:** In a real Raspberry Pi/microcontroller system, physical pins like Ground (GND), 5V, 3.3V, and other non-GPIO power pins are not connected to the CPU's GPIO multiplexer. Trying to open or control these pins using `System.Device.Gpio` will result in an `ArgumentException` stating that the pin number is not valid or not supported under the active numbering scheme.
* **Our Solution:** Implement strict pin validation inside `GpioController.cs`:
  - **Logical Scheme:** Pin numbers must be between `0` and `27` (inclusive).
  - **Board Scheme:** Pin numbers must match physical header pins that map to GPIOs: `3, 5, 7, 8, 10, 11, 12, 13, 15, 16, 18, 19, 21, 22, 23, 24, 26, 27, 28, 29, 31, 32, 33, 35, 36, 37, 38, 40`.
  - Throwing `ArgumentException` on invalid pins guarantees that testing code behaves exactly as it would on real hardware.

### B. Changing Pin Modes without Closing (Calling `OpenPin` on Open Pins)
* **Question:** *Can we call `OpenPin` on the original API to change the mode without first closing the pin?*
* **Answer:** No. If you call `controller.OpenPin(pin, newMode)` on a pin that is already open, the original API throws an `InvalidOperationException` stating that the pin is already open.
* **Official API Pattern:** The correct, standard way to change the mode of an already open pin is to call `controller.SetPinMode(pin, newMode)`.
* **Our Solution:** Update `GpioController.cs` to throw `InvalidOperationException` if a pin is already open when `OpenPin` is called. Ensure `SetPinMode(pin, mode)` is validated and behaves as the standard way to configure active pins.

---

## 2. Proposed Changes

### Phase 1: Simulator Driver Core (`src/System.Device.Gpio`)
* **Modify:** `src/System.Device.Gpio/GpioController.cs`
  * Add `IsValidPin(int pinNumber)` and `ValidatePin(int pinNumber)` helper methods.
  * In `OpenPin` variants, throw `ArgumentException` if the pin is invalid. Throw `InvalidOperationException` if the pin is already open.
  * Update `ClosePin`, `Write`, `Read`, `SetPinMode`, `GetPinMode`, `IsPinOpen`, and `IsPinModeSupported` to invoke `ValidatePin(pinNumber)` first.
  * Update `ReceiveWebSocketMessages` to support parsing both `"value"` and `"mode"` dynamically from incoming server updates, updating internal state maps.

### Phase 2: Web Server (`src/DevDecoder.GpioSimulator.Web`)
* **Modify:** `src/DevDecoder.GpioSimulator.Web/Program.cs`
  * In the `"mode"` action WebSocket handler, change the broadcast action from a simple `"write"` message to a full `"state_change"` broadcast message containing both `mode` and `value`. This guarantees all connected UI clients synchronize their values and mode visual badges in real-time.

### Phase 3: Web UI (`src/DevDecoder.GpioSimulator.Web/wwwroot`)
* **Modify:** `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`
  * In the WebSocket message handler, update `"mode"` and `"state_change"` actions to correctly refresh the internal pin state maps and call `updatePinVisuals(msg.pin)`. Ensure that changing to `InputPullUp` sets the default value to `High` immediately.
  * In `refreshTooltipContent`, if `pin.logical !== null`, render a beautiful dropdown (`<select id="tooltip-mode-select">`) for the `"Current Mode"`.
  * Wire up the `onchange` event of the dropdown to send a `"mode"` action to the server using a new `sendPinMode(pin, mode)` function, updating the local state maps immediately for sub-millisecond visual response.
* **Modify:** `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css`
  * Style the dropdown `tooltip-mode-select` with Outfit font, dark translucent background, premium border highlights, and smooth hover/focus transitions.
  * Add stylish CSS badges for `mode-inputpullup` and `mode-inputpulldown`.

### Phase 4: Interactive CLI Sample (`src/DevDecoder.GpioSimulator.Sample`)
* **Modify:** `src/DevDecoder.GpioSimulator.Sample/Program.cs`
  * Adjust status print spacing from `PadRight(6)` to `PadRight(13)` to perfectly align longer mode strings (e.g. `InputPullUp`, `InputPullDown`).
  * Add sample pins opened as `InputPullUp` (Logical Pin 7) and `InputPullDown` (Logical Pin 8) at startup to immediately showcase the new capabilities.

---

## 3. Verification & Validation Plan
1. **Compilation:** Run `dotnet build` to ensure the simulated library, web server, and CLI sample build cleanly.
2. **Behavioral Testing:** Launch the sample program and interactively open invalid pins (e.g. physical pin 30) or double-open pins to verify exceptions are correctly thrown.
3. **Web Interface Verification:** Load the Web UI, open the interactive pin tooltips, change modes, verify the board state is updated in real-time, and check that `InputPullUp` immediately transitions to `High`.
