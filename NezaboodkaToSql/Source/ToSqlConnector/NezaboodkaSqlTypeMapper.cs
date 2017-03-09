using System;
using System.Collections.Generic;

namespace Nezaboodka.ToSqlConnector
{
    public static class NezaboodkaSqlTypeMapper
    {
        // Public

        public static SqlType SqlTypeByNezaboodkaTypeName(string nezaboodkaTypeName, int capacity = 0)
        {
            string typeName;
            bool nullable = false;
            if (nezaboodkaTypeName == "String")
            {
                if ((capacity > 0) && (capacity < 256))
                    typeName = $"VARCHAR({capacity})";
                else
                    typeName = "TEXT";
            }
            else
            {
                nullable = nezaboodkaTypeName.EndsWith("?");
                typeName = nezaboodkaTypeName.TrimEnd('?');
                if (SqlTypeByNezaboodkaTypeNameMap.ContainsKey(typeName))
                    typeName = SqlTypeByNezaboodkaTypeNameMap[typeName];
            }
            SqlType result = new SqlType(typeName, nullable);
            return result;
        }

        public static string NezaboodkaTypeNameBySqlType(SqlType sqlType)
        {
            string result = sqlType.Name;
            if (result.StartsWith("VARCHAR") || result.StartsWith("TEXT"))
                result = "String";  // TODO: extract VARCHAR length to Capacity
            else if (NezaboodkaTypeNameBySqlTypeMap.ContainsKey(result))
                result = NezaboodkaTypeNameBySqlTypeMap[result] + (sqlType.IsNullable ? "?" : string.Empty);
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

    public struct SqlType
    {
        public string Name;
        public bool IsNullable;

        public SqlType(string name, bool isNullable = false)
        {
            Name = name;
            IsNullable = isNullable;
        }
    }
}
