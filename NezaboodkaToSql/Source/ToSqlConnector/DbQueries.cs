using System.Collections.Generic;
using System.Linq;

namespace Nezaboodka.ToSqlConnector
{
    internal static class DbQueries
    {
        public static string GetDatabaseListQuery =>
            "SELECT `name`" +
            "FROM `db_list`;";

        public static string GetDatabaseAccessModeQuery(string dbName)
        {
            return "SELECT `access` " +
                   "FROM `db_list` " +
                   $"WHERE `name` = '{dbName}';";
        }

        public static string RemoveDatabaseListPrepareQuery(IEnumerable<string> namesList)
        {
            string namesListStr = FormatValuesList(namesList);
            return "INSERT INTO `db_rem_list` " +
                   "(`name`) " +
                   $"VALUES {namesListStr};";
        }

        public static string AddDatabaseListPrepareQuery(IEnumerable<string> namesList)
        {
            string namesListStr = FormatValuesList(namesList);
            return "INSERT INTO `db_add_list` " +
                   "(`name`) " +
                   $"VALUES {namesListStr};";
        } 

        public static string AlterDatabaseListQuery =>
            "CALL alter_database_list();";
        
        private static string FormatValuesList(IEnumerable<string> values)
        {
            return string.Join(",", values.Select(s => $"('{s}')"));
        }
    }
}
