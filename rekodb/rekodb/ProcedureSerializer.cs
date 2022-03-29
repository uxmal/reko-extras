using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Database
{
    public class ProcedureSerializer
    {
        private JsonWriter json;
        private TypeReferenceSerializer typerefSer;
        private ExpressionSerializer expSer;

        public ProcedureSerializer(JsonWriter json)
        {
            this.json = json;
            this.typerefSer = new TypeReferenceSerializer(json);
            this.expSer = new ExpressionSerializer(typerefSer, json);
        }

        public void Serialize(Procedure procedure)
        {
            json.BeginObject();
            json.WriteKeyValue("addr", procedure.EntryAddress.ToString());
            json.WriteKeyValue("ids", () => SerializeIdentifiers(procedure));
            json.WriteKeyValue("blocks", () => SerializeBlocks(procedure));
            json.EndObject();
        }

        private void SerializeBlocks(Procedure proc)
        {
            json.WriteList(proc.SortBlocksByName(), block =>
            {
                json.BeginObject();
                json.WriteKeyValue("id", block.Id);
                json.WriteKeyValue("stm", () =>
                {
                    json.WriteList(block.Statements, SerializeStatement);
                });
                json.WriteKeyValue("succ", () =>
                {
                    json.WriteList(block.Succ, s => json.Write(s.Id));
                });
                json.EndObject();
            });
        }

        private void SerializeStatement(Statement stm)
        {
            switch (stm.Instruction)
            {
            case Assignment ass:
                json.BeginObject();
                json.WriteKeyValue("a", ass.Dst.Name);
                json.WriteKeyValue("s", () => ass.Src.Accept(expSer));
                json.EndObject();
                break;
            case ReturnInstruction ret:
                json.BeginObject();
                json.WriteKeyValue("r", () =>
                {
                    if (ret.Expression is null)
                        json.Write((object)null!);
                    else
                        ret.Expression.Accept(expSer);
                });
                json.EndObject();
                break;
            default: throw new NotImplementedException(stm.Instruction.GetType().Name);
            }
        }

        private void SerializeIdentifiers(Procedure proc)
        {
            json.WriteList(
                CollectIdentifiers(proc)
                    .OrderBy(i => i.Name)
                    .ThenBy(i => i.Storage.Domain),
                id =>
                {
                    json.BeginObject();
                    json.WriteKeyValue("id", id.Name);
                    json.WriteKeyValue("dt", () => this.typerefSer.Serialize(id.DataType));
                    json.WriteKeyValue("st", () => SerializeStorage(id.Storage));
                });
        }

        private List<Identifier> CollectIdentifiers(Procedure proc)
        {
            var ids = new List<Identifier>();
            var idCollector = new IdCollector(ids);
            foreach (var stm in proc.Statements)
            {
                stm.Instruction.Accept(idCollector);
            }
            return ids;
        }

        private void SerializeStorage(Storage stg)
        {
            json.BeginObject();
            switch (stg)
            {
            case RegisterStorage reg:
                json.WriteKeyValue("reg", reg.Name);
                json.WriteKeyValue("off", reg.BitAddress);
                json.WriteKeyValue("sz", reg.BitSize);
                break;
            default: throw new NotImplementedException();
            }
            json.EndObject();
        }

        private class IdCollector : InstructionVisitorBase
        {
            private List<Identifier> ids;

            public IdCollector(List<Identifier> ids)
            {
                this.ids = ids;
            }

            public override void VisitIdentifier(Identifier id)
            {
                ids.Add(id);
            }
        }
    }
}
