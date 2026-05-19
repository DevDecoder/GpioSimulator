# Refactoring Plan: Thin Sample CLI App

To ensure the Sample CLI application behaves as a high-fidelity test harness, we must eliminate all duplicated state-tracking (`_pins`, `_lastValues`) and custom validation logic in `Program.cs` that mirrors code in the driver or simulation engine. Instead, the application should delegate completely to the native APIs and error-handling of the customized `System.Device.Gpio` shim.

---

## Principles

1. **Preserve Mirror-Image APIs**: The custom `System.Device.Gpio` shims must mirror the official dotnet API perfectly. We do not add custom APIs (like `GetOpenPins`) to the shimmed classes.
2. **Stateless Test Harness**: The CLI app does not need to duplicate open/close or value state tracking. It should just naively invoke methods (e.g. `controller.OpenPin(...)`, `controller.Write(...)`) and let the driver/engine handle all boundary validations, propagating standard .NET exceptions (like `InvalidOperationException`) back to the CLI shell.
3. **Stateless Pin Status**: To support the `status` command without introducing non-standard APIs to `GpioController`, we can simply query the standard `controller.IsPinOpen(pin)` API in a loop from pin 1 to 40 (the typical Raspberry Pi header range). This gives us a 100% compliant, standard-compatible status query.

---

## Refactoring Program.cs to be Thin

We will clean up and rebuild `Program.cs` to eliminate duplicate state management, background thread polling, and manual validation.

### 1. Eliminate Fields & Background Work
- Delete `_pins` dictionary.
- Delete `_lastValues` dictionary.
- Remove `WatchInputPins` background polling method.
- Remove `_watcherCts` and `_watcherTask`.

### 2. Event-Driven Input Tracking
- Keep a reactive value change listener using the standard event callbacks:
```csharp
        private static void OnPinValueChanged(object sender, PinValueChangedEventArgs args)
        {
            PinValue val = _controller.Read(args.PinNumber);
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"\n[ALERT] Input state changed: Pin {args.PinNumber} is now {val}!");
                Console.ResetColor();
                PrintPrompt();
            }
        }
```
- In `OpenPin` helper, register the callback if the mode is an input mode.
- In `ClosePin` helper, unregister the callback.
- In the `setmode` / `sm` command, unregister first, change mode, and register if it became input.

### 3. Delegate Commands directly to GpioController
- **`scheme`**: Reset scheme by reinstantiating `_controller` without needing to clear local state.
- **`open`**: Call `OpenPin` directly. Let the controller/driver validate.
- **`close`**: Call `ClosePin` directly. Let the controller/driver validate open status.
- **`write`**: Convert input value to `PinValue` and call `controller.Write` directly. Let the driver/engine throw if invalid or unauthorized.
- **`read`**: Call `controller.Read` directly.
- **`setmode`**: Set the mode directly. Manage callbacks reactively.
- **`status`**: Loop through pin 1 to 40, check `controller.IsPinOpen(pin)` naively, and print the current mode and value using only standard `GpioController` methods!

---

## Verification Plan

1. **Compilation**: Run `dotnet build` to verify clean compilation of all libraries and the sample application.
2. **Unit Tests**: Run `dotnet test` to ensure existing driver and engine tests pass perfectly.
3. **Interactive Testing**: Run the Sample app, open output/input pins, toggle values from both the CLI and the Web UI, and verify that the alerts and commands reflect state in real-time.
