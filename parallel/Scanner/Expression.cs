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

    public class Address : Expression, IComparable<Address>
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

        public static Address operator + (Address a, long b)
        {
            return new Address { Value = a.Value + b };
        }

        public static bool operator ==(Address a, Address b) => a.Value == b.Value;
        public static bool operator !=(Address a, Address b) => a.Value != b.Value;

        public static bool operator <(Address a, Address b) => a.Value < b.Value;
        public static bool operator <=(Address a, Address b) => a.Value <= b.Value;
        public static bool operator >(Address a, Address b) => a.Value > b.Value;
        public static bool operator >=(Address a, Address b) => a.Value >= b.Value;

        public override bool Equals(object? obj)
        {
            return (obj is Address that && this.Value == that.Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString("X8");
        }

        public int CompareTo(Address? that)
        {
            if (that is null)
                return 1;
            return this.Value.CompareTo(that.Value);
        }
    }
}
