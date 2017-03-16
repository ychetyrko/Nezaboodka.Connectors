using System.Text;

namespace Nezaboodka.ToSqlConnector
{
    static class QueryFormatter
    {
        // Public

        public static string GetAddTypeString(TypeDefinition typeDefinition)
        {
            string currentTypeName = typeDefinition.TypeName;
            string tableName = GetLowerName(currentTypeName);
            return $"'{currentTypeName}', '{tableName}', '{typeDefinition.BaseTypeName}'";
        }

        public static string GetRemoveTypeString(TypeDefinition typeDefinition)
        {
            return $"'{typeDefinition.TypeName}'";
        }

        public static string GetAddFieldString(string ownerTypeName, FieldDefinition fieldDefinition)
        {
            string columnName = GetLowerName(fieldDefinition.FieldName);
            string fieldTypeName = fieldDefinition.FieldTypeName;
            var fieldInfo = NezaboodkaSqlTypeMapper.SqlTypeByNezaboodkaTypeName(fieldTypeName);
            string result =
                $"'{fieldDefinition.FieldName}', '{columnName}', '{ownerTypeName}', '{fieldInfo.Name}', {fieldInfo.IsNullable.ToString().ToUpper()}, {fieldDefinition.IsList.ToString().ToUpper()}, '{fieldDefinition.CompareOptions:g}', '{fieldDefinition.BackReferenceFieldName}'";
            return result;
        }

        public static string GetRemoveFieldString(string ownerTypeName, FieldDefinition fieldDefinition)
        {
            return $"'{ownerTypeName}', '{fieldDefinition.FieldName}'";
        }

        // Internal

        private static string GetLowerName(string typeName)
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
