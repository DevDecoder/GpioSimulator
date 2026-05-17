using System;
using System.Threading;

namespace System.Device.Gpio
{
    public abstract class GpioDriver : IDisposable
    {
        protected internal abstract int PinCount { get; }
        protected internal abstract int ConvertPinNumberToLogicalNumberingScheme(int pinNumber);
        protected internal abstract void OpenPin(int pinNumber);
        protected internal abstract void ClosePin(int pinNumber);
        protected internal abstract void Write(int pinNumber, PinValue value);
        protected internal abstract PinValue Read(int pinNumber);
        protected internal abstract void SetPinMode(int pinNumber, PinMode mode);
        protected internal abstract PinMode GetPinMode(int pinNumber);
        protected internal abstract bool IsPinModeSupported(int pinNumber, PinMode mode);
        protected internal abstract void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback);
        protected internal abstract void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback);
        protected internal abstract WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken);
        protected internal virtual void Write(ReadOnlySpan<PinValuePair> pinValuePairs)
        {
            foreach (var pair in pinValuePairs)
            {
                Write(pair.PinNumber, pair.PinValue);
            }
        }
        protected internal virtual void Read(Span<PinValuePair> pinValuePairs)
        {
            for (int i = 0; i < pinValuePairs.Length; i++)
            {
                pinValuePairs[i] = new PinValuePair(pinValuePairs[i].PinNumber, Read(pinValuePairs[i].PinNumber));
            }
        }
        protected internal virtual void Toggle(int pinNumber) => Write(pinNumber, !Read(pinNumber));
        protected internal abstract bool IsPinOpen(int pinNumber);

        protected abstract void Dispose(bool disposing);
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
