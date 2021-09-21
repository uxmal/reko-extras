using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Benchmarks
{
    class NWayMergeSort
    {
        private record chunk(int[] Items, int Index, int Length);

        public void Sort(int [] items)
        {
            var chunks = MakeChunks(items);
            Parallel.For(0, chunks.Length, i => SortChunk(chunks[i]));
            NWayMerge(chunks);
        }

        private void NWayMerge(chunk[] chunks)
        {
            throw new NotImplementedException();
        }

        private chunk[] MakeChunks(int[] items)
        {
            int chunksize = 30;
            int nItems = items.Length;
            int nChunks = (nItems+ (chunksize - 1)) / chunksize;
            var chunks = new chunk[nChunks];
            for (int i = 0, index = 0; i < nChunks; ++i, index += chunksize, nItems -= chunksize)
            {
                var cItems = new int[Math.Min(chunksize, nItems)];
                Array.Copy(items, index, cItems, 0, cItems.Length);
                chunks[i] = new chunk(cItems, 0, cItems.Length);
            }
            return chunks;
        }

        private void SortChunk(chunk chunk)
        {
            Array.Sort(chunk.Items);
        }

    }
}
