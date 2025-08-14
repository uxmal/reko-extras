using Reko.Core;
using Reko.Extras.Interactive.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive;

public class Decompiler
{
    private readonly IDecompilerHost rwhost;
    private readonly Core.Program program;
    private readonly StorageBinder binder;

    public Decompiler(IDecompilerHost host, Reko.Core.Program program)
    {
        this.rwhost  = host;
        this.program = program;
        this.binder = new StorageBinder();
        this.rwhost = new RewriterHost();
    }

    public void ScanImage()
    {
        var wis = CollectWorkitems();
        HashSet<Address> visited = [];
        while (wis.TryDequeue(out var wi))
        {
            if (!visited.Add(wi.Address))
                continue;
            this.Process(wi);
        }
    }

    private void Process(Workitem wi)
    {
        if (!program.TryCreateImageReader(wi.Address, out var rdr))
            return;
        var rw = program.Architecture.CreateRewriter(rdr, wi.State, binder, rwhost);
        throw new NotImplementedException();
    }

    private Queue<Workitem> CollectWorkitems()
    {
        throw new NotImplementedException();
    }
}
