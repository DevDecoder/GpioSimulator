namespace System.Device.Gpio
{
    public struct WaitForEventResult
    {
        public bool TimedOut { get; set; }
        public PinEventTypes EventTypes { get; set; }
    }
}
