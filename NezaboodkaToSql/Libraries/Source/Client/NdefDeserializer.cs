using System;
using System.IO;
using System.Collections.Generic;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public class NdefDeserializer
    {
        private NdefReader fReader;
        private NdefLinker fLinker;
        private NdefStream fStream;

        // Public

        public string DataSetHeader { get { return fReader.CurrentDataSet.Header; } }
        public ClientTypeBinder TypeBinder { get { return fLinker.TypeBinder; } }
        public object CurrentObject { get { return fReader.CurrentObject.DeserializedInstance; } }
        public NdefStream CurrentStream { get { return fStream; } }
        public GenerateObjectKeyDelegate GenerateObjectKeyDelegate
        {
            get { return fLinker.GenerateObjectKeyDelegate; }
            set { fLinker.GenerateObjectKeyDelegate = value; }
        }

        public NdefDeserializer(Stream stream, NdefLinkingMode linkingMode)
        {
            fReader = new NdefReader(stream);
            fLinker = new NdefLinker(linkingMode);
        }

        public bool MoveToNextDataSet()
        {
            fStream = null;
            bool result = fReader.MoveToNextDataSet();
            if (!string.IsNullOrEmpty(fReader.CurrentDataSet.Header))
                fLinker.Clear();
            return result;
        }

        public void SetTypeBinder(ClientTypeBinder typeBinder)
        {
            fLinker.TypeBinder = typeBinder;
        }

        public List<object> ReadObjects()
        {
            fStream = null;
            var result = new List<object>();
            foreach (var obj in WalkObjects())
                result.Add(obj);
            fLinker.LinkObjectsAndReferences();
            return result;
        }

        public bool MoveToNextObject()
        {
            if (fStream != null)
            {
                var buffer = new byte[4096];
                while (fStream.Read(buffer, 0, buffer.Length) > 0) { }
                fStream.Close();
            }
            fStream = null;
            bool result = false;
            if (fReader.MoveToNextObject())
            {
                NdefObject o = fReader.CurrentObject;
                fLinker.FindObject(o);
                fReader.MoveToNextElement();
                if (fReader.CurrentElement.Value.AsStream != null)
                    fStream = new NdefStream(fReader);
                result = true;
            }
            return result;
        }

        // Internal

        private IEnumerable<object> WalkObjects()
        {
            while (fReader.MoveToNextObject())
            {
                NdefObject o = fReader.CurrentObject;
                ResolveObjectTypeInfoAndRegisterObject(o);
                IEnumerable<NdefElement> elements = WalkObjectButDeferReferences(o);
                o.Header.TypeInfo.Formatter.Boxed.FromNdefElements(o.DeserializedInstance, elements);
                if (o.IsEndOfObject && o.Parent == null)
                    yield return o.DeserializedInstance;
            }
        }

        private void ResolveObjectTypeInfoAndRegisterObject(NdefObject o)
        {
            if (o.Header.TypeInfo == null)
            {
                Type formalType;
                if (string.IsNullOrEmpty(o.Header.TypeName) || o.Header.TypeName == NdefConst.ListTypeBraces)
                {
                    // Пытаемся определить тип данных через поле родительского объекта.
                    o.Header.TypeInfo = fLinker.TypeBinder.LookupTypeInfoByField(o.Parent.Header.TypeInfo,
                        o.Parent.CurrentElement.Field, o.Header.Kind == NdefObjectKind.List, out formalType);
                    if (o.Header.Kind == NdefObjectKind.Object)
                        o.Header.TypeName = o.Header.TypeInfo.SerializableName;
                }
                else
                {
                    o.Header.TypeInfo = fLinker.TypeBinder.LookupTypeInfoByName(o.Header.TypeName);
                    formalType = o.Header.TypeInfo.SystemType;
                }
                if (o.Header.Kind == NdefObjectKind.List && !o.Header.TypeInfo.IsListType)
                    throw new FormatException("actual object kind does not match formal definition");
                if (o.Header.TypeInfo.IsListType)
                    o.Header.Kind = NdefObjectKind.List;
                fLinker.RegisterObject(formalType, o);
            }
        }

        private IEnumerable<NdefElement> WalkObjectButDeferReferences(NdefObject o)
        {
            while (fReader.MoveToNextElement())
            {
                NdefValue value = o.CurrentElement.Value;
                // TODO: To simplify by getting rid of IsEndOfObject check
                if (value.AsNestedObjectToDeserialize == null || value.AsNestedObjectToDeserialize.IsEndOfObject)
                {
                    if (value.Kind != NdefValueKind.Reference)
                    {
                        if (value.Kind != NdefValueKind.Object ||
                            value.AsNestedObjectToDeserialize == null ||
                            (string.IsNullOrEmpty(value.AsNestedObjectToDeserialize.Header.Key) &&
                            string.IsNullOrEmpty(value.AsNestedObjectToDeserialize.Header.Number) &&
                            value.AsNestedObjectToDeserialize.IsEndOfObject))
                        {
                            yield return o.CurrentElement;
                        }
                    }
                    if (value.Kind != NdefValueKind.Scalar)
                    {
                        if (value.Kind != NdefValueKind.Object ||
                            (value.AsNestedObjectToDeserialize != null &&
                            (!string.IsNullOrEmpty(value.AsNestedObjectToDeserialize.Header.Key) ||
                            !string.IsNullOrEmpty(value.AsNestedObjectToDeserialize.Header.Number))))
                        {
                            if (o.CurrentElement.Field.Name != null)
                                fLinker.RegisterReference(o, o.CurrentElement.Field, value);
                            else
                                fLinker.RegisterReference(o.Parent, o.Parent.CurrentElement.Field, value);
                        }
                    }
                }
            }
        }
    }
}
