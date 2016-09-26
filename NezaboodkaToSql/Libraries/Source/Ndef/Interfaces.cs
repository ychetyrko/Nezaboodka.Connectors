using System;
using System.IO;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public interface INdefIterator
    {
        NdefBlock CurrentBlock { get; }
        NdefObject CurrentObject { get; }
        bool MoveToNextBlockHeader();
        bool MoveToNextObjectHeader();
        bool MoveToNextFieldOrListItem();
        Stream OpenReadBlockFooter(); // returns null if no binary data
    }

    public interface INdefWriter
    {
        void WriteBlockHeader(string header);
        void WriteObjectHeader(string type, NdefObjectKind kind, string serialKey, string logicalKey);
        void WriteFieldNameAndValue(string name, string explicitTypeName, string scalar, string serialKey, string logicalKey);
        void WriteFieldNameForNestedObjectOrList(string name);
        void WriteListItem(NdefValueUpdateMode valueUpdateMode, string explicitTypeName, string scalar, string serialKey, string logicalKey);
        void WriteObjectFooter();
        Stream OpenWriteBlockFooter(long length);
    }

    public interface INdefTypeBinder
    {
        Type RootTypeForObjectsWithKey { get; }
        Type PreferredListType { get; }
        NdefTypeInfo LookupTypeInfo(object obj);
        NdefTypeInfo LookupTypeInfoByType(Type type);
        NdefTypeInfo LookupTypeInfoByName(string typeName);
        NdefTypeInfo LookupTypeInfoByField(NdefTypeInfo ndefTypeInfo, NdefField ndefField, bool adjustToActualType);
        object CreateObject(NdefTypeInfo typeInfo, int typeNumber, string typeName, string key);
        long GetObjectLogicalId(string key);
        string GetObjectKeyFromLogicalId(long logicalId);
        string GetObjectKey(object obj);
        bool IsStubObject(object obj);
        void MarkAsStubObject(object obj);
        bool IsImplicitObject(object obj);
        void MarkAsImplicitObject(object obj);
        NdefField GetBackReferenceField(NdefTypeInfo ndefTypeInfo, NdefField ndefField,
            out bool isList, out NdefTypeInfo fieldTypeInfo);
    }

    public interface INdefObjectFormatter
    {
        IEnumerable<NdefLine> ToNdefLines(object obj, int[] fieldNumbers);
        void FromNdefLines(object obj, IEnumerable<NdefLine> lines);
    }

    public interface INdefValueFormatter
    {
        Type TypeOfValue { get; }
        string SerializableTypeName { get; }
        NdefValue AnyToNdefValue(Type formalType, object value);
        object AnyFromNdefValue(Type formalType, NdefValue value);
    }

    public interface INdefValueFormatter<T> : INdefValueFormatter
    {
        NdefValue ToNdefValue(Type formalType, T value);
        T FromNdefValue(Type formalType, NdefValue value);
    }

    public static class NdefConst
    {
        public const char BlockStartMarker = '@';
        public const char BlockEndMarker = '.';
        public const char ObjectStartMarker = '>';
        public const char ObjectEndMarker = '<';
        public const char SerialKeyPrefix = '@';
        public const char LogicalKeyPrefix = '#';
        public const char ExplicitTypeNamePrefix = '`';
        public const char ValueSeparator = ':';
        public const char QuotationMarker = '|';
        public const char ListItemMarker = '*';
        public const char ListItemToRemoveMarker = '~';
        public const char CommentMarker = '/';
    }
}
