namespace Nezaboodka.ToSqlConnector
{
    public static class SqlAuthData  // TODO: read credentials from Protected Configuration
    {
        public static string UserId => "nz_admin";
        public static string Pass => "nezaboodka";

        public static uint DefaultPort => 3306;
    }

    public static class AdminDatabaseConst
    {
        public const string AdminDbName = "nz_admin_db";

        public const string DatabasesList = "db_list";
        public const string RemoveDbList = "db_rem_list";
        public const string AddDbList = "db_add_list";

        public const string NameField = "name";
        public const string AccessField = "access";
    }

    public static class SchemaTableConst
    {
        public const string TypeTableName = "type";
        public const string FieldTableName = "field";
    }

    public static class SchemaFieldConst
    {
        public const string SysId = "sys_id";
        public const string RealTypeId = "real_type_id";

        public const string TypeName = "name";
        public const string BaseTypeName = "base_type_name";
        public const string TableName = "table_name";

        public const string FieldName = "name";
        public const string FieldColumnName = "col_name";
        public const string FieldOwnerTypeName = "owner_type_name";
        public const string FieldTypeName = "type_name";
        public const string FieldIsList = "is_list";
        public const string FieldCompareOptions = "compare_options";
        public const string FieldBackRefName = "back_ref_name";
    }

}
