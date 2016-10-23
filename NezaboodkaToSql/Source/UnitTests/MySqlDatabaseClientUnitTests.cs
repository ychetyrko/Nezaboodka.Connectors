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

            var alterList = DatabaseNamesGenerator.RandomDatabaseNames(3, 15, "nz_");
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
    }
}
