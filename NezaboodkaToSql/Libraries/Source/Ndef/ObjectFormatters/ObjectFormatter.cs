using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Nezaboodka.Ndef
{
    public class ObjectFormatter<T> : AbstractObjectFormatter<T>
    {
        // Fields
        private NdefFieldAccessor<T>[] fFieldAccessors;
        private Dictionary<string, int> fFieldNumberByName;

        // Public
        public int FieldCount { get { return fFieldAccessors.Length; } }
        public string GetFieldNameByNumber(int number) { return fFieldAccessors[number].Name; }
        public int GetFieldNumberByName(string name) { return fFieldNumberByName[name]; }

        public ObjectFormatter()
        {
        }

        public ObjectFormatter(string serializableTypeName)
        {
            SerializableTypeName = serializableTypeName;
        }

        public override void Initialize(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            base.Initialize(typeBinder, codegen);
            if (codegen != null)
            {
                IEnumerable<FieldInfo> fields = typeof(T).GetFields()
                    .Where((FieldInfo x) => x.DeclaringType.Name != "DbObject");
                codegen.GenerateFieldsAccessors(typeBinder, typeof(T), fields);
            }
        }

        public override void Configure(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            base.Configure(typeBinder, codegen);
            if (codegen != null)
                SetFieldAccessors(codegen.GetFieldAccessors<T>(typeBinder));
        }

        public override IEnumerable<NdefElement> ToNdefElements(T o, int[] fieldNumbers)
        {
            if (fieldNumbers != null)
            {
                for (int i = 0; i < fieldNumbers.Length; i++)
                {
                    int n = fieldNumbers[i];
                    NdefFieldAccessor<T> accessor = fFieldAccessors[n];
                    yield return new NdefElement()
                    {
                        Field = new NdefField() { Number = n, Name = accessor.Name },
                        Value = accessor.Getter(o)
                    };
                }
            }
            else // all fields
            {
                for (int n = 0; n < fFieldAccessors.Length; n++)
                {
                    NdefFieldAccessor<T> accessor = fFieldAccessors[n];
                    yield return new NdefElement()
                    {
                        Field = new NdefField() { Number = n, Name = accessor.Name },
                        Value = accessor.Getter(o)
                    };
                }
            }
        }

        public override void FromNdefElements(T o, IEnumerable<NdefElement> elements)
        {
            foreach (NdefElement x in elements)
            {
                int n = x.Field.Number;
                if (n < 0)
                    n = fFieldNumberByName[x.Field.Name];
                NdefFieldAccessor<T> accessor = fFieldAccessors[n];
                accessor.Setter(o, x.Value);
            }
        }

        // Internal

        protected void SetFieldAccessors(IEnumerable<NdefFieldAccessor<T>> fields)
        {
            if (fFieldAccessors == null)
            {
                fFieldAccessors = fields.ToArray();
                // TODO: To define field order, for now: order by name
                //Array.Sort(fFieldAccessors, (a, b) => string.Compare(a.Name, b.Name));
                fFieldNumberByName = new Dictionary<string, int>(fFieldAccessors.Length);
                for (int i = 0; i < fFieldAccessors.Length; ++i)
                    fFieldNumberByName.Add(fFieldAccessors[i].Name, i);
            }
            else
                throw new InvalidOperationException("cannot set field list twice for a same formatter");
        }

        private IEnumerable<NdefFieldAccessor<T>> CreateReflectionBasedFieldAccessors(INdefTypeBinder typeBinder)
        {
            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where((FieldInfo x) => x.DeclaringType.Name != "DbObject").ToArray(); // игнорировать DbObject.Key
            INdefFormatter<object>[] formatters = fields.Select((FieldInfo x) => (INdefFormatter<object>)typeBinder.LookupTypeInfoByType(x.FieldType).Formatter).ToArray();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                INdefFormatter<object> formatter = formatters[i];
                NdefFieldGetter<T> getter = delegate (T obj)
                {
                    object value = f.GetValue(obj);
                    return formatter.ToNdefValue(formatter.FormalType, value);
                };
                NdefFieldSetter<T> setter = delegate (T obj, NdefValue value)
                {
                    object t = formatter.FromNdefValue(f.FieldType, value);
                    f.SetValue(obj, t);
                };
                yield return new NdefFieldAccessor<T>(f.Name, getter, setter);
            }
        }
    }
}
