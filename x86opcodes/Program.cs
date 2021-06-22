using System;
using System.Xml;
using System.Linq;
using System.Xml.Linq;

namespace x86opcodes
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var doc = XElement.Load(args[0]);
            var ops = from x in doc.Descendants("mnem")
                      orderby x.Value
                      let parent = x.Parent.Parent
                      select new OpInfo
                      {
                          elem = x,
                          mnem = x.Value,
                          def = parent.Descendants("modif_f").SingleOrDefault(),
                          use = parent.Descendants("test_f").SingleOrDefault(),
                          fvalues = parent.Descendants("f_vals").SingleOrDefault(),
                          def_fpu = parent.Descendants("modif_f_fpu").SingleOrDefault(),
                      };
            //DumpOpInfos(ops);

            foreach (var op in ops.Where(o => !string.IsNullOrEmpty(o.def?.Value)))
            {
                Console.WriteLine("case Mnemonic.{0}:", op.mnem);
                EmitDefFlags(op.def?.Value ?? "", op.fvalues?.Value ?? "");
            }
        }

        private static void EmitDefFlags(string def, string set)
        {
            var clrFlags = set.Where(c => char.IsLower(c)).ToHashSet();
            var setFlags = set.Where(c => char.IsUpper(c)).Select(c => char.ToLower(c)).ToHashSet();
            var defFlags = def.Except(clrFlags).Except(setFlags).Distinct();

            var df = string.Join("", defFlags.OrderBy(c => c)).ToUpperInvariant();
            if (df.Length > 0)
            {
                Console.WriteLine("    Registers.{0}", df);
            }
            foreach (var f in setFlags.OrderBy(c => c))
            {
                Console.WriteLine("    m.Assign(Registers.{0}, m.True());", char.ToUpper(f));
            }
            foreach (var f in clrFlags.OrderBy(c => c))
            {
                Console.WriteLine("    m.Assign(Registers.{0}, m.False());", char.ToUpper(f));
            }
            Console.WriteLine("    break;");
        }

        private static void DumpOpInfos(System.Collections.Generic.IEnumerable<OpInfo> ops)
        {
            foreach (var x in ops)
            {
                if (!string.IsNullOrEmpty(x.def?.Value) ||
                    !string.IsNullOrEmpty(x.use?.Value) ||
                    !string.IsNullOrEmpty(x.def_fpu?.Value))
                {
                    Console.WriteLine("Mnemonic.{0}", x.mnem.ToLower());
                    DumpLine("def", x.def?.Value);
                    DumpLine("use", x.use?.Value);
                    DumpLine("fpu", x.def_fpu?.Value);
                    DumpLine("fvl", x.fvalues?.Value);
                }
            }
        }

        private static void DumpLine(string caption, string flag)
        {
            if (string.IsNullOrEmpty(flag))
                return;
            Console.WriteLine("  {0}: {1}", caption, flag);
        }
    }

    internal class OpInfo
    {
        public XElement elem { get; set; }
        public string mnem { get; set; }
        public XElement def { get; set; }
        public XElement use { get; set; }
        public XElement fvalues { get; set; }
        public XElement def_fpu { get; set; }
    }
}
