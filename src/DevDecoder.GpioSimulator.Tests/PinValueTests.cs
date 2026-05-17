using System.Device.Gpio;
using Xunit;

namespace DevDecoder.GpioSimulator.Tests
{
    public class PinValueTests
    {
        [Fact]
        public void Low_RepresentedCorrectly()
        {
            PinValue val = PinValue.Low;
            
            bool boolVal = val;
            int intVal = val;

            Assert.False(boolVal);
            Assert.Equal(0, intVal);
            Assert.Equal("Low", val.ToString());
        }

        [Fact]
        public void High_RepresentedCorrectly()
        {
            PinValue val = PinValue.High;
            
            bool boolVal = val;
            int intVal = val;

            Assert.True(boolVal);
            Assert.Equal(1, intVal);
            Assert.Equal("High", val.ToString());
        }

        [Fact]
        public void ImplicitConversions_FromBool_BehaveCorrectly()
        {
            PinValue highFromBool = true;
            PinValue lowFromBool = false;

            Assert.Equal(PinValue.High, highFromBool);
            Assert.Equal(PinValue.Low, lowFromBool);
        }

        [Fact]
        public void ImplicitConversions_FromInt_BehaveCorrectly()
        {
            PinValue highFromInt1 = 1;
            PinValue highFromInt42 = 42;
            PinValue lowFromInt = 0;

            Assert.Equal(PinValue.High, highFromInt1);
            Assert.Equal(PinValue.High, highFromInt42);
            Assert.Equal(PinValue.Low, lowFromInt);
        }

        [Fact]
        public void EqualityOperators_BehaveCorrectly()
        {
            PinValue val1 = PinValue.High;
            PinValue val2 = 1;
            PinValue val3 = PinValue.Low;

            Assert.True(val1 == val2);
            Assert.False(val1 == val3);
            Assert.True(val1 != val3);
            Assert.False(val1 != val2);
            Assert.True(val1.Equals(val2));
            Assert.False(val1.Equals(val3));
        }
    }
}
