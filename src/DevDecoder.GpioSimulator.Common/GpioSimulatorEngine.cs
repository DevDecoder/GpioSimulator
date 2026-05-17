using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DevDecoder.GpioSimulator.Common
{
    public class GpioSimulatorEngine
    {
        private readonly ConcurrentDictionary<int, string> _pinModes = new ConcurrentDictionary<int, string>();
        private readonly ConcurrentDictionary<int, string> _pinValues = new ConcurrentDictionary<int, string>();
        private readonly ConcurrentDictionary<int, string> _pinOwnerIds = new ConcurrentDictionary<int, string>();
        private readonly ConcurrentDictionary<int, string> _pinOwnerTypes = new ConcurrentDictionary<int, string>();

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

            if (_pinModes.ContainsKey(physicalPin))
            {
                errorType = "InvalidOperationException";
                errorMessage = $"Pin {physicalPin} is already open.";
                return false;
            }

            _pinModes[physicalPin] = mode;
            var defaultVal = mode == "InputPullUp" ? "High" : "Low";
            _pinValues[physicalPin] = defaultVal;
            _pinOwnerIds[physicalPin] = ownerId;
            _pinOwnerTypes[physicalPin] = ownerType;

            PinStateChanged?.Invoke(physicalPin, mode, defaultVal, ownerId, ownerType);
            return true;
        }

        public void ClosePin(int physicalPin)
        {
            if (_pinModes.TryRemove(physicalPin, out _))
            {
                _pinValues.TryRemove(physicalPin, out _);
                _pinOwnerIds.TryRemove(physicalPin, out _);
                _pinOwnerTypes.TryRemove(physicalPin, out _);
                PinClosed?.Invoke(physicalPin);
            }
        }

        public void WritePin(int physicalPin, string value, string ownerId, string ownerType)
        {
            if (!_pinModes.ContainsKey(physicalPin)) return;

            _pinValues[physicalPin] = value;
            PinStateChanged?.Invoke(physicalPin, _pinModes[physicalPin], value, ownerId, ownerType);
        }

        public string ReadPin(int physicalPin)
        {
            return _pinValues.TryGetValue(physicalPin, out var val) ? val : "Low";
        }

        public void SetPinMode(int physicalPin, string mode)
        {
            if (!_pinModes.ContainsKey(physicalPin)) return;

            _pinModes[physicalPin] = mode;
            var defaultVal = mode == "InputPullUp" ? "High" : "Low";
            _pinValues[physicalPin] = defaultVal;

            _pinOwnerIds.TryGetValue(physicalPin, out var ownerId);
            _pinOwnerTypes.TryGetValue(physicalPin, out var ownerType);

            PinStateChanged?.Invoke(physicalPin, mode, defaultVal, ownerId ?? "", ownerType ?? "");
        }

        public string GetPinMode(int physicalPin)
        {
            return _pinModes.TryGetValue(physicalPin, out var mode) ? mode : "None";
        }

        public bool IsPinOpen(int physicalPin)
        {
            return _pinModes.ContainsKey(physicalPin);
        }

        public Dictionary<int, (string Mode, string Value, string OwnerId, string OwnerType)> GetAllPinStates()
        {
            var states = new Dictionary<int, (string Mode, string Value, string OwnerId, string OwnerType)>();
            foreach (var pin in _pinModes.Keys)
            {
                _pinModes.TryGetValue(pin, out var mode);
                _pinValues.TryGetValue(pin, out var value);
                _pinOwnerIds.TryGetValue(pin, out var ownerId);
                _pinOwnerTypes.TryGetValue(pin, out var ownerType);
                states[pin] = (mode ?? "None", value ?? "Low", ownerId ?? "", ownerType ?? "");
            }
            return states;
        }
    }
}
