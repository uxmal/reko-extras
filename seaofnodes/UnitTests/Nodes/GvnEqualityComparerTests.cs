using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.UnitTests.Nodes;

[TestFixture]
public class GvnEqualityComparerTests
{
    private NodeFactory factory = new NodeFactory();

    [Test]
    public void Gvneq_Constants_SameDataType()
    {
        var c1 = factory.Const(PrimitiveType.Int32, 3);
        var c2 = factory.Const(PrimitiveType.Int32, 3);

        var cmp = new GvnEqualityComparer();
        Assert.That(cmp.Equals(c1, c2), Is.True);
        Assert.That(cmp.GetHashCode(c1), Is.EqualTo(cmp.GetHashCode(c2)));
    }
}
