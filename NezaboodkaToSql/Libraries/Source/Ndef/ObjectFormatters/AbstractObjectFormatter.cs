using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public abstract class AbstractObjectFormatter : INdefObjectFormatter
    {
        public abstract IEnumerable<NdefLine> ToNdefLines(object obj, int[] fieldNumbers);
        public abstract void FromNdefLines(object obj, IEnumerable<NdefLine> lines);
    }
}
