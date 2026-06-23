using FindLoadAddr;
using Reko.Core;
using Reko.Core.Collections;
using Reko.Core.Memory;

namespace UnitTests
{
    [TestFixture]
    public class PatternFinderTests
    {


        private void AddPattern(ByteTrie<object> trie, string sPattern)
        {
            var bytes = BytePattern.FromHexBytes(sPattern);
            trie.Add(bytes, bytes.Length);
        }

        private ByteMemoryArea Given_Image(string sBytes)
        {
            var bytes = BytePattern.FromHexBytes(sBytes);
            return new ByteMemoryArea(Address.Ptr32(0), bytes);
        }

        [Test]
        public void Ppf_One()
        {
            var mem = Given_Image("00 55 89 E5 82 C3");
            var trie = new ByteTrie<object>();
            AddPattern(trie, "55 89 E5");

            var prologs = PatternFinder.FindProcedurePrologs(mem, trie);
            Assert.That(prologs.Count, Is.EqualTo(1));
            Assert.That(prologs[0], Is.EqualTo(1));
        }

        [Test]
        public void Ppf_Two()
        {
            var mem = Given_Image("00 55 89 E5 82 C3 00 88 88 00");
            var trie = new ByteTrie<object>();
            AddPattern(trie, "55 89 E5");
            AddPattern(trie, "88 88");

            var prologs = PatternFinder.FindProcedurePrologs(mem, trie);
            Assert.That(prologs.Count, Is.EqualTo(2));
            Assert.That(prologs[0], Is.EqualTo(1));
            Assert.That(prologs[1], Is.EqualTo(7));
        }

        [Test]
        public void Ppf_Overlaps()
        {
            var mem = Given_Image("00 88 88 88 00 00 00");
            var trie = new ByteTrie<object>();
            AddPattern(trie, "88 88");

            var prologs = PatternFinder.FindProcedurePrologs(mem, trie);
            Assert.That(prologs.Count, Is.EqualTo(2));
            Assert.That(prologs[0], Is.EqualTo(1));
            Assert.That(prologs[1], Is.EqualTo(2));
        }
    }
}
