using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.blocksoup;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Reko.Core;
using Reko.Core.Machine;

public class BlockSoup
{
    private readonly Program program;

    public BlockSoup(Program program)
    {
        this.program = program;
    }

    [System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]
    public void Extract()
    {
        IRewriterHost host = new RewriterHost();
        var instrs = CollectInstructions(new InstrAdapter(program.Architecture), host);
        Console.WriteLine($"{instrs.Count,9} instructions");
        var clusters = CollectInstructions(new ClusterAdapter(program.Architecture, host), host);
        Console.WriteLine($"{clusters.Count,9} clusters");
    }

    private List<T> CollectInstructions<T>(Adapter<T> adapter, IRewriterHost host)
    {
        var instrs = new List<T>();
        foreach (var segment in program.SegmentMap.Segments.Values)
        {
            var offsetStart = segment.Address - segment.MemoryArea.BaseAddress;
            var offsetEnd = offsetStart + segment.Size;
            var arch = program.Architecture;
            var step = arch.InstructionBitSize / arch.CodeMemoryGranularity;
            var sw = Stopwatch.StartNew();
            var active = new Dictionary<Address, IEnumerator<T>>();
            for (int offset = 0; offset < segment.Size; offset += step)
            {
                var addr = segment.Address + offset;
                if (!active.TryGetValue(addr, out var e))
                {
                    var rdr = arch.CreateImageReader(segment.MemoryArea, addr);
                    e = adapter.CreateNewEnumerator(rdr);
                }
                else
                {
                    active.Remove(addr);
                }
                try
                {
                    if (e.MoveNext())
                    {
                        var item = e.Current;
                        instrs.Add(item);
                        var addrNext = adapter.NextAddress(item);
                        active.TryAdd(addrNext, e);
                    }
                }
                catch (Exception ex)
                {
                    host.Error(addr, $"*** {ex.Message}");
                }

                if (offset > 0 && offset % 100_000 == 0)
                {
                    Status(instrs, segment, sw, offset);
                }
            }
            Status(instrs, segment, sw);
            Console.WriteLine();
        }
        return instrs;
    }

    private static void Status<T>(List<T> instrs, Core.Loading.ImageSegment segment, Stopwatch sw, int offset = -1)
    {
        var percentage = offset >= 0
            ? $"{100.0 * offset / segment.Size:###.0}%"
            : "Done";
        Console.Write($"{segment.Name,-12}: 0x{segment.Size:X8} {instrs.Count / sw.Elapsed.TotalMilliseconds,10:######.0} instr/ms [{percentage}]     \r");
    }
}
