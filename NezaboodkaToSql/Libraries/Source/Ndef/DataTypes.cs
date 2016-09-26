using System;

namespace Nezaboodka.Ndef
{
    public class NdefTypeInfo
    {
        public readonly int TypeNumber;
        public readonly Type SystemType;
        public readonly bool IsListType;
        public readonly string SerializableName;
        public readonly INdefValueFormatter ValueFormatter;
        public INdefObjectFormatter ObjectFormatter;

        public NdefTypeInfo(int typeNumber, Type type, bool isListType, string serializableName,
            INdefValueFormatter formatter)
        {
            TypeNumber = typeNumber;
            SystemType = type;
            IsListType = isListType;
            SerializableName = serializableName;
            ValueFormatter = formatter;
        }
    }

    public class NdefBlock
    {
        public string Header;
        public NdefBlock Parent;
        public bool IsStartOfBlock;
        public bool IsEndOfBlock;
        public object CustomData;

        public void Clear()
        {
            Header = default(string);
            Parent = default(NdefBlock);
            IsStartOfBlock = default(bool);
            IsEndOfBlock = default(bool);
            CustomData = default(object);
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", NdefConst.BlockStartMarker, Header);
        }
    }

    public enum NdefObjectKind
    {
        Undefined = 0,
        ToBeDefined = 1,
        Object,
        List
    }

    public class NdefObject
    {
        public bool IsListItem;
        public string TypeName;
        public NdefObjectKind Kind;
        public string SerialKey;
        public string LogicalKey;
        public NdefLine CurrentLine;
        public NdefObject Parent;
        public bool IsStartOfObject;
        public bool IsEndOfObject;
        public bool IsBackFromNestedObject;
        public NdefTypeInfo TypeInfo;
        public object DeserializedInstance;
        public int DeserializedInstanceIndex;

        public bool IsHeaderEmpty
        {
            get
            {
                return TypeName == null && Kind == NdefObjectKind.Undefined &&
                    SerialKey  == null && LogicalKey == null;
            }
        }

        public bool IsUntypedList
        {
            get
            {
                return Kind == NdefObjectKind.List && string.IsNullOrEmpty(TypeName) &&
                    SerialKey == null && LogicalKey == null;
            }
        }

        public void Clear()
        {
            TypeName = default(string);
            Kind = default(NdefObjectKind);
            SerialKey = default(string);
            LogicalKey = default(string);
            CurrentLine = default(NdefLine);
            Parent = default(NdefObject);
            IsStartOfObject = default(bool);
            IsEndOfObject = default(bool);
            DeserializedInstance = default(object);
        }

        public override string ToString()
        {
            if (LogicalKey != null)
                return string.Format("{0} {1} {2}{3}", NdefConst.ObjectStartMarker, TypeName, NdefConst.LogicalKeyPrefix, LogicalKey);
            else
                return string.Format("{0} {1}", NdefConst.ObjectStartMarker, TypeName);
        }
    }

    public struct NdefLine
    {
        public NdefField Field;
        public NdefValue Value;

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
    }

    public struct NdefValue
    {
        public static readonly NdefValue UndefinedValue = new NdefValue();
        public static readonly NdefValue NullValue = new NdefValue() { AsSerialKey = string.Empty };

        public string ActualSerializableTypeName;
        public NdefValueUpdateMode ValueUpdateMode;
        public string AsScalar; // ==> NdefValueKind.Value
        public string AsSerialKey; // ==> NdefValueKind.Reference
        public string AsLogicalKey; // ==> NdefValueKind.Reference
        public NdefObject AsNestedObjectToDeserialize; // ==> NdefValueKind.ObjectOrList
        public object AsNestedObjectToSerialize; // ==> NdefValueKind.ObjectOrList
        public override string ToString() { return AsScalar; }

        public NdefValueKind Kind
        {
            get
            {
                NdefValueKind result;
                if (AsNestedObjectToSerialize != null || AsNestedObjectToDeserialize != null)
                    result = NdefValueKind.Object;
                else if ((AsSerialKey != null || AsLogicalKey != null) && !IsNull)
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
                return AsScalar == null && AsSerialKey == null && AsLogicalKey == null &&
                    AsNestedObjectToSerialize == null && AsNestedObjectToDeserialize == null;
            }
        }

        public bool IsNull
        {
            get
            {
                return AsSerialKey == NullValue.AsSerialKey;
            }
        }

        public bool IsUntypedList
        {
            get
            {
                return Kind == NdefValueKind.Object && ActualSerializableTypeName == string.Empty &&
                    AsSerialKey == null && AsLogicalKey == null;
            }
        }
    }

    public enum NdefValueKind
    {
        Scalar = 0,
        Reference = 1,
        Object = 2 // включает в себя и списки (массивы)
    }

    public enum NdefValueUpdateMode
    {
        SetOrAddToList = 0,
        ResetOrRemoveFromList = 1
    }

    public enum NdefLinkingMode
    {
        OneWayLinkingAndOriginalOrder = 0,
        TwoWayLinkingAndNormalizedOrder, // normalized = sorted + distinct
        NoLinkingAndOriginalOrder
    }
}
