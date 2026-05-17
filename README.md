# Gpio Simulator (System.Device.Gpio Shim)

An extensible, drop-in C# NuGet replacement library for `System.Device.Gpio` that mimics the hardware namespace, but spins up a beautiful browser-based microcontroller visual simulator rather than requiring physical hardware. Designed for teaching, desktop prototyping, and locked-down learning environments.

---

## Features
* **Drop-in Compatibility**: Uses the identical namespace and APIs as `System.Device.Gpio` (e.g. `GpioController`, `PinMode`, `PinValue`).
* **Multi-Board Extensibility**: Supports simulating multiple different boards (e.g. Raspberry Pi 5, Raspberry Pi 4, Arduino Uno) using metadata-driven JSON Board Schemas.
* **Zero Admin / Firewall Prompts**: Binds strictly to `127.0.0.1` (loopback) to prevent Windows Defender and firewall dialogs on restricted school PCs.
* **Modern ASP.NET Core UI**: Web interface built on `.NET 8.0` with WebSockets for real-time, low-latency visual feedback of pin states.
* **Auto-Spawning**: Automatically launches the local web server on the first `GpioController` instantiation and opens your system's default browser automatically.

---

## Directory Structure

```
GpioSimulator/
├── GpioSimulator.sln                # Visual Studio 2022 Solution
├── docs/
│   └── superpowers/
│       └── specs/
│           └── 2026-05-17-gpio-simulator-design.md   # Extensible Design Spec
└── src/
    ├── System.Device.Gpio/          # The Shim Library (.NET Standard 2.0)
    └── DevDecoder.GpioSimulator.Web/ # The Web Simulator UI (.NET 8.0)
```

---

## Prerequisites
* **IDE**: Visual Studio 2022, JetBrains Rider, or VS Code.
* **Runtime**: .NET SDK 8.0 or above.

---

## How to Build

Open the root folder in your terminal or IDE:

1. **Restore dependencies**:
   ```bash
   dotnet restore
   ```
2. **Build the solution**:
   ```bash
   dotnet build
   ```

---

## License
MIT License. Created by DevDecoder.
