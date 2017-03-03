using System;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public class DbObject
    {
        public DbKey Key;
        public bool IsObject { get { return Key.IsObject; } }
        public bool IsReference { get { return Key.IsReference; } }
        public bool IsExisting { get { return Key.IsExisting; } }
        public bool IsIndefinite { get { return Key.IsIndefinite; } }
        public bool IsNew { get { return Key.IsNew; } }
        public bool IsToBeDeleted { get { return Key.IsObject && Key.IsRemoved; } }
        public bool IsToBeExcluded { get { return Key.IsReference && Key.IsRemoved; } }
        public bool IsImplicit { get { return Key.IsImplicit; } }

        public override string ToString()
        {
            if (IsObject)
                return string.Format("{0} {1} {2}{3}\n{4}", NdefConst.ObjectStartMarker, GetType().Name,
                    NdefConst.ObjectKeyPrefix, Key.ToString(), NdefConst.ObjectEndMarker);
            else
                return string.Format("{0}{1}", NdefConst.ObjectKeyPrefix, Key.ToString());
        }
    }

    public static class DbObjectProcedures
    {
        public static T ToBeDeleted<T>(this T obj) where T : DbObject, new()
        {
            if (obj.IsObject)
            {
                if (obj.IsExisting)
                    return new T() { Key = obj.Key.AsRemoved };
                else if (obj.Key.IsRemoved)
                    return obj; // не создавать новую заглушку, а вернуть уже имеющуюся
                else
                    throw new ArgumentException(string.Format(
                        "given object cannot be used to create an object to be deleted ({0})", obj.Key));
            }
            else
            {
                if (obj.IsExisting)
                    return new T() { Key = obj.Key.AsObject.AsRemoved };
                else
                    throw new ArgumentException(string.Format(
                        "given reference cannot be used to create an object to be deleted ({0})", obj.Key));
            }
        }

        public static T ToBeExcluded<T>(this T obj) where T : DbObject, new()
        {
            if (obj.IsReference)
            {
                if (obj.IsExisting)
                    return new T() { Key = obj.Key.AsRemoved };
                else if (obj.Key.IsRemoved)
                    return obj; // не создавать новую заглушку, а вернуть уже имеющуюся
                else
                    throw new ArgumentException(string.Format(
                        "given reference cannot be used to create a reference to be excluded ({0})", obj.Key));
            }
            else
            {
                if (obj.IsExisting)
                    return new T() { Key = obj.Key.AsReference.AsRemoved };
                else
                    throw new ArgumentException(string.Format(
                        "given object cannot be used to create a reference as to be excluded ({0})", obj.Key));
            }
        }
    }
}
