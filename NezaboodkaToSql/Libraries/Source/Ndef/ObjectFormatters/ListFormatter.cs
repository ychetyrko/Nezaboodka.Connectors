using System.Collections;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public class ListFormatter : AbstractObjectFormatter
    {
        public INdefValueFormatter ListItemFormatter { get; private set; }

        public ListFormatter(INdefValueFormatter listItemFormatter)
        {
            ListItemFormatter = listItemFormatter;
        }

        public override IEnumerable<NdefLine> ToNdefLines(object obj, int[] fieldNumbers)
        {
            var list = (IList)obj;
            var f = new NdefLine();
            foreach (object x in list)
            {
                f.Value = ListItemFormatter.AnyToNdefValue(ListItemFormatter.TypeOfValue, x);
                if (!f.Value.IsUndefined)
                    yield return f;
            }
        }

        public override void FromNdefLines(object obj, IEnumerable<NdefLine> lines)
        {
            var list = (IList)obj;
            foreach (NdefLine line in lines)
            {
                object value = ListItemFormatter.AnyFromNdefValue(ListItemFormatter.TypeOfValue, line.Value);
                list.Add(value);
            }
        }
    }
}
