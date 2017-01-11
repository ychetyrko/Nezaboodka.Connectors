using System.Collections.Generic;

namespace Nezaboodka
{
    public class DbDynamic : DbObject
    {
        public string TypeName { get; set; }
        public Dictionary<string, object> Fields { get; set; }
        public object this[string field]
        {
            get
            {
                object result = null;
                if (Fields != null)
                {
                    try
                    {
                        result = Fields[field];
                    }
                    catch (KeyNotFoundException)
                    {
                    }
                }
                return result;
            }
            set
            {
                Fields[field] = value;
            }
        }
    }
}
