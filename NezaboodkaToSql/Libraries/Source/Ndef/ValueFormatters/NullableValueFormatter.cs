using System;
using System.Reflection;

namespace Nezaboodka.Ndef
{
    public class NullableValueFormatter<T> : AbstractValueFormatter<T?> where T : struct
    {
        // Fields
        private Func<string, T> fParse;

        public NullableValueFormatter() : this(null)
        {
        }

        public NullableValueFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
            Type t = typeof(T);
            MethodInfo m = t.GetMethod("Parse", new Type[] { typeof(string) });
            fParse = (Func<string, T>)Delegate.CreateDelegate(typeof(Func<string, T>), m, true);
        }

        public override NdefValue ToNdefValue(Type formalType, T? value)
        {
            return new NdefValue()
            {
                AsScalar = value.ToString(),
                HasNoLineFeeds = true
            };
        }

        public override T? FromNdefValue(Type formalType, NdefValue value)
        {
            T? result = null;
            if (!string.IsNullOrWhiteSpace(value.AsScalar))
                result = fParse(value.AsScalar);
            return result;
        }
    }
}
