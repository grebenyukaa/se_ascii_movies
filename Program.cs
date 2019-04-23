using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using code_type = System.Int32;

namespace Utils
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

namespace LZW
{
    static class Alphabet
    {
        public static char[] StarWars = {'g', 'v', '0', 'D', '9', 'J', 'C', 'k', 'N', 'n', 'M', 'V', '#', '~', 'H', 't', 'P',
            '>', 'o', 'E', '|', '"', 'f', '4', 'z', 'b', '7', 'p', 'w', 'X', 'ÿ', 'a', ':', 'q', ' ', '$', 'U', '[',
            '6', 'I', 'T', 'j', 'u', 'W', ')', 'i', 'Y', '+', 's', 'S', ';', 'c', 'B', '=', 'A', '^', '`', 'K', 'R',
            'y', '%', '!', 'O', '/', 'e', '\\', ']', 'd', '?', 'F', '\'', '}', 'l', '.', '2', 'L', '_', 'G', '*', ',',
            'x', 'r', '1', '{', 'm', '(', '\n', '-', '@', '5', '<', '8', '3', 'h'
        };
    }

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

        public code_type[] Encode(string input)
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

            return codes.ToArray();
        }

        public string Decode(code_type[] input)
        {
            // see https://www.geeksforgeeks.org/lzw-lempel-ziv-welch-compression-technique/

            string text = "";

            code_type old = 0;
            string s = revDict[input[old]];
            text += s;
            
            foreach (code_type code in input.Skip(1))
            { 
                if (!revDict.ContainsKey(code))
                { 
                    s = revDict[old] + s[0];
                } 
                else
                { 
                    s = revDict[code];
                } 

                text += s;
                AddSymbol(revDict[old] + s[0]);
                old = code;
            }

            return text;
        }

        private code_type AddSymbol(string sym)
        {
            code_type code = nextCode++;
            fwdDict.Add(sym, code);
            revDict.Add(code, sym);
            return code;
        }

        public byte[] PackBits(code_type[] input)
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

            List<byte> ret = new List<byte>();
            ret.AddRange(BitConverter.GetBytes(fullBytes));

            // write full byte boundaries
            for (int i = 0; i < fullBytes; ++i)
            {
                ret.AddRange(BitConverter.GetBytes((fullByteCodeEnd[i] - fullByteCodeStart[i] + 1) * (i + 1)));
            }

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

        public code_type[] UnpackBits(byte[] input)
        {
            int offset = 0;
            int fullBytes = BitConverter.ToInt32(input);
            offset += sizeof(Int32);

            int[] fullByteCodeCount = new int[fullBytes];
            for (int i = 0; i < fullBytes; ++i)
            {
                fullByteCodeCount[i] = BitConverter.ToInt32(input, offset);
                offset += sizeof(Int32);
            }

            int[] fullByteCodeEnd = new int[fullBytes];
            int idx = offset;
            for (int i = 0; i < fullBytes; ++i)
            {
                fullByteCodeEnd[i] = idx + fullByteCodeCount[i] - 1;
                idx += fullByteCodeCount[i];
            }

            byte[] buf = new byte[sizeof(code_type)];
            List<code_type> ret = new List<code_type>();
            for (int i = offset; i < input.Length;)
            {
                int curFullBytes = Array.FindIndex(fullByteCodeEnd, x => i <= x) + 1;
                for (int j = 0; j < curFullBytes; ++j)
                    buf[j] = input[i + j];
                ret.Add(Utils.Converter.ToCodeType(buf));
                i += curFullBytes;
            }

            return ret.ToArray();
        }

        private Dictionary<string, code_type> fwdDict;
        private Dictionary<code_type, string> revDict;
        private code_type nextCode;
        private char[] alphabet;
    }
}

namespace MoviePlayback
{
    class AsciiMovie
    {
        public AsciiMovie(code_type[] input)
        {
            DecodeInput(input);
        }

        public class Frame
        {
            public Frame(IEnumerable<string> rows)
            {
                var sTime = rows.FirstOrDefault();
                if (sTime.Length == 0)
                    throw new ArgumentException("bad frame data: empty");
                
                time = Convert.ToInt32(sTime);
                this.rows = rows.Skip(1).ToArray();
            }
            public string[] rows { get; private set; }
            public int time { get; private set; }
        }

        public IEnumerable<Frame> GetFrameIterator(int rowsPerFrame = 13)
        {
            IEnumerable<string> slice = rowStream;
            for (int fid = 0; fid < rowStream.Length / rowsPerFrame; fid += rowsPerFrame)
            {
                IEnumerable<string> frameRows = slice.Take(rowsPerFrame);
                slice = slice.Skip(rowsPerFrame);
                yield return new Frame(frameRows.ToArray());
            }
        }

        private void DecodeInput(code_type[] input, char rowSep = '\n')
        {
            LZW.LZW lzw = new LZW.LZW(LZW.Alphabet.StarWars);
            string decodedData = lzw.Decode(input);
            rowStream = decodedData.Split(rowSep);
        }
        private string[] rowStream;
    }
}

namespace test
{
    class Program
    {
        static void Main(string[] args)
        {
            using (StreamReader sr = new StreamReader(@"C:\Users\amadeus\Desktop\space engineers\bar\movie\ascii_star_wars.txt"))
            {
                string data = sr.ReadToEnd();
                
                LZW.LZW lzw = new LZW.LZW(LZW.Alphabet.StarWars);
                code_type[] encoded = lzw.Encode(data);

                byte[] _encoded = Utils.Converter.AsByteArray(encoded).ToArray();
                char[] asText = Encoding.ASCII.GetString(_encoded).ToArray();
                
                byte[] packed = lzw.PackBits(encoded);
                code_type[] unpacked = lzw.UnpackBits(packed);

                using (StreamWriter sw = new StreamWriter(@"C:\Users\amadeus\Desktop\space engineers\bar\movie\ascii_star_wars.compressed.txt"))
                {
                    sw.Write(asText);
                }
            }
        }
    }
}
