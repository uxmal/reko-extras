using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Database
{
    public class ProcedureDeserializer : AbstractDeserializer
    {
        private TypeReferenceDeserializer td;

        public ProcedureDeserializer(JsonReader rdr) : base(rdr)
        {
            this.td = new TypeReferenceDeserializer(rdr);
        }


        public Procedure Deserialize(IProcessorArchitecture arch)
        {
            string? sAddr = null;
            Dictionary<string, Identifier>? ids = null;
            Dictionary<string, List<string>>? succ = null;
            Dictionary<string, Block>? blocks = null;
            Expect(JsonToken.BeginObject);
            while (!PeekAndDiscard(JsonToken.EndObject))
            {
                Expect(JsonToken.PropertyName);
                switch (rdr.GetString())
                {
                case "addr":
                    Expect(JsonToken.String);
                    sAddr = rdr.GetString();
                    break;
                case "ids":
                    ids = DeserializeIds();
                    break;
                case "blocks":
                    if (!arch.TryParseAddress(sAddr, out Address addr))
                        throw new BadImageFormatException();
                    var proc = Procedure.Create(arch, addr, arch.CreateFrame());
                    blocks = DeserializeBlocks(proc, ids);
                    break;
                case "succ":
                    succ = DeserializeSuccessors();
                    break;
                }
            }
            return BuildProcedure(sAddr, ids, blocks, succ);
        }

        private Dictionary<string, List<string>>? DeserializeSuccessors()
        {
            throw new NotImplementedException();
        }

        private Procedure BuildProcedure(
            string? sAddr,
            Dictionary<string,Identifier>? ids,
            Dictionary<string, Block>? blocks,
            Dictionary<string, List<string>>? succs)
        {
            throw new NotImplementedException();
        }

        private Dictionary<string, Identifier> DeserializeIds()
        {
            var result = new Dictionary<string,Identifier>();
            Expect(JsonToken.BeginList);
            while (!PeekAndDiscard(JsonToken.EndList))
            {
                var id = DeserializeId();
                result.Add(id.Name, id);
            }
            return result;
        }

        private Identifier DeserializeId()
        {
            string? name = null;
            DataType? dt = null;
            Storage? stg = null;
            Expect(JsonToken.BeginObject);
            while (!PeekAndDiscard(JsonToken.EndObject))
            {
                Expect(JsonToken.PropertyName);
                switch (rdr.GetString())
                {
                case "id":
                    Expect(JsonToken.String);
                    name = rdr.GetString();
                    break;
                case "dt":
                    dt = td.Deserialize();
                    break;
                case "st":
                    stg = DeserializeStorage();
                    break;
                }
            }
            if (name is null)
                throw new BadImageFormatException("Identifier needs a name.");
            if (dt is null)
                throw new BadImageFormatException("Identifier needs a data type.");
            if (stg is null)
                throw new BadImageFormatException("Identifier needs a storage.");
            return new Identifier(name, dt, stg);
        }

        public Storage DeserializeStorage()
        {
            //['reg',{'n':'r2','off':0,'dt':'w32'}]
            Expect(JsonToken.BeginList);
            {
                Expect(JsonToken.String);
                switch (rdr.GetString())
                {
                case "reg":
                    Expect(JsonToken.BeginObject);
                    Expect(JsonToken.PropertyName);
                    Expect(JsonToken.String);
                    var n = rdr.GetString();

                    Expect(JsonToken.PropertyName);
                    Expect(JsonToken.Number);
                    if (!rdr.TryGetInt32(out int offset))
                        throw new BadImageFormatException();

                    Expect(JsonToken.PropertyName);
                    var dt = td.Deserialize();
                    Expect(JsonToken.EndObject);
                    Expect(JsonToken.EndList);
                    return new RegisterStorage(n, -1, (uint)offset, (PrimitiveType) dt);
                }
                throw new NotImplementedException();
            }
            throw new NotImplementedException();
        }

        private Dictionary<string, Block> DeserializeBlocks(Procedure proc, Dictionary<string,Identifier>? ids)
        {
            var result = new Dictionary<string, Block>();
            Expect(JsonToken.BeginList);
            while (!PeekAndDiscard(JsonToken.EndList))
            {
                var block = DeserializeBlock(proc);
                result.Add(block.Id, block);
            }
            return result;
        }

        private Block DeserializeBlock(Procedure proc)
        {
            Expect(JsonToken.BeginList);

            Expect(JsonToken.String);
            var id = rdr.GetString();
            Expect(JsonToken.String);
            var sAddr = rdr.GetString();
            if (!proc.Architecture.TryParseAddress(sAddr, out var addr))
                throw new BadImageFormatException();
            var block = new Block(proc, addr, id);

            Expect(JsonToken.EndList);
            return block;
        }
    }
}