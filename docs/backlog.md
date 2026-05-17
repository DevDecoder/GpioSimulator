# DevDecoder.GpioSimulator Backlog

## Architectural Refactoring: GpioDriver Abstraction

### Overview
In standard `System.Device.Gpio` architectures, a central `GpioController` interacts with a underlying hardware chip via a modular `GpioDriver` class. To improve testing flexibility and standalone capabilities:
- Create a distinct **`WebAPIDriver`** class implementing the WebSocket/WebAPI-based communication scheme to connect to the external high-fidelity simulated UI.
- Create a distinct **`LocalDriver`** class that manages states in-memory without any WebSocket or web server dependencies, returning mock outputs.
- This will allow developers/tests to run `new GpioController(new LocalDriver())` when they do not require or care about the visual Web UI, making unit/integration testing fully offline, fast, and robust while reusing the exact same API footprint.

To support this the core logic should be moved into a DevDecoder.GpioSimulator.Common library, included in both the DevDecoder.GpioSimulator.Web library and a new DevDecoder.GpioSimulator.Local library.

## API and Eventing for Local Driver

The LocalDriver must expose the same event model as the WebAPIDriver to ensure 100% backward compatibility with applications that use event handlers for GPIO state changes.

**Required Events:**
- `PinModeChanged`: Fired when a pin's mode changes (e.g., Input, Output, PWM).
- `PinValueChanges`: Fired when a pin's value changes (e.g., High, Low).
- `ControllerDisposed`: Fired when the controller is disposed.