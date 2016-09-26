using System;

namespace Nezaboodka.Ndef
{
    public class StringFormatter : AbstractValueFormatter<string>
    {
        public override NdefValue ToNdefValue(Type formalType, string value)
        {
            if (value != null)
                return new NdefValue() { AsScalar = value };
            else
                return NdefValue.NullValue;
        }

        public override string FromNdefValue(Type formalType, NdefValue value)
        {
            return value.AsScalar;
        }
    }
}
