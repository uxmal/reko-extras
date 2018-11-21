/*
 * Quick and Dirty helper to discover unknown opcodes
 * Copyright (C) 2018 Stefano Moioli <smxdev4@gmail.com>
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace batch
{
    struct ParseResult
    {
        public string hex;
        public string asm;
    }

    class Program
    {
        static string REKO = Environment.GetEnvironmentVariable("REKO");

        static BlockingCollection<string> Seen = new BlockingCollection<string>();

        static int running = 0;
        static bool didStartRunning = false;
        static ManualResetEvent finished = new ManualResetEvent(false);

        static void GenTest(dynamic obj, TextWriter output)
        {
            string hex = obj.hex;
            string asm = obj.asm;

            var parts = Regex.Split(asm, @"\s+");

            string mnem = parts[0];
            mnem = Regex.Replace(mnem, @"\(|\)|\s+|\[|\]|\+|\*|\-|,|\.", "_");

            string args = string.Join(" ", parts.Skip(1));

            List<string> hexBytes = new List<string>();
            if ((hex.Length % 2) != 0)
            {
                output.WriteLine($"//UNHANDLED CASE {hex} {asm}");
                return;
            }
            for (int i=0; i<hex.Length; i+= 2)
            {
                hexBytes.Add("0x" + hex.Substring(i, 2));
            }

            string bytesStr = string.Join(",", hexBytes);

            output.WriteLine("[Test]");
            output.WriteLine($"public void X86Dis_{mnem}_{hex}");
            output.WriteLine("{");
            output.WriteLine($"    var instr = Disassemble64({bytesStr});");
            output.WriteLine($"    Assert.AreEqual(\"{mnem}\\t{args}\", instr.ToString());");
            output.WriteLine("}");
            output.WriteLine();
        }

        static IEnumerable<ParseResult> ParseLLVM(string inputHex, Process llvm)
        {
            var stream = llvm.StandardOutput;
            bool found = false;

            while (!stream.EndOfStream)
            {
                string line = stream.ReadLine().Trim();
                string stderr = llvm.StandardError.ReadLine();
                if(!found){
                    if (stderr != null && stderr.Contains("invalid instruction encoding"))
                    {
                        // Discard the line LLVM complains about so we can find 
                        // the caret on the next line.
                        /*string cause = */ llvm.StandardError.ReadLine();

                        string caretLine = llvm.StandardError.ReadLine();
                        // llvm writes a caret where it found the encoding error
                        // if it's at the beginning, the original hex is invalid
                        if (caretLine.StartsWith("^"))
                        {
                            yield return new ParseResult()
                            {
                                hex = inputHex,
                                asm = "(bad)"
                            };
                            yield break;
                        }
                    }
                    if (line.StartsWith(".text")){
                        found = true;
                    }
                    continue;
                }

                // mnem\targs  # encoding: [0xde,0xad,0xbe,0xff]
                // mnem\targs  // encoding: [...]

                var m = Regex.Match(line, @"(.*?) .* encoding: \[(.*?)\]");
                if (!m.Success ||
                    !m.Groups[1].Success ||
                    !m.Groups[2].Success)
                    continue;


                //annoying, since we need tabs for Gen, but we need to be consistent with objdump
                string asm = m.Groups[1].Value.Replace("\t", " ");
                string bytes = m.Groups[2].Value
                    .Replace("0x", "")
                    .Replace(",", "");

                yield return new ParseResult
                {
                    hex = bytes,
                    asm = asm
                };

                break; //only first line for now
            }
        }

        static IEnumerable<ParseResult> ParseObjDump(Process objDump)
        {
            var stream = objDump.StandardOutput;
            bool found = false;
            while (!stream.EndOfStream)
            {
                string line = stream.ReadLine().TrimEnd();
                if (!found){
                    if(line.StartsWith("00000000 <.data>:"))
                    {
                        found = true;
                    }
                    continue;
                }

                //line number - hex - asm
                var parts = line.Split('\t');
                if (parts.Length < 3)
                    continue;

                string hex = parts[1].Trim();
                hex = Regex.Replace(hex, @"\s", "");

                string asm = parts[2].Trim();

                yield return new ParseResult
                {
                    hex = hex,
                    asm = asm
                };

                break; //only first line for now
            }
        }

        static void RunLLVM(string chunk)
        {
            byte[] bin = File.ReadAllBytes(chunk);


            var asmFragment = string.Join(",", bin.Select(b => $"0x{b:X2}"));

            Process llvm = Process.Start(new ProcessStartInfo()
            {
                FileName = "cmd.exe",
                Arguments = $"/c llvm-mc -disassemble -triple={OptDasmArchArgs} -show-encoding -output-asm-variant=1", //variant 1 -> Intel syntax
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            });

            llvm.StandardInput.WriteLine(asmFragment);
            llvm.StandardInput.Close();

            string inputHex = string.Join("", bin.Select(b => $"{b:X2}"));
            ParseLLVM(inputHex, llvm).All(obj =>
            {
                Console.Error.WriteLine($"[LLVM] {obj.hex} => {obj.asm}");
                if (OptGen)
                {
                    GenTest(obj, Console.Out);
                }
                return true;
            });

            llvm.WaitForExit();
        }

        static void RunObjDump(string chunk)
        {
            Process objDump = Process.Start(new ProcessStartInfo()
            {
                FileName = "cmd",
                Arguments = $"/c objdump -D {OptDasmArchArgs} -b binary {chunk}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            ParseObjDump(objDump).All(obj =>
            {
                Console.Error.WriteLine($"[OBJDUMP] {obj.hex} => {obj.asm}");
                if (OptGen)
                {
                    GenTest(obj, Console.Out);
                }
                return true;
            });

            objDump.WaitForExit();
        }

        static void ProcessFile(string path)
        {
            finished.Reset();

            if (OptMzOnly)
            {
                string MZ = Encoding.ASCII.GetString(
                    new BinaryReader(File.OpenRead(path)).ReadBytes(2)
                );

                if (MZ != "MZ")
                    return;
            }

            didStartRunning = true;

            ThreadPool.QueueUserWorkItem(stateInfo => CollectRekoUnimplementedInstructions(path));
        }

        private static void CollectRekoUnimplementedInstructions(string path)
        {
            Interlocked.Increment(ref running);

            Console.Error.WriteLine($"Processing {path}");
            Process proc = Process.Start(new ProcessStartInfo
            {
                FileName = REKO,
                Arguments = $" --time-limit 120 --scan-only --arch {OptRekoArchArgs} --base 0 --loader raw --heuristic shingle \"{path}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
            using (var fs = File.OpenRead(path))
            {
                while (!proc.StandardOutput.EndOfStream)
                {
                    string line = proc.StandardOutput.ReadLine();
                    var match = Regex.Match(line, @"Reko: a decoder for .*? instruction (.*?) at address (.*?) has not");
                    if (!match.Success || !match.Groups[1].Success || !match.Groups[2].Success)
                        continue;

                    //string hex = match.Groups[1].Value;
                    long addr = Convert.ToInt64(match.Groups[2].Value, 16);

                    string hexPrefix = match.Groups[1].Value;

                    if (!Seen.Contains(hexPrefix))
                    {
                        Seen.Add(hexPrefix);
                        Console.Error.WriteLine($"[NEW] {addr:X8} {hexPrefix}");

                        string filePath = WriteChunkFile(fs, addr);

                        if (OptObjDump)
                            RunObjDump(filePath);
                        if (OptLLVM)
                            RunLLVM(filePath);

                        if (!OptKeepChunks)
                            File.Delete(filePath);
                    }
                }
            }

            proc.WaitForExit();

            if (Interlocked.Decrement(ref running) == 0)
            {
                finished.Set();
            }
        }

        /// <summary>
        /// Copies the 15 bytes starting at the offending offset
        /// in the source file and writes them into a chunk file.
        /// The chunk files are processed later by a disassembler.
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="addr"></param>
        /// <returns></returns>
        private static string WriteChunkFile(FileStream fs, long addr)
        {
            fs.Seek(addr, SeekOrigin.Begin);
            byte[] buf = new byte[16];
            fs.Read(buf, 0, buf.Length);

            string name = buf.GetHashCode().ToString();

            string filePath = $"chunks/{name}.bin";

            File.WriteAllBytes(filePath, buf);
            return filePath;
        }

        static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private static bool OptMzOnly = false;
        private static bool OptObjDump = false;
        private static bool OptLLVM = false;
        private static bool OptGen = false;
		private static bool OptKeepChunks = false;

        private static string OptDasmArchArgs;
        private static string OptRekoArchArgs;

        private static int OptNumThreads = -1;

        static string Unquote(string str)
        {
            var m = Regex.Match(str, "\"(.*)\"");
            if (m.Success && m.Groups[1].Success)
            {
                str = m.Groups[1].Value;
            }
            return str;
        }

        /// <summary>
        /// Parses a command line option
        /// </summary>
        /// <param name="args"></param>
        /// <param name="i"></param>
        /// <param name="value"></param>
        /// <returns>Either 
        /// null, in which case the caller should bail out, or the index
        /// of the first non-option parameter in <paramref name="args"/></returns>
        static int? ParseOption(string[] args, ref int i, out string value)
        {
            string val = args[i];
            var m = Regex.Match(val, ".*?=(.*)");

            int next_i = -1;

            if(m.Success && m.Groups[1].Success)
            {
                value = m.Groups[1].Value;
                next_i = i;
            } else if(i + 1 < args.Length)
            {
                value = args[i + 1];
                next_i = i + 1;
            } else
            {
                value = null;
                return null;
            }

            if(next_i > -1)
            {
                value = Unquote(value);
                i = next_i;
            }
            return next_i;
        }

        /// <summary>
        /// Parse the command line arguments.
        /// </summary>
        /// <returns>
        /// Either 
        /// null, in which case the caller should bail out, or the index
        /// of the first non-option parameter in <paramref name="args"/></returns>
        static int? ParseArguments(string[] args)
        {
            int i = 0;
            int? next;
            for (; i < args.Length; i++)
            {
                string arg = args[i];
                var m = Regex.Match(arg, "(.*?)=.*");
                if(m.Success && m.Groups[1].Success)
                {
                    arg = m.Groups[1].Value;
                }

                switch (arg)
                {
                case "--":
                    return ++i;
                case "-help":
                case "-h":
                    Usage();
                    return null;
                case "-reko":
                    next = ParseOption(args, ref i, out REKO);
                    if (next == null)
                        goto Usage;
                    break;
                case "-mzonly":
                    OptMzOnly = true;
                    break;
                case "-objdump":
                    OptObjDump = true;
                    break;
                case "-llvm":
                    OptLLVM = true;
                    break;
                case "-gentests":
                    OptGen = true;
                    break;
				case "-keep":
					OptKeepChunks = true;
					break;
                case "-arch-dis":
                    next = ParseOption(args, ref i, out OptDasmArchArgs);
                    if (next == null)
                        goto Usage;
                    break;
                case "-arch-reko":
                    next = ParseOption(args, ref i, out OptRekoArchArgs);
                    if (next == null)
                        goto Usage;
                    break;
                case "-nproc":
                    next = ParseOption(args, ref i, out string nprocValue);
                    if (next == null)
                        goto Usage;
                    OptNumThreads = int.Parse(nprocValue);
                    break;
                default:
                    return i;
                }
            }
            return i;

            Usage:
            Usage();
            return null;
        }

        static void Main(string[] args)
        {

            int? i = ParseArguments(args);
            if (i == null)
                return;

            if(OptDasmArchArgs == null || OptRekoArchArgs == null)
            {
                OptRekoArchArgs = "x86-protected-64";
                if (OptLLVM)
                {
                    OptDasmArchArgs = "x86_64";
                } else if (OptObjDump)
                {
                    OptDasmArchArgs = "-Mintel,x86-64 -m i386";
                }
            }

            if (OptNumThreads > -1)
            {
                if(!ThreadPool.SetMinThreads(OptNumThreads, OptNumThreads))
                {
                    throw new ArgumentException($"Invalid value {OptNumThreads} for -nproc");
                }
                if(!ThreadPool.SetMaxThreads(OptNumThreads, OptNumThreads))
                {
                    throw new ArgumentException($"Invalid value {OptNumThreads} for -nproc");
                }
            }

            Console.Error.WriteLine($"DIS-ARCH  => {OptDasmArchArgs}");
            Console.Error.WriteLine($"REKO-ARCH => {OptRekoArchArgs}");

            if (!Directory.Exists("chunks"))
                Directory.CreateDirectory("chunks");

            var arg = args[i.Value].TrimEnd();
            new DirectoryIterator(arg, ProcessFile).Run();

            if (!didStartRunning)
            {
                Console.Error.WriteLine("Nothing to do");
                return;
            }

            finished.WaitOne();


            /*string SEP = "C3909090909090909090909090909090";

            string finalHex = string.Join(SEP, Seen.ToArray()) + SEP;

            byte[] bin = StringToByteArray(finalHex);
            File.WriteAllBytes("collected.bin", bin);*/
        }

        private static void Usage()
        {
            Console.Error.Write(
@"batch [options] file...

Disassembles each file with Reko to discover instructions that
are not yet implemented. These instructions are then collated with
disassemblies from other disassemblies for comparison.
In order to run this tool, the environment variable REKO must be set
to the absolute path of the instance of Reko you wish to execute.
Options:
    -h, -help   Displays this message.
    -mzonly     Only process files that have the MZ magic number (MS-DOS or 
                PE executables).
    -objdump    Use objdump to verify disassembly of machine code.
    -llvm       Use LLVM's llvm-mc tool to verify disassembly of machine code.
    -gentests   Generate unit tests ready to incorporate into Reko unit
                test project.
    -keep       Keep binary chunks
    -arch-dis   Architecture argument(s) for the disassembler
    -arch-reko  Architecture argument(s) for Reko
    -nproc      Number of parallel jobs to run (for directory lookup)
");
        }
    }
}
