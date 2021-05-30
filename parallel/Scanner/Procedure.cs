using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelScan
{
    public class Procedure
    {
        public Procedure(Address addr)
        {
            this.EntryAddress = addr;
            this.ExitBlock = new Block();
        }

        public Address EntryAddress { get; }
        public ProcedureReturn Returns { get; set; }
        public Block ExitBlock { get; }
    }
}
