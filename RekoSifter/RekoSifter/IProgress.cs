using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RekoSifter
{
    public interface IProgress
    {
        void Advance();
        void Reset();
    }
}
