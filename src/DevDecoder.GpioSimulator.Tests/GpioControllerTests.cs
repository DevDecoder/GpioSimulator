using System;
using System.Device.Gpio;
using System.Threading;
using Xunit;

namespace DevDecoder.GpioSimulator.Tests
{
    public class GpioControllerTests
    {
        [Fact]
        public void PinValuePair_PropertiesAndDeconstruction_WorkCorrectly()
        {
            var pair = new PinValuePair(17, PinValue.High);

            Assert.Equal(17, pair.PinNumber);
            Assert.Equal(PinValue.High, pair.PinValue);

            var (pin, value) = pair;
            Assert.Equal(17, pin);
            Assert.Equal(PinValue.High, value);
        }

        [Fact]
        public void GpioController_DefaultsToLogicalSchemeAndSupportsPinCount()
        {
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                Assert.Equal(PinNumberingScheme.Logical, controller.NumberingScheme);
                Assert.Equal(40, controller.PinCount);
            }
        }

        [Fact]
        public void GpioController_SupportsCustomNumberingScheme()
        {
            var driver = new LocalDriver(PinNumberingScheme.Board);
            using (var controller = new GpioController(PinNumberingScheme.Board, driver))
            {
                Assert.Equal(PinNumberingScheme.Board, controller.NumberingScheme);
            }
        }

        [Fact]
        public void OpenPin_ClosePin_GetPinMode_BehaveCorrectly()
        {
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                Assert.False(controller.IsPinOpen(5));
                
                controller.OpenPin(5, PinMode.Output);
                Assert.True(controller.IsPinOpen(5));
                Assert.Equal(PinMode.Output, controller.GetPinMode(5));
                Assert.Equal(PinValue.Low, controller.Read(5));

                controller.ClosePin(5);
                Assert.False(controller.IsPinOpen(5));
            }
        }

        [Fact]
        public void MultiPinWriteAndRead_Spans_WorkCorrectly()
        {
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                controller.OpenPin(10, PinMode.Output);
                controller.OpenPin(11, PinMode.Output);

                var writePairs = new PinValuePair[]
                {
                    new PinValuePair(10, PinValue.High),
                    new PinValuePair(11, PinValue.Low)
                };

                // Write multiple pins using Span overload
                controller.Write(writePairs);

                Assert.Equal(PinValue.High, controller.Read(10));
                Assert.Equal(PinValue.Low, controller.Read(11));

                var readPairs = new PinValuePair[]
                {
                    new PinValuePair(10, PinValue.Low), // will be overwritten by read
                    new PinValuePair(11, PinValue.High) // will be overwritten by read
                };

                // Read multiple pins using Span overload
                controller.Read(readPairs);

                Assert.Equal(10, readPairs[0].PinNumber);
                Assert.Equal(PinValue.High, readPairs[0].PinValue);
                Assert.Equal(11, readPairs[1].PinNumber);
                Assert.Equal(PinValue.Low, readPairs[1].PinValue);
            }
        }

        [Fact]
        public void EdgeCallbacks_FireSuccessfullyOnStateTransitions()
        {
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                controller.OpenPin(4, PinMode.Input);

                int risingCount = 0;
                int fallingCount = 0;

                PinChangeEventHandler callback = (sender, args) =>
                {
                    if (args.ChangeType == PinEventTypes.Rising)
                    {
                        risingCount++;
                    }
                    else if (args.ChangeType == PinEventTypes.Falling)
                    {
                        fallingCount++;
                    }
                };

                controller.RegisterCallbackForPinValueChangedEvent(4, PinEventTypes.Rising | PinEventTypes.Falling, callback);

                // Act - Simulate physical high external stimulus (button press)
                driver.SetPinValueByTest(4, PinValue.High);
                Thread.Sleep(20);

                Assert.Equal(1, risingCount);
                Assert.Equal(0, fallingCount);

                // Transition High -> Low (Falling)
                driver.SetPinValueByTest(4, PinValue.Low);
                Thread.Sleep(20);

                Assert.Equal(1, risingCount);
                Assert.Equal(1, fallingCount);
                
                // Let's also verify that we can unregister the callback cleanly
                controller.UnregisterCallbackForPinValueChangedEvent(4, callback);
            }
        }

        [Fact]
        public void WaitForEvent_TimesOut_Correctly()
        {
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                controller.OpenPin(12, PinMode.Input);

                // WaitForEvent should block and return TimedOut = true when the timeout occurs without an event
                var result = controller.WaitForEvent(12, PinEventTypes.Rising, TimeSpan.FromMilliseconds(50));

                Assert.True(result.TimedOut);
                Assert.Equal(PinEventTypes.None, result.EventTypes);
            }
        }

        [Fact]
        public void WaitForEvent_BlocksUntilSignalChangeOccurs()
        {
            // Arrange
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                controller.OpenPin(4, PinMode.Input);

                // Act & Assert
                // Start trigger task
                System.Threading.Tasks.Task.Run(() =>
                {
                    Thread.Sleep(100);
                    driver.SetPinValueByTest(4, PinValue.High);
                });

                var result = controller.WaitForEvent(4, PinEventTypes.Rising, TimeSpan.FromSeconds(2));

                Assert.False(result.TimedOut);
                Assert.Equal(PinEventTypes.Rising, result.EventTypes);
            }
        }

        [Fact]
        public void OpenPin_InputPullUpAndPullDown_HaveCorrectDefaultStates()
        {
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                controller.OpenPin(14, PinMode.InputPullUp);
                Assert.Equal(PinMode.InputPullUp, controller.GetPinMode(14));
                Assert.Equal(PinValue.High, controller.Read(14));

                controller.OpenPin(15, PinMode.InputPullDown);
                Assert.Equal(PinMode.InputPullDown, controller.GetPinMode(15));
                Assert.Equal(PinValue.Low, controller.Read(15));
                
                // Changing to InputPullUp should set value to High
                controller.SetPinMode(15, PinMode.InputPullUp);
                Assert.Equal(PinValue.High, controller.Read(15));
            }
        }

        [Fact]
        public void IsPinModeSupported_ReturnsCorrectValues()
        {
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                Assert.True(controller.IsPinModeSupported(5, PinMode.Input));
                Assert.True(controller.IsPinModeSupported(5, PinMode.Output));
                Assert.True(controller.IsPinModeSupported(5, PinMode.InputPullUp));
                Assert.True(controller.IsPinModeSupported(5, PinMode.InputPullDown));
                Assert.False(controller.IsPinModeSupported(-1, PinMode.Input));
                Assert.False(controller.IsPinModeSupported(99, PinMode.Input));
            }
        }

#if !OFFICIAL_GPIO
        [Fact]
        public void OpenPin_AlreadyOpen_ThrowsInvalidOperationException()
        {
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                controller.OpenPin(4, PinMode.Input);
                Assert.True(controller.IsPinOpen(4));

                // Opening again must throw InvalidOperationException
                Assert.Throws<InvalidOperationException>(() => controller.OpenPin(4, PinMode.Input));
                Assert.Throws<InvalidOperationException>(() => controller.OpenPin(4, PinMode.Output));
            }
        }
#endif

#if !OFFICIAL_GPIO
        [Fact]
        public void IsValidPin_InvalidRanges_ReturnsFalse()
        {
            var driver = new LocalDriver(PinNumberingScheme.Logical);
            using (var controller = new GpioController(PinNumberingScheme.Logical, driver))
            {
                Assert.True(controller.IsValidPin(5));
                Assert.True(controller.IsValidPin(2));
                Assert.True(controller.IsValidPin(27));
                Assert.False(controller.IsValidPin(-1));
                Assert.False(controller.IsValidPin(0));
                Assert.False(controller.IsValidPin(28));
            }
        }
#endif
    }
}
