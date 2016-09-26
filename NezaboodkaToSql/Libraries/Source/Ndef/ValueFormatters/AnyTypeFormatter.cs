using System;

namespace Nezaboodka.Ndef
{
    public class AnyTypeFormatter : AbstractValueFormatter<object>
    {
        public INdefTypeBinder TypeBinder { get; private set; }

        public AnyTypeFormatter(INdefTypeBinder typeBinder)
        {
            TypeBinder = typeBinder;
        }

        public override NdefValue ToNdefValue(Type formalType, object value)
        {
            NdefValue result;
            if (value != null)
            {
                NdefTypeInfo typeInfo = TypeBinder.LookupTypeInfo(value);
                result = typeInfo.ValueFormatter.AnyToNdefValue(formalType, value);
                if (formalType == typeof(object) && result.Kind != NdefValueKind.Object)
                    result.ActualSerializableTypeName = typeInfo.ValueFormatter.SerializableTypeName;
            }
            else
                result = NdefValue.NullValue;
            return result;
        }

        public override object FromNdefValue(Type formalType, NdefValue value)
        {
            return AnyTypeConvertFromNdefValueBoxed(TypeBinder, formalType, typeof(object), value);
        }

        // Helpers

        public static object AnyTypeConvertFromNdefValueBoxed(INdefTypeBinder typeBinder,
            Type formalType, Type listItemType, NdefValue value)
        {
            object result;
            if (value.Kind == NdefValueKind.Object)
            {
                NdefObject o = value.AsNestedObjectToDeserialize;
                if (o != null)
                {
                    if (o.DeserializedInstance == null)
                    {
                        if (!string.IsNullOrEmpty(o.TypeName))
                            o.TypeInfo = typeBinder.LookupTypeInfoByName(o.TypeName);
                        else if (o.Kind == NdefObjectKind.List) // untyped list
                            o.TypeInfo = typeBinder.LookupTypeInfoByType(typeBinder.PreferredListType.MakeGenericType(listItemType));
                        else
                            throw new ArgumentException("object type info cannot be empty");
                        o.DeserializedInstance = typeBinder.CreateObject(o.TypeInfo, o.TypeInfo.TypeNumber,
                            o.TypeInfo.SerializableName, o.LogicalKey);
                    }
                    result = o.DeserializedInstance;
                }
                else
                    throw new ArgumentException("object or list expected by this formatter");
            }
            else if (value.Kind == NdefValueKind.Scalar)
            {
                if (formalType == typeof(object))
                {
                    if (!string.IsNullOrEmpty(value.ActualSerializableTypeName))
                    {
                        NdefTypeInfo typeInfo = typeBinder.LookupTypeInfoByName(value.ActualSerializableTypeName);
                        result = typeInfo.ValueFormatter.AnyFromNdefValue(formalType, value);
                    }
                    else
                        result = value.AsScalar;
                }
                else
                    throw new ArgumentException("boxing-unboxing is not supported for non-object fields");
            }
            else
                throw new ArgumentException("boxing-unboxing is not supported for object references");
            return result;
        }
    }
}
