using System.Collections;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public class NdefSerializer : NdefIterator
    {
        // Fields
        private IEnumerator fWalker;
        private Dictionary<object, long> fVisitedObjects;
        private Queue<NdefValue> fQueuedObjects;

        // Public

        public INdefTypeBinder TypeBinder { get; private set; }
        public bool StepIntoNestedDatabaseObjects { get; private set; }

        public NdefSerializer(INdefTypeBinder typeBinder, bool stepIntoNestedDatabaseObjects, IEnumerable objects)
            : base()
        {
            TypeBinder = typeBinder;
            StepIntoNestedDatabaseObjects = stepIntoNestedDatabaseObjects;
            fVisitedObjects = new Dictionary<object, long>();
            fQueuedObjects = new Queue<NdefValue>();
            fWalker = Walk(objects.GetEnumerator()).GetEnumerator();
        }

        protected override bool NeedMoreData()
        {
            return fWalker.MoveNext();
        }

        // Internal

        private IEnumerable<bool> Walk(IEnumerator roots)
        {
            NdefValue p = MoveToNextObject(roots);
            while (!p.IsUndefined)
            {
                foreach (bool x in WalkObject(false, p, true))
                    yield return x;
                p = MoveToNextObject(roots);
            }
        }

        private IEnumerable<bool> WalkObject(bool isListItem, NdefValue obj, bool stepIntoNestedDbObjects)
        {
            NdefTypeInfo typeInfo = TypeBinder.LookupTypeInfo(obj.AsNestedObjectToSerialize);
            if (!StepIntoNestedDatabaseObjects && stepIntoNestedDbObjects)
                stepIntoNestedDbObjects = !TypeBinder.RootTypeForObjectsWithKey.IsAssignableFrom(typeInfo.SystemType);
            if (obj.AsSerialKey == null && !obj.IsUntypedList)
                AcquireSerialKeyAndLogicalKey(obj.AsNestedObjectToSerialize, true, false,
                    out obj.AsSerialKey, out obj.AsLogicalKey);
            if (obj.ActualSerializableTypeName == null) // Note: string.Empty is treated as untyped list!
                obj.ActualSerializableTypeName = typeInfo.SerializableName;
            NdefObjectKind kind = typeInfo.IsListType ? NdefObjectKind.List : NdefObjectKind.Object;
            PutObjectStartToBuffer(isListItem, obj.ActualSerializableTypeName, kind, obj.AsSerialKey, obj.AsLogicalKey);
            yield return true; // return control back to caller
            IEnumerable<NdefLine> lines = typeInfo.ObjectFormatter.ToNdefLines(obj.AsNestedObjectToSerialize, null);
            foreach (NdefLine line in lines)
            {
                if (!line.Value.IsUndefined)
                {
                    if (line.Field.Name != null) // обычное поле, не элемента массива
                    {
                        if (!line.Value.IsNull)
                        {
                            PutFieldToBuffer(line.Field.Name);
                            foreach (var t in WalkValue(false, line.Value, stepIntoNestedDbObjects))
                                yield return true; // return control back to caller
                        }
                    }
                    else
                    {
                        foreach (var t in WalkValue(true, line.Value, stepIntoNestedDbObjects))
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
                if (isListItem)
                    PutListItemToBuffer(value.ValueUpdateMode, value.ActualSerializableTypeName,
                        value.AsScalar, value.AsSerialKey, value.AsLogicalKey);
                else
                    PutValueToBuffer(value.ActualSerializableTypeName,
                        value.AsScalar, value.AsSerialKey, value.AsLogicalKey);
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
            bool isReference = !value.IsUntypedList &&
                AcquireSerialKeyAndLogicalKey(value.AsNestedObjectToSerialize, stepIntoNestedDbObjects, true,
                    out value.AsSerialKey, out value.AsLogicalKey);
            if (isReference)
            {
                // TODO: To support typed references
                if (isListItem)
                    PutListItemToBuffer(value.ValueUpdateMode, null, null, value.AsSerialKey, value.AsLogicalKey);
                else
                    PutValueToBuffer(null, null, value.AsSerialKey, value.AsLogicalKey);
                yield return true; // return control back to caller
            }
            else // in-place nested object
                foreach (bool x in WalkObject(isListItem, value, stepIntoNestedDbObjects))
                    yield return x; // return control back to caller on each element of nested object
        }

        private NdefValue MoveToNextObject(IEnumerator objects)
        {
            var result = NdefValue.UndefinedValue;
            if (fQueuedObjects.Count > 0)
                result = fQueuedObjects.Dequeue();
            while (result.IsUndefined && objects.MoveNext())
            {
                if (!fVisitedObjects.ContainsKey(objects.Current))
                    result = new NdefValue() { AsNestedObjectToSerialize = objects.Current };
            }
            return result;
        }

        private bool AcquireSerialKeyAndLogicalKey(object obj, bool stepIntoNestedDbObjects,
            bool allowEnqueue, out string serialKey, out string logicalKey)
        {
            logicalKey = TypeBinder.GetObjectKey(obj);
            bool result = logicalKey != null && (!stepIntoNestedDbObjects || TypeBinder.IsStubObject(obj));
            if (!result)
            {
                long serialKeyAsLong;
                result = fVisitedObjects.TryGetValue(obj, out serialKeyAsLong);
                if (!result)
                {
                    serialKeyAsLong = fVisitedObjects.Count;
                    fVisitedObjects.Add(obj, serialKeyAsLong);
                    result = !string.IsNullOrEmpty(logicalKey);
                    if (result && allowEnqueue)
                        fQueuedObjects.Enqueue(new NdefValue() { AsNestedObjectToSerialize = obj });
                }
                serialKey = serialKeyAsLong.ToString();
            }
            else
                serialKey = null;
            return result /*&& !string.IsNullOrEmpty(logicalKey)*/;
        }
    }
}
