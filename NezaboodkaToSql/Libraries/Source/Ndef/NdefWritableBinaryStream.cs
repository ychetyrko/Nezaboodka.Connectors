using System;
using System.IO;

namespace Nezaboodka.Ndef
{
    internal class NdefWritableBinaryStream : Stream
    {
        private readonly Stream fOutput;
        private readonly long fLength;
        private long fPosition;
        private bool fIsDisposed;

        public NdefWritableBinaryStream(Stream output, long length)
        {
            fOutput = output;
            fLength = length;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!fIsDisposed)
            {
                if (count >= 0)
                {
                    if (fPosition + count <= fLength)
                    {
                        fOutput.Write(buffer, offset, count);
                        fPosition += count;
                    }
                    else
                        throw new InvalidOperationException("The end of the stream is reached.");
                }
                else
                    throw new ArgumentOutOfRangeException("parameter " + nameof(count) + " must be non-negative");
            }
            else
                throw new ObjectDisposedException(nameof(NdefWritableBinaryStream));
        }

        protected override void Dispose(bool disposing)
        {
            fIsDisposed = true;
            if (fPosition != fLength)
                throw new InvalidOperationException("The end of the stream isn't reached.");
        }

        public override void Close()
        {
            Dispose(true);
        }

        public override bool CanRead { get {return false;} }
        public override bool CanSeek { get { return false;} }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return fLength; } }
        public override long Position { get { return fPosition; } set { throw new NotSupportedException();} }
    }
}