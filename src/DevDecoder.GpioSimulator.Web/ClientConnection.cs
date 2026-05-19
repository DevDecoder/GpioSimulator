using System;
using System.Net.WebSockets;

namespace DevDecoder.GpioSimulator.Web
{
    /// <summary>
    /// Represents an active WebSocket client connection to the web server, 
    /// encapsulating connection metadata and authorized admin privileges.
    /// </summary>
    public class ClientConnection
    {
        /// <summary>
        /// Gets or sets the unique identifier of the connection.
        /// </summary>
        public Guid ClientId { get; set; }

        /// <summary>
        /// Gets or sets the active WebSocket instance.
        /// </summary>
        public WebSocket Socket { get; set; } = null!;

        /// <summary>
        /// Gets or sets the type/category of the connected client (e.g. "ui", "controller").
        /// </summary>
        public string Type { get; set; } = "ui";

        /// <summary>
        /// Gets or sets the pin numbering scheme the client is operating on ("Board" or "Logical").
        /// </summary>
        public string Scheme { get; set; } = "Board";

        /// <summary>
        /// Gets or sets a value indicating whether the client is an authorized administrative client.
        /// </summary>
        public bool IsAuthorizedAdmin { get; set; }
    }
}
