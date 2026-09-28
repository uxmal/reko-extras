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

using Reko.Analysis;
using Reko.Core;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Analysis;
using Reko.Extras.SeaOfNodes.Nodes;
using ProjectionPropagator = Reko.Extras.SeaOfNodes.Analysis.ProjectionPropagator;
using SegmentedAccessClassifier = Reko.Extras.SeaOfNodes.Analysis.SegmentedAccessClassifier;

namespace Reko.Extras.SeaOfNodes.UnitTests.Analysis;

[TestFixture]
public class SegmentedAccessClassifierTests
{
    private ProcedureNode procNode;

    private void Prepare(ProcedureBuilder mock)
    {
        var program = new Program()
        {
            Architecture = mock.Architecture,
            Platform = new FakePlatform(new(), mock.Architecture)
        };
        var factory = new NodeFactory();
        var programFlow = new ProgramDataFlow();
        var procNode = new NodeGraphBuilder(factory, programFlow, program.Architecture)
            .Transform(mock.Procedure);
        this.procNode = procNode;
    }

    public class Mp1 : ProcedureBuilder
    {
        protected override void BuildBody()
        {
            var ds = this.Reg16("ds", 42);
            var ax = this.Reg16("ax", 0);
            var bx = this.Reg16("bx", 3);

            LoadId(ax, SegMem(PrimitiveType.Word16, ds, IAdd(bx, Int16(4))));
        }
    }

    public class Mp2 : ProcedureBuilder
    {
        // ds:bx is used in both fetches, so ds is strongly associated with bx.
        protected override void BuildBody()
        {
            var ds = this.Reg16("ds", 42);
            var ax = this.Reg16("ax", 0);
            var bx = this.Reg16("bx", 3);

            LoadId(ax, SegMem(PrimitiveType.Word16, ds, IAdd(bx, Int16(4))));
            LoadId(ax, SegMem(PrimitiveType.Word16, ds, IAdd(bx, Int16(8))));
        }
    }

    public class Mp3 : ProcedureBuilder
    {
        // ds is used both as ds:[bx+4] and ds:[0x3000<16>], which means
        // it isn't strongly associated with  any register.

        protected override void BuildBody()
        {
            var ds = this.Reg16("ds", 42);
            var ax = this.Reg16("ax", 0);
            var bx = this.Reg16("bx", 3);
            LoadId(ax, MembPtr16(ds, IAdd(bx, Int16(4))));
            LoadId(ax, MembPtr16(ds, Int16(0x3000)));
        }
    }
    [Test]
    public void SacAssociate()
    {
        var _foo = new TemporaryStorage("foo", 42, PrimitiveType.SegmentSelector);
        var _bar = new TemporaryStorage("bar", 43, PrimitiveType.Word16);
        var m = new NodeFactory();
        var foo = m.Def(procNode, _foo);
        var bar = m.Def(procNode, _bar);
        var mpc = new SegmentedAccessClassifier(procNode);
        mpc.Associate(foo, bar);
        Assert.That(mpc.AssociatedIdentifier(foo), Is.Not.Null, "Bar should be associated");
        mpc.Associate(foo, bar);
        Assert.That(mpc.AssociatedIdentifier(foo), Is.Not.Null, "Bar should still be associated");
    }

    [Test]
    public void SacDisassociate()
    {
        var m = new NodeFactory();
        var foo = m.Def(procNode, new TemporaryStorage("foo", 42, PrimitiveType.SegmentSelector));
        var bar = m.Def(procNode, new TemporaryStorage("bar", 43, PrimitiveType.Word16));
        var baz = m.Def(procNode, new TemporaryStorage("baz", 44, PrimitiveType.Word16));
        SegmentedAccessClassifier mpc = new SegmentedAccessClassifier(procNode);
        mpc.Associate(foo, bar);
        mpc.Associate(foo, baz);
        Assert.That(mpc.AssociatedIdentifier(foo), Is.Null, "Bar should no longer be associated");
    }

    [Test]
    public void SacAssociateConsts()
    {
        var m = new NodeFactory();
        var ptr = m.Def(procNode, new TemporaryStorage("ptr", 42, PrimitiveType.SegmentSelector));
        var mpc = new SegmentedAccessClassifier(procNode);
        mpc.Associate(ptr, m.Word32(3));
        mpc.Associate(ptr, m.Word32(4));
        Assert.That(mpc.IsOnlyAssociatedWithConstants(ptr), Is.True, "Should only have been associated with constants");
    }

    [Test]
    public void SacDisassociateConsts()
    {
        var m = new NodeFactory();
        var ptr = m.Def(procNode, new TemporaryStorage("ptr", 42, PrimitiveType.SegmentSelector));
        var mp = m.Def(procNode, new TemporaryStorage("mp", 43, PrimitiveType.SegmentSelector));
        var mpc = new SegmentedAccessClassifier(procNode);
        mpc.Associate(ptr, m.Word32(3));
        mpc.Associate(ptr, mp);
        mpc.Associate(ptr, m.Word32(4));
        Assert.That(mpc.IsOnlyAssociatedWithConstants(ptr), Is.False, "Should have been disassociated");
    }

    [Test]
    public void SacClassify1()
    {
        Prepare(new Mp1());
        var mpc = new SegmentedAccessClassifier(procNode);
        mpc.Classify();
        var allNodes = procNode.CollectReachableNodes().ToArray();
        var ds = allNodes.Single(s => s.Storage?.Name == "ds");
        Assert.That(ds.Storage!.Name, Is.EqualTo("ds"));
        var bx = allNodes.Single(s => s.Storage?.Name == "bx");
        Assert.That(bx.Storage!.Name, Is.EqualTo("bx"));
        Node? a = mpc.AssociatedIdentifier(ds);
        Assert.That(a, Is.SameAs(bx));
    }

    [Test]
    public void SacClassify2()
    {
        Prepare(new Mp2());
        var mpc = new SegmentedAccessClassifier(procNode);
        mpc.Classify();
        var allNodes = procNode.CollectReachableNodes().ToArray();
        var ds = allNodes.Single(s => s.Storage?.Name == "ds");
        Assert.That(ds.Storage!.Name, Is.EqualTo("ds"));
        var bx = allNodes.Single(s => s.Storage?.Name == "bx");
        Assert.That(bx.Storage!.Name, Is.EqualTo("bx"));
        Node? a = mpc.AssociatedIdentifier(ds);
        Assert.That(a, Is.SameAs(bx));
    }

    [Test]
    public void SacClassify3()
    {
        Prepare(new Mp3());
        SegmentedAccessClassifier mpc = new SegmentedAccessClassifier(procNode);
        mpc.Classify();
        var allNodes = procNode.CollectReachableNodes().ToArray();
        var ds = allNodes.Single(s => s.Storage?.Name == "ds");
        Assert.That(ds.Storage!.Name, Is.EqualTo("ds"));
        var bx = allNodes.Single(s => s.Storage?.Name == "bx");
        Assert.That(bx.Storage!.Name, Is.EqualTo("bx"));
        Node? a = mpc.AssociatedIdentifier(ds);
        Assert.That(a, Is.Null, "ds is used both as ds:[bx+4] and ds:[0x3000<16>], it should't be strongly associated with a register");
    }
}
