using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.IO;

#region ingame_script
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

//using code_type = System.Int32;

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

static class Alphabet
{
    public static char[] StarWars = {'g', 'v', '0', 'D', '9', 'J', 'C', 'k', 'N', 'n', 'M', 'V', '#', '~', 'H', 't', 'P',
        '>', 'o', 'E', '|', '"', 'f', '4', 'z', 'b', '7', 'p', 'w', 'X', 'ÿ', 'a', ':', 'q', ' ', '$', 'U', '[',
        '6', 'I', 'T', 'j', 'u', 'W', ')', 'i', 'Y', '+', 's', 'S', ';', 'c', 'B', '=', 'A', '^', '`', 'K', 'R',
        'y', '%', '!', 'O', '/', 'e', '\\', ']', 'd', '?', 'F', '\'', '}', 'l', '.', '2', 'L', '_', 'G', '*', ',',
        'x', 'r', '1', '{', 'm', '(', '\n', '-', '@', '5', '<', '8', '3', 'h'
    };
}

#region plain LZW
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
#endregion plain LZW

class LZWInputStream
{
    public LZWInputStream(int blockSize)
    {
        this.blockSize = blockSize;
    }

    public class UnpackBitsState
    {
        public UnpackBitsState(byte[] input)
        {
            inputIndex = 0;
            outputStringLen = ReadInt32(input);
            fullBytes = ReadInt32(input);
            fullByteCodeCount = new int[fullBytes];
            for (int i = 0; i < fullBytes; ++i)
            {
                fullByteCodeCount[i] = ReadInt32(input);
            }

            fullByteCodeEnd = new int[fullBytes];
            int idx = inputIndex;
            for (int i = 0; i < fullBytes; ++i)
            {
                fullByteCodeEnd[i] = idx + fullByteCodeCount[i] - 1;
                idx += fullByteCodeCount[i];
            }

            int codeArrayLen = 0;
            for (int cc = 0; cc < fullByteCodeCount.Length; ++cc)
                codeArrayLen += fullByteCodeCount[cc] / (cc + 1);
            codes = new code_type[codeArrayLen];
        }

        public void UnpackNext(byte[] input, int lastIndex)
        {
            byte[] buf = new byte[code_type.sizeOf];
            for (; inputIndex <= lastIndex;)
            {
                int curFullBytes = Array.FindIndex(fullByteCodeEnd, x => inputIndex <= x) + 1;
                for (int j = 0; j < curFullBytes; ++j)
                    buf[j] = input[inputIndex + j];
                
                codes[codeIndex++] = Converter.ToCodeType(buf);
                inputIndex += curFullBytes;
            }
        }

        private int ReadInt32(byte[] input)
        {
            int ret = BitConverter.ToInt32(input, inputIndex);
            inputIndex += sizeof(Int32);
            return ret;
        }

        public code_type[] codes { get; private set; }
        public int outputStringLen { get; private set; }

        private int inputIndex;
        private int codeIndex;
        private int fullBytes;
        private int[] fullByteCodeCount;
        private int[] fullByteCodeEnd;
    }

    public class DecoderState
    {
        public DecoderState(char[] alphabet, code_type[] input, int outputStringLen)
        {
            nextCode = 0;
            revDict = new Dictionary<code_type, string>();
            foreach (char c in alphabet)
            {
                AddSymbol(c.ToString());
            }

            text = new char[outputStringLen];
            
            inputIndex = 0;
            old = input[inputIndex++];
            s = revDict[old];

            outputIndex = 0;
            s.CopyTo(0, text, outputIndex, s.Length);
            outputIndex += s.Length;
        }

        public void DecodeNext(code_type[] input, int lastIndex)
        {
            // see https://www.geeksforgeeks.org/lzw-lempel-ziv-welch-compression-technique/

            for (; inputIndex <= lastIndex; ++inputIndex)
            {
                code_type code = input[inputIndex];
                if (!revDict.ContainsKey(code))
                { 
                    s = revDict[old] + s[0];
                } 
                else
                { 
                    s = revDict[code];
                } 

                s.CopyTo(0, text, outputIndex, s.Length);
                outputIndex += s.Length;

                AddSymbol(revDict[old] + s[0]);
                old = code;
            }
        }

        private code_type AddSymbol(string sym)
        {
            code_type code = nextCode++;
            revDict.Add(code, sym);
            return code;
        }

        public char[] text { get; private set; }
        private int outputIndex;
        private int inputIndex;
        private code_type old;
        private string s;

        private code_type nextCode;
        private Dictionary<code_type, string> revDict;
    }

    public IEnumerator<UnpackBitsState> UnpackBits(UnpackBitsState st, byte[] input)
    {
        int next = 0;
        for (int i = 0; i < input.Length;)
        {
            next += (i + blockSize) < input.Length ? blockSize : (input.Length - i);
            st.UnpackNext(input, next);
            i = next + 1;
            yield return st;
        }
        yield return st;
    }

    public IEnumerator<DecoderState> Decode(DecoderState st, code_type[] input)
    {
        int next = 0;
        for (int i = 0; i < input.Length;)
        {
            next += (i + blockSize) < input.Length ? blockSize : (input.Length - i);
            st.DecodeNext(input, next);
            i = next + 1;
            yield return st;
        }
        yield return st;
    }

    private int blockSize;
}

class AsciiMovie
{
    public AsciiMovie(byte[] input)
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

    private void DecodeInput(byte[] input, char rowSep = '\n')
    {
        LZW lzw = new LZW(Alphabet.StarWars);
        string decodedData = lzw.Decode(input);
        rowStream = decodedData.Split(rowSep);
    }

    private string[] rowStream;
}

#endregion ingame_script

namespace test
{
    class Program
    {
        static void Main(string[] args)
        {
            using (StreamReader sr = new StreamReader(@"C:\Users\amadeus\Desktop\space engineers\bar\movie\ascii_star_wars.txt"))
            {
                string data = sr.ReadToEnd();
                
                byte[] encoded;
                {
                    LZW lzw = new LZW(Alphabet.StarWars);
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    encoded = lzw.Encode(data);
                    watch.Stop();
                    var elapsedMs = watch.ElapsedMilliseconds;
                    Console.WriteLine($"encode takes: {elapsedMs} (ms)");
                }

                {
                    LZWInputStream lzw = new LZWInputStream(4096);
                    string decoded;

                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    {
                        var uState = new LZWInputStream.UnpackBitsState(encoded);
                        var itp = lzw.UnpackBits(uState, encoded);
                        while (itp.MoveNext())
                        {}
                        
                        var dState = new LZWInputStream.DecoderState(Alphabet.StarWars, uState.codes, uState.outputStringLen);
                        var itd = lzw.Decode(dState, uState.codes);
                        while (itd.MoveNext())
                        {}
                        decoded = new string(dState.text);
                    }
                    var elapsedMs = watch.ElapsedMilliseconds;
                    Console.WriteLine($"decode takes: {elapsedMs} (ms)");

                    Console.WriteLine($"integrity check: {data.GetHashCode() == decoded.GetHashCode()}");
                }

                // {
                //     LZW lzw = new LZW(Alphabet.StarWars);
                //     var watch = System.Diagnostics.Stopwatch.StartNew();
                //     string decoded = lzw.Decode(encoded);
                //     watch.Stop();
                //     var elapsedMs = watch.ElapsedMilliseconds;
                //     Console.WriteLine($"decode takes: {elapsedMs} (ms)");

                //     Console.WriteLine($"integrity check: {data.GetHashCode() == decoded.GetHashCode()}");
                // }

                using (StreamWriter sw = new StreamWriter(@"C:\Users\amadeus\Desktop\space engineers\bar\movie\ascii_star_wars.base64.txt"))
                {
                    sw.Write(Convert.ToBase64String(encoded));
                }
            }          
        }
    }
}
