using System;
using System.Text;
using System.Collections;

namespace Nezaboodka.Ndef
{
    public class BitArrayFormatter : AbstractValueFormatter<BitArray>
    {
        private static readonly char[] DelimiterBetweenLengthAndData = new char[] { ':' };

        public override NdefValue ToNdefValue(Type formalType, BitArray value)
        {
            NdefValue result;
            if (value != null)
            {
                byte[] data = new byte[(value.Length - 1) / 8 + 1];
                value.CopyTo(data, 0);
                var sb = new StringBuilder(3);
                sb.Append(value.Length);
                sb.Append(DelimiterBetweenLengthAndData[0]);
                sb.Append(Convert.ToBase64String(data));
                result = new NdefValue() { AsScalar = sb.ToString() };
            }
            else
                result = NdefValue.NullValue;
            return result;
        }

        public override BitArray FromNdefValue(Type formalType, NdefValue value)
        {
            BitArray result = null;
            if (!value.IsNull)
            {
                bool error = false;
                string[] t = value.AsScalar.Split(DelimiterBetweenLengthAndData);
                if (t.Length == 2)
                {
                    int length;
                    if (int.TryParse(t[0], out length) && !string.IsNullOrWhiteSpace(t[1]) && t[1].Length >= 1)
                    {
                        byte[] data = Convert.FromBase64String(t[1]);
                        result = new BitArray(data);
                        result.Length = length;
                    }
                    else
                        error = true;
                }
                else
                    error = true;
                if (error)
                    throw new FormatException(string.Format("wrong format of encoded bit array: {0}", value.AsScalar));
            }
            return result;
        }
    }
}
