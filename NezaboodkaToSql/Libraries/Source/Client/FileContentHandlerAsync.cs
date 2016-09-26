using System;
using System.IO;
using System.Threading.Tasks;

namespace Nezaboodka
{
    public partial class FileContentHandler
    {
        // Asynchronous

        public virtual async Task WriteFilesToStreamAsync(WriteObjectsRequest request, Stream stream)
        {
            if (request.FileObjects != null)
                foreach (FileObject fileObject in request.FileObjects)
                    await WriteFileToStreamAsync(fileObject, fileObject.FileContent.FileRange.Position,
                        fileObject.FileContent.FileRange.Length, stream).ConfigureAwait(false);
        }

        public virtual async Task ReadFilesFromStreamAsync(ReadObjectsResponse response, Stream stream)
        {
            if (response.FileObjects != null)
                foreach (FileObject fileObject in response.FileObjects)
                    await ReadFileFromStreamAsync(fileObject, fileObject.FileContent.FileRange.Position,
                        fileObject.FileContent.FileRange.Length, stream).ConfigureAwait(false);
        }

        // WriteFilesToStreamAsync

        protected virtual async Task WriteFileToStreamAsync(FileObject fileObject, long position, long length, Stream stream)
        {
            object fileData = fileObject.FileContent.FileData;
            if (fileData is string)
                await WriteFromFileAsync(fileData as string, position, length, stream).ConfigureAwait(false);
            else if (fileData is Stream)
                await WriteFromStreamAsync(fileData as Stream, position, length, stream).ConfigureAwait(false);
            else if (fileData.GetType() == typeof(byte[]))
            {
                byte[] buffer = fileData as byte[];
                if (position >= 0 && position <= int.MaxValue && length >= 0 && length <= buffer.Length)
                    await WriteFromBufferAsync(buffer, position, length, stream).ConfigureAwait(false);
                else
                    throw new NezaboodkaException(string.Format(
                        "wrong buffer range [{0}, {1}] specified for file object {2} (file name: {3})",
                        position, length, fileObject.Key, fileObject.FileName));
            }
            else
                throw new NezaboodkaException(string.Format(
                    "type {0} in FileObject.FileObjectMetadata.FileData is not supported, file object {1} (file name: {2})",
                    fileData.GetType(), fileObject.Key, fileObject.FileName));
        }

        protected virtual async Task WriteFromFileAsync(string filePath, long position, long length, Stream destination)
        {
            using (var source = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                await WriteFromStreamAsync(source, position, length, destination).ConfigureAwait(false);
            }
        }

        protected virtual async Task WriteFromStreamAsync(Stream source, long position, long length, Stream destination)
        {
            if (position != 0)
                source.Seek(position, SeekOrigin.Begin);
            var buffer = new byte[Const.DefaultFileBlockSize];
            int count = 0;
            while (length - count > 0)
            {
                int n = (int)Math.Min(buffer.Length, length - count);
                n = await source.ReadAsync(buffer, 0, n).ConfigureAwait(false);
                if (n > 0)
                {
                    await destination.WriteAsync(buffer, 0, n).ConfigureAwait(false);
                    count += n;
                }
            }
        }

        protected virtual async Task WriteFromBufferAsync(byte[] source, long position, long length, Stream destination)
        {
            await destination.WriteAsync(source, (int)position, (int)length).ConfigureAwait(false);
        }

        // ReadFilesFromStreamAsync

        protected virtual async Task ReadFileFromStreamAsync(FileObject fileObject, long position, long length, Stream stream)
        {
            switch (ReadingDestination)
            {
                case ReadingToFile:
                    string filePath = Path.Combine(LocalFilesFolder, fileObject.Key.ToString());
                    fileObject.FileContent.FileData = filePath;
                    await ReadToFileAsync(stream, filePath, position, length).ConfigureAwait(false);
                    break;
                case ReadingToStream:
                    var destination = CreateStream(fileObject, position, length);
                    fileObject.FileContent.FileData = destination;
                    await ReadToStreamAsync(stream, destination, position, length).ConfigureAwait(false);
                    break;
                case ReadingToBuffer:
                    var buffer = new byte[(int)fileObject.FileContent.FileRange.Length];
                    fileObject.FileContent.FileData = buffer;
                    await ReadToBufferAsync(stream, buffer, position, length).ConfigureAwait(false);
                    break;
                default:
                    throw new NezaboodkaException(string.Format("unknown value {0} in FileContentHandler.ReadingDestination",
                        ReadingDestination));
            }
        }

        protected virtual async Task ReadToFileAsync(Stream source, string filePath, long position, long length)
        {
            using (var destination = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
            {
                await ReadToStreamAsync(source, destination, position, length).ConfigureAwait(false);
            }
        }

        protected virtual async Task ReadToStreamAsync(Stream source, Stream destination, long position, long length)
        {
            destination.Position = position;
            var buffer = new byte[Const.DefaultFileBlockSize];
            int count = 0;
            while (length - count > 0)
            {
                int n = await source.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, length - count)).ConfigureAwait(false);
                await destination.WriteAsync(buffer, 0, n).ConfigureAwait(false);
                count += n;
            }
        }

        protected virtual async Task ReadToBufferAsync(Stream source, byte[] destination, long position, long length)
        {
            int count = 0;
            while (length - count > 0)
            {
                count += await source.ReadAsync(destination, count, (int)length - count).ConfigureAwait(false);
            }
        }
    }
}
