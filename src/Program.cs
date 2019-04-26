using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.CodeDom.Compiler;

using LZW;
using AsciiMovie;

namespace test
{
    class TimeIt : IDisposable
    {
        public TimeIt(string msg)
        {
            this.msg = msg;
            watch = System.Diagnostics.Stopwatch.StartNew();
        }

        public void Dispose()
        {
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Console.WriteLine($"{msg} takes: {elapsedMs} (ms)");
        }

        System.Diagnostics.Stopwatch watch;
        string msg;
    }

    class Program
    {
        // SE has limited size of CustomData field in IMyTerminalBlocks.
        // The size limit is 64000 bytes
        // So we split base64 encoded data into several "volumes"
        public static string[] storageIDs = new string[8] {
            "072a46b4-483e-4674-93f9-2121ab485aa8",
            "2f20bcce-4c90-47bc-bc73-0b3a55c75c05",
            "442adb76-0a22-47a5-b790-1ae415fb4cf1",
            "45896890-152b-417a-b8a3-a1049012ea81",
            "5da80810-0a16-4b79-8f06-ea0139796628",
            "77f6fd59-9cfb-4bda-bfde-1b2a724ca871",
            "fb23ac8b-2013-4444-b7d2-b82aa36ceb78",
            "ffb2fc60-b3a8-4b52-b6c5-8e0b6dc399ff"
        };

        public static string inputDir = "movies/input/";
        public static string outputDir = "movies/output/";

        public static char[] GetAlphabet(string input)
        {
            return new SortedSet<char>(input).ToArray();
        }

        public static byte[] Encode(char[] alphabet, string data)
        {
            LZW.LZW lzw = new LZW.LZW(alphabet);
            byte[] encoded;
            using (TimeIt tm = new TimeIt("encode"))
            {
                encoded = lzw.Encode(data);
            }
            return encoded;
        }

        public static string Decode(char[] alphabet, byte[] encoded)
        {
            LZWInputStream lzw = new LZWInputStream(4096);
            string decoded;

            using (TimeIt tm = new TimeIt("decode full"))
            {
                var uState = new LZWInputStream.UnpackBitsState(encoded);

                using (TimeIt _tm = new TimeIt("unpack bits"))
                {
                    var itp = lzw.UnpackBits(uState, encoded);
                    while (itp.MoveNext())
                    {}
                }
                
                var dState = new LZWInputStream.DecoderState(alphabet, uState.codes, uState.outputStringLen);
                using (TimeIt _tm = new TimeIt("decode"))
                {
                    var itd = lzw.Decode(dState, uState.codes);
                    while (itd.MoveNext())
                    {}
                }

                decoded = new string(dState.text);
            }

            return decoded;
        }

        public static void PlayMovie(string data)
        {
            var movie = new AsciiMovie.AsciiMovie(data);
            var itFrames = movie.GetFrameIterator();
            while (itFrames.MoveNext())
            {
                var cur = itFrames.Current;
                //Console.WriteLine(string.Join("\n", cur.rows)); 
            }
        }

        public static void Base64Encode(byte[] encoded, string inputPath, string outputPath)
        {
            int blockSize = 64000; // SE limitation for CustomData field
            char[] base64 = Convert.ToBase64String(encoded).ToCharArray();
            for (int i = 0, storID = 0; i < base64.Length; ++storID, i += blockSize)
            {
                var basename = Path.GetFileNameWithoutExtension(inputPath);
                var fullname = Path.ChangeExtension(basename, $"base64.{storageIDs[storID]}.txt");
                var path = Path.Combine(outputPath, fullname);
                using (StreamWriter sw = new StreamWriter(path))
                {
                    int size = ((i + blockSize) < base64.Length) ? blockSize : (base64.Length - i);
                    sw.Write(base64, i, size);
                }
            }
        }

        public static void TestBase64Decode(string inputPath, string outputPath, byte[] encoded)
        {
            string refBase64 = Convert.ToBase64String(encoded);

            string recombinedBase64 = "";
            List<byte[]> parts = new List<byte[]>();
            int total = 0;
            for (int storID = 0; storID < storageIDs.Length; ++storID)
            {
                var basename = Path.GetFileNameWithoutExtension(inputPath);
                var fullname = Path.ChangeExtension(basename, $"base64.{storageIDs[storID]}.txt");
                var path = Path.Combine(outputPath, fullname);
                if (!File.Exists(path))
                    continue;
                
                using (StreamReader _sr = new StreamReader(path))
                {
                    string _base64 = _sr.ReadToEnd();
                    recombinedBase64 += _base64;
                    byte[] bytes = Convert.FromBase64String(_base64);
                    total += bytes.Length;
                    parts.Add(bytes);
                }
            }
            Debug.Assert(recombinedBase64.GetHashCode() == (new string(refBase64).GetHashCode()));

            byte[] recombined = new byte[total];
            int ri = 0;
            foreach (var part in parts)
            {
                Array.Copy(part, 0, recombined, ri, part.Length);
                ri += part.Length;
            }
            
            Debug.Assert(recombined.Zip(encoded, (x, y) => x == y).All(x => x));
        }

        static void StoreAlphabet(char[] alphabet, string outputPath)
        {
            using (StreamWriter sw = new StreamWriter(Path.Combine(outputPath, "alphabet.base64.txt")))
            {
                sw.Write(Convert.ToBase64String(Encoding.UTF8.GetBytes(alphabet)));
            }
        }

        static void TestAlphabet(char[] alphabet, string outputPath)
        {
            using (StreamReader sr = new StreamReader(Path.Combine(outputPath, "alphabet.base64.txt")))
            {
                byte[] ba = Convert.FromBase64String(sr.ReadToEnd());
                char[] _alphabet = Encoding.UTF8.GetString(ba).ToCharArray();
                Debug.Assert(_alphabet.Zip(alphabet, (x, y) => x == y).All(x => x));
            }
        }

        static void Main(string[] args)
        {
            // if (args.Length == 0)
            // {
            //     var self = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            //     Console.WriteLine($"Usage: {self} path_to_movie.txt");
            //     return;
            // }

            // string path = args[0];
            string path = "movies/input/ascii_star_wars.txt";

            using (StreamReader sr = new StreamReader(path))
            {
                string data = sr.ReadToEnd();
                char[] alphabet = GetAlphabet(data);

                // write alphabet
                StoreAlphabet(alphabet, outputDir);
                TestAlphabet(alphabet, outputDir);
                
                byte[] encoded = Encode(alphabet, data);

                {
                    // test decoder
                    string decoded = Decode(alphabet, encoded);
                    Debug.Assert(data.GetHashCode() == decoded.GetHashCode());

                    // test movie iterator
                    PlayMovie(decoded);
                }

                // write base64 encoded batches
                Base64Encode(encoded, path, outputDir);
                TestBase64Decode(path, outputDir, encoded);
            }          
        }
    }
}
