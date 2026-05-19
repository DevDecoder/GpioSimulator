namespace DevDecoder.GpioSimulator.Common
{
    /// <summary>
    /// Represents the current state of a simulated GPIO pin, including its mode, value, and owner.
    /// </summary>
    /// <param name="Mode">The current mode of the pin (e.g., "Input", "Output").</param>
    /// <param name="Value">The current value of the pin (e.g., "High", "Low").</param>
    /// <param name="OwnerId">The unique identifier of the client that currently owns or opened the pin.</param>
    /// <param name="OwnerType">The type of the client that owns the pin (e.g., "controller", "ui").</param>
    public record PinState(string Mode, string Value, string OwnerId, string OwnerType);
}
