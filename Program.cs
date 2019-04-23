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

            List<byte> ret = new List<byte>();
            ret.AddRange(BitConverter.GetBytes(fullBytes));

            foreach (code_type code in input)
            {
                code_type _code = code;
                byte[] curBytes = new byte[fullBytes];
                for (int b = 0; b < fullBytes; ++b)
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
            int fullBytes = BitConverter.ToInt32(input);

            byte[] buf = new byte[sizeof(code_type)];

            List<code_type> ret = new List<code_type>();
            for (int i = 4; i < input.Length; i += fullBytes)
            {
                for (int j = 0; j < fullBytes; ++j)
                buf[j] = input[i + j];
                ret.Add(Utils.Converter.ToCodeType(buf));
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
