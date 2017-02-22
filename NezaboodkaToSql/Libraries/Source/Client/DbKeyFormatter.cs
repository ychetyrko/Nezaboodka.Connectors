using System;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public class DbKeyFormatter : AbstractValueFormatter<DbKey>
    {
        public DbKeyFormatter()
        {
            SerializableTypeName = typeof(DbKey).FullName;
        }

        public override NdefValue ToNdefValue(Type formalType, DbKey value)
        {
            return new NdefValue() { AsScalar = value.ToString(), HasNoLineFeeds = true };
        }

        public override DbKey FromNdefValue(Type formalType, NdefValue value)
        {
            return DbKey.Parse(value.AsScalar, isObject: false);
        }
    }
}
