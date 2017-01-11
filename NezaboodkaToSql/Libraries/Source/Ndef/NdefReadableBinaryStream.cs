using System;
using System.IO;

namespace Nezaboodka.Ndef
{
    internal class NdefReadableBinaryStream : Stream
    {
        private readonly TextAndBinaryReader fInput;
        private readonly long fLength;
        private long fPosition;
        private bool fIsDisposed;

        public NdefReadableBinaryStream(TextAndBinaryReader input, long length)
        {
            fInput = input;
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
            int result;
            if (!fIsDisposed)
            {
                if (count >= 0)
                {
                    if (fPosition + count <= fLength)
                        result = fInput.Read(buffer, offset, count);
                    else
                    {
                        int n = (int)(fLength - fPosition);
                        result = fInput.Read(buffer, offset, n);
                    }
                    fPosition += result;
                }
                else
                    throw new ArgumentOutOfRangeException("parameter " + nameof(count) + " must be non-negative");
            }
            else
                throw new ObjectDisposedException(nameof(NdefReadableBinaryStream));
            return result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return fLength; } }
        public override long Position { get { return fPosition; } set { throw new NotSupportedException(); } }
    }
}