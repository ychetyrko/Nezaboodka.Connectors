using System;
using System.Collections;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public class ObjectsReader : AbstractNdefReader
    {
        private IEnumerator fWalker;
        private Queue<NdefValue> fQueuedObjects;
        private INdefFormatter<object> fRootFormatter;

        // Public

        public INdefTypeBinder TypeBinder { get; private set; }
        public bool StepIntoNestedDbObjects { get; private set; }
        public Dictionary<object, long> VisitedObjects { get; private set; }

        public ObjectsReader(INdefTypeBinder typeBinder, bool stepIntoNestedDbObjects, IEnumerable objects)
            : base()
        {
            TypeBinder = typeBinder;
            StepIntoNestedDbObjects = stepIntoNestedDbObjects;
            VisitedObjects = new Dictionary<object, long>();
            fQueuedObjects = new Queue<NdefValue>();
            fRootFormatter = TypeBinder.LookupFormatter<INdefFormatter<object>>(typeof(object));
            fWalker = WalkObjects(objects.GetEnumerator()).GetEnumerator();
        }

        protected override bool MoveToNextLine()
        {
            return fWalker.MoveNext();
        }

        // Internal

        private IEnumerable<bool> WalkObjects(IEnumerator roots)
        {
            NdefValue p = MoveToNextObject(roots);
            while (!p.IsUndefined)
            {
                NdefTypeInfo typeInfo = TypeBinder.LookupTypeInfo(p.AsNestedObjectToSerialize);
                if (!p.IsUntypedList)
                    AcquireObjectNumberAndKey(typeInfo, p, true, false,
                        out p.AsObjectNumber, out p.AsObjectKey);
                foreach (bool x in WalkObject(false, typeInfo, p, true))
                    yield return x;
                p = MoveToNextObject(roots);
            }
        }

        private IEnumerable<bool> WalkObject(bool isListItem, NdefTypeInfo typeInfo, NdefValue obj,
            bool stepIntoNestedDbObjects)
        {
            if (!StepIntoNestedDbObjects && stepIntoNestedDbObjects)
                stepIntoNestedDbObjects = !TypeBinder.RootTypeForObjectsWithKey.IsAssignableFrom(typeInfo.SystemType);
            if (obj.ActualSerializableTypeName == null)
                obj.ActualSerializableTypeName = typeInfo.SerializableName;
            NdefObjectKind kind = typeInfo.IsListType ? NdefObjectKind.List : NdefObjectKind.Object;
            PutObjectStartToBuffer(isListItem, obj.ActualSerializableTypeName, kind, obj.AsObjectNumber, obj.AsObjectKey);
            yield return true; // return control back to caller
            INdefFormatter<object> formatter = typeInfo.Formatter.Boxed;
            IEnumerable<NdefElement> elements = formatter.ToNdefElements(obj.AsNestedObjectToSerialize, null);
            foreach (NdefElement element in elements)
            {
                if (!element.Value.IsUndefined)
                {
                    if (element.Field.Name != null) // обычное поле, не элемента массива
                    {
                        if (!element.Value.IsNull)
                        {
                            PutFieldNameToBuffer(element.Field.Name);
                            foreach (var t in WalkValue(false, element.Value, stepIntoNestedDbObjects))
                                yield return true; // return control back to caller
                        }
                    }
                    else
                    {
                        PutListItemToBuffer(element.Field.Kind == NdefFieldKind.Remove);
                        foreach (var t in WalkValue(true, element.Value, stepIntoNestedDbObjects))
                            yield return true; // return control back to caller
                    }
                }
            }
            PutObjectEndToBuffer();
            yield return true; // return control back to caller
        }

        private IEnumerable<bool> WalkValue(bool isListItem, NdefValue value, bool stepIntoNestedDbObjects)
        {
            if (value.Kind != NdefValueKind.Object)
            {
                PutValueToBuffer(value.ActualSerializableTypeName,
                    value.AsScalar, value.AsObjectNumber, value.AsObjectKey);
                yield return true; // return control back to caller
            }
            else // value or reference
            {
                foreach (bool t in WalkNestedObject(isListItem, value, stepIntoNestedDbObjects))
                    yield return true; // return control back to caller
            }
        }

        private IEnumerable<bool> WalkNestedObject(bool isListItem, NdefValue value, bool stepIntoNestedDbObjects)
        {
            NdefTypeInfo typeInfo = TypeBinder.LookupTypeInfo(value.AsNestedObjectToSerialize);
            bool isReference = !value.IsUntypedList &&
                AcquireObjectNumberAndKey(typeInfo, value,
                    stepIntoNestedDbObjects, true, out value.AsObjectNumber, out value.AsObjectKey);
            if (isReference)
            {
                // TODO: To support typed references
                PutValueToBuffer(null, null, value.AsObjectNumber, value.AsObjectKey);
                yield return true; // return control back to caller
            }
            else // in-place nested object
                foreach (bool x in WalkObject(isListItem, typeInfo, value, stepIntoNestedDbObjects))
                    yield return x; // return control back to caller on each element of nested object
        }

        private NdefValue MoveToNextObject(IEnumerator objects)
        {
            var result = NdefValue.UndefinedValue;
            if (fQueuedObjects.Count > 0)
                result = fQueuedObjects.Dequeue();
            while (result.IsUndefined && objects.MoveNext())
            {
                if (!VisitedObjects.ContainsKey(objects.Current))
                {
                    result = fRootFormatter.ToNdefValue(typeof(object), objects.Current);
                    if (result.Kind != NdefValueKind.Object)
                        throw new FormatException("root item must be an object");
                }
            }
            return result;
        }

        private bool AcquireObjectNumberAndKey(NdefTypeInfo typeInfo, NdefValue obj,
            bool stepIntoNestedDbObjects, bool allowEnqueue, out string number, out string key)
        {
            key = obj.AsObjectKey;
            bool result = key != null && (!stepIntoNestedDbObjects || obj.AsNestedObjectToSerialize == null);
            if (!result)
            {
                long numberAsLong;
                result = VisitedObjects.TryGetValue(obj.AsNestedObjectToSerialize, out numberAsLong);
                if (!result)
                {
                    numberAsLong = VisitedObjects.Count;
                    VisitedObjects.Add(obj.AsNestedObjectToSerialize, numberAsLong);
                    result = !string.IsNullOrEmpty(key);
                    if (result && allowEnqueue)
                        fQueuedObjects.Enqueue(obj);
                }
                number = numberAsLong.ToString();
            }
            else
                number = null;
            return result /*&& !string.IsNullOrEmpty(key)*/;
        }
    }
}
