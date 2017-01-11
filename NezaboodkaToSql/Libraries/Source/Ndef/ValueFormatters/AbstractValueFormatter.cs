using System;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public abstract class AbstractValueFormatter<T> : AbstractFormatter<T>
    {
        public override void FromNdefElements(T obj, IEnumerable<NdefElement> elements)
        {
            throw new InvalidOperationException("cannot serialize scalar value as an object");
        }

        public override IEnumerable<NdefElement> ToNdefElements(T obj, int[] fieldNumbers)
        {
            throw new InvalidOperationException("cannot deserialize scalar value as an object");
        }
    }
}
