using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Emulation;
using Reko.Core.Expressions;
using Reko.Core.Hll.C;
using Reko.Core.Loading;
using Reko.Core.Machine;
using Reko.Core.Memory;
using Reko.Core.Rtl;
using Reko.Core.Serialization;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.UnitTests;

public class FakePlatform : IPlatform
{
    private ServiceContainer sc;

    public FakePlatform(ServiceContainer sc, IProcessorArchitecture arch)
    {
        this.sc = sc;
        this.Architecture = arch;
    }

    public string Name => "fakeOS";

    public IProcessorArchitecture Architecture { get; }

    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> CallingConventions => throw new NotImplementedException();

    public string DefaultCallingConvention => throw new NotImplementedException();

    public Encoding DefaultTextEncoding { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public string Description { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public PrimitiveType FramePointerType => throw new NotImplementedException();

    public PlatformHeuristics Heuristics => throw new NotImplementedException();

    public MemoryMap_v1? MemoryMap { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string PlatformIdentifier => throw new NotImplementedException();

    public PrimitiveType PointerType => throw new NotImplementedException();

    public MaskedPattern[] ProcedurePrologs => throw new NotImplementedException();

    public IReadOnlySet<RegisterStorage> TrashedRegisters => throw new NotImplementedException();

    public IReadOnlySet<RegisterStorage> PreservedRegisters => throw new NotImplementedException();

    public int StructureMemberAlignment => throw new NotImplementedException();

    public Address AdjustProcedureAddress(Address addrCode)
    {
        throw new NotImplementedException();
    }

    public SegmentMap? CreateAbsoluteMemoryMap()
    {
        throw new NotImplementedException();
    }

    public CParser CreateCParser(TextReader rdr, ParserState? state = null)
    {
        throw new NotImplementedException();
    }

    public IPlatformEmulator CreateEmulator(SegmentMap segmentMap, Dictionary<Address, ImportReference> importReferences)
    {
        throw new NotImplementedException();
    }

    public TypeLibrary CreateMetadata()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Address> CreatePointerScanner(SegmentMap map, EndianImageReader rdr, IEnumerable<Address> addresses, PointerScannerFlags flags)
    {
        throw new NotImplementedException();
    }

    public (string, SerializedType, SerializedType)? DataTypeFromImportName(string importName)
    {
        throw new NotImplementedException();
    }

    public ICallingConvention? DetermineCallingConvention(FunctionType signature, IProcessorArchitecture? arch)
    {
        throw new NotImplementedException();
    }

    public DispatchProcedure_v1? FindDispatcherProcedureByAddress(Address addr)
    {
        throw new NotImplementedException();
    }

    public Constant? FindGlobalPointerValue(Program program, Address addrStart)
    {
        throw new NotImplementedException();
    }

    public ImageSymbol? FindMainProcedure(Program program, Address addrStart)
    {
        throw new NotImplementedException();
    }

    public SystemService? FindService(int vector, ProcessorState? state, IMemory? memory)
    {
        throw new NotImplementedException();
    }

    public SystemService? FindService(RtlInstruction call, ProcessorState? state, IMemory? memory)
    {
        throw new NotImplementedException();
    }

    public int GetBitSizeFromCBasicType(CBasicType cb)
    {
        throw new NotImplementedException();
    }

    public ICallingConvention? GetCallingConvention(string? ccName)
    {
        throw new NotImplementedException();
    }

    public string? GetPrimitiveTypeName(PrimitiveType t, string language)
    {
        throw new NotImplementedException();
    }

    public Trampoline? GetTrampolineDestination(Address addrJumpInstr, List<RtlInstructionCluster> clusters, IRewriterHost host)
    {
        throw new NotImplementedException();
    }

    public ProcedureBase? GetTrampolineDestination(Address addrInstr, IEnumerable<RtlInstruction> instrs, IRewriterHost host)
    {
        throw new NotImplementedException();
    }

    public void InjectProcedureEntryStatements(Procedure proc, Address addr, CodeEmitter emitter)
    {
        throw new NotImplementedException();
    }

    public List<RtlInstruction>? InlineCall(Address addrCallee, Address addrContinuation, EndianImageReader rdr, IStorageBinder binder)
    {
        throw new NotImplementedException();
    }

    public bool IsImplicitArgumentRegister(RegisterStorage reg)
    {
        throw new NotImplementedException();
    }

    public bool IsPossibleArgumentRegister(RegisterStorage reg)
    {
        throw new NotImplementedException();
    }

    public void LoadUserOptions(Dictionary<string, object> options)
    {
        throw new NotImplementedException();
    }

    public ProcedureCharacteristics? LookupCharacteristicsByName(string procName)
    {
        throw new NotImplementedException();
    }

    public ProcedureBase? LookupProcedureByAddress(Address address)
    {
        throw new NotImplementedException();
    }

    public ExternalProcedure? LookupProcedureByName(string? moduleName, string procName)
    {
        throw new NotImplementedException();
    }

    public ExternalProcedure? LookupProcedureByOrdinal(string moduleName, int ordinal)
    {
        throw new NotImplementedException();
    }

    public Address? MakeAddressFromConstant(Constant c, bool codeAlign)
    {
        throw new NotImplementedException();
    }

    public Address MakeAddressFromLinear(ulong uAddr, bool codeAlign)
    {
        throw new NotImplementedException();
    }

    public Storage? PossibleReturnValue(IEnumerable<Storage> storages)
    {
        throw new NotImplementedException();
    }

    public SerializedService PreprocessSerializedService(SerializedService service)
    {
        throw new NotImplementedException();
    }

    public Expression? ResolveImportByName(string? moduleName, string globalName)
    {
        throw new NotImplementedException();
    }

    public Expression? ResolveImportByOrdinal(string moduleName, int ordinal)
    {
        throw new NotImplementedException();
    }

    public Address? ResolveIndirectCall(RtlCall instr)
    {
        throw new NotImplementedException();
    }

    public Dictionary<string, object>? SaveUserOptions()
    {
        throw new NotImplementedException();
    }

    public ProcedureBase_v1? SignatureFromName(string fnName)
    {
        throw new NotImplementedException();
    }

    public bool TryParseAddress(string? sAddress, [MaybeNullWhen(false)] out Address addr)
    {
        throw new NotImplementedException();
    }

    public void WriteMetadata(Program program, string path)
    {
        throw new NotImplementedException();
    }
}
