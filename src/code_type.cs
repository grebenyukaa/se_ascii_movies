using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

struct code_type : IComparable<code_type>, IEquatable<code_type>
{
    public code_type(int val)
    {
        value = val;
    }
    
    public static implicit operator int(code_type t)
    {
        return t.value;
    }

    public static implicit operator code_type(int t)
    {
        return new code_type(t);
    }

    public static int sizeOf { get { return sizeof(int); } }
    public override string ToString() { return value.ToString(); }
    public override int GetHashCode() { return value.GetHashCode(); }
    public int CompareTo(code_type other) { return value.CompareTo(other.value); }
    public bool Equals(code_type other) { return value.Equals(other.value); }

    private int value;
}
