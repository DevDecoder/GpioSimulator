using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Tasks;

namespace DevDecoder.GpioSimulator.Sample
{
    class Program
    {
        private static readonly object _consoleLock = new object();
        private static GpioController _controller = null!;

        static void Main(string[] args)
        {
            ReadLine.AutoCompletionHandler = new CommandAutoCompleteHandler();
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("==================================================");
                Console.WriteLine("      DevDecoder GPIO Board Simulator Sample      ");
                Console.WriteLine("==================================================");
                Console.ResetColor();
                Console.WriteLine("Booting simulator...");
            }

            PinNumberingScheme defaultScheme = PinNumberingScheme.Board;

            // Parse args
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--scheme" || args[i] == "-s") && i + 1 < args.Length)
                {
                    string val = args[i + 1].ToLower();
                    if (val == "board" || val == "b")
                    {
                        defaultScheme = PinNumberingScheme.Board;
                    }
                    else if (val == "logical" || val == "l")
                    {
                        defaultScheme = PinNumberingScheme.Logical;
                    }
                }
            }

            _controller = new GpioController(defaultScheme);

            // Default startup configuration matching Pi 5 test
            OpenPin(_controller, 3, PinMode.Output);
            OpenPin(_controller, 5, PinMode.Input);

            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[Ready] Web UI spun up at: http://127.0.0.1:5050");
                Console.ResetColor();
                Console.WriteLine("Type 'help' to see list of interactive commands.");
                PrintPinsStatus(_controller);
            }

            while (true)
            {
                string input = ReadLine.Read("GpioSim> ");
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                ReadLine.AddHistory(input);

                string[] parts = input.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string cmd = parts[0].ToLower();

                if (cmd == "exit" || cmd == "quit" || cmd == "q")
                {
                    _controller.Dispose();
                    break;
                }

                lock (_consoleLock)
                {
                    try
                    {
                        var controller = _controller;
                        switch (cmd)
                        {
                            case "scheme":
                            case "schema":
                            case "sc":
                                if (parts.Length < 2)
                                {
                                    WriteError("Error: Please specify scheme. Syntax: scheme <logical|board>");
                                    break;
                                }
                                string newSchemeStr = parts[1].ToLower();
                                PinNumberingScheme? newScheme = null;
                                if (newSchemeStr == "logical" || newSchemeStr == "l")
                                {
                                    newScheme = PinNumberingScheme.Logical;
                                }
                                else if (newSchemeStr == "board" || newSchemeStr == "b")
                                {
                                    newScheme = PinNumberingScheme.Board;
                                }

                                if (newScheme == null)
                                {
                                    WriteError($"Error: Invalid scheme '{parts[1]}'. Use 'logical' or 'board'.");
                                    break;
                                }

                                if (newScheme == _controller.NumberingScheme)
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"Already using scheme: {newScheme}");
                                    Console.ResetColor();
                                    break;
                                }

                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"Switching numbering scheme to {newScheme}... Resetting simulator state.");
                                Console.ResetColor();

                                // 1. Dispose old controller
                                _controller.Dispose();

                                // 2. Start fresh controller
                                _controller = new GpioController(newScheme.Value);

                                // 3. Setup default pins under new scheme
                                OpenPin(_controller, 3, PinMode.Output);
                                OpenPin(_controller, 5, PinMode.Input);

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"Successfully initialized GpioController under scheme: {newScheme}");
                                Console.ResetColor();
                                PrintPinsStatus(_controller);
                                break;
                            case "help":
                            case "h":
                                PrintHelp();
                                break;
                            case "status":
                            case "s":
                                PrintPinsStatus(controller);
                                break;
                            case "open":
                            case "o":
                                if (parts.Length < 3)
                                {
                                    WriteError("Error: Please specify pin and mode. Syntax: open <pin> <in|out|pullup|pulldown>");
                                    break;
                                }
                                if (int.TryParse(parts[1], out int openPin))
                                {
                                    string modeStr = parts[2].ToLower();
                                    PinMode mode;
                                    if (modeStr == "out" || modeStr == "output")
                                    {
                                        mode = PinMode.Output;
                                    }
                                    else if (modeStr == "pullup" || modeStr == "inputpullup" || modeStr == "pu")
                                    {
                                        mode = PinMode.InputPullUp;
                                    }
                                    else if (modeStr == "pulldown" || modeStr == "inputpulldown" || modeStr == "pd")
                                    {
                                        mode = PinMode.InputPullDown;
                                    }
                                    else
                                    {
                                        mode = PinMode.Input;
                                    }
                                    OpenPin(controller, openPin, mode);
                                    Console.WriteLine($"Successfully opened Pin {openPin} as {mode}.");
                                }
                                else
                                {
                                    WriteError("Error: Invalid pin number.");
                                }
                                break;
                            case "close":
                            case "c":
                                if (parts.Length < 2 || !int.TryParse(parts[1], out int closePin))
                                {
                                    WriteError("Error: Please specify pin number. Syntax: close <pin>");
                                    break;
                                }
                                ClosePin(controller, closePin);
                                Console.WriteLine($"Successfully closed Pin {closePin}.");
                                break;
                            case "write":
                            case "w":
                                if (parts.Length < 3)
                                {
                                    WriteError("Error: Please specify pin and value. Syntax: write <pin> <1|0|high|low>");
                                    break;
                                }
                                if (int.TryParse(parts[1], out int writePin))
                                {
                                    string valStr = parts[2].ToLower();
                                    PinValue val = valStr == "1" || valStr == "high" || valStr == "h" ? PinValue.High : PinValue.Low;
                                    controller.Write(writePin, val);
                                    Console.WriteLine($"Wrote {val} to Pin {writePin}.");
                                }
                                else
                                {
                                    WriteError("Error: Invalid pin number.");
                                }
                                break;
                            case "read":
                            case "r":
                                if (parts.Length < 2 || !int.TryParse(parts[1], out int readPin))
                                {
                                    WriteError("Error: Please specify pin number. Syntax: read <pin>");
                                    break;
                                }
                                PinValue readVal = controller.Read(readPin);
                                Console.WriteLine($"Pin {readPin} value is: {readVal}");
                                break;
                            case "setmode":
                            case "sm":
                            case "set":
                                if (parts.Length < 3)
                                {
                                    WriteError("Error: Please specify pin and mode. Syntax: setmode <pin> <in|out|pullup|pulldown>");
                                    break;
                                }
                                if (int.TryParse(parts[1], out int smPin))
                                {
                                    string modeStr = parts[2].ToLower();
                                    PinMode mode;
                                    if (modeStr == "out" || modeStr == "output")
                                    {
                                        mode = PinMode.Output;
                                    }
                                    else if (modeStr == "pullup" || modeStr == "inputpullup" || modeStr == "pu")
                                    {
                                        mode = PinMode.InputPullUp;
                                    }
                                    else if (modeStr == "pulldown" || modeStr == "inputpulldown" || modeStr == "pd")
                                    {
                                        mode = PinMode.InputPullDown;
                                    }
                                    else
                                    {
                                        mode = PinMode.Input;
                                    }

                                    // Unregister first if previously input
                                    try
                                    {
                                        controller.UnregisterCallbackForPinValueChangedEvent(smPin, OnPinValueChanged);
                                    }
                                    catch {}

                                    controller.SetPinMode(smPin, mode);

                                    // Register if it became input
                                    if (mode == PinMode.Input || mode == PinMode.InputPullUp || mode == PinMode.InputPullDown)
                                    {
                                        controller.RegisterCallbackForPinValueChangedEvent(smPin, PinEventTypes.Rising | PinEventTypes.Falling, OnPinValueChanged);
                                    }

                                    Console.WriteLine($"Successfully set Pin {smPin} mode to {mode}.");
                                }
                                else
                                {
                                    WriteError("Error: Invalid pin number.");
                                }
                                break;
                            case "isopen":
                            case "io":
                                if (parts.Length < 2 || !int.TryParse(parts[1], out int ioPin))
                                {
                                    WriteError("Error: Please specify pin number. Syntax: isopen <pin>");
                                    break;
                                }
                                bool isPinOpen = controller.IsPinOpen(ioPin);
                                Console.WriteLine($"Pin {ioPin} open status: {isPinOpen}");
                                break;
                            case "issupported":
                            case "is":
                                if (parts.Length < 3)
                                {
                                    WriteError("Error: Please specify pin and mode. Syntax: issupported <pin> <in|out|pullup|pulldown>");
                                    break;
                                }
                                if (int.TryParse(parts[1], out int suppPin))
                                {
                                    string modeStr = parts[2].ToLower();
                                    PinMode mode;
                                    if (modeStr == "out" || modeStr == "output")
                                    {
                                        mode = PinMode.Output;
                                    }
                                    else if (modeStr == "pullup" || modeStr == "inputpullup" || modeStr == "pu")
                                    {
                                        mode = PinMode.InputPullUp;
                                    }
                                    else if (modeStr == "pulldown" || modeStr == "inputpulldown" || modeStr == "pd")
                                    {
                                        mode = PinMode.InputPullDown;
                                    }
                                    else
                                    {
                                        mode = PinMode.Input;
                                    }
                                    bool supported = controller.IsPinModeSupported(suppPin, mode);
                                    Console.WriteLine($"Pin {suppPin} mode {mode} supported: {supported}");
                                }
                                else
                                {
                                    WriteError("Error: Invalid pin number.");
                                }
                                break;
                            default:
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"Unknown command: '{cmd}'. Type 'help' for support.");
                                Console.ResetColor();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteError($"Execution error: {ex.Message}");
                    }
                }
            }

            Console.WriteLine("Shutting down sample program. Goodbye!");
        }

        private static void WriteError(string message)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        private static void OpenPin(GpioController controller, int pin, PinMode mode)
        {
            controller.OpenPin(pin, mode);
            if (mode == PinMode.Input || mode == PinMode.InputPullUp || mode == PinMode.InputPullDown)
            {
                controller.RegisterCallbackForPinValueChangedEvent(pin, PinEventTypes.Rising | PinEventTypes.Falling, OnPinValueChanged);
            }
        }

        private static void ClosePin(GpioController controller, int pin)
        {
            try
            {
                controller.UnregisterCallbackForPinValueChangedEvent(pin, OnPinValueChanged);
            }
            catch {}
            controller.ClosePin(pin);
        }

        private static void PrintPrompt()
        {
            Console.Write("\nGpioSim> ");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("\nAvailable Interactive Commands:");
            Console.WriteLine("  open <pin> <in|out|pullup|pulldown>  - Open a pin with specified mode");
            Console.WriteLine("  close <pin>                          - Close an open pin");
            Console.WriteLine("  write <pin> <1|0|h|l>                - Write High/Low to an output pin (e.g. write 3 1)");
            Console.WriteLine("  read <pin>                           - Read the current value of an open pin");
            Console.WriteLine("  setmode <pin> <mode>                 - Change the mode of an open pin");
            Console.WriteLine("  isopen <pin>                         - Check if a pin is currently open");
            Console.WriteLine("  issupported <pin> <mode>             - Check if a pin mode is supported");
            Console.WriteLine("  scheme <logical|board>               - Switch dynamic pin numbering scheme (e.g. scheme board)");
            Console.WriteLine("  status                               - Display status of all currently opened pins");
            Console.WriteLine("  help                                 - Show this guide");
            Console.WriteLine("  exit | quit | q                      - Terminate the simulation program");
        }

        private static void PrintPinsStatus(GpioController controller)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n--- Current Open Pins Status (Scheme: {controller.NumberingScheme}) ---");
            
            bool anyOpen = false;
            // Standard Raspberry Pi headers have 40 pins. Loop through 1 to 40 to query open pins.
            for (int pin = 1; pin <= 40; pin++)
            {
                try
                {
                    if (controller.IsPinOpen(pin))
                    {
                        anyOpen = true;
                        PinMode mode = controller.GetPinMode(pin);
                        PinValue currentVal = controller.Read(pin);
                        Console.WriteLine($"  * Pin {pin.ToString().PadRight(2)} | Mode: {mode.ToString().PadRight(6)} | Value: {currentVal}");
                    }
                }
                catch {}
            }

            if (!anyOpen)
            {
                Console.WriteLine("No pins are currently open on this controller.");
            }
            Console.WriteLine("--------------------------------");
            Console.ResetColor();
        }

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
    }
}
