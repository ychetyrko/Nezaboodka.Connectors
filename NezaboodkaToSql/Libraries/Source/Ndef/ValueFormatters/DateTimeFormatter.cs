using System;
using System.Globalization;

namespace Nezaboodka.Ndef
{
    public class DateTimeFormatter : AbstractValueFormatter<DateTime>
    {
        public DateTimeFormatter() : this(null)
        {
        }

        public DateTimeFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override NdefValue ToNdefValue(Type formalType, DateTime value)
        {
            NdefValue result;
            if (value == default(DateTime))
                result = NdefValue.NullValue;
            else
                result = new NdefValue() { AsScalar = value.ToString("o"), HasNoLineFeeds = true };
            return result;
        }

        public override DateTime FromNdefValue(Type formalType, NdefValue value)
        {
            DateTime result;
            if (value.AsScalar == null)
                result = default(DateTime);
            else
                result = DateTime.Parse(value.AsScalar, null, DateTimeStyles.AdjustToUniversal);
            return result;
        }
    }
}
