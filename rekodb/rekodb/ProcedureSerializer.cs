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
        private Procedure proc;
        private JsonWriter json;
        private TypeReferenceSerializer typerefSer;
        private ExpressionSerializer expSer;

        public ProcedureSerializer(Procedure proc, JsonWriter json)
        {
            this.proc = proc;
            this.json = json;
            this.typerefSer = new TypeReferenceSerializer(json);
            this.expSer = new ExpressionSerializer(typerefSer, json);
        }

        public void Serialize()
        {
            json.BeginObject();
            json.WriteKeyValue("addr", proc.EntryAddress.ToString());
            json.WriteKeyValue("ids", () => SerializeIdentifiers(proc));
            json.WriteKeyValue("blocks", () => SerializeBlocks(proc));
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
                json.BeginList();
                json.WriteListItem("a");
                json.WriteListItem(ass.Dst.Name);
                json.WriteListItem(() => ass.Src.Accept(expSer));
                json.EndList();
                break;
            case Store store:
                json.BeginList();
                json.WriteListItem("s");
                json.WriteListItem(() => store.Dst.Accept(expSer));
                json.WriteListItem(() => store.Src.Accept(expSer));
                json.EndList();
                break;
            case Branch branch:
                json.BeginList();
                json.WriteListItem("if");
                json.WriteListItem(() => branch.Condition.Accept(this.expSer));
                json.WriteListItem(branch.Target.Id);
                json.EndList();
                break;
            case CallInstruction call:
                json.BeginList();
                json.WriteListItem("c");
                json.WriteListItem(() => call.Callee.Accept(this.expSer));
                json.BeginObject();
                json.WriteKeyValue("site", () => SerializeCallSite(call.CallSite));
                json.WriteKeyValue("u", () => json.WriteList(call.Uses, SerializeCallBinding));
                json.WriteKeyValue("d", () => json.WriteList(call.Definitions, SerializeCallBinding));
                json.EndObject();
                json.EndList();
                break;
            case ReturnInstruction ret:
                json.BeginList();
                json.WriteListItem("r");
                if (ret.Expression is not null)
                    ret.Expression.Accept(expSer);
                json.EndList();
                break;
            case SideEffect side:
                json.BeginList();
                json.WriteListItem("se");
                side.Expression.Accept(expSer);
                json.EndList();
                break;
            case SwitchInstruction sw:
                json.BeginList();
                json.WriteListItem("sw");
                json.WriteListItem(() => expSer.Serialize(sw.Expression));
                json.WriteList(sw.Targets, t => json.Write(t.Id));
                json.EndList();
                break;
            default: throw new NotImplementedException(stm.Instruction.GetType().Name);
            }
        }

        private void SerializeCallBinding(CallBinding binding)
        {
            json.BeginList();
            json.WriteListItem(() => SerializeStorage(binding.Storage));
            json.WriteListItem(() => binding.Expression.Accept(expSer));
            json.EndList();
        }

        private void SerializeCallSite(CallSite callSite)
        {
            json.BeginList();
            json.WriteListItem(callSite.StackDepthOnEntry);
            json.WriteListItem(callSite.FpuStackDepthBefore);
            json.WriteListItem(callSite.SizeOfReturnAddressOnStack);
            json.EndList();
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

        private HashSet<Identifier> CollectIdentifiers(Procedure proc)
        {
            var ids = new HashSet<Identifier>();
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
            case FlagGroupStorage grf:
                json.WriteKeyValue("grf", grf.Name);
                json.WriteKeyValue("off", grf.FlagGroupBits);
                json.WriteKeyValue("freg", grf.FlagRegister.Name);
                break;
            case SequenceStorage seq:
                json.WriteKeyValue("seq", seq.Name);
                json.WriteKeyValue("sz", seq.BitSize);
                json.WriteKeyValue("el", () => json.WriteList(seq.Elements, SerializeStorage));
                break;
            case TemporaryStorage tmp:
                if (tmp == proc.Frame.FramePointer.Storage)
                {
                    json.WriteKeyValue("frp", true);
                }
                else
                {
                    json.WriteKeyValue("tmp", tmp.Name);
                    json.WriteKeyValue("sz", tmp.BitSize);
                }
                break;
            case MemoryStorage mem:
                json.WriteKeyValue("mem", true);
                break;
            default: throw new NotImplementedException(stg.GetType().Name);
            }
            json.EndObject();
        }

        private class IdCollector : InstructionVisitorBase
        {
            private HashSet<Identifier> ids;

            public IdCollector(HashSet<Identifier> ids)
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
