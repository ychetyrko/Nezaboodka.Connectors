using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public class NdefLinker
    {
        private NdefLinkingMode fLinkingMode;
        private List<NdefInstanceInfo> fObjects;
        private List<NdefRelation> fRelations;
        private NdefRelationParty fCurrentSource;
        private Dictionary<long, long> fObjectKeySubstitution;

        // Public

        public ClientTypeBinder TypeBinder { get; set; }
        public GenerateObjectKeyDelegate GenerateObjectKeyDelegate { get; set; }

        public NdefLinker(NdefLinkingMode linkingMode)
        {
            fLinkingMode = linkingMode;
            fObjects = new List<NdefInstanceInfo>();
            fRelations = new List<NdefRelation>();
            fCurrentSource = null;
            fObjectKeySubstitution = new Dictionary<long, long>();
        }

        public void RegisterObject(Type formalType, NdefObject ndefObject)
        {
            if (ndefObject.DeserializedInstance == null)
            {
                ndefObject.DeserializedInstanceIndex = AcquireInstanceIndex();
                ndefObject.DeserializedInstance =
                    ndefObject.Header.TypeInfo.Formatter.Boxed.CreateObjectInstance(
                        formalType, ndefObject.Header);
                fObjects[ndefObject.DeserializedInstanceIndex] =
                    new NdefInstanceInfo() { RuntimeObject = ndefObject.DeserializedInstance };
                DoRegisterObject(ndefObject);
            }
        }

        public void RegisterReference(NdefObject ndefObject, NdefField ndefField, NdefValue reference)
        {
            long objectKey = 0;
            DbObject dbObject = ndefObject.DeserializedInstance as DbObject;
            if (dbObject != null)
                objectKey = dbObject.Key.SystemId;
            long objectNumber = long.MaxValue;
            if (!string.IsNullOrEmpty(ndefObject.Header.Number))
                objectNumber = long.Parse(ndefObject.Header.Number);
            if (fCurrentSource == null ||
                objectKey != fCurrentSource.ObjectKey ||
                objectNumber != fCurrentSource.ObjectNumber ||
                ndefField.Name != fCurrentSource.Field.Name ||
                (objectKey == 0 && ndefObject.DeserializedInstanceIndex != fCurrentSource.ObjectIndex))
            {
                fCurrentSource = new NdefRelationParty()
                {
                    ObjectNumber = objectNumber,
                    ObjectKey = objectKey,
                    ObjectIndex = ndefObject.DeserializedInstanceIndex,
                    ObjectTypeInfo = ndefObject.Header.TypeInfo,
                    Field = ndefField,
                    IsListInMemberInfo = ndefObject.Header.TypeInfo.IsListType
                };
            }
            long targetNumber = long.MaxValue;
            long targetKey = 0;
            if (reference.Kind == NdefValueKind.Reference)
            {
                if (!string.IsNullOrEmpty(reference.AsObjectNumber))
                    targetNumber = long.Parse(reference.AsObjectNumber);
                if (!string.IsNullOrEmpty(reference.AsObjectKey))
                    targetKey = SubstituteDbKeyIfNeeded(DbKey.Parse(reference.AsObjectKey)).SystemId;
            }
            else if (reference.Kind == NdefValueKind.Object)
            {
                if (!string.IsNullOrEmpty(reference.AsNestedObjectToDeserialize.Header.Number))
                    targetNumber = long.Parse(reference.AsNestedObjectToDeserialize.Header.Number);
                if (!string.IsNullOrEmpty(reference.AsNestedObjectToDeserialize.Header.Key))
                    targetKey = SubstituteDbKeyIfNeeded(DbKey.Parse(reference.AsNestedObjectToDeserialize.Header.Key)).SystemId;
            }
            var target = new NdefRelationParty()
            {
                ObjectNumber = targetNumber,
                ObjectKey = targetKey,
                ObjectIndex = -1 // to be resolved by SetupObjectNumbersAndCreateStubObjects
            };
            //TODO: Избавиться от ndefObject.Header.TypeName == "N.UnitTests.NdefTestSaveQuery"
            if (ndefField.Name != null && (ndefObject.DeserializedInstance is SaveQuery ||
                ndefObject.Header.TypeName == "N.UnitTests.NdefTestSaveQuery") && ndefField.Name == nameof(SaveQuery.ForEachIn))
                target.TwoWayLinkingObjectList =
                    ndefObject.CurrentElement.Value.AsNestedObjectToDeserialize.DeserializedInstance as IList;
            target.Field = GetBackReferenceField(ndefObject.Header.TypeInfo, ndefField,
                out target.IsListInMemberInfo, out target.ObjectTypeInfo);
            var relation = new NdefRelation()
            {
                NaturalOrderNumber = fRelations.Count,
                Source = fCurrentSource,
                Target = target,
                ValueUpdateMode = ndefField.Kind,
                IsImplicitBackRelation = false
            };
            var implicitBackRelation = new NdefRelation()
            {
                NaturalOrderNumber = fRelations.Count + 1,
                Source = target,
                Target = fCurrentSource,
                ValueUpdateMode = ndefField.Kind,
                IsImplicitBackRelation = true
            };
#if DEBUG
            relation.DebugHint = string.Format("{0}^{1} →   #{2}^{3} ({4}.{5})",
                ndefObject.Header.Key != null ? "#" + ndefObject.Header.Key : "", ndefObject.Header.Number,
                reference.AsObjectKey ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.Header.Key : ""),
                reference.AsObjectNumber ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.Header.Number : ""),
                ndefObject.Header.TypeName, ndefObject.CurrentElement.Field.Name);
            implicitBackRelation.DebugHint = target.Field.Name != null ?
                string.Format("#{0}^{1}   ← #{2}^{3} ({4}.{5}) ",
                    reference.AsObjectKey ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.Header.Key : ""),
                    reference.AsObjectNumber ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.Header.Number : ""),
                    ndefObject.Header.Key, ndefObject.Header.Number,
                    target.ObjectTypeInfo.SerializableName, target.Field.Name) :
                string.Format("#{0}^{1}   ← #{2}^{3} ({4}.{5})",
                reference.AsObjectKey ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.Header.Key : ""),
                reference.AsObjectNumber ?? (reference.AsNestedObjectToDeserialize != null ? reference.AsNestedObjectToDeserialize.Header.Number : ""),
                    ndefObject.Header.Key, ndefObject.Header.Number,
                    ndefObject.Header.TypeName, ndefObject.CurrentElement.Field.Name);
#endif
            fRelations.Add(relation);
            fRelations.Add(implicitBackRelation);
        }

        public void LinkObjectsAndReferences()
        {
            //string pairsBeforeSorting = string.Concat((IEnumerable<string>)fLinkPairs.ConvertAll((LinkPair x) => x.DebugHint + "\n"));
            switch (fLinkingMode)
            {
                case NdefLinkingMode.OneWayLinkingAndOriginalOrder:
                    fRelations.Sort(LinkPairOriginalOrderComparer.Default);
                    break;
                case NdefLinkingMode.TwoWayLinkingAndNormalizedOrder:
                    fRelations.Sort(LinkPairSortedOrderComparer.Default);
                    break;
                default:
                    throw new NotImplementedException(string.Format("{0} is not implemented", fLinkingMode));
            }
            //string pairsAfterSorting = string.Concat((IEnumerable<string>)fLinkPairs.ConvertAll((LinkPair x) => x.DebugHint + "\n"));
            SetupObjectNumbersAndCreateStubObjects();
            FillReferenceFields();
        }

        public void FindObject(NdefObject ndefObject)
        {
            long objectKey = SubstituteDbKeyIfNeeded(DbKey.Parse(ndefObject.Header.Key)).SystemId;
            // Здесь должно быть найдено значение для поля ndefObject.DeserializedInstance по значению objectKey.
        }

        public void Clear()
        {
            fObjects.Clear();
            fRelations.Clear();
            fCurrentSource = null;
            fObjectKeySubstitution.Clear();
        }

        // Internal

        private void DoRegisterObject(NdefObject ndefObject)
        {
            long objectKey = 0;
            DbObject dbObject = ndefObject.DeserializedInstance as DbObject;
            if (dbObject != null)
            {
                dbObject.Key = SubstituteDbKeyIfNeeded(dbObject.Key);
                objectKey = dbObject.Key.SystemId;
            }
            long objectNumber = long.MaxValue;
            if (!string.IsNullOrEmpty(ndefObject.Header.Number))
                objectNumber = long.Parse(ndefObject.Header.Number);
            var source = new NdefRelationParty()
            {
                ObjectNumber = objectNumber,
                ObjectKey = objectKey,
                ObjectIndex = ndefObject.DeserializedInstanceIndex,
                ObjectTypeInfo = ndefObject.Header.TypeInfo,
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
                relation.ValueUpdateMode = NdefFieldKind.SetOrAdd;
            else
                relation.ValueUpdateMode = NdefFieldKind.Remove;
#if DEBUG
            relation.DebugHint = ndefObject.Header.Kind != NdefObjectKind.List ?
                string.Format("{0} {1} {2}{3}{4}\n{5}", NdefConst.ObjectStartMarker, ndefObject.Header.TypeName,
                    ndefObject.Header.Key != null ? NdefConst.ObjectKeyPrefix + ndefObject.Header.Key : "",
                    NdefConst.ObjectNumberPrefix, ndefObject.Header.Number, NdefConst.ObjectEndMarker) :
                string.Format("[] {0}{1}{2}{3}.{4}",
                    ndefObject.Parent.Header.TypeName,
                    ndefObject.Parent.Header.Key != null ? NdefConst.ObjectKeyPrefix + ndefObject.Parent.Header.Key : "",
                    NdefConst.ObjectNumberPrefix, ndefObject.Parent.Header.Number, ndefObject.Parent.CurrentElement.Field.Name);
#endif
            fRelations.Add(relation);
        }

        private DbKey SubstituteDbKeyIfNeeded(DbKey key)
        {

            DbKey result = key;
            // В следующей строке вызывать key.IsNew нельзя, потому что положительный key.SystemId считается уже замененным (сгенерированным) значением.
            if (key.SystemId < 0 && GenerateObjectKeyDelegate != null)
            {
                if (!fObjectKeySubstitution.TryGetValue(key.SystemId, out result.SystemId))
                {
                    result.SystemId = GenerateObjectKeyDelegate();
                    fObjectKeySubstitution.Add(key.SystemId, result.SystemId);
                }
                if (result.RevisionAndFlags == 0)
                    result.RevisionAndFlags = DbKey.FlagForNew;
            }
            return result;
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

        private object CreateNewObject(int objectNumber, Type type, long objectKey)
        {
            NdefTypeInfo ndefTypeInfo = TypeBinder.LookupTypeInfoByType(type);
            var h = new NdefObjectHeader() { TypeInfo = ndefTypeInfo };
            object result = ndefTypeInfo.Formatter.Boxed.CreateObjectInstance(ndefTypeInfo.SystemType, h);
            ((DbObject)result).Key.SystemId = objectKey;
            fObjects[objectNumber] = new NdefInstanceInfo() { RuntimeObject = result };
            return result;
        }

        public NdefField GetBackReferenceField(NdefTypeInfo ndefTypeInfo, NdefField ndefField,
            out bool isList, out NdefTypeInfo fieldTypeInfo)
        {
            NdefField result = default(NdefField);
            isList = false;
            fieldTypeInfo = default(NdefTypeInfo);
            ClientTypeSystem typeSystem = TypeBinder.TypeSystem;
            if (typeSystem != null && ndefTypeInfo.TypeNumber >= 0)
            {
                if (ndefField.Number < 0)
                    ndefField.Number = typeSystem.GetFieldNumberByName(ndefTypeInfo.TypeNumber, ndefField.Name);
                int backReferenceTypeNumber;
                typeSystem.GetFieldBackReferenceInfo(ndefTypeInfo.TypeNumber, ndefField.Number,
                    out backReferenceTypeNumber, out result.Number);
                if (result.Number >= 0)
                {
                    result.Name = typeSystem.GetFieldName(backReferenceTypeNumber, result.Number);
                    FieldKind fieldKind = typeSystem.GetFieldKind(backReferenceTypeNumber, result.Number);
                    isList = fieldKind == FieldKind.ObjectList || fieldKind == FieldKind.ValueList;
                    fieldTypeInfo = TypeBinder.LookupTypeInfoByName(typeSystem.GetTypeName(backReferenceTypeNumber));
                }
            }
            return result;
        }

        private void SetupObjectNumbersAndCreateStubObjects()
        {
            NdefRelationParty current = null;
            Type typeOfObjectToCreate = null;
            for (int i = 0; i < fRelations.Count; ++i)
            {
                NdefRelation x = fRelations[i];
                if (current == null || x.Source.ObjectKey != current.ObjectKey ||
                    (x.Source.ObjectNumber != current.ObjectNumber && x.Source.ObjectNumber != long.MaxValue) ||
                    (x.Source.ObjectKey == 0 && x.Source.ObjectNumber == long.MaxValue &&
                    x.Source.ObjectIndex != current.ObjectIndex))
                {
                    if (typeOfObjectToCreate != null)
                    {
                        object t = CreateNewObject(current.ObjectIndex, typeOfObjectToCreate, current.ObjectKey);
                        TypeBinder.MarkAsStubObject(t);
                    }
                    // Switch to a new current master
                    current = x.Source;
                    if (current.ObjectIndex < 0) // implicit back reference
                    {
                        Type type;
                        if (x.Source.Field.Name == null)
                        {
                            // Infer object type via back reference field
                            NdefTypeInfo fieldTypeInfo = TypeBinder.LookupTypeInfoByField(
                                x.Target.ObjectTypeInfo, x.Target.Field, false, out type);
                            type = NdefUtils.GetElementType(type, type);
                        }
                        else
                            type = x.Source.ObjectTypeInfo.SystemType;
                        if (type.IsSubclassOf(TypeBinder.RootTypeForObjectsWithKey))
                            typeOfObjectToCreate = type;
                        else
                            typeOfObjectToCreate = TypeBinder.RootTypeForObjectsWithKey;
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
                                    new DbKey(x.Source.ObjectKey, 0).ToString(),
                                    type.FullName, typeOfObjectToCreate.FullName));
                        }
                    }
                    else
                    {
                        if (x.Target == null)
                            // TODO: Objects[currentObject.ObjectNumber].PatchFrom(Objects[x.ObjectNumber]);
                            throw new NotImplementedException("object duplicates within same N*DEF data set are not implemented");
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
                object t = CreateNewObject(current.ObjectIndex, typeOfObjectToCreate, current.ObjectKey);
                TypeBinder.MarkAsStubObject(t);
            }
        }

        private void FillReferenceFields()
        {
            for (int i = 0; i < fRelations.Count; ++i)
            {
                NdefRelation x = fRelations[i];
                if (x.Target != null)
                {
                    if (x.Target.ObjectKey != 0 || x.Target.ObjectNumber != long.MaxValue)
                    {
                        if (!x.IsImplicitBackRelation || (x.Target.ObjectKey != 0 &&
                            fLinkingMode == NdefLinkingMode.TwoWayLinkingAndNormalizedOrder))
                        {
                            NdefInstanceInfo source = fObjects[x.Source.ObjectIndex];
                            object sourceObject = source.RuntimeObject;
                            NdefInstanceInfo target = fObjects[x.Target.ObjectIndex];
                            object targetObject = target.RuntimeObject;
                            if (x.Source.ObjectKey > 0 && x.Target.ObjectKey > 0 && // TODO: Проверить ObjectKey > 0
                                ((source.TwoWayLinkingObjectList != null) != (target.TwoWayLinkingObjectList != null)))
                            {
                                if (source.TwoWayLinkingObjectList != null)
                                {
                                    if (target.RuntimeObjectStub == null)
                                    {
                                        targetObject = CreateNewObject(x.Target.ObjectIndex, targetObject.GetType(), x.Target.ObjectKey);
                                        TypeBinder.MarkAsImplicitObject(targetObject);
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
                                        sourceObject = CreateNewObject(x.Source.ObjectIndex, sourceObject.GetType(), x.Source.ObjectKey);
                                        TypeBinder.MarkAsImplicitObject(sourceObject);
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
                                        TypeBinder.PreferredListType, TypeBinder.RootTypeForObjectsWithKey, out listCreated);
                                    if (list == null || targetObject is IList) // if scalar value
                                    {
                                        if (x.ValueUpdateMode == NdefFieldKind.Remove)
                                        {
                                            // Create stub copy (db null)
                                            NdefTypeInfo ndefTypeInfo = TypeBinder.LookupTypeInfo(targetObject);
                                            var objectHeader = new NdefObjectHeader() { TypeInfo = ndefTypeInfo, Key = "#0" };
                                            targetObject = ndefTypeInfo.Formatter.Boxed.CreateObjectInstance(
                                                ndefTypeInfo.SystemType, objectHeader);
                                            TypeBinder.MarkAsStubObject(targetObject);
                                            // TODO: to think if static null object can be used
                                        }
                                        SetMemberValue(sourceObject, member, targetObject);
                                    }
                                    else
                                    {
                                        AddObjectToList(list, targetObject, x.ValueUpdateMode,
                                            fLinkingMode == NdefLinkingMode.TwoWayLinkingAndNormalizedOrder &&
                                            x.Source.ObjectKey > 0 && x.Target.ObjectKey > 0);
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
            NdefFieldKind valueUpdateMode, bool skipDuplicates)
        {
            if (valueUpdateMode == NdefFieldKind.SetOrAdd)
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

    public delegate long GenerateObjectKeyDelegate();

    public enum NdefLinkingMode
    {
        OneWayLinkingAndOriginalOrder = 0,
        TwoWayLinkingAndNormalizedOrder, // normalized = sorted + distinct
        NoLinkingAndOriginalOrder
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
        public long ObjectNumber;
        public long ObjectKey;
        public NdefField Field;
        public NdefTypeInfo ObjectTypeInfo;
        public bool IsListInMemberInfo; // избавиться?
        public IList TwoWayLinkingObjectList; // избавиться?

        public override string ToString()
        {
            if (Field.Name != null)
                return string.Format("[{0}] {1}#{2}^{3}.{4}", ObjectIndex, ObjectTypeInfo.SerializableName,
                    ObjectKey, ObjectNumber, Field.Name);
            else
                return string.Format("[{0}] #{1}^{2}", ObjectIndex, ObjectKey, ObjectNumber);
        }
    }

    internal class NdefRelation
    {
        public int NaturalOrderNumber;
        public NdefRelationParty Source;
        public NdefRelationParty Target;
        public NdefFieldKind ValueUpdateMode;
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
                    Source, ValueUpdateMode == NdefFieldKind.SetOrAdd ? "Root" : "Nested");
            }
#endif
        }
    }

    internal class LinkPairSortedOrderComparer : IComparer<NdefRelation>
    {
        public static readonly LinkPairSortedOrderComparer Default = new LinkPairSortedOrderComparer();

        public int Compare(NdefRelation x, NdefRelation y)
        {
            int result = x.Source.ObjectKey.CompareTo(y.Source.ObjectKey);
            if (result == 0)
            {
                if (x.Source.ObjectKey == 0 || (x.Source.ObjectNumber != long.MaxValue &&
                    y.Source.ObjectNumber != long.MaxValue))
                    result = x.Source.ObjectNumber.CompareTo(y.Source.ObjectNumber);
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
                                if (x.Source.ObjectKey != 0)
                                    result = x.Target.ObjectKey.CompareTo(y.Target.ObjectKey);
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
            int result = x.Source.ObjectKey.CompareTo(y.Source.ObjectKey);
            if (result == 0)
            {
                result = x.Source.ObjectNumber.CompareTo(y.Source.ObjectNumber);
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
