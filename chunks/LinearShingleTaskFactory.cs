namespace chunks
{
    public class LinearShingleTaskFactory : RewriterTaskFactory
    {
        public override string Name => "Linear shingle scan";
        
        public override RewriterTask Create(WorkUnit workUnit, int iStart, int iEnd)
        {
            return new LinearShingleTask(workUnit, iStart, iEnd);
        }
    }
}