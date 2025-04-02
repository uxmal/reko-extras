#if NYI
namespace prologs;

using angr.analyses;
using Reko.Core;
using Reko.Core.Configuration;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static class boyscout
{

    public static TraceSwitch l = new TraceSwitch(nameof(boyscout), "");

    // 
    //     Try to determine the architecture and endieness of a binary blob
    //     
    public class BoyScout
    {

        private IConfigurationService cfgSvc;
        private Program program;

        public IProcessorArchitecture? arch;

        public int cookiesize;

        public object? endianness;

        public defaultdict<float, float>? votes;

        public BoyScout(IConfigurationService cfgSvc, Program program, int cookiesize = 1)
        {
            this.cfgSvc = cfgSvc;
            this.program = program;
            this.arch = null;
            this.endianness = null;
            this.votes = null;
            this.cookiesize = cookiesize;
            this._reconnoiter();
        }

        // 
        //         The implementation here is simple - just perform a pattern matching of all different architectures we support,
        //         and then perform a vote.
        //         
        public virtual void _reconnoiter()
        {
            // Retrieve the binary string of main binary
            var votes = new defaultdict<(string, string), int>(() => 0);
            foreach (var arch in cfgSvc.GetArchitectures())
            {
                var regexes = new HashSet<object>();
                if (arch.FunctionPrologs.Count == 0)
                {
                    continue;
                }
                // TODO: BoyScout does not support Thumb-only / Cortex-M binaries yet.
                foreach (var ins_regex in new HashSet<object>(arch.function_prologs).Concat(arch.function_epilogs))
                {
                    var r = re.compile(ins_regex);
                    regexes.Add(r);
                }
                foreach (var (start_, data) in this.project.loader.main_object.memory.backers())
                {
                    foreach (var regex in regexes)
                    {
                        // Match them!
                        foreach (var mo in regex.finditer(data))
                        {
                            var position = mo.start() + start_;
                            if (position % arch.instruction_alignment == 0)
                            {
                                votes[(arch.name, arch.memory_endness)] += 1;
                            }
                        }
                    }
                }
                Debug.WriteLineIf(l.TraceVerbose, $"{arch.Name} %s hits %d times", arch.memory_endness, votes[(arch.name, arch.memory_endness)]);
            }
            var (arch_name, endianness, hits) = (from _tup_2 in votes
                                                 let k = _tup_2.Key
                                                 let v = _tup_2.Value
                                                 select (k.Item1, k.Item2, v)).ToList().OrderByDescending(x => x.Item3).ToList()[0];
            if (hits < this.cookiesize * 2)
            {
                // this cannot possibly be code
                arch_name = "DATA";
                endianness = "";
            }
            this.arch = arch_name;
            this.endianness = endianness;
            // Save it as well for debugging
            this.votes = votes;
            l.debug("The architecture should be %s with %s", this.arch, this.endianness);
        }
    }

    static boyscout()
    {
        AnalysesHub.register_default("BoyScout", typeof(BoyScout));
    }
}
}
#endif
