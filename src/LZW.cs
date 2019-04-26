using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LZW
{
    class LZW
    {
        public LZW(char[] alphabet)
        {
            nextCode = 0;
            this.alphabet = alphabet;
            fwdDict = new Dictionary<string, code_type>();
            revDict = new Dictionary<code_type, string>();

            foreach (char c in alphabet)
            {
                AddSymbol(c.ToString());
            }
        }

        public byte[] Encode(string input)
        {
            List<code_type> codes = new List<code_type>();
            
            string curSeq = input[0].ToString();
            foreach (char c in input.Skip(1))
            {
                var sym = c.ToString();
                var _curSeq = curSeq + sym;
                if (!fwdDict.ContainsKey(_curSeq))
                {
                    code_type code = AddSymbol(_curSeq);
                    codes.Add(fwdDict[curSeq]);
                    curSeq = sym;
                }
                else
                {
                    curSeq += sym;
                }
            }

            if (curSeq.Length > 0)
            {
                codes.Add(fwdDict[curSeq]);
            }

            return PackBits(codes.ToArray(), input.Length);
        }

        public string Decode(byte[] _input)
        {
            // see https://www.geeksforgeeks.org/lzw-lempel-ziv-welch-compression-technique/

            int outputStringLen = 0;
            code_type[] input = UnpackBits(_input, out outputStringLen);
            char[] text = new char[outputStringLen];

            code_type old = input[0];
            string s = revDict[old];

            int offset = 0;
            s.CopyTo(0, text, offset, s.Length);
            offset += s.Length;
            
            for (int i = 1; i < input.Length; ++i)
            {
                code_type code = input[i];
                if (!revDict.ContainsKey(code))
                { 
                    s = revDict[old] + s[0];
                } 
                else
                { 
                    s = revDict[code];
                } 

                s.CopyTo(0, text, offset, s.Length);
                offset += s.Length;

                AddSymbol(revDict[old] + s[0]);
                old = code;
            }

            return new string(text);
        }

        private code_type AddSymbol(string sym)
        {
            code_type code = nextCode++;
            fwdDict.Add(sym, code);
            revDict.Add(code, sym);
            return code;
        }

        /// Packs bits in the following fashion:
        ///   Encode() output is aligned to code_type size, but this is a waste of precious space.
        ///   So, we calculate full-byte boundaries, e.g. boundaries, where another 1 byte is needed for the byte representation of the code. 
        ///
        ///   NOTE: .NET uses little-endian byte order and big endian bit order.
        ///
        /// Input:
        ///
        /// 00000000 00000000 00000000 11111110
        /// 00000000 00000000 00000000 11111111
        /// 00000000 00000000 00000001 00000000
        /// ...
        /// 00000000 00000000 11111111 11111111
        /// 00000000 00000001 00000000 00000000
        /// 00000000 00000001 00000000 00000001
        ///
        /// Boundaries:
        ///
        /// 00000000 00000000 00000000 11111110
        /// 00000000 00000000 00000000 11111111
        /// -------- full byte boundary -------
        /// 00000000 00000000 00000001 00000000
        /// ...
        /// 00000000 00000000 11111111 11111111
        /// -------- full byte boundary -------
        /// 00000000 00000001 00000000 00000000
        /// 00000000 00000001 00000000 00000001
        ///
        /// Wasted bytes, shown inside | |
        ///
        /// |00000000 00000000 00000000| 11111110
        /// |00000000 00000000 00000000| 11111111
        /// -------- full byte boundary -------
        /// |00000000 00000000| 00000001 00000000
        /// ...
        /// |00000000 00000000| 11111111 11111111
        /// -------- full byte boundary -------
        /// |00000000| 00000001 00000000 00000000
        /// |00000000| 00000001 00000000 00000001
        ///
        /// This approach helps us remove the wasted fully zeroed top bytes and gradually align output codes by the required byte count for the max code value.
        ///
        /// Output:
        /// inputStringLen             | Int32 (4 bytes)
        /// maxFullBytes               | Int32 (4 bytes)
        /// byteBoundaries             | maxFullBytes * Int32 (4 * maxFullBytes bytes)
        /// 00000000                   | compressed codes ...
        /// ...                        |
        /// 11111110                   |
        /// 11111111                   |
        /// 00000001 00000000          |
        /// ...                        |
        /// 11111111 11111111          |
        /// 00000001 00000000 00000000 |
        /// 00000001 00000000 00000001 |
        /// ...                        |
        private byte[] PackBits(code_type[] input, int inLen)
        {
            int bitsPerCode = (int)Math.Ceiling(Math.Log(nextCode - 1, 2.0));
            int fullBytes = (int)Math.Ceiling(bitsPerCode / 8.0);

            int[] fullByteCodeStart = new int[fullBytes];
            int[] fullByteCodeEnd = new int[fullBytes];
            fullByteCodeStart[0] = 0;
            int idx = 0;
            for (int i = 1; i < fullBytes; ++i)
            {
                idx = Array.FindIndex(input, idx, x => ((((byte)1) << (i * 8)) & x) != 0);
                fullByteCodeStart[i] = idx;
                fullByteCodeEnd[i - 1] = idx - 1;
            }
            fullByteCodeEnd[fullBytes - 1] = input.Length - 1;

            // write header:
            //    inputStringLen :: Int32
            //    fullByteCount :: Int32
            //    fullByteCount * fullByteCodeCount :: Int32
            List<byte> ret = new List<byte>();
            ret.AddRange(BitConverter.GetBytes(inLen));
            ret.AddRange(BitConverter.GetBytes(fullBytes));
            for (int i = 0; i < fullBytes; ++i)
            {
                ret.AddRange(BitConverter.GetBytes((fullByteCodeEnd[i] - fullByteCodeStart[i] + 1) * (i + 1)));
            }

            // write data:
            for (int i = 0; i < input.Length; ++i)
            {
                code_type _code = input[i];
                int curFullBytes = Array.FindIndex(fullByteCodeEnd, x => i <= x) + 1;

                byte[] curBytes = new byte[curFullBytes];
                for (int b = 0; b < curFullBytes; ++b)
                {
                    curBytes[/*fullBytes - 1 - */b] = (byte)(_code & byte.MaxValue);
                    _code = _code >> 8;
                }

                ret.AddRange(curBytes);
            }

            return ret.ToArray();
        }

        /// Unpacks bits, see PackBits for the packed format desciption.
        private code_type[] UnpackBits(byte[] input, out int outputStringLen)
        {
            int offset = 0;

            // read header
            outputStringLen = ReadInt32(input, ref offset);
            int fullBytes = ReadInt32(input, ref offset);
            int[] fullByteCodeCount = new int[fullBytes];
            for (int i = 0; i < fullBytes; ++i)
            {
                fullByteCodeCount[i] = ReadInt32(input, ref offset);
            }

            int[] fullByteCodeEnd = new int[fullBytes];
            int idx = offset;
            for (int i = 0; i < fullBytes; ++i)
            {
                fullByteCodeEnd[i] = idx + fullByteCodeCount[i] - 1;
                idx += fullByteCodeCount[i];
            }

            // read data
            int codeArrayLen = 0;
            for (int cc = 0; cc < fullByteCodeCount.Length; ++cc)
                codeArrayLen += fullByteCodeCount[cc] / (cc + 1);
            code_type[] ret = new code_type[codeArrayLen];

            byte[] buf = new byte[/*sizeof(*/code_type.sizeOf/*)*/];
            for (int i = offset, ri = 0; i < input.Length;)
            {
                int curFullBytes = Array.FindIndex(fullByteCodeEnd, x => i <= x) + 1;
                for (int j = 0; j < curFullBytes; ++j)
                    buf[j] = input[i + j];
                
                ret[ri++] = Converter.ToCodeType(buf);
                i += curFullBytes;
            }

            return ret;
        }

        private int ReadInt32(byte[] input, ref int offset)
        {
            int ret = BitConverter.ToInt32(input, offset);
            offset += sizeof(Int32);
            return ret;
        }

        private Dictionary<string, code_type> fwdDict;
        private Dictionary<code_type, string> revDict;
        private code_type nextCode;
        private char[] alphabet;
    }
}