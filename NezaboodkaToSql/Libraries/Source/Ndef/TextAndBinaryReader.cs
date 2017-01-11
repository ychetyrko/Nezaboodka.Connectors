using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Nezaboodka.Ndef
{
    public class TextAndBinaryReader
    {
        public const int DefaultBufferSizeInBytes = 1024;

        // Fields

        private readonly Stream fInput;
        private readonly Encoding fEncoding;
        private readonly LinkedList<byte[]> fBuffers;
        private readonly List<int> fProcessedBytesCounts;
        private readonly int fBufferSizeInBytes;
        private int fCurrentByteIndexInLastBuffer;
        private int fLastBufferLength;

        // Public

        public TextAndBinaryReader(Stream input)
            : this(input, NdefConst.Encoding, DefaultBufferSizeInBytes)
        {
        }

        public TextAndBinaryReader(Stream input, Encoding encoding)
            : this(input, encoding, DefaultBufferSizeInBytes)
        {
        }

        public TextAndBinaryReader(Stream input, int bufferSizeInBytes)
            : this(input, NdefConst.Encoding, bufferSizeInBytes)
        {
        }

        public TextAndBinaryReader(Stream input, Encoding encoding, int bufferSizeInBytes)
        {
            fInput = input;
            fEncoding = encoding;
            fBufferSizeInBytes = bufferSizeInBytes;
            fBuffers = new LinkedList<byte[]>();
            fProcessedBytesCounts = new List<int>(1);
        }

        public string ReadLine()
        {
            string result = null;
            bool endOfLine = false;
            int startIndexInFirstBuffer = fCurrentByteIndexInLastBuffer;
            while (!endOfLine)
            {
                if (fCurrentByteIndexInLastBuffer < fLastBufferLength)
                {
                    endOfLine = !ProcessCurrentByte();
                    ++fCurrentByteIndexInLastBuffer;
                }
                else
                    endOfLine = !FillBuffer();
            }
            int processedBytesCountInAllBuffers = fProcessedBytesCounts.Sum();
            if (processedBytesCountInAllBuffers > 0)
            {
                result = GetStringFromBuffers(startIndexInFirstBuffer, processedBytesCountInAllBuffers);
                CleanBuffers();
            }
            return result;
        }

        public byte[] ReadBytes(int count)
        {
            byte[] result;
            if (count >= 0)
            {
                int availableBytesCount = fLastBufferLength - fCurrentByteIndexInLastBuffer;
                if (count <= availableBytesCount)
                {
                    result = new byte[count];
                    Array.Copy(fBuffers.Last.Value, fCurrentByteIndexInLastBuffer, result, 0, count);
                    fCurrentByteIndexInLastBuffer += count;
                }
                else
                {
                    int extraBytesCount = count - availableBytesCount;
                    var buffer = new byte[extraBytesCount];
                    var readBytesCount = fInput.Read(buffer, 0, buffer.Length);
                    result = new byte[availableBytesCount + readBytesCount];
                    Array.Copy(fBuffers.Last.Value, fCurrentByteIndexInLastBuffer, result, 0, availableBytesCount);
                    Array.Copy(buffer, 0, result, availableBytesCount, readBytesCount);
                    fCurrentByteIndexInLastBuffer += availableBytesCount;
                }
            }
            else
                throw new ArgumentOutOfRangeException("parameter " + nameof(count) + " must be non-negative");
            return result;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int result;
            if (count >= 0)
            {
                int availableBytesCount = fLastBufferLength - fCurrentByteIndexInLastBuffer;
                if (count <= availableBytesCount)
                {
                    Array.Copy(fBuffers.Last.Value, fCurrentByteIndexInLastBuffer, buffer, offset, count);
                    fCurrentByteIndexInLastBuffer += count;
                    result = count;
                }
                else
                {
                    int extraBytesCount = count - availableBytesCount;
                    var bufferEx = new byte[extraBytesCount];
                    var readedBytesCount = fInput.Read(bufferEx, 0, buffer.Length);
                    Array.Copy(fBuffers.Last.Value, fCurrentByteIndexInLastBuffer, buffer, offset, availableBytesCount);
                    Array.Copy(bufferEx, 0, buffer, offset + availableBytesCount, readedBytesCount);
                    fCurrentByteIndexInLastBuffer += availableBytesCount;
                    result = count;
                }
            }
            else
                throw new ArgumentOutOfRangeException("parameter " + nameof(count) + " must be non-negative");
            return result;
        }

        public string ReadToEnd()
        {
            string result = string.Empty;
            bool endOfStream = false;
            int startIndexInFirstBuffer = fCurrentByteIndexInLastBuffer;
            while (!endOfStream)
            {
                while (fCurrentByteIndexInLastBuffer < fLastBufferLength)
                {
                    fProcessedBytesCounts[fProcessedBytesCounts.Count - 1]++;
                    ++fCurrentByteIndexInLastBuffer;
                }
                endOfStream = !FillBuffer();
            }
            int processedBytesCountInAllBuffers = fProcessedBytesCounts.Sum();
            if (processedBytesCountInAllBuffers > 0)
            {
                result = GetStringFromBuffers(startIndexInFirstBuffer, processedBytesCountInAllBuffers);
                CleanBuffers();
            }
            return result;
        }

        private bool ProcessCurrentByte()
        {
            bool result = false;
            byte b = fBuffers.Last.Value[fCurrentByteIndexInLastBuffer];
            switch (b)
            {
                case 13:
                    ++fCurrentByteIndexInLastBuffer;
                    bool canMoveNext = true;
                    bool bufferWasAdded = false;
                    if (fCurrentByteIndexInLastBuffer == fLastBufferLength)
                    {
                        canMoveNext = FillBuffer();
                        bufferWasAdded = canMoveNext;
                    }
                    if (canMoveNext)
                    {
                        byte next = fBuffers.Last.Value[fCurrentByteIndexInLastBuffer];
                        if (next != 10)
                        {
                            if (bufferWasAdded)
                            {
                                fProcessedBytesCounts[fProcessedBytesCounts.Count - 2]++;
                                fProcessedBytesCounts[fProcessedBytesCounts.Count - 1]++;
                            }
                            else
                                fProcessedBytesCounts[fProcessedBytesCounts.Count - 1] += 2;
                            result = true;
                        }
                    }
                    else
                    {
                        fProcessedBytesCounts[fProcessedBytesCounts.Count - 1]++;
                        result = true;
                    }
                    break;
                case 10:
                    break;
                default:
                    fProcessedBytesCounts[fProcessedBytesCounts.Count - 1]++;
                    result = true;
                    break;
            }
            return result;
        }

        private bool FillBuffer()
        {
            bool result = false;
            var buffer = new byte[fBufferSizeInBytes];
            fLastBufferLength = fInput.Read(buffer, 0, buffer.Length);
            if (fLastBufferLength > 0)
            {
                fBuffers.AddLast(buffer);
                fProcessedBytesCounts.Add(0);
                fCurrentByteIndexInLastBuffer = 0;
                result = true;
            }
            return result;
        }

        private string GetStringFromBuffers(int startInFirstBuffer, int processedBytesCountInAllBuffers)
        {
            var buffer = new byte[processedBytesCountInAllBuffers];
            int count = 0;
            int i = 0;
            foreach (byte[] buf in fBuffers)
            {
                if (buf == fBuffers.First.Value)
                    Array.Copy(buf, startInFirstBuffer, buffer, count, fProcessedBytesCounts[i]);
                else if (buf == fBuffers.Last.Value)
                    Array.Copy(buf, 0, buffer, count, fProcessedBytesCounts[i]);
                else
                    Array.Copy(buf, 0, buffer, count, fProcessedBytesCounts[i]);
                count += fProcessedBytesCounts[i];
                i++;
            }
            string result = fEncoding.GetString(buffer, 0, count);
            return result;
        }

        private void CleanBuffers()
        {
            while (fBuffers.Count > 1)
                fBuffers.RemoveFirst();
            fProcessedBytesCounts.Clear();
            fProcessedBytesCounts.Add(0);
        }
    }
}