using System.Collections.Generic;
using System.Text;

namespace Nezaboodka.ToSqlConnector
{
    static class DatabaseSchemaUtils
    {
        // Public

        public static DatabaseSchemaDiff GetDiff(DatabaseSchema oldDatabaseSchema, DatabaseSchema newDatabaseSchema)
        {
            var result = new DatabaseSchemaDiff();

            ClientTypeSystem oldTypeSystem = new ClientTypeSystem(oldDatabaseSchema.TypeDefinitions);
            ClientTypeSystem newTypeSystem = new ClientTypeSystem(newDatabaseSchema.TypeDefinitions);

            List<string> typesToAdd, fieldsToAdd;
            List<string> typesToRemove, fieldsToRemove;

            GetUpdatedTypesFields(oldTypeSystem, newTypeSystem, out typesToAdd, out fieldsToAdd, out typesToRemove,
                out fieldsToRemove);
            result.TypesToAdd.AddRange(typesToAdd);
            result.FieldsToAdd.AddRange(fieldsToAdd);
            result.TypesToRemove.AddRange(typesToRemove);
            result.FieldsToRemove.AddRange(fieldsToRemove);

            GetNewTypesFields(oldTypeSystem, newTypeSystem, out typesToAdd, out fieldsToAdd);
            result.TypesToAdd.AddRange(typesToAdd);
            result.FieldsToAdd.AddRange(fieldsToAdd);

            return result;
        }

        // Internal

        private static void GetUpdatedTypesFields(ClientTypeSystem oldTypeSystem, ClientTypeSystem newTypeSystem,
            out List<string> typesToAdd, out List<string> fieldsToAdd,
            out List<string> typesToRemove, out List<string> fieldsToRemove)
        {
            typesToAdd = new List<string>();
            fieldsToAdd = new List<string>();
            typesToRemove = new List<string>();
            fieldsToRemove = new List<string>();

            for (int oldTypeNumber = 0; oldTypeNumber < oldTypeSystem.GetTypeCount(); ++oldTypeNumber)
            {
                string typeName = oldTypeSystem.GetTypeName(oldTypeNumber);
                int typeNumber = newTypeSystem.GetTypeNumberByName(typeName);
                if (typeNumber >= 0)
                {
                    if (oldTypeSystem.TypeDefinitions[oldTypeNumber].BaseTypeName ==
                        newTypeSystem.TypeDefinitions[typeNumber].BaseTypeName)
                    {
                        int fieldCount = oldTypeSystem.GetFieldCount(oldTypeNumber);
                        for (int oldFieldNumber = 0; oldFieldNumber < fieldCount; ++oldFieldNumber)
                        {
                            int fieldNumber = newTypeSystem.GetFieldNumberByName(typeNumber,
                                oldTypeSystem.GetFieldName(oldTypeNumber, oldFieldNumber));
                            bool isInherited = IsInheritedField(oldTypeSystem, oldTypeNumber, oldFieldNumber);
                            if (fieldNumber >= 0 && !isInherited)
                            {
                                if (oldTypeSystem.GetFieldTypeName(oldTypeNumber, oldFieldNumber) ==
                                    newTypeSystem.GetFieldTypeName(typeNumber, fieldNumber))
                                {
                                    // TODO: update BackReferences if needed
                                }
                                else
                                {
                                    FieldDefinition fieldDefinition = newTypeSystem.GetFieldDefinition(typeNumber,
                                        fieldNumber);
                                    string fieldAddString = GetAddFieldString(typeName, fieldDefinition);
                                    fieldsToAdd.Add(fieldAddString);
                                    fieldNumber = -1;
                                }
                            }
                            if (fieldNumber == -1 && !isInherited)
                            {
                                FieldDefinition fieldDefinition = oldTypeSystem.GetFieldDefinition(oldTypeNumber,
                                oldFieldNumber);
                                string fieldRemoveString = GetRemoveFieldString(typeName, fieldDefinition);
                                fieldsToRemove.Add(fieldRemoveString);
                            }
                        }

                        for (int newFieldNumber = 0;
                            newFieldNumber < newTypeSystem.GetFieldCount(oldTypeNumber);
                            ++newFieldNumber)
                        {
                            int fieldNumber = oldTypeSystem.GetFieldNumberByName(oldTypeNumber,
                                newTypeSystem.GetFieldName(typeNumber, newFieldNumber));
                            if (fieldNumber == -1)
                            {
                                FieldDefinition fieldDefinition = newTypeSystem.GetFieldDefinition(typeNumber,
                                    newFieldNumber);
                                string fieldAddString = GetAddFieldString(typeName, fieldDefinition);
                                fieldsToAdd.Add(fieldAddString);
                            }
                        }
                    }
                    else
                    {
                        TypeDefinition typeDefinition = newTypeSystem.TypeDefinitions[typeNumber];
                        string typeAddString = GetAddTypeString(typeDefinition);
                        typesToAdd.Add(typeAddString);
                        AddAllTypeFields(fieldsToAdd, typeDefinition);
                        typeNumber = -1;
                    }
                }
                if (typeNumber == -1)
                {
                    TypeDefinition typeDefinition = oldTypeSystem.TypeDefinitions[oldTypeNumber];
                    string typeRemoveString = GetRemoveTypeString(typeDefinition);
                    typesToRemove.Add(typeRemoveString);
                }
            }
        }

        private static bool IsInheritedField(ClientTypeSystem typeSystem, int typeNumber, int fieldNumber)
        {
            bool result = false;
            int baseTypeNumber = typeSystem.GetBaseTypeNumber(typeNumber);
            if (baseTypeNumber >= 0)
            {
                int baseFieldNumber = typeSystem.GetFieldNumberByName(baseTypeNumber,
                    typeSystem.GetFieldName(typeNumber, fieldNumber));
                if (baseFieldNumber >= 0)
                {
                    result = true;
                }
            }
            return result;
        }

        private static void GetNewTypesFields(ClientTypeSystem oldTypeSystem, ClientTypeSystem newTypeSystem,
            out List<string> newTypes, out List<string> newFields)
        {
            newTypes = new List<string>();
            newFields = new List<string>();
            for (int i = 0; i < newTypeSystem.GetTypeCount(); ++i)
            {
                string typeName = newTypeSystem.GetTypeName(i);
                int typeNumber = oldTypeSystem.GetTypeNumberByName(typeName);
                if (typeNumber == -1)
                {
                    TypeDefinition typeDefinition = newTypeSystem.TypeDefinitions[i];
                    string typeAddString = GetAddTypeString(typeDefinition);
                    newTypes.Add(typeAddString);
                    AddAllTypeFields(newFields, typeDefinition);
                }
            }
        }

        private static void AddAllTypeFields(List<string> fieldsList, TypeDefinition typeDefinition)
        {
            foreach (var fieldDefinition in typeDefinition.FieldDefinitions)
            {
                string fieldAddString = GetAddFieldString(typeDefinition.TypeName, fieldDefinition);
                fieldsList.Add(fieldAddString);
            }
        }

        private static string GetAddTypeString(TypeDefinition typeDefinition)
        {
            string currentTypeName = typeDefinition.TypeName;
            string tableName = GenerateLowerName(currentTypeName);
            return $"'{currentTypeName}', '{tableName}', '{typeDefinition.BaseTypeName}'";
        }

        private static string GetRemoveTypeString(TypeDefinition typeDefinition)
        {
            return $"'{typeDefinition.TypeName}'";
        }

        private static string GetAddFieldString(string ownerTypeName, FieldDefinition fieldDefinition)
        {
            string columnName = GenerateLowerName(fieldDefinition.FieldName);
            string fieldTypeName = fieldDefinition.FieldTypeName;
            fieldTypeName = NezaboodkaSqlTypeMapper.SqlTypeNameByNezaboodkaTypeName(fieldTypeName);
            string result =
                $"'{fieldDefinition.FieldName}', '{columnName}', '{ownerTypeName}', '{fieldTypeName}', {fieldDefinition.IsList.ToString().ToUpper()}, '{fieldDefinition.CompareOptions:g}', '{fieldDefinition.BackReferenceFieldName}'";
            return result;
        }

        private static string GetRemoveFieldString(string ownerTypeName, FieldDefinition fieldDefinition)
        {
            return $"'{ownerTypeName}', '{fieldDefinition.FieldName}'";
        }

        private static string GenerateLowerName(string typeName)
        {
            var result = new StringBuilder();

            // TODO: limit result length to 64 symbols (+ unique part for alike results)
            foreach (char c in typeName)
            {
                if (char.IsUpper(c))
                    result.Append('_');
                result.Append(char.ToLower(c));
            }
            return result.ToString();
        }
    }

    class DatabaseSchemaDiff
    {
        public List<string> TypesToRemove = new List<string>();
        public List<string> TypesToAdd = new List<string>();
        public List<string> FieldsToRemove = new List<string>();
        public List<string> FieldsToAdd = new List<string>();
    }
}
