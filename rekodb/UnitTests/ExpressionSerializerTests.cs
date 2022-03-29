using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Database.UnitTests
{
    [TestFixture]
    public class ExpressionSerializerTests
    {
        [Test]
        public void ExSer_Id()
        {
            var id = new Identifier("r2", PrimitiveType.Int64, new RegisterStorage("r2", 2, 0, PrimitiveType.Word64));
        }
    }
}
