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
}
