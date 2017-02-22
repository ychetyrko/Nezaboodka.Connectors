using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public partial class FileContentHandler
    {
        public const int ReadingToFile = 0;
        public const int ReadingToStream = 1;
        public const int ReadingToBuffer = 2;

        // Public

        public int ReadingDestination { get; set; }
        public string LocalFilesFolder { get; set; }

        public FileContentHandler()
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            LocalFilesFolder = Path.Combine(Path.GetDirectoryName(assembly.Location), "NezaboodkaCachedFiles");
            Directory.CreateDirectory(LocalFilesFolder);
        }

        public virtual void WriteFiles(NdefWriter ndefWriter, SaveObjectsRequest request,
            Dictionary<object, long> objectNumberByFileObject)
        {
            ndefWriter.WriteDataSetStart(null, true, false);
            foreach (SaveQuery query in request.Queries)
                for (int i = 0; i < query.ForEachIn.Count; i++)
                {
                    FileObject fileObject = query.ForEachIn[i] as FileObject;
                    if (fileObject != null)
                    {
                        ndefWriter.WriteObjectStart(null, objectNumberByFileObject[fileObject].ToString(),
                            fileObject.Key.ToString(), false);
                        using (Stream stream = ndefWriter.WriteBinaryData(fileObject.FileContent.FileRange.Length))
                            WriteFile(fileObject, stream);
                        ndefWriter.WriteObjectEnd(false);
                    }
                }
            ndefWriter.WriteDataSetEnd(false);
        }

        public virtual void ReadFiles(NdefDeserializer deserializer)
        {
            if (deserializer.MoveToNextDataSet() && !deserializer.CurrentDataSet.IsStartOfDataSet)
                while (deserializer.MoveToNextObject())
                {
                    FileObject fileObject = (FileObject)deserializer.CurrentObject;
                    if (deserializer.CurrentStream != null)
                        ReadFile(fileObject, deserializer.CurrentStream);
                }
        }

        // WriteFile

        protected virtual void WriteFile(FileObject fileObject, Stream destination)
        {
            object source = fileObject.FileContent.FileData;
            if (source is string)
                WriteFileFromFile(fileObject, source as string, destination);
            else if (source is Stream)
                WriteFileFromStream(fileObject, source as Stream, destination);
            else if (source.GetType() == typeof(byte[]))
                WriteFileFromBuffer(fileObject, source as byte[], destination);
            else
                throw new NezaboodkaException(string.Format(
                    "type {0} in FileObject.FileObjectMetadata.FileData is not supported, file object {1} (file name: {2})",
                    source.GetType(), fileObject.Key, fileObject.FileName));
        }

        protected virtual void WriteFileFromFile(FileObject fileObject, string source, Stream destination)
        {
            using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read))
            {
                if (fileObject.FileContent.FileRange.Length <= sourceStream.Length)
                    WriteFileFromStream(fileObject, sourceStream, destination);
                else
                    throw new NezaboodkaException(string.Format(
                        "actual file length {0} is less than the specified length {1} for file object {2} with file name {3}, see local file {4}",
                        sourceStream.Length, fileObject.FileContent.FileRange.Length, fileObject, fileObject.FileName, source));
            }
        }

        protected virtual void WriteFileFromStream(FileObject fileObject, Stream source, Stream destination)
        {
            var buffer = new byte[Const.DefaultFileBlockSize];
            long length = fileObject.FileContent.FileRange.Length;
            int received = 1; // Начальное значение больше нуля, чтобы зайти в цикл. Оно будет сразу же изменено внутри цикла.
            while (length > 0 && received > 0)
            {
                received = source.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
                if (received > 0)
                {
                    destination.Write(buffer, 0, received);
                    length -= received;
                }
            }
            if (length > 0)
                throw new NezaboodkaException(string.Format(
                    "actual stream length {0} is less than the specified length {1} for file object {2} with file name {3}",
                    fileObject.FileContent.FileRange.Length - length, fileObject.FileContent.FileRange.Length,
                    fileObject, fileObject.FileName));
        }

        protected virtual void WriteFileFromBuffer(FileObject fileObject, byte[] source, Stream destination)
        {
            if (fileObject.FileContent.FileRange.Length <= source.Length)
                destination.Write(source, 0, (int)fileObject.FileContent.FileRange.Length);
            else
                throw new NezaboodkaException(string.Format(
                    "actual buffer length {0} is less than the specified length {1} for file object {2} with file name {3}",
                    source.Length, fileObject.FileContent.FileRange.Length, fileObject, fileObject.FileName));
        }

        // ReadFile

        protected virtual void ReadFile(FileObject fileObject, Stream source)
        {
            switch (ReadingDestination)
            {
                case ReadingToFile:
                    string filePath = Path.Combine(LocalFilesFolder, fileObject.Key.ToString());
                    fileObject.FileContent.FileData = filePath;
                    ReadFileToFile(fileObject, source, filePath);
                    break;
                case ReadingToStream:
                    var destination = new MemoryStream((int)fileObject.FileContent.FileRange.Length);
                    fileObject.FileContent.FileData = destination;
                    ReadFileToStream(fileObject, source, destination);
                    break;
                case ReadingToBuffer:
                    var buffer = new byte[(int)fileObject.FileContent.FileRange.Length];
                    fileObject.FileContent.FileData = buffer;
                    ReadFileToBuffer(fileObject, source, buffer);
                    break;
                default:
                    throw new NezaboodkaException(string.Format("unknown value {0} in FileContentHandler.ReadingDestination",
                        ReadingDestination));
            }
        }

        protected virtual void ReadFileToFile(FileObject fileObject, Stream source, string destination)
        {
            using (var destinationStream = new FileStream(destination, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                ReadFileToStream(fileObject, source, destinationStream);
        }

        protected virtual void ReadFileToStream(FileObject fileObject, Stream source, Stream destination)
        {
            var buffer = new byte[Const.DefaultFileBlockSize];
            long length = fileObject.FileContent.FileRange.Length;
            int received = 1; // Начальное значение больше нуля, чтобы зайти в цикл. Оно будет сразу же изменено внутри цикла.
            while (length > 0 && received > 0)
            {
                received = source.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
                destination.Write(buffer, 0, received);
                length -= received;
            }
            if (length > 0)
                throw new NezaboodkaException(string.Format(
                    "actual received data length {0} is less than the specified length {1} for file object {2} with file name {3}",
                    fileObject.FileContent.FileRange.Length - length, fileObject.FileContent.FileRange.Length,
                    fileObject, fileObject.FileName));
        }

        protected virtual void ReadFileToBuffer(FileObject fileObject, Stream source, byte[] destination)
        {
            int offset = 0;
            int length = destination.Length;
            int received = 1; // Начальное значение больше нуля, чтобы зайти в цикл. Оно будет сразу же изменено внутри цикла.
            while (length > 0 && received > 0)
            {
                received = source.Read(destination, offset, length);
                offset += received;
                length -= received;
            }
            if (length > 0)
                throw new NezaboodkaException(string.Format(
                    "actual received data length {0} is less than the specified length {1} for file object {2} with file name {3}",
                    fileObject.FileContent.FileRange.Length - length, fileObject.FileContent.FileRange.Length,
                    fileObject, fileObject.FileName));
        }
    }
}
