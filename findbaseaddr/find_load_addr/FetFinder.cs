using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.Core.Memory;
using Reko.ImageLoaders.OdbgScript;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace FindLoadAddr
{
    internal class FetFinder
    {
        private const int MaxGap = 3;

        private readonly IProcessorArchitecture arch;
        private readonly MemoryArea mem;
        private readonly ulong alignMask;
        private readonly ulong maskedValue;
        private readonly BigInteger wordMask;

        private uint word_size;

        public FetFinder(
            IProcessorArchitecture arch, 
            MemoryArea mem,
            ulong alignMask, 
            ulong maskedValue)
        {
            this.arch = arch;
            this.mem = mem;
            this.alignMask = alignMask;
            this.maskedValue = maskedValue;
            this.wordMask = Bits.Mask(arch.PointerType.BitSize);
        }

        private uint uAddrMax;
        private uint uAddrMin;

        //$TODO: what about small machines? 0x200?
        private const uint AddrDistance = 0x1_0000;

        private static bool IsNearby(Constant wAddrCandidate, Constant wPrev)
        {
            // instrs are within 64kb of each other
            var uCandidate = wAddrCandidate.ToUInt64();
            var uPrev = wPrev.ToUInt64();
            if (uCandidate > uPrev)
                return uCandidate - uPrev < AddrDistance;
            else
                return uPrev - uCandidate < AddrDistance;
        }

        private static readonly ulong[] fillerWords = new ulong[]
        {
            0ul,
            ~0ul,
            0xCCCCCCCC_CCCCCCCCul,
        };

        private bool IsFiller(Constant w, Constant? wPrev)
        {
            var uw = w.ToUInt64();
            foreach (var filler in fillerWords)
                if (((uw ^ filler) & wordMask) == 0x0)
                    return true;
            return false;
        }

        public bool IsAligned(Constant addr)
        {
            var uAddr = addr.ToUInt64();
            return (uAddr & this.alignMask) == maskedValue;
        }


        public List<FET> FindFETs(uint start, uint fileSize, uint wnd)
        {
            var result = new List<FET>();
            uint pos = 0;
            uint end = (uint)mem.Length;
            while (start <= pos && pos < end)
            {
                for (uint gapWords = 0; gapWords < MaxGap; ++gapWords)
                {
                    var rdr = arch.CreateImageReader(mem, pos);
                    if (!read_word(rdr, out var w))
                        break;
                    if (!IsFiller(w, null) && IsAligned(w))
                    {
                        uint head = pos;
                        uint tableSize = wnd;
                        MoveWindow(rdr, pos, gapWords);
                        var wPrev = w;
                        while (rdr.Offset < end)
                        {
                            if (!read_word(rdr, out w))
                                break; ;
                            if (!IsNearby(w, wPrev) || IsFiller(w, wPrev) || !IsAligned(w))
                                break;
                            ++tableSize;
                            MoveWindow(rdr, pos, gapWords);
                        }
                        result.Add(new FET(head, gapWords, tableSize));
                    }
                }
                pos += word_size;
            }
            return result;
        }

        public record FET(uint head, uint gap, uint tableSize);

        private bool read_word(EndianImageReader rdr, out Constant ptrValue)
        {
            return rdr.TryRead(arch.PointerType, out ptrValue);
        }

        private void MoveWindow(EndianImageReader rdr, uint pos, uint gapWords)
        {
            // We've alread read a word, so advance by 
            // (gapWords - 1) words
            rdr.Offset = (pos + gapWords) * word_size;
        }

        public List<(uint, double)> FindBaseCandidates(byte[] bin, List<FET> function_entry_table, double threshold)
        {
            uint fileSize = (uint)bin.Length;
            var entryAddresses = function_entry_table.Select(fet => fet.head).Distinct().OrderBy(f => f).ToArray();
            var n = entryAddresses.Length;
            int thumbCount = 0;
            int armCount = 0;
            var candidates = new List<(uint, double)>();
            for (uint x = entryAddresses[^1] - fileSize; x >= entryAddresses[0]; --x)
            {
                foreach (uint entryAddress in entryAddresses)
                {
                    if ((entryAddress & 1) == 1)
                    {
                        if (bin[entryAddress - x] == 0xB5)
                            ++thumbCount;
                    } else if ((entryAddress - x + 2) == 0x2D &&
                               (entryAddress - x + 3) == 0xE9)
                    {
                        ++armCount;
                    }
                }
                double matchRate = (thumbCount + armCount) / (double)n;
                if (matchRate >= threshold)
                {
                    candidates.Add((x, matchRate));
                }
            }
            return candidates;
        }
    }
}