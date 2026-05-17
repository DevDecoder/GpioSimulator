using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Tasks;

namespace DevDecoder.GpioSimulator.Sample
{
    class Program
    {
        private static readonly Dictionary<int, PinMode> _pins = new Dictionary<int, PinMode>();
        private static readonly Dictionary<int, PinValue> _lastValues = new Dictionary<int, PinValue>();
        private static readonly object _consoleLock = new object();

        static void Main(string[] args)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("==================================================");
                Console.WriteLine("      DevDecoder GPIO Board Simulator Sample      ");
                Console.WriteLine("==================================================");
                Console.ResetColor();
                Console.WriteLine("Booting simulator...");
            }

            using var controller = new GpioController();

            // Default startup configuration matching Pi 5 test
            OpenPin(controller, 3, PinMode.Output);
            OpenPin(controller, 5, PinMode.Input);

            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[Ready] Web UI spun up at: http://127.0.0.1:5050");
                Console.ResetColor();
                Console.WriteLine("Type 'help' to see list of interactive commands.");
                PrintPinsStatus(controller);
                PrintPrompt();
            }

            // Background task to watch for dynamic input transitions from the Web UI
            var watcherCts = new CancellationTokenSource();
            var watcherTask = Task.Run(() => WatchInputPins(controller, watcherCts.Token));

            while (true)
            {
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    lock (_consoleLock)
                    {
                        PrintPrompt();
                    }
                    continue;
                }

                string[] parts = input.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string cmd = parts[0].ToLower();

                if (cmd == "exit" || cmd == "quit" || cmd == "q")
                {
                    watcherCts.Cancel();
                    try
                    {
                        watcherTask.Wait();
                    }
                    catch
                    {
                        // Absorb
                    }
                    break;
                }

                lock (_consoleLock)
                {
                    try
                    {
                        switch (cmd)
                        {
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
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Error: Please specify pin and mode. Syntax: open <pin> <in|out>");
                                    Console.ResetColor();
                                    break;
                                }
                                if (int.TryParse(parts[1], out int openPin))
                                {
                                    string modeStr = parts[2].ToLower();
                                    PinMode mode = modeStr == "in" || modeStr == "input" ? PinMode.Input : PinMode.Output;
                                    OpenPin(controller, openPin, mode);
                                    Console.WriteLine($"Successfully opened Pin {openPin} as {mode}.");
                                }
                                else
                                {
                                    Console.WriteLine("Error: Invalid pin number.");
                                }
                                break;
                            case "close":
                            case "c":
                                if (parts.Length < 2 || !int.TryParse(parts[1], out int closePin))
                                {
                                    Console.WriteLine("Error: Please specify pin number. Syntax: close <pin>");
                                    break;
                                }
                                ClosePin(controller, closePin);
                                Console.WriteLine($"Successfully closed Pin {closePin}.");
                                break;
                            case "write":
                            case "w":
                                if (parts.Length < 3)
                                {
                                    Console.WriteLine("Error: Please specify pin and value. Syntax: write <pin> <1|0|high|low>");
                                    break;
                                }
                                if (int.TryParse(parts[1], out int writePin))
                                {
                                    bool isOpen = false;
                                    PinMode currentMode = PinMode.Input;
                                    
                                    lock (_pins)
                                    {
                                        isOpen = _pins.TryGetValue(writePin, out currentMode);
                                    }

                                    if (!isOpen)
                                    {
                                        Console.WriteLine($"Error: Pin {writePin} is not open. Use 'open {writePin} out' first.");
                                        break;
                                    }
                                    if (currentMode != PinMode.Output)
                                    {
                                        Console.WriteLine($"Error: Pin {writePin} is configured as Input. Cannot write to it.");
                                        break;
                                    }
                                    string valStr = parts[2].ToLower();
                                    PinValue val = valStr == "1" || valStr == "high" || valStr == "h" ? PinValue.High : PinValue.Low;
                                    controller.Write(writePin, val);
                                    lock (_lastValues)
                                    {
                                        _lastValues[writePin] = val;
                                    }
                                    Console.WriteLine($"Wrote {val} to Pin {writePin}.");
                                }
                                else
                                {
                                    Console.WriteLine("Error: Invalid pin number.");
                                }
                                break;
                            case "read":
                            case "r":
                                if (parts.Length < 2 || !int.TryParse(parts[1], out int readPin))
                                {
                                    Console.WriteLine("Error: Please specify pin number. Syntax: read <pin>");
                                    break;
                                }
                                bool isReadOpen = false;
                                lock (_pins)
                                {
                                    isReadOpen = _pins.ContainsKey(readPin);
                                }
                                if (!isReadOpen)
                                {
                                    Console.WriteLine($"Error: Pin {readPin} is not open.");
                                    break;
                                }
                                PinValue readVal = controller.Read(readPin);
                                Console.WriteLine($"Pin {readPin} value is: {readVal}");
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
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Execution error: {ex.Message}");
                        Console.ResetColor();
                    }

                    PrintPrompt();
                }
            }

            Console.WriteLine("Shutting down sample program. Goodbye!");
        }

        private static void OpenPin(GpioController controller, int pin, PinMode mode)
        {
            controller.OpenPin(pin, mode);
            lock (_pins)
            {
                _pins[pin] = mode;
            }
            lock (_lastValues)
            {
                _lastValues[pin] = controller.Read(pin);
            }
        }

        private static void ClosePin(GpioController controller, int pin)
        {
            controller.ClosePin(pin);
            lock (_pins)
            {
                _pins.Remove(pin);
            }
            lock (_lastValues)
            {
                _lastValues.Remove(pin);
            }
        }

        private static void PrintPrompt()
        {
            Console.Write("\nGpioSim> ");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("\nAvailable Interactive Commands:");
            Console.WriteLine("  open <pin> <in|out>      - Open a pin with specified mode (e.g. open 7 out)");
            Console.WriteLine("  close <pin>              - Close an open pin");
            Console.WriteLine("  write <pin> <1|0|h|l>    - Write High/Low to an output pin (e.g. write 3 1)");
            Console.WriteLine("  read <pin>               - Read the current value of an open pin");
            Console.WriteLine("  status                   - Display status of all currently opened pins");
            Console.WriteLine("  help                     - Show this guide");
            Console.WriteLine("  exit | quit | q          - Terminate the simulation program");
        }

        private static void PrintPinsStatus(GpioController controller)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n--- Current Open Pins Status ---");
            int pinCount = 0;
            lock (_pins)
            {
                pinCount = _pins.Count;
            }

            if (pinCount == 0)
            {
                Console.WriteLine("No pins are currently open.");
            }
            else
            {
                List<KeyValuePair<int, PinMode>> sortedPins;
                lock (_pins)
                {
                    sortedPins = new List<KeyValuePair<int, PinMode>>(_pins);
                }
                sortedPins.Sort((a, b) => a.Key.CompareTo(b.Key));

                foreach (var kvp in sortedPins)
                {
                    PinValue currentVal = controller.Read(kvp.Key);
                    Console.WriteLine($"  * Pin {kvp.Key.ToString().PadRight(2)} | Mode: {kvp.Value.ToString().PadRight(6)} | Value: {currentVal}");
                }
            }
            Console.WriteLine("--------------------------------");
            Console.ResetColor();
        }

        private static void WatchInputPins(GpioController controller, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Copy pin keys under lock or safe enumeration to prevent concurrent mods
                    List<int> openInputPins = new List<int>();
                    lock (_pins)
                    {
                        foreach (var kvp in _pins)
                        {
                            if (kvp.Value == PinMode.Input)
                            {
                                openInputPins.Add(kvp.Key);
                            }
                        }
                    }

                    foreach (var pin in openInputPins)
                    {
                        PinValue val = controller.Read(pin);
                        bool hasLastVal = false;
                        PinValue lastVal = PinValue.Low;

                        lock (_lastValues)
                        {
                            if (_lastValues.TryGetValue(pin, out lastVal))
                            {
                                hasLastVal = true;
                            }
                        }

                        if (hasLastVal && val != lastVal)
                        {
                            lock (_consoleLock)
                            {
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write($"\n[ALERT] Input state changed: Pin {pin} is now {val}!");
                                Console.ResetColor();
                                PrintPrompt();
                            }

                            lock (_lastValues)
                            {
                                _lastValues[pin] = val;
                            }
                        }
                    }

                    Thread.Sleep(100);
                }
            }
            catch
            {
                // Silently shut down watcher thread
            }
        }
    }
}
