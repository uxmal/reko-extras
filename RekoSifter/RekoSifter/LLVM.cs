using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace RekoSifter
{
    /// <summary>
    /// This class uses LLVM to disassemble instructions. It does so by spawning
    /// LLVM in a separate process.
    /// </summary>
	public class LLVM
    {
        static IEnumerable<ParseResult> ParseLLVM(string inputHex, Process llvm) {
            var stream = llvm.StandardOutput;
            bool found = false;

            while (!stream.EndOfStream) {
                string line = stream.ReadLine().Trim();
                string stderr = llvm.StandardError.ReadLine();

                if (!found) {
                    if (stderr != null && stderr.Contains("invalid instruction encoding")) {
                        // Discard the line LLVM complains about so we can find 
                        // the caret on the next line.
                        /*string cause = */
                        llvm.StandardError.ReadLine();

                        string caretLine = llvm.StandardError.ReadLine();
                        // llvm writes a caret where it found the encoding error
                        // if it's at the beginning, the original hex is invalid
                        if (caretLine.StartsWith("^")) {
                            yield return new ParseResult() {
                                hex = inputHex,
                                asm = "(bad)"
                            };
                            yield break;
                        }
                    }
                    if (line.StartsWith(".text")) {
                        found = true;
                    }
                    continue;
                }

                // mnem\targs  # encoding: [0xde,0xad,0xbe,0xff]
                // mnem\targs  // encoding: [...]

                var m = Regex.Match(line, @"(.*?) # encoding: \[(.*?)\]");
                if (!m.Success ||
                    !m.Groups[1].Success ||
                    !m.Groups[2].Success)
                    continue;


                //annoying, since we need tabs for Gen, but we need to be consistent with objdump
                string asm = m.Groups[1].Value.Replace("\t", " ").Trim();
                string bytes = m.Groups[2].Value
                    .Replace("0x", "")
                    .Replace(",", " ")
                    .ToUpperInvariant();

                yield return new ParseResult {
                    hex = bytes,
                    asm = asm
                };

                break; //only first line for now
            }
        }

        public static IEnumerable<ParseResult> Disassemble(string arch, byte[] bin) {

            var asmFragment = string.Join(",", bin.Select(b => $"0x{b:X2}"));

            Process llvm = Process.Start(new ProcessStartInfo() {
                FileName = "cmd.exe",
                Arguments = $"/c llvm-mc -disassemble -triple={arch} -show-encoding -output-asm-variant=1", //variant 1 -> Intel syntax
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            });

            llvm.StandardInput.WriteLine(asmFragment);
            llvm.StandardInput.Close();

            string inputHex = string.Join("", bin.Select(b => $"{b:X2}"));
            foreach(var obj in ParseLLVM(inputHex, llvm)) {
                yield return obj;
            }

            llvm.WaitForExit();
        }
    }
}