using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Machine;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.UnitTests.Nodes;

public class FakeCallingConvention : ICallingConvention
{
    private Storage[] storages1;
    private Storage[] storages2;

    public FakeCallingConvention(Storage[] storages1, Storage[] storages2)
    {
        this.storages1 = storages1;
        this.storages2 = storages2;
    }

    public string Name => throw new NotImplementedException();

    public IComparer<Identifier>? InArgumentComparer => throw new NotImplementedException();

    public IComparer<Identifier>? OutArgumentComparer => throw new NotImplementedException();

    public void Generate(ICallingConventionBuilder ccr, int retAddressOnStack, DataType? dtRet, DataType? dtThis, List<DataType> dtParams)
    {
        throw new NotImplementedException();
    }

    public bool IsArgument(Storage stg)
    {
        throw new NotImplementedException();
    }

    public bool IsOutArgument(Storage stg)
    {
        throw new NotImplementedException();
    }
}