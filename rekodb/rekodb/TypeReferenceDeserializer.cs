using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Reko.Database
{
    public class TypeReferenceDeserializer : AbstractDeserializer
    {
        public TypeReferenceDeserializer(JsonReader rdr) : base(rdr)
        {
        }

        public DataType Deserialize()
        {
            var token = rdr.Read();
            switch (token)
            {
            case JsonToken.String:
                var s = rdr.GetString();
                if (s.Length < 2)
                    throw new InvalidOperationException();
                Domain domain;
                switch (s[0])
                {
                case 'i':
                    domain = Domain.SignedInt;
                    break;
                case 'w':
                    return PrimitiveType.CreateWord(BitSize(s[1..].AsSpan()));
                default:
                    throw new NotImplementedException($"Primitive type {s[0]}.");
                }
                return PrimitiveType.Create(domain, BitSize(s[1..].AsSpan()));
            case JsonToken.BeginList:
                Expect(JsonToken.String);
                var ctor = rdr.GetString();
                switch (ctor[0])
                {
                case 'p':
                    Expect(JsonToken.Number);
                    if (!rdr.TryGetInt32(out int ptrBitsize))
                        throw new BadImageFormatException();
                    var dtPointee = Deserialize();
                    Expect(JsonToken.EndList);
                    return new Pointer(dtPointee, ptrBitsize);
                }
                break;
            default:
                throw new NotImplementedException($"JSON token {token}.");
            }
            
            throw new NotImplementedException($"JSON token {token}.");
        }

        private int BitSize(ReadOnlySpan<char> valueSpan)
        {
            int bitsize = 0;
            for (int i = 0; i < valueSpan.Length; ++i)
            {
                bitsize = bitsize * 10 + (valueSpan[i] - '0');
            }
            return bitsize;
        }
    }
}
