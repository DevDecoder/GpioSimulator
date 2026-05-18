using System;
using Xunit;
using DevDecoder.GpioSimulator.Common;

namespace DevDecoder.GpioSimulator.Tests
{
    public class GpioSimulatorEngineTests
    {
        private GpioSimulatorEngine CreateEngine()
        {
            var engine = new GpioSimulatorEngine();
            engine.LoadBoardMapping("test-board", new System.Collections.Generic.Dictionary<int, int> { { 4, 4 } });
            return engine;
        }

        [Fact]
        public void OpenPin_AssignsOwnership()
        {
            var engine = CreateEngine();
            int pin = 4;
            string clientId = "client-1";
            string clientType = "app";

            bool success = engine.TryOpenPin(pin, "Output", clientId, clientType, out var errorType, out var errorMessage);

            Assert.True(success);
            Assert.Equal(clientId, engine.GetPinOwnerId(pin));
        }

        [Fact]
        public void OpenPin_FailsWhenAlreadyOwnedByAnother()
        {
            var engine = CreateEngine();
            int pin = 4;
            
            engine.TryOpenPin(pin, "Output", "client-1", "app", out _, out _);

            bool success = engine.TryOpenPin(pin, "Output", "client-2", "app", out var errorType, out var errorMessage);

            Assert.False(success);
            Assert.Equal("InvalidOperationException", errorType);
        }

        [Fact]
        public void ClosePin_ReleasesOwnership()
        {
            var engine = CreateEngine();
            int pin = 4;
            string clientId = "client-1";
            
            engine.TryOpenPin(pin, "Output", clientId, "app", out _, out _);
            bool success = engine.TryClosePin(pin, clientId, false, out _, out _);

            Assert.True(success);
            Assert.Null(engine.GetPinOwnerId(pin));
        }

        [Fact]
        public void WritePin_And_ReadPin_UpdateState()
        {
            var engine = CreateEngine();
            int pin = 4;
            string clientId = "client-1";
            
            engine.TryOpenPin(pin, "Output", clientId, "app", out _, out _);
            
            bool writeSuccess = engine.TryWritePin(pin, "High", clientId, false, out _, out _);
            Assert.True(writeSuccess);
            
            bool readSuccess = engine.TryReadPin(pin, clientId, false, out var val, out _, out _);
            Assert.True(readSuccess);
            Assert.Equal("High", val);
        }
    }
}
