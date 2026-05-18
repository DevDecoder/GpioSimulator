using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DevDecoder.GpioSimulator.Common
{
    public record PinState(string Mode, string Value, string OwnerId, string OwnerType);

    public class GpioSimulatorEngine
    {
        private readonly ConcurrentDictionary<int, PinState> _pinStates = new ConcurrentDictionary<int, PinState>();

        public event Action<int, string, string, string, string>? PinStateChanged; // physicalPin, mode, value, ownerId, ownerType
        public event Action<int>? PinClosed; // physicalPin
        public event Action<string, Dictionary<int, int>>? BoardChanged; // boardId, activePhysToLog

        private readonly object _mappingLock = new object();
        private string _activeBoardId = "raspberry_pi_5_breakout";
        private Dictionary<int, int> _activePhysToLog = new Dictionary<int, int>();
        private Dictionary<int, int> _activeLogToPhys = new Dictionary<int, int>();

        public string ActiveBoardId
        {
            get { lock (_mappingLock) return _activeBoardId; }
        }

        public Dictionary<int, int> ActivePhysToLog
        {
            get { lock (_mappingLock) return new Dictionary<int, int>(_activePhysToLog); }
        }

        public Dictionary<int, int> ActiveLogToPhys
        {
            get { lock (_mappingLock) return new Dictionary<int, int>(_activeLogToPhys); }
        }

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

        public bool IsValidPhysicalPin(int physicalPin)
        {
            lock (_mappingLock)
            {
                return _activePhysToLog.ContainsKey(physicalPin);
            }
        }

        public bool IsValidLogicalPin(int logicalPin)
        {
            lock (_mappingLock)
            {
                return _activeLogToPhys.ContainsKey(logicalPin);
            }
        }

        public int ConvertLogicalToPhysical(int logicalPin)
        {
            lock (_mappingLock)
            {
                return _activeLogToPhys.TryGetValue(logicalPin, out int phys) ? phys : -1;
            }
        }

        public int ConvertPhysicalToLogical(int physicalPin)
        {
            lock (_mappingLock)
            {
                return _activePhysToLog.TryGetValue(physicalPin, out int log) ? log : -1;
            }
        }

        public bool TryOpenPin(int physicalPin, string mode, string ownerId, string ownerType, out string? errorType, out string? errorMessage)
        {
            errorType = null;
            errorMessage = null;

            if (!IsValidPhysicalPin(physicalPin))
            {
                errorType = "ArgumentException";
                errorMessage = $"Pin {physicalPin} is not a valid physical pin for this board.";
                return false;
            }

            var defaultVal = mode == "InputPullUp" ? "High" : "Low";
            var newState = new PinState(mode, defaultVal, ownerId, ownerType);

            if (!_pinStates.TryAdd(physicalPin, newState))
            {
                errorType = "InvalidOperationException";
                errorMessage = $"Pin {physicalPin} is already open.";
                return false;
            }

            PinStateChanged?.Invoke(physicalPin, mode, defaultVal, ownerId, ownerType);
            return true;
        }

        private bool CheckAccess(int physicalPin, string clientId, bool isAdmin, out string? errorType, out string? errorMessage)
        {
            errorType = null;
            errorMessage = null;

            if (!_pinStates.TryGetValue(physicalPin, out var state))
            {
                errorType = "InvalidOperationException";
                errorMessage = $"Pin {physicalPin} is not open.";
                return false;
            }

            if (state.OwnerId != clientId && !isAdmin)
            {
                errorType = "UnauthorizedAccessException";
                errorMessage = $"Client is not authorized to perform this action on pin {physicalPin}. It is owned by {state.OwnerId}.";
                return false;
            }

            return true;
        }

        public bool TryClosePin(int physicalPin, string clientId, bool isAdmin, out string? errorType, out string? errorMessage)
        {
            if (!CheckAccess(physicalPin, clientId, isAdmin, out errorType, out errorMessage)) return false;

            if (_pinStates.TryRemove(physicalPin, out _))
            {
                PinClosed?.Invoke(physicalPin);
            }
            return true;
        }

        public bool TryWritePin(int physicalPin, string value, string clientId, bool isAdmin, out string? errorType, out string? errorMessage)
        {
            if (!CheckAccess(physicalPin, clientId, isAdmin, out errorType, out errorMessage)) return false;

            while (true)
            {
                if (!_pinStates.TryGetValue(physicalPin, out var state)) return false;
                var newState = state with { Value = value };
                if (_pinStates.TryUpdate(physicalPin, newState, state))
                {
                    PinStateChanged?.Invoke(physicalPin, newState.Mode, newState.Value, newState.OwnerId, newState.OwnerType);
                    break;
                }
            }
            return true;
        }

        public bool TryReadPin(int physicalPin, string clientId, bool isAdmin, out string value, out string? errorType, out string? errorMessage)
        {
            value = "Low";
            if (!CheckAccess(physicalPin, clientId, isAdmin, out errorType, out errorMessage)) return false;

            if (_pinStates.TryGetValue(physicalPin, out var state))
            {
                value = state.Value;
                return true;
            }
            
            errorType = "InvalidOperationException";
            errorMessage = $"Pin {physicalPin} is not open.";
            return false;
        }

        public bool TrySetPinMode(int physicalPin, string mode, string clientId, bool isAdmin, out string? errorType, out string? errorMessage)
        {
            if (!CheckAccess(physicalPin, clientId, isAdmin, out errorType, out errorMessage)) return false;

            while (true)
            {
                if (!_pinStates.TryGetValue(physicalPin, out var state)) return false;
                var defaultVal = mode == "InputPullUp" ? "High" : "Low";
                var newState = state with { Mode = mode, Value = defaultVal };
                if (_pinStates.TryUpdate(physicalPin, newState, state))
                {
                    PinStateChanged?.Invoke(physicalPin, newState.Mode, newState.Value, newState.OwnerId, newState.OwnerType);
                    break;
                }
            }
            return true;
        }

        public string GetPinMode(int physicalPin)
        {
            return _pinStates.TryGetValue(physicalPin, out var state) ? state.Mode : "None";
        }

        public bool IsPinOpen(int physicalPin)
        {
            return _pinStates.ContainsKey(physicalPin);
        }

        public string? GetPinOwnerId(int physicalPin)
        {
            return _pinStates.TryGetValue(physicalPin, out var state) ? state.OwnerId : null;
        }

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
