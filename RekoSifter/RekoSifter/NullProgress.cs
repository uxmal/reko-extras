using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RekoSifter
{
    public class NullProgress : IProgress
    {
        public void Advance()
        {
        }

        public void Reset()
        {
        }
    }
}
