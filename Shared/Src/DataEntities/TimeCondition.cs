using System;
using ProtoBuf;

namespace VirtualSpace.Shared
{
    [ProtoContract]
    public enum BoundType
    {
        [ProtoEnum]
        Equal,
        [ProtoEnum]
        SmallerEqual,
        [ProtoEnum]
        NotEqual
    }

    [ProtoContract]
    public class Bound : TimeCondition
    {
        [ProtoMember(1)]
        public Value Left;
        [ProtoMember(2)]
        public BoundType BoundType;
        [ProtoMember(3)]
        public Value Right;

        private Bound() { }

        public Bound(Value left, BoundType type, Value right)
        {
            Left = left;
            BoundType = type;
            Right = right;
        }

        public static string BoundToString(BoundType type)
        {
            string bound = "";

            switch (type)
            {
                case BoundType.Equal:
                    bound = "==";
                    break;
                case BoundType.SmallerEqual:
                    bound = "<=";
                    break;
                case BoundType.NotEqual:
                    bound = "!=";
                    break;
            }

            return bound;
        }

        public override string ToString()
        {
            return Left.ToString() + " " + BoundToString(BoundType) + " " + Right.ToString();
        }
    }

    [ProtoContract]
    public enum OperationType
    {
        [ProtoEnum]
        Plus,
        [ProtoEnum]
        Minus,
        [ProtoEnum]
        Multiply
    }

    [ProtoContract]
    public class Expression : Value
    {
        [ProtoMember(1)]
        public Value Left;
        [ProtoMember(2)]
        public OperationType OperationType;
        [ProtoMember(3)]
        public Value Right;

        private Expression() { }

        public Expression(Value left, OperationType type, Value right)
        {
            Left = left;
            OperationType = type;
            Right = right;
        }

        public static string BoundToString(OperationType type)
        {
            string operation = "";

            switch (type)
            {
                case OperationType.Minus:
                    operation = "-";
                    break;
                case OperationType.Multiply:
                    operation = "*";
                    break;
                case OperationType.Plus:
                    operation = "+";
                    break;
            }

            return operation;
        }

        public override string ToString()
        {
            return "(" + Left.ToString() + " " + BoundToString(OperationType) + " " + Right.ToString() + ")";
        }
    }

    [ProtoContract]
    public enum VariableTypes {
        [ProtoEnum]
        Integer,
        [ProtoEnum]
        Continuous,
        [ProtoEnum]
        PreperationTime,
        [ProtoEnum]
        ExecutionTime,
        [ProtoEnum]
        ArrivalTime,
        [ProtoEnum]
        CalculationTime
    }

    [ProtoContract]
    public class Variable : Value
    {
        private static int _nextVariableId;
        [ProtoMember(1)]
        public int VariableId;
        [ProtoMember(2)]
        public VariableTypes Type;
        [ProtoMember(3)]
        public string Name;

        private Variable() { }

        public Variable(VariableTypes type, string name="")
        {
            Type = type;

            switch (type)
            {
                case VariableTypes.Integer:
                case VariableTypes.Continuous:
                    VariableId = _nextVariableId++;
                    break;
                case VariableTypes.PreperationTime:
                case VariableTypes.ExecutionTime:
                case VariableTypes.CalculationTime:
                case VariableTypes.ArrivalTime:
                    VariableId = (int)type;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(type.ToString(), type, null);
            }

            Name = name;
        }

        public override string ToString()
        {
            return Name + "(" + Type.ToString() + ")";
        }
    }

    [ProtoContract]
    public class Constant : Value
    {
        [ProtoMember(1)]
        public double Value;

        private Constant() { }

        public Constant(double value)
        {
            Value = value;
        }

        public static implicit operator double(Constant c) {  return c.Value; }

        public static implicit operator Constant(double d) {  return new Constant(d); }

        public override string ToString()
        {
            return String.Format("{0:0.00}", Value);
        }
    }

    [ProtoInclude(501, typeof(Constant))]
    [ProtoInclude(502, typeof(Variable))]
    [ProtoInclude(503, typeof(Expression))]
    [ProtoContract]
#pragma warning disable CS0660 // unimportant: overwriting == but not Equals/GetHashcode
#pragma warning disable CS0661
    public abstract class Value
#pragma warning restore CS0661
#pragma warning restore CS0660
    {
        public static implicit operator Value(double d)
        {
            return new Constant(d);
        }

        public static bool IsDefined(Value value)
        {
            return !ReferenceEquals(null, value);
        }

        public static Bound operator <=(Value v1, Value v2) {  return new Bound(v1, BoundType.SmallerEqual, v2); }
        public static Bound operator >=(Value v1, Value v2) {  return v2 <= v1; }
        public static Bound operator ==(Value v1, Value v2) {  return new Bound(v1, BoundType.Equal, v2); }
        public static Bound operator !=(Value v1, Value v2) {  return new Bound(v1, BoundType.NotEqual, v2); }
        public static Value operator +(Value v1, Value v2) {  return new Expression(v1, OperationType.Plus, v2); }
        public static Value operator -(Value v1, Value v2) {  return new Expression(v1, OperationType.Minus, v2); }
        public static Value operator *(Constant c, Value v)
        {
            if (v is Constant)
            {
                Constant cOther = (Constant) v;
                return new Constant(c.Value * cOther.Value);
            }
            if (v is Variable)
                return new Expression(c, OperationType.Multiply, v);
            if (v is Expression)
            {
                Expression e = (Expression) v;
                return new Expression(e.Left * c, e.OperationType, e.Right * c);
            }
            throw new ArgumentException();
        }
        public static Value operator *(Value v, Constant c)
        {
            return c * v;
        }

        public override abstract string ToString();
    }

    [ProtoInclude(501, typeof(Bound))]
    [ProtoContract]
    public abstract class TimeCondition
    {
        public override abstract string ToString();
    }
}