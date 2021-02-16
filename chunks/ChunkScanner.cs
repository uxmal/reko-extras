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

        public void doit()
        {
            var taskUnits = new List<TaskUnit>();
            for (int i = 0; i < workUnit.Length; i += chunkSize)
            {
                taskUnits.Add(new TaskUnit(workUnit, i, i + chunkSize));
            }
            var results = new TaskResult[taskUnits.Count];
            Parallel.ForEach(taskUnits, (src, state, n) =>
            {
                results[n] = src.Run();
            });
        }

        private static void Time(Action action)
        {
            var sw = new Stopwatch();
            sw.Start();
            action();
            sw.Stop();
            Console.WriteLine("*** Elapsed time {0}", sw.ElapsedMilliseconds);
        }
    }
}