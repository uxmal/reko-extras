using Reko.Core.Rtl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chunks
{
    public class LinearTask : RewriterTask
    {
        public LinearTask(WorkUnit workUnit, int iStart, int iEnd) : base(workUnit, iStart, iEnd)
        {
        }

        public override TaskResult Run()
        {
            IEnumerable<RtlInstructionCluster> rw = CreateRewriter(iStart);
            var clusters =
                (from cluster in rw
                 where cluster.Address - workUnit.MemoryArea.BaseAddress < iEnd
                 select cluster).ToArray();
            return new TaskResult
            {
                Clusters = clusters,
            };
        }
    }
}
