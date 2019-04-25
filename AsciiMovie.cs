using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LZW;

namespace AsciiMovie
{
    public class AsciiMovie
    {
        public AsciiMovie(string decodedMovie)
        {
            rowStream = decodedMovie.Split('\n');
        }

        public class Frame
        {
            public Frame(IEnumerable<string> rows)
            {
                var sTime = rows.FirstOrDefault();
                if (sTime.Length == 0)
                    throw new ArgumentException("bad frame data: empty");
                
                time = Math.Max(Convert.ToInt32(sTime), 1) * 4; // 1 second = 60 ticks, value is given in 1/15th of a second
                this.rows = rows.Skip(1).ToArray();
            }
            public string[] rows { get; private set; }
            public int time { get; private set; }
        }

        public IEnumerator<Frame> GetFrameIterator(int rowsPerFrame = 14)
        {
            for (int fid = 0; fid < rowStream.Length / rowsPerFrame; ++fid)
            {
                IEnumerable<string> frameRows = rowStream.Skip(fid * rowsPerFrame).Take(rowsPerFrame);
                yield return new Frame(frameRows.ToArray());
            }
        }

        private string[] rowStream;
    }
}