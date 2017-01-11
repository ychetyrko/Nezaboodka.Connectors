using System;
using System.Text;
using System.IO;

namespace Nezaboodka.Ndef
{
    public class NdefDataSet
    {
        public string Header;
        public bool IsStartOfDataSet;
        public bool IsEndOfDataSet;

        public void Clear()
        {
            Header = default(string);
            IsStartOfDataSet = default(bool);
            IsEndOfDataSet = default(bool);
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", NdefConst.DataSetStartMarker, Header);
        }
    }

    public class NdefObject
    {
        public NdefObjectHeader Header;
        public NdefElement CurrentElement;
        public NdefObject Parent;
        public bool IsStartOfObject;
        public bool IsEndOfObject;
        public bool IsBackFromNestedObject;
        public object DeserializedInstance;
        public int DeserializedInstanceIndex;

        public bool IsUntypedList
        {
            get
            {
                return Header.Kind == NdefObjectKind.List &&
                    Header.TypeName == NdefConst.ListTypeBraces &&
                    Header.Number == null && Header.Key == null;
            }
        }

        public void Clear()
        {
            Header = default(NdefObjectHeader);
            CurrentElement = default(NdefElement);
            Parent = default(NdefObject);
            IsStartOfObject = default(bool);
            IsEndOfObject = default(bool);
            DeserializedInstance = default(object);
            DeserializedInstanceIndex = default(int);
        }

        public override string ToString()
        {
            if (Header.Key != null)
                return string.Format("{0} {1} {2}{3}", NdefConst.ObjectStartMarker,
                    Header.TypeName, NdefConst.ObjectKeyPrefix, Header.Key);
            else
                return string.Format("{0} {1}", NdefConst.ObjectStartMarker, Header.TypeName);
        }
    }

    public struct NdefObjectHeader
    {
        public bool IsListItem;
        public NdefObjectKind Kind;
        public string TypeName;
        public NdefTypeInfo TypeInfo;
        public string Number;
        public string Key;

        public bool IsEmpty
        {
            get
            {
                return TypeName == null && Kind == NdefObjectKind.Undefined &&
                    Number == null && Key == null;
            }
        }
    }

    public class NdefTypeInfo
    {
        public readonly int TypeNumber;
        public readonly Type SystemType;
        public readonly bool IsListType;
        public readonly string SerializableName;
        public readonly INdefFormatter Formatter;

        public NdefTypeInfo(int typeNumber, Type type, bool isListType,
            string serializableName, INdefFormatter formatter)
        {
            TypeNumber = typeNumber;
            SystemType = type;
            IsListType = isListType;
            SerializableName = serializableName;
            Formatter = formatter;
        }
    }

    public struct NdefElement
    {
        public NdefField Field;
        public NdefValue Value;
        public string Comment;

        public override string ToString()
        {
            if (Field.Name != null)
                return string.Format("{0}{1} {2}", Field.Name,
                    NdefConst.ValueSeparator, Value.AsScalar);
            else
                return string.Format("{0} {1}", NdefConst.ListItemMarker,
                    Value.AsScalar ?? Value.AsNestedObjectToDeserialize.ToString());
        }
    }

    public struct NdefField
    {
        public int Number;
        public string Name;
        public NdefFieldKind Kind;
    }

    public struct NdefValue
    {
        public static readonly NdefValue UndefinedValue = new NdefValue();
        public static readonly NdefValue NullValue = new NdefValue() { AsObjectNumber = string.Empty };

        public string ActualSerializableTypeName;
        public string AsScalar; // ==> NdefValueKind.Value
        public string AsObjectNumber; // ==> NdefValueKind.Reference
        public string AsObjectKey; // ==> NdefValueKind.Reference
        public bool HasNoLineFeeds;
        public NdefObject AsNestedObjectToDeserialize; // ==> NdefValueKind.ObjectOrList
        public object AsNestedObjectToSerialize; // ==> NdefValueKind.ObjectOrList
        public Stream AsStream;

        public override string ToString()
        {
            return AsScalar;
        }

        public NdefValueKind Kind
        {
            get
            {
                NdefValueKind result;
                if (AsNestedObjectToSerialize != null || AsNestedObjectToDeserialize != null)
                    result = NdefValueKind.Object;
                else if ((AsObjectNumber != null || AsObjectKey != null) && !IsNull)
                    result = NdefValueKind.Reference;
                else
                    result = NdefValueKind.Scalar;
                return result;
            }
        }

        public bool IsUndefined
        {
            get
            {
                return AsScalar == null && AsObjectNumber == null && AsObjectKey == null &&
                    AsNestedObjectToSerialize == null && AsNestedObjectToDeserialize == null &&
                    AsStream == null;
            }
        }

        public bool IsNull
        {
            get
            {
                return AsObjectNumber == NullValue.AsObjectNumber;
            }
        }

        public bool IsUntypedList
        {
            get
            {
                return Kind == NdefValueKind.Object &&
                    ActualSerializableTypeName == NdefConst.ListTypeBraces &&
                    AsObjectNumber == null && AsObjectKey == null;
            }
        }
    }

    public delegate NdefValue NdefFieldGetter<T>(T obj);
    public delegate void NdefFieldSetter<T>(T obj, NdefValue value);

    public class NdefFieldAccessor<T>
    {
        public readonly string Name;
        public readonly NdefFieldGetter<T> Getter;
        public readonly NdefFieldSetter<T> Setter;

        public NdefFieldAccessor(string name, NdefFieldGetter<T> getter, NdefFieldSetter<T> setter)
        {
            Name = name;
            Getter = getter;
            Setter = setter;
        }
    }

    public enum NdefObjectKind
    {
        Undefined = 0,
        ToBeDefined = 1,
        Object = 2,
        List = 3
    }

    public enum NdefValueKind
    {
        Scalar = 0,
        Reference = 1,
        Object = 2 // включает в себя и списки (массивы)
    }

    public enum NdefFieldKind
    {
        SetOrAdd = 0,
        Remove = 1
    }

    public static class NdefConst
    {
        public const char DataSetStartMarker = '[';
        public const char DataSetEndMarker = ']';
        public const char ObjectStartMarker = '{';
        public const char ObjectEndMarker = '}';
        public const char ObjectKeyPrefix = '#';
        public const char ObjectNumberPrefix = '^';
        public const char ExplicitTypeNamePrefix = '`';
        public const char ValueSeparator = ':';
        public const char QuotationMarker = '|';
        public const char ListItemMarker = '*';
        public const char ListItemToRemoveMarker = '~';
        public const char CommentMarker = '/';
        public const string ListTypeBraces = "[]";
        public const string FixedArrayTypeFormat = "{0}[{1}]";
        public const char BinaryDataMarker = '\\';
        public static readonly Encoding Encoding = new UTF8Encoding(false);
    }
}
