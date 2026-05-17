using System;
using System.Reflection;
using System.Linq;

var asm = Assembly.LoadFrom("/Users/craigdean/.nuget/packages/system.device.gpio/3.2.0/lib/netstandard2.0/System.Device.Gpio.dll");
var type = asm.GetType("System.Device.Gpio.GpioController");
foreach (var c in type.GetConstructors())
{
    Console.WriteLine($"GpioController({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
}
