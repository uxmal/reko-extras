using Reko.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive;

public interface IDecompilerHost
{
    void Run();
    void Pause();
    void OnBeforeInstruction(Core.Graphs.DiGraph<Address> cfg, Scanning.RtlBlock block, Address addr);
    void OnCompleted();
}
