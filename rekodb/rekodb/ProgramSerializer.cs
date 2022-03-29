using Reko.Core;
using Reko.Core.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Database
{
    public class ProgramSerializer
    {
        private JsonWriter json;

        public ProgramSerializer(JsonWriter json)
        {
            this.json = json;
        }

        public void Serialize(Program program)
        {
            json.BeginObject();
            json.WriteKeyValue("procs", () => SerializeProcedures(program));
            json.EndObject();
        }

        private void SerializeProcedures(Program program)
        {
            json.WriteList(program.Procedures.Values, SerializeProcedure);
        }

        private void SerializeProcedure(Procedure proc)
        {
            new ProcedureSerializer(proc, json).Serialize();
        }
    }
}
