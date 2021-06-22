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
                      select new
                      {
                          elem = x,
                          mnem = x.Value,
                          def = parent.Descendants("modif_f").SingleOrDefault(),
                          use = parent.Descendants("test_f").SingleOrDefault(),
                          fvalues = parent.Descendants("f_vals").SingleOrDefault(),
                          def_fpu = parent.Descendants("modif_f_fpu").SingleOrDefault(),
                      };
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
}
