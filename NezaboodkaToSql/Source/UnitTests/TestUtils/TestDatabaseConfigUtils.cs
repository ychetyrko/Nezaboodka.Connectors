using System.Globalization;

namespace Nezaboodka.MySqlClient.UnitTests.TestUtils
{
    public static class TestDatabaseConfigUtils
    {
        // Public 

        public static bool AreEqualDbConfigurations(DatabaseConfiguration config1, DatabaseConfiguration config2)
        {
            var config1Ndef = config1.ToNdefText();
            var config2Ndef = config2.ToNdefText();

            return config1Ndef.Equals(config2Ndef);
        }

        public static DatabaseSchema GetSingleClassDbSchema()
        {
            var typeDef = GetTestUserType();

            var result = new DatabaseSchema();
            result.TypeDefinitions.Add(typeDef);

            return result;
        }

        // Internal

        private static TypeDefinition GetTestUserType()
        {
            var result = new TypeDefinition()
            {
                TypeName = "User",
                BaseTypeName = string.Empty
            };

            result.FieldDefinitions.Add(CreateFieldDefinition("Login", "VARCHAR(60)", CompareOptions.IgnoreCase));
            result.FieldDefinitions.Add(CreateFieldDefinition("Email", "VARCHAR(255)", CompareOptions.IgnoreCase));
            result.FieldDefinitions.Add(CreateFieldDefinition("Age", "INT UNSIGNED"));

            return result;
        }

        private static FieldDefinition CreateFieldDefinition(string name, string fieldTypeName,
            CompareOptions compare = CompareOptions.None, string backReferenceFieldName = "", bool isList = false)
        {
            return new FieldDefinition
            {
                FieldName = name,
                FieldTypeName = fieldTypeName,
                BackReferenceFieldName = backReferenceFieldName,
                IsList = isList,
                CompareOptions = compare
            };
        }

    }
}
