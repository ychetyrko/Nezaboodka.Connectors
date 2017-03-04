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
            
            List<string> newTypes, newFields;
            GetNewTypesFields(oldTypeSystem, newTypeSystem, out newTypes, out newFields);

            result.typesToAdd.AddRange(newTypes);
            result.fieldsToAdd.AddRange(newFields);

            return result;
        }

        // Internal

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

                    foreach (var fieldDefinition in typeDefinition.FieldDefinitions)
                    {
                        string fieldAddString = GetAddFieldString(typeDefinition.TypeName, fieldDefinition);
                        newFields.Add(fieldAddString);
                    }
                }
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
        public List<string> typesToRemove = new List<string>();
        public List<string> typesToAdd = new List<string>();
        public List<string> fieldsToRemove = new List<string>();
        public List<string> fieldsToAdd = new List<string>();
    }
}
