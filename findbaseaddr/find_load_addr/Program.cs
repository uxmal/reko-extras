using Reko.Core;
using Reko.Core.Memory;
using Reko.ImageLoaders.OdbgScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FindLoadAddr
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var endianness = EndianServices.Little;
            Func<ByteMemoryArea, IBaseAddressFinder> ctor = CreateStringFinder;
            int i = 0;
            if (args.Length > 1)
            {
                switch (args[0])
                {
                case "-b":
                    ++i;
                    endianness = EndianServices.Big;
                    break;
                case "-s":
                    ++i;
                    ctor = CreateStringFinder;
                    break;
                case "-p":
                    ++i;
                    ctor = CreatePrologFinder;
                    break;
                case "-f":
                    ++i;
                    ctor = CreateFetFinder;
                    break;
                }
            }
            var bytes = File.ReadAllBytes(args[i]);
            var mem = new ByteMemoryArea(Address.Ptr32(0), bytes);

            IBaseAddressFinder s = ctor(mem);
            s.Endianness = endianness;
            s.Run();
        }

        private static IBaseAddressFinder CreateFetFinder(ByteMemoryArea mem)
        {
            throw new NotImplementedException();
        }

        private static IBaseAddressFinder CreatePrologFinder(ByteMemoryArea mem)
        {
            throw new NotImplementedException();
        }

        private static IBaseAddressFinder CreateStringFinder(ByteMemoryArea mem) => new FindBaseString(mem);
    }
}
