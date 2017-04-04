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
            DatabaseSchemaDiff diff = DatabaseSchemaUtils.GetDiff(oldConfig.DatabaseSchema, newConfig.DatabaseSchema);
            result += AlterDatabaseTypesFieldsQuery(dbName, diff);
            // TODO: process SecondaryIndexDefinitions & ReferentialIntexDefinitions
            return result;
        }

        // Internal

        private static string AlterDatabaseTypesFieldsQuery(string dbName, DatabaseSchemaDiff diff)
        {
            string removeTypesPart = GetRemoveTypesPart(diff);
            string addTypesPart = GetAddTypesPart(diff);
            string removeFieldsPart = GetRemoveFieldsPart(diff);
            string addFieldsPart = GetAddFieldsPart(diff);
            string backrefUpdatePart = GetBackRefUpdatePart(diff);
            string result =
                CallBeforeAlterDatabaseSchema + " " +
                removeTypesPart + " " + addTypesPart + " " +
                removeFieldsPart + " " + addFieldsPart + " " +
                backrefUpdatePart + " " +
                CallAlterDatabaseSchema(dbName);
            return result;
        }

        private static string GetRemoveTypesPart(DatabaseSchemaDiff diff)
        {
            string result = string.Empty;
            string typesRemoveListStr = FormatValuesList(diff.TypesToRemove);
            if (!string.IsNullOrEmpty(typesRemoveListStr))
            {
                result =
                    "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.RemoveTypeList + "` " +
                    "(" +
                        "`" + SchemaFieldConst.TypeName + "`" +
                    ") " +
                    $"VALUES {typesRemoveListStr};";
            }
            return result;
        }

        private static string GetAddTypesPart(DatabaseSchemaDiff diff)
        {
            string result = string.Empty;
            string typesAddListStr = FormatValuesList(diff.TypesToAdd);
            if (!string.IsNullOrEmpty(typesAddListStr))
            {
                result =
                    "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.AddTypeList + "` " +
                    "(" +
                        "`" + SchemaFieldConst.TypeName + "`, " +
                        "`" + SchemaFieldConst.TableName + "`, " +
                        "`" + SchemaFieldConst.BaseTypeName + "`" +
                    ") " +
                    $"VALUES {typesAddListStr};";
            }
            return result;
        }

        private static string GetRemoveFieldsPart(DatabaseSchemaDiff diff)
        {
            string result = string.Empty;
            string fieldsRemoveListStr = FormatValuesList(diff.FieldsToRemove);
            if (!string.IsNullOrEmpty(fieldsRemoveListStr))
            {
                result =
                    "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.RemoveFieldList + "` " +
                    "(" +
                        "`" + SchemaFieldConst.FieldOwnerTypeName + "`, " +
                        "`" + SchemaFieldConst.FieldName + "`" +
                    ") " +
                    $"VALUES {fieldsRemoveListStr};";
            }
            return result;
        }

        private static string GetAddFieldsPart(DatabaseSchemaDiff diff)
        {
            string result = string.Empty;
            string fieldsAddListStr = FormatValuesList(diff.FieldsToAdd);
            if (!string.IsNullOrEmpty(fieldsAddListStr))
            {
                result =
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
            return result;
        }

        private static string GetBackRefUpdatePart(DatabaseSchemaDiff diff)
        {
            string result = string.Empty;
            string backrefUpdateListStr = FormatValuesList(diff.BackRefsToUpdate);
            if (!string.IsNullOrEmpty(backrefUpdateListStr))
            {
                result =
                    "INSERT INTO `" + AdminDatabaseConst.AdminDbName + "`.`" + AdminDatabaseConst.UpdateBackRefsList + "` " +
                    "(" +
                        "`" + SchemaFieldConst.BackRefFieldOwnerTypeName + "`, " +
                        "`" + SchemaFieldConst.BackRefFieldName + "`, " +
                        "`" + SchemaFieldConst.BackRefNewRefFieldName + "`" +
                    ") " +
                    $"VALUES {backrefUpdateListStr};";
            }
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
        private const string CallBeforeAlterDatabaseSchema = "CALL before_alter_database_schema(); ";

        private static string CallAlterDatabaseSchema(string dbName) => $"CALL alter_database_Schema('{dbName}');";
    }
}
