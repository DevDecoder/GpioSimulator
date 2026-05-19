using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DevDecoder.GpioSimulator.Common
{
    /// <summary>
    /// The core simulator engine that manages pin states, board mappings, 
    /// and coordinates interactions between connected simulator clients.
    /// </summary>
    public class GpioSimulatorEngine
    {
        private readonly ConcurrentDictionary<int, PinState> _pinStates = new ConcurrentDictionary<int, PinState>();

        /// <summary>
        /// Occurs when a pin's mode, value, or ownership changes.
        /// Arguments: physicalPin, mode, value, ownerId, ownerType.
        /// </summary>
        public event Action<int, string, string, string, string>? PinStateChanged;

        /// <summary>
        /// Occurs when a pin is closed and released.
        /// Arguments: physicalPin.
        /// </summary>
        public event Action<int>? PinClosed;

        /// <summary>
        /// Occurs when the active board physical-to-logical mapping changes.
        /// Arguments: boardId, activePhysToLog.
        /// </summary>
        public event Action<string, Dictionary<int, int>>? BoardChanged;

        private readonly object _mappingLock = new object();
        private string _activeBoardId = "raspberry_pi_5_breakout";
        private Dictionary<int, int> _activePhysToLog = new Dictionary<int, int>();
        private Dictionary<int, int> _activeLogToPhys = new Dictionary<int, int>();

        /// <summary>
        /// Gets the identifier of the currently loaded board mapping layout.
        /// </summary>
        public string ActiveBoardId
        {
            get { lock (_mappingLock) return _activeBoardId; }
        }

        /// <summary>
        /// Gets the current active physical-to-logical pin mapping dictionary.
        /// </summary>
        public Dictionary<int, int> ActivePhysToLog
        {
            get { lock (_mappingLock) return new Dictionary<int, int>(_activePhysToLog); }
        }

        /// <summary>
        /// Gets the current active logical-to-physical pin mapping dictionary.
        /// </summary>
        public Dictionary<int, int> ActiveLogToPhys
        {
            get { lock (_mappingLock) return new Dictionary<int, int>(_activeLogToPhys); }
        }

        /// <summary>
        /// Loads a new board physical-to-logical pin mapping and updates client lookups.
        /// </summary>
        /// <param name="boardId">The unique identifier of the board layout.</param>
        /// <param name="physToLog">The mapping of physical pins to logical pins.</param>
        public void LoadBoardMapping(string boardId, Dictionary<int, int> physToLog)
        {
            var logToPhys = new Dictionary<int, int>();
            foreach (var kvp in physToLog)
            {
                logToPhys[kvp.Value] = kvp.Key;
            }

            lock (_mappingLock)
            {
                _activeBoardId = boardId;
                _activePhysToLog = physToLog;
                _activeLogToPhys = logToPhys;
            }

            BoardChanged?.Invoke(boardId, physToLog);
        }

        /// <summary>
        /// Checks whether the specified pin number is a valid physical pin in the current board layout.
        /// </summary>
        /// <param name="physicalPin">The physical pin number to check.</param>
        /// <returns>True if the physical pin is valid; otherwise, false.</returns>
        public bool IsValidPhysicalPin(int physicalPin)
        {
            lock (_mappingLock)
            {
                return _activePhysToLog.ContainsKey(physicalPin);
            }
        }

        /// <summary>
        /// Checks whether the specified pin number is a valid logical pin in the current board layout.
        /// </summary>
        /// <param name="logicalPin">The logical pin number to check.</param>
        /// <returns>True if the logical pin is valid; otherwise, false.</returns>
        public bool IsValidLogicalPin(int logicalPin)
        {
            lock (_mappingLock)
            {
                return _activeLogToPhys.ContainsKey(logicalPin);
            }
        }

        /// <summary>
        /// Converts a logical pin number to its mapped physical pin number based on the current layout.
        /// </summary>
        /// <param name="logicalPin">The logical pin number.</param>
        /// <returns>The mapped physical pin number, or -1 if the logical pin is invalid.</returns>
        public int ConvertLogicalToPhysical(int logicalPin)
        {
            lock (_mappingLock)
            {
                return _activeLogToPhys.TryGetValue(logicalPin, out int phys) ? phys : -1;
            }
        }

        /// <summary>
        /// Converts a physical pin number to its mapped logical pin number based on the current layout.
        /// </summary>
        /// <param name="physicalPin">The physical pin number.</param>
        /// <returns>The mapped logical pin number, or -1 if the physical pin is invalid.</returns>
        public int ConvertPhysicalToLogical(int physicalPin)
        {
            lock (_mappingLock)
            {
                return _activePhysToLog.TryGetValue(physicalPin, out int log) ? log : -1;
            }
        }

        /// <summary>
        /// Connects a new client to the simulator engine with specified details.
        /// </summary>
        /// <param name="clientId">A unique identity string for the client.</param>
        /// <param name="clientType">The category of client (e.g. "ui", "controller").</param>
        /// <param name="isAdmin">Whether the client has admin overrides.</param>
        /// <returns>An instance of <see cref="IGpioSimulatorClient"/>.</returns>
        public IGpioSimulatorClient Connect(string clientId, string clientType, bool isAdmin)
        {
            return new GpioSimulatorClient(this, clientId, clientType, isAdmin);
        }

        internal GSEResult OpenPin(IGpioSimulatorClient client, int physicalPin, string mode)
        {
            if (!IsValidPhysicalPin(physicalPin))
            {
                return GSEResult.Error("ArgumentException", $"Pin {physicalPin} is not a valid physical pin for this board.");
            }

            var defaultVal = mode == "InputPullUp" ? "High" : "Low";
            var newState = new PinState(mode, defaultVal, client.ClientId, client.ClientType);

            if (!_pinStates.TryAdd(physicalPin, newState))
            {
                return GSEResult.Error("InvalidOperationException", $"Pin {physicalPin} is already open.");
            }

            PinStateChanged?.Invoke(physicalPin, mode, defaultVal, client.ClientId, client.ClientType);
            return GSEResult.OK;
        }

        private bool CheckAccess(IGpioSimulatorClient client, int physicalPin, out string? errorType, out string? errorMessage)
        {
            errorType = null;
            errorMessage = null;

            if (!_pinStates.TryGetValue(physicalPin, out var state))
            {
                errorType = "InvalidOperationException";
                errorMessage = $"Pin {physicalPin} is not open.";
                return false;
            }

            if (state.OwnerId != client.ClientId && !client.IsAdmin)
            {
                errorType = "UnauthorizedAccessException";
                errorMessage = $"Client is not authorized to perform this action on pin {physicalPin}. It is owned by {state.OwnerId}.";
                return false;
            }

            return true;
        }

        internal GSEResult ClosePin(IGpioSimulatorClient client, int physicalPin)
        {
            if (!CheckAccess(client, physicalPin, out var errorType, out var errorMessage)) 
                return GSEResult.Error(errorType!, errorMessage!);

            if (_pinStates.TryRemove(physicalPin, out _))
            {
                PinClosed?.Invoke(physicalPin);
            }
            return GSEResult.OK;
        }

        internal GSEResult WritePin(IGpioSimulatorClient client, int physicalPin, string value)
        {
            if (!CheckAccess(client, physicalPin, out var errorType, out var errorMessage)) 
                return GSEResult.Error(errorType!, errorMessage!);

            while (true)
            {
                if (!_pinStates.TryGetValue(physicalPin, out var state)) 
                    return GSEResult.Error("InvalidOperationException", $"Pin {physicalPin} is not open.");
                var newState = state with { Value = value };
                if (_pinStates.TryUpdate(physicalPin, newState, state))
                {
                    PinStateChanged?.Invoke(physicalPin, newState.Mode, newState.Value, newState.OwnerId, newState.OwnerType);
                    break;
                }
            }
            return GSEResult.OK;
        }

        internal GSEResult<string> ReadPin(IGpioSimulatorClient client, int physicalPin)
        {
            if (!CheckAccess(client, physicalPin, out var errorType, out var errorMessage)) 
                return GSEResult<string>.Error(errorType!, errorMessage!);

            if (_pinStates.TryGetValue(physicalPin, out var state))
            {
                return GSEResult<string>.OK(state.Value);
            }
            
            return GSEResult<string>.Error("InvalidOperationException", $"Pin {physicalPin} is not open.");
        }

        internal GSEResult SetPinMode(IGpioSimulatorClient client, int physicalPin, string mode)
        {
            if (!CheckAccess(client, physicalPin, out var errorType, out var errorMessage)) 
                return GSEResult.Error(errorType!, errorMessage!);

            while (true)
            {
                if (!_pinStates.TryGetValue(physicalPin, out var state)) 
                    return GSEResult.Error("InvalidOperationException", $"Pin {physicalPin} is not open.");
                var defaultVal = mode == "InputPullUp" ? "High" : "Low";
                var newState = state with { Mode = mode, Value = defaultVal };
                if (_pinStates.TryUpdate(physicalPin, newState, state))
                {
                    PinStateChanged?.Invoke(physicalPin, newState.Mode, newState.Value, newState.OwnerId, newState.OwnerType);
                    break;
                }
            }
            return GSEResult.OK;
        }

        internal GSEResult<string> GetPinMode(IGpioSimulatorClient client, int physicalPin)
        {
            return GSEResult<string>.OK(_pinStates.TryGetValue(physicalPin, out var state) ? state.Mode : "None");
        }

        internal GSEResult<bool> IsPinOpen(IGpioSimulatorClient client, int physicalPin)
        {
            return GSEResult<bool>.OK(_pinStates.ContainsKey(physicalPin));
        }

        internal GSEResult<string?> GetPinOwnerId(IGpioSimulatorClient client, int physicalPin)
        {
            return GSEResult<string?>.OK(_pinStates.TryGetValue(physicalPin, out var state) ? state.OwnerId : null);
        }

        /// <summary>
        /// Retrieves the current states of all open pins.
        /// </summary>
        /// <returns>A dictionary containing active pin states keyed by physical pin number.</returns>
        public Dictionary<int, (string Mode, string Value, string OwnerId, string OwnerType)> GetAllPinStates()
        {
            var states = new Dictionary<int, (string Mode, string Value, string OwnerId, string OwnerType)>();
            foreach (var kvp in _pinStates)
            {
                var state = kvp.Value;
                states[kvp.Key] = (state.Mode, state.Value, state.OwnerId, state.OwnerType);
            }
            return states;
        }
    }
}

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2 || NETCOREAPP3_0 || NETCOREAPP3_1 || NET45 || NET451 || NET452 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472 || NET48
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
