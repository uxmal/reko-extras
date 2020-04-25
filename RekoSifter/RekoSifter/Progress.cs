using System;
using System.Collections.Generic;
using System.Text;

namespace RekoSifter
{
    public class Progress
    {
        private int pos;

        public void Advance()
        {
            Console.Write('.');
            if (++pos > 72)
            {
                pos = 0;
                Console.WriteLine();
            }
        }

        public void Reset()
        {
            pos = 0;
            Console.WriteLine();
        }
    }
}
