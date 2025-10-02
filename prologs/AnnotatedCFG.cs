using Reko.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace annocfg;

class AnnotatedCFG
{
    private Project project;
    private object value;
    private long target_irsb_addr;
    private bool detect_loops;

    public AnnotatedCFG(Project project, object value, long target_irsb_addr, bool detect_loops)
    {
        this.project = project;
        this.value = value;
        this.target_irsb_addr = target_irsb_addr;
        this.detect_loops = detect_loops;
    }

    internal void from_digraph(object slice)
    {
        throw new NotImplementedException();
    }
}
