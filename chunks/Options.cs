using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace chunks
{
    public class Options
    {
        public Regex? excludedArchs;
        public Regex? includedArchs;
        public bool captureInvalidInstructions;
    }
}
