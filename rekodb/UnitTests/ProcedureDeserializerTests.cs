using NUnit.Framework;
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
    public class ProcedureDeserializerTests
    {
        private readonly FakeArchitecture arch;

        public ProcedureDeserializerTests()
        {
            this.arch = new Mocks.FakeArchitecture();
        }

        private void RunTest(string sExpected, string sInput)
        {
            var abInput = Encoding.UTF8.GetBytes(sInput.Replace("\'", "\""));
            var rdr = new JsonReader(new MemoryStream(abInput));
            var deser = new ProcedureDeserializer(rdr);
            var proc = deser.Deserialize(arch);

            var sw = new StringWriter();
            proc.Write(false, sw);
            Assert.AreEqual(sExpected, sw.ToString());
        }

        [Test]
        [Ignore("Fix rendering of RegisterStorage")]
        public void ProcDes_Simple()
        {
            var sInput=
            #region Input
                @"
{
    'addr':'00123400',
    'ids':[
        {'id':'r2','dt':'i32','st':['reg',{'n':'r2','off':0,'dt':'w32'}]}],
    'blocks':[
        { 
            'id':'ProcedureBuilder_entry',
            'a':'00123400',
            'stm':[]
        },
        {
            'id':'l1',
            'a':'00123400',
            'stm':[
                ['a','r2',['+','r2',{ 'c':'i32','v':'42<i32>'},'w32']],
                ['r']
            ]
        },
        { 
            'id':'ProcedureBuilder_exit',
            'a':'00123400',
            'stm':[]
        }
    ],
    'succ':{
        'ProcedureBuilder_entry':['l1'],
        'l1':['ProcedureBuilder_exit'],
        'ProcedureBuilder_exit':[]
    }
}
";
            #endregion

            var sExpected =
            #region Expected
                "@@@";
            #endregion

            RunTest(sExpected, sInput);
        }
    }
}
