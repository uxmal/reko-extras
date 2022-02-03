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

        protected override TaskResult DoRun()
        {
            var clusters = new List<RtlInstructionCluster>();
            int iMark = iStart;
            try
            {
                var rw = CreateRewriter(iStart).GetEnumerator();
                while (iMark < iEnd && rw.MoveNext())
                {
                    clusters.Add(rw.Current);
                    iMark += rw.Current.Length;
                }
            } 
            catch (Exception ex)
            {
                ReportException(ex, iMark);
            }
            return new TaskResult
            {
                Clusters = clusters.ToArray()
            };
        }
    }
}
