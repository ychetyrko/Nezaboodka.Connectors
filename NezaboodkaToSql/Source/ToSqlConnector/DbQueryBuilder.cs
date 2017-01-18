using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nezaboodka.ToSqlConnector
{
    public static class DbQueryBuilder
    {
        // Public

        public static string GetDatabaseListQuery =>
            $"SELECT `{AdminDatabaseConst.NameField}`" +
            $"FROM `{AdminDatabaseConst.DatabasesList}`;";

        public static string GetDatabaseAccessModeQuery(string dbName)
        {
            return $"SELECT `{AdminDatabaseConst.AccessField}` " +
                   $"FROM `{AdminDatabaseConst.DatabasesList}` " +
                   $"WHERE `{AdminDatabaseConst.NameField}` = '{dbName}' " +
                   "LIMIT 1;";  // optimization
        }

        public static string AlterDatabaseListQuery(IEnumerable<string> namesToRemove, IEnumerable<string> namesToAdd)
        {
            string result = string.Empty;

            if (namesToRemove != null)
                result += RemoveDatabaseListPrepareQuery(namesToRemove);

            if (namesToAdd != null)
                result += AddDatabaseListPrepareQuery(namesToAdd);

            result += "CALL alter_database_list();";

            result += GetDatabaseListQuery;

            return result;
        }

        public static string GetDatabaseConfigurationQuery(string dbName)
        {
            var result = $"SELECT `{SchemaFieldConst.TypeName}`, " +
                                $"`{SchemaFieldConst.BaseTypeName}` " +
                         $"FROM `{dbName}`.`{SchemaTableConst.TypeTableName}`;" +

                         $"SELECT `{SchemaFieldConst.FieldName}`, " +
                                $"`{SchemaFieldConst.FieldOwnerTypeName}`, " +
                                $"`{SchemaFieldConst.FieldTypeName}`, " +
                                $"`{SchemaFieldConst.FieldIsList}`, " +
                                $"`{SchemaFieldConst.FieldBackRefName}`, " +
                                $"`{SchemaFieldConst.FieldCompareOptions}` " +
                         $"FROM `{dbName}`.`{SchemaTableConst.FieldTableName}`;";

            // TODO: add Secondary and Referencial indexes

            return result;
        }

        public static string AlterDatabaseConfigurationQuery(string dbName, DatabaseConfiguration config)
        {
            string result = string.Empty;

            result += AlterDatabaseSchemaQuery(dbName, config.DatabaseSchema);
            // TODO: process SecondaryIndexDefinitions & ReferentialIntexDefinitions

            return result;
        }

        // Private

        private static string AlterDatabaseSchemaQuery(string dbName, DatabaseSchema schema)
        {
            IEnumerable<TypeDefinition> typeDefinitionsList = schema.TypeDefinitions;

            List<string> typesList = new List<string>();
            List<string> fieldsList = new List<string>();

            foreach (var typeDefinition in typeDefinitionsList)
            {
                string currentTypeName = typeDefinition.TypeName;
                string tableName = GenerateLowerName(currentTypeName);
                string typeRec = $"'{currentTypeName}', '{tableName}', '{typeDefinition.BaseTypeName}'";

                typesList.Add(typeRec);

                foreach (var fieldDefinition in typeDefinition.FieldDefinitions)
                {
                    string columnName = GenerateLowerName(fieldDefinition.FieldName);
                    string fieldTypeName = fieldDefinition.FieldTypeName;   // TODO: .NET to SQL type mappping

                    string fieldRec = $"'{fieldDefinition.FieldName}', '{columnName}', '{currentTypeName}', '{fieldTypeName}', {fieldDefinition.IsList.ToString().ToUpper()}, '{fieldDefinition.CompareOptions:g}', '{fieldDefinition.BackReferenceFieldName}'";

                    fieldsList.Add(fieldRec);
                }
            }

            string typesListStr = FormatValuesList(typesList);
            string fieldsListStr = FormatValuesList(fieldsList);

            return $"INSERT INTO `{dbName}`.`{SchemaTableConst.TypeTableName}` " +
                   $"(`{SchemaFieldConst.TypeName}`, `{SchemaFieldConst.TableName}`, `{SchemaFieldConst.BaseTypeName}`) " +
                   $"VALUES {typesListStr}; " +

                   $"INSERT INTO `{dbName}`.`{SchemaTableConst.FieldTableName}` " +
                   $"(`{SchemaFieldConst.FieldName}`, `{SchemaFieldConst.FieldColumnName}`, `{SchemaFieldConst.FieldOwnerTypeName}`, `{SchemaFieldConst.FieldTypeName}`, `{SchemaFieldConst.FieldIsList}`, `{SchemaFieldConst.FieldCompareOptions}`, `{SchemaFieldConst.FieldBackRefName}`) " +
                   $"VALUES {fieldsListStr}; " +

                   $"CALL alter_db_schema('{dbName}');";
        }

        private static string RemoveDatabaseListPrepareQuery(IEnumerable<string> namesList)
        {
            string namesListStr = FormatStringList(namesList);

            if (string.IsNullOrEmpty(namesListStr))
            {
                return string.Empty;
            }

            return $"INSERT INTO `{AdminDatabaseConst.RemoveDbList}` " +
                   $"(`{AdminDatabaseConst.NameField}`) " +
                   $"VALUES {namesListStr};";
        }

        private static string AddDatabaseListPrepareQuery(IEnumerable<string> namesList)
        {
            string namesListStr = FormatStringList(namesList);

            if (string.IsNullOrEmpty(namesListStr))
            {
                return string.Empty;
            }

            return $"INSERT INTO `{AdminDatabaseConst.AddDbList}` " +
                   $"(`{AdminDatabaseConst.NameField}`) " +
                   $"VALUES {namesListStr};";
        }

        private static string FormatValuesList(IEnumerable<string> values)
        {
            return FormatListExt(values, "(", ")");
        }

        private static string FormatStringList(IEnumerable<string> strList)
        {
            return FormatListExt(strList, "('", "')");
        }

        private static string FormatListExt(IEnumerable<string> strList, string pre, string post, string separator = ",")
        {
            return string.Join(separator, strList.Select(s => pre + s + post));
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

}
