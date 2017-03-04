using System;
using System.Collections.Generic;
using System.Text;

namespace Nezaboodka.ToSqlConnector
{
    static class DatabaseSchemaUtils
    {
        // Public

        public static DatabaseSchemaDiff GetDiff(DatabaseSchema oldDatabaseSchema, DatabaseSchema newDatabaseSchema)
        {
            throw new NotImplementedException();
        }

        // Internal

        private static void GetTypeDefinitionsStringList(DatabaseSchema schema)
        {
            IEnumerable<TypeDefinition> typeDefinitionsList = schema.TypeDefinitions;
            List<string> typesAddList = new List<string>();
            List<string> fieldsAddList = new List<string>();
            foreach (var typeDefinition in typeDefinitionsList)
            {
                string currentTypeName = typeDefinition.TypeName;
                string tableName = GenerateLowerName(currentTypeName);
                string typeRec = $"'{currentTypeName}', '{tableName}', '{typeDefinition.BaseTypeName}'";
                typesAddList.Add(typeRec);
                var currentTypeFieldsList = GetFieldDefinitionsStringList(typeDefinition.FieldDefinitions,
                    currentTypeName);
                fieldsAddList.AddRange(currentTypeFieldsList);
            }
        }

        private static List<string> GetFieldDefinitionsStringList(List<FieldDefinition> fieldDefinitions, string ownerTypeName)
        {
            var result = new List<string>();
            foreach (var fieldDefinition in fieldDefinitions)
            {
                string columnName = GenerateLowerName(fieldDefinition.FieldName);
                string fieldTypeName = fieldDefinition.FieldTypeName;
                fieldTypeName = NezaboodkaSqlTypeMapper.SqlTypeNameByNezaboodkaTypeName(fieldTypeName);
                string fieldRec =
                    $"'{fieldDefinition.FieldName}', '{columnName}', '{ownerTypeName}', '{fieldTypeName}', {fieldDefinition.IsList.ToString().ToUpper()}, '{fieldDefinition.CompareOptions:g}', '{fieldDefinition.BackReferenceFieldName}'";
                result.Add(fieldRec);
            }
            return result;
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
