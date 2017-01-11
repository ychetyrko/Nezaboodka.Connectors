using System;

namespace Nezaboodka.Ndef
{
    public abstract class AbstractObjectFormatter<T> : AbstractFormatter<T>
    {
        public AbstractObjectFormatter() : base()
        {
        }

        public override NdefValue ToNdefValue(Type formalType, T value)
        {
            return new NdefValue() { AsNestedObjectToSerialize = value };
        }

        public override T FromNdefValue(Type formalType, NdefValue value)
        {
            return (T)value.AsNestedObjectToDeserialize.DeserializedInstance;
        }
    }
}
