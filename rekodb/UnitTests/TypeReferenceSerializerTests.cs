using NUnit.Framework;
using Reko.Core.Serialization.Json;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reko.Database;

namespace Reko.Database.UnitTests
{
    internal class TypeReferenceSerializerTests
    {
        private void RunTest(string sExpected, DataType dt)
        {
            var sw = new StringWriter();
            var json = new JsonWriter(sw);
            var tyrefser = new TypeReferenceSerializer(json);
            tyrefser.Serialize(dt);
            Assert.AreEqual(sExpected, sw.ToString().Replace('\"','\''));
        }

        [Test]
        public void TyRefSer_Primitive()
        {
            var dt = PrimitiveType.Real64;
            RunTest("'r64'", dt);
        }

        [Test]
        public void TyRefSer_Ptr()
        {
            var dt = new Pointer(PrimitiveType.UInt16, 32);
            RunTest("['p',32,'u16']", dt);
        }
    }
}
