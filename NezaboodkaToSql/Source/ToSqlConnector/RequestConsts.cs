namespace Nezaboodka.ToSqlConnector
{
    internal static class RequestConsts
    {
        public static string GetDatabaseListQuery =>
            "SELECT `name`" +
            "FROM `db_list`;";

        public static string GetDatabaseAccessModeQuery =>
            "SELECT `access`" +
            "FROM `db_list`" +
            "WHERE `name` = '{0}';";

        public static string RemoveDatabaseListPrepareQuery =>
            "INSERT INTO `db_rem_list`" +
            "(`name`)" +
            "VALUES {0};";

        public static string AddDatabaseListPrepareQuery => 
            "INSERT INTO `db_add_list`" +
            "(`name`)" +
            "VALUES {0};";

        public static string AlterDatabaseListQuery =>
            "CALL alter_database_list();";

        public static string InsertTypesQuery =>
            "INSERT INTO `{0}`.`type`" +
            "(`name`, `table_name`, `base_type_name`)" +
            "VALUES {1};";

        public static string InsertFieldsQuery =>
            "INSERT INTO `{0}`.`fields`" +
            "(`name`, `col_name`, `owner_type_name`, `type_name`, `back_ref_name`, `is_list`)" +
            "VALUES {1};";

        public static string UpdateDatabaseSchema =>
            "CALL alter_db_schema({0})";
    }
}
