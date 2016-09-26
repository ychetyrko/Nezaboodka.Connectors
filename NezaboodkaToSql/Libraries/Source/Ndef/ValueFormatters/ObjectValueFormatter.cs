using System;

namespace Nezaboodka.Ndef
{
    public class ObjectValueFormatter<T> : AbstractValueFormatter<T>
    {
        public ObjectValueFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
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
