using System;
using System.IO;
using System.Text;

namespace Nezaboodka.Ndef
{
    public class NdefTextReader : NdefIterator
    {
        // Fields
        private TextReader fInput;
        private string fCurrentLine;
        private int fCurrentLineStart;
        private StringBuilder fMemoValueBuffer = new StringBuilder();

        // Public

        public bool MsDosLineFeed { get; set; }

        public NdefTextReader(TextReader input) : base()
        {
            fInput = input;
            ReadNextLineSkipEmptyAndComment();
        }

        protected override bool NeedMoreData()
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
                    case NdefConst.BlockStartMarker:
                        ParseBlockStart(); break;
                    case NdefConst.BlockEndMarker:
                        ParseBlockEnd(); break;
                    case NdefConst.ObjectStartMarker:
                        ParseObjectStart(isListItem); break;
                    case NdefConst.ObjectEndMarker:
                        if (!justTry) ParseObjectEnd(); break;
                    case NdefConst.ListItemMarker:
                        if (!isListItem) ParseListItem(NdefValueUpdateMode.SetOrAddToList); break;
                    case NdefConst.ListItemToRemoveMarker:
                        if (!isListItem) ParseListItem(NdefValueUpdateMode.ResetOrRemoveFromList); break;
                    case NdefConst.QuotationMarker:
                        ParseMemoValue(isListItem); break;
                    default:
                        if (!justTry) ParseField(); break;
                }
            }
            return result;
        }

        private void ParseBlockStart()
        {
            string header = NdefUtils.TrimEx(fCurrentLine, fCurrentLineStart + 1,
                fCurrentLine.Length - fCurrentLineStart - 1);
            PutBlockStartToBuffer(header);
            ReadNextLineSkipEmptyAndComment();
        }

        private void ParseBlockEnd()
        {
            PutBlockEndToBuffer();
            ReadNextLineSkipEmptyAndComment();
        }

        private void ParseObjectStart(bool isListItem)
        {
            string serialKey = null;
            string logicalKey = null;
            string type = null;
            char prefix;
            int i = NdefUtils.ParsePrefixedTokenForObjectHeader(fCurrentLine, fCurrentLineStart, out prefix, out type);
            if (i >= 0)
            {
                i = NdefUtils.ParsePrefixedTokenForObjectHeader(fCurrentLine, i, out prefix, out logicalKey);
                if (prefix == NdefConst.LogicalKeyPrefix)
                {
                    if (i >= 0)
                    {
                        NdefUtils.ParsePrefixedTokenForObjectHeader(fCurrentLine, i, out prefix, out serialKey);
                        if (prefix != NdefConst.SerialKeyPrefix)
                            throw new FormatException(string.Format("unexpected token: {0}{1}", prefix, serialKey));
                    }
                }
                else if (prefix == NdefConst.SerialKeyPrefix)
                {
                    serialKey = logicalKey;
                    logicalKey = null;
                }
                else
                    throw new FormatException(string.Format("unexpected token: {0}{1}", prefix, logicalKey));
            }
            PutObjectStartToBuffer(isListItem, type, NdefObjectKind.ToBeDefined, serialKey, logicalKey);
            ReadNextLineSkipEmptyAndComment();
        }

        private void ParseObjectEnd()
        {
            if (!CurrentObject.IsUntypedList)
            {
                PutObjectEndToBuffer();
                ReadNextLineSkipEmptyAndComment();
            }
            else
                PutObjectEndToBuffer();
        }

        private void ParseField()
        {
            if (CurrentObject.Kind == NdefObjectKind.Object || CurrentObject.Kind == NdefObjectKind.ToBeDefined)
            {
                CurrentObject.Kind = NdefObjectKind.Object;
                string name = null;
                string value = null;
                NdefUtils.SplitAndTrim(fCurrentLine, fCurrentLineStart, out name, NdefConst.ValueSeparator, out value, false);
                PutFieldToBuffer(name);
                string explicitTypeName;
                string serialKey;
                string logicalKey;
                ParseValueEx(value, 0, out explicitTypeName, out value, out serialKey, out logicalKey);
                ReadNextLineSkipEmptyAndComment();
                if (explicitTypeName != null || value != null || serialKey != null || logicalKey != null)
                    PutValueToBuffer(explicitTypeName, value, serialKey, logicalKey);
                else
                    TryParseCurrentLine(true, false);
            }
            else if (CurrentObject.IsUntypedList)
                PutObjectEndToBuffer();
            else
                throw new FormatException(string.Format("cannot parse line: {0}", fCurrentLine));
        }

        private void ParseListItem(NdefValueUpdateMode valueUpdateMode)
        {
            if (CurrentObject.Kind == NdefObjectKind.List || CurrentObject.Kind == NdefObjectKind.ToBeDefined)
            {
                CurrentObject.Kind = NdefObjectKind.List;
                string explicitTypeName;
                string serialKey;
                string logicalKey;
                string value;
                ParseValueEx(fCurrentLine.Substring(fCurrentLineStart + 1), 0, out explicitTypeName,
                    out value, out serialKey, out logicalKey);
                ReadNextLineSkipEmptyAndComment();
                if (explicitTypeName == null && value == null && serialKey == null && logicalKey == null)
                {
                    TryParseCurrentLine(false, true);
                    // TODO: Cosmetics - to get rid of access to BufferObject
                    if (BufferObject.CurrentLine.Value.IsUndefined)
                        PutListItemToBuffer(valueUpdateMode, null, null, NdefValue.NullValue.AsSerialKey, null);
                }
                else
                    PutListItemToBuffer(valueUpdateMode, explicitTypeName, value, serialKey, logicalKey);
            }
            else if (BufferObject.CurrentLine.Field.Name != null)
                PutObjectStartToBuffer(true, null, NdefObjectKind.List, null, null); // untyped list
            else
                throw new FormatException(string.Format("cannot parse line: {0}", fCurrentLine));
        }

        private void ParseMemoValue(bool isListItem)
        {
            string dummy;
            string dummy2;
            string dummy3;
            bool newLineNeeded = false;
            while (fCurrentLine[fCurrentLineStart] == NdefConst.QuotationMarker)
            {
                string value = ParseValue(fCurrentLine.Substring(fCurrentLineStart), false,
                    out dummy, out dummy2, out dummy3);
                if (fMemoValueBuffer.Length > 0 || newLineNeeded)
                {
                    fMemoValueBuffer.Append(MsDosLineFeed ? "\r\n" : "\n");
                    newLineNeeded = false;
                }
                if (!string.IsNullOrEmpty(value))
                    fMemoValueBuffer.Append(value);
                else
                    newLineNeeded = true;
                ReadNextLineSkipEmptyAndComment();
            }
            if (isListItem)
                PutListItemToBuffer(NdefValueUpdateMode.SetOrAddToList, null, fMemoValueBuffer.ToString(), null, null);
            else
                PutValueToBuffer(null, fMemoValueBuffer.ToString(), null, null);
            fMemoValueBuffer.Length = 0;
        }

        private void ReadNextLineSkipEmptyAndComment()
        {
            fCurrentLine = fInput.ReadLine();
            fCurrentLineStart = FindFirstNonWhitespaceChar(fCurrentLine, 0);
            while (fCurrentLine != null && (fCurrentLineStart < 0 || fCurrentLine[fCurrentLineStart] == NdefConst.CommentMarker))
            {
                fCurrentLine = fInput.ReadLine();
                fCurrentLineStart = FindFirstNonWhitespaceChar(fCurrentLine, 0);
            }
        }


        private static void ParseValueEx(string value, int start, out string explicitTypeName,
            out string scalar, out string serialKey, out string logicalKey)
        {
            scalar = null;
            explicitTypeName = null;
            serialKey = null;
            logicalKey = null;
            char prefix;
            string result;
            int  i = NdefUtils.ParsePrefixedToken(value, 0, out prefix, out result);
            if (prefix == NdefConst.LogicalKeyPrefix)
            {
                logicalKey = result;
                if (i > 0)
                {
                    NdefUtils.ParsePrefixedToken(value, i, out prefix, out result);
                    if (prefix == NdefConst.SerialKeyPrefix)
                        serialKey = result;
                    else
                        throw new FormatException(string.Format("unexpected token: {0}", result));
                }
            }
            else if (prefix == NdefConst.SerialKeyPrefix)
                serialKey = result;
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
            out string explicitTypeName, out string serialKey, out string logicalKey)
        {
            bool isReference = false;
            explicitTypeName = null;
            serialKey = null;
            logicalKey = null;
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
                        else if (value[0] == NdefConst.SerialKeyPrefix)
                        {
                            throw new NotImplementedException();
                        }
                        else if (value[0] == NdefConst.LogicalKeyPrefix)
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
                            logicalKey = value.Substring(i, len - i);
                            result = null;
                        }
                        else
                            result = value.Substring(i, len - i);
                        if (result == "")
                            result = null;
                        if (logicalKey == "")
                            logicalKey = null;
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
    }
}
