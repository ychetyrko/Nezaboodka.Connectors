using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nezaboodka.ToSqlConnector;

namespace Nezaboodka.MySqlClient.UnitTests
{
    /// <summary>
    /// Summary description for DbQueryBuilderTests
    /// </summary>
    [TestClass]
    public class DbQueryBuilderTests
    {
        [TestMethod]
        public void AlterDatabaseConfigurationQueryTest()
        {
            string expectedResult = "INSERT INTO `db_nz_test`.`type` " +
                                    "(`name`, `table_name`, `base_type_name`) " +
                                    "VALUES ('User', '_user', ''); " +
                                    "INSERT INTO `db_nz_test`.`fields` " +
                                    "(`name`, `col_name`, `owner_type_name`, `type_name`, `compare_options`, `is_list`, `back_ref_name`) " +
                                    "VALUES ('Name', '_name', 'User', 'string', 'StringSort', FALSE, ''),('Age', '_age', 'User', 'int', 'Ordinal', FALSE, ''); " +
                                    "CALL alter_db_schema('db_nz_test');";

            DatabaseSchema schema = new DatabaseSchema();

            FieldDefinition nameFieldDef = new FieldDefinition()
            {
                FieldName = "Name",
                FieldTypeName = "string",
                IsList = false,
                CompareOptions = CompareOptions.StringSort,
                BackReferenceFieldName = ""
            };

            FieldDefinition ageFieldDef = new FieldDefinition()
            {
                FieldName = "Age",
                FieldTypeName = "int",
                IsList = false,
                CompareOptions = CompareOptions.Ordinal,
                BackReferenceFieldName = ""
            };

            TypeDefinition tdef = new TypeDefinition()
            {
                TypeName = "User",
                BaseTypeName = "",
                FieldDefinitions = new List<FieldDefinition>() { nameFieldDef, ageFieldDef }
            };
            schema.TypeDefinitions.Add(tdef);

            string actualResult = DbQueryBuilder.AlterDatabaseSchemaQuery("db_nz_test", schema.TypeDefinitions);

            Assert.AreEqual(expectedResult, actualResult);
        }
    }
}
