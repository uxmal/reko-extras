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
            
            int i = 0;
            if (args.Length > 1)
            {
                if (args[0] == "-b")
                {
                    ++i;
                    endianness = EndianServices.Big;
                }
            }
            var bytes = File.ReadAllBytes(args[i]);
            var mem = new ByteMemoryArea(Address.Ptr32(0), bytes);

            var s = new FindBaseString(mem);
            s.Endianness = endianness;
            s.run();
        }
    }
}
