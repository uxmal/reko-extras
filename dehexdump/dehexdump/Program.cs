using System;
using System.IO;
using System.Text;

namespace Reko.Extras.DeHexdump
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                using (Stream binOut = Console.OpenStandardOutput())
                {
                    var dehex = new DeHexdumper(Console.In, binOut);
                    dehex.Dehex();
                }
            }
            if (args.Length == 1)
            {
                using (Stream binOut = Console.OpenStandardOutput())
                using (TextReader input = new StreamReader(args[0], Encoding.ASCII))
                {
                    var dehex = new DeHexdumper(input, binOut);
                    dehex.Dehex();
                }
            }
        }
    }
}
