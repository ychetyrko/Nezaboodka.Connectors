using System;
using System.Reflection;

namespace Nezaboodka.Ndef
{
    public class ValueFormatter<T> : AbstractValueFormatter<T>
    {
        private Func<string, T> fParse;

        // Public

        public ValueFormatter() : this(typeof(T).FullName)
        {
        }

        public ValueFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
            Type t = typeof(T);
            if (t.IsEnum)
            {
                fParse = (Func<string, T>)delegate(string s) { return (T)Enum.Parse(t, s); };
            }
            else
            {
                MethodInfo m = t.GetMethod("Parse", new Type[] { typeof(string) });
                fParse = (Func<string, T>)Delegate.CreateDelegate(typeof(Func<string, T>), m, true);
            }
        }

        public override NdefValue ToNdefValue(Type formalType, T value)
        {
            return new NdefValue()
            {
                AsScalar = value.ToString(),
                HasNoLineFeeds = true
            };
        }

        public override T FromNdefValue(Type formalType, NdefValue value)
        {
            return fParse(value.AsScalar);
        }
    }
}
