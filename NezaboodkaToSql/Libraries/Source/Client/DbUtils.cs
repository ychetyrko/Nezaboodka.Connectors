using System;
using System.Collections.Generic;
using System.Linq;

namespace Nezaboodka
{
    public static class DbUtils
    {
        public static List<string> SplitWithSeparatorsIncluded(this string line, char[] separators)
        {
            List<string> result = new List<string>();
            int i = 0;
            while (i < line.Length)
            {
                int j = i;
                if (separators.Contains(line[j]))
                    j++;
                while (j < line.Length && !separators.Contains(line[j]))
                    j++;
                if (j > i && j <= line.Length)
                    result.Add(line.Substring(i, j - i).Trim());
                i = j;
            }
            return result;
        }

        public static bool ContainsWhitespace(this string str)
        {
            return str.Any((char c) => char.IsWhiteSpace(c));
        }

        public static string ToBase32String(long value)
        {
            var buffer = new char[gMaxNumberOfDigitsForInt64];
            var pos = gMaxNumberOfDigitsForInt64 - 1;
            do
            {
                buffer[pos] = gDigits[value & (gBaseNumber - 1)];
                value = value >> gBitsPerDigit;
                pos -= 1;
            }
            while (value != 0 && pos >= 0);
            return new string(buffer, pos + 1, gMaxNumberOfDigitsForInt64 - pos - 1);
        }

        public static long FromBase32String(string s)
        {
            return FromBase32String(s, 0, s.Length);
        }

        public static long FromBase32String(string s, int startIndex)
        {
            return FromBase32String(s, startIndex, s.Length - startIndex);
        }

        public static long FromBase32String(string s, int startIndex, int length)
        {
            long result = 0;
            if (length < 0)
                length = 0;
            int i = startIndex;
            int end = Math.Min(startIndex + length, s.Length);
            while (i < end)
            {
                var digit = gDigitValues[s[i]];
                if (digit >= gBaseNumber)
                    throw new FormatException(string.Format("unsupported symbol '{0}'", s[i]));
                result = (result << gBitsPerDigit) | digit;
                i++;
            }
            return result;
        }

        private static byte[] InvertedArray(char[] digits)
        {
            var result = new byte[char.MaxValue];
            for (var i = 0; i < result.Length; ++i)
                result[i] = 255;
            for (byte i = 0; i < digits.Length; ++i)
                result[digits[i]] = i;
            return result;
        }

        // Constants

        //private static string[] gIdMarkers = new string[] { string.Empty, "~", "?", "=" };
        private const byte gBitsPerDigit = 5;
        private const byte gBaseNumber = 1 << gBitsPerDigit;
        private const byte gMaxNumberOfDigitsForInt64 = 64 / gBitsPerDigit + 1;
        private static char[] gDigits = new char[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K',
            'M', 'N', 'P', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'
        };
        private static byte[] gDigitValues = InvertedArray(gDigits);
    }
}
