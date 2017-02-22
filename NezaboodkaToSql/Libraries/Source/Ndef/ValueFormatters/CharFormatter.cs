using System;

namespace Nezaboodka.Ndef
{
    public class CharFormatter : AbstractValueFormatter<char>
    {
        public const char NullValue = char.MaxValue;

        public CharFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, char value)
        {
            if (value != NullValue)
                return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
            else
                return NdefValue.NullValue;
        }

        public override char FromNdefValue(Type formalType, NdefValue value)
        {
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                return char.Parse(value.AsScalar);
            else
                return NullValue;
        }
    }
}
