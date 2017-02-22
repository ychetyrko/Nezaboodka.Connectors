using System;
using System.Text;
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

        private static long gLastNewPrimaryId;

        // Fields

        public long PrimaryId;
        public long RevisionAndFlags;
        
        // Properties
        
        public long Revision { get { return GetRevision(); } set { SetRevision(value); } }
        public bool IsObject { get { return RevisionAndFlags != FlagForReference && RevisionAndFlags != ~FlagForReference; } }
        public bool IsReference { get { return !IsObject; } }
        public bool IsExisting { get { return PrimaryId > 0 && RevisionAndFlags != FlagForNew; } }
        public bool IsIndefinite { get { return PrimaryId == 0; } }
        public bool IsNew { get { return PrimaryId < 0 || RevisionAndFlags == FlagForNew; } }
        public bool IsRemoved { get { return RevisionAndFlags < 0; } }
        public bool IsImplicit { get { return RevisionAndFlags == FlagForImplicit; } }

        public DbKey AsPrimaryId
        {
            get { return new DbKey(PrimaryId, 0); }
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
                return new DbKey(PrimaryId, r);
            }
        }

        public DbKey AsReference
        {
            get { return new DbKey(PrimaryId, RevisionAndFlags >= 0 ? FlagForReference : ~FlagForReference); }
        }

        public DbKey AsRemoved
        {
            get
            {
                if (PrimaryId >= 0)
                    return new DbKey(PrimaryId, RevisionAndFlags >= 0 ? ~RevisionAndFlags : RevisionAndFlags);
                else
                    throw new Exception("new object cannot be deleted");
            }
        }

        public DbKey AsImplicit
        {
            get
            {
                if (IsExisting)
                    return new DbKey(PrimaryId, FlagForImplicit);
                else
                    throw new Exception(string.Format("wrong usage of {0} method", nameof(AsImplicit)));
            }
        }

        // Public

        public DbKey(DbKey key)
        {
            PrimaryId = key.PrimaryId;
            RevisionAndFlags = key.RevisionAndFlags;
        }

        public DbKey(long primaryId, long revisionAndFlags)
        {
            PrimaryId = primaryId;
            RevisionAndFlags = revisionAndFlags;
        }

        public static DbKey NewObject()
        {
            // Ключи новых объектов всегда отрицательные.
            return new DbKey(Interlocked.Decrement(ref gLastNewPrimaryId), 0);
        }

        public static DbKey Parse(string value, bool isObject)
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
                        key.PrimaryId = DbUtils.FromBase32String(value.Substring(i, j - i));
                        if (value.Length == j + 2 && value[j + 1] == StringPrefixForNew[0])
                            key.RevisionAndFlags = FlagForNew;
                        else
                            key.RevisionAndFlags = DbUtils.FromBase32String(value.Substring(j + 1));
                        if (value[0] == StringPrefixForToBeDeleted[0])
                            key.RevisionAndFlags = ~key.RevisionAndFlags;
                    }
                    else
                    {
                        key.PrimaryId = DbUtils.FromBase32String(value.Substring(i));
                        if (isObject)
                            key.RevisionAndFlags = 0; // IsObject
                        else
                            key.RevisionAndFlags = FlagForReference; // IsReference
                        if (value[0] == StringPrefixForNew[0])
                            key.PrimaryId = -key.PrimaryId;
                        else if (value[0] == StringPrefixForToBeDeleted[0])
                            key.RevisionAndFlags = ~key.RevisionAndFlags;
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
            if (PrimaryId > 0)
            {
                var sb = new StringBuilder(4);
                if (RevisionAndFlags > 0 && RevisionAndFlags <= MaxRevision)
                {
                    sb.Append(DbUtils.ToBase32String(PrimaryId));
                    sb.Append('.');
                    sb.Append(DbUtils.ToBase32String(RevisionAndFlags));
                }
                else if (RevisionAndFlags == FlagForNew)
                {
                    sb.Append(DbUtils.ToBase32String(PrimaryId));
                    sb.Append('.');
                    sb.Append(StringPrefixForNew);
                }
                else if (RevisionAndFlags == 0 || RevisionAndFlags > MaxRevision) // including FlagForReference and FlagForImplicit
                {
                    sb.Append(DbUtils.ToBase32String(PrimaryId));
                }
                else if (RevisionAndFlags < ~0 && RevisionAndFlags >= ~MaxRevision)
                {
                    sb.Append(StringPrefixForToBeDeleted);
                    sb.Append(DbUtils.ToBase32String(PrimaryId));
                    sb.Append('.');
                    sb.Append(DbUtils.ToBase32String(~RevisionAndFlags));
                }
                else if (RevisionAndFlags == ~FlagForNew)
                {
                    sb.Append(StringPrefixForToBeDeleted);
                    sb.Append(DbUtils.ToBase32String(PrimaryId));
                    sb.Append('.');
                    sb.Append(StringPrefixForNew);
                }
                else if (RevisionAndFlags == ~0 || RevisionAndFlags < ~MaxRevision) // including ~FlagForReference and ~FlagForImplicit
                {
                    sb.Append(StringPrefixForToBeDeleted);
                    sb.Append(DbUtils.ToBase32String(PrimaryId));
                }
                result = sb.ToString();
            }
            else if (PrimaryId < 0)
                result = StringPrefixForNew + DbUtils.ToBase32String(-PrimaryId);
            else if (RevisionAndFlags == ~0)
                result = StringPrefixForToBeDeleted;
            return result;
        }

        public int CompareTo(DbKey key)
        {
            int result = PrimaryId.CompareTo(key.PrimaryId);
            if (result == 0)
                result = RevisionAndFlags.CompareTo(key.RevisionAndFlags);
            return result;
        }

        public int CompareAsReferenceTo(DbKey key)
        {
            int result = PrimaryId.CompareTo(key.PrimaryId);
            if (result == 0)
            {
                if (RevisionAndFlags >= 0)
                {
                    if (key.RevisionAndFlags >= 0)
                        result = 0;
                    else
                        result = 1;
                }
                else if (key.RevisionAndFlags >= 0)
                    result = -1;
                else
                    result = 0;
            }
            return result;
        }

        public bool Equals(DbKey value)
        {
            return PrimaryId == value.PrimaryId && RevisionAndFlags == value.RevisionAndFlags;
        }

        public override bool Equals(object obj)
        {
            return Equals((DbKey)obj);
        }

        public override int GetHashCode()
        {
            return PrimaryId.GetHashCode() + RevisionAndFlags.GetHashCode();
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
                if (PrimaryId > 0)
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
