using System;

namespace Nezaboodka.Ndef
{
    public class UInt8Formatter : AbstractValueFormatter<byte>
    {
        public const byte NullValue = byte.MaxValue;

        public UInt8Formatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, byte value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override byte FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return byte.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }

    public class UInt16Formatter : AbstractValueFormatter<ushort>
    {
        public const ushort NullValue = ushort.MaxValue;

        public UInt16Formatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, ushort value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override ushort FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return ushort.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }

    public class UInt32Formatter : AbstractValueFormatter<uint>
    {
        public const uint NullValue = uint.MaxValue;

        public UInt32Formatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, uint value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override uint FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return uint.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }

    public class UInt64Formatter : AbstractValueFormatter<ulong>
    {
        public const ulong NullValue = ulong.MaxValue;

        public UInt64Formatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, ulong value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override ulong FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return ulong.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }
}
