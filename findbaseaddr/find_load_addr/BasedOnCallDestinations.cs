using Reko.Core;
using Reko.Core.Machine;
using Reko.Core.Memory;
using System.Collections.Generic;

public class CallDestinationLoadAddressFinder
{
    private IProcessorArchitecture arch;
    private MemoryArea mem;

    public CallDestinationLoadAddressFinder(
        IProcessorArchitecture arch,
        MemoryArea mem)
    {
        this.arch = arch;
        this.mem = mem;
    }

    public void Find()
    {
        //var addrCallTargets = FindAllPossibleCallTargets();
        var addrProcEntries = FindAllPossibleProcedureEntries();


    }

    private HashSet<Address> FindAllPossibleProcedureEntries()
    {
        var dasms = new Dictionary<int, IEnumerator<MachineInstruction>>();
        throw new System.NotImplementedException();
    }


}
