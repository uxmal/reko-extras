#region License
/* 
 * Copyright (C) 1999-2026 John Källén.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */
#endregion

using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;

namespace Reko.Extras.SeaOfNodes.UnitTests;

/// <summary>
/// Supports the building of a procedure without having to go through assembler.
/// </summary>
public class ProcedureBuilder : CodeEmitter
{
    private Dictionary<string, Block> blocks;
    private Block? branchBlock;
    private Block? lastBlock;
    private int numBlock;
    private List<ProcUpdater> unresolvedProcedures;
    public Address addrCur;

    public ProcedureBuilder()
        : this(new FakeArchitecture(new ServiceContainer()), null, Address.Ptr32(0x00123400), null)
    {
    }

    public ProcedureBuilder(string name)
    :
        this(new FakeArchitecture(new ServiceContainer()), name, Address.Ptr32(0x00123400), null)
    {
    }

    public ProcedureBuilder(IProcessorArchitecture arch)
        : this(arch, null, Address.Ptr32(0x00123400), null) {;
    }

    public ProcedureBuilder(IProcessorArchitecture arch, string? name)
        : this(arch, name, Address.Ptr32(0x00123400), null)
    {
    }

    public ProcedureBuilder(IProcessorArchitecture arch, string name, Address addr)
        : this(arch, name, addr, null)
    {
    }

    public ProcedureBuilder(IProcessorArchitecture arch, string name, Dictionary<string, Block> blocks)
        : this(arch, name, Address.Ptr32(0x00123400), blocks)
    {
    }

    public ProcedureBuilder(IProcessorArchitecture arch, string?name, Address addr, Dictionary<string, Block>? blocks)
    {
        this.InstructionSize = 1;
        name ??= this.GetType().Name;
        this.Architecture = arch ?? throw new ArgumentNullException(nameof(arch));
        this.Procedure = new Procedure(arch, name, addr, arch.CreateFrame());
        this.blocks = blocks ?? new Dictionary<string, Block>();
        this.unresolvedProcedures = new List<ProcUpdater>();
        this.addrCur = addr;
        BuildBody();
    }

    /// <summary>
    /// Current block, into which the next statement will be added.
    /// </summary>
    public Block? Block { get; private set; }
    public Procedure Procedure { get; private set; }
    public ProgramBuilder? ProgramBuilder { get; set; }
    public IProcessorArchitecture Architecture { get; private set; }
    public int InstructionSize { get; set; }

    protected Block BlockOf(string label)
    {
        if (!blocks.TryGetValue(label, out Block? b))
        {
            b = Procedure.AddBlock(default, label);
            blocks.Add(label, b);
        }
        return b;
    }

    public Statement BranchIf(Expression expr, string label)
    {
        Block b = EnsureBlock(null);
        branchBlock = BlockOf(label);

        var stm = Emit(new Branch(expr, branchBlock));
        TerminateBlock();
        return stm;
    }

    protected virtual void BuildBody()
    {
    }

    public CallInstruction Call(string procedureName, int retSizeOnStack)
    {
        var tmp = InvalidConstant.Create(PrimitiveType.Word32);
        var ci = new CallInstruction(tmp, new CallSite(retSizeOnStack, 0));
        unresolvedProcedures.Add(new ProcedureConstantUpdater(procedureName, ci));
        Emit(ci);
        return ci;
    }

    public CallInstruction Call(ProcedureBase callee, int retSizeOnStack)
    {
        var c = new ProcedureConstant(PrimitiveType.Ptr32, callee);
        var ci = new CallInstruction(c, new CallSite(retSizeOnStack, 0));
        Emit(ci);
        return ci;
    }

    public CallInstruction Call(Expression e, int retSizeOnstack)
    {
        var ci = new CallInstruction(e, new CallSite(retSizeOnstack, 0));
        Emit(ci);
        return ci;
    }

    public Statement Call(
        Expression e,
        int retSizeOnstack,
        IEnumerable<Identifier> uses,
        IEnumerable<Identifier> definitions)
    {
        var ci = new CallInstruction(e, new CallSite(retSizeOnstack, 0));
        ci.Uses.UnionWith(uses.Select(u => new CallBinding(u.Storage, u)));
        ci.Definitions.UnionWith(definitions.Select(d => new CallBinding(d.Storage, d)));
        return Emit(ci);
    }

    public Statement Call(
        string procedureName,
        int retSizeOnStack,
        IEnumerable<Identifier> uses,
        IEnumerable<Identifier> definitions)
    {
        var tmp = InvalidConstant.Create(PrimitiveType.Word32);
        var ci = new CallInstruction(tmp, new CallSite(retSizeOnStack, 0));
        ci.Uses.UnionWith(uses.Select(u => new CallBinding(u.Storage, u)));
        ci.Definitions.UnionWith(definitions.Select(d => new CallBinding(d.Storage, d)));
        unresolvedProcedures.Add(new ProcedureConstantUpdater(procedureName, ci));
        return Emit(ci);
    }

    public Statement Call(
        string procedureName,
        int retSizeOnStack,
        IEnumerable<(Storage stg, Expression e)> uses,
        IEnumerable<(Storage stg, Identifier e)> definitions)
    {
        var tmp = InvalidConstant.Create(PrimitiveType.Word32);
        var ci = new CallInstruction(tmp, new CallSite(retSizeOnStack, 0));
        ci.Uses.UnionWith(uses.Select(u => new CallBinding(u.stg, u.e)));
        ci.Definitions.UnionWith(definitions.Select(d => new CallBinding(d.stg, d.e)));
        unresolvedProcedures.Add(new ProcedureConstantUpdater(procedureName, ci));
        return Emit(ci);
    }

    public Statement Call(
        ProcedureBase callee,
        int retSizeOnStack,
        IEnumerable<(Storage stg, Expression e)> uses,
        IEnumerable<(Storage stg, Identifier e)> definitions)
    {
        var tmp = new ProcedureConstant(PrimitiveType.Ptr32, callee);
        var ci = new CallInstruction(tmp, new CallSite(retSizeOnStack, 0));
        ci.Uses.UnionWith(uses.Select(u => new CallBinding(u.stg, u.e)));
        ci.Definitions.UnionWith(definitions.Select(d => new CallBinding(d.stg, d.e)));
        return Emit(ci);
    }


    public void Compare(string flags, Expression a, Expression b)
    {
        var grf = Flags(flags);
        Assign(grf, Cond(grf.DataType, ISub(a, b)));
    }

    public Block? CurrentBlock
    {
        get { return this.Block; }
    }

    public Identifier Declare(DataType dt, string name)
    {
        return Procedure.Frame.CreateTemporary(name, dt);
    }

    public override Statement Emit(Instruction instr)
    {
        var block = EnsureBlock(null);
        var stm = block.Statements.Add(addrCur, instr);
        addrCur += (uint)InstructionSize;
        return stm;
    }

    public Identifier Flags(string s)
    {
        var grf = Architecture.GetFlagGroup(s)
            ?? throw new ArgumentException($"Unknown flag group {s}");
        return Frame.EnsureFlagGroup(grf);
    }

    public Application Fn(string name, params Expression[] exps)
    {
        var appl = base.Fn(
            new ProcedureConstant(PrimitiveType.Ptr32, new IntrinsicProcedure(name, true, VoidType.Instance, 0)),
            PrimitiveType.Word32, exps);
        unresolvedProcedures.Add(new ApplicationUpdater(name, appl));
        return appl;
    }

    public void Goto(string name)
    {
        var block = EnsureBlock(null);
        Block blockTo = BlockOf(name);
        Procedure.ControlGraph.AddEdge(block, blockTo);
        Block = null;
    }

    public Block Label(string name)
    {
        TerminateBlock();
        return EnsureBlock(name);
    }

    private Block EnsureBlock(string? name)
    {
        if (Block is not null)
            return Block;

        if (name is null)
        {
            ++numBlock;
            name = $"l{numBlock}";
        }
        Block = BlockOf(name);
        Block.Address = addrCur;
        if (Procedure.EntryBlock.Succ.Count == 0)
        {
            Procedure.ControlGraph.AddEdge(Procedure.EntryBlock, Block);
        }

        if (lastBlock is not null)
        {
            if (branchBlock is not null)
            {
                Procedure.ControlGraph.AddEdge(lastBlock, Block);
                Procedure.ControlGraph.AddEdge(lastBlock, branchBlock);
                branchBlock = null;
            }
            else
            {
                Procedure.ControlGraph.AddEdge(lastBlock, Block);
            }
            lastBlock = null;
        }
        return Block;
    }

    public void FinishProcedure()
    {
        TerminateBlock();
        Debug.Assert(lastBlock is not null);
        Procedure.ControlGraph.AddEdge(lastBlock, Procedure.ExitBlock);
    }

    public ICollection<ProcUpdater> UnresolvedProcedures
    {
        get { return unresolvedProcedures; }
    }

    public override Frame Frame
    {
        get { return Procedure.Frame; }
    }

    public Statement Phi(Identifier idDst, params (Expression, string)[] exprs)
    {
        var phi = new PhiFunction(
            idDst.DataType,
            exprs.Select(de => new PhiArgument(
                BlockOf(de.Item2),
                de.Item1))
            .ToArray());
        return Emit(new PhiAssignment(idDst, phi));
    }

    public Identifier Register(string name)
    {
        var reg = Architecture.GetRegister(name)
            ?? throw new ArgumentException($"Unknown register {name}");
        return Frame.EnsureRegister(reg);
    }

    public Identifier Register(RegisterStorage reg)
    {
        return Frame.EnsureRegister(reg);
    }

    public override void Return()
    {
        base.Return();
        Debug.Assert(Block is not null);
        Procedure.ControlGraph.AddEdge(Block, Procedure.ExitBlock);
        TerminateBlock();
        lastBlock = null;
    }

    public override void Return(Expression exp)
    {
        base.Return(exp);
        Debug.Assert(Block is not null);
        Procedure.ControlGraph.AddEdge(Block, Procedure.ExitBlock);
        TerminateBlock();
        lastBlock = null;
    }

    public void Switch(Expression e, params string[] labels)
    {
        Block[] blox = new Block[labels.Length];
        for (int i = 0; i < blox.Length; ++i)
        {
            blox[i] = BlockOf(labels[i]);
        }

        Emit(new SwitchInstruction(e, blox));
        for (int i = 0; i < blox.Length; ++i)
        {
            Debug.Assert(Block is not null);
            Procedure.ControlGraph.AddEdge(Block, blox[i]);
        }
        lastBlock = null;
        Block = null;
    }

    public void TerminateBlock()
    {
        if (Block is not null)
        {
            lastBlock = Block;
            Block = null;
        }
    }

    /// <summary>
    /// Call this method right after a terminating function.
    /// </summary>
    public void ExitThread()
    {
        lastBlock = null;
        Block = null;
    }

    public virtual Identifier Reg64(string name, int number)
    {
        return Frame.EnsureRegister(RegisterStorage.Reg64(name, number));
    }

    /// <summary>
    /// Returns an identifier for a 32-bit register. The name of the register is 
    /// specified by <paramref name="name"/>, and its storage domain by <paramref name="number"/>.
    /// </summary>
    /// <param name="name">Name of the register and identifier.</param>
    /// <param name="number">Storage domain of the register.</param>
    /// <returns>An identifier whose storage is the 32-bit register with the given 
    /// domain.
    /// </returns>
    public virtual Identifier Reg32(string name, int number)
    {
        return Frame.EnsureRegister(RegisterStorage.Reg32(name, number));
    }

    public virtual Identifier Reg32(string name)
    {
        var reg = Architecture.GetRegister(name)
            ?? throw new ArgumentException($"Unknown register {name}");
        return Frame.EnsureRegister(reg);
    }

    public virtual Identifier Reg16(string name, int number)
    {
        return Frame.EnsureRegister(RegisterStorage.Reg16(name, number));
    }

    public virtual Identifier Reg8(string name, int number)
    {
        return Frame.EnsureRegister(RegisterStorage.Reg8(name, number));
    }

    // Use this method to model the x86 "ah" or the z80 "h" registers which are 
    // offset from the start of their word registers.
    public virtual Identifier Reg8(string name, int number, uint bitOffset)
    {
        return Frame.EnsureRegister(RegisterStorage.Reg8(name, number, bitOffset));
    }
}
