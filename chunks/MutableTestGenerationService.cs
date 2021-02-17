using Reko.Core;
using Reko.Core.Machine;
using Reko.Core.Memory;
using Reko.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chunks
{
    public class MutableTestGenerationService : ITestGenerationService
    {
        private ITestGenerationService svc;

        public MutableTestGenerationService(ITestGenerationService svc)
        {
            this.svc = svc;
        }

        public bool IsMuted { get; set; }

        public string? OutputDirectory {
            get { return svc.OutputDirectory; }
            set { svc.OutputDirectory = value; }
        }
        public void RemoveFiles(string filePrefix)
        {
            svc.RemoveFiles(filePrefix);
        }

        public void ReportMissingDecoder(string testPrefix, Address addrStart, EndianImageReader rdr, string message, Func<byte[], string>? hexize = null)
        {
            if (IsMuted) 
                return;
            svc.ReportMissingDecoder(testPrefix, addrStart, rdr, message, hexize);
        }

        public void ReportMissingRewriter(string testPrefix, MachineInstruction instr, string mnemonic, EndianImageReader rdr, string message, Func<byte[], string>? hexize = null)
        {
            if (IsMuted)
                return;
            svc.ReportMissingRewriter(testPrefix, instr, mnemonic, rdr, message, hexize);
        }

        public void ReportProcedure(string fileName, string testCaption, Procedure proc)
        {
            if (IsMuted)
                return;
            svc.ReportProcedure(fileName, testCaption, proc);
        }
    }
}
