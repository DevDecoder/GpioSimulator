using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        var asm = Assembly.LoadFrom("/Users/craigdean/.nuget/packages/system.device.gpio/3.2.0/lib/netstandard2.0/System.Device.Gpio.dll");
        var type = asm.GetType("System.Device.Gpio.GpioController");
        if (type == null) { Console.WriteLine("Type not found"); return; }
        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == "OpenPin"))
        {
            Console.WriteLine($"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
        }
    }
}
