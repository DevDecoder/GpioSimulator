# Implementation Plan: Analogue ADC & DAC Support

Implement analogue input and output simulation compatible with the .NET nanoFramework `System.Device.Adc` and `System.Device.Dac` APIs, add a premium ESP32 board schema, and support dynamic voltage scaling per board.

## User Review Required

> [!NOTE]
> **API Integrity Constraints**: To ensure 100% compatibility with real hardware, we will **NOT** alter the public API signatures of any simulated classes in `System.Device.Gpio`, `System.Device.Adc`, or `System.Device.Dac`. There will be no simulator-specific helper classes or properties exposed to simulated client applications.

> [!IMPORTANT]
> **Simulation Engine Voltage Tracking & Automatic Mapping**:
> - The `SimulationEngine` tracks pin states internally using absolute voltages (in Volts) and pin modes.
> - **Board Operating Voltage**: Defined in each board schema (e.g., `5.0` V for Arduino, `3.3` V for ESP32 and Raspberry Pi).
> - **Digital Write**: Writing `"High"` sets the pin to the board's operating voltage (e.g. `5.0` V). Writing `"Low"` sets it to `0.0` V.
> - **Digital Read**: Reading a pin returns `"High"` if its current voltage is `>= operatingVoltage / 2`, otherwise `"Low"`.
> - **Analog Write / Read**: Writes and reads raw voltages (e.g., `"2.5"`). The `AdcController` and `DacController` shims calculate the raw register values using the board's configuration (resolution and reference voltage) fetched out-of-band from the simulator.
> - **Driver Simplicity**: The drivers (`WebAPIDriver`, `LocalDriver`) do not need to understand voltages or perform any threshold/ratio conversions. They pass raw string values between the client application and the engine.

---

## Proposed Changes

### Component 1: Board Schemas & Metadata

#### [MODIFY] [arduino_uno.gsc](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/components/arduino_uno.gsc)
- Add `"operatingVoltage": 5.0` at the root level.
- Add `"adcChannel"` field to analog pins (A0-A5, physical pins 23-28) representing ADC channel indices `0` to `5`.
- Add `"adc"` configuration block at the root level:
  ```json
  "adc": {
    "resolutionInBits": 10,
    "minValue": 0,
    "maxValue": 1023,
    "referenceVoltage": 5.0
  }
  ```

#### [NEW] [esp32.gsc](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/components/esp32.gsc)
- Create a beautiful geo-mapped SVG layout for an ESP32 DevKitC (38-pin layout).
- Set `"operatingVoltage": 3.3` at the root level.
- Map the ESP32's ADC channels to the corresponding GPIO pins (e.g., GPIO 36 -> ADC Channel 0, GPIO 39 -> ADC Channel 3, etc.) using `"adcChannel"`.
- Map the ESP32's DAC channels to the corresponding GPIO pins (GPIO 25 -> DAC Channel 0, GPIO 26 -> DAC Channel 1) using `"dacChannel"`.
- Add `"adc"` and `"dac"` configuration blocks at the root level:
  ```json
  "adc": {
    "resolutionInBits": 12,
    "minValue": 0,
    "maxValue": 4095,
    "referenceVoltage": 3.3
  },
  "dac": {
    "resolutionInBits": 8,
    "minValue": 0,
    "maxValue": 255,
    "referenceVoltage": 3.3
  }
  ```

---

### Component 2: Simulator Web UI

#### [MODIFY] [style.css](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/style.css)
- Add styling for `.pin-hotspot.gpio.mode-analoginput` and `.pin-hotspot.gpio.mode-analogoutput`.
- Add styling for the custom range slider inside the tooltip (`#tooltip-state-slider`).
- Add styling for the custom analog output progress indicator.
- Add styling for `.badge.mode-analoginput` and `.badge.mode-analogoutput`.
- Implement light theme overrides for the new elements.

#### [MODIFY] [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js)
- Register `"AnalogInput"` and `"AnalogOutput"` as supported modes.
- Update `updatePinVisuals` to set a CSS custom property `--analog-intensity` on the hotspot based on the value ratio (`0.0` - `1.0`).
- Update `refreshTooltipContent` to render:
  - A premium range slider control when a pin is in `"AnalogInput"` mode, allowing the user to select absolute voltage between `0` and `operatingVoltage`.
  - A read-only voltage/percentage progress bar when a pin is in `"AnalogOutput"` mode.
- Bind the range slider's `oninput` event to send new voltage values to the server via WebSocket.

---

### Component 3: GpioSimulator.Common & Web API

#### [MODIFY] [PinState.cs](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Common/PinState.cs)
- Keep state representation generic. The `Value` field stores the raw string state (which can be `"High"`, `"Low"`, or a voltage string like `"2.5"`).

#### [MODIFY] [GpioSimulatorEngine.cs](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Common/GpioSimulatorEngine.cs)
- Add tracking for the active board's `operatingVoltage` (parsed when loading a board mapping layout).
- Update `WritePin` to map `"High"` and `"Low"` writes to absolute operating voltages internally if the mode or schema suggests, or store the string directly and evaluate on read.
- Update `ReadPin` to dynamically evaluate the read value:
  - If the pin is in a digital mode, and the current value is a float string, evaluate threshold: `voltage >= operatingVoltage / 2 ? "High" : "Low"`.
  - Otherwise, return the stored value string directly.

---

### Component 4: System.Device.Adc Assembly (Shim)

#### [NEW] [System.Device.Adc.csproj](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Adc/System.Device.Adc.csproj)
- Create a new project targeting `netstandard2.0` referencing `DevDecoder.GpioSimulator.Common` or using standard networking.

#### [NEW] [AdcChannelMode.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Adc/AdcChannelMode.cs)
- Implement `System.Device.Adc.AdcChannelMode` enum (`SingleEnded` and `Differential`).

#### [NEW] [AdcControllerBase.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Adc/AdcControllerBase.cs)
- Implement the abstract `System.Device.Adc.AdcControllerBase` class representing the nanoFramework contract.

#### [NEW] [AdcChannelBase.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Adc/AdcChannelBase.cs)
- Implement the abstract `System.Device.Adc.AdcChannelBase` class.

#### [NEW] [AdcController.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Adc/AdcController.cs)
- Implement the `System.Device.Adc.AdcController` class with `GetDefault()` and `OpenChannel(int channelNumber)`.
- Connects to the simulator via WebSocket to read input values (voltages).
- Automatically determines resolution and mapping from the active board schema.

#### [NEW] [AdcChannel.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Adc/AdcChannel.cs)
- Implement `ReadRatio()` (voltage divided by reference voltage) and `ReadValue()` (ratio scaled to resolution).

---

### Component 5: System.Device.Dac Assembly (Shim)

#### [NEW] [System.Device.Dac.csproj](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Dac/System.Device.Dac.csproj)
- Create a new project targeting `netstandard2.0`.

#### [NEW] [DacController.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Dac/DacController.cs)
- Implement `System.Device.Dac.DacController` with `GetDefault()` and `OpenChannel(int channelNumber)`.

#### [NEW] [DacChannel.cs](file:///Users/craigdean/Repos/GpioSimulator/src/System.Device.Dac/DacChannel.cs)
- Implement `WriteRatio(double ratio)` and `WriteValue(ushort value)` to output analog signals.
- Calculates absolute voltage outputs based on resolution and reference voltage, sending the write command to the simulator WebSocket server.

---

## Verification Plan

### Automated Tests
- Run `dotnet build` to ensure the new projects compile successfully.
- Add unit tests in `src/tests/DevDecoder.GpioSimulator.Tests/AdcControllerTests.cs` and `src/tests/DevDecoder.GpioSimulator.Tests/DacControllerTests.cs` using the simulation engine to verify:
  - `AdcController` reads and scales values based on board configuration.
  - `DacController` writes values and registers the output states correctly.

### Manual Verification
- Spin up the web app, select the new "ESP32 DevKitC" board.
- Open analog inputs and outputs, verify that the UI shows functional sliders (for inputs) and real-time voltage/percentage readouts (for outputs) with glowing intensity.
