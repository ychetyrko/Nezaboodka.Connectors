using System.Collections.Generic;

namespace Nezaboodka.ToSqlConnector
{
    public static class NezaboodkaSqlTypeMapper
    {
        // Public

        public static string SqlTypeNameByNezaboodkaTypeName(string nezaboodkaTypeName, int capacity = 0)
        {
            string result;
            if (nezaboodkaTypeName == "String")
            {
                if ((capacity > 0) && (capacity < 256))
                    result = $"VARCHAR({capacity})";
                else
                    result = "TEXT";
            }
            else
            {
                bool nullable = nezaboodkaTypeName.EndsWith("?");
                result = nezaboodkaTypeName.TrimEnd('?');
                if (SqlTypeByNezaboodkaTypeNameMap.ContainsKey(result))
                    result = SqlTypeByNezaboodkaTypeNameMap[result] + (nullable ? "?" : string.Empty);
            }
            return result;
        }

        public static string NezaboodkaTypeNameBySqlTypeName(string sqlTypeName)
        {
            bool nullable = sqlTypeName.EndsWith("?");
            string result = sqlTypeName.TrimEnd('?');
            if (result.StartsWith("VARCHAR") || result.StartsWith("TEXT"))
                result = "String";
            else if (NezaboodkaTypeNameBySqlTypeMap.ContainsKey(result))
                result = NezaboodkaTypeNameBySqlTypeMap[result] + (nullable ? "?" : string.Empty);
            return result;
        }

        // Internal

        private static readonly Dictionary<string, string> SqlTypeByNezaboodkaTypeNameMap = new Dictionary<string, string>()
        {
            {"Boolean", "BOOLEAN"},
            {"SByte", "TINYINT"},
            {"Byte", "TINYINT UNSIGNED"},
            {"Int16", "SMALLINT"},
            {"UInt16", "SMALLINT UNSIGNED"},
            {"Int32", "INT"},
            {"UInt32", "INT UNSIGNED"},
            {"Int64", "BIGINT"},
            {"UInt64", "BIGINT UNSIGNED"},
            {"Float", "FLOAT"},
            {"Double", "DOUBLE"},
            {"Decimal", "DECIMAL(58, 29)"},
            {"Char", "CHAR(1)"},
            {"DateTime", "DATETIMEOFFSET"}
        };

        private static readonly Dictionary<string, string> NezaboodkaTypeNameBySqlTypeMap = new Dictionary<string, string>()
        {
            {"BOOLEAN", "Boolean"},
            {"TINYINT", "SByte"},
            {"TINYINT UNSIGNED", "Byte"},
            {"SMALLINT", "Int16"},
            {"SMALLINT UNSIGNED", "UInt16"},
            {"INT", "Int32"},
            {"INT UNSIGNED", "UInt32"},
            {"BIGINT", "Int64"},
            {"BIGINT UNSIGNED", "UInt64"},
            {"FLOAT", "Float"},
            {"DOUBLE", "Double"},
            {"DECIMAL(58, 29)", "Decimal"},
            {"CHAR(1)", "Char"},
            {"DATETIMEOFFSET", "DateTime"},
        };
    }
}
