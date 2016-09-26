using System;

namespace Nezaboodka.Ndef
{
    public abstract class AbstractValueFormatter<T> : INdefValueFormatter<T>
    {
        public Type TypeOfValue { get { return typeof(T); } }
        public string SerializableTypeName { get; protected set; }

        public abstract NdefValue ToNdefValue(Type formalType, T value);
        public abstract T FromNdefValue(Type formalType, NdefValue value);

        public NdefValue AnyToNdefValue(Type formalType, object value)
        {
            if (value != null)
                return ToNdefValue(formalType, (T)value);
            else
                return NdefValue.NullValue;
        }

        public object AnyFromNdefValue(Type formalType, NdefValue value)
        {
            if (!value.IsUndefined && !value.IsNull)
                return FromNdefValue(formalType, value);
            else
                return null;
        }
    }
}
