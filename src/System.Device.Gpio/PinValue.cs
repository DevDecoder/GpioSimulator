using System;

namespace System.Device.Gpio
{
    public struct PinValue : IEquatable<PinValue>
    {
        private readonly byte _value;

        private PinValue(byte value) => _value = value;

        public static PinValue Low => new PinValue(0);
        public static PinValue High => new PinValue(1);

        public static implicit operator PinValue(bool value) => value ? High : Low;
        public static implicit operator bool(PinValue value) => value._value != 0;
        public static implicit operator PinValue(int value) => value == 0 ? Low : High;
        public static implicit operator int(PinValue value) => value._value;

        public bool Equals(PinValue other) => _value == other._value;
        public override bool Equals(object obj) => obj is PinValue other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value == 0 ? "Low" : "High";

        public static bool operator ==(PinValue left, PinValue right) => left.Equals(right);
        public static bool operator !=(PinValue left, PinValue right) => !left.Equals(right);
    }
}
