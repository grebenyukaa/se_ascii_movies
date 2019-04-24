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

                using (StreamWriter sw = new StreamWriter(@"movies/ascii_star_wars.base64.txt"))
                {
                    sw.Write(Convert.ToBase64String(encoded));
                }
            }          
        }
    }
}
