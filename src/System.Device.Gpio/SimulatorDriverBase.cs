using System;
using System.Collections.Concurrent;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Tasks;

namespace DevDecoder.GpioSimulator
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

#if SHIM_BUILD
        protected internal override int PinCount => 40;
#else
        protected override int PinCount => 40;
#endif

#if SHIM_BUILD
        protected internal override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber) => pinNumber;
#else
        protected override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber) => pinNumber;
#endif

#if SHIM_BUILD
        protected internal override void OpenPin(int pinNumber)
#else
        protected override void OpenPin(int pinNumber)
#endif
        {
            OpenPin(pinNumber, PinMode.Input);
        }

#if SHIM_BUILD
        protected internal virtual void OpenPin(int pinNumber, PinMode mode)
#else
        protected virtual void OpenPin(int pinNumber, PinMode mode)
#endif
        {
            OpenPinInternal(pinNumber, mode);
            OpenPins[pinNumber] = mode;
            var defaultVal = mode == PinMode.InputPullUp ? PinValue.High : PinValue.Low;
            PinValues[pinNumber] = defaultVal;
        }

#if SHIM_BUILD
        protected internal override void ClosePin(int pinNumber)
#else
        protected override void ClosePin(int pinNumber)
#endif
        {
            ClosePinInternal(pinNumber);
            OpenPins.TryRemove(pinNumber, out _);
            PinValues.TryRemove(pinNumber, out _);
        }

#if SHIM_BUILD
        protected internal override void Write(int pinNumber, PinValue value)
#else
        protected override void Write(int pinNumber, PinValue value)
#endif
        {
            if (!OpenPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            WriteInternal(pinNumber, value);
            PinValues[pinNumber] = value;
        }

#if SHIM_BUILD
        protected internal override PinValue Read(int pinNumber)
#else
        protected override PinValue Read(int pinNumber)
#endif
        {
            if (!OpenPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            return ReadInternal(pinNumber);
        }

#if SHIM_BUILD
        protected internal override void SetPinMode(int pinNumber, PinMode mode)
#else
        protected override void SetPinMode(int pinNumber, PinMode mode)
#endif
        {
            if (!OpenPins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            SetPinModeInternal(pinNumber, mode);
            OpenPins[pinNumber] = mode;
            var defaultVal = mode == PinMode.InputPullUp ? PinValue.High : PinValue.Low;
            PinValues[pinNumber] = defaultVal;
        }

#if SHIM_BUILD
        protected internal override PinMode GetPinMode(int pinNumber)
#else
        protected override PinMode GetPinMode(int pinNumber)
#endif
        {
            if (!OpenPins.TryGetValue(pinNumber, out var mode))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            return mode;
        }

#if SHIM_BUILD
        protected internal override bool IsPinOpen(int pinNumber)
#else
        public bool IsPinOpen(int pinNumber)
#endif
        {
            return OpenPins.ContainsKey(pinNumber);
        }

#if SHIM_BUILD
        protected internal override bool IsPinModeSupported(int pinNumber, PinMode mode)
#else
        protected override bool IsPinModeSupported(int pinNumber, PinMode mode)
#endif
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

#if SHIM_BUILD
        protected internal override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
#else
        protected override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
#endif
        {
            var list = Callbacks.GetOrAdd(pinNumber, _ => new ConcurrentBag<(PinEventTypes, PinChangeEventHandler)>());
            list.Add((eventTypes, callback));
        }

#if SHIM_BUILD
        protected internal override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
#else
        protected override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
#endif
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

#if SHIM_BUILD
        protected internal override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
#else
        protected override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
#endif
        {
            var tcs = new TaskCompletionSource<PinEventTypes>();
            
            PinChangeEventHandler tempHandler = (sender, args) =>
            {
                tcs.TrySetResult(args.ChangeType);
            };

            AddCallbackForPinValueChangedEvent(pinNumber, eventTypes, tempHandler);

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
                RemoveCallbackForPinValueChangedEvent(pinNumber, tempHandler);
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
