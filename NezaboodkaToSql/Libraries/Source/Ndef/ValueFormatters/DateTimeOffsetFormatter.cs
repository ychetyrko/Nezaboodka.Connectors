using System;
using System.Globalization;

namespace Nezaboodka.Ndef
{
    public class DateTimeOffsetFormatter : AbstractValueFormatter<DateTimeOffset>
    {
        public DateTimeOffsetFormatter() : this(null)
        {
        }

        public DateTimeOffsetFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, DateTimeOffset value)
        {
            NdefValue result;
            if (value == default(DateTimeOffset))
                result = NdefValue.NullValue;
            else
                result = new NdefValue() { AsScalar = value.ToString("o") };
            return result;
        }

        public override DateTimeOffset FromNdefValue(Type formalType, NdefValue value)
        {
            DateTimeOffset result;
            if (value.AsScalar == null)
                result = default(DateTimeOffset);
            else
                result = DateTimeOffset.Parse(value.AsScalar, null, DateTimeStyles.AssumeUniversal);
            return result;
        }
    }
}
