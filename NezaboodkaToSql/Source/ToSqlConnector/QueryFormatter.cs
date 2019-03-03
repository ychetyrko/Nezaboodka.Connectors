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
                $"'{fieldDefinition.FieldName}', '{columnName}', '{ownerTypeName}', '{fieldInfo.Name}', {fieldInfo.IsNullable.ToString().ToUpper()}, {fieldDefinition.IsList.ToString().ToUpper()}, '{fieldDefinition.CompareOptions:g}'";
            return result;
        }

        public static string GetRemoveFieldString(string ownerTypeName, string fieldName)
        {
            return $"'{ownerTypeName}', '{fieldName}'";
        }

        public static string GetAddBackRefString(string ownerTypeName, string fieldName, string newBackRefName)
        {
            return GetUpdateBackRefString(ownerTypeName, fieldName, newBackRefName);
        }

        public static string GetRemoveBackRefString(string ownerTypeName, string fieldName)
        {
            return GetUpdateBackRefString(ownerTypeName, fieldName, null);
        }

        private static string GetUpdateBackRefString(string ownerTypeName, string fieldName, string newBackRefName)
        {
            return $"'{ownerTypeName}', '{fieldName}', '{newBackRefName}'";
        }

        // Internal

        private static string GetLowerName(string typeName)
        {
            var result = new StringBuilder();

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
