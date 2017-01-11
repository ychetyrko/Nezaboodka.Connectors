using System;

namespace Nezaboodka.Ndef
{
    public class BinarySegmentFormatter : AbstractValueFormatter<ArraySegment<byte>>
    {
        public override NdefValue ToNdefValue(Type formalType, ArraySegment<byte> value)
        {
            var result = new NdefValue() { AsScalar = Convert.ToBase64String(value.Array, value.Offset, value.Count), HasNoLineFeeds = true };
            return result;
        }

        public override ArraySegment<byte> FromNdefValue(Type formalType, NdefValue value)
        {
            var result = new ArraySegment<byte>(Convert.FromBase64String(value.AsScalar));
            return result;
        }
    }
}
