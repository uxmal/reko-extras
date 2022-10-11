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
                case "-h":
                case "--help":
                    Usage();
                    return;
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

        private static void Usage()
        {
            Console.WriteLine("Usage: FindLoadAddr [options] <filename>");
            Console.WriteLine();
            Console.WriteLine("Guesses the best candidates for a blob of executable code");
            Console.WriteLine("whose base address is unknown.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -b         Force big endian interpretation (default is little endian)");
            Console.WriteLine("  -s         Match strings and pointers pointing to them (default heuristic)");
            Console.WriteLine("  -p         Match procedure prologs and pointers pointing to them");
            Console.WriteLine("  -f         Match function table entries and code (WIP)");
            Console.WriteLine("  -h, --help This help message");
        }

        private static IBaseAddressFinder CreateFetFinder(ByteMemoryArea mem)
        {
            throw new NotImplementedException();
        }

        private static IBaseAddressFinder CreatePrologFinder(ByteMemoryArea mem)
        {
            return new ProcedurePrologFinder(mem);
        }

        private static IBaseAddressFinder CreateStringFinder(ByteMemoryArea mem) => new FindBaseString(mem);
    }
}
