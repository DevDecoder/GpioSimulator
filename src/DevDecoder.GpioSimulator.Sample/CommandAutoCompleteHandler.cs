using System;
using System.Collections.Generic;
using System.Linq;

namespace DevDecoder.GpioSimulator.Sample
{
    /// <summary>
    /// Provides auto-completion suggestions for the interactive GPIO board simulator terminal.
    /// </summary>
    public class CommandAutoCompleteHandler : IAutoCompleteHandler
    {
        private static readonly string[] Commands =
        {
            "open", "close", "write", "read", "setmode", "isopen", 
            "issupported", "scheme", "schema", "status", "help", 
            "exit", "quit"
        };

        private static readonly string[] Modes = { "in", "out", "pullup", "pulldown" };
        private static readonly string[] Schemes = { "logical", "board" };
        private static readonly string[] WriteValues = { "1", "0", "high", "low" };

        /// <summary>
        /// Gets or sets the characters that define the boundary for word separations.
        /// </summary>
        public char[] Separators { get; set; } = new char[] { ' ' };

        /// <summary>
        /// Generates auto-completion suggestions based on the current input text and cursor index.
        /// </summary>
        /// <param name="text">The current input line text.</param>
        /// <param name="index">The current cursor index.</param>
        /// <returns>An array of suggestions matching the context.</returns>
        public string[] GetSuggestions(string text, int index)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Commands;
            }

            // Split the line by spaces
            string[] parts = text.Split(Separators, StringSplitOptions.None);
            if (parts.Length == 0)
            {
                return Commands;
            }

            string cmd = parts[0].ToLower();

            // If we're typing the first word (command)
            if (parts.Length == 1)
            {
                return Commands.Where(c => c.StartsWith(cmd, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            // Auto-complete sub-commands or parameters
            // Case 1: scheme/schema parameter autocomplete (e.g., scheme <tab>)
            if ((cmd == "scheme" || cmd == "schema") && parts.Length == 2)
            {
                string arg = parts[1].ToLower();
                return Schemes.Where(s => s.StartsWith(arg, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            // Case 2: open <pin> <mode> -> parts[2] is the mode
            if (cmd == "open" && parts.Length == 3)
            {
                string arg = parts[2].ToLower();
                return Modes.Where(m => m.StartsWith(arg, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            // Case 3: setmode <pin> <mode> / set <pin> <mode> -> parts[2] is the mode
            if ((cmd == "setmode" || cmd == "set" || cmd == "sm") && parts.Length == 3)
            {
                string arg = parts[2].ToLower();
                return Modes.Where(m => m.StartsWith(arg, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            // Case 4: issupported <pin> <mode> -> parts[2] is the mode
            if ((cmd == "issupported" || cmd == "is") && parts.Length == 3)
            {
                string arg = parts[2].ToLower();
                return Modes.Where(m => m.StartsWith(arg, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            // Case 5: write <pin> <value> -> parts[2] is the value
            if ((cmd == "write" || cmd == "w") && parts.Length == 3)
            {
                string arg = parts[2].ToLower();
                return WriteValues.Where(v => v.StartsWith(arg, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            return Array.Empty<string>();
        }
    }
}
