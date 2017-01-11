using System;
using System.Threading;

namespace Nezaboodka
{
    public struct DbKey
    {
        public const string StringPrefixForNew = "?";
        public const string StringPrefixForToBeDeleted = "~";
        public const long MaxRevision = long.MaxValue - 3;
        public const long FlagForReference = long.MaxValue; // ~FlagForReference = long.MinValue
        public const long FlagForImplicit = long.MaxValue - 1;
        public const long FlagForNew = long.MaxValue - 2;

        // Global

        private static long gLastNewSystemId;

        // Fields

        public long SystemId;
        public long RevisionAndFlags;
        
        // Properties
        
        public long Revision { get { return GetRevision(); } set { SetRevision(value); } }
        public bool IsObject { get { return RevisionAndFlags != FlagForReference && RevisionAndFlags != ~FlagForReference; } }
        public bool IsReference { get { return !IsObject; } }
        public bool IsExisting { get { return SystemId > 0 && RevisionAndFlags != FlagForNew; } }
        public bool IsIndefinite { get { return SystemId == 0; } }
        public bool IsNew { get { return SystemId < 0 || RevisionAndFlags == FlagForNew; } }
        public bool IsRemoved { get { return RevisionAndFlags < 0; } }
        public bool IsImplicit { get { return RevisionAndFlags == FlagForImplicit; } }

        public DbKey AsSystemId
        {
            get { return new DbKey(SystemId, 0); }
        }

        public DbKey AsObject
        {
            get
            {
                long r;
                if (RevisionAndFlags == FlagForReference)
                    r = 0;
                else if (RevisionAndFlags == ~FlagForReference)
                    r = ~0;
                else
                    r = RevisionAndFlags;
                return new DbKey(SystemId, r);
            }
        }

        public DbKey AsReference
        {
            get { return new DbKey(SystemId, RevisionAndFlags >= 0 ? FlagForReference : ~FlagForReference); }
        }

        public DbKey AsRemoved
        {
            get
            {
                if (SystemId >= 0)
                    return new DbKey(SystemId, RevisionAndFlags >= 0 ? ~RevisionAndFlags : RevisionAndFlags);
                else
                    throw new Exception("new object cannot be deleted");
            }
        }

        public DbKey AsImplicit
        {
            get
            {
                if (IsExisting)
                    return new DbKey(SystemId, FlagForImplicit);
                else
                    throw new Exception(string.Format("wrong usage of {0} method", nameof(AsImplicit)));
            }
        }

        // Public

        public DbKey(DbKey key)
        {
            SystemId = key.SystemId;
            RevisionAndFlags = key.RevisionAndFlags;
        }

        public DbKey(long systemId, long revisionAndFlags)
        {
            SystemId = systemId;
            RevisionAndFlags = revisionAndFlags;
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
                        key.RevisionAndFlags = DbUtils.FromBase32String(value.Substring(j + 1));
                        if (value[0] == StringPrefixForToBeDeleted[0])
                            key.RevisionAndFlags = ~key.RevisionAndFlags;
                    }
                    else
                    {
                        key.SystemId = DbUtils.FromBase32String(value.Substring(i));
                        key.RevisionAndFlags = 0;
                        if (value[0] == StringPrefixForNew[0])
                            key.SystemId = -key.SystemId;
                        else if (value[0] == StringPrefixForToBeDeleted[0])
                            key.RevisionAndFlags = ~0;
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
                if (RevisionAndFlags > 0 && RevisionAndFlags < FlagForImplicit)
                    result = result + '.' + DbUtils.ToBase32String(RevisionAndFlags);
                else if (RevisionAndFlags < ~0)
                    result = StringPrefixForToBeDeleted + result + '.' + DbUtils.ToBase32String(~RevisionAndFlags);
                else if (RevisionAndFlags < 0)
                    result = StringPrefixForToBeDeleted + result;
            }
            else if (SystemId < 0)
                result = StringPrefixForNew + DbUtils.ToBase32String(-SystemId);
            else if (RevisionAndFlags == ~0)
                result = StringPrefixForToBeDeleted;
            return result;
        }

        public bool Equals(DbKey value)
        {
            return SystemId == value.SystemId && RevisionAndFlags == value.RevisionAndFlags;
        }

        public override bool Equals(object obj)
        {
            return Equals((DbKey)obj);
        }

        public override int GetHashCode()
        {
            return SystemId.GetHashCode() + RevisionAndFlags.GetHashCode();
        }

        // Internal

        private long GetRevision()
        {
            if (RevisionAndFlags >= 0)
            {
                if (RevisionAndFlags <= MaxRevision)
                    return RevisionAndFlags;
                else
                    return 0; // ссылки, объекты indefinite, new и implicit не имеют ревизии
            }
            else
            {
                if (RevisionAndFlags >= ~MaxRevision)
                    return ~RevisionAndFlags;
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
                    if (RevisionAndFlags >= 0)
                        RevisionAndFlags = value;
                    else
                        RevisionAndFlags = ~value; // сохранять признак удаления/изъятия объекта
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
