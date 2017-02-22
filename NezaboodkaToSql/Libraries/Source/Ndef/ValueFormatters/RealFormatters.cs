using System;

namespace Nezaboodka.Ndef
{
    public class SingleFormatter : AbstractValueFormatter<float>
    {
        public const float NullValue = float.MinValue;

        public SingleFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, float value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override float FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return float.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }

    public class DoubleFormatter : AbstractValueFormatter<double>
    {
        public const double NullValue = double.MinValue;

        public DoubleFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, double value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override double FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return double.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }

    public class DecimalFormatter : AbstractValueFormatter<decimal>
    {
        public const decimal NullValue = decimal.MinValue;

        public DecimalFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, decimal value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override decimal FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return decimal.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }
}
