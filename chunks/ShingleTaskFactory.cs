using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chunks
{
    public class ShingleTaskFactory : RewriterTaskFactory
    {
        public override string Name => "Shingle scan";

        public override RewriterTask Create(WorkUnit workUnit, int iStart, int iEnd)
        {
            return new ShingleTask(workUnit, iStart, iEnd);
        }
    }
}
