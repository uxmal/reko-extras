using NUnit.Framework;
using Reko.Core.Types;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Reko.Database.UnitTests
{
    [TestFixture]
    public class TypeReferenceDeserializerTests
    {
        private void RunTest(string sExpected, DataType dt)
        {
            var mem = new MemoryStream();
            var tw = new StreamWriter(mem, new UTF8Encoding(false));
            SerializeToWriter(dt, tw);

            mem.Position = 0;
            var trd = new TypeReferenceDeserializer(new JsonReader(mem));
            var dtNew = trd.Deserialize();

            var sw = new StringWriter();
            SerializeToWriter(dtNew, sw);
            Assert.AreEqual(sExpected, sw.ToString().Replace("\"", "\'"));
        }

        private static void SerializeToWriter(DataType dt, TextWriter tw)
        {
            var jw = new JsonWriter(tw);
            var trs = new TypeReferenceSerializer(jw);
            trs.Serialize(dt);
            tw.Flush();
        }

        [Test]
        public void TyrefDes_Int32()
        {
            RunTest("'i32'", PrimitiveType.Int32);
        }

        [Test]
        public void TyrefDes_Ptr_Int32()
        {
            RunTest("['p',16,'i32']", new Pointer(PrimitiveType.Int32, 16));
        }
    }
}
