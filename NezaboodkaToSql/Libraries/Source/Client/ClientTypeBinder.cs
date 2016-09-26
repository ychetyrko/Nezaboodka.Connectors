using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public class ClientTypeBinder : INdefTypeBinder
    {
        private Dictionary<Type, NdefTypeInfo> fTypeInfoByType = new Dictionary<Type, NdefTypeInfo>();
        private Dictionary<string, NdefTypeInfo> fTypeInfoByName = new Dictionary<string, NdefTypeInfo>();

        // Public

        public ClientTypeSystem TypeSystem { get; private set; }
        public Type RootTypeForObjectsWithKey { get; private set; }
        public Type PreferredListType { get; private set; }

        public static readonly Type[] KnownDatabaseTypes = { typeof(DbObject), typeof(FileObject) };
        public static readonly Type[] KnownSystemTypes = {
            typeof(EnvironmentConfiguration), typeof(NodeTemplateConfiguration), typeof(NodeConfiguration),
            typeof(ServiceConfiguration), typeof(DatabaseServiceConfiguration), typeof(NodeServiceConfiguration),
            typeof(DatabaseConfiguration), typeof(DatabaseSchema), typeof(TypeDefinition),
            typeof(PrimaryIndexDistribution), typeof(PrimaryIndexDistributionRange),
            typeof(SecondaryIndexDistribution), typeof(SecondaryIndexDistributionRange),
            typeof(TextIndexCacheDistribution), typeof(TextIndexCacheDistributionRange),
            typeof(FileStorageDistribution), typeof(FileStorageDistributionRange),
            typeof(DatabaseRequest), typeof(DatabaseResponse), typeof(ErrorResponse),
            typeof(AdministrationRequest), typeof(AdministrationResponse),
            typeof(WriteObjectsRequest), typeof(ReadObjectsResponse),
            typeof(SaveObjectsRequest), typeof(SaveObjectsResponse), typeof(SaveQuery),
            typeof(DeleteObjectsRequest), typeof(DeleteObjectsResponse), typeof(DeleteQuery), typeof(DeleteResult),
            typeof(GetObjectsRequest), typeof(GetObjectsResponse), typeof(GetQuery),
            typeof(LookupObjectsRequest), typeof(LookupObjectsResponse), typeof(LookupQuery),
            typeof(SearchObjectsRequest), typeof(SearchObjectsResponse), typeof(SearchQuery),
            typeof(QueryResult), typeof(Parameter),
            typeof(TextFilter), typeof(TextCondition),
            typeof(GetServerInfoRequest), typeof(GetServerInfoResponse),
            typeof(GetEnvironmentConfigurationRequest), typeof(GetEnvironmentConfigurationResponse), typeof(GetDatabaseListRequest),
            typeof(GetDatabaseListResponse), typeof(AlterDatabaseListRequest), typeof(AlterDatabaseListResponse),
            typeof(GetDatabaseConfigurationRequest), typeof(GetDatabaseConfigurationResponse),
            typeof(AlterDatabaseConfigurationRequest), typeof(AlterDatabaseConfigurationResponse),
            typeof(GetDatabaseAccessModeRequest), typeof(GetDatabaseAccessModeResponse),
            typeof(SetDatabaseAccessModeRequest), typeof(SetDatabaseAccessModeResponse),
            typeof(UnloadDatabaseRequest), typeof(UnloadDatabaseResponse),
            typeof(LoadDatabaseRequest), typeof(LoadDatabaseResponse),
            typeof(RefreshDatabaseCryptoKeyRequest), typeof(RefreshDatabaseCryptoKeyResponse),
            typeof(RefreshEnvironmentCryptoKeyRequest), typeof(RefreshEnvironmentCryptoKeyResponse),
            typeof(CleanupRemovedDatabasesRequest), typeof(CleanupRemovedDatabasesResponse),
            typeof(FileContent) };
        public static readonly INdefValueFormatter[] KnownValueFormatters = {
            new ValueFormatter<bool>("b"), new ValueFormatter<sbyte>("sb"),
            new ValueFormatter<byte>("by"), new ValueFormatter<short>("sh"),
            new ValueFormatter<ushort>("us"), new ValueFormatter<int>("i"),
            new ValueFormatter<uint>("u"), new ValueFormatter<long>("l"),
            new ValueFormatter<ulong>("ul"), new ValueFormatter<float>("f"),
            new ValueFormatter<double>("d"), new ValueFormatter<decimal>("n"),
            new ValueFormatter<char>("c"), new StringFormatter(),
            new NullableValueFormatter<bool>("b?"), new NullableValueFormatter<sbyte>("sb?"),
            new NullableValueFormatter<byte>("by?"), new NullableValueFormatter<short>("sh?"),
            new NullableValueFormatter<ushort>("us?"), new NullableValueFormatter<int>("i?"),
            new NullableValueFormatter<uint>("u?"), new NullableValueFormatter<long>("l?"),
            new NullableValueFormatter<ulong>("ul?"), new NullableValueFormatter<float>("f?"),
            new NullableValueFormatter<double>("d?"), new NullableValueFormatter<decimal>("n?"),
            new NullableValueFormatter<char>("c?"), new DateTimeFormatter("dc"),
            new DateTimeOffsetFormatter("dt"), new BinaryDataFormatter(),
            new BinarySegmentFormatter(),
            new ValueFormatter<DbKey>(),
            new ValueFormatter<DatabaseAccessMode>(),
            new ValueFormatter<TypeAndFields>(),
            new ValueFormatter<FileRange>(),
            new ValueFormatter<FieldDefinition>(),
            new ValueFormatter<IndexFieldDefinition>(),
            new ValueFormatter<SecondaryIndexDefinition>(),
            new ValueFormatter<ReferentialIndexDefinition>(),
            new ValueFormatter<TextIndexDefinition>() };
        public static readonly ClientTypeBinder Default; // initialized in static constructor
        public static readonly string NdefObjectFormattersSourceCodeForKnownSystemTypes; // initialized in static constructor

        static ClientTypeBinder()
        {
            Default = new ClientTypeBinder();
            var assemblyWriter = new ClientAssemblyWriter();
            IEnumerable<TypeDefinition> typeDefs = ProduceTypeDefinitionsFromSystemTypes(KnownSystemTypes);
            NdefObjectFormattersSourceCodeForKnownSystemTypes = assemblyWriter.GenerateNdefObjectFormattersSourceCode(
                typeDefs, typeof(ClientTypeBinder).Namespace);
        }

        public ClientTypeBinder()
            : this(null, typeof(DbObject), "DbObject", KnownDatabaseTypes, KnownSystemTypes,
                  KnownValueFormatters)
        {
        }

        public ClientTypeBinder(string databaseConfigurationNdefText)
            : this(new ClientTypeSystem(databaseConfigurationNdefText), typeof(DbObject), "DbObject",
                  KnownDatabaseTypes, KnownSystemTypes, KnownValueFormatters)
        {
        }

        public ClientTypeBinder(string databaseConfigurationNdefText, IEnumerable<Type> databaseTypes)
            : this(new ClientTypeSystem(databaseConfigurationNdefText), typeof(DbObject), "DbObject",
                  KnownDatabaseTypes.Concat(databaseTypes), KnownSystemTypes, KnownValueFormatters)
        {
        }

        public ClientTypeBinder(IEnumerable<TypeDefinition> databaseTypeDefinitions, IEnumerable<Type> databaseTypes)
            : this(new ClientTypeSystem(databaseTypeDefinitions), typeof(DbObject), "DbObject",
                  KnownDatabaseTypes.Concat(databaseTypes), KnownSystemTypes, KnownValueFormatters)
        {
        }

        public ClientTypeBinder(IEnumerable<TypeDefinition> databaseTypeDefinitions, IEnumerable<Type> databaseTypes,
            IEnumerable<Type> systemTypes)
            : this(new ClientTypeSystem(databaseTypeDefinitions), typeof(DbObject), "DbObject",
                KnownDatabaseTypes.Concat(databaseTypes), KnownSystemTypes.Concat(systemTypes), KnownValueFormatters)
        {
        }

        public virtual NdefTypeInfo LookupTypeInfo(object obj)
        {
            NdefTypeInfo result;
            Type type = obj.GetType();
            if (type == typeof(DbDynamic))
                result = LookupTypeInfoByName((obj as DbDynamic).TypeName);
            else
                result = LookupTypeInfoByType(type);
            return result;
        }

        public virtual NdefTypeInfo LookupTypeInfoByType(Type type)
        {
            NdefTypeInfo result;
            if (!fTypeInfoByType.TryGetValue(type, out result))
            {
                if (!fTypeInfoByName.TryGetValue(type.Name, out result))
                {
                    if (!fTypeInfoByName.TryGetValue(type.FullName, out result))
                        throw new NezaboodkaException(string.Format("type {0} is not registered in {1}",
                            type.FullName, this.GetType().FullName));
                    else
                        throw new NezaboodkaException(string.Format(
                            "type {0}/{1} is not registered, but {2} is registered in {3}",
                            type.FullName, type.Assembly.GetName().Version, type.FullName, this.GetType().FullName));
                }
                else
                    throw new NezaboodkaException(string.Format(
                        "type {0}/{1} is not registered, but {2} is registered in {3}",
                        type.FullName, type.Assembly.GetName().Version, type.Name, this.GetType().FullName));
            }
            return result;
        }

        public virtual NdefTypeInfo LookupTypeInfoByName(string typeName)
        {
            NdefTypeInfo result;
            if (!fTypeInfoByName.TryGetValue(typeName, out result))
            {
                Assembly assembly = typeof(object).Assembly;
                Type systemType = assembly.GetType("System." + typeName);
                if (!fTypeInfoByType.TryGetValue(systemType, out result))
                    throw new NezaboodkaException(string.Format("type name {0} is not registered in {1}",
                        typeName, this.GetType().FullName));
            }
            return result;
        }

        public virtual NdefTypeInfo LookupTypeInfoByField(NdefTypeInfo typeInfo,
            NdefField ndefField, bool adjustToActualType)
        {
            NdefTypeInfo result;
            if (typeInfo.IsListType)
            {
                if (ndefField.Name == null)
                {
                    Type formalType = NdefUtils.GetElementType(typeInfo.SystemType, RootTypeForObjectsWithKey);
                    result = LookupTypeInfoByType(formalType);
                }
                else
                    throw new ArgumentException("list type has cannot have named fields");
            }
            else if (ndefField.Name != null)
            {
                FieldInfo fieldInfo = typeInfo.SystemType.GetField(
                    ndefField.Name, BindingFlags.Public | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    result = LookupTypeInfoByType(fieldInfo.FieldType);
                    if (adjustToActualType)
                    {
                        if (result.SystemType == typeof(object) ||
                            (result.IsListType && (result.SystemType.IsInterface || result.SystemType.IsAbstract)))
                        {
                            Type elementType = NdefUtils.GetElementType(result.SystemType, RootTypeForObjectsWithKey);
                            Type adjustedListType = PreferredListType.MakeGenericType(elementType);
                            result = LookupTypeInfoByType(adjustedListType);
                        }
                    }
                }
                else
                {
                    if (adjustToActualType)
                    {
                        // Если по типу данных поля родительского объекта невозможно выявить тип данных,
                        // то исходим из предположения, что родительский объект - это DbDynamic, в котором
                        // все поля-списки - это List<DbDynamic>.
                        Type formalType = PreferredListType.MakeGenericType(typeInfo.SystemType);
                        result = LookupTypeInfoByType(formalType);
                    }
                    else
                        throw new NotImplementedException("internal error");
                }
            }
            else
                throw new ArgumentNullException("cannot lookup field type by empty name");
            return result;
        }

        public long GetObjectLogicalId(string key)
        {
            return DbKey.Parse(key).SystemId;
        }

        public string GetObjectKeyFromLogicalId(long logicalId)
        {
            DbKey key = new DbKey() { SystemId = logicalId, Revision = 0 };
            return key.ToString();
        }

        public virtual object CreateObject(NdefTypeInfo typeInfo, int typeNumber, string typeName, string key)
        {
            object result = Activator.CreateInstance(typeInfo.SystemType);
            if (!string.IsNullOrEmpty(key))
                ((DbObject)result).Key = DbKey.Parse(key);
            if (typeInfo.SystemType == typeof(DbDynamic))
                ((DbDynamic)result).TypeName = typeName;
            return result;
        }

        public virtual string GetObjectKey(object obj)
        {
            var t = obj as DbObject;
            return t != null ? t.Key.ToString() : null;
        }

        public virtual bool IsStubObject(object obj)
        {
            var t = obj as DbObject;
            return t != null ? !t.IsObject : false;
        }

        public virtual void MarkAsStubObject(object obj)
        {
            var t = (DbObject)obj;
            t.Key = ((DbObject)obj).Key.AsReference;
        }

        public virtual bool IsImplicitObject(object obj)
        {
            var t = obj as DbObject;
            return t != null ? t.IsImplicit : false;
        }

        public virtual void MarkAsImplicitObject(object obj)
        {
            var t = (DbObject)obj;
            t.Key = t.Key.AsImplicit;
        }

        public virtual NdefField GetBackReferenceField(NdefTypeInfo ndefTypeInfo, NdefField ndefField,
            out bool isList, out NdefTypeInfo fieldTypeInfo)
        {
            NdefField result = default(NdefField);
            isList = false;
            fieldTypeInfo = default(NdefTypeInfo);
            if (TypeSystem != null)
            {
                //TODO: Get rid of search 1, 2, 3 below.
                int typeNumber = TypeSystem.GetTypeNumberByName(ndefTypeInfo.SerializableName); // search 1
                if (typeNumber >= 0)
                {
                    int fieldNumber = TypeSystem.GetFieldNumberByName(typeNumber, ndefField.Name); // search 2
                    int backReferenceTypeNumber;
                    TypeSystem.GetFieldBackReferenceInfo(typeNumber, fieldNumber, out backReferenceTypeNumber,
                        out result.Number);
                    if (result.Number >= 0)
                    {
                        NdefTypeInfo backReferenceTypeInfo = LookupTypeInfoByName( // search 3
                            TypeSystem.GetTypeName(backReferenceTypeNumber));
                        result.Name = TypeSystem.GetFieldName(backReferenceTypeNumber, result.Number);
                        FieldKind fieldKind = TypeSystem.GetFieldKind(backReferenceTypeNumber, result.Number);
                        isList = fieldKind == FieldKind.ObjectList || fieldKind == FieldKind.ValueList;
                        fieldTypeInfo = LookupTypeInfoByName(TypeSystem.GetTypeName(backReferenceTypeNumber));
                    }
                }
            }
            return result;
        }

        // Internal

        protected static IEnumerable<TypeDefinition> ProduceTypeDefinitionsFromSystemTypes(IEnumerable<Type> types)
        {
            foreach (Type t in types)
            {
                var typeDef = new TypeDefinition();
                typeDef.TypeName = t.Name;
                typeDef.BaseTypeName = t.BaseType != null && t.BaseType != typeof(object) ? t.BaseType.Name : null;
                typeDef.FieldDefinitions = new List<FieldDefinition>();
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var fieldDef = new FieldDefinition();
                    fieldDef.FieldName = f.Name;
                    Type type = NdefUtils.TryGetElementType(f.FieldType);
                    fieldDef.IsList = type == null;
                    if (type == null)
                        type = f.FieldType;
                    fieldDef.FieldTypeName = type.Name;
                    typeDef.FieldDefinitions.Add(fieldDef);
                }
                yield return typeDef;
            }
        }

        protected ClientTypeBinder(ClientTypeSystem typeSystem, Type rootTypeForObjectsWithKey, string rootTypeName,
            IEnumerable<Type> databaseTypes, IEnumerable<Type> systemTypes, IEnumerable<INdefValueFormatter> valueFormatters)
        {
            TypeSystem = typeSystem;
            RootTypeForObjectsWithKey = rootTypeForObjectsWithKey;
            PreferredListType = typeof(List<>);
            var anyTypeFormatter = new AnyTypeFormatter(this);
            var listFormatter = CreateGenericListFormatter(typeof(IList), null);
            RegisterType(-1, typeof(IList), true, null, listFormatter);
            RegisterType(-1, typeof(IEnumerable), true, null, listFormatter);
            RegisterTypeAndRelevantListTypes(-1, typeof(object), anyTypeFormatter);
            RegisterTypeAndRelevantListTypes(-1, typeof(Dictionary<string, object>),
                new ObjectValueFormatter<Dictionary<string, object>>("Nezaboodka.Dictionary"));
            RegisterListType(-1, typeof(DbDynamic[]), null);
            RegisterListType(-1, typeof(List<DbDynamic>), null);
            RegisterListType(-1, typeof(IList<DbDynamic>), null);
            if (valueFormatters != null)
                foreach (INdefValueFormatter x in valueFormatters)
                    RegisterTypeAndRelevantListTypes(-1, x.TypeOfValue, x);
            if (typeSystem != null)
                RegisterDatabaseTypesFromTypeSystem(rootTypeName, databaseTypes);
            else
                RegisterDatabaseTypesWithoutTypeSystem(rootTypeName, databaseTypes);
            if (systemTypes != null)
                foreach (Type x in systemTypes)
                    if (x != rootTypeForObjectsWithKey)
                        RegisterTypeAndRelevantListTypes(-1, x, CreateGenericValueToObjectFormatter(x, x.FullName));
            var assemblyName = new AssemblyName("Z" + Guid.NewGuid().ToString("N"));
            var generator = new FormatterAssemblyGenerator();
            AssemblyBuilder formattersAssembly = generator.GenerateNdefObjectFormattersAssembly(assemblyName,
                fTypeInfoByType.Select((KeyValuePair<Type, NdefTypeInfo> x) => x.Value)
                    .Where((NdefTypeInfo y) => !y.IsListType && IsObjectFormatterRequired(y)), this);
            // Generate object formatters for all registered types
            foreach (KeyValuePair<Type, NdefTypeInfo> x in fTypeInfoByType)
                if (IsObjectFormatterRequired(x.Value))
                    if (x.Value.ObjectFormatter == null)
                        x.Value.ObjectFormatter = CreateObjectFormatter(x.Value, formattersAssembly);
            foreach (KeyValuePair<string, NdefTypeInfo> x in fTypeInfoByName)
                if (IsObjectFormatterRequired(x.Value))
                    if (x.Value.ObjectFormatter == null)
                        x.Value.ObjectFormatter = CreateObjectFormatter(x.Value, formattersAssembly);
        }

        private void RegisterDatabaseTypesFromTypeSystem(string rootTypeName, IEnumerable<Type> databaseTypes)
        {
            var databaseTypesDictionary = new Dictionary<string, Type>();
            if (databaseTypes != null)
                foreach (Type x in databaseTypes)
                    if (x != RootTypeForObjectsWithKey)
                        databaseTypesDictionary.Add(x.Name, x);
                    else
                        databaseTypesDictionary.Add(rootTypeName, x);
            for (int i = 0; i < TypeSystem.GetTypeCount(); i++)
            {
                string typeName = TypeSystem.GetTypeName(i);
                Type type;
                if (databaseTypesDictionary.TryGetValue(typeName, out type))
                    RegisterTypeAndRelevantListTypes(i, type, CreateGenericValueToObjectFormatter(type, typeName));
                else
                    RegisterDynamicTypeAndRelevantListTypes(i, typeName);
            }
        }

        private void RegisterDatabaseTypesWithoutTypeSystem(string rootTypeName, IEnumerable<Type> databaseTypes)
        {
            if (databaseTypes != null)
            {
                int i = 0;
                foreach (Type x in databaseTypes)
                {
                    if (x != RootTypeForObjectsWithKey)
                        RegisterTypeAndRelevantListTypes(i, x, CreateGenericValueToObjectFormatter(x, x.Name));
                    else
                        RegisterTypeAndRelevantListTypes(i, x, CreateGenericValueToObjectFormatter(x, rootTypeName));
                    i++;
                }
            }
            else
                RegisterTypeAndRelevantListTypes(0, RootTypeForObjectsWithKey, CreateGenericValueToObjectFormatter(
                    RootTypeForObjectsWithKey, rootTypeName));
        }

        private void RegisterTypeAndRelevantListTypes(int typeNumber, Type type, INdefValueFormatter formatter)
        {
            RegisterType(typeNumber, type, false, formatter.SerializableTypeName, formatter);
            string typeName = formatter.SerializableTypeName ?? type.FullName;
            if (type != typeof(byte))
            {
                RegisterListType(typeNumber, type.MakeArrayType(), typeName + "[]");
                RegisterListType(typeNumber, typeof(List<>).MakeGenericType(type), "List<" + typeName + ">");
                RegisterListType(typeNumber, typeof(IList<>).MakeGenericType(type), null);
            }
        }

        private void RegisterListType(int typeNumber, Type listType, string serializableName)
        {
            RegisterType(typeNumber, listType, true, serializableName, CreateGenericListFormatter(listType,
                serializableName));
        }

        private void RegisterType(int typeNumber, Type type, bool isListType, string serializableName,
            INdefValueFormatter formatter)
        {
            string additionalSerializableName = null;
            if (serializableName != null)
            {
                if (serializableName.StartsWith("Nezaboodka.Server."))
                {
                    additionalSerializableName = serializableName;
                    serializableName = "N." + serializableName.Substring("Nezaboodka.Server.".Length);
                }
                else if (serializableName.StartsWith("Nezaboodka."))
                {
                    additionalSerializableName = serializableName;
                    serializableName = "N." + serializableName.Substring("Nezaboodka.".Length);
                }
            }
            NdefTypeInfo typeInfo;
            if (type.IsArray)
                typeInfo = new NdefTypeInfo(typeNumber, typeof(NdefArrayBuffer<>).MakeGenericType(type.GetElementType()),
                    isListType, serializableName, formatter);
            else
                typeInfo = new NdefTypeInfo(typeNumber, type, isListType, serializableName, formatter);
            fTypeInfoByType.Add(type, typeInfo);
            if (serializableName != null)
            {
                fTypeInfoByName.Add(serializableName, typeInfo);
                if (additionalSerializableName != null)
                    fTypeInfoByName.Add(additionalSerializableName, typeInfo);
            }
        }

        private void RegisterDynamicTypeAndRelevantListTypes(int typeNumber, string serializableName)
        {
            RegisterDynamicType(typeNumber, typeof(DbDynamic), false, serializableName,
                new ObjectValueFormatter<DbDynamic>(serializableName));
            RegisterDynamicListType(typeNumber, typeof(DbDynamic[]), serializableName + "[]");
            RegisterDynamicListType(typeNumber, typeof(List<DbDynamic>), "List<" + serializableName + ">");
        }

        private void RegisterDynamicListType(int typeNumber, Type listType, string serializableName)
        {
            RegisterDynamicType(typeNumber, listType, true, serializableName, CreateGenericListFormatter(listType,
                serializableName));
        }

        private void RegisterDynamicType(int typeNumber, Type type, bool isListType, string serializableName,
            INdefValueFormatter formatter)
        {
            NdefTypeInfo typeInfo;
            if (type.IsArray)
                typeInfo = new NdefTypeInfo(typeNumber, typeof(NdefArrayBuffer<DbDynamic>), isListType,
                    serializableName, formatter);
            else
                typeInfo = new NdefTypeInfo(typeNumber, type, isListType, serializableName, formatter);
            fTypeInfoByName.Add(serializableName, typeInfo);
        }

        private INdefValueFormatter CreateGenericValueToObjectFormatter(Type type, string serializableName)
        {
            Type formatterType = typeof(ObjectValueFormatter<>).MakeGenericType(type);
            return (INdefValueFormatter)Activator.CreateInstance(formatterType, serializableName);
        }

        private INdefValueFormatter CreateGenericListFormatter(Type type, string serializableName)
        {
            Type formatterType = typeof(ListValueFormatter<>).MakeGenericType(type);
            return (INdefValueFormatter)Activator.CreateInstance(formatterType, serializableName,
                PreferredListType, RootTypeForObjectsWithKey);
        }

        private bool IsObjectFormatterRequired(NdefTypeInfo ndefTypeInfo)
        {
            Type formatterType = ndefTypeInfo.ValueFormatter.GetType();
            return ndefTypeInfo.IsListType ||
                (formatterType.IsGenericType && formatterType.GetGenericTypeDefinition() == typeof(ObjectValueFormatter<>));
        }

        private AbstractObjectFormatter CreateObjectFormatter(NdefTypeInfo typeInfo, Assembly formattersAssembly)
        {
            AbstractObjectFormatter result;
            if (!typeInfo.IsListType)
            {
                MethodInfo methodInfo = GetType().GetMethod("ActivateObjectFormatter", BindingFlags.NonPublic |
                    BindingFlags.Instance, null, new Type[] { typeof(NdefTypeInfo), typeof(Assembly) }, null);
                result = (AbstractObjectFormatter)methodInfo.MakeGenericMethod(typeInfo.SystemType).Invoke(
                    this, new object[] { typeInfo, formattersAssembly });
            }
            else
                result = CreateListFormatter(typeInfo);
            return result;
        }

        // Вызывается по имени через Reflection из метода CreateObjectFormatter.
        protected AbstractObjectFormatter ActivateObjectFormatter<T>(NdefTypeInfo typeInfo, Assembly formattersAssembly)
        {
            AbstractObjectFormatter result;
            if (typeInfo.SystemType != typeof(DbDynamic))
            {
                if (typeInfo.SystemType != typeof(Dictionary<string, object>))
                {
                    IEnumerable<NdefFieldAccessor<T>> fields;
                    object compiledFormatter = TryCreateInstanceOfCompiledObjectFormatter(
                        typeInfo.SystemType, formattersAssembly);
                    if (compiledFormatter != null)
                        fields = CreateCompiledFieldAccessors<T>(typeInfo, compiledFormatter);
                    else
                        fields = CreateReflectionBasedFieldAccessors<T>(typeInfo);
                    result = new ObjectFormatter<T>(fields);
                }
                else
                {
                    var formatter = new AnyTypeFormatter(this);
                    result = new DictionaryFormatter(formatter);
                }
            }
            else
            {
                IEnumerable<NdefFieldAccessor<DbDynamic>> fieldAccessors = CreateDynamicObjectFieldAccessors(typeInfo);
                result = new ObjectFormatter<DbDynamic>(fieldAccessors);
            }
            return result;
        }

        private object TryCreateInstanceOfCompiledObjectFormatter(Type objectType, Assembly formattersAssembly)
        {
            object result;
            Type formatterType = formattersAssembly.GetType(formattersAssembly.GetName().Name + "." + objectType.FullName);
            if (formatterType != null)
                result = Activator.CreateInstance(formatterType, this);
            else
                result = null;
            return result;
        }

        private IEnumerable<NdefFieldAccessor<T>> CreateCompiledFieldAccessors<T>(NdefTypeInfo typeInfo, object compiledFormatter)
        {
            IEnumerable<string> names = compiledFormatter.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where((MethodInfo x) => x.ReturnParameter.ParameterType == typeof(NdefValue))
                .Select((MethodInfo x) => x.Name);
            foreach (string name in names)
            {
                // Getter and setter have the same name and different parameters.
                // Getter: public NdefValue Age(User obj) { return as_int.ToNdefValue(obj.Age); }
                // Setter: public void Age(User obj, NdefValue value) { obj.Age = as_int.FromNdefValue(value); }
                MethodInfo getMethodInfo = compiledFormatter.GetType().GetMethod(name, new Type[] { typeInfo.SystemType });
                MethodInfo setMethodInfo = compiledFormatter.GetType().GetMethod(name, new Type[] { typeInfo.SystemType,
                    typeof(NdefValue) });
                if (getMethodInfo != null && setMethodInfo != null)
                {
                    NdefFieldGetter<T> getter = Delegate.CreateDelegate(typeof(NdefFieldGetter<T>), compiledFormatter,
                        getMethodInfo, false) as NdefFieldGetter<T>;
                    NdefFieldSetter<T> setter = Delegate.CreateDelegate(typeof(NdefFieldSetter<T>), compiledFormatter,
                        setMethodInfo, false) as NdefFieldSetter<T>;
                    if (getter != null && setter != null)
                    {
                        var fieldAccessors = new NdefFieldAccessor<T>(name, getter, setter);
                        yield return fieldAccessors;
                    }
                }
            }
        }

        private IEnumerable<NdefFieldAccessor<T>> CreateReflectionBasedFieldAccessors<T>(NdefTypeInfo typeInfo)
        {
            FieldInfo[] fields = typeInfo.SystemType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where((FieldInfo x) => x.DeclaringType != typeof(DbObject)).ToArray(); // игнорировать DbObject.Key
            INdefValueFormatter[] formatters = fields.Select((FieldInfo x) => LookupTypeInfoByType(x.FieldType).ValueFormatter).ToArray();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                INdefValueFormatter c = formatters[i];
                NdefFieldGetter<T> getter = delegate(T obj)
                {
                    object value = f.GetValue(obj);
                    return c.AnyToNdefValue(c.TypeOfValue, value);
                };
                NdefFieldSetter<T> setter = delegate(T obj, NdefValue value)
                {
                    object t = c.AnyFromNdefValue(f.FieldType, value);
                    f.SetValue(obj, t);
                };
                yield return new NdefFieldAccessor<T>(f.Name, getter, setter);
            }
        }

        private IEnumerable<NdefFieldAccessor<DbDynamic>> CreateDynamicObjectFieldAccessors(NdefTypeInfo typeInfo)
        {
            int typeNumber = TypeSystem.GetTypeNumberByName(typeInfo.SerializableName);
            if (typeNumber >= 0)
            {
                int fieldCount = TypeSystem.GetFieldCount(typeNumber);
                var formatters = new INdefValueFormatter[fieldCount];
                for (int i = 0; i < fieldCount; i++)
                {
                    string fieldTypeName = TypeSystem.GetFieldTypeName(typeNumber, i);
                    FieldKind fieldKind = TypeSystem.GetFieldKind(typeNumber, i);
                    if (fieldKind == FieldKind.ObjectList || fieldKind == FieldKind.ValueList)
                        if (PreferredListType.IsArray)
                            fieldTypeName = fieldTypeName + "[]";
                        else
                            fieldTypeName = "List<" + fieldTypeName + ">";
                    formatters[i] = LookupTypeInfoByName(fieldTypeName).ValueFormatter;
                }
                for (int i = 0; i < fieldCount; i++)
                {
                    string fieldName = TypeSystem.GetFieldName(typeNumber, i);
                    INdefValueFormatter formatter = formatters[i];
                    NdefFieldGetter<DbDynamic> getter = delegate(DbDynamic obj)
                    {
                        object value = null;
                        if (obj.Fields != null)
                            obj.Fields.TryGetValue(fieldName, out value);
                        return formatter.AnyToNdefValue(formatter.TypeOfValue, value);
                    };
                    NdefFieldSetter<DbDynamic> setter = delegate(DbDynamic obj, NdefValue value)
                    {
                        object t = formatter.AnyFromNdefValue(formatter.TypeOfValue, value);
                        if (obj.Fields == null)
                            obj.Fields = new Dictionary<string, object>();
                        if (obj.Fields.ContainsKey(fieldName))
                            obj.Fields[fieldName] = t;
                        else
                            obj.Fields.Add(fieldName, t);
                    };
                    yield return new NdefFieldAccessor<DbDynamic>(fieldName, getter, setter);
                }
            }
        }

        private AbstractObjectFormatter CreateListFormatter(NdefTypeInfo typeInfo)
        {
            Type elementType = NdefUtils.GetElementType(typeInfo.SystemType, typeof(object));
            if (elementType == typeof(DbDynamic))
                elementType = typeof(object);
            INdefValueFormatter formatter = LookupTypeInfoByType(elementType).ValueFormatter;
            return new ListFormatter(formatter);
        }
    }
}
