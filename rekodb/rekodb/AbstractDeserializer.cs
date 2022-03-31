using System.Text;

namespace Reko.Database
{
    public class AbstractDeserializer
    {
        protected readonly JsonReader rdr;

        public AbstractDeserializer(JsonReader rdr)
        {
            this.rdr = rdr;
        }

        protected void Expect(JsonToken token)
        {
            var t = rdr.Read();
            if (t != token)
                throw new InvalidOperationException(
                    $"Expected {token} but read {t}.");
        }

        protected bool PeekAndDiscard(JsonToken token)
        {
            var t = rdr.Peek();
            if (t != token)
                return false;
            rdr.Read();
            return true;
        }
    }
}