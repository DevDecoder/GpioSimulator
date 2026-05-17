using System;
using System.Device.Gpio;
using System.Threading;

namespace DevDecoder.GpioSimulator.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Starting DevDecoder Gpio Simulator Test ===");
            using var controller = new GpioController();
            
            // Open Pin 3 as Output (LED)
            controller.OpenPin(3, PinMode.Output);
            
            // Open Pin 5 as Input (momentary button)
            controller.OpenPin(5, PinMode.Input);

            Console.WriteLine("Pin 3 opened as Output, Pin 5 opened as Input.");
            Console.WriteLine("Observe the Web UI: dynamic board and LEDs should appear.");
            Console.WriteLine("Press Ctrl+C to terminate test.");

            bool ledState = false;

            while (true)
            {
                // 1. Read Button State from UI
                PinValue buttonState = controller.Read(5);
                
                // 2. Output to console on press
                if (buttonState == PinValue.High)
                {
                    Console.WriteLine($"[Console Log] Button (Pin 5) is ACTIVE!");
                }

                // 3. Toggle LED
                ledState = !ledState;
                controller.Write(3, ledState);
                
                Thread.Sleep(1000);
            }
        }
    }
}
