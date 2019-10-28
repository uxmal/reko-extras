using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Reko.Extras.DeHexdump
{
    public class DeHexdumper
    {
        private TextReader input;
        private Stream binOut;

        public DeHexdumper(TextReader input, Stream binOut)
        {
            this.input = input;
            this.binOut = binOut;
        }

        public int Dehex()
        {
            var firstLine = input.ReadLine();
            if (firstLine == null)
                return 0;
            firstLine = firstLine.Trim().ToLower();
            var re = Sniff(firstLine);
            if (re == null)
                return 1;
            var line = firstLine;
            while (line != null)
            {
                line = line.Trim();
                ProcessLine(line, re);
                line = input.ReadLine();
            }
            return 0;
        }

        private void ProcessLine(string line, Regex re)
        {
            var ma = re.Match(line);
            if (!ma.Success)
            {
                // Early failure!
                return;
            }

            var sAddr = ma.Groups[1].Value;
            var firstHalf = ma.Groups[2].Value;
            var secondHalf = ma.Groups[4].Value;
            Emit(firstHalf);
            Emit(secondHalf);
        }

        private void Emit(string item)
        {
            int hex = 0;
            int nDigits = 0;
            for (int i = 0; i < item.Length; ++i)
            {
                char c = item[i];
                if ('0' <= c && c <= '9')
                {
                    hex = hex * 16 + (c - '0');
                    ++nDigits;
                }
                else if ('a' <= c && c <= 'f')
                {
                    hex = hex * 16 + (c - 'a') + 10;
                    ++nDigits;
                }
                if (nDigits == 2)
                {
                    this.binOut.WriteByte((byte)hex);
                    nDigits = 0;
                }
            }
        }

        public Regex Sniff(string firstLine)
        {
            Regex re;
            // 00000000: 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00  ................
            re = new Regex("^([0-9a-f]{8}):(( [0-9a-f]{2}){8})(( [0-9a-f]{2}){8})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var ma = re.Match(firstLine);
            if (ma.Success)
                return re;
            // 00000000  62 6c 6f 62 2e 68 65 78  20 77 61 73 20 6f 62 74  |blob.hex was obt|
            re = new Regex("^([0-9a-f]{8})  (([0-9a-f]{2} ){8}) (([0-9a-f]{2} ){8})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            ma = re.Match(firstLine);
            if (ma.Success)
            {
                return re;
            }
            return null;
        }
    }
}
