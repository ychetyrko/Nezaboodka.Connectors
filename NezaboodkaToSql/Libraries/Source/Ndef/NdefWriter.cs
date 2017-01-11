using System;
using System.Collections.Generic;
using System.IO;

namespace Nezaboodka.Ndef
{
    public class NdefWriter : INdefWriter, IDisposable
    {
        private static char[] SpecialCharacters = new char[] {
            NdefConst.ValueSeparator, NdefConst.ObjectStartMarker, NdefConst.ObjectKeyPrefix,
            NdefConst.ListItemMarker, NdefConst.ListItemToRemoveMarker };
        private static byte[] Indent = NdefConst.Encoding.GetBytes("  ");
        private static byte[] LineFeed = NdefConst.Encoding.GetBytes(Environment.NewLine);
        private static char[] LineFeedChars = NdefConst.Encoding.GetChars(LineFeed);

        // Fields

        private BinaryWriter fOutput;
        private byte[] fBuffer;
        private bool fIsLineFeedNeeded;
        private int fMargin;

        // Public

        public NdefWriter(Stream output)
        {
            fOutput = new BinaryWriter(output, NdefConst.Encoding, true);
            fBuffer = new byte[128];
            fMargin = 0;
        }

        public void Dispose()
        {
            fOutput.Dispose();
            fOutput = null;
        }

        public void WriteDataSetStart(bool isExtensionOfPreviousDataSet, string header)
        {
            if (!isExtensionOfPreviousDataSet)
                WriteLineFeedIfNeeded();
            Write(true, NdefConst.DataSetStartMarker);
            if (!isExtensionOfPreviousDataSet && !string.IsNullOrEmpty(header))
            {
                Write(false, " ");
                Write(false, header);
            }
            WriteLineFeed();
        }

        public void WriteDataSetEnd()
        {
            WriteLineFeedIfNeeded();
            Write(false, NdefConst.DataSetEndMarker);
            fIsLineFeedNeeded = true;
        }

        public void WriteObjectStart(bool noBraces, string type, string number, string key)
        {
            WriteLineFeedIfNeeded();
            if (!noBraces)
            {
                Write(true, NdefConst.ObjectStartMarker);
                fMargin++;
                if (!string.IsNullOrEmpty(type))
                {
                    Write(false, " ");
                    Write(false, type);
                }
                if (!string.IsNullOrEmpty(key))
                {
                    Write(false, " ");
                    Write(false, NdefConst.ObjectKeyPrefix);
                    Write(false, key);
                }
                if (!string.IsNullOrEmpty(number))
                {
                    Write(false, " ");
                    Write(false, NdefConst.ObjectNumberPrefix);
                    Write(false, number);
                }
                WriteLineFeed();
            }
        }

        public void WriteObjectEnd(bool noBraces)
        {
            WriteLineFeedIfNeeded();
            if (!noBraces)
            {
                fMargin--;
                WriteLine(true, NdefConst.ObjectEndMarker);
            }
        }

        public void WriteFieldName(string name)
        {
            WriteLineFeedIfNeeded();
            if (name.IndexOfAny(SpecialCharacters) >= 0)
                throw new ArgumentException(string.Format("invalid field name '{0}'", name));
            Write(true, name);
            Write(false, NdefConst.ValueSeparator);
            fIsLineFeedNeeded = true;
        }

        public void WriteListItem(bool isRemoved)
        {
            WriteLineFeedIfNeeded(); // Интересный use case - в самом общем случае возможно ли, что после записи поля идет запись элемента массива?
            char listItemPrefix = isRemoved ? NdefConst.ListItemToRemoveMarker : NdefConst.ListItemMarker;
            Write(true, listItemPrefix);
            fIsLineFeedNeeded = true;
        }

        public void WriteValue(string type, string value, bool hasNoLineFeeds)
        {
            if (!string.IsNullOrEmpty(type))
            {
                Write(false, " ");
                Write(false, NdefConst.ExplicitTypeNamePrefix);
                Write(false, type);
            }
            if (value != null)
            {
                if (hasNoLineFeeds)
                {
                    Write(false, " ");
                    WriteLine(false, value);
                }
                else
                {
                    bool isMultiLine = false;
                    foreach (var s in SplitTextIntoLines(value))
                    {
                        if (s != null)
                        {
                            if (isMultiLine)
                                Write(true, "");
                            else
                                Write(false, " ");
                            if (isMultiLine || s.Length == 0 || (s.Length > 0 &&
                                (Char.IsWhiteSpace(s[0]) || s[0] == NdefConst.QuotationMarker ||
                                s[0] == NdefConst.ObjectNumberPrefix || s[0] == NdefConst.ObjectKeyPrefix)))
                                Write(false, NdefConst.QuotationMarker);
                            Write(false, s);
                            if (s.Length == 0 || (s.Length > 0 && Char.IsWhiteSpace(s[s.Length - 1])))
                                Write(false, NdefConst.QuotationMarker);
                            WriteLineFeed();
                        }
                        else
                        {
                            isMultiLine = true;
                            WriteLineFeed();
                        }
                    }
                }
            }
            else
                WriteLineFeed();
            fIsLineFeedNeeded = false;
        }

        public void WriteReference(string key, string number)
        {
            if (!string.IsNullOrEmpty(key))
            {
                Write(false, " ");
                Write(false, NdefConst.ObjectKeyPrefix);
                Write(false, key);
            }
            if (!string.IsNullOrEmpty(number))
            {
                Write(false, " ");
                Write(false, NdefConst.ObjectNumberPrefix);
                Write(false, number);
            }
            WriteLineFeed();
            fIsLineFeedNeeded = false;
        }

        public Stream WriteBinaryData(long length)
        {
            WriteLineFeedIfNeeded();
            Write(true, NdefConst.BinaryDataMarker);
            WriteLine(false, length.ToString());
            fIsLineFeedNeeded = true;
            var result = new NdefWritableBinaryStream(fOutput.BaseStream, length);
            return result;
        }

        public void Flush()
        {
            fOutput.Flush();
        }

        // Internal

        private IEnumerable<string> SplitTextIntoLines(string text)
        {
            int k = 0;
            int i = text.IndexOfAny(LineFeedChars);
            while (i >= 0)
            {
                if (k == 0)
                    yield return null; // indicator of multi-line text
                bool isCRLF = i == k && i > 0 && text[i - 1] == '\r' && text[i] == '\n';
                if (!isCRLF)
                    yield return text.Substring(k, i - k);
                k = i + 1;
                i = text.IndexOfAny(LineFeedChars, k);
            }
            yield return text.Substring(k, text.Length - k);
        }

        private void Write(bool margin, string text)
        {
            if (margin)
                for (int i = 0; i < fMargin; ++i)
                    fOutput.Write(Indent);
            WriteAsUtf8(text);
        }

        private void Write(bool margin, char text)
        {
            if (margin)
                for (int i = 0; i < fMargin; ++i)
                    fOutput.Write(Indent);
            fOutput.Write(text);
        }

        private void WriteLine(bool margin, string text)
        {
            if (margin)
                for (int i = 0; i < fMargin; ++i)
                    fOutput.Write(Indent);
            WriteAsUtf8(text);
            WriteLineFeed();
        }

        private void WriteLine(bool margin, char text)
        {
            if (margin)
                for (int i = 0; i < fMargin; ++i)
                    fOutput.Write(Indent);
            fOutput.Write(text);
            WriteLineFeed();
        }

        private void WriteLineFeed()
        {
            fOutput.Write(LineFeed);
        }

        private void WriteLineFeedIfNeeded()
        {
            if (fIsLineFeedNeeded)
            {
                fOutput.Write(LineFeed);
                fIsLineFeedNeeded = false;
            }
        }

        private void WriteAsUtf8(string text)
        {
            int n = NdefConst.Encoding.GetMaxByteCount(text.Length);
            if (n > fBuffer.Length)
                fBuffer = new byte[n];
            n = NdefConst.Encoding.GetBytes(text, 0, text.Length, fBuffer, 0);
            fOutput.Write(fBuffer, 0, n);
        }
    }
}
