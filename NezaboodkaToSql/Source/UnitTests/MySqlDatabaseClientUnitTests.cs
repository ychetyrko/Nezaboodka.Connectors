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

            var alterList = DatabaseNamesGenerator.RandomDatabaseNames(3, 10, "nz_");
            var databaseNames = client.GetDatabaseList();
            foreach (string name in alterList)
            {
                databaseNames.Add(name);
            }

            HashSet<string> expectedResult = new HashSet<string>(databaseNames);
            var actualResult = client.AlterDatabaseList(alterList, null);

            try
            {
                Assert.IsTrue(expectedResult.SetEquals(actualResult));
            }
            finally
            {
                client.AlterDatabaseList(null, alterList);  // remove added databases
            }
        }
    }
}
