using System.Collections;
using System.Collections.Generic;

namespace Nezaboodka
{
    public class ClientTypeSystem
    {
        public static TypeDefinition DbObjectTypeDefinition = new TypeDefinition()
        {
            TypeName = "DbObject",
            BaseTypeName = string.Empty,
            FieldDefinitions = new List<FieldDefinition>()
        };
        public static readonly TypeDefinition FileObjectTypeDefinition = new TypeDefinition()
        {
            TypeName = "FileObject",
            BaseTypeName = "DbObject",
            FieldDefinitions = new List<FieldDefinition>() {
                new FieldDefinition() { FieldName = "FileName", FieldTypeName = "String" },
                new FieldDefinition() { FieldName = "FileLength", FieldTypeName = "Int64" },
                new FieldDefinition() { FieldName = "OverwriteCount", FieldTypeName = "Int64" },
                new FieldDefinition() { FieldName = "AppendCount", FieldTypeName = "Int64" },
                new FieldDefinition() { FieldName = "CreationTimeUtc", FieldTypeName = "DateTime" },
                new FieldDefinition() { FieldName = "LastWriteTimeUtc", FieldTypeName = "DateTime" },
                new FieldDefinition() { FieldName = "HashValue", FieldTypeName = "String" },
                new FieldDefinition() { FieldName = "ContentType", FieldTypeName = "String" },
                new FieldDefinition() { FieldName = "FileContent", FieldTypeName = "Object" } }
        };

        // Fields

        private Dictionary<string, int> fTypeNumberByName;
        private int[] fBaseTypeNumbers;
        private Dictionary<string, int> fFieldNumberByTypeAndFieldName;
        private FieldDefInfo[][] fFieldDefs;

        // Public

        public List<TypeDefinition> TypeDefinitions { get; private set; }

        public ClientTypeSystem()
        {
            Initialize(null);
        }

        public ClientTypeSystem(string databaseConfigurationNdefText)
        {
            DatabaseConfiguration databaseConfig = DatabaseConfiguration.CreateFromNdefText(databaseConfigurationNdefText);
            Initialize(databaseConfig.DatabaseSchema.TypeDefinitions);
        }

        public ClientTypeSystem(IEnumerable<TypeDefinition> typeDefinitions)
        {
            Initialize(typeDefinitions);
        }

        public int GetTypeCount()
        {
            return fBaseTypeNumbers.Length; // optimized equivalent of TypeDefinitions.Count
        }

        public string GetTypeName(int typeNumber)
        {
            string result = string.Empty;
            if (typeNumber != -1)
                result = TypeDefinitions[typeNumber].TypeName;
            return result;
        }

        public int GetTypeNumberByName(string typeName)
        {
            int result;
            if (!fTypeNumberByName.TryGetValue(typeName, out result))
                result = -1;
            return result;
        }

        public int GetBaseTypeNumber(int typeNumber)
        {
            int result = -1;
            if (typeNumber != -1)
                result = fBaseTypeNumbers[typeNumber];
            return result;
        }

        public bool IsTypeAssignableFromAnotherType(int theTypeNumber, int fromTypeNumber)
        {
            while (theTypeNumber != fromTypeNumber && fromTypeNumber > 0)
                fromTypeNumber = fBaseTypeNumbers[fromTypeNumber];
            return theTypeNumber == fromTypeNumber;
        }

        public int GetFieldCount(int typeNumber)
        {
            return fFieldDefs[typeNumber].Length;
        }

        public string GetFieldName(int typeNumber, int fieldNumber)
        {
            return fFieldDefs[typeNumber][fieldNumber].FieldDefinition.FieldName;
        }

        public string GetFieldTypeName(int typeNumber, int fieldNumber)
        {
            return fFieldDefs[typeNumber][fieldNumber].FieldDefinition.FieldTypeName;
        }

        public FieldKind GetFieldKind(int typeNumber, int fieldNumber)
        {
            FieldKind result;
            FieldDefInfo fieldDefInfo = fFieldDefs[typeNumber][fieldNumber];
            if (fieldDefInfo.FieldDefinition.IsList)
                if (fieldDefInfo.BackReferenceTypeNumber >= 0)
                    result = FieldKind.ObjectList;
                else
                    result = FieldKind.ValueList;
            else
                if (fieldDefInfo.BackReferenceTypeNumber >= 0)
                    result = FieldKind.Object;
                else
                    result = FieldKind.Value;
            return result;
        }

        public int GetFieldNumberByName(int typeNumber, string fieldName)
        {
            return GetFieldNumberByTypeAndFieldName(TypeDefinitions[typeNumber].TypeName, fieldName);
        }

        public int GetFieldNumberByTypeAndFieldName(string typeName, string fieldName)
        {
            int result;
            if (!fFieldNumberByTypeAndFieldName.TryGetValue(GetTypeAndFieldName(typeName, fieldName), out result))
                result = -1;
            return result;
        }

        public FieldDefinition GetFieldDefinition(int typeNumber, int fieldNumber)
        {
            return fFieldDefs[typeNumber][fieldNumber].FieldDefinition;
        }

        public void GetFieldBackReferenceInfo(int typeNumber, int fieldNumber,
            out int backReferenceTypeNumber, out int backReferenceFieldNumber)
        {
            FieldDefInfo fieldDefInfo = fFieldDefs[typeNumber][fieldNumber];
            backReferenceTypeNumber = fieldDefInfo.BackReferenceTypeNumber;
            backReferenceFieldNumber = fieldDefInfo.BackReferenceFieldNumber;
        }

        protected virtual void AddKnownTypeDefinitions()
        {
            TypeDefinitions.Add(DbObjectTypeDefinition);
            TypeDefinitions.Add(FileObjectTypeDefinition);
        }

        // Internal

        private void Initialize(IEnumerable<TypeDefinition> typeDefinitions)
        {
            InitializeTypeDefinitions(typeDefinitions);
            InitializeTypeDictionary();
            InitializeBaseTypeNumbers();
            InitializeFieldDictionary();
            InitializeFields();
        }

        private void InitializeTypeDefinitions(IEnumerable<TypeDefinition> typeDefinitions)
        {
            TypeDefinitions = new List<TypeDefinition>();
            AddKnownTypeDefinitions();
            TypeDefinitions.AddRange(typeDefinitions);
        }

        private void InitializeTypeDictionary()
        {
            fTypeNumberByName = new Dictionary<string, int>();
            for (int i = 0; i < TypeDefinitions.Count; i++)
            {
                TypeDefinition typeDef = TypeDefinitions[i];
                fTypeNumberByName.Add(typeDef.TypeName, i);
            }
        }

        private void InitializeBaseTypeNumbers()
        {
            fBaseTypeNumbers = new int[TypeDefinitions.Count];
            for (int i = 0; i < fBaseTypeNumbers.Length; i++)
                fBaseTypeNumbers[i] = GetTypeNumberByName(TypeDefinitions[i].BaseTypeName);
            // Check for circular references
            var typesInHierarchy = new BitArray(fBaseTypeNumbers.Length);
            for (int i = 0; i < fBaseTypeNumbers.Length; i++)
            {
                typesInHierarchy.SetAll(false);
                for (int j = i; j >= 0; j = fBaseTypeNumbers[j])
                {
                    if (!typesInHierarchy[j])
                        typesInHierarchy[j] = true;
                    else
                        throw new NezaboodkaException(string.Format("circular reference in type hierarchy of {0}",
                            TypeDefinitions[j].TypeName));
                }
            }
        }

        private void InitializeFieldDictionary()
        {
            fFieldNumberByTypeAndFieldName = new Dictionary<string, int>();
            fFieldDefs = new FieldDefInfo[TypeDefinitions.Count][];
            for (int i = 0; i < TypeDefinitions.Count; i++)
                InitializeFieldDictionaryForTypeName(TypeDefinitions[i].TypeName, i);
        }

        private int InitializeFieldDictionaryForTypeName(string typeName, int baseTypeNumber)
        {
            int result = 0;
            if (baseTypeNumber >= 0)
            {
                int baseTypeFieldCount = InitializeFieldDictionaryForTypeName(typeName,
                    fBaseTypeNumbers[baseTypeNumber]);
                TypeDefinition typeDef = TypeDefinitions[baseTypeNumber];
                List<FieldDefinition> fieldDefs = typeDef.FieldDefinitions;
                for (int j = 0; j < fieldDefs.Count; j++)
                    fFieldNumberByTypeAndFieldName.Add(GetTypeAndFieldName(typeName, fieldDefs[j].FieldName),
                        baseTypeFieldCount + j);
                result = baseTypeFieldCount + fieldDefs.Count;
            }
            return result;
        }

        private void InitializeFields()
        {
            fFieldDefs = new FieldDefInfo[TypeDefinitions.Count][];
            for (int i = 0; i < fFieldDefs.Length; i++)
            {
                int baseFieldCount = CalculateTotalFieldCount(fBaseTypeNumbers[i]);
                List<FieldDefinition> fieldDefs = TypeDefinitions[i].FieldDefinitions;
                fFieldDefs[i] = new FieldDefInfo[baseFieldCount + fieldDefs.Count];
                for (int j = 0; j < fieldDefs.Count; j++)
                {
                    FieldDefinition fieldDef = fieldDefs[j];
                    FieldDefInfo fieldDefInfo;
                    fieldDefInfo.FieldDefinition = fieldDef;
                    fieldDefInfo.BackReferenceTypeNumber = GetTypeNumberByName(fieldDef.FieldTypeName);
                    if (!string.IsNullOrEmpty(fieldDef.BackReferenceFieldName))
                        fieldDefInfo.BackReferenceFieldNumber = GetFieldNumberByTypeAndFieldName(
                            fieldDef.FieldTypeName, fieldDef.BackReferenceFieldName);
                    else
                        fieldDefInfo.BackReferenceFieldNumber = -1;
                    fFieldDefs[i][baseFieldCount + j] = fieldDefInfo;
                }
            }
            for (int i = 0; i < fFieldDefs.Length; i++)
            {
                FieldDefInfo[] fields = fFieldDefs[i];
                int typeNumber = fBaseTypeNumbers[i];
                while (typeNumber >= 0)
                {
                    int fieldCount = TypeDefinitions[typeNumber].FieldDefinitions.Count;
                    FieldDefInfo[] baseFields = fFieldDefs[typeNumber];
                    for (int j = baseFields.Length - fieldCount; j < baseFields.Length; j++)
                        fields[j] = baseFields[j];
                    typeNumber = fBaseTypeNumbers[typeNumber];
                }
            }
        }

        private int CalculateTotalFieldCount(int typeNumber)
        {
            int result = 0;
            if (typeNumber >= 0)
                result = CalculateTotalFieldCount(fBaseTypeNumbers[typeNumber]) +
                    TypeDefinitions[typeNumber].FieldDefinitions.Count;
            return result;
        }

        private static string GetTypeAndFieldName(string typeName, string fieldName)
        {
            return typeName + '.' + fieldName;
        }
    }

    public struct FieldDefInfo
    {
        public FieldDefinition FieldDefinition;
        public int BackReferenceTypeNumber;
        public int BackReferenceFieldNumber;
    }

    public enum FieldKind
    {
        Value = 0,
        ValueList,
        Object,
        ObjectList
    }
}
