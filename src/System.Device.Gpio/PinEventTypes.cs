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
