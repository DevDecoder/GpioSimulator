namespace DevDecoder.GpioSimulator.Common
{
    internal class GpioSimulatorClient : IGpioSimulatorClient
    {
        private readonly GpioSimulatorEngine _engine;
        
        public string ClientId { get; }
        public string ClientType { get; }
        public bool IsAdmin { get; }

        internal GpioSimulatorClient(GpioSimulatorEngine engine, string clientId, string clientType, bool isAdmin)
        {
            _engine = engine;
            ClientId = clientId;
            ClientType = clientType;
            IsAdmin = isAdmin;
        }

        public GSEResult OpenPin(int pin, string mode)
        {
            return _engine.OpenPin(this, pin, mode);
        }

        public GSEResult WritePin(int pin, string value)
        {
            return _engine.WritePin(this, pin, value);
        }

        public GSEResult<string> ReadPin(int pin)
        {
            return _engine.ReadPin(this, pin);
        }

        public GSEResult ClosePin(int pin)
        {
            return _engine.ClosePin(this, pin);
        }

        public GSEResult SetPinMode(int pin, string mode)
        {
            return _engine.SetPinMode(this, pin, mode);
        }

        public GSEResult<string> GetPinMode(int pin) => _engine.GetPinMode(this, pin);
        public GSEResult<bool> IsPinOpen(int pin) => _engine.IsPinOpen(this, pin);
        public GSEResult<string?> GetPinOwnerId(int pin) => _engine.GetPinOwnerId(this, pin);
    }
}
