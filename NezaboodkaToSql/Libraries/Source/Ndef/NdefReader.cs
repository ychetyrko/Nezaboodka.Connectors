using System;
using System.IO;
using System.Text;

namespace Nezaboodka.Ndef
{
    public class NdefReader : AbstractNdefReader
    {
        private TextAndBinaryReader fInput;
        private string fCurrentLine;
        private int fCurrentLineStart;
        private StringBuilder fMemoValueBuffer;

        // Public

        public NdefReader(Stream input) : base()
        {
            fInput = new TextAndBinaryReader(input, NdefConst.Encoding);
            fMemoValueBuffer = new StringBuilder();
            ReadNextLineSkipEmpty();
        }

        protected override bool MoveToNextLine()
        {
            return TryParseCurrentLine(false, false);
        }

        // Internal

        private bool TryParseCurrentLine(bool justTry, bool isListItem)
        {
            bool result = fCurrentLineStart >= 0;
            if (result)
            {
                switch (fCurrentLine[fCurrentLineStart])
                {
                    case NdefConst.DataSetStartMarker:
                        ParseDataSetStart(); break;
                    case NdefConst.DataSetEndMarker:
                        ParseDataSetEnd(); break;
                    case NdefConst.ObjectStartMarker:
                        ParseObjectStart(isListItem); break;
                    case NdefConst.ObjectEndMarker:
                        if (!justTry) ParseObjectEnd(); break;
                    case NdefConst.ListItemMarker:
                        if (!isListItem) ParseListItem(false); break;
                    case NdefConst.ListItemToRemoveMarker:
                        if (!isListItem) ParseListItem(true); break;
                    case NdefConst.QuotationMarker:
                        ParseMemoValue(isListItem); break;
                    case NdefConst.BinaryDataMarker:
                        ParseBinaryData(); break;
                    case NdefConst.CommentMarker:
                        ParseComment(); break;
                    default:
                        if (!justTry) ParseField(); break;
                }
            }
            return result;
        }

        private void ParseDataSetStart()
        {
            string header = NdefUtils.TrimEx(fCurrentLine, fCurrentLineStart + 1,
                fCurrentLine.Length - fCurrentLineStart - 1);
            PutDataSetStartToBuffer(header);
            ReadNextLineSkipEmpty();
        }

        private void ParseDataSetEnd()
        {
            PutDataSetEndToBuffer();
            ReadNextLineSkipEmpty();
        }

        private void ParseObjectStart(bool isListItem)
        {
            string number = null;
            string key = null;
            string type = null;
            char prefix;
            int i = NdefUtils.ParsePrefixedTokenForObjectHeader(fCurrentLine, fCurrentLineStart, out prefix, out type);
            if (i >= 0)
            {
                i = NdefUtils.ParsePrefixedTokenForObjectHeader(fCurrentLine, i, out prefix, out key);
                if (prefix == NdefConst.ObjectKeyPrefix)
                {
                    if (i >= 0)
                    {
                        NdefUtils.ParsePrefixedTokenForObjectHeader(fCurrentLine, i, out prefix, out number);
                        if (prefix != NdefConst.ObjectNumberPrefix)
                            throw new FormatException(string.Format("unexpected token: {0}{1}", prefix, number));
                    }
                }
                else if (prefix == NdefConst.ObjectNumberPrefix)
                {
                    number = key;
                    key = null;
                }
                else
                    throw new FormatException(string.Format("unexpected token: {0}{1}", prefix, key));
            }
            PutObjectStartToBuffer(isListItem, type, NdefObjectKind.ToBeDefined, number, key);
            ReadNextLineSkipEmpty();
        }

        private void ParseObjectEnd()
        {
            if (!CurrentObject.IsUntypedList)
            {
                PutObjectEndToBuffer();
                ReadNextLineSkipEmpty();
            }
            else
                PutObjectEndToBuffer();
        }

        private void ParseField()
        {
            if (CurrentObject.Header.Kind == NdefObjectKind.Object || CurrentObject.Header.Kind == NdefObjectKind.ToBeDefined)
            {
                CurrentObject.Header.Kind = NdefObjectKind.Object;
                string name = null;
                string value = null;
                NdefUtils.SplitAndTrim(fCurrentLine, fCurrentLineStart, out name, NdefConst.ValueSeparator, out value, false);
                PutFieldNameToBuffer(name);
                string explicitTypeName;
                string number;
                string key;
                ParseValueEx(value, 0, out explicitTypeName, out value, out number, out key);
                ReadNextLineSkipEmpty();
                if (explicitTypeName != null || value != null || number != null || key != null)
                    PutValueToBuffer(explicitTypeName, value, number, key);
                else
                    TryParseCurrentLine(true, false);
            }
            else if (CurrentObject.IsUntypedList)
                PutObjectEndToBuffer();
            else
                throw new FormatException(string.Format("cannot parse line: {0}", fCurrentLine));
        }

        private void ParseListItem(bool removed)
        {
            if (CurrentObject.Header.Kind == NdefObjectKind.List || CurrentObject.Header.Kind == NdefObjectKind.ToBeDefined)
            {
                CurrentObject.Header.Kind = NdefObjectKind.List;
                string explicitTypeName;
                string number;
                string key;
                string value;
                ParseValueEx(fCurrentLine.Substring(fCurrentLineStart + 1), 0, out explicitTypeName,
                    out value, out number, out key);
                ReadNextLineSkipEmpty();
                if (explicitTypeName == null && value == null && number == null && key == null)
                {
                    TryParseCurrentLine(false, true);
                    // TODO: Cosmetics - to get rid of access to BufferObject
                    if (BufferObject.CurrentElement.Value.IsUndefined && BufferObject.CurrentElement.Comment == null)
                    {
                        PutListItemToBuffer(removed);
                        PutValueToBuffer(null, null, NdefValue.NullValue.AsObjectNumber, null);
                    }
                }
                else
                {
                    PutListItemToBuffer(removed);
                    PutValueToBuffer(explicitTypeName, value, number, key);
                }
            }
            else if (BufferObject.CurrentElement.Field.Name != null)
                PutObjectStartToBuffer(true, NdefConst.ListTypeBraces, NdefObjectKind.List, null, null); // untyped list
            else
                throw new FormatException(string.Format("cannot parse line: {0}", fCurrentLine));
        }

        private void ParseMemoValue(bool isListItem)
        {
            string dummy1;
            string dummy2;
            string dummy3;
            bool newLineNeeded = false;
            while (fCurrentLine[fCurrentLineStart] == NdefConst.QuotationMarker)
            {
                string value = ParseValue(fCurrentLine.Substring(fCurrentLineStart), false,
                    out dummy1, out dummy2, out dummy3);
                if (fMemoValueBuffer.Length > 0 || newLineNeeded)
                {
                    fMemoValueBuffer.Append('\n');
                    newLineNeeded = false;
                }
                if (!string.IsNullOrEmpty(value))
                    fMemoValueBuffer.Append(value);
                else
                    newLineNeeded = true;
                ReadNextLineSkipEmpty();
            }
            if (isListItem)
            {
                PutListItemToBuffer(false);
                PutValueToBuffer(null, fMemoValueBuffer.ToString(), null, null);
            }
            else
                PutValueToBuffer(null, fMemoValueBuffer.ToString(), null, null);
            fMemoValueBuffer.Length = 0;
        }

        private void ParseBinaryData()
        {
            long length = GetBinaryDataLength();
            var binaryStream = new NdefReadableBinaryStream(fInput, length);
            PutBinaryDataToBuffer(binaryStream);
        }

        private void ParseComment()
        {
            PutCommentToBuffer(fCurrentLine);
            ReadNextLineSkipEmpty();
        }

        private void ReadNextLineSkipEmpty()
        {
            fCurrentLine = fInput.ReadLine();
            fCurrentLineStart = FindFirstNonWhitespaceChar(fCurrentLine, 0);
            while (fCurrentLine != null && fCurrentLineStart < 0)
            {
                fCurrentLine = fInput.ReadLine();
                fCurrentLineStart = FindFirstNonWhitespaceChar(fCurrentLine, 0);
            }
        }

        private static void ParseValueEx(string value, int start, out string explicitTypeName,
            out string scalar, out string number, out string key)
        {
            scalar = null;
            explicitTypeName = null;
            number = null;
            key = null;
            char prefix;
            string result;
            int  i = NdefUtils.ParsePrefixedToken(value, 0, out prefix, out result);
            if (prefix == NdefConst.ObjectKeyPrefix)
            {
                key = result;
                if (i > 0)
                {
                    NdefUtils.ParsePrefixedToken(value, i, out prefix, out result);
                    if (prefix == NdefConst.ObjectNumberPrefix)
                        number = result;
                    else
                        throw new FormatException(string.Format("unexpected token: {0}", result));
                }
            }
            else if (prefix == NdefConst.ObjectNumberPrefix)
                number = result;
            else if (prefix == NdefConst.QuotationMarker)
            {
                if (string.IsNullOrEmpty(result) || result[result.Length - 1] != NdefConst.QuotationMarker)
                    scalar = result;
                else
                    scalar = result.Substring(0, result.Length - 1);
            }
            else if (prefix == NdefConst.ExplicitTypeNamePrefix)
            {
                explicitTypeName = result;
                scalar = value.Substring(i + 1);
            }
            else
                scalar = result;
        }

        private static string ParseValue(string value, bool detectReferenceAndExplicitTypeName,
            out string explicitTypeName, out string number, out string key)
        {
            bool isReference = false;
            explicitTypeName = null;
            number = null;
            key = null;
            if (value == null)
                return null; // TODO: refactor
            value = value.Trim();
            string result = value;
            if (value.Length > 0)
            {
                int i = 0;
                int len = value.Length;
                if (value.Length > 0)
                {
                    if (detectReferenceAndExplicitTypeName)
                    {
                        if (value[0] == NdefConst.ExplicitTypeNamePrefix)
                        {
                            i = FindFirstWhitespaceChar(value, 1);
                            if (i < 0)
                                i = ~i;
                            explicitTypeName = value.Substring(1, i - 1);
                            i++;
                        }
                        else if (value[0] == NdefConst.ObjectNumberPrefix)
                        {
                            throw new NotImplementedException();
                        }
                        else if (value[0] == NdefConst.ObjectKeyPrefix)
                        {
                            isReference = true;
                            i = 1;
                        }
                    }
                    else if (value[0] == NdefConst.QuotationMarker)
                        i = 1;
                }
                if (value.Length > 0 && value[len - 1] == NdefConst.QuotationMarker)
                    len--;
                if (i != 0 || len != value.Length)
                {
                    if (len > 0)
                    {
                        if (isReference)
                        {
                            key = value.Substring(i, len - i);
                            result = null;
                        }
                        else
                            result = value.Substring(i, len - i);
                        if (result == "")
                            result = null;
                        if (key == "")
                            key = null;
                    }
                    else
                        result = null;
                }
            }
            else
                result = null;
            return result;
        }

        private static int FindFirstWhitespaceChar(string text, int start)
        {
            int result = start;
            int length = text != null ? text.Length : 0;
            while (result < length && !IsWhiteSpaceChar(text[result]))
                result += 1;
            return result < length ? result : ~result;
        }

        private static int FindFirstNonWhitespaceChar(string text, int start)
        {
            int result = start;
            int length = text != null ? text.Length : 0;
            while (result < length && IsWhiteSpaceChar(text[result]))
                result += 1;
            return result < length ? result : ~result;
        }

        private static bool IsWhiteSpaceChar(char c)
        {
            return Char.IsWhiteSpace(c) || c == '\0';
        }

        private long GetBinaryDataLength()
        {
            string[] items = fCurrentLine.Split(NdefConst.BinaryDataMarker);
            long length = long.Parse(items[1]);
            return length;
        }
    }
}
