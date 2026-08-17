using Reko.Core;
using Reko.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Analysis;

public class NodeAnalysisContext
{
    public NodeAnalysisContext(
        IReadOnlyProgram program,
        PeepholeOptimizer peepholeOptimizer,
        IEventListener listener)
    {
        this.Program = program;
        this.PeepholeOptimizer = peepholeOptimizer;
        this.EventListener = listener;
    }

    public IReadOnlyProgram Program { get; }
    public PeepholeOptimizer PeepholeOptimizer { get; }
    public IEventListener EventListener { get; }
}
