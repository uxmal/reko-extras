/*
 * Copyright 2018 Stefano Moioli<smxdev4@gmail.com>
 **/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LLVMTestsConverter
{
    public class TestScanner
    {

        public delegate void PrintUnitTestDelegate(string assembly, string encoding, CommentSubType modifier);

        private enum Architecture
        {
            AArch64,
            Arm,
            Mips,
            PowerPc,
            Sparc,
            x86
        }

        public static void PrintDummyUnitTest(string assembly, string encoding)
        {
            Console.WriteLine($"//TODO: {assembly} => {encoding}");
        }

        private static byte[] ByteSwap(dynamic value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return bytes;
        }

        private static UInt32 ToValueBe(string encoding)
        {
            return Convert.ToUInt32(encoding.Replace(",", "").Replace("0x", ""), 16);
        }

        private static UInt32 ToValueLe(string encoding)
        {
            return BitConverter.ToUInt32(
                ByteSwap(ToValueBe(encoding)
            ), 0);
        }

        private static readonly char[] testName_trim = new char[]{
            '(', ')',
            '{', '}',
            '$', '!',
            ','
        };

        private const string MEM_REGEX = @"\[(.*?)\]";
        private static readonly Dictionary<string, string> testName_repl = new Dictionary<string, string>()
        {
            { @"(\s+|\:|\.)+", "_" },
            { "%", "_reg_" },
            { "#", "_const_" },
            { @"\+", "_plus_" },
            { "-", "_minus_" },
			{ "*", "_times_" },
            { MEM_REGEX, "_mem_" },
        };

        private static string ConvertTestName(string assembly)
        {
            string testName = string.Concat(
                assembly.Where(c => !testName_trim.Contains(c))
            );
            foreach (var pair in testName_repl)
            {
                MatchCollection mem = null;
                if (pair.Key == MEM_REGEX)
                {
                    mem = Regex.Matches(testName, MEM_REGEX);

                    string[] chunks = new string[mem.Count];
                    // replace chunks from the end (so that other parts don't shift to the right)
                    for (int i = mem.Count - 1; i >= 0; i--)
                    {
                        var textChunk = mem[i].Groups[0];
                        var match = mem[i].Groups[1];

                        testName =
                                    testName.Substring(0, textChunk.Index) +
                                    "_mem_" +
                                    ConvertTestName(match.Value) +
                                    testName.Substring(textChunk.Index + textChunk.Length);
                    }
                }

                testName = Regex.Replace(testName, pair.Key, pair.Value);

            }

            testName = Regex.Replace(testName, @"_+", "_");

            return testName;
            /*
                var chars = Regex.Replace(assembly, @"(\s|-|%|#|\[|\])+", "_")
				    .Where(c => !testName_trim.Contains(c));
			    return string.Concat(chars);
            */
        }

        private static bool writeFiles = true;

        private static HashSet<String> knownTestNames = new HashSet<string>();

        private static void PrintAArch64UnitTest(string assembly, string encoding, CommentSubType modifier)
        {
            StringBuilder sb = new StringBuilder();

            UInt32 valueBe = ToValueBe(encoding);
            UInt32 valueLe = ToValueLe(encoding);

            string template = @"		[Test]
	// Little-endian: {0:X8}
	public void AArch64Dis_0x{1:X8}() {{
		Given_Instruction(0x{1:X8});
		Expect_Code(""{2}"");
	}}";
            string unitTest = string.Format(template, valueLe, valueBe, assembly);
            WriteUnitTest(
                Convert.ToString(valueBe),
                Architecture.AArch64, unitTest
            );
        }

        private static void PrintArmUnitTest(string assembly, string encoding, CommentSubType modifier)
        {
            string template = @"	[Test]
	public void ArmDasm_{0}() {{
		var instr = Disassemble32(0x{2:X8});
		Assert.AreEqual(""{1}"", instr.ToString());
	}}";

            string testName = ConvertTestName(assembly);
            string unitTest = string.Format(template,
                testName,
                assembly,
                ToValueBe(encoding)
            );
            WriteUnitTest(
                testName,
                Architecture.Arm, unitTest
            );
        }

        private static void PrintPowerPcUnitTest(string assembly, string encoding, CommentSubType modifier)
        {
            string template = @"	[Test]
	public void PPCDis_{0}() {{
			var instr = DisassembleWord(0x{2:X8});
			Assert.AreEqual(""{1}"", instr.ToString());
	}}";

            string testName = string.Format("{0:X8}", ToValueBe(encoding));

            switch (modifier)
            {
            case CommentSubType.PPCServer:
                testName += "_server";
                break;
            case CommentSubType.PPCEmbedded:
                testName += "_embedded";
                break;
            case CommentSubType.PPCBigEndian:
                testName += "_be";
                break;
            case CommentSubType.PPCLittleEndian:
                testName += "_le";
                break;
            }

            string unitTest = string.Format(template,
                testName,
                assembly,
                ToValueBe(encoding)
            );
            WriteUnitTest(
                testName,
                Architecture.PowerPc, unitTest
            );
        }

        private static void PrintMipsUnitTest(string assembly, string encoding, CommentSubType modifier)
        {
            string template = @"	[Test]
	public void MipsDis_{0}() {{
		AssertCode(""{1}"", 0x{2:X8});
	}}";

            string testName = ConvertTestName(assembly);
            string unitTest = string.Format(template,
                testName,
                assembly,
                ToValueBe(encoding)
            );
            WriteUnitTest(
                testName,
                Architecture.Mips, unitTest
            );
        }

        private static void PrintSparcUnitTest(string assembly, string encoding, CommentSubType modifier)
        {
            string template = @"	[Test]
	public void SparcDis_{0}() {{
		AssertInstruction(0x{2:X8}, ""{1}"");
	}}";
            string testName = ConvertTestName(assembly);
            if (modifier == CommentSubType.SparcNoCasa)
            {
                testName += "_no_casa";
            }
            string unitTest = string.Format(template,
                testName,
                assembly,
                ToValueBe(encoding)
            );
            WriteUnitTest(
                testName,
                Architecture.Sparc, unitTest
            );
        }

        private static void PrintX86UnitTest(string assembly, string encoding, CommentSubType modifier)
        {
            string template = @"	[Test]
	public void Dis_{0}() {{
		var instr = Disassemble64({1});
		Assert.AreEqual(""{2}"", instr.ToString());
	}}";
            string testName = ConvertTestName(assembly);

            string unitTest = string.Format(template, testName, encoding, assembly);
            WriteUnitTest(
                testName,
                Architecture.x86, unitTest
            );

        }

        private static void WriteUnitTest(string testName, Architecture arch, string unitTest)
        {
            if (knownTestNames.Contains(testName))
                return;
            knownTestNames.Add(testName);

            if (!writeFiles)
            {
                Console.WriteLine(unitTest);
                return;
            }
            File.AppendAllText(fileNames[arch], unitTest + Environment.NewLine);
        }

        private static readonly Dictionary<Architecture, string> fileNames = new Dictionary<Architecture, string>() {
            { Architecture.AArch64, "reko_aarch64.cs" },
            { Architecture.Arm, "reko_arm.cs" },
            { Architecture.Mips, "reko_mips.cs" },
            { Architecture.PowerPc, "reko_ppc.cs" },
            { Architecture.Sparc, "reko_sparc.cs" },
            { Architecture.x86, "reko_x86.cs" },
        };

        private static readonly Dictionary<string, PrintUnitTestDelegate> arches = new Dictionary<string, PrintUnitTestDelegate>() {
            { "AArch64", new PrintUnitTestDelegate(PrintAArch64UnitTest) },
            { "ARM", new PrintUnitTestDelegate(PrintArmUnitTest) },
            { "Mips", new PrintUnitTestDelegate(PrintMipsUnitTest) },
            { "PowerPC", new PrintUnitTestDelegate(PrintPowerPcUnitTest) },
            { "Sparc", new PrintUnitTestDelegate(PrintSparcUnitTest) },
            { "X86", new PrintUnitTestDelegate(PrintX86UnitTest) },
        };

        private string dirPath;
        public TestScanner(string dirPath)
        {
            this.dirPath = dirPath;
        }

        private void ProcessDir(string dir, PrintUnitTestDelegate unitTestPrinter)
        {
            foreach (string file in Directory.GetFiles(dir, "*.s", SearchOption.AllDirectories))
            {
                Console.WriteLine($"  FILE => {file}");
                using (StreamReader rdr = File.OpenText(file))
                {
                    var asmFile = new LLVMTestConverter(rdr, unitTestPrinter, out bool success);
                    if (!success)
                        continue;
                }
            }
        }

        public void Work()
        {
            foreach (string filename in fileNames.Values)
            {
                if (File.Exists(filename))
                    File.Delete(filename);
            }

            string[] dirs = Directory.GetDirectories(dirPath);
            foreach (string dir in dirs)
            {
                var match = arches.Where((pair) => pair.Key.Contains(Path.GetFileName(dir))).FirstOrDefault();
                if (match.Value != null)
                {
                    Console.WriteLine($" DIR => {dir}");
                    ProcessDir(dir, match.Value);
                }
            }
        }
    }
}
