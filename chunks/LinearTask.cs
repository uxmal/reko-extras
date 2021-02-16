using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chunks
{
    public class LinearTask : RewriterTask
    {
        public LinearTask(WorkUnit workUnit, int i, int v) : base(workUnit, i, v)
        {
        }
    }
}
