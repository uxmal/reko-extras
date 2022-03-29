using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Database
{
    public class ExpressionSerializer : IExpressionVisitor
    {
        private JsonWriter json;
        private TypeReferenceSerializer tyrefSer;

        public ExpressionSerializer(TypeReferenceSerializer tyrefSer, JsonWriter json)
        {
            this.tyrefSer = tyrefSer;
            this.json = json;
        }

        public void VisitAddress(Address addr)
        {
            throw new NotImplementedException();
        }

        public void VisitApplication(Application appl)
        {
            throw new NotImplementedException();
        }

        public void VisitArrayAccess(ArrayAccess acc)
        {
            throw new NotImplementedException();
        }

        public void VisitBinaryExpression(BinaryExpression binExp)
        {
            throw new NotImplementedException();
        }

        public void VisitCast(Cast cast)
        {
            throw new NotImplementedException();
        }

        public void VisitConditionalExpression(ConditionalExpression cond)
        {
            throw new NotImplementedException();
        }

        public void VisitConditionOf(ConditionOf cof)
        {
            throw new NotImplementedException();
        }

        public void VisitConstant(Constant c)
        {
            json.BeginObject();
            json.WriteKeyValue("c", () => tyrefSer.Serialize(c.DataType));
            json.WriteKeyValue("v", c.ToString());
            json.EndObject();
        }

        public void VisitConversion(Conversion conversion)
        {
            throw new NotImplementedException();
        }

        public void VisitDereference(Dereference deref)
        {
            throw new NotImplementedException();
        }

        public void VisitFieldAccess(FieldAccess acc)
        {
            throw new NotImplementedException();
        }

        public void VisitIdentifier(Identifier id)
        {
            throw new NotImplementedException();
        }

        public void VisitMemberPointerSelector(MemberPointerSelector mps)
        {
            throw new NotImplementedException();
        }

        public void VisitMemoryAccess(MemoryAccess access)
        {
            throw new NotImplementedException();
        }

        public void VisitMkSequence(MkSequence seq)
        {
            throw new NotImplementedException();
        }

        public void VisitOutArgument(OutArgument outArgument)
        {
            throw new NotImplementedException();
        }

        public void VisitPhiFunction(PhiFunction phi)
        {
            throw new NotImplementedException();
        }

        public void VisitPointerAddition(PointerAddition pa)
        {
            throw new NotImplementedException();
        }

        public void VisitProcedureConstant(ProcedureConstant pc)
        {
            throw new NotImplementedException();
        }

        public void VisitScopeResolution(ScopeResolution scopeResolution)
        {
            throw new NotImplementedException();
        }

        public void VisitSegmentedAccess(SegmentedAccess access)
        {
            throw new NotImplementedException();
        }

        public void VisitSlice(Slice slice)
        {
            throw new NotImplementedException();
        }

        public void VisitTestCondition(TestCondition tc)
        {
            throw new NotImplementedException();
        }

        public void VisitUnaryExpression(UnaryExpression unary)
        {
            throw new NotImplementedException();
        }
    }
}
