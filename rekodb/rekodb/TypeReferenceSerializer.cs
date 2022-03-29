using Reko.Core.Serialization.Json;
using Reko.Core.Types;
using System;

namespace Reko.Database
{
    public class TypeReferenceSerializer : IDataTypeVisitor
    {
        private JsonWriter json;

        public TypeReferenceSerializer(JsonWriter json)
        {
            this.json = json;
        }

        public void Serialize(DataType dt)
        {
            dt.Accept(this);
        }

        public void VisitArray(ArrayType at)
        {
            throw new NotImplementedException();
        }

        public void VisitClass(ClassType ct)
        {
            throw new NotImplementedException();
        }

        public void VisitCode(CodeType c)
        {
            throw new NotImplementedException();
        }

        public void VisitEnum(EnumType e)
        {
            throw new NotImplementedException();
        }

        public void VisitEquivalenceClass(EquivalenceClass eq)
        {
            throw new NotImplementedException();
        }

        public void VisitFunctionType(FunctionType ft)
        {
            throw new NotImplementedException();
        }

        public void VisitMemberPointer(MemberPointer memptr)
        {
            throw new NotImplementedException();
        }

        public void VisitPointer(Pointer ptr)
        {
            json.BeginObject();
            json.WriteKeyValue($"p{ptr.BitSize}", () => ptr.Pointee.Accept(this));
            json.EndObject();
        }

        public void VisitPrimitive(PrimitiveType pt)
        {
            char c;
            if (pt.IsWord)
            {
                c = 'w';
            }
            else
            {
                c = pt.Domain switch
                {
                    Domain.Boolean => 'b',
                    Domain.Character => 'c',
                    Domain.Real => 'r',
                    Domain.SignedInt => 'i',
                    Domain.UnsignedInt => 'u',
                    _ => throw new NotImplementedException()
                };
            }
            json.Write($"{c}{pt.BitSize}");
        }

        public void VisitReference(ReferenceTo refTo)
        {
            throw new NotImplementedException();
        }

        public void VisitString(StringType str)
        {
            throw new NotImplementedException();
        }

        public void VisitStructure(StructureType str)
        {
            throw new NotImplementedException();
        }

        public void VisitTypeReference(TypeReference typeref)
        {
            throw new NotImplementedException();
        }

        public void VisitTypeVariable(TypeVariable tv)
        {
            throw new NotImplementedException();
        }

        public void VisitUnion(UnionType ut)
        {
            throw new NotImplementedException();
        }

        public void VisitUnknownType(UnknownType ut)
        {
            throw new NotImplementedException();
        }

        public void VisitVoidType(VoidType voidType)
        {
            throw new NotImplementedException();
        }

    }
}