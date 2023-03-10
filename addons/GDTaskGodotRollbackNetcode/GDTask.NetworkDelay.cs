using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fractural.Tasks
{
    // NOTE: Purusing async await rollback in C# may be more cumbersome than it's worth.
    //       This is because async await are lower-level language features that create statemachines
    //       that form a chain of calls. I don't believe it's possible to easily revert that
    //       statemachine to a previous state.
    public partial struct GDTask
    {
        public static GDTask DelayTicks(int delayTickCount, IPlayerLoopTiming delayTiming = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (delayTiming == null) delayTiming = NetworkProcessLoopTiming.Type;
            if (delayTickCount < 0)
            {
                throw new ArgumentOutOfRangeException("Delay does not allow minus delayTickCount. delayTickCount:" + delayTickCount);
            }

            return new GDTask(DelayTicksPromise.Create(delayTickCount, delayTiming, cancellationToken, out var token), token);
        }

        sealed class DelayTicksPromise : IGDTaskSource, IPlayerLoopItem, ITaskPoolNode<DelayTicksPromise>
        {
            static TaskPool<DelayTicksPromise> pool;
            DelayTicksPromise nextNode;
            public ref DelayTicksPromise NextNode => ref nextNode;

            static DelayTicksPromise()
            {
                TaskPool.RegisterSizeGetter(typeof(DelayTicksPromise), () => pool.Size);
            }

            int initialFrame;
            int delayFrameCount;
            CancellationToken cancellationToken;

            int currentFrameCount;
            GDTaskCompletionSourceCore<AsyncUnit> core;

            DelayTicksPromise()
            {
            }

            public static IGDTaskSource Create(int delayFrameCount, IPlayerLoopTiming timing, CancellationToken cancellationToken, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AutoResetGDTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);
                }

                if (!pool.TryPop(out var result))
                {
                    result = new DelayTicksPromise();
                }

                result.delayFrameCount = delayFrameCount;
                result.cancellationToken = cancellationToken;
                result.initialFrame = GDTaskPlayerLoopAutoload.IsMainThread ? Engine.GetFramesDrawn() : -1;

                TaskTracker.TrackActiveTask(result, 3);

                GDTaskPlayerLoopAutoload.AddAction(timing, result);

                token = result.core.Version;
                return result;
            }

            public void GetResult(short token)
            {
                try
                {
                    core.GetResult(token);
                }
                finally
                {
                    TryReturn();
                }
            }

            public GDTaskStatus GetStatus(short token)
            {
                return core.GetStatus(token);
            }

            public GDTaskStatus UnsafeGetStatus()
            {
                return core.UnsafeGetStatus();
            }

            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                core.OnCompleted(continuation, state, token);
            }

            public bool MoveNext()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (currentFrameCount == 0)
                {
                    if (delayFrameCount == 0) // same as Yield
                    {
                        core.TrySetResult(AsyncUnit.Default);
                        return false;
                    }

                    // skip in initial frame.
                    if (initialFrame == Engine.GetFramesDrawn())
                    {
#if DEBUG
                        // force use Realtime.
                        if (GDTaskPlayerLoopAutoload.IsMainThread && Engine.EditorHint)
                        {
                            //goto ++currentFrameCount
                        }
                        else
                        {
                            return true;
                        }
#else
                        return true;
#endif
                    }
                }

                if (++currentFrameCount >= delayFrameCount)
                {
                    core.TrySetResult(AsyncUnit.Default);
                    return false;
                }

                return true;
            }

            bool TryReturn()
            {
                TaskTracker.RemoveTracking(this);
                core.Reset();
                currentFrameCount = default;
                delayFrameCount = default;
                cancellationToken = default;
                return pool.TryPush(this);
            }
        }
    }
}
