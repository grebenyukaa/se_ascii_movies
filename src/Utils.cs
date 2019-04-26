using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LZW
{
    static class Converter
    {
        public static IEnumerable<byte> AsByteArray(code_type[] input)
        {
            foreach (code_type e in input)
            {
                foreach (byte b in BitConverter.GetBytes(e))
                    yield return b;
            }
        }

        public static code_type ToCodeType(byte[] bytes)
        {
            return BitConverter.ToInt32(bytes);
        }
    }
}