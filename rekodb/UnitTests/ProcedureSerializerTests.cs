using NUnit.Framework;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Serialization.Json;
using Reko.Core.Types;
using Reko.Database.UnitTests.Mocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Database.UnitTests
{
    [TestFixture]
    public class ProcedureSerializerTests
    {
        private void RunTest(string sExpected, Action<ProcedureBuilder> builder)
        {
            var m = new ProcedureBuilder(new FakeArchitecture());
            builder(m);

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            var json = new JsonWriter(sw);
            var procser = new ProcedureSerializer(m.Procedure, json);
            procser.Serialize();
            sExpected = sExpected.Replace(" ", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "");
            var sActual = sb.Replace('\"', '\'').ToString();
            if (sExpected != sActual)
            {
                Console.WriteLine(sActual);
                Assert.AreEqual(sExpected, sActual);
            }
        }

        [Test]
        public void ProcSer_Assign()
        {
            var sExpected =
            #region Expected
                @"
{
  'addr':'00123400',
  'ids':[
    {'id':'r2','dt':'i32','st':['reg'{'n':'r2','off':0,'sz':32]
  ],
  'blocks':[
    {'id':'ProcedureBuilder_entry',
     'stm':[],
     'succ':['l1']},
    {'id':'l1',
     'stm':[
       ['a','r2',{'c':'i32','v':'42<i32>'}],
       ['r']
     ],
     'succ':['ProcedureBuilder_exit']},
    {'id':'ProcedureBuilder_exit',
     'stm':[],'succ':[]
    }
  ]
}";
            #endregion

            RunTest(sExpected, m =>
            {
                var r2 = new Identifier("r2", PrimitiveType.Int32, new RegisterStorage("r2", 2, 0, PrimitiveType.Word32));

                m.Assign(r2, m.Int32(42));
                m.Return();
            });
        }

        [Test]
        public void ProcSer_Assign_Twice()
        {
            var sExpected =
            #region Expected
                @"
{
    'addr':'00123400',
    'ids':[
        {'id':'r2','dt':'i32','st':['reg'{'n':'r2','off':0,'sz':32]],
    'blocks':[
        { 
            'id':'ProcedureBuilder_entry',
            'stm':[],
            'succ':['l1']
        },
        {
            'id':'l1',
            'stm':[
                ['a','r2',['+','r2',{ 'c':'i32','v':'42<i32>'},'w32']],
                ['r']
            ],
            'succ':['ProcedureBuilder_exit']},
        { 
            'id':'ProcedureBuilder_exit',
            'stm':[],
            'succ':[]
        }
    ]
}
";
                           #endregion

                    RunTest(sExpected, m =>
            {
                var r2 = new Identifier("r2", PrimitiveType.Int32, new RegisterStorage("r2", 2, 0, PrimitiveType.Word32));

                m.Assign(r2, m.IAdd(r2, m.Int32(42)));
                m.Return();
            });
        }
    }
}
