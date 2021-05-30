using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelScan
{
    public class RegisterStorage
    {
        public RegisterStorage(string name, BitRange bitrange, DataType dt)
        {
            this.Name = name;
            this.BitRange = bitrange;
            this.DataType = dt;
        }

        public string Name { get; }
        public BitRange BitRange { get; }
        public DataType DataType { get; }
    }
}
