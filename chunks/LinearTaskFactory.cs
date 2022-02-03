namespace chunks
{
    public class LinearTaskFactory : RewriterTaskFactory
    {
        public override string Name => "Linear";

        public override RewriterTask Create(WorkUnit workUnit, int iStart, int iEnd)
        {
            return new LinearTask(workUnit, iStart, iEnd);
        }
    }
}