using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public class ClientTypeBinder : INdefTypeBinder
    {
        public static readonly ClientTypeBinder Default; // initialized in static constructor
        public static readonly Type[] KnownDatabaseTypes = { typeof(DbObject), typeof(FileObject) };
        //public static readonly string NdefObjectFormattersSourceCodeForKnownSystemTypes; // initialized in static constructor

        // Fields

        private Dictionary<Type, NdefTypeInfo> fTypeInfoByType = new Dictionary<Type, NdefTypeInfo>();
        private Dictionary<string, NdefTypeInfo> fTypeInfoByName = new Dictionary<string, NdefTypeInfo>();

        // Public

        public ClientTypeSystem TypeSystem { get; private set; }
        public Type RootTypeForObjectsWithKey { get; private set; }
        public Type PreferredListType { get; private set; }

        static ClientTypeBinder()
        {
            Default = new ClientTypeBinder();
            //var assemblyWriter = new ClientAssemblyWriter();
            //List<TypeDefinition> typeDefs = ProduceTypeDefinitionsFromSystemTypes(CreateKnownFormatters().Select(x => x.ApplicableType)).ToList();
            //NdefObjectFormattersSourceCodeForKnownSystemTypes = assemblyWriter.GenerateNdefObjectFormattersSourceCode(
            //    typeDefs, typeof(ClientTypeBinder).Namespace);
        }

        public static INdefFormatter[] CreateKnownFormatters()
        {
            return new INdefFormatter[] {
                new ObjectFormatter<EnvironmentConfiguration>(), new ObjectFormatter<NodeTemplateConfiguration>(),
                new ObjectFormatter<NodeConfiguration>(), new ObjectFormatter<ServiceConfiguration>(),
                new ObjectFormatter<DatabaseServiceConfiguration>(), new ObjectFormatter<NodeServiceConfiguration>(),
                new ObjectFormatter<DatabaseConfiguration>(), new ObjectFormatter<DatabaseSchema>(),
                new ObjectFormatter<TypeDefinition>(), new ObjectFormatter<PrimaryIndexDistribution>(),
                new ObjectFormatter<PrimaryIndexDistributionRange>(), new ObjectFormatter<SecondaryIndexDistribution>(),
                new ObjectFormatter<SecondaryIndexDistributionRange>(), new ObjectFormatter<TextIndexCacheDistribution>(),
                new ObjectFormatter<TextIndexCacheDistributionRange>(),new ObjectFormatter<FileStorageDistribution>(),
                new ObjectFormatter<FileStorageDistributionRange>(), new ObjectFormatter<DatabaseRequest>(),
                new ObjectFormatter<DatabaseResponse>(), new ObjectFormatter<ErrorResponse>(),
                new ObjectFormatter<AdministrationRequest>(), new ObjectFormatter<AdministrationResponse>(),
                new ObjectFormatter<SaveObjectsRequest>(), new ObjectFormatter<SaveObjectsResponse>(),
                new ObjectFormatter<SaveQuery>(), new ObjectFormatter<DeleteObjectsRequest>(),
                new ObjectFormatter<DeleteObjectsResponse>(), new ObjectFormatter<DeleteQuery>(),
                new ObjectFormatter<GetObjectsRequest>(), new ObjectFormatter<GetObjectsResponse>(),
                new ObjectFormatter<GetQuery>(), new ObjectFormatter<LookupObjectsRequest>(),
                new ObjectFormatter<LookupObjectsResponse>(), new ObjectFormatter<LookupQuery>(),
                new ObjectFormatter<SearchObjectsRequest>(), new ObjectFormatter<SearchObjectsResponse>(),
                new ObjectFormatter<SearchQuery>(), new ObjectFormatter<QueryResult>(),
                new ObjectFormatter<Parameter>(), new ObjectFormatter<TextFilter>(),
                new ObjectFormatter<TextCondition>(), new ObjectFormatter<GetServerInfoRequest>(),
                new ObjectFormatter<GetServerInfoResponse>(), new ObjectFormatter<GetEnvironmentConfigurationRequest>(),
                new ObjectFormatter<GetEnvironmentConfigurationResponse>(), new ObjectFormatter<GetDatabaseListRequest>(),
                new ObjectFormatter<GetDatabaseListResponse>(), new ObjectFormatter<AlterDatabaseListRequest>(),
                new ObjectFormatter<AlterDatabaseListResponse>(), new ObjectFormatter<GetDatabaseConfigurationRequest>(),
                new ObjectFormatter<GetDatabaseConfigurationResponse>(), new ObjectFormatter<AlterDatabaseConfigurationRequest>(),
                new ObjectFormatter<AlterDatabaseConfigurationResponse>(), new ObjectFormatter<GetDatabaseAccessModeRequest>(),
                new ObjectFormatter<GetDatabaseAccessModeResponse>(), new ObjectFormatter<SetDatabaseAccessModeRequest>(),
                new ObjectFormatter<SetDatabaseAccessModeResponse>(), new ObjectFormatter<UnloadDatabaseRequest>(),
                new ObjectFormatter<UnloadDatabaseResponse>(), new ObjectFormatter<LoadDatabaseRequest>(),
                new ObjectFormatter<LoadDatabaseResponse>(), new ObjectFormatter<RefreshDatabaseCryptoKeyRequest>(),
                new ObjectFormatter<RefreshDatabaseCryptoKeyResponse>(), new ObjectFormatter<RefreshEnvironmentCryptoKeyRequest>(),
                new ObjectFormatter<RefreshEnvironmentCryptoKeyResponse>(), new ObjectFormatter<CleanupRemovedDatabasesRequest>(),
                new ObjectFormatter<CleanupRemovedDatabasesResponse>(), new ObjectFormatter<FileContent>(),
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
                new AnyTypeFormatter(typeof(object)),
                new AnyTypeFormatter(typeof(DbDynamic)),
                new DictionaryFormatter(),
                new ValueFormatter<DbKey>(),
                new ValueFormatter<DatabaseAccessMode>(),
                new ValueFormatter<TypeAndFields>(),
                new ValueFormatter<FileRange>(),
                new ValueFormatter<FieldDefinition>(),
                new ValueFormatter<IndexFieldDefinition>(),
                new ValueFormatter<SecondaryIndexDefinition>(),
                new ValueFormatter<ReferentialIndexDefinition>(),
                new ValueFormatter<TextIndexDefinition>(),
                new ListFormatter(typeof(IList)),
                new ListFormatter(typeof(List<>)),
                new ListFormatter(typeof(IList<>)),
                new ListFormatter(typeof(Array))
            };
        }

        public ClientTypeBinder()
            : this(null, typeof(DbObject), "DbObject", KnownDatabaseTypes, CreateKnownFormatters())
        {
        }

        public ClientTypeBinder(string databaseConfigurationNdefText)
            : this(new ClientTypeSystem(databaseConfigurationNdefText), typeof(DbObject), "DbObject",
                  KnownDatabaseTypes, CreateKnownFormatters())
        {
        }

        public ClientTypeBinder(string databaseConfigurationNdefText, IEnumerable<Type> databaseTypes)
            : this(new ClientTypeSystem(databaseConfigurationNdefText), typeof(DbObject), "DbObject",
                  KnownDatabaseTypes.Concat(databaseTypes), CreateKnownFormatters())
        {
        }

        public ClientTypeBinder(IEnumerable<TypeDefinition> databaseTypeDefinitions, IEnumerable<Type> databaseTypes)
            : this(new ClientTypeSystem(databaseTypeDefinitions), typeof(DbObject), "DbObject",
                  KnownDatabaseTypes.Concat(databaseTypes), CreateKnownFormatters())
        {
        }

        public ClientTypeBinder(IEnumerable<TypeDefinition> databaseTypeDefinitions, IEnumerable<Type> databaseTypes,
            IEnumerable<INdefFormatter> formatters)
            : this(new ClientTypeSystem(databaseTypeDefinitions), typeof(DbObject), "DbObject",
                KnownDatabaseTypes.Concat(databaseTypes), CreateKnownFormatters().Concat(formatters))
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

        public virtual NdefTypeInfo LookupTypeInfoByTypeOld(Type type)
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
                if (!typeName.EndsWith(NdefConst.ListTypeBraces))
                {
                    Assembly assembly = typeof(object).Assembly;
                    Type systemType = assembly.GetType("System." + typeName);
                    if (!fTypeInfoByType.TryGetValue(systemType, out result))
                        throw new NezaboodkaException(string.Format("type name {0} is not registered in {1}",
                            typeName, this.GetType().FullName));
                }
                else
                    result = LookupTypeInfoByType(PreferredListType);
            }
            return result;
        }

        public virtual NdefTypeInfo LookupTypeInfoByType(Type type)
        {
            NdefTypeInfo result;
            if (!fTypeInfoByType.TryGetValue(type, out result))
            {
                Type generic = null;
                if (type.IsConstructedGenericType)
                    generic = type.GetGenericTypeDefinition();
                else if (type.IsArray)
                    generic = typeof(Array);
                if (generic == null || !fTypeInfoByType.TryGetValue(generic, out result))
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
            }
            return result;
        }

        public virtual NdefTypeInfo LookupTypeInfoByField(NdefTypeInfo typeInfo,
            NdefField ndefField, bool adjustToActualType, out Type formalType)
        {
            NdefTypeInfo result;
            if (typeInfo.IsListType)
            {
                if (ndefField.Name == null)
                {
                    formalType = NdefUtils.GetElementType(typeInfo.SystemType, RootTypeForObjectsWithKey);
                    result = LookupTypeInfoByType(formalType);
                }
                else
                    throw new ArgumentException("list type cannot have named fields");
            }
            else if (ndefField.Name != null)
            {
                FieldInfo fieldInfo = typeInfo.SystemType.GetField(
                    ndefField.Name, BindingFlags.Public | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    formalType = fieldInfo.FieldType;
                    result = LookupTypeInfoByType(formalType);
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
                        formalType = PreferredListType.MakeGenericType(typeInfo.SystemType);
                        result = LookupTypeInfoByType(formalType);
                    }
                    else
                        throw new NotImplementedException("internal error");
                }
            }
            else
                throw new ArgumentNullException("cannot look up field type by empty name");
            return result;
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

        protected ClientTypeBinder(ClientTypeSystem typeSystem, Type rootTypeForObjectsWithKey,
            string rootTypeName, IEnumerable<Type> databaseTypes,
            IEnumerable<INdefFormatter> formatters)
        {
            TypeSystem = typeSystem;
            RootTypeForObjectsWithKey = rootTypeForObjectsWithKey;
            PreferredListType = typeof(List<>);
            if (formatters != null)
                foreach (INdefFormatter x in formatters)
                    RegisterType(-1, x.FormalType, x is ListFormatter, x.SerializableTypeName, x);
            if (typeSystem != null)
                RegisterDatabaseTypesFromTypeSystem(rootTypeName, databaseTypes);
            else
                RegisterDatabaseTypesWithoutTypeSystem(rootTypeName, databaseTypes);
            // Initialize & configure all formatters
            var assemblyName = new AssemblyName("Z_" + GetType().Name + "_" + Guid.NewGuid().ToString("N"));
            var codegen = new CodeGenerator(assemblyName);
            IEnumerable<NdefTypeInfo> allTypes = fTypeInfoByType.Values.Concat(fTypeInfoByName.Values).Distinct();
            IEnumerable<INdefFormatter> allFormatters = allTypes.Select(x => x.Formatter).Distinct().ToList();
            foreach (INdefFormatter x in allFormatters)
                x.Initialize(this, codegen);
            codegen.GetGeneratedAssembly();
            foreach (INdefFormatter x in allFormatters)
                x.Configure(this, codegen);
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
                NdefTypeInfo typeInfo;
                if (databaseTypesDictionary.TryGetValue(typeName, out type))
                    typeInfo = RegisterType(i, type, false, typeName,
                        CreateDbObjectFormatter(type, typeName));
                else
                    typeInfo = RegisterDynamicType(i, typeName);
            }
        }

        private void RegisterDatabaseTypesWithoutTypeSystem(string rootTypeName, IEnumerable<Type> databaseTypes)
        {
            if (databaseTypes != null)
            {
                int i = 0;
                foreach (Type x in databaseTypes)
                {
                    NdefTypeInfo typeInfo;
                    if (x != RootTypeForObjectsWithKey)
                        typeInfo = RegisterType(i, x, false, x.Name, CreateDbObjectFormatter(x, x.Name));
                    else
                        typeInfo = RegisterType(i, x, false, rootTypeName, CreateDbObjectFormatter(x, rootTypeName));
                    i++;
                }
            }
            else
                RegisterType(0, RootTypeForObjectsWithKey, false, rootTypeName,
                    CreateDbObjectFormatter(RootTypeForObjectsWithKey, rootTypeName));
        }

        private NdefTypeInfo RegisterType(int typeNumber, Type type, bool isListType, string serializableName,
            INdefFormatter formatter)
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
            return typeInfo;
        }

        private NdefTypeInfo RegisterDynamicType(int typeNumber, string serializableName)
        {
            var formatter = new DbDynamicFormatter(serializableName);
            NdefTypeInfo result = new NdefTypeInfo(typeNumber, typeof(DbDynamic), false, serializableName, formatter);
            fTypeInfoByName.Add(serializableName, result);
            return result;
        }

        protected virtual INdefFormatter CreateDbObjectFormatter(Type type, string serializableName)
        {
            Type formatterType = typeof(DbObjectFormatter<>).MakeGenericType(type);
            INdefFormatter result = (INdefFormatter)Activator.CreateInstance(formatterType, serializableName);
            return result;
        }
    }
}
