using System;
using System.Collections;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public class ListValueFormatter<T> : AbstractValueFormatter<T>
    {
        public Type PreferredListType { get; private set; }
        public Type RootTypeForObjectsWithKey { get; private set; }

        public ListValueFormatter(string serializableTypeName,
            Type preferredListType, Type rootTypeForObjectsWithKey)
        {
            SerializableTypeName = serializableTypeName;
            PreferredListType = preferredListType;
            RootTypeForObjectsWithKey = rootTypeForObjectsWithKey;
        }

        public override NdefValue ToNdefValue(Type formalType, T value)
        {
            var result = new NdefValue() { AsNestedObjectToSerialize = value };
            // Set ActualSerializableTypeName only if actual type differs from formal type.
            // Otherwise, serialize as abstract object (with no serializable type name specified).
            if (value != null)
            {
                Type actualType = value.GetType();
                IList list = value as IList;
                if (list != null)
                {
                    if (list.Count > 0)
                    {
                        // Нетипизированный массив - это массив, для которого при сериализации
                        // тип данных не указывается. Такой массив может иметь разное физическое
                        // представление на сервере и на клиенте. Например, на сервере массив
                        // может существовать как DbObject[], а на клиенте - как List<User>/List<DbDynamic>.
                        // Массив сериализуется как нетипизированный, если формальный тип элемента
                        // совпадает с фактическим, либо если фактический тип элемента - это DbObject
                        // (или же любой его наследник).
                        Type formalElementType = NdefUtils.TryGetElementType(formalType);
                        Type actualElementType = NdefUtils.TryGetElementType(actualType);
                        if (formalElementType == actualElementType ||
                            RootTypeForObjectsWithKey.IsAssignableFrom(actualElementType))
                        {
                            result.ActualSerializableTypeName = string.Empty; // string.Empty is treated as untyped list
                        }
                    }
                    else
                        result = NdefValue.NullValue;
                }
            }
            return result;
        }

        public override T FromNdefValue(Type formalType, NdefValue value)
        {
            T result;
            Type type = typeof(T);
            if (type.IsArray)
            {
                IList list = (IList)value.AsNestedObjectToDeserialize.DeserializedInstance;
                object t = Activator.CreateInstance(typeof(T), list.Count);
                Array array = (Array)t;
                list.CopyTo(array, 0);
                result = (T)t;
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(NdefArrayBuffer<>))
            {
                throw new NotImplementedException(); //TODO: Clarify
            }
            else
                result = (T)value.AsNestedObjectToDeserialize.DeserializedInstance;
            return result;
        }
    }

    public class NdefArrayBuffer<T> : List<T>
    {
    }
}
