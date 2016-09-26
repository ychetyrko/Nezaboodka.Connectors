using System;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public class NdefDeserializer
    {
        // Fields
        private INdefIterator fIterator;
        private NdefLinker fLinker;
        private bool fIgnoreUnknownFields;

        public NdefDeserializer(INdefIterator iterator, bool ignoreUnknownFields)
        {
            fIterator = iterator;
            fIgnoreUnknownFields = ignoreUnknownFields;
            fLinker = new NdefLinker();
        }

        public bool MoveNextBlock(out string header)
        {
            bool result = fIterator.MoveToNextBlockHeader();
            header = fIterator.CurrentBlock.Header;
            return result;
        }

        public IList<T> ReadBlock<T>(INdefTypeBinder typeBinder, NdefLinkingMode mode,
            bool skipOtherTypes) where T : class
        {
            var roots = new List<T>();
            foreach (var obj in ReadObjects(typeBinder))
            {
                T t = skipOtherTypes ? obj as T : (T)obj;
                if (t != null)
                    roots.Add(t);
            }
            fLinker.LinkObjectsAndReferences<T>(typeBinder, mode, skipOtherTypes);
            return roots;
        }

        public IEnumerable<T> ReadAllBlocks<T>(INdefTypeBinder typeBinder, NdefLinkingMode mode,
            bool skipOtherTypes) where T : class
        {
            string header;
            while (MoveNextBlock(out header))
                foreach (var x in ReadBlock<T>(typeBinder, mode, skipOtherTypes))
                    yield return x;
        }

        // Internal

        private IEnumerable<object> ReadObjects(INdefTypeBinder typeBinder)
        {
            while (fIterator.MoveToNextObjectHeader())
            {
                NdefObject o = fIterator.CurrentObject;
                ResolveObjectTypeInfo(typeBinder, o);
                fLinker.RegisterObject(typeBinder, o);
                IEnumerable<NdefLine> lines = ReadCurrentObjectButDeferReferences(typeBinder);
                o.TypeInfo.ObjectFormatter.FromNdefLines(o.DeserializedInstance, lines);
                if (o.IsEndOfObject && o.Parent == null)
                    yield return o.DeserializedInstance;
            }
        }

        private void ResolveObjectTypeInfo(INdefTypeBinder typeBinder, NdefObject p)
        {
            if (p.TypeInfo == null)
            {
                if (string.IsNullOrEmpty(p.TypeName))
                {
                    // Пытаемся определить тип данных через поле родительского объекта.
                    p.TypeInfo = typeBinder.LookupTypeInfoByField(p.Parent.TypeInfo,
                        p.Parent.CurrentLine.Field, p.Kind == NdefObjectKind.List);
                    if (p.Kind == NdefObjectKind.Object)
                        p.TypeName = p.TypeInfo.SerializableName;
                }
                else
                    p.TypeInfo = typeBinder.LookupTypeInfoByName(p.TypeName);
                if (p.Kind == NdefObjectKind.List && !p.TypeInfo.IsListType)
                    throw new FormatException("actual object kind does not match formal definition");
                if (p.TypeInfo.IsListType)
                    p.Kind = NdefObjectKind.List;
            }
        }

        private IEnumerable<NdefLine> ReadCurrentObjectButDeferReferences(INdefTypeBinder typeBinder)
        {
            NdefObject o = fIterator.CurrentObject;
            while (fIterator.MoveToNextFieldOrListItem())
            {
                NdefValue value = o.CurrentLine.Value;
                // TODO: To simplify by getting rid of IsEndOfObject check
                if (value.AsNestedObjectToDeserialize == null || value.AsNestedObjectToDeserialize.IsEndOfObject)
                {
                    if (value.Kind != NdefValueKind.Reference)
                    {
                        if (value.Kind != NdefValueKind.Object ||
                            value.AsNestedObjectToDeserialize == null ||
                            (string.IsNullOrEmpty(value.AsNestedObjectToDeserialize.LogicalKey) &&
                            string.IsNullOrEmpty(value.AsNestedObjectToDeserialize.SerialKey) &&
                            value.AsNestedObjectToDeserialize.IsEndOfObject))
                        {
                            yield return o.CurrentLine;
                        }
                    }
                    if (value.Kind != NdefValueKind.Scalar)
                    {
                        if (value.Kind != NdefValueKind.Object ||
                            (value.AsNestedObjectToDeserialize != null &&
                            (!string.IsNullOrEmpty(value.AsNestedObjectToDeserialize.LogicalKey) ||
                            !string.IsNullOrEmpty(value.AsNestedObjectToDeserialize.SerialKey))))
                        {
                            if (o.CurrentLine.Field.Name != null)
                                fLinker.RegisterReference(typeBinder, o, o.CurrentLine.Field, value);
                            else
                                fLinker.RegisterReference(typeBinder, o.Parent, o.Parent.CurrentLine.Field, value);
                        }
                    }
                }
            }
        }
    }
}
