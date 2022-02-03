using Reko.Core;
using Reko.Core.Rtl;
using System.Collections.Generic;

namespace chunks
{
    public class TaskResult
    {
        public RtlInstructionCluster[]? Clusters { get; init; }
        public List<(Address, Address)>? Edges { get; init; }
    }
}