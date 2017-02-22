using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public partial class FileContentHandler
    {
        // Asynchronous

        public virtual async Task WriteFilesAsync(NdefWriter ndefWriter, SaveObjectsRequest request,
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
                            await WriteFileAsync(fileObject, stream).ConfigureAwait(false);
                        ndefWriter.WriteObjectEnd(false);
                    }
                }
            ndefWriter.WriteDataSetEnd(false);
        }

        public virtual async Task ReadFilesAsync(NdefDeserializer deserializer)
        {
            if (deserializer.MoveToNextDataSet() && !deserializer.CurrentDataSet.IsStartOfDataSet)
                while (deserializer.MoveToNextObject())
                {
                    FileObject fileObject = (FileObject)deserializer.CurrentObject;
                    if (deserializer.CurrentStream != null)
                        await ReadFileAsync(fileObject, deserializer.CurrentStream).ConfigureAwait(false);
                }
        }

        // WriteFileAsync

        protected virtual async Task WriteFileAsync(FileObject fileObject, Stream destination)
        {
            object source = fileObject.FileContent.FileData;
            if (source is string)
                await WriteFileFromFileAsync(fileObject, source as string, destination).ConfigureAwait(false);
            else if (source is Stream)
                await WriteFileFromStreamAsync(fileObject, source as Stream, destination).ConfigureAwait(false);
            else if (source.GetType() == typeof(byte[]))
                await WriteFileFromBufferAsync(fileObject, source as byte[], destination).ConfigureAwait(false);
            else
                throw new NezaboodkaException(string.Format(
                    "type {0} in FileObject.FileObjectMetadata.FileData is not supported, file object {1} (file name: {2})",
                    source.GetType(), fileObject.Key, fileObject.FileName));
        }

        protected virtual async Task WriteFileFromFileAsync(FileObject fileObject, string source, Stream destination)
        {
            using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read))
            {
                if (fileObject.FileContent.FileRange.Length <= sourceStream.Length)
                    await WriteFileFromStreamAsync(fileObject, sourceStream, destination).ConfigureAwait(false);
                else
                    throw new NezaboodkaException(string.Format(
                        "actual file length {0} is less than the specified length {1} for file object {2} with file name {3}, see local file {4}",
                        sourceStream.Length, fileObject.FileContent.FileRange.Length, fileObject, fileObject.FileName, source));
            }
        }

        protected virtual async Task WriteFileFromStreamAsync(FileObject fileObject, Stream source, Stream destination)
        {
            var buffer = new byte[Const.DefaultFileBlockSize];
            long length = fileObject.FileContent.FileRange.Length;
            int received = 1; // Начальное значение больше нуля, чтобы зайти в цикл. Оно будет сразу же изменено внутри цикла.
            while (length > 0 && received > 0)
            {
                received = await source.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, length)).ConfigureAwait(false);
                if (received > 0)
                {
                    await destination.WriteAsync(buffer, 0, received).ConfigureAwait(false);
                    length -= received;
                }
            }
            if (length > 0)
                throw new NezaboodkaException(string.Format(
                    "actual stream length {0} is less than the specified length {1} for file object {2} with file name {3}",
                    fileObject.FileContent.FileRange.Length - length, fileObject.FileContent.FileRange.Length,
                    fileObject, fileObject.FileName));
        }

        protected virtual async Task WriteFileFromBufferAsync(FileObject fileObject, byte[] source, Stream destination)
        {
            if (fileObject.FileContent.FileRange.Length <= source.Length)
                await destination.WriteAsync(source, 0, (int)fileObject.FileContent.FileRange.Length).ConfigureAwait(false);
            else
                throw new NezaboodkaException(string.Format(
                    "actual buffer length {0} is less than the specified length {1} for file object {2} with file name {3}",
                    source.Length, fileObject.FileContent.FileRange.Length, fileObject, fileObject.FileName));
        }

        // ReadFileAsync

        protected virtual async Task ReadFileAsync(FileObject fileObject, Stream source)
        {
            switch (ReadingDestination)
            {
                case ReadingToFile:
                    string filePath = Path.Combine(LocalFilesFolder, fileObject.Key.ToString());
                    fileObject.FileContent.FileData = filePath;
                    await ReadFileToFileAsync(fileObject, source, filePath).ConfigureAwait(false);
                    break;
                case ReadingToStream:
                    var destination = new MemoryStream((int)fileObject.FileContent.FileRange.Length);
                    fileObject.FileContent.FileData = destination;
                    await ReadFileToStreamAsync(fileObject, source, destination).ConfigureAwait(false);
                    break;
                case ReadingToBuffer:
                    var buffer = new byte[(int)fileObject.FileContent.FileRange.Length];
                    fileObject.FileContent.FileData = buffer;
                    await ReadFileToBufferAsync(fileObject, source, buffer).ConfigureAwait(false);
                    break;
                default:
                    throw new NezaboodkaException(string.Format("unknown value {0} in FileContentHandler.ReadingDestination",
                        ReadingDestination));
            }
        }

        protected virtual async Task ReadFileToFileAsync(FileObject fileObject, Stream source, string destination)
        {
            using (var destinationStream = new FileStream(destination, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                await ReadFileToStreamAsync(fileObject, source, destinationStream).ConfigureAwait(false);
        }

        protected virtual async Task ReadFileToStreamAsync(FileObject fileObject, Stream source, Stream destination)
        {
            var buffer = new byte[Const.DefaultFileBlockSize];
            long length = fileObject.FileContent.FileRange.Length;
            int received = 1; // Начальное значение больше нуля, чтобы зайти в цикл. Оно будет сразу же изменено внутри цикла.
            while (length > 0 && received > 0)
            {
                received = await source.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, length)).ConfigureAwait(false);
                await destination.WriteAsync(buffer, 0, received).ConfigureAwait(false);
                length -= received;
            }
            if (length > 0)
                throw new NezaboodkaException(string.Format(
                    "actual received data length {0} is less than the specified length {1} for file object {2} with file name {3}",
                    fileObject.FileContent.FileRange.Length - length, fileObject.FileContent.FileRange.Length,
                    fileObject, fileObject.FileName));
        }

        protected virtual async Task ReadFileToBufferAsync(FileObject fileObject, Stream source, byte[] destination)
        {
            int offset = 0;
            int length = destination.Length;
            int received = 1; // Начальное значение больше нуля, чтобы зайти в цикл. Оно будет сразу же изменено внутри цикла.
            while (length > 0 && received > 0)
            {
                received = await source.ReadAsync(destination, offset, length).ConfigureAwait(false);
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
