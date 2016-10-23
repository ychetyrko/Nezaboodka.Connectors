using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nezaboodka.MySqlClient.UnitTests.TestUtils;
using Nezaboodka.ToSqlConnector;

namespace Nezaboodka.MySqlClient.UnitTests
{
    [TestClass]
    public class MySqlDatabaseClientUnitTests
    {
        
        [TestMethod]
        public void AlterDatabaseListTest()
        {
            var client = new MySqlDatabaseClient("localhost", null, null);

            var alterList = DatabaseNamesGenerator.GetRandomDatabaseNamesList(3, 15, "nz_");
            var databaseNames = client.GetDatabaseList();

            HashSet<string> removeExpectedResult = new HashSet<string>(databaseNames);
            var addExpectedResult = new HashSet<string>(removeExpectedResult);
            foreach (string name in alterList)
            {
                addExpectedResult.Add(name);
            }

            try
            {
                var actualResult = client.AlterDatabaseList(alterList, null);
                Assert.IsTrue(addExpectedResult.SetEquals(actualResult));

                actualResult = client.AlterDatabaseList(null, alterList);
                Assert.IsTrue(removeExpectedResult.SetEquals(actualResult));
            }
            finally
            {
                // TODO: clear environment here
            }
        }

        [TestMethod]
        public void GetDatabaseAccessModeNewDatabaseTest()
        {
            DatabaseAccessMode expectedResult = DatabaseAccessMode.ReadWrite;

            var adminClient = new MySqlDatabaseClient("localhost", null, null);
            var dbName = DatabaseNamesGenerator.GetRandomDatabaseName(15, "nz_");
            var dbList = new List<string>() { dbName };
            adminClient.AlterDatabaseList(dbList, null);

            var client = new MySqlDatabaseClient("localhost", dbName, null);
            DatabaseAccessMode actualResult = client.GetDatabaseAccessMode();
            try
            {
                Assert.AreEqual(expectedResult, actualResult);
            }
            finally
            {
                adminClient.AlterDatabaseList(null, dbList);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(NezaboodkaAvailabilityException))]
        public void GetDatabaseAccessModeDatabaseNotExistsTest()
        {
            var dbName = DatabaseNamesGenerator.GetRandomDatabaseName(15, "nz_");
            var client = new MySqlDatabaseClient("localhost", dbName, null);
            client.GetDatabaseAccessMode();
        }
    }
}
