using Reko.Core;
using Reko.Core.Graphs;
using Reko.Extras.Interactive.ViewModels;
using Reko.Scanning;
using System.Threading;

namespace Reko.Extras.Interactive;

public class DecompilerHost : IDecompilerHost
{
    private ManualResetEventSlim pauseEvent;
    private readonly DiagnosticsViewModel diagnostics;

    public DecompilerHost(DiagnosticsViewModel diagnostics)
    {
        this.diagnostics = diagnostics;
        pauseEvent = new(false);
    }

    public void Run()
    {
        diagnostics.Start();
        pauseEvent.Set();
    }

    public void Pause()
    {
        pauseEvent.Reset();
    }

    public void OnBeforeInstruction(DiGraph<Address> cfg, RtlBlock block, Address addr)
    {
        if (!pauseEvent.IsSet)
        {
            pauseEvent.Wait();
        }
    }

    public void OnCompleted()
    {
    }
}
