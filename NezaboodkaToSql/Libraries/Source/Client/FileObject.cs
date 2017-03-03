using System;
using System.Globalization;

namespace Nezaboodka
{
    public class FileObject : DbObject
    {
        public string FileName;
        public long FileLength;
        public long OverwriteCount; // ревизия, увеличиваемая на 1 при каждой полной перезаписи файла.
        public long AppendCount; // ревизия, увеличиваемая на 1 при каждом расширении файла.
        public DateTimeOffset CreationTimeUtc;
        public DateTimeOffset LastWriteTimeUtc;
        public string HashValue;
        public string ContentType;
        public FileContent FileContent;
    }

    public sealed class FileContent
    {
        public FileRange FileRange;
        public object FileData { get; set; }
    }

    public struct FileRange
    {
        private static readonly char[] DelimiterBetweenPositionAndLength = new char[] { '+' };

        public long Position; // Запись: 0 - Save, не 0 - Append. Чтение: отрицательные значения превращаются в 0.
        public long Length; // 0 - IsNull

        public FileRange(long position, long length)
        {
            Position = position;
            Length = length;
        }

        public bool IsNull()
        {
            return Length == 0;
        }

        public override string ToString()
        {
            string result = null;
            if (!IsNull())
            {
                if (Position >= 0)
                    result = string.Format("{0:X}{1}{2:X}", Position, DelimiterBetweenPositionAndLength[0], Length);
                else
                    result = string.Format("{0}{1:X}", DelimiterBetweenPositionAndLength[0], Length);
            }
            return result;
        }

        public static FileRange Parse(string value)
        {
            var result = new FileRange();
            if (!string.IsNullOrEmpty(value))
            {
                string[] t = value.Split(DelimiterBetweenPositionAndLength, 2);
                if (t.Length == 2)
                {
                    string p = t[0].Trim();
                    if (!string.IsNullOrEmpty(p))
                        result.Position = long.Parse(p, NumberStyles.AllowHexSpecifier);
                    result.Length = long.Parse(t[1].Trim(), NumberStyles.AllowHexSpecifier);
                }
                else
                    throw new NezaboodkaException(string.Format("invalid file range format: '{0}'", value));
            }
            return result;
        }
    }
}
