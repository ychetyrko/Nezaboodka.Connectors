using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public class DictionaryFormatter : AbstractObjectFormatter<Dictionary<string, object>>
    {
        public INdefFormatter<object> ValueFormatter { get; private set; }

        public DictionaryFormatter()
        {
            SerializableTypeName = "Nezaboodka.Dictionary";
        }

        public override void Initialize(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            base.Initialize(typeBinder, codegen);
            ValueFormatter = typeBinder.LookupFormatter<INdefFormatter<object>>(typeof(object));
        }

        public override IEnumerable<NdefElement> ToNdefElements(Dictionary<string, object> dictionary, int[] fieldNumbers)
        {
            var f = new NdefElement();
            foreach (KeyValuePair<string, object> x in dictionary)
            {
                f.Field.Name = x.Key;
                f.Value = ValueFormatter.ToNdefValue(ValueFormatter.FormalType, x.Value);
                if (!f.Value.IsUndefined)
                    yield return f;
            }
        }

        public override void FromNdefElements(Dictionary<string, object> dictionary, IEnumerable<NdefElement> elements)
        {
            foreach (NdefElement element in elements)
            {
                object value = ValueFormatter.FromNdefValue(ValueFormatter.FormalType, element.Value);
                dictionary.Add(element.Field.Name, value);
            }
        }
    }
}
