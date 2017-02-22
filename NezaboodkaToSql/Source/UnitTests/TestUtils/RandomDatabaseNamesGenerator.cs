using System;
using System.Collections.Generic;

namespace Nezaboodka.MySqlClient.UnitTests.TestUtils
{
    static class RandomDatabaseNamesGenerator
    {
        private static readonly Random random = new Random();

        // Public

        /// <summary>
        /// Get random string of <c>length</c> with <c>prefix</c> containing only <c>allowedChars</c>.
        /// </summary>
        /// <param name="length">Length of name</param>
        /// <param name="prefix">Name prefix</param>
        /// <param name="allowedChars">Characters used for generating random string</param>
        /// <returns>Random database name</returns>
        public static string GetRandomDatabaseName(int length, string prefix, string allowedChars = Constants.DefaultAlphabet)
        {
            // as MySql gives you database names in lowercase
            return prefix + GetRandomString(length - prefix.Length, allowedChars).ToLower();
        }

        /// <summary>
        /// Get list of <c>count</c> random strings, each of which has <c>nameLength</c> length, with <c>prefix</c> and contains only <c>allowedChars</c>.
        /// </summary>
        /// <param name="count">List size</param>
        /// <param name="nameLength">Length of each name</param>
        /// <param name="prefix">Prefix to add to each name</param>
        /// <param name="allowedChars">Characters used for generating random string</param>
        /// <returns>List of random database names</returns>
        public static List<string> GetRandomDatabaseNamesList(int count, int nameLength, string prefix, string allowedChars = Constants.DefaultAlphabet)
        {
            var result = new List<string>();
            for (int i = 0; i < count; ++i)
            {
                string nextName = GetRandomDatabaseName(nameLength, prefix, allowedChars);
                result.Add(nextName);
            }
            return result;
        }

        // Internal

        private static string GetRandomString(int length, string alphabet)
        {
            char[] result = new char[length];
            for (int i = 0; i < length; ++i)
            {
                int r = random.Next(alphabet.Length);
                result[i] = alphabet[r];
            }

            return new String(result);
        }

        // Constants

        public static class Constants
        {
            public const string DefaultAlphabet = "abcdefghijklmnopqrstuvwxyz";
        }
    }
}
