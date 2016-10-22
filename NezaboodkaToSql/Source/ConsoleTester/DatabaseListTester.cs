using System;
using System.Collections.Generic;
using Nezaboodka.ToSqlConnector;

namespace MySqlTester
{
    class DatabaseListTester
    {
        static void Main(string[] args)
        {
            GetDatabaseListTest();
        }

        public static void GetDatabaseListTest()
        {
            var client = new MySqlDatabaseClient("localhost", null, null);

            var databaseList = client.GetDatabaseList();
            printList(databaseList);
            Console.ReadLine();

            var alterList = new List<string>
            {
                "test12", "test2", "test3"
            };

            databaseList = client.AlterDatabaseList(alterList, null);
            printList(databaseList);
            Console.ReadLine();

            databaseList = client.AlterDatabaseList(null, alterList);
            printList(databaseList);
            Console.ReadLine();

            //client.AlterDatabaseList(new List<string>() { "test1" }, null);
            //client.AlterDatabaseList(null, new List<string>() { "test1" });
        }

        private static void printList(IList<string> list)
        {
            Console.WriteLine();
            foreach (string s in list)
            {
                Console.WriteLine(s);
            }
        }
    }
}
