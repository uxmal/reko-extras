using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelScan
{
    public abstract class Expression
    {
    }

    public class Address : Expression
    {
        public long Value { get; private init; }

        public static Address Ptr32(int v)
        {
            return new Address { Value = v };
        }

        public static long operator - (Address a, Address b)
        {
            return a.Value - b.Value;
        }

        public static Address operator +(Address a, long b)
        {
            return new Address { Value = a.Value + b };
        }

        public override string ToString()
        {
            return Value.ToString("X8");
        }
    }
}
