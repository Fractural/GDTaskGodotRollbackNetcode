using System.Threading;
using Fractural.Tasks.Triggers;
using System;
using Fractural.Tasks.Internal;
using Godot;

namespace Fractural.Tasks
{

    public static partial class CancellationTokenSourceExtensions
    {
        readonly static Action<object> CancelCancellationTokenSourceStateDelegate = new Action<object>(CancelCancellationTokenSourceState);

        static void CancelCancellationTokenSourceState(object state)
        {
            var cts = (CancellationTokenSource)state;
            cts.Cancel();
        }

        public static IDisposable CancelAfterSlim(this CancellationTokenSource cts, int millisecondsDelay, DelayType delayType = DelayType.DeltaTime, IPlayerLoopTiming delayTiming = null)
        {
            return CancelAfterSlim(cts, TimeSpan.FromMilliseconds(millisecondsDelay), delayType, delayTiming);
        }

        public static IDisposable CancelAfterSlim(this CancellationTokenSource cts, TimeSpan delayTimeSpan, DelayType delayType = DelayType.DeltaTime, IPlayerLoopTiming delayTiming = null)
        {
            if (delayTiming == null)
                delayTiming = ProcessLoopTiming.Type;
            return PlayerLoopTimer.StartNew(delayTimeSpan, false, delayType, delayTiming, cts.Token, CancelCancellationTokenSourceStateDelegate, cts);
        }

        public static void RegisterRaiseCancelOnDestroy(this CancellationTokenSource cts, Node node)
        {
            var trigger = node.GetAsyncDestroyTrigger();
            trigger.CancellationToken.RegisterWithoutCaptureExecutionContext(CancelCancellationTokenSourceStateDelegate, cts);
        }
    }
}

