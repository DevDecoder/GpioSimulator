# Gpio Simulator (System.Device.Gpio Shim)

An extensible, drop-in C# NuGet replacement library for `System.Device.Gpio` that mimics the hardware namespace, but spins up a beautiful browser-based microcontroller visual simulator rather than requiring physical hardware. Designed for teaching, desktop prototyping, and locked-down learning environments.

![GPIO Simulator Workspace](docs/screenshot.png)

---

## Features
* **Drop-in Compatibility**: Uses the identical namespace and APIs as `System.Device.Gpio` (e.g. `GpioController`, `PinMode`, `PinValue`).
* **Interactive Workspace Canvas**: Elegant, high-performance **Pan & Zoom** controls (drag to pan, mousewheel or floating toolbar buttons to zoom, single-click to perfectly fit the board to the workspace screen).
* **Workspace Toolbar**: Select between the standard **Move** tool and **Inspect** tool, with dynamic cursor states and full highlighting/drag selection prevention on controls.
* **Real-time ASP.NET Core UI**: Real-time bidirectional pin synchronization using lightweight WebSockets on `.NET 8.0`.
* **Dockable / Toggleable Panels**: Hide or show the Components List and active Log Terminal panes on demand.
* **Multi-Board Extensibility**: Supports simulating multiple different boards (e.g. Raspberry Pi 5, Raspberry Pi 4, Arduino Uno) using metadata-driven JSON Board Schemas.
* **Zero Admin / Firewall Prompts**: Binds strictly to `127.0.0.1` (loopback) to prevent Windows Defender and firewall dialogs on restricted school PCs.
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
