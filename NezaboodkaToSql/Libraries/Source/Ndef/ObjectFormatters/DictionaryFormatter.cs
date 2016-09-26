using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public class DictionaryFormatter : AbstractObjectFormatter
    {
        public INdefValueFormatter ValueFormatter { get; private set; }

        public DictionaryFormatter(INdefValueFormatter valueFormatter)
        {
            ValueFormatter = valueFormatter;
        }

        public override IEnumerable<NdefLine> ToNdefLines(object obj, int[] fieldNumbers)
        {
            var dictionary = (Dictionary<string, object>)obj;
            var f = new NdefLine();
            foreach (KeyValuePair<string, object> x in dictionary)
            {
                f.Field.Name = x.Key;
                f.Value = ValueFormatter.AnyToNdefValue(ValueFormatter.TypeOfValue, x.Value);
                if (!f.Value.IsUndefined)
                    yield return f;
            }
        }

        public override void FromNdefLines(object obj, IEnumerable<NdefLine> lines)
        {
            var dictionary = (Dictionary<string, object>)obj;
            foreach (NdefLine line in lines)
            {
                object value = ValueFormatter.AnyFromNdefValue(ValueFormatter.TypeOfValue, line.Value);
                dictionary.Add(line.Field.Name, value);
            }
        }
    }
}
