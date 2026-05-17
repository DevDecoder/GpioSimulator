using System;
using System.Threading;
using DevDecoder.GpioSimulator;

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

        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, GpioPin> _gpioPins = new System.Collections.Concurrent.ConcurrentDictionary<int, GpioPin>();

        public virtual PinNumberingScheme NumberingScheme => _numberingScheme;
        public virtual int PinCount => _driver.PinCount;

        public virtual GpioPin OpenPin(int pinNumber)
        {
            if (IsPinOpen(pinNumber))
                return _gpioPins[pinNumber];

            _driver.OpenPin(pinNumber);
            var pin = new GpioPin(pinNumber, this);
            _gpioPins[pinNumber] = pin;
            return pin;
        }

        public virtual GpioPin OpenPin(int pinNumber, PinMode mode)
        {
            var pin = OpenPin(pinNumber);
            _driver.SetPinMode(pinNumber, mode);
            return pin;
        }

        public virtual GpioPin OpenPin(int pinNumber, PinMode mode, PinValue initialValue)
        {
            var pin = OpenPin(pinNumber);
            _driver.SetPinMode(pinNumber, mode);
            _driver.Write(pinNumber, initialValue);
            return pin;
        }
        public virtual void ClosePin(int pinNumber)
        {
            _driver.ClosePin(pinNumber);
            _gpioPins.TryRemove(pinNumber, out _);
        }
        public virtual void Write(int pinNumber, PinValue value) => _driver.Write(pinNumber, value);
        public virtual void Write(ReadOnlySpan<PinValuePair> pinValuePairs) => _driver.Write(pinValuePairs);
        public virtual PinValue Read(int pinNumber) => _driver.Read(pinNumber);
        public virtual void Read(Span<PinValuePair> pinValuePairs) => _driver.Read(pinValuePairs);
        public virtual void Toggle(int pinNumber) => _driver.Toggle(pinNumber);
        public virtual void SetPinMode(int pinNumber, PinMode mode) => _driver.SetPinMode(pinNumber, mode);
        public virtual PinMode GetPinMode(int pinNumber) => _driver.GetPinMode(pinNumber);
        public virtual bool IsPinOpen(int pinNumber) => _driver.IsPinOpen(pinNumber);
        public virtual bool IsPinModeSupported(int pinNumber, PinMode mode) => _driver.IsPinModeSupported(pinNumber, mode);
        
        public virtual void RegisterCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
            => _driver.AddCallbackForPinValueChangedEvent(pinNumber, eventTypes, callback);
            
        public virtual void UnregisterCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
            => _driver.RemoveCallbackForPinValueChangedEvent(pinNumber, callback);
            
        public virtual WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                return WaitForEvent(pinNumber, eventTypes, cts.Token);
            }
        }
            
        public virtual WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
            => _driver.WaitForEvent(pinNumber, eventTypes, cancellationToken);
        
        public void Dispose()
        {
            foreach (var pin in _gpioPins.Values)
            {
                _driver.ClosePin(pin.PinNumber);
            }
            _gpioPins.Clear();
            _driver?.Dispose();
        }
    }
}
