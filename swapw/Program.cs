using System;
using System.IO;

namespace swapw
{
    class Program
    {
        static void Main(string[] args)
        {
            using (Stream stmOut = Console.OpenStandardOutput())
            using (Stream stmIn = Console.OpenStandardInput())
            {
                var rdr = new BinaryReader(stmIn);
                    var w = new BinaryWriter(stmOut);
                try
                {
                    for (; ; )
                    {
                        var u = rdr.ReadUInt32();
                        var swappedU =
                            (u >> 24) |
                            ((u >> 8) & 0xFF00) |
                            ((u << 8) & 0xFF0000) |
                            ((u << 24));
                        w.Write(swappedU);
                    }
                }
                catch
                {
                    w.Flush();
                }
            }
        }
    }
}
