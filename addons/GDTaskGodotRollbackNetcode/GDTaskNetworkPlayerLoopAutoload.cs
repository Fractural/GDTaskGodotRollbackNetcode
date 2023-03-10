using Godot;
using Godot.Collections;
using GodotRollbackNetcode;

namespace Fractural.Tasks
{
    public class NetworkProcessLoopTiming : IPlayerLoopTiming
    {
        public static readonly NetworkProcessLoopTiming Type = new NetworkProcessLoopTiming();
    }
    public class NetworkPreprocessLoopTiming : IPlayerLoopTiming
    {
        public static readonly NetworkPreprocessLoopTiming Type = new NetworkPreprocessLoopTiming();
    }
    public class NetworkPostprocessLoopTiming : IPlayerLoopTiming
    {
        public static readonly NetworkPostprocessLoopTiming Type = new NetworkPostprocessLoopTiming();
    }

    public class GDTaskNetworkPlayerLoopAutoload : Node, INetworkProcess, INetworkPostProcess, INetworkPreProcess
    {
        public override void _Ready()
        {
            GDTaskPlayerLoopAutoload.AddTiming(NetworkProcessLoopTiming.Type);
            GDTaskPlayerLoopAutoload.AddTiming(NetworkPreprocessLoopTiming.Type);
            GDTaskPlayerLoopAutoload.AddTiming(NetworkPostprocessLoopTiming.Type);
        }

        public void _NetworkProcess(Dictionary input)
        {
            GDTaskPlayerLoopAutoload.StepPlayerLoop(NetworkProcessLoopTiming.Type);
        }

        public void _NetworkPreprocess(Dictionary input)
        {
            GDTaskPlayerLoopAutoload.StepPlayerLoop(NetworkPreprocessLoopTiming.Type);
        }
        public void _NetworkPostprocess(Dictionary input)
        {
            GDTaskPlayerLoopAutoload.StepPlayerLoop(NetworkPostprocessLoopTiming.Type);
        }
    }
}
