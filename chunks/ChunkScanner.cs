﻿using Reko.Core;
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

        /// <summary>
        /// Starts a number of parallel tasks.
        /// </summary>
        public (long, int) DoIt(RewriterTaskFactory factory)
        {
            var taskUnits = new List<RewriterTask>();
            for (int i = 0; i < workUnit.Length; i += chunkSize)
            {
                taskUnits.Add(factory.Create(workUnit, i, i + chunkSize));
            }
            var results = new TaskResult[taskUnits.Count];
#if !NO_PARALLEL_THREADS
            var msec = Time(() => Parallel.ForEach(taskUnits, (src, state, n) =>
            {
                results[n] = src.Run();
            }));
#else 
            var msec = Time(() =>
            {
                int n = -1;
                foreach (var task in taskUnits)
                {
                    ++n;
                    results[n] = task.Run();
                }
            });
#endif
            return (msec, results.Sum(r => r.Clusters!.Length));
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