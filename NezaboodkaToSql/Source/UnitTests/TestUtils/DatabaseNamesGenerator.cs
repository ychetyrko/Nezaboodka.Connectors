using System;
using System.Collections.Generic;
using System.Linq;

namespace Nezaboodka.MySqlClient.UnitTests.TestUtils
{
    static class DatabaseNamesGenerator
    {
        private static readonly Random random = new Random();

        // Public

        /// <summary>
        /// Get random string of <c>length</c> with <c>prefix</c> containing only <c>allowedChars</c>.
        /// </summary>
        /// <param name="length">Length of name</param>
        /// <param name="prefix">Name prefix</param>
        /// <param name="allowedChars">Characters used for generating random string</param>
        /// <returns></returns>
        public static string GetRandomDatabaseName(int length, string prefix, string allowedChars)
        {
            // as MySql gives you database names in lowercase
            return prefix + GetRandomString(length-prefix.Length, allowedChars).ToLower();
        }

        /// <summary>
        /// Get list of <c>count</c> random strings, each of which has <c>nameLength</c> length, with <c>prefix</c> and contains only <c>allowedChars</c>.
        /// </summary>
        /// <param name="count">List size</param>
        /// <param name="nameLength">Length of each name</param>
        /// <param name="prefix">Prefix to add to each name</param>
        /// <param name="allowedChars">Characters used for generating random string</param>
        /// <returns></returns>
        public static List<string> RandomDatabaseNames(int count, int nameLength, string prefix, string allowedChars = Constants.DefaultAlphabet)
        {
            var result = new List<string>();
            for (int i = 0; i < count; ++i)
            {
                result.Add(GetRandomDatabaseName(nameLength, prefix, allowedChars));
            }
            return result;
        }

        // Internal

        private static string GetRandomString(int length, string alphabet)
        {
            return new string(Enumerable.Repeat(alphabet, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Constants

        public static class Constants
        {
            public const string DefaultAlphabet = "abcdefghijklmnopqrstuvwxyz";
        }
    }
}
