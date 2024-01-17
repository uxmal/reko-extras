using Reko.Core.Collections;
using Reko.Core.Lib;
using Reko.Core.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FindLoadAddr
{
    public class PatternFinder
    {
        public static List<(ulong uAddress, uint length)> FindAsciiStrings(ByteMemoryArea buffer, int min_str_len)
        {
            var strings = new List<(ulong, uint)>();

            var bytes = buffer.Bytes;
            bool insideString = false;
            uint iStart = 0;
            for (uint i = 0; i < bytes.Length; ++i)
            {
                var b = bytes[i];
                if (' ' <= b && b <= '~' || b == '\t' || b == '\r' || b == '\n')
                {
                    if (!insideString)
                    {
                        insideString = true;
                        iStart = i;
                    }
                }
                else
                {
                    uint strlen = i - iStart;
                    if (insideString && strlen >= min_str_len)
                    {
                        strings.Add((iStart, strlen));
                    }
                    insideString = false;
                }
            }
            if (insideString)
            {
                uint strlen = (uint) bytes.Length - iStart;
                if (strlen >= min_str_len)
                {
                    strings.Add((iStart, strlen));
                }
            }
            return strings;
        }

        public static List<ulong> FindProcedurePrologs(ByteMemoryArea buffer, ByteTrie<object> prologs)
        {
            var match = prologs.Match(buffer.Bytes, 0);
            var results = new List<ulong>();
            while (match.Success)
            {
                results.Add((ulong)match.Index);
                match = match.NextMatch(buffer.Bytes);
            }
            return results;
        }

    }
}
