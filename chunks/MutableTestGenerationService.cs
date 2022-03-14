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
        private readonly object lockObject;

        public MutableTestGenerationService(ITestGenerationService svc)
        {
            this.svc = svc;
            this.lockObject = new object();
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
            lock (lockObject)
                svc.ReportMissingDecoder(testPrefix, addrStart, rdr, message, hexize);
        }

        public void ReportMissingDecoder(string testPrefix, Address addrStart, string message, string opcodeAsText)
        {
            if (IsMuted)
                return;
            lock (lockObject)
                svc.ReportMissingDecoder(testPrefix, addrStart, message, opcodeAsText);
        }

        public void ReportMissingRewriter(string testPrefix, MachineInstruction instr, string mnemonic, EndianImageReader rdr, string message, Func<byte[], string>? hexize = null)
        {
            if (IsMuted)
                return;
            lock (lockObject)
                svc.ReportMissingRewriter(testPrefix, instr, mnemonic, rdr, message, hexize);
        }

        public void ReportMissingRewriter(string testPrefix, MachineInstruction instr, string mnemonic, EndianImageReader rdr, string message, string opcodeAsText)
        {
            if (IsMuted)
                return;
            lock (lockObject)
                svc.ReportMissingRewriter(testPrefix, instr, mnemonic, rdr, message, opcodeAsText);
        }

        public void ReportProcedure(string fileName, string testCaption, Procedure proc)
        {
            if (IsMuted)
                return;
            lock (lockObject)
            svc.ReportProcedure(fileName, testCaption, proc);
        }
    }
}
