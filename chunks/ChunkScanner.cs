using Reko.Core;
using Reko.Core.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chunks
{
    public class ChunkScanner
    {
        private readonly WorkUnit workUnit;
        private readonly int chunkSize;

        public ChunkScanner(WorkUnit workUnit, int chunkSize)
        {
            this.workUnit = workUnit;
            this.chunkSize = chunkSize;
        }

        public long DoIt(RewriterTaskFactory factory)
        {
            var taskUnits = new List<RewriterTask>();
            for (int i = 0; i < workUnit.Length; i += chunkSize)
            {
                taskUnits.Add(factory.Create(workUnit, i, i + chunkSize));
            }
            var results = new TaskResult[taskUnits.Count];
            return Time(() => Parallel.ForEach(taskUnits, (src, state, n) =>
            {
                results[n] = src.Run();
            }));
        }

        private static long Time(Action action)
        {
            var sw = new Stopwatch();
            sw.Start();
            action();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
    }
}