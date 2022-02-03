using System;

namespace chunks
{
    public abstract class RewriterTaskFactory
    {
        public abstract string Name { get; }
        public abstract RewriterTask Create(WorkUnit workUnit, int iStart, int iEnd);
    }
}