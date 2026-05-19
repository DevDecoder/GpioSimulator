using System;
using System.Collections.Generic;
using System.Device.Gpio;
using DevDecoder.GpioSimulator.Common;

namespace DevDecoder.GpioSimulator
{
    /// <summary>
    /// A local, in-memory implementation of the <see cref="GpioDriver"/> that interacts directly 
    /// with the in-process <see cref="GpioSimulatorEngine"/>, allowing direct and high-performance simulation.
    /// </summary>
    public class LocalDriver : SimulatorDriverBase
    {
        private readonly GpioSimulatorEngine _engine;
        private readonly IGpioSimulatorClient _client;
        private readonly IGpioSimulatorClient _adminClient;
        private readonly string _clientId = Guid.NewGuid().ToString();

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDriver"/> class with a default in-memory engine 
        /// and a standard 40-pin Raspberry Pi physical-to-logical pin mapping.
        /// </summary>
        /// <param name="numberingScheme">The pin numbering scheme to use (default: Logical).</param>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDriver"/> class sharing a custom <see cref="GpioSimulatorEngine"/>.
        /// </summary>
        /// <param name="engine">The simulator engine to run the driver against.</param>
        /// <param name="numberingScheme">The pin numbering scheme to use (default: Logical).</param>
        public LocalDriver(GpioSimulatorEngine engine, PinNumberingScheme numberingScheme = PinNumberingScheme.Logical) 
            : base(numberingScheme)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _client = _engine.Connect(_clientId, "controller", false);
            _adminClient = _engine.Connect("local_admin", "local_admin", true);
            
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

        /// <summary>
        /// Converts the given pin number into the physical pin number format.
        /// </summary>
        /// <param name="pinNumber">The pin number under the current scheme.</param>
        /// <returns>The physical pin number.</returns>
        /// <exception cref="ArgumentException">Thrown if the logical pin is not mapped.</exception>
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

        /// <summary>
        /// Opens a GPIO pin on the local simulator engine.
        /// </summary>
        /// <param name="pinNumber">The logical or physical pin number depending on scheme.</param>
        /// <param name="mode">The pin mode.</param>
        protected override void OpenPinInternal(int pinNumber, PinMode mode)
        {
            int physicalPin = ConvertPin(pinNumber);
            
            var result = _client.OpenPin(physicalPin, mode.ToString());
            result.ThrowIfError();
        }

        /// <summary>
        /// Closes a GPIO pin on the local simulator engine.
        /// </summary>
        /// <param name="pinNumber">The logical or physical pin number.</param>
        protected override void ClosePinInternal(int pinNumber)
        {
            int physicalPin = ConvertPin(pinNumber);
            var result = _client.ClosePin(physicalPin);
            result.ThrowIfError();
        }

        /// <summary>
        /// Writes a value to a GPIO pin on the local simulator engine.
        /// </summary>
        /// <param name="pinNumber">The logical or physical pin number.</param>
        /// <param name="value">The pin value to write.</param>
        protected override void WriteInternal(int pinNumber, PinValue value)
        {
            int physicalPin = ConvertPin(pinNumber);
            var result = _client.WritePin(physicalPin, value.ToString());
            result.ThrowIfError();
        }

        /// <summary>
        /// Reads the value of a GPIO pin from the local simulator engine.
        /// </summary>
        /// <param name="pinNumber">The logical or physical pin number.</param>
        /// <returns>The read pin value.</returns>
        protected override PinValue ReadInternal(int pinNumber)
        {
            int physicalPin = ConvertPin(pinNumber);
            var result = _client.ReadPin(physicalPin);
            result.ThrowIfError();
            
            return result.Value == "High" ? PinValue.High : PinValue.Low;
        }

        /// <summary>
        /// Sets the mode of a GPIO pin on the local simulator engine.
        /// </summary>
        /// <param name="pinNumber">The logical or physical pin number.</param>
        /// <param name="mode">The target pin mode.</param>
        protected override void SetPinModeInternal(int pinNumber, PinMode mode)
        {
            int physicalPin = ConvertPin(pinNumber);
            var result = _client.SetPinMode(physicalPin, mode.ToString());
            result.ThrowIfError();
        }

        /// <summary>
        /// Gets the mode of a GPIO pin from the local simulator engine.
        /// </summary>
        /// <param name="pinNumber">The logical or physical pin number.</param>
        /// <returns>The current pin mode.</returns>
        protected override PinMode GetPinModeInternal(int pinNumber)
        {
            int physicalPin = ConvertPin(pinNumber);
            var result = _client.GetPinMode(physicalPin);
            result.ThrowIfError();
            return ParsePinMode(result.Value!);
        }

        /// <summary>
        /// Simulates external physical stimulus/hardware interaction on a pin using administrative bypass.
        /// </summary>
        /// <param name="pinNumber">The logical or physical pin number.</param>
        /// <param name="value">The pin value to drive the pin to.</param>
        public void SetPinValue(int pinNumber, PinValue value)
        {
            int physicalPin = ConvertPin(pinNumber);
            // Simulate physical button/external stimulus
            var result = _adminClient.WritePin(physicalPin, value.ToString());
            result.ThrowIfError();
        }

        /// <summary>
        /// Gets the underlying shared simulator engine.
        /// </summary>
        public GpioSimulatorEngine Engine => _engine;

        private PinMode ParsePinMode(string mode)
        {
            if (Enum.TryParse<PinMode>(mode, out var result)) return result;
            return PinMode.Input;
        }

        /// <summary>
        /// Disposes of the driver resources, unhooking simulator engine events and closing owned pins.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
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
                        _client.ClosePin(kvp.Key);
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
