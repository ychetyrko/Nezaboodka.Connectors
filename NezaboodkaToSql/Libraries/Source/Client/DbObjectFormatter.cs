using System;
using Nezaboodka.Ndef;
using System.Collections.Generic;

namespace Nezaboodka
{
    public class DbObjectFormatter<T> : ObjectFormatter<T> where T: DbObject
    {
        public DbObjectFormatter() : base()
        {
        }

        public DbObjectFormatter(string serializableTypeName) : base(serializableTypeName)
        {
        }

        public override NdefValue ToNdefValue(Type formalType, T value)
        {
            var result = new NdefValue();
            if (value != null)
            {
                result.AsObjectKey = value.Key.ToString();
                if (value.IsObject)
                    result.AsNestedObjectToSerialize = value;
            }
            return result;
        }

        public override T FromNdefValue(Type formalType, NdefValue value)
        {
            T result;
            if (value.AsNestedObjectToDeserialize == null)
            {
                if (!string.IsNullOrEmpty(value.AsObjectKey))
                {
                    var h = new NdefObjectHeader() { Key = value.AsObjectKey };
                    result = Activator.CreateInstance<T>();
                    result.Key = DbKey.Parse(value.AsObjectKey).AsReference;
                }
                else
                    result = null;
            }
            else
                result = base.FromNdefValue(formalType, value);
            return result;
        }

        public override T CreateObjectInstance(Type formalType, NdefObjectHeader objectHeader)
        {
            T result = base.CreateObjectInstance(formalType, objectHeader);
            if (!string.IsNullOrEmpty(objectHeader.Key))
                result.Key = DbKey.Parse(objectHeader.Key);
            return result;
        }
    }

    public class DbDynamicFormatter : DbObjectFormatter<DbDynamic>
    {
        public DbDynamicFormatter() : base()
        {
        }

        public DbDynamicFormatter(string serializableTypeName) : base(serializableTypeName)
        {
        }

        public override void Initialize(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            base.Initialize(typeBinder, null); // выключить кодогенерацию
        }

        public override void Configure(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            base.Configure(typeBinder, null); // выключить кодогенерацию
            SetFieldAccessors(CreateDynamicObjectFieldAccessors(typeBinder));
        }

        public override DbDynamic CreateObjectInstance(Type formalType, NdefObjectHeader objectHeader)
        {
            DbDynamic result = base.CreateObjectInstance(formalType, objectHeader);
            result.TypeName = objectHeader.TypeName;
            return result;
        }

        private IEnumerable<NdefFieldAccessor<DbDynamic>> CreateDynamicObjectFieldAccessors(
            INdefTypeBinder typeBinder)
        {
            ClientTypeSystem typeSystem = ((ClientTypeBinder)typeBinder).TypeSystem;
            int typeNumber = typeSystem.GetTypeNumberByName(SerializableTypeName);
            if (typeNumber >= 0)
            {
                int fieldCount = typeSystem.GetFieldCount(typeNumber);
                var formatters = new INdefFormatter<object>[fieldCount];
                for (int i = 0; i < fieldCount; i++)
                {
                    FieldKind fieldKind = typeSystem.GetFieldKind(typeNumber, i);
                    if (fieldKind != FieldKind.ObjectList && fieldKind != FieldKind.ValueList)
                    {
                        string fieldTypeName = typeSystem.GetFieldTypeName(typeNumber, i);
                        formatters[i] = typeBinder.LookupTypeInfoByName(fieldTypeName).Formatter.Boxed;
                    }
                    else
                        formatters[i] = typeBinder.LookupTypeInfoByType(typeBinder.PreferredListType).Formatter.Boxed;
                }
                for (int i = 0; i < fieldCount; i++)
                {
                    string fieldName = typeSystem.GetFieldName(typeNumber, i);
                    INdefFormatter<object> formatter = formatters[i];
                    NdefFieldGetter<DbDynamic> getter = delegate (DbDynamic obj)
                    {
                        object value = null;
                        if (obj.Fields != null)
                            obj.Fields.TryGetValue(fieldName, out value);
                        return formatter.ToNdefValue(formatter.FormalType, value);
                    };
                    NdefFieldSetter<DbDynamic> setter = delegate (DbDynamic obj, NdefValue value)
                    {
                        object t = formatter.FromNdefValue(formatter.FormalType, value);
                        if (obj.Fields == null)
                            obj.Fields = new Dictionary<string, object>();
                        if (obj.Fields.ContainsKey(fieldName))
                            obj.Fields[fieldName] = t;
                        else
                            obj.Fields.Add(fieldName, t);
                    };
                    yield return new NdefFieldAccessor<DbDynamic>(fieldName, getter, setter);
                }
            }
        }
    }
}
