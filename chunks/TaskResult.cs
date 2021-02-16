using Reko.Core.Rtl;

namespace chunks
{
    public class TaskResult
    {
        public RtlInstructionCluster[] Clusters { get; internal set; }
    }
}