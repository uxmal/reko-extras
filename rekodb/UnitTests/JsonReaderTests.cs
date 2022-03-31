using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Database.UnitTests
{
    [TestFixture]
    public class JsonReaderTests
    {
        private JsonReader rdr = default!;

        private void Lex(string json)
        {
            var utf8json = Encoding.UTF8.GetBytes(json.Replace('\'', '\"'));
            this.rdr = new JsonReader(new MemoryStream(utf8json));
        }

        [TearDown]
        public void TearDown()
        {
            rdr = null!;
        }

        private void AssertTokens(params JsonToken [] tokens)
        {
            foreach (var token in tokens)
            {
                var t = rdr.Read();
                Assert.AreEqual(token, t);
            }
        }

        [Test]
        public void Jr_Zero()
        {
            Lex("0");
            AssertTokens(JsonToken.Number);
            Assert.IsTrue(rdr.TryGetDouble(out var num));
            Assert.AreEqual(0.0, num);
        }

        [Test]
        public void Jr_Exponent()
        {
            Lex(" -21.5e-04");
            AssertTokens(JsonToken.Number);
            Assert.IsTrue(rdr.TryGetDouble(out var num));
            Assert.AreEqual(-21.5e-4, num);
        }

        [Test]
        public void Jr_List()
        {
            Lex(" [ 3.14 ]");
            AssertTokens(JsonToken.BeginList, JsonToken.Number, JsonToken.EndList);
        }

        [Test]
        public void Jr_BadComma()
        {
            Lex(",");
            Assert.Throws<BadImageFormatException>(() => { rdr.Read(); });
        }

        [Test]
        public void Jr_BadTrailingListComma()
        {
            Lex("[ 'a',]");
            Assert.AreEqual(JsonToken.BeginList, rdr.Read());
            Assert.AreEqual(JsonToken.String, rdr.Read());
            Assert.AreEqual("a", rdr.GetString());
            Assert.Throws<BadImageFormatException>(() => { rdr.Read(); });
        }

        [Test]
        public void Jr_List_two_items()
        {
            Lex("[ 'a', 'b']");
            Assert.AreEqual(JsonToken.BeginList, rdr.Read());
            Assert.AreEqual(JsonToken.String, rdr.Read());
            Assert.AreEqual("a", rdr.GetString());
            Assert.AreEqual(JsonToken.String, rdr.Read());
            Assert.AreEqual("b", rdr.GetString());
            Assert.AreEqual(JsonToken.EndList, rdr.Read());
        }

        [Test]
        public void Jr_List_bad_two_commas()
        {
            Lex("[ 'a',,'b']");
            Assert.AreEqual(JsonToken.BeginList, rdr.Read());
            Assert.AreEqual(JsonToken.String, rdr.Read());
            Assert.AreEqual("a", rdr.GetString());
            Assert.Throws<BadImageFormatException>(() => rdr.Read());
        }

        [Test]
        public void Jr_Empty_object()
        {
            Lex("{  }");
            Assert.AreEqual(JsonToken.BeginObject, rdr.Read());
            Assert.AreEqual(JsonToken.EndObject, rdr.Read());
        }

        [Test]
        public void Jr_object_missing_colon()
        {
            Lex("{ 'a' 3 }");
            Assert.AreEqual(JsonToken.BeginObject, rdr.Read());
            Assert.AreEqual(JsonToken.PropertyName, rdr.Read());
            Assert.AreEqual("a", rdr.GetString());
            Assert.Throws<BadImageFormatException>(() => rdr.Read());
        }

        [Test]
        public void Jr_object_single_value()
        {
            Lex("{ 'a': -3.0 }");
            Assert.AreEqual(JsonToken.BeginObject, rdr.Read());
            Assert.AreEqual(JsonToken.PropertyName, rdr.Read());
            Assert.AreEqual("a", rdr.GetString());
            Assert.AreEqual(JsonToken.Number, rdr.Read());
            Assert.IsTrue(rdr.TryGetDouble(out var d));
            Assert.AreEqual(-3.0, d);
            Assert.AreEqual(JsonToken.EndObject, rdr.Read());
        }

        [Test]
        public void Jr_object_bad_trailing_comma()
        {
            Lex("{ 'a': -3.0,  }");
            Assert.AreEqual(JsonToken.BeginObject, rdr.Read());
            Assert.AreEqual(JsonToken.PropertyName, rdr.Read());
            Assert.AreEqual("a", rdr.GetString());
            Assert.AreEqual(JsonToken.Number, rdr.Read());
            Assert.IsTrue(rdr.TryGetDouble(out var d));
            Assert.AreEqual(-3.0, d);
            Assert.Throws<BadImageFormatException>(() => rdr.Read());
        }

        [Test]
        public void Jr_object_two_properties()
        {
            Lex("{ 'a': -3.0, 'p':['b','c'] }");
            Assert.AreEqual(JsonToken.BeginObject, rdr.Read());
            Assert.AreEqual(JsonToken.PropertyName, rdr.Read());
            Assert.AreEqual("a", rdr.GetString());
            Assert.AreEqual(JsonToken.Number, rdr.Read());
            Assert.IsTrue(rdr.TryGetDouble(out var d));
            Assert.AreEqual(-3.0, d);
            Assert.AreEqual(JsonToken.PropertyName, rdr.Read());
            Assert.AreEqual(JsonToken.BeginList, rdr.Read());
            Assert.AreEqual(JsonToken.String, rdr.Read());
            Assert.AreEqual(JsonToken.String, rdr.Read());
            Assert.AreEqual(JsonToken.EndList, rdr.Read());
            Assert.AreEqual(JsonToken.EndObject, rdr.Read());
            Assert.AreEqual(JsonToken.Eof, rdr.Read());
        }
    }
}
