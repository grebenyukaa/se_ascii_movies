using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LZW
{
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
}