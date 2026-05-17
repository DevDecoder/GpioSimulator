using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace System.Device.Gpio
{
    public abstract class SimulatorDriverBase : GpioDriver
    {
        protected readonly ConcurrentDictionary<int, PinMode> OpenPins = new ConcurrentDictionary<int, PinMode>();
        protected readonly ConcurrentDictionary<int, PinValue> PinValues = new ConcurrentDictionary<int, PinValue>();
        protected readonly ConcurrentDictionary<int, ConcurrentBag<(PinEventTypes Types, PinChangeEventHandler Handler)>> Callbacks = 
            new ConcurrentDictionary<int, ConcurrentBag<(PinEventTypes Types, PinChangeEventHandler Handler)>>();

        public PinNumberingScheme NumberingScheme { get; }

        protected SimulatorDriverBase(PinNumberingScheme numberingScheme)
        {
            NumberingScheme = numberingScheme;
        }

        protected internal override int PinCount => 40;

        protected internal override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber) => pinNumber;

        protected internal override void OpenPin(int pinNumber)
        {
            OpenPin(pinNumber, PinMode.Input);
        }

        protected internal virtual void OpenPin(int pinNumber, PinMode mode)
        {
            OpenPinInternal(pinNumber, mode);
            OpenPins[pinNumber] = mode;
            var defaultVal = mode == PinMode.InputPullUp ? PinValue.High : PinValue.Low;
            PinValues[pinNumber] = defaultVal;
        }

        protected internal override void ClosePin(int pinNumber)
        {
            ClosePinInternal(pinNumber);
            OpenPins.TryRemove(pinNumber, out _);
            PinValues.TryRemove(pinNumber, out _);
        }

        protected internal override void Write(int pinNumber, PinValue value)
        {
            if (!OpenPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            WriteInternal(pinNumber, value);
            PinValues[pinNumber] = value;
        }

        protected internal override PinValue Read(int pinNumber)
        {
            if (!OpenPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            return ReadInternal(pinNumber);
        }

        protected internal override void SetPinMode(int pinNumber, PinMode mode)
        {
            if (!OpenPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            SetPinModeInternal(pinNumber, mode);
            OpenPins[pinNumber] = mode;
            var defaultVal = mode == PinMode.InputPullUp ? PinValue.High : PinValue.Low;
            PinValues[pinNumber] = defaultVal;
        }

        protected internal override PinMode GetPinMode(int pinNumber)
        {
            if (!OpenPins.TryGetValue(pinNumber, out var mode))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            return mode;
        }

        protected internal override bool IsPinOpen(int pinNumber)
        {
            return OpenPins.ContainsKey(pinNumber);
        }

        protected internal override bool IsPinModeSupported(int pinNumber, PinMode mode)
        {
            try
            {
                ConvertPin(pinNumber);
                return mode == PinMode.Input || 
                       mode == PinMode.Output || 
                       mode == PinMode.InputPullUp || 
                       mode == PinMode.InputPullDown;
            }
            catch
            {
                return false;
            }
        }

        protected internal override void RegisterCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
        {
            var list = Callbacks.GetOrAdd(pinNumber, _ => new ConcurrentBag<(PinEventTypes, PinChangeEventHandler)>());
            list.Add((eventTypes, callback));
        }

        protected internal override void UnregisterCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
        {
            if (Callbacks.TryGetValue(pinNumber, out var list))
            {
                var items = list.ToArray();
                var remaining = new ConcurrentBag<(PinEventTypes, PinChangeEventHandler)>();
                foreach (var item in items)
                {
                    if (item.Handler != callback)
                    {
                        remaining.Add(item);
                    }
                }
                Callbacks[pinNumber] = remaining;
            }
        }

        protected internal override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                return WaitForEvent(pinNumber, eventTypes, cts.Token);
            }
        }

        protected internal override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<PinEventTypes>();
            
            PinChangeEventHandler tempHandler = (sender, args) =>
            {
                tcs.TrySetResult(args.ChangeType);
            };

            RegisterCallbackForPinValueChangedEvent(pinNumber, eventTypes, tempHandler);

            try
            {
                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    tcs.Task.Wait(cancellationToken);
                    
                    return new WaitForEventResult
                    {
                        TimedOut = false,
                        EventTypes = tcs.Task.Result
                    };
                }
            }
            catch (OperationCanceledException)
            {
                return new WaitForEventResult
                {
                    TimedOut = true,
                    EventTypes = PinEventTypes.None
                };
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                return new WaitForEventResult
                {
                    TimedOut = true,
                    EventTypes = PinEventTypes.None
                };
            }
            finally
            {
                UnregisterCallbackForPinValueChangedEvent(pinNumber, tempHandler);
            }
        }

        protected void UpdatePinValue(int pinNumber, PinValue value)
        {
            PinValues[pinNumber] = value;
        }

        protected void ClearPinCache(int pinNumber)
        {
            OpenPins.TryRemove(pinNumber, out _);
            PinValues.TryRemove(pinNumber, out _);
        }

        protected void FireCallbacks(int pinNumber, PinEventTypes occurredType)
        {
            if (Callbacks.TryGetValue(pinNumber, out var list))
            {
                foreach (var item in list)
                {
                    if ((item.Types & occurredType) != PinEventTypes.None)
                    {
                        try
                        {
                            Task.Run(() => item.Handler(this, new PinValueChangedEventArgs(occurredType, pinNumber)));
                        }
                        catch { }
                    }
                }
            }
        }

        protected abstract int ConvertPin(int pinNumber);
        protected abstract void OpenPinInternal(int pinNumber, PinMode mode);
        protected abstract void ClosePinInternal(int pinNumber);
        protected abstract void WriteInternal(int pinNumber, PinValue value);
        protected abstract PinValue ReadInternal(int pinNumber);
        protected abstract void SetPinModeInternal(int pinNumber, PinMode mode);
        protected abstract PinMode GetPinModeInternal(int pinNumber);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                OpenPins.Clear();
                PinValues.Clear();
                Callbacks.Clear();
            }
        }
    }
}
