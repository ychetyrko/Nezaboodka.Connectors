using System.Globalization;

namespace Nezaboodka.MySqlClient.UnitTests.TestUtils
{
    public static class DatabaseConfigTestUtils
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

        public static DatabaseSchema GetMultipleClassDbSchema()
        {
            var userTypeDef = GetTestUserType();
            var groupTypeDef = GetTestGroupType();
            var adminTypeDef = GetTestAdminType();

            userTypeDef.FieldDefinitions.Add(CreateFieldDefinition("Group", "Group", CompareOptions.None, "Participants"));
            groupTypeDef.FieldDefinitions.Add(CreateFieldDefinition("Participants", "User", CompareOptions.None, "Group", true));

            groupTypeDef.FieldDefinitions.Add(CreateFieldDefinition("ManagedGroup", "Admin", CompareOptions.None, "Admin"));
            adminTypeDef.FieldDefinitions.Add(CreateFieldDefinition("Admin", "Group", CompareOptions.None, "ManagedGroup"));

            var result = new DatabaseSchema();

            result.TypeDefinitions.Add(userTypeDef);
            result.TypeDefinitions.Add(groupTypeDef);
            result.TypeDefinitions.Add(adminTypeDef);

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

            result.FieldDefinitions.Add(CreateFieldDefinition("Login", "String", CompareOptions.IgnoreCase));
            result.FieldDefinitions.Add(CreateFieldDefinition("Email", "String", CompareOptions.IgnoreCase));
            result.FieldDefinitions.Add(CreateFieldDefinition("Age", "UInt32"));

            return result;
        }

        private static TypeDefinition GetTestAdminType()
        {
            var result = new TypeDefinition()
            {
                TypeName = "Admin",
                BaseTypeName = "User"
            };

            result.FieldDefinitions.Add(CreateFieldDefinition("ManagedGroup", "Group", CompareOptions.None, "Admin"));

            return result;
        }

        private static TypeDefinition GetTestGroupType()
        {
            var result = new TypeDefinition()
            {
                TypeName = "Group",
                BaseTypeName = string.Empty
            };

            result.FieldDefinitions.Add(CreateFieldDefinition("Title", "String", CompareOptions.IgnoreCase));
            result.FieldDefinitions.Add(CreateFieldDefinition("Rating", "Byte"));
            result.FieldDefinitions.Add(CreateFieldDefinition("AccessMask", "UInt32"));

            return result;
        }

        private static FieldDefinition CreateFieldDefinition(string name, string fieldTypeName,
            CompareOptions compare = CompareOptions.None, string backReferenceFieldName = null, bool isList = false)
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
