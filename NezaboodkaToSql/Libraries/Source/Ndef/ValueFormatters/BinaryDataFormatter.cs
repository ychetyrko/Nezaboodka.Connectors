using System;
using System.Text;

namespace Nezaboodka.Ndef
{
    public class BinaryDataFormatter : AbstractValueFormatter<byte[]>
    {
        public override NdefValue ToNdefValue(Type formalType, byte[] value)
        {
            NdefValue result;
            if (value != null)
                result = new NdefValue() { AsScalar = Convert.ToBase64String(value) };
            else
                result = NdefValue.NullValue;
            return result;
        }

        public override byte[] FromNdefValue(Type formalType, NdefValue value)
        {
            byte[] result;
            if (!value.IsNull)
                result = Convert.FromBase64String(value.AsScalar);
            else
                result = null;
            return result;
        }
    }
}
