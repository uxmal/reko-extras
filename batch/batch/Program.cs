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
    class Program
    {
        static string REKO = Environment.GetEnvironmentVariable("REKO");

        static BlockingCollection<string> Seen = new BlockingCollection<string>();

        static int running = 0;
        static ManualResetEvent finished = new ManualResetEvent(false);

        static void GenTest(dynamic obj)
        {
            string hex = obj.hex;
            string asm = obj.asm;

            var parts = Regex.Split(asm, @"\s+");

            string mnem = parts[0];
            mnem = Regex.Replace(mnem, @"\(|\)|\s+|\[|\]|\+|\*|\-|,|\.", "_");


            string args = string.Join(" ", parts.Skip(1));

            List<string> hexBytes = new List<string>();
            if((hex.Length % 2) != 0)
            {
                Console.Error.WriteLine($"//UNHANDLED CASE {hex} {asm}");
                return;
            }
            for(int i=0; i<hex.Length; i+= 2)
            {
                hexBytes.Add("0x" + hex.Substring(i, 2));
            }

            string bytesStr = string.Join(",", hexBytes);

            string tpl = "[Test]" + Environment.NewLine +
                $"public void X86Dis_{mnem}_{hex}() {{" + Environment.NewLine +
                $"  var instr = Disassemble64({bytesStr});" + Environment.NewLine +
                $"  Assert.AreEqual(\"{mnem}\\t{args}\", instr.ToString());";
            Console.Error.WriteLine(tpl);
        }

        static IEnumerable<object> ParseLLVM(Process llvm)
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
                        yield return new
                        {
                            line = line,
                            hex = "",
                            asm = ""
                        };
                    }
                    if (line.StartsWith(".text")){
                        found = true;
                    }
                    continue;
                }

                // mnem\targs  # encoding: [0xde,0xad,0xbe,0xff]

                var m = Regex.Match(line, @"(.*?) # encoding: \[(.*?)\]");
                if (!m.Success || !m.Groups[1].Success || !m.Groups[2].Success)
                    continue;

                //annoying, since we need tabs for Gen, but we need to be consistent with objdump
                string asm = m.Groups[1].Value.Replace("\t", " ");
                string bytes = m.Groups[2].Value
                    .Replace("0x", "")
                    .Replace(",", "");

                yield return new
                {
                    line = line,
                    hex = bytes,
                    asm = asm
                };

                break; //only first line for now
            }
        }

        static IEnumerable<object> ParseObjDump(Process objDump)
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

                yield return new
                {
                    line = line,
                    hex = hex,
                    asm = asm
                };

                break; //only first line for now
            }
        }

        static void RunLLVM(string chunk)
        {
            byte[] bin = File.ReadAllBytes(chunk);

            StringBuilder sb = new StringBuilder();
            foreach(byte b in bin)
            {
                sb.AppendFormat("0x{0:X2},", b);
            }
            sb.Length--;

            Process llvm = Process.Start(new ProcessStartInfo()
            {
                FileName = "cmd",
                Arguments = $"/c echo {sb.ToString()} | llvm-mc -disassemble -triple=x86_64 -show-encoding -output-asm-variant=1", //variant 1 -> Intel syntax
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            ParseLLVM(llvm).All((dynamic obj) =>
            {
                Console.WriteLine($"[LLVM] {obj.hex} => {obj.asm}");
                if (OptGen)
                {
                    GenTest(obj);
                }
                return true;
            });
        }

        static void RunObjDump(string chunk)
        {
            Process objDump = Process.Start(new ProcessStartInfo()
            {
                FileName = "cmd",
                Arguments = $"/c objdump -D -Mintel,x86-64 -b binary -m i386 {chunk}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            ParseObjDump(objDump).All((dynamic obj) =>
            {
                Console.WriteLine($"[OBJDUMP] {obj.hex} => {obj.asm}");
                if (OptGen)
                {
                    GenTest(obj);
                }
                return true;
            });

            File.Delete(chunk);
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

            ThreadPool.QueueUserWorkItem(new WaitCallback((stateInfo) =>
            {
                Interlocked.Increment(ref running);

                Console.Error.WriteLine($"Processing {path}");

                Process proc = Process.Start(new ProcessStartInfo()
                {
                    FileName = REKO,
                    Arguments = $" --arch x86-protected-64 --base 0 --loader raw --heuristic shingle \"{path}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });

                while (!proc.StandardOutput.EndOfStream)
                {
                    string line = proc.StandardOutput.ReadLine();

                    var match = Regex.Match(line, @"decoder for the instruction (.*?) at address (.*?) \(");
                    if (!match.Success || !match.Groups[1].Success || !match.Groups[2].Success)
                        continue;

                    //string hex = match.Groups[1].Value;
                    long addr = Convert.ToInt64(match.Groups[2].Value, 16);

                    string hexPrefix = match.Groups[1].Value;

                    if (!Seen.Contains(hexPrefix))
                    {
                        Seen.Add(hexPrefix);
                        Console.Error.WriteLine($"[NEW] {hexPrefix}");

                        using(var fs = File.OpenRead(path))
                        {
                            fs.Seek(addr, SeekOrigin.Begin);
                            byte[] buf = new byte[15];
                            fs.Read(buf, 0, buf.Length);

                            string name = buf.GetHashCode().ToString();

                            string filePath = $"chunks/{name}.bin";

                            File.WriteAllBytes(filePath, buf);

                            if(OptObjDump)
                                RunObjDump(filePath);
                            if (OptLLVM)
                                RunLLVM(filePath);
                        }
                    }
                }

                if (Interlocked.Decrement(ref running) == 0)
                {
                    finished.Set();
                }
            }));
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

        static int ParseArguments(string[] args)
        {
            int i = 0;
            for (; i < args.Length; i++)
            {
                switch (args[i])
                {
                case "--":
                    return ++i;
                case "-mzonly":
                    OptMzOnly = true;
                    break;
                case "-objdump":
                    OptObjDump = true;
                    break;
                case "-llvm":
                    OptLLVM = true;
                    break;
                case "-gen":
                    OptGen = true;
                    break;
                }
            }
            return i;
        }

        static void Main(string[] args)
        {

            int i = ParseArguments(args);

            if (!Directory.Exists("chunks"))
                Directory.CreateDirectory("chunks");

            new DirectoryIterator(args[i], ProcessFile).Run();

            finished.WaitOne();


            /*string SEP = "C3909090909090909090909090909090";

            string finalHex = string.Join(SEP, Seen.ToArray()) + SEP;

            byte[] bin = StringToByteArray(finalHex);
            File.WriteAllBytes("collected.bin", bin);*/
        }
    }
}
