using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Operators;
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

        public void Serialize(Expression e)
        {
            e.Accept(this);
        }

        public void VisitAddress(Address addr)
        {
            json.BeginList();
            json.WriteListItem("@");
            json.WriteListItem(addr.ToString());
            json.EndList();
        }

        public void VisitApplication(Application appl)
        {
            json.BeginList();
            json.WriteListItem("a");
            json.WriteListItem(() => appl.Procedure.Accept(this));
            json.WriteListItem(() => json.WriteList(appl.Arguments, this.Serialize));
            json.EndList();
        }

        public void VisitArrayAccess(ArrayAccess acc)
        {
            throw new NotImplementedException();
        }

        public void VisitBinaryExpression(BinaryExpression binExp)
        {
            var op = binExp.Operator switch
            {
                IAddOperator _ => "+",
                AndOperator _ => "&",
                ISubOperator _ => "-",
                OrOperator _ => "|",
                XorOperator _ => "|",
                EqOperator _ => "==",
                LeOperator _ => "<=",
                LtOperator _ => "<",
                GeOperator _ => ">=",
                GtOperator _ => ">",
                UleOperator _ => "<=u",
                UltOperator _ => "<u",
                UgeOperator _ => ">=u",
                UgtOperator _ => ">u",
                NeOperator _ => "!=",
                ShlOperator _ => "<<",
                SarOperator _ => ">>",
                ShrOperator _ => ">>u",
                SMulOperator _ => "*s",
                SDivOperator _ => "/s",
                UMulOperator _ => "*u",
                UDivOperator _ => "/u",
                IMulOperator _ => "*",
                IModOperator => "%",
                FAddOperator => "+f",
                FSubOperator => "-f",
                FMulOperator => "*f",
                FDivOperator => "/f",
                ReqOperator => "==f",
                RneOperator => "!=f",
                RgeOperator => ">=f",
                RgtOperator => ">f",
                RleOperator => "<=f",
                RltOperator => "<f",
                _ => throw new NotImplementedException($"{binExp.Operator} ({binExp.Operator.GetType().Name}): Unhandled binary operator.")
            };
            json.BeginList();
            json.WriteListItem(op);
            json.WriteListItem(() => binExp.Left.Accept(this));
            json.WriteListItem(() => binExp.Right.Accept(this));
            json.WriteListItem(() => tyrefSer.Serialize(binExp.DataType));
            json.EndList();
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
            json.BeginList();
            json.WriteListItem("cof");
            json.WriteListItem(() => cof.Expression.Accept(this));
            json.EndList();
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
            json.BeginList();
            json.WriteListItem("cv");
            json.WriteListItem(() => conversion.Expression.Accept(this));
            json.WriteListItem(() => tyrefSer.Serialize(conversion.SourceDataType));
            json.WriteListItem(() => tyrefSer.Serialize(conversion.DataType));
            json.EndList();
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
            json.Write(id.Name);
        }

        public void VisitMemberPointerSelector(MemberPointerSelector mps)
        {
            throw new NotImplementedException();
        }

        public void VisitMemoryAccess(MemoryAccess access)
        {
            json.BeginList();
            json.WriteListItem("m");
            json.WriteListItem(() => access.EffectiveAddress.Accept(this));
            json.WriteListItem(() => tyrefSer.Serialize(access.DataType));
            json.EndList();
        }

        public void VisitMkSequence(MkSequence seq)
        {
            json.BeginList();
            json.WriteListItem("q");
            foreach (var e in seq.Expressions)
            {
                json.WriteListItem(() => e.Accept(this));
            }
            json.EndList();
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
            switch (pc.Procedure)
            {
            case Procedure p:
                json.Write(p.Name);
                break;
            case ExternalProcedure e:
                json.BeginList();
                json.WriteListItem("ex");
                json.WriteListItem(e.Name);
                json.EndList();
                break;
            case IntrinsicProcedure i:
                json.BeginList();
                json.WriteListItem("i");
                json.WriteListItem(i.Name);
                json.EndList();
                break;
            default:
                throw new NotImplementedException();
            }
        }

        public void VisitScopeResolution(ScopeResolution scopeResolution)
        {
            throw new NotImplementedException();
        }


        public void VisitSegmentedAddress(SegmentedPointer segptr)
        {
            json.BeginList();
            json.WriteListItem(":");
            json.WriteListItem(() => segptr.BasePointer.Accept(this));
            json.WriteListItem(() => segptr.Offset.Accept(this));
            json.EndList();
        }

        public void VisitSlice(Slice slice)
        {
            json.BeginList();
            json.WriteListItem("l");
            json.WriteListItem(() => slice.Expression.Accept(this));
            json.WriteListItem(() => tyrefSer.Serialize(slice.DataType));
            json.WriteListItem(slice.Offset);
            json.EndList();
        }

        public void VisitStringConstant(StringConstant str)
        {
            throw new NotImplementedException();
        }

        public void VisitTestCondition(TestCondition tc)
        {
            json.BeginList();
            json.WriteListItem("test");
            json.WriteListItem(tc.ConditionCode.ToString());
            json.WriteListItem(() => tc.Expression.Accept(this));
            json.EndList();
        }

        public void VisitUnaryExpression(UnaryExpression unary)
        {
            var op = unary.Operator.Type switch
            {
                OperatorType.Comp => "~",
                OperatorType.Neg => "-",
                OperatorType.Not => "!",
                _ => throw new NotImplementedException($"{unary}: unimplemented")
            };
            json.BeginList();
            json.WriteListItem("u");
            json.WriteListItem(op);
            json.WriteListItem(() => unary.Expression.Accept(this));
            json.EndList();
        }
    }
}
