using System.Collections.Generic;
using System.Linq;

namespace Nezaboodka.Ndef
{
    public class ObjectFormatter<T> : AbstractObjectFormatter
    {
        // Fields
        private NdefFieldAccessor<T>[] fFieldAccessors;
        private Dictionary<string, int> fFieldNumberByName;

        // Public
        public int FieldCount { get { return fFieldAccessors.Length; } }
        public string GetFieldNameByNumber(int number) { return fFieldAccessors[number].Name; }
        public int GetFieldNumberByName(string name) { return fFieldNumberByName[name]; }

        public ObjectFormatter(IEnumerable<NdefFieldAccessor<T>> fields)
        {
            fFieldAccessors = fields.ToArray();
            // TODO: To define field order, for now: order by name
            //Array.Sort(fFieldAccessors, (a, b) => string.Compare(a.Name, b.Name));
            fFieldNumberByName = new Dictionary<string, int>(fFieldAccessors.Length);
            for (int i = 0; i < fFieldAccessors.Length; ++i)
                fFieldNumberByName.Add(fFieldAccessors[i].Name, i);
        }

        public override IEnumerable<NdefLine> ToNdefLines(object obj, int[] fieldNumbers)
        {
            var o = (T)obj;
            if (fieldNumbers != null)
            {
                for (int i = 0; i < fieldNumbers.Length; i++)
                {
                    int n = fieldNumbers[i];
                    NdefFieldAccessor<T> accessor = fFieldAccessors[n];
                    yield return new NdefLine()
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
                    yield return new NdefLine()
                    {
                        Field = new NdefField() { Number = n, Name = accessor.Name },
                        Value = accessor.Getter(o)
                    };
                }
            }
        }

        public override void FromNdefLines(object obj, IEnumerable<NdefLine> lines)
        {
            var o = (T)obj;
            foreach (NdefLine x in lines)
            {
                int n = x.Field.Number;
                if (n < 0)
                    n = fFieldNumberByName[x.Field.Name];
                NdefFieldAccessor<T> accessor = fFieldAccessors[n];
                accessor.Setter(o, x.Value);
            }
        }
    }

    public class NdefFieldAccessor<T>
    {
        public readonly string Name;
        public readonly NdefFieldGetter<T> Getter;
        public readonly NdefFieldSetter<T> Setter;

        public NdefFieldAccessor(string name, NdefFieldGetter<T> getter, NdefFieldSetter<T> setter)
        {
            Name = name;
            Getter = getter;
            Setter = setter;
        }
    }

    public delegate NdefValue NdefFieldGetter<T>(T obj);
    public delegate void NdefFieldSetter<T>(T obj, NdefValue value);
}
