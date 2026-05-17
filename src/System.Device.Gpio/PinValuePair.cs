namespace System.Device.Gpio
{
    public struct PinValuePair
    {
        public int PinNumber { get; }
        public PinValue PinValue { get; }

        public PinValuePair(int pinNumber, PinValue pinValue)
        {
            PinNumber = pinNumber;
            PinValue = pinValue;
        }

        public void Deconstruct(out int pinNumber, out PinValue pinValue)
        {
            pinNumber = PinNumber;
            pinValue = PinValue;
        }
    }
}
