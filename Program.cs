using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using LZW;
using AsciiMovie;

namespace test
{
    class Program
    {
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

        static void Main(string[] args)
        {
            using (StreamReader sr = new StreamReader(@"movies/ascii_star_wars.txt"))
            {
                string data = sr.ReadToEnd();
                
                byte[] encoded;
                {
                    LZW.LZW lzw = new LZW.LZW(Alphabet.StarWars);
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

                var movie = new AsciiMovie.AsciiMovie(data);
                var itFrames = movie.GetFrameIterator();
                while (itFrames.MoveNext())
                {
                    var cur = itFrames.Current;
                    //Console.WriteLine(string.Join("\n", cur.rows)); 
                }

                int blockSize = 64000; // SE limitation for CustomData field
                char[] base64 = Convert.ToBase64String(encoded).ToCharArray();
                for (int i = 0, storID = 0; i < base64.Length; ++storID, i += blockSize)
                {
                    using (StreamWriter sw = new StreamWriter($"movies/ascii_star_wars.base64.{storageIDs[storID]}.txt"))
                    {
                        int size = ((i + blockSize) < base64.Length) ? blockSize : (base64.Length - 1 - i);
                        sw.Write(base64, i, size);
                    }
                }

                using (StreamWriter sw = new StreamWriter($"movies/ascii_star_wars.base64.txt"))
                {
                    sw.Write(base64);
                }
            }          
        }
    }
}
