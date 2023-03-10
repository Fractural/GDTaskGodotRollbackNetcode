using System;
using System.Linq;
using Fractural.Tasks.Internal;
using System.Threading;
using Godot;
using System.Collections.Generic;

namespace Fractural.Tasks
{
    public interface IPlayerLoopTiming { }
    public class ProcessLoopTiming : IPlayerLoopTiming
    {
        public static readonly ProcessLoopTiming Type = new ProcessLoopTiming();
    }
    public class PhysicsProcessLoopTiming : IPlayerLoopTiming
    {
        public static readonly ProcessLoopTiming Type = new ProcessLoopTiming();
    }

    public interface IPlayerLoopItem
    {
        bool MoveNext();
    }

    /// <summary>
    /// Singleton that forwards Godot calls and values to GDTasks.
    /// </summary>
    public class GDTaskPlayerLoopAutoload : Node
    {
        public static int MainThreadId => Global.mainThreadId;
        public static bool IsMainThread => System.Threading.Thread.CurrentThread.ManagedThreadId == Global.mainThreadId;
        public static void AddAction(IPlayerLoopTiming timing, IPlayerLoopItem action) => Global.InstAddAction(timing, action);
        public static void ThrowInvalidLoopTiming(IPlayerLoopTiming playerLoopTiming) => throw new InvalidOperationException("Target playerLoopTiming is not injected. Please check PlayerLoopHelper.Initialize. PlayerLoopTiming:" + playerLoopTiming);
        public static void AddContinuation(IPlayerLoopTiming timing, Action continuation) => Global.InstAddContinuation(timing, continuation);
        public static void AddTiming(IPlayerLoopTiming timing) => Global.InstAddTiming(timing);
        public static void StepPlayerLoop(IPlayerLoopTiming timing) => Global.InstStepPlayerLoop(timing);

        public IReadOnlyCollection<IPlayerLoopTiming> PlayerLoopTimings => timingToIndexDict.Keys;
        private Dictionary<IPlayerLoopTiming, int> timingToIndexDict = new Dictionary<IPlayerLoopTiming, int>();

        private void InstAddAction(IPlayerLoopTiming timing, IPlayerLoopItem action)
        {
            var runner = runners[timingToIndexDict[timing]];
            if (runner == null)
            {
                ThrowInvalidLoopTiming(timing);
            }
            runner.AddAction(action);
        }

        // NOTE: Continuation means a asynchronous task invoked by another task after the other task finishes.
        private void InstAddContinuation(IPlayerLoopTiming timing, Action continuation)
        {
            var q = yielders[timingToIndexDict[timing]];
            if (q == null)
            {
                ThrowInvalidLoopTiming(timing);
            }
            q.Enqueue(continuation);
        }

        private void InstAddTiming(IPlayerLoopTiming timing)
        {
            if (timingToIndexDict.ContainsKey(timing))
                return;
            timingToIndexDict[timing] = yielders.Count;
            yielders.Add(new ContinuationQueue(timing));
            runners.Add(new PlayerLoopRunner(timing));
        }

        private void InstStepPlayerLoop(IPlayerLoopTiming timing)
        {
            int idx = timingToIndexDict[timing];
            yielders[idx].Run();
            runners[idx].Run();
        }

        public static GDTaskPlayerLoopAutoload Global { get; private set; }
        public float DeltaTime => GetProcessDeltaTime();
        public float PhysicsDeltaTime => GetPhysicsProcessDeltaTime();

        private int mainThreadId;
        private List<ContinuationQueue> yielders;
        private List<PlayerLoopRunner> runners;

        public override void _Ready()
        {
            if (Global != null)
            {
                QueueFree();
                return;
            }
            Global = this;

            mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            AddTiming(ProcessLoopTiming.Type);
            AddTiming(PhysicsProcessLoopTiming.Type);
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
            {
                if (Global == this)
                    Global = null;
                if (yielders != null)
                {
                    foreach (var yielder in yielders)
                        yielder.Clear();
                    foreach (var runner in runners)
                        runner.Clear();
                }
            }
        }

        public override void _Process(float delta)
        {
            StepPlayerLoop(ProcessLoopTiming.Type);
        }

        public override void _PhysicsProcess(float delta)
        {
            StepPlayerLoop(PhysicsProcessLoopTiming.Type);
        }
    }
}

