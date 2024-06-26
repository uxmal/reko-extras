﻿using Reko.Core;
using Reko.Core.Memory;
using Reko.Core.Rtl;
using Reko.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chunks
{
    public class ShingleTask : RewriterTask
    {
        private readonly Dictionary<int, IEnumerator<RtlInstructionCluster>> rewriters;

        public ShingleTask(WorkUnit work, int iStart, int iEnd)
            : base(work, iStart, iEnd)
        {
            rewriters = new Dictionary<int, IEnumerator<RtlInstructionCluster>>();
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
                    if (!rw.MoveNext())
                        continue;
                    var cluster = rw.Current;
                    rtls.Add(cluster);

                    CacheRewriter(i + cluster.Length, rw);
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

        private IEnumerator<RtlInstructionCluster> GetRewriter(int i)
        {
            if (rewriters.TryGetValue(i, out var rw))
            {
                rewriters.Remove(i);
                return rw;
            }
            return base.CreateRewriter(i).GetEnumerator();
        }

        private void CacheRewriter(int i, IEnumerator<RtlInstructionCluster> rw)
        {
            rewriters[i] = rw;  // Stomps on existing rewriters. This may not be 100% optimal
        }
    }
}
