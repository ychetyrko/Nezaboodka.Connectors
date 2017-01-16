using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nezaboodka.ToSqlConnector
{
    public static class DbQueryBuilder
    {
        // Public

        public static string GetDatabaseListQuery =>
            "SELECT `name`" +
            "FROM `db_list`;";

        public static string GetDatabaseAccessModeQuery(string dbName)
        {
            return "SELECT `access` " +
                   "FROM `db_list` " +
                   $"WHERE `name` = '{dbName}'" +
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

            return result;
        }

        public static string GetDatabaseConfigurationQuery(string dbName)
        {
            var result = $"SELECT `name` AS '{DbSchemaColumnNames.TypeName}', " +
                            $"`base_type_name` AS '{DbSchemaColumnNames.BaseTypeName}' " +
                         $"FROM `{dbName}`.`type`;" +

                         $"SELECT `name` AS '{DbSchemaColumnNames.FieldName}', " +
                            $"`owner_type_name` AS '{DbSchemaColumnNames.FieldOwnerTypeName}', " +
                            $"`type_name` AS '{DbSchemaColumnNames.FieldTypeName}', " +
                            $"`is_list` AS '{DbSchemaColumnNames.FieldIsList}', " +
                            $"`back_ref_name` AS '{DbSchemaColumnNames.FieldBackRefName}', " +
                            $"`compare_options` AS '{DbSchemaColumnNames.FieldCompareOptions}' " +
                         $"FROM `{dbName}`.`field`;";
            // TODO: add Secondary and Referencial indexes

            return result;
        }

        // !! NO MERGE proveded
        // TODO: merge existing schema with new
        public static string AlterDatabaseSchemaQuery(string dbName, IEnumerable<TypeDefinition> typeDefinitionsList)
        {
            List<string> typesList = new List<string>();
            List<string> fieldsList = new List<string>();

            foreach (var typeDefinition in typeDefinitionsList)
            {
                string currentTypeName = typeDefinition.TypeName;
                string tableName = GenerateLowerName(currentTypeName);
                // (`name`, `table_name`, `base_type_name`)
                string typeRec = $"'{currentTypeName}', '{tableName}', '{typeDefinition.BaseTypeName}'";

                typesList.Add(typeRec);

                foreach (var fieldDefinition in typeDefinition.FieldDefinitions)
                {
                    string columnName = GenerateLowerName(fieldDefinition.FieldName);
                    string fieldTypeName = fieldDefinition.FieldTypeName;   // TODO: .NET to SQL type mappping

                    // (`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`)
                    string fieldRec = $"'{fieldDefinition.FieldName}', '{columnName}', '{currentTypeName}', '{fieldTypeName}', {fieldDefinition.IsList.ToString().ToUpper()}, '{fieldDefinition.CompareOptions.ToString("g")}', '{fieldDefinition.BackReferenceFieldName}'";

                    fieldsList.Add(fieldRec);
                }
            }

            string typesListStr = FormatValuesList(typesList);
            string fieldsListStr = FormatValuesList(fieldsList);

            return $"INSERT INTO `{dbName}`.`type` " +
                   "(`name`, `table_name`, `base_type_name`) " +
                   $"VALUES {typesListStr}; " +
                   $"INSERT INTO `{dbName}`.`field` " +
                   "(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`) " +
                   $"VALUES {fieldsListStr}; " +
                   $"CALL alter_db_schema('{dbName}');";
        }

        // Private

        private static string RemoveDatabaseListPrepareQuery(IEnumerable<string> namesList)
        {
            string namesListStr = FormatStringList(namesList);

            if (string.IsNullOrEmpty(namesListStr))
            {
                return string.Empty;
            }

            return "INSERT INTO `db_rem_list` " +
                   "(`name`) " +
                   $"VALUES {namesListStr};";
        }

        private static string AddDatabaseListPrepareQuery(IEnumerable<string> namesList)
        {
            string namesListStr = FormatStringList(namesList);

            if (string.IsNullOrEmpty(namesListStr))
            {
                return string.Empty;
            }

            return "INSERT INTO `db_add_list` " +
                   "(`name`) " +
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

    public class DbSchemaColumnNames
    {
        public const string TypeName = "Name";
        public const string BaseTypeName = "BaseTypeName";

        public const string FieldName = "Name";
        public const string FieldOwnerTypeName = "OwnerTypeName";
        public const string FieldTypeName = "TypeName";
        public const string FieldIsList = "IsList";
        public const string FieldBackRefName = "BackRefName";
        public const string FieldCompareOptions = "CompareOptions";
    }
}
