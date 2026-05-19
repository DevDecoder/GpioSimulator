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
            var client = engine.Connect("client-1", "app", false);

            var result = client.OpenPin(pin, "Output");

            Assert.True(result.Success);
            Assert.Equal("client-1", client.GetPinOwnerId(pin).Value);
        }

        [Fact]
        public void OpenPin_FailsWhenAlreadyOwnedByAnother()
        {
            var engine = CreateEngine();
            int pin = 4;
            
            var client1 = engine.Connect("client-1", "app", false);
            client1.OpenPin(pin, "Output");

            var client2 = engine.Connect("client-2", "app", false);
            var result = client2.OpenPin(pin, "Output");

            Assert.False(result.Success);
            Assert.Equal("InvalidOperationException", result.ErrorType);
        }

        [Fact]
        public void ClosePin_ReleasesOwnership()
        {
            var engine = CreateEngine();
            int pin = 4;
            var client = engine.Connect("client-1", "app", false);
            
            client.OpenPin(pin, "Output");
            var result = client.ClosePin(pin);

            Assert.True(result.Success);
            Assert.Null(client.GetPinOwnerId(pin).Value);
        }

        [Fact]
        public void WritePin_And_ReadPin_UpdateState()
        {
            var engine = CreateEngine();
            int pin = 4;
            var client = engine.Connect("client-1", "app", false);
            
            client.OpenPin(pin, "Output");
            
            var writeResult = client.WritePin(pin, "High");
            Assert.True(writeResult.Success);
            
            var readResult = client.ReadPin(pin);
            Assert.True(readResult.Success);
            Assert.Equal("High", readResult.Value);
        }

        [Fact]
        public void NonAdmin_Cannot_Modify_Others_Pin()
        {
            var engine = CreateEngine();
            int pin = 4;
            
            var client1 = engine.Connect("client-1", "app", false);
            client1.OpenPin(pin, "Output");

            var client2 = engine.Connect("client-2", "app", false);
            
            var writeResult = client2.WritePin(pin, "High");
            Assert.False(writeResult.Success);
            Assert.Equal("UnauthorizedAccessException", writeResult.ErrorType);

            var readResult = client2.ReadPin(pin);
            Assert.False(readResult.Success);
            Assert.Equal("UnauthorizedAccessException", readResult.ErrorType);

            var closeResult = client2.ClosePin(pin);
            Assert.False(closeResult.Success);
            Assert.Equal("UnauthorizedAccessException", closeResult.ErrorType);
        }

        [Fact]
        public void Admin_Can_Modify_Others_Pin()
        {
            var engine = CreateEngine();
            int pin = 4;
            
            var client1 = engine.Connect("client-1", "app", false);
            client1.OpenPin(pin, "Output");

            var adminClient = engine.Connect("admin-client", "test_harness", true);
            
            var writeResult = adminClient.WritePin(pin, "High");
            Assert.True(writeResult.Success);

            var readResult = adminClient.ReadPin(pin);
            Assert.True(readResult.Success);
            Assert.Equal("High", readResult.Value);

            var closeResult = adminClient.ClosePin(pin);
            Assert.True(closeResult.Success);
            Assert.Null(adminClient.GetPinOwnerId(pin).Value);
        }
    }
}
