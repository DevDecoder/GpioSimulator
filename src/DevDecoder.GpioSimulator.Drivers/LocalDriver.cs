using System;
using System.Collections.Generic;
using System.Device.Gpio;
using DevDecoder.GpioSimulator.Common;

namespace DevDecoder.GpioSimulator
{
    public class LocalDriver : SimulatorDriverBase
    {
        private readonly GpioSimulatorEngine _engine;
        private readonly string _clientId = Guid.NewGuid().ToString();

        public LocalDriver(PinNumberingScheme numberingScheme = PinNumberingScheme.Logical) 
            : this(new GpioSimulatorEngine(), numberingScheme)
        {
            // Seed a standard default 40-pin Raspberry Pi physical-to-logical pin mapping
            var defaultMapping = new Dictionary<int, int>();
            int[] rpiLogicalPins = { 2, 3, 4, 17, 27, 22, 10, 9, 11, 5, 6, 13, 19, 26, 14, 15, 18, 23, 24, 25, 8, 7, 12, 16, 20, 21 };
            int[] rpiPhysicalPins = { 3, 5, 7, 11, 13, 15, 19, 21, 23, 29, 31, 33, 35, 37, 8, 10, 12, 16, 18, 22, 24, 26, 32, 36, 38, 40 };
            for (int i = 0; i < rpiLogicalPins.Length; i++)
            {
                defaultMapping[rpiPhysicalPins[i]] = rpiLogicalPins[i];
            }
            _engine.LoadBoardMapping("raspberry_pi_5_breakout", defaultMapping);
        }

        public LocalDriver(GpioSimulatorEngine engine, PinNumberingScheme numberingScheme = PinNumberingScheme.Logical) 
            : base(numberingScheme)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            
            _engine.PinStateChanged += HandlePinStateChanged;
            _engine.PinClosed += HandlePinClosed;
        }

        private void HandlePinStateChanged(int physicalPin, string mode, string value, string ownerId, string ownerType)
        {
            int pin = NumberingScheme == PinNumberingScheme.Logical 
                ? _engine.ConvertPhysicalToLogical(physicalPin) 
                : physicalPin;

            if (pin < 0) return;

            PinValue val = value == "High" ? PinValue.High : PinValue.Low;
            UpdatePinValue(pin, val);

            PinEventTypes occurredType = val == PinValue.High ? PinEventTypes.Rising : PinEventTypes.Falling;
            FireCallbacks(pin, occurredType);
        }

        private void HandlePinClosed(int physicalPin)
        {
            int pin = NumberingScheme == PinNumberingScheme.Logical 
                ? _engine.ConvertPhysicalToLogical(physicalPin) 
                : physicalPin;

            if (pin < 0) return;
            
            ClearPinCache(pin);
        }

        protected override int ConvertPin(int pinNumber)
        {
            if (NumberingScheme == PinNumberingScheme.Logical)
            {
                int phys = _engine.ConvertLogicalToPhysical(pinNumber);
                if (phys < 0) throw new ArgumentException($"Pin {pinNumber} is not mapped in Logical scheme.");
                return phys;
            }
            return pinNumber;
        }

        protected override void OpenPinInternal(int pinNumber, PinMode mode)
        {
            int physicalPin = ConvertPin(pinNumber);
            
            if (!_engine.TryOpenPin(physicalPin, mode.ToString(), _clientId, "controller", out var errorType, out var errorMessage))
            {
                if (errorType == "ArgumentException") throw new ArgumentException(errorMessage);
                if (errorType == "InvalidOperationException") throw new InvalidOperationException(errorMessage);
                throw new Exception(errorMessage);
            }
        }

        private void ThrowException(string? errorType, string? errorMessage)
        {
            if (errorType == "ArgumentException") throw new ArgumentException(errorMessage);
            if (errorType == "InvalidOperationException") throw new InvalidOperationException(errorMessage);
            if (errorType == "UnauthorizedAccessException") throw new UnauthorizedAccessException(errorMessage);
            throw new Exception(errorMessage);
        }

        protected override void ClosePinInternal(int pinNumber)
        {
            int physicalPin = ConvertPin(pinNumber);
            if (!_engine.TryClosePin(physicalPin, _clientId, false, out var errorType, out var errorMessage))
            {
                ThrowException(errorType, errorMessage);
            }
        }

        protected override void WriteInternal(int pinNumber, PinValue value)
        {
            int physicalPin = ConvertPin(pinNumber);
            if (!_engine.TryWritePin(physicalPin, value.ToString(), _clientId, false, out var errorType, out var errorMessage))
            {
                ThrowException(errorType, errorMessage);
            }
        }

        protected override PinValue ReadInternal(int pinNumber)
        {
            int physicalPin = ConvertPin(pinNumber);
            if (!_engine.TryReadPin(physicalPin, _clientId, false, out var valStr, out var errorType, out var errorMessage))
            {
                ThrowException(errorType, errorMessage);
            }
            return valStr == "High" ? PinValue.High : PinValue.Low;
        }

        protected override void SetPinModeInternal(int pinNumber, PinMode mode)
        {
            int physicalPin = ConvertPin(pinNumber);
            if (!_engine.TrySetPinMode(physicalPin, mode.ToString(), _clientId, false, out var errorType, out var errorMessage))
            {
                ThrowException(errorType, errorMessage);
            }
        }

        protected override PinMode GetPinModeInternal(int pinNumber)
        {
            int physicalPin = ConvertPin(pinNumber);
            return ParsePinMode(_engine.GetPinMode(physicalPin));
        }

        public void SetPinValueByTest(int pinNumber, PinValue value)
        {
            int physicalPin = ConvertPin(pinNumber);
            // Simulate physical button/external stimulus
            if (!_engine.TryWritePin(physicalPin, value.ToString(), "test_harness", true, out var errorType, out var errorMessage))
            {
                ThrowException(errorType, errorMessage);
            }
        }

        public GpioSimulatorEngine Engine => _engine;

        private PinMode ParsePinMode(string mode)
        {
            if (Enum.TryParse<PinMode>(mode, out var result)) return result;
            return PinMode.Input;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _engine.PinStateChanged -= HandlePinStateChanged;
                _engine.PinClosed -= HandlePinClosed;
                
                var states = _engine.GetAllPinStates();
                foreach (var kvp in states)
                {
                    if (kvp.Value.OwnerId == _clientId)
                    {
                        _engine.TryClosePin(kvp.Key, _clientId, false, out _, out _);
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
