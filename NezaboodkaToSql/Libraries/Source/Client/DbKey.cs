using System;
using System.Threading;

namespace Nezaboodka
{
    public struct DbKey
    {
        public const string StringPrefixForNew = "?";
        public const string StringPrefixForToBeDeleted = "~";
        public const long MaxRevision = long.MaxValue - 3;
        public const long RawRevisionForReference = long.MaxValue; // ~RawRevisionForReference = long.MinValue
        public const long RawRevisionForImplicit = long.MaxValue - 1;
        public const long RawRevisionForNew = long.MaxValue - 2;

        // Global

        private static long gLastNewSystemId;

        // Fields

        public long SystemId;
        public long RawRevision;
        
        // Properties
        
        public long Revision { get { return GetRevision(); } set { SetRevision(value); } }
        // DbKey.Id == 0 пока тоже трактуется как объект для совместимости с существующим кодом
        public bool IsObject { get { return RawRevision != RawRevisionForReference && RawRevision != ~RawRevisionForReference; } }
        public bool IsReference { get { return !IsObject; } }
        public bool IsExisting { get { return SystemId > 0 && RawRevision != RawRevisionForNew; } }
        public bool IsIndefinite { get { return SystemId == 0; } }
        public bool IsNew { get { return SystemId < 0 || RawRevision == RawRevisionForNew; } }
        public bool IsRemoved { get { return RawRevision < 0; } }
        public bool IsImplicit { get { return RawRevision == RawRevisionForImplicit; } }

        public DbKey AsSystemId
        {
            get { return new DbKey(SystemId, 0); }
        }

        public DbKey AsObject
        {
            get
            {
                long r;
                if (RawRevision == RawRevisionForReference)
                    r = 0;
                else if (RawRevision == ~RawRevisionForReference)
                    r = ~0;
                else
                    r = RawRevision;
                return new DbKey(SystemId, r);
            }
        }

        public DbKey AsReference
        {
            get { return new DbKey(SystemId, RawRevision >= 0 ? RawRevisionForReference : ~RawRevisionForReference); }
        }

        public DbKey AsRemoved
        {
            get
            {
                if (SystemId >= 0)
                    return new DbKey(SystemId, RawRevision >= 0 ? ~RawRevision : RawRevision);
                else
                    throw new Exception("new object cannot be deleted");
            }
        }

        public DbKey AsImplicit
        {
            get
            {
                if (IsExisting)
                    return new DbKey(SystemId, RawRevisionForImplicit);
                else
                    throw new Exception(string.Format("wrong usage of {0} method", nameof(AsImplicit)));
            }
        }

        // Public

        public DbKey(DbKey key)
        {
            SystemId = key.SystemId;
            RawRevision = key.RawRevision;
        }

        public DbKey(long systemId, long rawRevision)
        {
            SystemId = systemId;
            RawRevision = rawRevision;
        }

        public static DbKey NewObject()
        {
            // Ключи новых объектов всегда отрицательные.
            return new DbKey(Interlocked.Decrement(ref gLastNewSystemId), 0);
        }

        public static DbKey Parse(string value)
        {
            DbKey key = new DbKey();
            if (value != null && value.Length > 0)
            {
                try
                {
                    int i = 0;
                    if (value[0] == StringPrefixForNew[0] || value[0] == StringPrefixForToBeDeleted[0])
                        i = 1;
                    int j = value.IndexOf('.', i);
                    if (j >= 0)
                    {
                        key.SystemId = DbUtils.FromBase32String(value.Substring(i, j - i));
                        key.RawRevision = DbUtils.FromBase32String(value.Substring(j + 1));
                        if (value[0] == StringPrefixForToBeDeleted[0])
                            key.RawRevision = ~key.RawRevision;
                    }
                    else
                    {
                        key.SystemId = DbUtils.FromBase32String(value.Substring(i));
                        key.RawRevision = 0;
                        if (value[0] == StringPrefixForNew[0])
                            key.SystemId = -key.SystemId;
                        else if (value[0] == StringPrefixForToBeDeleted[0])
                            key.RawRevision = ~0;
                    }
                }
                catch
                {
                    throw new NezaboodkaException(string.Format("invalid format of object key '{0}'", value));
                }
            }
            return key;
        }

        public override string ToString()
        {
            string result = string.Empty;
            if (SystemId > 0)
            {
                result = DbUtils.ToBase32String(SystemId);
                if (RawRevision > 0 && RawRevision < RawRevisionForImplicit)
                    result = result + '.' + DbUtils.ToBase32String(RawRevision);
                else if (RawRevision < ~0)
                    result = StringPrefixForToBeDeleted + result + '.' + DbUtils.ToBase32String(~RawRevision);
                else if (RawRevision < 0)
                    result = StringPrefixForToBeDeleted + result;
            }
            else if (SystemId < 0)
                result = StringPrefixForNew + DbUtils.ToBase32String(-SystemId);
            else if (RawRevision == ~0)
                result = StringPrefixForToBeDeleted;
            return result;
        }

        public bool Equals(DbKey value)
        {
            return SystemId == value.SystemId && RawRevision == value.RawRevision;
        }

        public override bool Equals(object obj)
        {
            return Equals((DbKey)obj);
        }

        public override int GetHashCode()
        {
            return SystemId.GetHashCode() + RawRevision.GetHashCode();
        }

        // Internal

        private long GetRevision()
        {
            if (RawRevision >= 0)
            {
                if (RawRevision <= MaxRevision)
                    return RawRevision;
                else
                    return 0; // ссылки, объекты indefinite, new и implicit не имеют ревизии
            }
            else
            {
                if (RawRevision >= ~MaxRevision)
                    return ~RawRevision;
                else
                    return 0; // удаляемые ссылки не имеют ревизии
            }
        }

        private void SetRevision(long value)
        {
            if (value >= 0 && value <= MaxRevision)
            {
                if (SystemId > 0)
                {
                    if (RawRevision >= 0)
                        RawRevision = value;
                    else
                        RawRevision = ~value; // сохранять признак удаления/изъятия объекта
                }
                else
                    throw new ArgumentException("object revision cannot be set for non-existing key");
            }
            else
                throw new ArgumentOutOfRangeException(string.Format(
                    "object revision ({0}) is out of range", value));
        }
    }
}
