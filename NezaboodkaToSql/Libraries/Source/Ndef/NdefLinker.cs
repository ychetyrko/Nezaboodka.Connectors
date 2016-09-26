using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Nezaboodka.Ndef
{
    public class NdefLinker
    {
        // Fields
        private List<NdefInstanceInfo> fObjects;
        private List<NdefRelation> fRelations;
        private NdefRelationParty fCurrentSource;

        // Public

        public NdefLinker()
        {
            fObjects = new List<NdefInstanceInfo>();
            fRelations = new List<NdefRelation>();
            fCurrentSource = null;
        }

        public void RegisterObject(INdefTypeBinder typeBinder, NdefObject ndefObject)
        {
            if (ndefObject.DeserializedInstance == null)
            {
                ndefObject.DeserializedInstanceIndex = AcquireInstanceIndex();
                ndefObject.DeserializedInstance = typeBinder.CreateObject(
                    ndefObject.TypeInfo, ndefObject.TypeInfo.TypeNumber,
                    ndefObject.TypeInfo.SerializableName, ndefObject.LogicalKey);
                fObjects[ndefObject.DeserializedInstanceIndex] = new NdefInstanceInfo() { RuntimeObject = ndefObject.DeserializedInstance };
                DoRegisterObject(typeBinder, ndefObject);
            }
        }

        public void RegisterReference(INdefTypeBinder typeBinder,
            NdefObject ndefObject, NdefField ndefField, NdefValue reference)
        {
            long logicalId = typeBinder.GetObjectLogicalId(ndefObject.LogicalKey);
            long serialKey = long.MaxValue;
            if (!string.IsNullOrEmpty(ndefObject.SerialKey))
                serialKey = long.Parse(ndefObject.SerialKey);
            if (fCurrentSource == null ||
                logicalId != fCurrentSource.ObjectLogicalId ||
                serialKey != fCurrentSource.ObjectSerialKey ||
                ndefField.Name != fCurrentSource.Field.Name ||
                (logicalId == 0 && ndefObject.DeserializedInstanceIndex != fCurrentSource.ObjectIndex))
            {
                fCurrentSource = new NdefRelationParty()
                {
                    ObjectSerialKey = serialKey,
                    ObjectLogicalId = logicalId,
                    ObjectIndex = ndefObject.DeserializedInstanceIndex,
                    ObjectTypeInfo = ndefObject.TypeInfo,
                    Field = ndefField,
                    IsListInMemberInfo = ndefObject.TypeInfo.IsListType
                };
            }
            long targetSerialKey = long.MaxValue;
            long targetLogicalId = 0;
            if (reference.Kind == NdefValueKind.Reference)
            {
                if (!string.IsNullOrEmpty(reference.AsSerialKey))
                    targetSerialKey = long.Parse(reference.AsSerialKey);
                if (!string.IsNullOrEmpty(reference.AsLogicalKey))
                    targetLogicalId = typeBinder.GetObjectLogicalId(reference.AsLogicalKey);
            }
            else if (reference.Kind == NdefValueKind.Object)
            {
                if (!string.IsNullOrEmpty(reference.AsNestedObjectToDeserialize.SerialKey))
                    targetSerialKey = long.Parse(reference.AsNestedObjectToDeserialize.SerialKey);
                if (!string.IsNullOrEmpty(reference.AsNestedObjectToDeserialize.LogicalKey))
                    targetLogicalId = typeBinder.GetObjectLogicalId(reference.AsNestedObjectToDeserialize.LogicalKey);
            }
            var target = new NdefRelationParty()
            {
                ObjectSerialKey = targetSerialKey,
                ObjectLogicalId = targetLogicalId,
                ObjectIndex = -1 // to be resolved by SetupObjectNumbersAndCreateStubObjects
            };
            // TODO: Switch to typeof(SaveQuery)
            if (ndefField.Name != null && (ndefObject.TypeName == "N.SaveQuery" ||
                ndefObject.TypeName == "N.UnitTests.NdefTestSaveQuery") && ndefField.Name == "InObjects")
                target.TwoWayLinkingObjectList = ndefObject.CurrentLine.Value.AsNestedObjectToDeserialize.DeserializedInstance as IList;
            target.Field = typeBinder.GetBackReferenceField(ndefObject.TypeInfo, ndefField,
                out target.IsListInMemberInfo, out target.ObjectTypeInfo);
            var relation = new NdefRelation()
            {
                NaturalOrderNumber = fRelations.Count,
                Source = fCurrentSource,
                Target = target,
                ValueUpdateMode = reference.ValueUpdateMode,
                IsImplicitBackRelation = false
            };
            var implicitBackRelation = new NdefRelation()
            {
                NaturalOrderNumber = fRelations.Count + 1,
                Source = target,
                Target = fCurrentSource,
                ValueUpdateMode = reference.ValueUpdateMode,
                IsImplicitBackRelation = true
            };
#if DEBUG
            relation.DebugHint = string.Format("{0}@{1} →   #{2}@{3} ({4}.{5})",
                ndefObject.LogicalKey != null ? "#" + ndefObject.LogicalKey : "", ndefObject.SerialKey,
                reference.AsLogicalKey ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.LogicalKey : ""),
                reference.AsSerialKey ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.SerialKey : ""),
                ndefObject.TypeName, ndefObject.CurrentLine.Field.Name);
            implicitBackRelation.DebugHint = target.Field.Name != null ?
                string.Format("#{0}@{1}   ← #{2}@{3} ({4}.{5}) ",
                    reference.AsLogicalKey ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.LogicalKey : ""),
                    reference.AsSerialKey ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.SerialKey : ""),
                    ndefObject.LogicalKey, ndefObject.SerialKey,
                    target.ObjectTypeInfo.SerializableName, target.Field.Name) :
                string.Format("#{0}@{1}   ← #{2}@{3} ({4}.{5})",
                reference.AsLogicalKey ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.LogicalKey : ""),
                reference.AsSerialKey ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.SerialKey : ""),
                    ndefObject.LogicalKey, ndefObject.SerialKey,
                    ndefObject.TypeName, ndefObject.CurrentLine.Field.Name);
#endif
            fRelations.Add(relation);
            fRelations.Add(implicitBackRelation);
        }

        public void LinkObjectsAndReferences<T>(INdefTypeBinder typeBinder,
            NdefLinkingMode mode, bool skipOtherTypes) where T: class
        {
            //string pairsBeforeSorting = string.Concat((IEnumerable<string>)fLinkPairs.ConvertAll((LinkPair x) => x.DebugHint + "\n"));
            switch (mode)
            {
                case NdefLinkingMode.OneWayLinkingAndOriginalOrder:
                    fRelations.Sort(LinkPairOriginalOrderComparer.Default);
                    break;
                case NdefLinkingMode.TwoWayLinkingAndNormalizedOrder:
                    fRelations.Sort(LinkPairSortedOrderComparer.Default);
                    break;
                default:
                    throw new NotImplementedException(string.Format("{0} is not implemented", mode));
            }
            //string pairsAfterSorting = string.Concat((IEnumerable<string>)fLinkPairs.ConvertAll((LinkPair x) => x.DebugHint + "\n"));
            SetupObjectNumbersAndCreateStubObjects(typeBinder, mode);
            FillReferenceFields<T>(typeBinder, mode, skipOtherTypes);
            // Зачистка связей перед переключением на следующий N*DEF блок.
            fCurrentSource = null;
            fRelations.Clear();
            fObjects.Clear();
        }

        // Internal

        private void DoRegisterObject(INdefTypeBinder typeBinder, NdefObject ndefObject)
        {
            long logicalId = typeBinder.GetObjectLogicalId(ndefObject.LogicalKey);
            long serialKey = long.MaxValue;
            if (!string.IsNullOrEmpty(ndefObject.SerialKey))
                serialKey = long.Parse(ndefObject.SerialKey);
            var source = new NdefRelationParty()
            {
                ObjectSerialKey = serialKey,
                ObjectLogicalId = logicalId,
                ObjectIndex = ndefObject.DeserializedInstanceIndex,
                ObjectTypeInfo = ndefObject.TypeInfo,
                Field = default(NdefField),
                IsListInMemberInfo = false
            };
            var relation = new NdefRelation()
            {
                NaturalOrderNumber = fRelations.Count,
                Source = source,
                Target = null, // indicates that relation is an object, but not an actual relation between two objects
                IsImplicitBackRelation = false
            };
            if (ndefObject.Parent == null) // is object is root
                relation.ValueUpdateMode = NdefValueUpdateMode.SetOrAddToList;
            else
                relation.ValueUpdateMode = NdefValueUpdateMode.ResetOrRemoveFromList;
#if DEBUG
            relation.DebugHint = ndefObject.Kind != NdefObjectKind.List ?
                string.Format("* {0} {1}@{2}",
                    ndefObject.TypeName, ndefObject.LogicalKey != null ? "#" + ndefObject.LogicalKey : "",
                    ndefObject.SerialKey) :
                string.Format("[] {0}{1}@{2}.{3}",
                    ndefObject.Parent.TypeName, ndefObject.Parent.LogicalKey != null ? "#" + ndefObject.Parent.LogicalKey : "",
                    ndefObject.Parent.SerialKey, ndefObject.Parent.CurrentLine.Field.Name);
#endif
            fRelations.Add(relation);
        }

        private int AcquireInstanceIndex()
        {
            int result = fObjects.Count;
            fObjects.Add(new NdefInstanceInfo());
            return result;
        }

        private void SetMemberValue(object obj, MemberInfo member, object value)
        {
            var list = value as IList;
            if (list != null)
            {
                Type type = NdefUtils.GetMemberType(member);
                if (type.IsArray)
                {
                    value = type.GetConstructor(new Type[] { typeof(int) }).Invoke(new object[] { list.Count });
                    list.CopyTo((Array)value, 0);
                }
                //else if (!type.IsInterface)
                //{
                //    value = type.GetConstructor(new Type[] { typeof(int) }).Invoke(new object[] { list.Count });
                //    var target = (IList)value;
                //    foreach (object x in list)
                //        target.Add(x);
                //}
                //else if (Binder.PatchListType.IsArray)
                //{
                //    value = Binder.PatchListType.GetConstructor(new Type[] { typeof(int) }).Invoke(
                //        new object[] { list.Count });
                //    list.CopyTo((Array)value, 0);
                //}
                else
                    value = list;
            }
            NdefUtils.SetMemberValue(obj, member, value);
        }

        private object CreateNewObject(INdefTypeBinder typeBinder, int objectNumber, Type type, string key)
        {
            NdefTypeInfo ndefTypeInfo = typeBinder.LookupTypeInfoByType(type);
            object result = typeBinder.CreateObject(ndefTypeInfo, ndefTypeInfo.TypeNumber,
                ndefTypeInfo.SerializableName, key);
            fObjects[objectNumber] = new NdefInstanceInfo() { RuntimeObject = result };
            return result;
        }

        private void SetupObjectNumbersAndCreateStubObjects(INdefTypeBinder typeBinder, NdefLinkingMode mode)
        {
            NdefRelationParty current = null;
            Type typeOfObjectToCreate = null;
            for (int i = 0; i < fRelations.Count; ++i)
            {
                NdefRelation x = fRelations[i];
                if (current == null || x.Source.ObjectLogicalId != current.ObjectLogicalId ||
                    (x.Source.ObjectSerialKey != current.ObjectSerialKey && x.Source.ObjectSerialKey != long.MaxValue) ||
                    (x.Source.ObjectLogicalId == 0 && x.Source.ObjectSerialKey == long.MaxValue &&
                    x.Source.ObjectIndex != current.ObjectIndex))
                {
                    if (typeOfObjectToCreate != null)
                    {
                        object t = CreateNewObject(typeBinder, current.ObjectIndex, typeOfObjectToCreate,
                            typeBinder.GetObjectKeyFromLogicalId(current.ObjectLogicalId)); // TODO: how to avoid ToString?
                        typeBinder.MarkAsStubObject(t);
                    }
                    // Switch to a new current master
                    current = x.Source;
                    if (current.ObjectIndex < 0) // implicit back reference
                    {
                        Type type;
                        if (x.Source.Field.Name == null)
                        {
                            // Infer object type via back reference field
                            NdefTypeInfo fieldTypeInfo = typeBinder.LookupTypeInfoByField(
                                x.Target.ObjectTypeInfo, x.Target.Field, false);
                            type = fieldTypeInfo.SystemType;
                            type = NdefUtils.GetElementType(type, type);
                        }
                        else
                            type = x.Source.ObjectTypeInfo.SystemType;
                        if (type.IsSubclassOf(typeBinder.RootTypeForObjectsWithKey))
                            typeOfObjectToCreate = type;
                        else
                            typeOfObjectToCreate = typeBinder.RootTypeForObjectsWithKey;
                        current.ObjectIndex = AcquireInstanceIndex();
                    }
                    else
                        typeOfObjectToCreate = null;
                }
                else
                {
                    if (typeOfObjectToCreate != null)
                    {
                        if (x.Source.Field.Name != null && x.Source.ObjectIndex < 0)
                        {
                            Type type = x.Source.ObjectTypeInfo.SystemType;
                            if (type.IsSubclassOf(typeOfObjectToCreate))
                                typeOfObjectToCreate = type;
                            else if (type != typeOfObjectToCreate && !typeOfObjectToCreate.IsSubclassOf(type))
                                throw new FormatException(string.Format(
                                    "object with key {0} has ambiguous type: {1} or {2}",
                                    typeBinder.GetObjectKeyFromLogicalId(x.Source.ObjectLogicalId),
                                    type.FullName, typeOfObjectToCreate.FullName));
                        }
                    }
                    else
                    {
                        if (x.Target == null)
                            // TODO: Objects[currentObject.ObjectNumber].PatchFrom(Objects[x.ObjectNumber]);
                            throw new NotImplementedException("object duplicates within same N*DEF block are not implemented");
                    }
                    x.Source.ObjectIndex = current.ObjectIndex;
                    if (x.Source.TwoWayLinkingObjectList != null)
                    {
                        NdefInstanceInfo t = fObjects[x.Source.ObjectIndex];
                        t.TwoWayLinkingObjectList = x.Source.TwoWayLinkingObjectList;
                        fObjects[x.Source.ObjectIndex] = t;
                    }
                }
            }
            if (typeOfObjectToCreate != null)
            {
                object t = CreateNewObject(typeBinder, current.ObjectIndex, typeOfObjectToCreate,
                    typeBinder.GetObjectKeyFromLogicalId(current.ObjectLogicalId)); // TODO: how to avoid ToString?
                typeBinder.MarkAsStubObject(t);
            }
        }

        private void FillReferenceFields<T>(INdefTypeBinder typeBinder, NdefLinkingMode mode,
            bool skipOtherTypes) where T : class
        {
            for (int i = 0; i < fRelations.Count; ++i)
            {
                NdefRelation x = fRelations[i];
                if (x.Target != null)
                {
                    if (x.Target.ObjectLogicalId != 0 || x.Target.ObjectSerialKey != long.MaxValue)
                    {
                        if (!x.IsImplicitBackRelation || (x.Target.ObjectLogicalId != 0 &&
                            mode == NdefLinkingMode.TwoWayLinkingAndNormalizedOrder))
                        {
                            NdefInstanceInfo source = fObjects[x.Source.ObjectIndex];
                            object sourceObject = source.RuntimeObject;
                            NdefInstanceInfo target = fObjects[x.Target.ObjectIndex];
                            object targetObject = target.RuntimeObject;

                            if (x.Source.ObjectLogicalId > 0 && x.Target.ObjectLogicalId > 0 && // TODO: Проверить LogicalId > 0
                                ((source.TwoWayLinkingObjectList != null) != (target.TwoWayLinkingObjectList != null)))
                            {
                                if (source.TwoWayLinkingObjectList != null)
                                {
                                    if (target.RuntimeObjectStub == null)
                                    {
                                        targetObject = CreateNewObject(typeBinder, x.Target.ObjectIndex, targetObject.GetType(),
                                            typeBinder.GetObjectKeyFromLogicalId(x.Target.ObjectLogicalId));
                                        typeBinder.MarkAsImplicitObject(targetObject);
                                        target.RuntimeObjectStub = targetObject;
                                        fObjects[x.Target.ObjectIndex] = target;
                                        source.TwoWayLinkingObjectList.Add(targetObject);
                                    }
                                    else
                                        targetObject = target.RuntimeObjectStub;
                                }
                                else
                                {
                                    if (source.RuntimeObjectStub == null)
                                    {
                                        sourceObject = CreateNewObject(typeBinder, x.Source.ObjectIndex, sourceObject.GetType(),
                                            typeBinder.GetObjectKeyFromLogicalId(x.Source.ObjectLogicalId));
                                        typeBinder.MarkAsImplicitObject(sourceObject);
                                        source.RuntimeObjectStub = sourceObject;
                                        fObjects[x.Source.ObjectIndex] = source;
                                        target.TwoWayLinkingObjectList.Add(sourceObject);
                                    }
                                    else
                                        sourceObject = source.RuntimeObjectStub;
                                }
                            }

                            if (x.Source.Field.Name != null)
                            {
                                Type type = x.Source.ObjectTypeInfo.SystemType;
                                MemberInfo member = type.GetField(x.Source.Field.Name);
                                if (member != null)
                                {
                                    //o.TypeInfo.DefaultFieldMask.SetObjectFieldOrListItem(o.RuntimeObject, f);
                                    bool listCreated;
                                    IList list = NdefUtils.TryAcquireList(sourceObject, member, x.Source.IsListInMemberInfo,
                                        typeBinder.PreferredListType, typeBinder.RootTypeForObjectsWithKey, out listCreated);
                                    if (list == null || targetObject is IList) // if scalar value
                                    {
                                        if (x.ValueUpdateMode == NdefValueUpdateMode.ResetOrRemoveFromList)
                                        {
                                            // Create stub copy (db null)
                                            NdefTypeInfo ndefTypeInfo = typeBinder.LookupTypeInfo(targetObject);
                                            targetObject = typeBinder.CreateObject(ndefTypeInfo,
                                                ndefTypeInfo.TypeNumber, ndefTypeInfo.SerializableName, "#0");
                                            typeBinder.MarkAsStubObject(targetObject);
                                            // TODO: to think if static null object can be used
                                        }
                                        SetMemberValue(sourceObject, member, targetObject);
                                    }
                                    else
                                    {
                                        AddObjectToList(list, targetObject, x.ValueUpdateMode,
                                            mode == NdefLinkingMode.TwoWayLinkingAndNormalizedOrder &&
                                            x.Source.ObjectLogicalId > 0 && x.Target.ObjectLogicalId > 0);
                                        if (listCreated)
                                            SetMemberValue(sourceObject, member, list);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddObjectToList(IList list, object value,
            NdefValueUpdateMode valueUpdateMode, bool skipDuplicates)
        {
            if (valueUpdateMode == NdefValueUpdateMode.SetOrAddToList)
            {
                if (skipDuplicates)
                {
                    if (!IsDuplicate(list, value))
                        list.Add(value);
                }
                else // OneWayLinkingAndOriginalOrder, NoLinkingAndOriginalOrder
                    list.Add(value);
            }
            else
                throw new NotImplementedException("not implemented: NdefValueUpdateMode.ResetOrRemoveFromList");
        }

        private bool IsDuplicate(IList list, object value)
        {
            return list.Count > 0 && list[list.Count - 1] == value;
        }
    }

    internal struct NdefInstanceInfo
    {
        public object RuntimeObject;
        public IList TwoWayLinkingObjectList;
        public object RuntimeObjectStub;
    }

    internal class NdefRelationParty
    {
        public int ObjectIndex;
        public long ObjectSerialKey;
        public long ObjectLogicalId;
        public NdefField Field;
        public NdefTypeInfo ObjectTypeInfo;
        public bool IsListInMemberInfo; // избавиться?
        public IList TwoWayLinkingObjectList; // избавиться?

        public override string ToString()
        {
            if (Field.Name != null)
                return string.Format("[{0}] {1}#{2}@{3}.{4}", ObjectIndex, ObjectTypeInfo.SerializableName,
                    ObjectLogicalId, ObjectSerialKey, Field.Name);
            else
                return string.Format("[{0}] #{1}@{2}", ObjectIndex, ObjectLogicalId, ObjectSerialKey);
        }
    }

    internal class NdefRelation
    {
        public int NaturalOrderNumber;
        public NdefRelationParty Source;
        public NdefRelationParty Target;
        public NdefValueUpdateMode ValueUpdateMode;
        public bool IsImplicitBackRelation;

#if DEBUG
        internal string DebugHint;
#endif

        public override string ToString()
        {
#if DEBUG
            return DebugHint;
#else
            if (Target != null)
            {
                string arrow;
                if (IsImplicitBackRelation)
                {
                    if (Source.TwoWayLinkingObjectList != null)
                        arrow = "<==";
                    else
                        arrow = "<--";
                }
                else
                {
                    if (Target != null && Target.TwoWayLinkingObjectList != null)
                        arrow = "==>";
                    else
                        arrow = "-->";
                }
                return string.Format("{0}  {1}  {2}", Source, arrow, Target);
            }
            else
            {
                return string.Format("{0} -- {1}",
                    Source, ValueUpdateMode == NdefValueUpdateMode.SetOrAddToList ? "Root" : "Nested");
            }
#endif
        }
    }

    internal class LinkPairSortedOrderComparer : IComparer<NdefRelation>
    {
        public static readonly LinkPairSortedOrderComparer Default = new LinkPairSortedOrderComparer();

        public int Compare(NdefRelation x, NdefRelation y)
        {
            int result = x.Source.ObjectLogicalId.CompareTo(y.Source.ObjectLogicalId);
            if (result == 0)
            {
                if (x.Source.ObjectLogicalId == 0 || (x.Source.ObjectSerialKey != long.MaxValue &&
                    y.Source.ObjectSerialKey != long.MaxValue))
                    result = x.Source.ObjectSerialKey.CompareTo(y.Source.ObjectSerialKey);
                if (result == 0)
                {
                    if (x.Target != null)
                    {
                        if (y.Target != null)
                        {
                            // references are ordered by referencing field name and then by id of referenced objects
                            string xFieldName = x.Source.Field.Name;
                            string yFieldName = y.Source.Field.Name;
                            result = string.Compare(xFieldName, yFieldName);
                            if (result == 0)
                            {
                                // Ссылки внутри DbObject'а упорядочиваются по логическому ключу.
                                // В остальных объектах ссылки упорядочиваются в очередности появления в N*DEF тексте.
                                if (x.Source.ObjectLogicalId != 0)
                                    result = x.Target.ObjectLogicalId.CompareTo(y.Target.ObjectLogicalId);
                                else
                                    result = x.NaturalOrderNumber.CompareTo(y.NaturalOrderNumber);
                            }
                        }
                        else
                            result = 1; // при упорядочивании ссылки идут после объектов
                    }
                    else if (y.Target != null)
                        result = -1; // при упорядочивании объекты идут до ссылок
                    else
                        result = x.NaturalOrderNumber.CompareTo(y.NaturalOrderNumber); // use natural order for objects with equal ids
                }
            }
            return result;
        }
    }

    internal class LinkPairOriginalOrderComparer : IComparer<NdefRelation>
    {
        public static readonly LinkPairOriginalOrderComparer Default = new LinkPairOriginalOrderComparer();

        public int Compare(NdefRelation x, NdefRelation y)
        {
            int result = x.Source.ObjectLogicalId.CompareTo(y.Source.ObjectLogicalId);
            if (result == 0)
            {
                result = x.Source.ObjectSerialKey.CompareTo(y.Source.ObjectSerialKey);
                if (result == 0)
                {
                    if (x.Target != null)
                    {
                        if (y.Target != null)
                        {
                            // references are ordered by referencing field name and then by id of referenced objects
                            string fieldName1 = x.Source.Field.Name;
                            string fieldName2 = y.Source.Field.Name;
                            result = string.Compare(fieldName1, fieldName2);
                            if (result == 0)
                                result = x.NaturalOrderNumber.CompareTo(y.NaturalOrderNumber);
                        }
                        else
                            result = 1; // references go after objects
                    }
                    else if (y.Target != null)
                        result = -1; // objects go before references
                    else
                        result = x.NaturalOrderNumber.CompareTo(y.NaturalOrderNumber); // use natural order for objects with equal ids
                }
            }
            return result;
        }
    }
}
