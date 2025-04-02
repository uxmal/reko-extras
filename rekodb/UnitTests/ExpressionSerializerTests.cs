using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Serialization.Json;
using Reko.Core.Types;
using Reko.Database.UnitTests.Mocks;

namespace Reko.Database.UnitTests
{
    [TestFixture]
    public class ExpressionSerializerTests
    {
        private ExpressionEmitter m;
        private FakeArchitecture arch;
        private Procedure proc;
        private Identifier seg;
        private Identifier r2;

        public ExpressionSerializerTests()
        {
            m = new ExpressionEmitter();
            arch = new FakeArchitecture();
            proc = Procedure.Create(
                arch,
                Address.Ptr32(0x00123400),
                new Frame(arch, PrimitiveType.Ptr32));
            seg = new Identifier("seg", PrimitiveType.SegmentSelector, new RegisterStorage("seg", 3, 0, PrimitiveType.SegmentSelector));
            r2 = new Identifier("r2", PrimitiveType.Int64, new RegisterStorage("r2", 2, 0, PrimitiveType.Word64));
        }

        private void RunTest(string sExpected, Expression e)
        {
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            var json = new JsonWriter(sw);
            var tyser = new TypeReferenceSerializer(json);
            var expser = new ExpressionSerializer(tyser, json);
            expser.Serialize(e);
            Assert.AreEqual(sExpected, sb.Replace("\"", "\'").ToString());
        }

        [Test]
        public void ExSer_Id()
        {
        }

        [Test]
        public void ExSer_SegmentedAccess()
        {
            RunTest("['m',[':','seg',{'c':'w16','v':'0x1234<16>'},'w16']", m.SegMem16(seg, m.Word16(0x1234)));
        }
    }
}
