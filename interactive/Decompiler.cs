using Reko.Core;
using Reko.Core.Graphs;
using Reko.Core.Services;
using Reko.Scanning;
using System;
using System.Collections.Generic;

namespace Reko.Extras.Interactive;

public class Decompiler
{
    private readonly IServiceProvider services;
    private readonly IEventListener listener;
    private readonly IDecompilerHost host;
    private readonly Core.Program program;
    private readonly IRewriterHost rwhost;

    public Decompiler(
        IServiceProvider services, 
        IEventListener listener,
        IDecompilerHost host,
        IRewriterHost rwhost,
        Reko.Core.Program program)
    {
        this.listener = listener;
        this.services = services;
        this.host = host;
        this.program = program;
        this.rwhost = rwhost;
    }

    public ScanResults ScanImage()
    {
        var scanner = new Scanner(program, listener, host, rwhost);
        var scanResults = scanner.ScanImage();

        var procBuilder = new ProcedureBuilder(scanResults, program, listener);
        procBuilder.BuildProcedures();
        return scanResults;
    }
}
