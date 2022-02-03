using Reko.Core.Rtl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chunks
{
    public class LinearShingleTask : RewriterTask
    {
        private readonly bool[] visited;

        public LinearShingleTask(WorkUnit work, int iStart, int iEnd)
            : base(work, iStart, iEnd)
        {
            visited = new bool[iEnd - iStart];
        }

        protected override TaskResult DoRun()
        {
            var step = workUnit.Architecture.InstructionBitSize / workUnit.MemoryArea.CellBitSize;
            var rtls = new List<RtlInstructionCluster>();
            for (int i = iStart; i < iEnd; i += step)
            {
                try
                {
                    var rw = GetRewriter(i);
                    if (rw is not null)
                    {
                        int iMark = i - iStart;
                        while (iMark < visited.Length && !visited[iMark] && rw.MoveNext())
                        {
                            visited[iMark] = true;
                            var cluster = rw.Current;
                            rtls.Add(cluster);
                            iMark += cluster.Length;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ReportException(ex, i))
                        break;
                }
            }
            return new TaskResult
            {
                Clusters = rtls.ToArray()
            };
        }

        private IEnumerator<RtlInstructionCluster>? GetRewriter(int i)
        {
            if (visited[i - iStart])
                return null;
            return base.CreateRewriter(i).GetEnumerator();
        }
    }
}

