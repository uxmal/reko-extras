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
                Assert.That(t, Is.EqualTo(token));
            }
        }

        [Test]
        public void Jr_Zero()
        {
            Lex("0");
            AssertTokens(JsonToken.Number);
            Assert.That(rdr.TryGetDouble(out var num), Is.True);
            Assert.That(num, Is.EqualTo(0.0));
        }

        [Test]
        public void Jr_Exponent()
        {
            Lex(" -21.5e-04");
            AssertTokens(JsonToken.Number);
            Assert.That(rdr.TryGetDouble(out var num), Is.True);
            Assert.That(num, Is.EqualTo(-21.5e-4));
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
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.BeginList));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.String));
            Assert.That(rdr.GetString(), Is.EqualTo("a"));
            Assert.Throws<BadImageFormatException>(() => { rdr.Read(); });
        }

        [Test]
        public void Jr_List_two_items()
        {
            Lex("[ 'a', 'b']");
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.BeginList));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.String));
            Assert.That(rdr.GetString(), Is.EqualTo("a"));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.String));
            Assert.That(rdr.GetString(), Is.EqualTo("b"));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.EndList));
        }

        [Test]
        public void Jr_List_bad_two_commas()
        {
            Lex("[ 'a',,'b']");
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.BeginList));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.String));
            Assert.That(rdr.GetString(), Is.EqualTo("a"));
            Assert.Throws<BadImageFormatException>(() => rdr.Read());
        }

        [Test]
        public void Jr_Empty_object()
        {
            Lex("{  }");
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.BeginObject));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.EndObject));
        }

        [Test]
        public void Jr_object_missing_colon()
        {
            Lex("{ 'a' 3 }");
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.BeginObject));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.PropertyName));
            Assert.That(rdr.GetString(), Is.EqualTo("a"));
            Assert.Throws<BadImageFormatException>(() => rdr.Read());
        }

        [Test]
        public void Jr_object_single_value()
        {
            Lex("{ 'a': -3.0 }");
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.BeginObject));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.PropertyName));
            Assert.That(rdr.GetString(), Is.EqualTo("a"));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.Number));
            Assert.That(rdr.TryGetDouble(out var d), Is.True);
            Assert.That(d, Is.EqualTo(-3.0));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.EndObject));
        }

        [Test]
        public void Jr_object_bad_trailing_comma()
        {
            Lex("{ 'a': -3.0,  }");
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.BeginObject));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.PropertyName));
            Assert.That(rdr.GetString(), Is.EqualTo("a"));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.Number));
            Assert.That(rdr.TryGetDouble(out var d), Is.True);
            Assert.That(d, Is.EqualTo(-3.0));
            Assert.Throws<BadImageFormatException>(() => rdr.Read());
        }

        [Test]
        public void Jr_object_two_properties()
        {
            Lex("{ 'a': -3.0, 'p':['b','c'] }");
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.BeginObject));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.PropertyName));
            Assert.That(rdr.GetString(), Is.EqualTo("a"));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.Number));
            Assert.That(rdr.TryGetDouble(out var d), Is.True);
            Assert.That(d, Is.EqualTo(-3.0));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.PropertyName));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.BeginList));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.String));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.String));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.EndList));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.EndObject));
            Assert.That(rdr.Read(), Is.EqualTo(JsonToken.Eof));
        }
    }
}
