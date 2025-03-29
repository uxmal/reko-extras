using Reko.Core;
using Reko.Core.Machine;
using Reko.Core.Memory;
using Reko.Core.Rtl;

namespace Reko.Extras.blocksoup;

public abstract class Adapter<T>
{
    public abstract IEnumerator<T> CreateNewEnumerator(EndianImageReader rdr);
    public abstract Address NextAddress(T item);
}

public class InstrAdapter : Adapter<MachineInstruction>
{
    private readonly IProcessorArchitecture arch;

    public InstrAdapter(IProcessorArchitecture arch)
    {
        this.arch = arch;
    }

    public override IEnumerator<MachineInstruction> CreateNewEnumerator(EndianImageReader rdr)
    {
        return arch.CreateDisassembler(rdr).GetEnumerator();
    }

    public override Address NextAddress(MachineInstruction item)
    {
        return item.Address + item.Length;
    }
}


public class ClusterAdapter : Adapter<RtlInstructionCluster>
{
    private IProcessorArchitecture arch;
    private IRewriterHost host;
    private StorageBinder binder;
    private ProcessorState state;

    public ClusterAdapter(IProcessorArchitecture arch, IRewriterHost host)
    {
        this.arch = arch;
        this.host = host;
        this.binder = new StorageBinder();
        this.state = arch.CreateProcessorState();
    }

    public override IEnumerator<RtlInstructionCluster> CreateNewEnumerator(EndianImageReader rdr)
    {
        return arch.CreateRewriter(rdr, state, binder, host).GetEnumerator();
    }

    public override Address NextAddress(RtlInstructionCluster item)
    {
        return item.Address + item.Length;
    }
}