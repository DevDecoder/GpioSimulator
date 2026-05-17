# System.Device.Gpio API Compatibility Audit

This document provides a highly rigorous comparison between the current custom `System.Device.Gpio` simulator shim implementation and the official Microsoft .NET IoT [System.Device.Gpio NuGet package](https://www.nuget.org/packages/System.Device.Gpio/).

The primary goal of this audit is to identify any API surface differences and establish concrete recommendations to make the simulator a truly seamless, 100% compliant drop-in replacement for production applications.

---

## Side-by-Side API Comparison

### 1. `System.Device.Gpio.GpioController`

| Official API Member | Local Simulator Implementation | Compatibility Status / Action |
| :--- | :--- | :--- |
| **Constructors** | | |
| `GpioController()` | `GpioController()` | **COMPATIBLE** |
| `GpioController(PinNumberingScheme numberingScheme)` | *Missing* | **MISSING** — Add numbering scheme tracking. |
| `GpioController(PinNumberingScheme numberingScheme, GpioDriver driver)` | *Missing* | **MISSING** — Add stubbed driver overload to avoid compiler failures for custom drivers. |
| **Properties** | | |
| `public virtual int PinCount { get; }` | *Missing* | **MISSING** — Add simulated pin count (e.g. default 40). |
| `public PinNumberingScheme NumberingScheme { get; }` | *Missing* | **MISSING** — Add read-only numbering scheme property. |
| **Core Pin Operations** | | |
| `OpenPin(int pinNumber)` | *Missing* | **MISSING** — Add overload default to opening as `PinMode.Input`. |
| `OpenPin(int pinNumber, PinMode mode)` | `OpenPin(int pinNumber, PinMode mode)` | **COMPATIBLE** |
| `OpenPin(int pinNumber, PinMode mode, PinValue initialValue)` | *Missing* | **MISSING** — Add overload with initial output value to prevent standard bootstrap errors. |
| `ClosePin(int pinNumber)` | `ClosePin(int pinNumber)` | **COMPATIBLE** |
| `Write(int pinNumber, PinValue value)` | `Write(int pinNumber, PinValue value)` | **COMPATIBLE** |
| `Write(ReadOnlySpan<PinValuePair> pinValuePairs)` | *Missing* | **MISSING** — Add high-performance batch write support. |
| `Read(int pinNumber)` | `Read(int pinNumber)` | **COMPATIBLE** (returns `PinValue`) |
| `Read(Span<PinValuePair> pinValuePairs)` | *Missing* | **MISSING** — Add high-performance batch read support. |
| `SetPinMode(int pinNumber, PinMode mode)` | `SetPinMode(int pinNumber, PinMode mode)` | **COMPATIBLE** |
| `GetPinMode(int pinNumber)` | *Missing* | **MISSING** — Return current mode of open pin. |
| `IsPinModeSupported(int pinNumber, PinMode mode)` | *Missing* | **MISSING** — Return `true` (simulator virtually supports all modes). |
| `IsPinOpen(int pinNumber)` | *Missing* | **MISSING** — Return whether the pin index is open. |
| **Event Callbacks & Listener Loop** | | |
| `RegisterCallbackForPinValueChangedEvent(...)` | *Missing* | **MISSING** — Crucial for push-based interrupt simulations. |
| `UnregisterCallbackForPinValueChangedEvent(...)` | *Missing* | **MISSING** — Unregister callback delegates. |
| `WaitForEvent(int, PinEventTypes, TimeSpan)` | *Missing* | **MISSING** — Synchronous thread block waiting for state change. |
| `WaitForEvent(int, PinEventTypes, CancellationToken)`| *Missing* | **MISSING** — Cancellation-token gated wait block. |

### 2. Supporting Types & Enums

#### A. `PinMode` (Enum)
* **Official Values**: `Input` (0), `Output` (1), `InputPullUp` (2), `InputPullDown` (3).
* **Local Implementation**: `Input` (0), `Output` (1), `InputPullUp` (2), `InputPullDown` (3).
* **Status**: **100% COMPATIBLE**.

#### B. `PinValue` (Struct)
* **Official Structure**: A readonly struct representing High (1) or Low (0). Overloads comparison and equals.
* **Local Implementation**: A struct matching implicit conversions to `bool` and `int`, carrying standard comparison logic.
* **Status**: **COMPATIBLE** (We have already added full unit test coverage confirming exact parity).

#### C. `PinNumberingScheme` (Enum) — **MISSING**
* Represents the physical board layout pin scheme vs logical system-on-chip scheme.
```csharp
namespace System.Device.Gpio
{
    public enum PinNumberingScheme
    {
        Logical = 0,
        Board = 1
    }
}
```

#### D. `PinValuePair` (Struct) — **MISSING**
* Represents a pair containing a pin number and its current value.
```csharp
namespace System.Device.Gpio
{
    public readonly struct PinValuePair
    {
        public PinValuePair(int pinNumber, PinValue pinValue)
        {
            PinNumber = pinNumber;
            PinValue = pinValue;
        }

        public int PinNumber { get; }
        public PinValue PinValue { get; }

        public void Deconstruct(out int pinNumber, out PinValue pinValue)
        {
            pinNumber = PinNumber;
            pinValue = PinValue;
        }
    }
}
```

#### E. `PinEventTypes` (Enum) — **MISSING**
* Flags enum representing edge transitions (Rising, Falling, None).
```csharp
using System;

namespace System.Device.Gpio
{
    [Flags]
    public enum PinEventTypes
    {
        None = 0,
        Rising = 1,
        Falling = 2
    }
}
```

#### F. `PinValueChangedEventArgs` (Class) & Delegates — **MISSING**
* Carries event arguments when an input pin transitions edge state.
```csharp
using System;

namespace System.Device.Gpio
{
    public class PinValueChangedEventArgs : EventArgs
    {
        public PinValueChangedEventArgs(PinEventTypes changeType, int pinNumber)
        {
            ChangeType = changeType;
            PinNumber = pinNumber;
        }

        public PinEventTypes ChangeType { get; }
        public int PinNumber { get; }
    }

    public delegate void PinChangeEventHandler(object sender, PinValueChangedEventArgs args);
}
```

---

## Impact of Missing APIs

1. **Compiler Breakers**:
   Production scripts often open pins specifying an initial value (e.g. `controller.OpenPin(3, PinMode.Output, PinValue.High)`). Missing these overloads causes compiler errors during dropped-in migration.
2. **Event Loop Failures**:
   Sensors and switches almost always hook up to interrupts using event registration (e.g. `controller.RegisterCallbackForPinValueChangedEvent(...)`) or synchronous blocking transitions (`controller.WaitForEvent(...)`). Without event loop simulation, button inputs from the Web UI can only be read by active polling loops, rather than native event-driven handlers.

---

## Compatibility Enhancement Recommendations

We recommend implementing the missing API structures across a new dedicated task:

### Phase 1: Support Core Overloads, Enums & Structs
1. **Create `PinNumberingScheme.cs`**: Add the enum containing `Logical` and `Board`.
2. **Create `PinEventTypes.cs`**: Add the edge-triggered flags enum.
3. **Create `PinValuePair.cs`**: Add the readonly struct representation.
4. **Create `PinValueChangedEventArgs.cs`**: Add event arguments and delegate handlers.
5. **Enrich `GpioController.cs`**:
   * Add constructors supporting numbering scheme configuration.
   * Expose properties: `PinCount` (defaulting to 40) and `NumberingScheme`.
   * Add overloaded methods for `OpenPin(int pinNumber)`, `OpenPin(int pinNumber, PinMode mode, PinValue initialValue)`, `IsPinOpen`, `IsPinModeSupported`, `GetPinMode`.

### Phase 2: Add Event Callback & Interrupt Simulation
1. **Add Callback Registry**:
   Maintain a registry of `PinChangeEventHandler` callbacks mapped to pins and event types inside `GpioController.cs`.
2. **Handle Inbound UI Transitions**:
   When the WebSocket background reader thread receives a state change from the browser for an input pin:
   * Evaluate if it is a Rising edge (Low -> High) or Falling edge (High -> Low).
   * Fire the registered delegates asynchronously or on a thread-pool thread to replicate production interrupt behavior!
3. **Implement Blocking `WaitForEvent`**:
   Use `TaskCompletionSource` or thread signaling primitives to support synchronous/cancellationToken-driven event waits (`WaitForEvent`).
