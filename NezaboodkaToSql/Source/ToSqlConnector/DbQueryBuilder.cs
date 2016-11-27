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

        // !! NO MERGE proveded
        // TODO: merge existing schema with new
        public static string AlterDatabaseSchemaQuery(string dbName, IEnumerable<TypeDefinition> typeDefinitionsList)
        {
            List<string> typesList = new List<string>();
            List<string> fieldsList = new List<string>();

            foreach (var typeDefinition in typeDefinitionsList)
            {
                string typeName = typeDefinition.TypeName;
                string tableName = GenerateLowerName(typeName);
                string typeRec = $"'{typeName}', '{tableName}', '{typeDefinition.BaseTypeName}'";

                typesList.Add(typeRec);

                foreach (var fieldDefinition in typeDefinition.FieldDefinitions)
                {
                    string columnName = GenerateLowerName(fieldDefinition.FieldName);
                    string fieldRec = $"'{fieldDefinition.FieldName}', '{columnName}', '{typeName}', '{fieldDefinition.FieldTypeName}', '{fieldDefinition.CompareOptions}', {fieldDefinition.IsList.ToString().ToUpper()}, '{fieldDefinition.BackReferenceFieldName}'";

                    fieldsList.Add(fieldRec);
                }
            }

            string typesListStr = FormatValuesList(typesList);
            string fieldsListStr = FormatValuesList(fieldsList);

            return $"INSERT INTO `{dbName}`.`type` " +
                   "(`name`, `table_name`, `base_type_name`) " +
                   $"VALUES {typesListStr}; " +
                   $"INSERT INTO `{dbName}`.`fields` " +
                   "(`name`, `col_name`, `owner_type_name`, `type_name`, `compare_options`, `is_list`, `back_ref_name`) " +
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
            return string.Join(",", values.Select(s => $"({s})"));
        }

        private static string FormatStringList(IEnumerable<string> strList)
        {
            return string.Join(",", strList.Select(s => $"('{s}')"));
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
