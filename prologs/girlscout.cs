namespace angr.analyses;
#if !NOT_YET
using pickle;

using re;

using datetime = datetime.datetime;

using progressbar;

using AnnotatedCFG = annocfg.AnnotatedCFG;

using System.Collections.Generic;
using System.Linq;
using System;
using AnalysesHub = angr.analyses.AnalysesHub;
using Blade = blade.Blade;
using AngrGirlScoutError = errors.AngrGirlScoutError;
using static angr.analyses.analysis;
using cle.backends;
using System.IO;
using o = angr.sim_options;
using static angr.engines.pcode.lifter;
using Reko.Core;
using Reko.Core.Lib;
using Reko.Scanning;
using Reko.Core.Loading;
using Reko.Core.Graphs;
using Reko.Core.Expressions;
using Reko.Core.Memory;
using Reko.ImageLoaders.WebAssembly;
using System.Net.Sockets;
using Reko.Core.Rtl;

public static class girlscout
{

    public static logging.Logger l = logging.getLogger(name: nameof(girlscout));

    // 
    //     We find functions inside the given binary, try to decide the base address if needed, and build a control-flow
    //     graph on top of that to see if there is an entry or not. Obviously if the binary is not loaded as a blob (not
    //     using Blob as its backend), GirlScout will not try to determine the base address.
    // 
    //     It's also optional to perform a full code scan of the binary to show where all codes are. By default we don't scan
    //     the entire binary since it's time consuming.
    // 
    //     You probably need a BoyScout to determine the possible architecture and endianess of your binary blob.
    //     
    public class GirlScout
    {

        public Project project;

        public Program program;

        private IProcessorArchitecture arch;

        public Program _binary;

        public Dictionary<object, int> _block_size;

        public Address _end;

        public HashSet<(string, long)> _indirect_jumps;

        public Address _next_addr;

        public bool _perform_full_code_scan;

        public bool _pickle_intermediate_results;

        public defaultdict<long, List<object>> _read_addr_to_run;

        public SegmentList _seg_list;

        public Address _start;

        public HashSet<long> _unassured_functions;

        public long _valid_memory_region_size;

        public List<(Address addrStart, Address addrEnd)> _valid_memory_regions;

        public defaultdict<long, List<object>> _write_addr_to_run;

        public long? base_address;

        public DiGraph<Address> cfg;

        public HashSet<Address> functions;

        private DiGraph<Address> _call_map;

        public GirlScout(
            Project project,
            Program? binary = null,
            Address? start = null,
            Address? end = null,
            bool pickle_intermediate_results = false,
            bool perform_full_code_scan = false)
        {
            this.project = project;
            this._binary = binary is not null ? binary : this.project.Programs[0];
            this._start = start is not null ? start.Value : this._binary.SegmentMap.BaseAddress;
            this._end = end is not null ? end.Value : this._binary.GetMaxAddress();
            this._pickle_intermediate_results = pickle_intermediate_results;
            this._perform_full_code_scan = perform_full_code_scan;
            l.debug("Starts at 0x%08x and ends at 0x%08x.", this._start, this._end);
            // Valid memory regions
            this._valid_memory_regions = (from _tup_1 in this.program.SegmentMap.Segments.Values
                                          let startR = _tup_1.Address
                                          let backer = _tup_1.MemoryArea
                                          select (startR, (startR + backer.Length))).OrderBy(_p_1 => _p_1).ToList();
            this._valid_memory_region_size = (from _tup_2 in this._valid_memory_regions
                                              let startr = _tup_2.Item1
                                              let endr = _tup_2.Item2
                                              select (endr - startr)).ToList().Sum();
            // Size of each basic block
            this._block_size = new Dictionary<object, int> { };
            this._next_addr = this._start - 1;
            // Starting point of functions
            this.functions = new();
            // Calls between functions
            this._call_map = new DiGraph<Address>();
            // A CFG - this is not what you get from project.analyses.CFG() !
            this.cfg = new DiGraph<Address>();
            // Create the segment list
            this._seg_list = new SegmentList();
            this._read_addr_to_run = new defaultdict<long, List<object>>(() => []);
            this._write_addr_to_run = new defaultdict<long, List<object>>(() => new List<object>());
            // All IRSBs with an indirect exit target
            this._indirect_jumps = new HashSet<(string, long)>();
            this._unassured_functions = new HashSet<long>();
            this.base_address = null;
            // Start working!
            this._reconnoiter();
        }

        public DiGraph<Address> call_map
        {
            get
            {
                return this._call_map;
            }
            private set
            {
                this._call_map = value;
            }
        }

        public virtual Address? _get_next_addr_to_search(uint? alignment = null)
        {
            // TODO: Take care of those functions that are already generated
            var curr_addr = this._next_addr;
            if (this._seg_list.has_blocks)
            {
                curr_addr = this._seg_list.next_free_pos(curr_addr);
            }
            if (alignment != null)
            {
                if (curr_addr.Offset % alignment.Value > 0)
                {
                    curr_addr = curr_addr - (long)(curr_addr.Offset % alignment.Value + alignment.Value);
                }
            }
            // Make sure curr_addr exists in binary
            var accepted = false;
            foreach (var (start, end) in this._valid_memory_regions)
            {
                if (curr_addr >= start && curr_addr < end)
                {
                    // accept
                    accepted = true;
                    break;
                }
                if (curr_addr < start)
                {
                    // accept, but we are skipping the gap
                    accepted = true;
                    curr_addr = start;
                }
            }
            if (!accepted)
            {
                // No memory available!
                return null;
            }
            this._next_addr = curr_addr;
            if (curr_addr < this._end)
            {
                l.debug("Returning new recon address: 0x%08x", curr_addr);
                return curr_addr;
            }
            else
            {
                l.debug("0x%08x is beyond the ending point.", curr_addr);
                return null;
            }
        }

        // 
        //         Besides calling _get_next_addr, we will check if data locates at that address seems to be code or not. If not, 
        //         we'll move on to request for next valid address.
        //         
        public virtual Address? _get_next_code_addr(ProcessorState initial_state)
        {
            var next_addr = this._get_next_addr_to_search();
            if (next_addr is null)
            {
                return null;
            }
            var start_addr = next_addr.Value;
            var sz = "";
            var is_sz = true;
            var memory = (ByteProgramMemory)this.program.Memory;
            while (is_sz)
            {
                // Get data until we meet a 0
                while (this.program.SegmentMap.IsValidAddress(next_addr.Value))
                {
                    try
                    {
                        l.debug("Searching address {0:X}", next_addr);
                        if (memory.TryReadUInt8(next_addr.Value, out byte val);
                        if (val == 0)
                        {
                            if (sz.Length < 4)
                            {
                                is_sz = false;
                            }
                            else
                            {
                                reach_end = true;
                            }
                            break;
                        }
                        if (!@string.printable.Contains((char)val))
                        {
                            is_sz = false;
                            break;
                        }
                        sz += (char)val;
                        next_addr += 1;
                    }
                    catch (SimValueError)
                    {
                        // Not concretizable
                        l.debug("Address 0x{0:X8} is not concretizable!", next_addr);
                        break;
                    }
                }
                if (sz.Length > 0 && is_sz)
                {
                    l.debug("Got a string of {0} chars: [{1}]", sz.Count, sz);
                    // l.debug("Occpy %x - %x", start_addr, start_addr + len(sz) + 1)
                    this._seg_list.occupy(start_addr, sz.Length + 1);
                    sz = "";
                    next_addr = this._get_next_addr_to_search();
                    if (next_addr is null)
                    {
                        return null;
                    }
                    // l.debug("next addr = %x", next_addr)
                    start_addr = next_addr.Value;
                }
                if (is_sz)
                {
                    next_addr += 1;
                }
            }
            var instr_alignment = initial_state.Architecture.InstructionBitSize / initial_state.Architecture.MemoryGranularity;
            if ((long)start_addr.Offset % instr_alignment > 0)
            {
                start_addr = start_addr - (long) start_addr.Offset % instr_alignment + instr_alignment;
            }
            l.debug("_get_next_code_addr() returns 0x%x", start_addr);
            return start_addr;
        }

        // 
        //         When an IRSB has more than two exits (for example, a jumptable), we
        //         cannot concretize their exits in concrete mode. Hence we statically
        //         execute the function from beginning in this method, and then switch to
        //         symbolic mode for the final IRSB to get all possible exits of that
        //         IRSB.
        //         
        public virtual List<Address> _symbolic_reconnoiter(long addr, long target_addr, int max_depth = 10)
        {
            var state = this.project.factory.blank_state(addr: addr, mode: "symbolic", add_options: new HashSet{
                    o.CALLLESS});
            var initial_exit = this.project.factory.path(state);
            var explorer = new Explorer(this.project, start: initial_exit, max_depth: max_depth, find: target_addr, num_find: 1).run();
            if (explorer.found.Count > 0)
            {
                var path = explorer.found[0];
                var last_run = path.last_run;
                return last_run.flat_exits();
            }
            else
            {
                return new List<Address>();
            }
        }

        public virtual void _static_memory_slice(object run)
        {
            object concrete_addr;
            object addr;
            if (run is engines.pcode.lifter.IRSB irsb)
            {
                foreach (var stmt in irsb.statements)
                {
                    var refs = stmt.actions;
                    if (refs.Count > 0)
                    {
                        var real_ref = refs[^1];
                        if (real_ref is SimActionData sad)
                        {
                            if (sad.action == "write")
                            {
                                addr = real_ref.addr;
                                if (!run.initial_state.solver.symbolic(addr))
                                {
                                    concrete_addr = run.initial_state.solver.eval(addr);
                                    this._write_addr_to_run[addr].append(run.addr);
                                }
                            }
                            else if (real_ref.action == "read")
                            {
                                addr = real_ref.addr;
                                if (!run.initial_state.solver.symbolic(addr))
                                {
                                    concrete_addr = run.initial_state.solver.eval(addr);
                                }
                                this._read_addr_to_run[addr].append(run.addr);
                            }
                        }
                    }
                }
            }
        }

        public virtual void _scan_code(ISet<Address> traced_addresses, defaultdict<Address, HashSet<float>> function_exits, ProcessorState initial_state, Address starting_address)
        {
            // Saving tuples like (current_function_addr, next_exit_addr)
            // Current_function_addr == -1 for exits not inside any function
            var remaining_exits = new HashSet<(Address, Address, Address, ProcessorState)>();
            var next_addr = starting_address;
            // Initialize the remaining_exits set
            remaining_exits.Add((next_addr, next_addr, next_addr, initial_state.Clone()));
            while (remaining_exits.Count > 0)
            {
                var (current_function_addr, previous_addr, parent_addr, state) = remaining_exits.pop();
                if (traced_addresses.Contains(previous_addr))
                {
                    continue;
                }
                // Add this node to the CFG first, in case this is a dangling node
                this.cfg.AddNode(previous_addr);
                if (current_function_addr.Offset != -1)
                {
                    l.debug("Tracing new exit 0x%08x in function 0x%08x", previous_addr, current_function_addr);
                }
                else
                {
                    l.debug("Tracing new exit 0x%08x", previous_addr);
                }
                traced_addresses.Add(previous_addr);
                this._scan_block(previous_addr, state, current_function_addr, function_exits, remaining_exits, traced_addresses);
            }
        }

        public virtual void _scan_block(
            Address addr,
            ProcessorState state,
            Address? current_function_addr,
            IDictionary<Address, List<Address>> function_exits,
            List<(Address, Address, Address, ProcessorState?)> remaining_exits,
            HashSet<Address> traced_addresses)
        {
            Address? next_addr;
            RtlBlock irsb;
            // Let's try to create the pyvex IRSB directly, since it's much faster
            try
            {
                irsb = this.project.factory.block(addr).vex;
                // Log the size of this basic block
                this._block_size[addr] = irsb.size;
                // Occupy the block
                this._seg_list.occupy(addr, irsb.size);
            }
            catch
            {
                return;
            }
            var next = irsb.next;
            var jumpkind = irsb.jumpkind;
            var successors = irsb.Instructions
                .OfType<RtlTransfer>()
                .Where(b => b.Target is Address)
                .Select(i => ((Address)i.Target, i)).ToList();
            successors.Add((next, jumpkind));
            // Process each successor
            foreach (var suc in successors)
            {
                var (target, jk) = suc;
                if (target is Address a)
                {
                    next_addr = a;
                }
                else
                {
                    next_addr = null;
                }
                if (jk == "Ijk_Boring" && next_addr != null)
                {
                    remaining_exits.Add((current_function_addr.Value, next_addr.Value, addr, null));
                }
                else if (jk == "Ijk_Call" && next_addr != null)
                {
                    // Log it before we cut the tracing :)
                    if (jk == "Ijk_Call")
                    {
                        if (current_function_addr.HasValue)
                        {
                            this.functions.Add(current_function_addr.Value);
                            this.functions.Add(next_addr.Value);
                            this.call_map.AddEdge(current_function_addr.Value, next_addr.Value);
                        }
                        else
                        {
                            this.functions.Add(next_addr.Value);
                            this.call_map.AddNode(next_addr.Value);
                        }
                    }
                    else if (jumpkind == "Ijk_Boring" || jumpkind == "Ijk_Ret")
                    {
                        if (current_function_addr.HasValue)
                        {
                            function_exits[current_function_addr.Value].Add(next_addr.Value);
                        }
                    }
                    // If we have traced it before, don't trace it anymore
                    if (traced_addresses.Contains(next_addr.Value))
                    {
                        return;
                    }
                    remaining_exits.Add((next_addr.Value, next_addr.Value, addr, null));
                    l.debug("Function calls: %d", this.call_map.Nodes.Count);
                }
            }
        }

        private void _scan_block_(
            Address addr,
            ProcessorState state,
            Address? current_function_addr,
            Dictionary<Address, List<Address>> function_exits,
            List<(Address, Address, Address, ProcessorState)> remaining_exits,
            HashSet<Address> traced_addresses)
        {
            ProcessorState new_state;
            // Get a basic block
            state.InstructionPointer = addr;
            var s_path = this.project!.factory.path(state);
            try
            {
                var s_run = s_path.next_run;
            }
            catch (Exception ex)
            {
                // Cannot concretize something when executing the SimRun
                l.debug(ex);
                return;
            }
            if (s_run is SimIRSB sirsb)
            {
                // Calculate its entropy to avoid jumping into uninitialized/all-zero space
                var bytes = sirsb.irsb._state[1]["bytes"];
                var size = sirsb.irsb.size;
                var ent = this._calc_entropy(bytes, size: size);
                if (ent < 1.0 && size > 40)
                {
                    // Skipping basic blocks that have a very low entropy
                    return;
                }
            }
            // self._static_memory_slice(s_run)
            // Mark that part as occupied
            if (s_run is SimIRSB)
            {
                this._seg_list.occupy(addr, s_run.irsb.size);
            }
            var successors = s_run.flat_successors + s_run.unsat_successors;
            var has_call_exit = false;
            var tmp_exit_set = new HashSet<Address>();
            foreach (var suc in successors)
            {
                if (suc.history.jumpkind == "Ijk_Call")
                {
                    has_call_exit = true;
                }
            }
            foreach (var suc in successors)
            {
                var jumpkind = suc.history.jumpkind;
                if (has_call_exit && jumpkind == "Ijk_Ret")
                {
                    jumpkind = "Ijk_FakeRet";
                }
                if (jumpkind == "Ijk_Ret")
                {
                    continue;
                }
                Address next_addr;
                try
                {
                    // Try to concretize the target. If we can't, just move on
                    // to the next target
                    next_addr = suc.solver.eval_one(suc.ip);
                }
                catch
                {
                    // Undecidable jumps (might be a function return, or a conditional branch, etc.)
                    // We log it
                    this._indirect_jumps.Add((suc.history.jumpkind, addr));
                    l.info("IRSB 0x%x has an indirect exit %s.", addr, suc.history.jumpkind);
                    continue;
                }
                this.cfg.AddEdge(addr, next_addr, jumpkind: jumpkind);
                // Log it before we cut the tracing :)
                if (jumpkind == "Ijk_Call")
                {
                    if (current_function_addr.HasValue)
                    {
                        this.call_map.AddEdge(current_function_addr.Value, next_addr);
                    }
                    else
                    {
                        this.call_map.AddNode(next_addr);
                    }
                }
                else if (jumpkind == "Ijk_Boring" || jumpkind == "Ijk_Ret")
                {
                    if (current_function_addr.HasValue)
                    {
                        function_exits[current_function_addr.Value].add(next_addr);
                    }
                }
                // If we have traced it before, don't trace it anymore
                if (traced_addresses.Contains(next_addr))
                {
                    continue;
                }
                // If we have traced it in current loop, don't tract it either
                if (tmp_exit_set.Contains(next_addr))
                {
                    continue;
                }
                tmp_exit_set.Add(next_addr);
                if (jumpkind == "Ijk_Call")
                {
                    // This is a call. Let's record it
                    new_state = suc.copy();
                    // Unconstrain those parameters
                    // TODO: Support other archs as well
                    // if 12 + 16 in new_state.registers.mem:
                    //    del new_state.registers.mem[12 + 16]
                    //if 16 + 16 in new_state.registers.mem:
                    //    del new_state.registers.mem[16 + 16]
                    //if 20 + 16 in new_state.registers.mem:
                    //    del new_state.registers.mem[20 + 16]
                    // 0x8000000: call 0x8000045
                    remaining_exits.Add((next_addr, next_addr, addr, new_state));
                    l.debug("Function calls: %d", this.call_map.nodes().Count);
                }
                else if (jumpkind == "Ijk_Boring" || jumpkind == "Ijk_Ret" || jumpkind == "Ijk_FakeRet")
                {
                    new_state = suc.copy();
                    l.debug("New exit with jumpkind %s", jumpkind);
                    // FIXME: should not use current_function_addr if jumpkind is "Ijk_Ret"
                    remaining_exits.add((current_function_addr, next_addr.Value, addr, new_state));
                }
                else if (jumpkind == "Ijk_NoDecode")
                {
                    // That's something VEX cannot decode!
                    // We assume we ran into a deadend
                }
                else if (jumpkind.startswith("Ijk_Sig"))
                {
                    // Should not go into that exit
                }
                else if (jumpkind == "Ijk_TInval")
                {
                    // ppc32: isync
                    // FIXME: It is the same as Ijk_Boring! Process it later
                }
                else if (jumpkind == "Ijk_Sys_syscall")
                {
                    // Let's not jump into syscalls
                }
                else if (jumpkind == "Ijk_InvalICache")
                {
                }
                else if (jumpkind == "Ijk_MapFail")
                {
                }
                else if (jumpkind == "Ijk_EmWarn")
                {
                }
                else
                {
                    throw new Exception("NotImplemented");
                }
            }
        }

        // 
        //         Scan the entire program space for prologues, and start code scanning at those positions
        //         :param traced_address:
        //         :param function_exits:
        //         :param initial_state:
        //         :param next_addr:
        //         :returns:
        //         
        public virtual void _scan_function_prologues(long traced_address, IDictionary<long, HashSet<long>> function_exits, object initial_state)
        {
            // Precompile all regexes
            var regexes = new HashSet<object>();
            foreach (var ins_regex in this.project.arch.function_prologs)
            {
                var r = re.compile(ins_regex);
                regexes.Add(r);
            }
            // TODO: Make sure self._start is aligned
            // Construct the binary blob first
            foreach (var segment in this.project.Programs[0].SegmentMap.Segments.Values)
            {
                var bytes_ = segment.MemoryArea;
                var start_ = segment.Address;
                foreach (var regex in regexes)
                {
                    // Match them!
                    foreach (var mo in regex.finditer(bytes_))
                    {
                        var position = mo.start() + start_;
                        if (position % (this.arch.InstructionBitSize / this.arch.MemoryGranularity) == 0)
                        {
                            if (!traced_address.Contains(position))
                            {
                                var percentage = this._seg_list.occupied_size * 100.0 / this._valid_memory_region_size;
                                l.info("Scanning %xh, progress %0.04f%%", position, percentage);
                                this._unassured_functions.Add(position);
                                this._scan_code(traced_address, function_exits, initial_state, position);
                            }
                            else
                            {
                                l.info("Skipping %xh", position);
                            }
                        }
                    }
                }
            }
        }

        // 
        //         Execute each basic block with an indeterminiable exit target
        //         :returns:
        //         
        public virtual IEnumerable<Address> _process_indirect_jumps()
        {
            var function_starts = new HashSet<Address>();
            l.info("We have %d indirect jumps", this._indirect_jumps.Count);
            foreach (var (jumpkind, irsb_addr) in this._indirect_jumps)
            {
                // First execute the current IRSB in concrete mode
                if (function_starts.Count > 20)
                {
                    break;
                }
                if (jumpkind == "Ijk_Call")
                {
                    var state = this.project.factory.blank_state(addr: irsb_addr, mode: "concrete", add_options: new HashSet{
                            o.SYMBOLIC_INITIAL_VALUES});
                    var path = this.project.factory.path(state);
                    l.debug(irsb_addr.ToString("X"));
                    try
                    {
                        var r = (path.next_run.successors + path.next_run.unsat_successors)[0];
                        var ip = r.solver.eval_one(r.ip);
                        function_starts.Add(ip);
                        continue;
                    }
                    catch
                    {
                    }
                    var irsb = this.project.factory.block(irsb_addr).vex;
                    var stmts = irsb.statements;
                    // Start slicing from the "next"
                    var b = new Blade(this.cfg, irsb.addr, -1, project: this.project);
                    // Debugging output
                    foreach (var (addr, stmt_idx) in b.slice.nodes().OrderBy(_p_1 => _p_1).ToList())
                    {
                        irsb = this.project.factory.block(addr).vex;
                        stmts = irsb.statements;
                        l.debug("%x: %d | %s %d", (addr, stmt_idx), stmts[stmt_idx], b.slice.in_degree((addr, stmt_idx)));
                    }
                    // Get all sources
                    var sources = (from n in b.slice.nodes()
                                   where b.slice.in_degree(n) == 0
                                   select n).ToList();
                    // Create the annotated CFG
                    var annotatedcfg = new AnnotatedCFG(this.project, null, target_irsb_addr: irsb_addr, detect_loops: false);
                    annotatedcfg.from_digraph(b.slice);
                    foreach (var (src_irsb, src_stmt_idx) in sources)
                    {
                        // Use slicecutor to execute each one, and get the address
                        // We simply give up if any exception occurs on the way
                        var start_state = this.project.factory.blank_state(addr: src_irsb, add_options: new HashSet<string>{
                                o.DO_RET_EMULATION, o.TRUE_RET_EMULATION_GUARD});
                        var start_path = this.project.factory.path(start_state);
                        // Create the slicecutor
                        var slicecutor = new Slicecutor(this.project, annotatedcfg, start: start_path, targets: ValueTuple.Create(irsb_addr));
                        // Run it!
                        try
                        {
                            slicecutor.run();
                        }
                        catch (KeyError)
                        {
                            // This is because the program slice is incomplete.
                            // Blade will support more IRExprs and IRStmts
                            l.debug("KeyError occurred due to incomplete program slice.", exc_info: ex);
                            continue;
                        }
                        foreach (var r in slicecutor.reached_targets)
                        {
                            if (r.next_run.successors)
                            {
                                var target_ip = r.next_run.successors[0].ip;
                                var se = r.next_run.successors[0].se;
                                if (!se.symbolic(target_ip))
                                {
                                    var concrete_ip = se.eval_one(target_ip);
                                    function_starts.Add(concrete_ip);
                                    l.info("Found a function address {0:X}", concrete_ip);
                                }
                            }
                        }
                    }
                }
            }
            return function_starts;
        }

        // 
        //         Voting for the most possible base address.
        // 
        //         :param function_starts:
        //         :param functions:
        //         :returns:
        //         
        public virtual Address? _solve_forbase_address(IEnumerable<Address> function_starts, HashSet<Address> functions)
        {
            var pseudo_base_addr = this.program.SegmentMap.BaseAddress;
            var base_addr_ctr = new Dictionary<Address, int> { };
            foreach (var s in function_starts)
            {
                foreach (var f in functions)
                {
                    var base_addr = pseudo_base_addr + (s - f);
                    var ctr = 1;
                    foreach (var k in function_starts)
                    {
                        if (functions.Contains(pseudo_base_addr + (k - base_addr)))
                        {
                            ctr += 1;
                        }
                    }
                    if (ctr > 5)
                    {
                        base_addr_ctr[base_addr] = ctr;
                    }
                }
            }
            if (base_addr_ctr.Count > 0)
            {
                var (base_addr, hits) = (from _tup_1 in base_addr_ctr
                                         let k = _tup_1.Key
                                         let v = _tup_1.Value
                                         select (k, v)).OrderByDescending(x => x.Item2).First();
                return base_addr;
            }
            else
            {
                return null;
            }
        }

        public virtual void _reconnoiter()
        {
            if (this._binary is cle.backends.blob.Blob)
            {
                this._determinebase_address();
            }
            if (this._perform_full_code_scan)
            {
                this._full_code_scan();
            }
        }

        // 
        //         The basic idea is simple: start from a specific point, try to construct
        //         functions as much as we can, and maintain a function distribution graph
        //         and a call graph simultaneously. Repeat searching until we come to the
        //         end that there is no new function to be found.
        //         A function should start with:
        //             # some addresses that a call exit leads to, or
        //             # certain instructions. They are recoreded in SimArch.
        // 
        //         For a better performance, instead of blindly scanning the entire process
        //         space, we first try to search for instruction patterns that a function
        //         may start with, and start scanning at those positions. Then we try to
        //         decode anything that is left.
        //         
        public virtual void _determinebase_address()
        {
            var traced_address = new HashSet<Address>();
            this.functions = new HashSet<Address>();
            this.call_map = new DiGraph<Address>();
            this.cfg = new DiGraph<Address>();
            var initial_state = this.project.factory.blank_state(mode: "fastpath");
            var initial_options = initial_state.options - new HashSet {
                    o.TRACK_CONSTRAINTS} - o.refs;
            initial_options.Add(o.SUPER_FASTPATH);
            // initial_options.remove(o.COW_STATES)
            initial_state.options = initial_options;
            // Sadly, not all calls to functions are explicitly made by call
            // instruction - they could be a jmp or b, or something else. So we
            // should record all exits from a single function, and then add
            // necessary calling edges in our call map during the post-processing
            // phase.
            var function_exits = new defaultdict<Address, HashSet<Address>>(() => new HashSet<Address>());
            var dump_file_prefix = this.program.Location.GetFilename();
            if (this._pickle_intermediate_results && File.Exists(dump_file_prefix + "_indirect_jumps.angr"))
            {
                l.debug("Loading existing intermediate results.");
                this._indirect_jumps = pickle.load(open(dump_file_prefix + "_indirect_jumps.angr", "rb"));
                this.cfg = pickle.load(open(dump_file_prefix + "_coercecfg.angr", "rb"));
                this._unassured_functions = pickle.load(open(dump_file_prefix + "_unassured_functions.angr", "rb"));
            }
            else
            {
                // Performance boost :-)
                // Scan for existing function prologues
                this._scan_function_prologues(traced_address, function_exits, initial_state);
                if (this._pickle_intermediate_results)
                {
                    l.debug("Dumping intermediate results.");
                    pickle.dump(this._indirect_jumps, open(dump_file_prefix + "_indirect_jumps.angr", "wb"), -1);
                    pickle.dump(this.cfg, open(dump_file_prefix + "_coercecfg.angr", "wb"), -1);
                    pickle.dump(this._unassured_functions, open(dump_file_prefix + "_unassured_functions.angr", "wb"), -1);
                }
            }
            if (this._indirect_jumps.Count > 0)
            {
                // We got some indirect jumps!
                // Gotta execute each basic block and see where it wants to jump to
                var function_starts = this._process_indirect_jumps();
                this.base_address = this._solve_forbase_address(function_starts, this._unassured_functions);
                l.info("Base address should be 0x{0:X}", this.base_address);
            }
            else
            {
                l.debug("No indirect jumps are found. We switch to the slowpath mode.");
                // TODO: Slowpath mode...
                while (true)
                {
                    var next_addr = this._get_next_code_addr(initial_state);
                    var percentage = this._seg_list.occupied_size * 100.0 / this._valid_memory_region_size;
                    l.info("Analyzing %xh, progress %0.04f%%", next_addr, percentage);
                    if (next_addr == null)
                    {
                        break;
                    }
                    this.call_map.AddNode(next_addr);
                    this._scan_code(traced_address, function_exits, initial_state, next_addr);
                }
            }
            // Post-processing: Map those calls that are not made by call/blr
            // instructions to their targets in our map
            foreach (var (src, s) in function_exits)
            {
                if (this.call_map.Nodes.Contains(src))
                {
                    foreach (var target in s)
                    {
                        if (this.call_map.Nodes.Contains(target))
                        {
                            this.call_map.AddEdge(src, target);
                        }
                    }
                }
            }
            var nodes = this.call_map.Nodes.OrderBy(_p_1 => _p_1).ToList();
            foreach (var i in Enumerable.Range(0, nodes.Count - 1))
            {
                if (nodes[i] >= nodes[i + 1] - 4)
                {
                    foreach (var dst in this.call_map.Successors(nodes[i + 1]))
                    {
                        this.call_map.AddEdge(nodes[i], dst);
                    }
                    foreach (var src in this.call_map.Predecessors(nodes[i + 1]))
                    {
                        this.call_map.AddEdge(src, nodes[i]);
                    }
                    this.call_map.RemoveNode(nodes[i + 1]);
                }
            }
            l.debug("Construction finished.");
        }

        // 
        //         Perform a full code scan on the target binary.
        //         
        public virtual void _full_code_scan()
        {
            // We gotta time this function
            var start_time = datetime.now();
            var traced_address = new HashSet<Address>();
            this.functions = new HashSet<Address>();
            this.call_map = new DiGraph<Address>();
            this.cfg = new DiGraph<Address>();
            var initial_state = this.arch.CreateProcessorState();
            var initial_options = initial_state.options - new HashSet{
                    o.TRACK_CONSTRAINTS} - o.refs;
            initial_options.Add(o.SUPER_FASTPATH);
            // initial_options.remove(o.COW_STATES)
            initial_state.options = initial_options;

            // Sadly, not all calls to functions are explicitly made by call
            // instruction - they could be a jmp or b, or something else. So we
            // should record all exits from a single function, and then add
            // necessary calling edges in our call map during the post-processing
            // phase.
            var function_exits = new defaultdict<Address, HashSet<float>>(() => new HashSet<float>());
            while (true)
            {
                var next_addr = this._get_next_code_addr(initial_state);
                var percentage = this._seg_list.occupied_size * 100.0 / this._valid_memory_region_size;
                if (percentage > 100.0)
                {
                    percentage = 100.0;
                }
                // pb.update(percentage * 10000);
                if (next_addr != null)
                {
                    l.info("Analyzing %xh, progress %0.04f%%", next_addr.Value, percentage);
                }
                else
                {
                    l.info("No more addr to analyze. Progress %0.04f%%", percentage);
                    break;
                }
                this.call_map.AddNode(next_addr.Value);
                this._scan_code(traced_address, function_exits, initial_state, next_addr);
            }
            pb.finish();
            var end_time = datetime.now();
            l.info("A full code scan takes %d seconds.", (end_time - start_time).seconds);
        }

        public virtual double _calc_entropy(byte[] data, int size = 0)
        {
            if (data == null || data.Length == 0)
            {
                return 0;
            }
            double entropy = 0;
            if (size == 0)
            {
                size = data.Length;
            }
            int[] hist = new int[256];
            foreach (var b in data)
            {
                hist[b] += 1;
            }
            for (int i = 0; i < hist.Length; ++i)
            {
                var p_x = (double)hist[i] / size;
                if (p_x > 0)
                {
                    entropy += -p_x * Math.Log(p_x) / Math.Log(2);
                }
            }
            return entropy;
        }

        public virtual string _dbg_output()
        {
            var ret = "";
            ret += "Functions:\n";
            var function_list = this.functions.ToList();
            // Sort it
            function_list = function_list.OrderBy(_p_1 => _p_1).ToList();
            foreach (var f in function_list)
            {
                ret += String.Format("0x%08x", f);
            }
            return ret;
        }

        // 
        //         Generate a sif file from the call map
        //         
        public virtual void genenare_callmap_sif(string filepath)
        {
            var graph = this.call_map;
            if (graph == null)
            {
                throw new AngrGirlScoutError("Please generate the call graph first.");
            }
            var f = File.CreateText(filepath);
            foreach (var (src, dst) in graph.Edges)
            {
                f.WriteLine(String.Format("0x{0,X}\tDirectEdge\t0x{1,X}", src, dst));
            }
            f.Close();
        }

        // 
        //         Generate a list of all recovered basic blocks.
        //         
        public virtual List<(Address, int)> generate_code_cover()
        {
            var lst = new List<(Address, int)>();
            foreach (var irsb_addr in this.cfg.Nodes)
            {
                if (!this._block_size.TryGetValue(irsb_addr, out int irsb_size))
                {
                    continue;
                }
                lst.Add((irsb_addr, irsb_size));
            }
            lst = lst.OrderBy(x => x.Item1).ToList();
            return lst;
        }
    }

}

public static class ProgramExtensions
{
    public static Address GetMaxAddress(this Program program)
    {
        return program.SegmentMap.Segments.Values.Max(s => s.Address + s.Size);
    }
}
#endif