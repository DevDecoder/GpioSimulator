using System;
using System.Threading;

namespace System.Device.Gpio
{
    public class GpioController : IDisposable
    {
        private readonly GpioDriver _driver;
        private readonly PinNumberingScheme _numberingScheme;

        public GpioController() : this(PinNumberingScheme.Logical)
        {
        }

        public GpioController(PinNumberingScheme numberingScheme) 
            : this(numberingScheme, new WebAPIDriver(numberingScheme))
        {
        }

        public GpioController(PinNumberingScheme numberingScheme, GpioDriver driver)
        {
            _numberingScheme = numberingScheme;
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public virtual PinNumberingScheme NumberingScheme => _numberingScheme;
        public virtual int PinCount => _driver.PinCount;

        public virtual bool IsValidPin(int pinNumber) => _driver.IsPinModeSupported(pinNumber, PinMode.Input);

        public virtual void OpenPin(int pinNumber) => _driver.OpenPin(pinNumber);
        public virtual void OpenPin(int pinNumber, PinMode mode)
        {
            _driver.OpenPin(pinNumber);
            _driver.SetPinMode(pinNumber, mode);
        }
        public virtual void OpenPin(int pinNumber, PinMode mode, PinValue initialValue)
        {
            _driver.OpenPin(pinNumber);
            _driver.SetPinMode(pinNumber, mode);
            _driver.Write(pinNumber, initialValue);
        }
        public virtual void ClosePin(int pinNumber) => _driver.ClosePin(pinNumber);
        public virtual void Write(int pinNumber, PinValue value) => _driver.Write(pinNumber, value);
        public virtual void Write(ReadOnlySpan<PinValuePair> pinValuePairs) => _driver.Write(pinValuePairs);
        public virtual PinValue Read(int pinNumber) => _driver.Read(pinNumber);
        public virtual void Read(Span<PinValuePair> pinValuePairs) => _driver.Read(pinValuePairs);
        public virtual void SetPinMode(int pinNumber, PinMode mode) => _driver.SetPinMode(pinNumber, mode);
        public virtual PinMode GetPinMode(int pinNumber) => _driver.GetPinMode(pinNumber);
        public virtual bool IsPinOpen(int pinNumber) => _driver.IsPinOpen(pinNumber);
        public virtual bool IsPinModeSupported(int pinNumber, PinMode mode) => _driver.IsPinModeSupported(pinNumber, mode);
        
        public virtual void RegisterCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
            => _driver.RegisterCallbackForPinValueChangedEvent(pinNumber, eventTypes, callback);
            
        public virtual void UnregisterCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
            => _driver.UnregisterCallbackForPinValueChangedEvent(pinNumber, callback);
            
        public virtual WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, TimeSpan timeout)
            => _driver.WaitForEvent(pinNumber, eventTypes, timeout);
            
        public virtual WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
            => _driver.WaitForEvent(pinNumber, eventTypes, cancellationToken);
        
        public void Dispose()
        {
            _driver?.Dispose();
        }
    }
}
