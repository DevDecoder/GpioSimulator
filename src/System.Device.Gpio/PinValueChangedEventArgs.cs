using System;

namespace System.Device.Gpio
{
    public class PinValueChangedEventArgs : EventArgs
    {
        public PinEventTypes ChangeType { get; }
        public int PinNumber { get; }

        public PinValueChangedEventArgs(PinEventTypes changeType, int pinNumber)
        {
            ChangeType = changeType;
            PinNumber = pinNumber;
        }
    }

    public delegate void PinChangeEventHandler(object sender, PinValueChangedEventArgs pinValueChangedEventArgs);
}
