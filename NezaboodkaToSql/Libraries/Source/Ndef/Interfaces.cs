using System;
using System.IO;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public interface INdefReader
    {
        NdefDataSet CurrentDataSet { get; }
        NdefObject CurrentObject { get; }
        NdefElement CurrentElement { get; } //TODO: Сделать NdefElement классом. Сейчас обращение CurrentElement возвращает огромную структуру как значение.
        bool MoveToNextDataSet();
        bool MoveToNextObject();
        bool MoveToNextElement();
    }

    public interface INdefWriter
    {
        void WriteDataSetStart(string header, bool isExtension, bool noBraces);
        void WriteDataSetEnd(bool noBraces);
        void WriteObjectStart(string type, string key, string number, bool noBraces);
        void WriteObjectEnd(bool noBraces);
        void WriteFieldName(string name); // string.IsNullOrEmpty(name) означает поле без имени, объект как целое
        void WriteListItem(bool isRemoved);
        void WriteValue(string type, string value, bool hasNoLineFeeds);
        void WriteReference(string key, string number);
        Stream WriteBinaryData(long length);
        void Flush();
    }

    public interface INdefTypeBinder
    {
        Type RootTypeForObjectsWithKey { get; }
        Type PreferredListType { get; }
        NdefTypeInfo LookupTypeInfo(object obj);
        NdefTypeInfo LookupTypeInfoByType(Type type);
        NdefTypeInfo LookupTypeInfoByName(string typeName);
        NdefTypeInfo LookupTypeInfoByField(NdefTypeInfo ndefTypeInfo, NdefField ndefField, bool adjustToActualType, out Type formalType);
    }

    public interface INdefFormatter
    {
        INdefFormatter<object> Boxed { get; }
        Type FormalType { get; }
        string SerializableTypeName { get; }
        void Initialize(INdefTypeBinder typeBinder, CodeGenerator codegen);
        void Configure(INdefTypeBinder typeBinder, CodeGenerator codegen);
    }

    public interface INdefFormatter<T> : INdefFormatter
    {
        NdefValue ToNdefValue(Type formalType, T value);
        T FromNdefValue(Type formalType, NdefValue value);
        IEnumerable<NdefElement> ToNdefElements(T obj, int[] fieldNumbers);
        void FromNdefElements(T obj, IEnumerable<NdefElement> elements);
        T CreateObjectInstance(Type formalType, NdefObjectHeader objectHeader);
    }
}
