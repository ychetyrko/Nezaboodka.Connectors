using System;

namespace Nezaboodka
{
    public class FileObject : DbObject
    {
        public string FileName;
        public long FileLength;
        public long RewritingRevision; // ревизия, увеличиваемая на 1 при каждой полной перезаписи файла (Save).
        public long AppendingRevision; // ревизия, увеличиваемая на 1 при каждом расширении файла (Append).
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
}
