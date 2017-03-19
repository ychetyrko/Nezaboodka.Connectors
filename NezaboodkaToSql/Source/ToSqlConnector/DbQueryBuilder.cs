using System.Collections.Generic;
using System.Linq;

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
                                "`" + SchemaFieldConst.FieldTypeIsNullable + "`, " +
                                "`" + SchemaFieldConst.FieldIsList + "`, " +
                                "`" + SchemaFieldConst.FieldBackRefName + "`, " +
                                "`" + SchemaFieldConst.FieldCompareOptions + "` " +
                         $"FROM `{dbName}`.`" + SchemaTableConst.FieldTableName + "`;";
            // TODO: get SecondaryIndexDefinitions & ReferentialIntexDefinitions
            return result;
        }

        public static string AlterDatabaseConfigurationQuery(string dbName, DatabaseConfiguration oldConfig,
            DatabaseConfiguration newConfig)
        {
            string result = string.Empty;
            result += AlterDatabaseTypesFieldsQuery(dbName, oldConfig.DatabaseSchema, newConfig.DatabaseSchema);
            // TODO: process SecondaryIndexDefinitions & ReferentialIntexDefinitions
            return result;
        }

        // Internal

        private static string AlterDatabaseTypesFieldsQuery(string dbName, DatabaseSchema oldSchema,
            DatabaseSchema newSchema)
        {
            DatabaseSchemaDiff diff = DatabaseSchemaUtils.GetDiff(oldSchema, newSchema);

            string typesRemoveListStr = FormatValuesList(diff.TypesToRemove);
            string typesAddListStr = FormatValuesList(diff.TypesToAdd);
            string fieldsRemoveListStr = FormatValuesList(diff.FieldsToRemove);
            string fieldsAddListStr = FormatValuesList(diff.FieldsToAdd);
            string backrefUpdateListStr = FormatValuesList(diff.BackRefsToUpdate);

            string removeTypesPart = string.Empty;
            string addTypesPart = string.Empty;
            string removeFieldsPart = string.Empty;
            string addFieldsPart = string.Empty;
            string backrefUpdatePart = string.Empty;

            if (!string.IsNullOrEmpty(typesRemoveListStr))
            {
                removeTypesPart =
                    "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.RemoveTypeList + "` " +
                    "(" +
                        "`" + SchemaFieldConst.TypeName + "`" +
                    ") " +
                    $"VALUES {typesRemoveListStr};";
            }

            if (!string.IsNullOrEmpty(typesAddListStr))
            {
                addTypesPart =
                    "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.AddTypeList + "` " +
                    "(" +
                        "`" + SchemaFieldConst.TypeName + "`, " +
                        "`" + SchemaFieldConst.TableName + "`, " +
                        "`" + SchemaFieldConst.BaseTypeName + "`" +
                    ") " +
                    $"VALUES {typesAddListStr};";
            }

            if (!string.IsNullOrEmpty(fieldsRemoveListStr))
            {
                removeFieldsPart =
                    "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.RemoveFieldList + "` " +
                    "(" +
                        "`" + SchemaFieldConst.FieldOwnerTypeName + "`, " +
                        "`" + SchemaFieldConst.FieldName + "`" +
                    ") " +
                    $"VALUES {fieldsRemoveListStr};";
            }

            if (!string.IsNullOrEmpty(fieldsAddListStr))
            {
                addFieldsPart =
                    "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.AddFieldList + "` " +
                    "(" +
                        "`" + SchemaFieldConst.FieldName + "`, " +
                        "`" + SchemaFieldConst.FieldColumnName + "`, " +
                        "`" + SchemaFieldConst.FieldOwnerTypeName + "`, " +
                        "`" + SchemaFieldConst.FieldTypeName + "`, " +
                        "`" + SchemaFieldConst.FieldTypeIsNullable + "`, " +
                        "`" + SchemaFieldConst.FieldIsList + "`, " +
                        "`" + SchemaFieldConst.FieldCompareOptions + "`" +
                    ") " +
                    $"VALUES {fieldsAddListStr};";
            }

            if (!string.IsNullOrEmpty(backrefUpdateListStr))
            {
                backrefUpdatePart =
                    "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.UpdateBackRefsList + "` " +
                    "(" +
                        "`" + SchemaFieldConst.BackRefFieldOwnerTypeName + "`, " +
                        "`" + SchemaFieldConst.BackRefFieldName + "`, " +
                        "`" + SchemaFieldConst.BackRefNewRefFieldName + "`" +
                    ") " +
                    $"VALUES {backrefUpdateListStr};";
            }

            var result =
                "CALL before_alter_database_schema(); " +
                removeTypesPart + " " + addTypesPart + " " +
                removeFieldsPart + " " + addFieldsPart + " " +
                backrefUpdatePart + " " +
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

        // Constants

        private const string BeforeAlterDatabaseListQuery = "CALL before_alter_database_list();";
        private const string CallAlterDatabaseListProc = "CALL alter_database_list();";
    }
}
