using System;

namespace Nezaboodka.Ndef
{
    public class Int8Formatter : AbstractValueFormatter<sbyte>
    {
        public const sbyte NullValue = sbyte.MinValue;

        public Int8Formatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, sbyte value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override sbyte FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return sbyte.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }

    public class Int16Formatter : AbstractValueFormatter<short>
    {
        public const short NullValue = short.MinValue;

        public Int16Formatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, short value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override short FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return short.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }

    public class Int32Formatter : AbstractValueFormatter<int>
    {
        public const int NullValue = int.MinValue;

        public Int32Formatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, int value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override int FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return int.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }

    public class Int64Formatter : AbstractValueFormatter<long>
    {
        public const long NullValue = long.MinValue;

        public Int64Formatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, long value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override long FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return long.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }
}
