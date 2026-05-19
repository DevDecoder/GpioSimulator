namespace DevDecoder.GpioSimulator.Common
{
    /// <summary>
    /// Represents a client connected to the GPIO Simulator Engine, encapsulating its identity, capabilities, and interface for pin operations.
    /// </summary>
    public interface IGpioSimulatorClient
    {
        /// <summary>
        /// Gets the unique identifier of the client.
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// Gets the type or category of the client (e.g., "controller", "ui").
        /// </summary>
        string ClientType { get; }

        /// <summary>
        /// Gets a value indicating whether the client has administrative privileges (e.g., bypasses ownership checks).
        /// </summary>
        bool IsAdmin { get; }

        /// <summary>
        /// Opens a GPIO pin in the specified mode.
        /// </summary>
        /// <param name="pin">The physical pin number.</param>
        /// <param name="mode">The target pin mode (e.g., "Input", "Output").</param>
        /// <returns>The result of the operation.</returns>
        GSEResult OpenPin(int pin, string mode);

        /// <summary>
        /// Writes a value to a GPIO pin.
        /// </summary>
        /// <param name="pin">The physical pin number.</param>
        /// <param name="value">The value to write (e.g., "High", "Low").</param>
        /// <returns>The result of the operation.</returns>
        GSEResult WritePin(int pin, string value);

        /// <summary>
        /// Reads the current value of a GPIO pin.
        /// </summary>
        /// <param name="pin">The physical pin number.</param>
        /// <returns>A generic result containing the pin value ("High" or "Low").</returns>
        GSEResult<string> ReadPin(int pin);

        /// <summary>
        /// Closes a GPIO pin, releasing its ownership.
        /// </summary>
        /// <param name="pin">The physical pin number.</param>
        /// <returns>The result of the operation.</returns>
        GSEResult ClosePin(int pin);

        /// <summary>
        /// Configures the mode of an open GPIO pin.
        /// </summary>
        /// <param name="pin">The physical pin number.</param>
        /// <param name="mode">The target pin mode (e.g., "Input", "Output").</param>
        /// <returns>The result of the operation.</returns>
        GSEResult SetPinMode(int pin, string mode);
        
        /// <summary>
        /// Gets the currently configured mode of a GPIO pin.
        /// </summary>
        /// <param name="pin">The physical pin number.</param>
        /// <returns>A generic result containing the pin mode name.</returns>
        GSEResult<string> GetPinMode(int pin);

        /// <summary>
        /// Checks if a GPIO pin is currently open.
        /// </summary>
        /// <param name="pin">The physical pin number.</param>
        /// <returns>A generic result indicating whether the pin is open.</returns>
        GSEResult<bool> IsPinOpen(int pin);

        /// <summary>
        /// Gets the unique identifier of the client that currently owns or opened the pin.
        /// </summary>
        /// <param name="pin">The physical pin number.</param>
        /// <returns>A generic result containing the owner's ClientId, or null if the pin is closed.</returns>
        GSEResult<string?> GetPinOwnerId(int pin);
    }
}
