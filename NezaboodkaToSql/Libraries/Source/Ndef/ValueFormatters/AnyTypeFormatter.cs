using System;

namespace Nezaboodka.Ndef
{
    public class AnyTypeFormatter : AbstractValueFormatter<object>
    {
        public INdefTypeBinder TypeBinder { get; private set; }

        public AnyTypeFormatter(Type formalType) : base()
        {
            FormalType = formalType;
            SerializableTypeName = null;
        }
            
        public override void Initialize(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            base.Initialize(typeBinder, codegen);
            TypeBinder = typeBinder;
        }

        public override NdefValue ToNdefValue(Type formalType, object value)
        {
            NdefValue result;
            if (value != null)
            {
                NdefTypeInfo typeInfo = TypeBinder.LookupTypeInfo(value);
                result = typeInfo.Formatter.Boxed.ToNdefValue(formalType, value);
                if (formalType == typeof(object) && result.Kind != NdefValueKind.Object)
                    result.ActualSerializableTypeName = typeInfo.Formatter.SerializableTypeName;
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
                        if (!string.IsNullOrEmpty(o.Header.TypeName))
                            o.Header.TypeInfo = typeBinder.LookupTypeInfoByName(o.Header.TypeName);
                        else if (o.Header.Kind == NdefObjectKind.List) // untyped list
                            o.Header.TypeInfo = typeBinder.LookupTypeInfoByType(typeBinder.PreferredListType.MakeGenericType(listItemType));
                        else
                            throw new ArgumentException("object type info cannot be empty");
                        INdefFormatter<object> f = o.Header.TypeInfo.Formatter.Boxed;
                        o.DeserializedInstance = f.CreateObjectInstance(formalType, o.Header);
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
                        result = typeInfo.Formatter.Boxed.FromNdefValue(formalType, value);
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
