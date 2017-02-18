using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nezaboodka.ToSqlConnector
{
    public static class DbQueryBuilder
    {
        public const string CleanupRemovedDatabasesQuery = "CALL cleanup_removed_databases();";

        public const string GetDatabaseListQuery =
            "SELECT `" + AdminDatabaseConst.NameField + "` " +
            "FROM `" + AdminDatabaseConst.DatabasesList + "` " +
            "WHERE NOT `" + AdminDatabaseConst.IsRemovedField + "`;";

        // Public

        public static string GetDatabaseAccessModeQuery(string dbName)
        {
            return "SELECT `" + AdminDatabaseConst.AccessField + "` " +
                   "FROM `" + AdminDatabaseConst.DatabasesList + "` " +
                   "WHERE `" + AdminDatabaseConst.NameField + $"` = '{dbName}' " +
                   "LIMIT 1;";
        }

        public static string AlterDatabaseListQuery(IEnumerable<string> namesToRemove, IEnumerable<string> namesToAdd)
        {
            string result = BeforeAlterDatabaseListQuery;
            if (namesToRemove != null)
                result += RemoveDatabaseListPrepareQuery(namesToRemove);
            if (namesToAdd != null)
                result += AddDatabaseListPrepareQuery(namesToAdd);
            result += CallAlterDatabaseListProc;
            result += GetDatabaseListQuery;
            return result;
        }

        public static string GetDatabaseConfigurationQuery(string dbName)
        {
            var result = "SELECT `" + SchemaFieldConst.TypeName + "`, " +
                                "`" + SchemaFieldConst.BaseTypeName + "` " +
                         $"FROM `{dbName}`.`" + SchemaTableConst.TypeTableName + "`; " +
                         "SELECT `" + SchemaFieldConst.FieldName + "`, " +
                                "`" + SchemaFieldConst.FieldOwnerTypeName + "`, " +
                                "`" + SchemaFieldConst.FieldTypeName + "`, " +
                                "`" + SchemaFieldConst.FieldIsList + "`, " +
                                "`" + SchemaFieldConst.FieldBackRefName + "`, " +
                                "`" + SchemaFieldConst.FieldCompareOptions + "` " +
                         $"FROM `{dbName}`.`" + SchemaTableConst.FieldTableName + "`;";
            // TODO: get SecondaryIndexDefinitions & ReferentialIntexDefinitions
            return result;
        }

        public static string AlterDatabaseConfigurationQuery(string dbName, DatabaseConfiguration config)
        {
            string result = string.Empty;
            result += AlterDatabaseTypesFieldsQuery(dbName, config.DatabaseSchema);
            // TODO: process SecondaryIndexDefinitions & ReferentialIntexDefinitions
            return result;
        }

        // Internal

        private static string AlterDatabaseTypesFieldsQuery(string dbName, DatabaseSchema schema)
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
                    string fieldTypeName = fieldDefinition.FieldTypeName;
                    fieldTypeName = NezaboodkaSqlTypeMapper.SqlTypeNameByNezaboodkaTypeName(fieldTypeName);
                    string fieldRec =
                        $"'{fieldDefinition.FieldName}', '{columnName}', '{currentTypeName}', '{fieldTypeName}', {fieldDefinition.IsList.ToString().ToUpper()}, '{fieldDefinition.CompareOptions:g}', '{fieldDefinition.BackReferenceFieldName}'";
                    fieldsList.Add(fieldRec);
                }
            }
            string typesListStr = FormatValuesList(typesList);
            string fieldsListStr = FormatValuesList(fieldsList);
            var result =
                "CALL before_alter_database_schema(); " +
                "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.AddTypeList + "` " +
                "(" +
                    "`" + SchemaFieldConst.TypeName + "`, " +
                    "`" + SchemaFieldConst.TableName + "`, " +
                    "`" + SchemaFieldConst.BaseTypeName + "`" +
                ") " +
                $"VALUES {typesListStr}; " +
                "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.AddFieldList + "` " +
                "(" +
                    "`" + SchemaFieldConst.FieldName + "`, " +
                    "`" + SchemaFieldConst.FieldColumnName + "`, " +
                    "`" + SchemaFieldConst.FieldOwnerTypeName + "`, " +
                    "`" + SchemaFieldConst.FieldTypeName + "`, " +
                    "`" + SchemaFieldConst.FieldIsList + "`, " +
                    "`" + SchemaFieldConst.FieldCompareOptions + "`, " +
                    "`" + SchemaFieldConst.FieldBackRefName + "`" +
                ") " +
                $"VALUES {fieldsListStr}; " +
                $"CALL alter_database_Schema('{dbName}');";
            return result;
        }

        private static string RemoveDatabaseListPrepareQuery(IEnumerable<string> namesList)
        {
            return InsertIntoNamesListPrepareQuery(namesList, AdminDatabaseConst.RemoveDbList);
        }

        private static string AddDatabaseListPrepareQuery(IEnumerable<string> namesList)
        {
            return InsertIntoNamesListPrepareQuery(namesList, AdminDatabaseConst.AddDbList);
        }

        private static string InsertIntoNamesListPrepareQuery(IEnumerable<string> namesList, string tableName)
        {
            string result = string.Empty;
            string namesListStr = FormatStringList(namesList);
            if (!string.IsNullOrEmpty(namesListStr))
            {
                result = $"INSERT INTO `{tableName}` " +
                         $"(`{AdminDatabaseConst.NameField}`) " +
                         $"VALUES {namesListStr};";
            }
            return result;
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

        // Constants

        private const string BeforeAlterDatabaseListQuery = "CALL before_alter_database_list();";
        private const string CallAlterDatabaseListProc = "CALL alter_database_list();";
    }
}
