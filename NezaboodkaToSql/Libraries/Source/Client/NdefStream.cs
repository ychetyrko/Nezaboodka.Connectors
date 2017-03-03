using System;
using System.IO;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public sealed class NdefStream : Stream
    {
        private NdefReader fReader;

        // Public

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override long Length { get { throw new NotSupportedException(); } }

        public NdefStream(NdefReader reader)
        {
            fReader = reader;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int resultCount = 0;
            if (fReader.CurrentElement.Value.AsStream != null)
            {
                resultCount = fReader.CurrentElement.Value.AsStream.Read(buffer, offset, count);
                while (resultCount == 0 && fReader.MoveToNextElement() && fReader.CurrentElement.Value.AsStream != null)
                    resultCount = fReader.CurrentElement.Value.AsStream.Read(buffer, offset, count);
            }
            return resultCount;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
