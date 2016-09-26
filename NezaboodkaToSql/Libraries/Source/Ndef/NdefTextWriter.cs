using System;
using System.Collections.Generic;
using System.IO;

namespace Nezaboodka.Ndef
{
    public class NdefTextWriter : INdefWriter
    {
        // Global
        private static char[] SpecialCharacters = new char[] {
            NdefConst.ValueSeparator, NdefConst.ObjectStartMarker, NdefConst.LogicalKeyPrefix,
            NdefConst.ListItemMarker, NdefConst.ListItemToRemoveMarker };
        private static char[] LineFeedCharacters = new char[] { '\x0D', '\x0A' };

        // Fields
        private TextWriter fOutput;
        private Stack<bool> fIsUntypedListStack;
        private int fMargin;

        // Public

        public NdefTextWriter(TextWriter output)
        {
            fOutput = output;
            fIsUntypedListStack = new Stack<bool>();
            fMargin = 0;
            fIsUntypedListStack.Push(false);
        }

        public void WriteBlockHeader(string header)
        {
            Write(true, NdefConst.BlockStartMarker);
            if (!string.IsNullOrEmpty(header))
            {
                Write(false, " ");
                Write(false, header);
            }
            WriteLine(false, "");
        }

        public void WriteObjectHeader(string type, NdefObjectKind kind, string serialKey, string logicalKey)
        {
            bool isUntypedList = string.IsNullOrEmpty(type) && kind == NdefObjectKind.List;
            fIsUntypedListStack.Push(isUntypedList);
            if (!isUntypedList)
            {
                Write(true, NdefConst.ObjectStartMarker);
                fMargin++;
                if (!string.IsNullOrEmpty(type))
                {
                    Write(false, " ");
                    Write(false, type);
                }
                if (!string.IsNullOrEmpty(logicalKey))
                {
                    Write(false, " ");
                    Write(false, NdefConst.LogicalKeyPrefix);
                    Write(false, logicalKey);
                }
                if (!string.IsNullOrEmpty(serialKey))
                {
                    Write(false, " ");
                    Write(false, NdefConst.SerialKeyPrefix);
                    Write(false, serialKey);
                }
                WriteLine(false, "");
            }
        }

        public void WriteObjectFooter()
        {
            if (!fIsUntypedListStack.Pop())
            {
                fMargin--;
                WriteLine(true, NdefConst.ObjectEndMarker);
            }
        }

        public void WriteFieldNameAndValue(string name, string explicitTypeName,
            string scalar, string serialKey, string logicalKey)
        {
            if (name.IndexOfAny(SpecialCharacters) >= 0)
                throw new ArgumentException(string.Format("invalid field name '{0}'", name));
            Write(true, name);
            Write(false, NdefConst.ValueSeparator);
            WriteValueImpl(false, NdefValueUpdateMode.SetOrAddToList, explicitTypeName, scalar, serialKey, logicalKey);
        }

        public void WriteFieldNameForNestedObjectOrList(string name)
        {
            if (name.IndexOfAny(SpecialCharacters) >= 0)
                throw new ArgumentException(string.Format("invalid field name '{0}'", name));
            Write(true, name);
            WriteLine(false, NdefConst.ValueSeparator);
        }

        public void WriteListItem(NdefValueUpdateMode valueUpdateMode, string explicitTypeName,
            string scalar, string serialKey, string logicalKey)
        {
            WriteValueImpl(true, valueUpdateMode, explicitTypeName, scalar, serialKey, logicalKey);
        }

        public void WriteListItemForNestedObjectOrList(NdefValueUpdateMode valueUpdateMode)
        {
            WriteValueImpl(true, valueUpdateMode, null, null, null, null);
        }

        public Stream OpenWriteBlockFooter(long length)
        {
            // TODO: write binaryData
            WriteLine(true, NdefConst.BlockEndMarker);
            return null; // not implemented yet
        }

        public void WriteObjectsFrom(INdefIterator iterator)
        {
            GenerateObjectsFrom(iterator);
        }

        public void WriteAllFrom(INdefIterator iterator, bool blocksRequired)
        {
            if (!blocksRequired)
                GenerateObjectsFrom(iterator);
            while (iterator.MoveToNextBlockHeader())
            {
                NdefBlock block = iterator.CurrentBlock;
                if (block.IsStartOfBlock)
                    WriteBlockHeader(block.Header);
                GenerateObjectsFrom(iterator);
                if (block.IsEndOfBlock)
                    OpenWriteBlockFooter(0);
                if (!blocksRequired)
                    GenerateObjectsFrom(iterator);
            }
        }

        // Internal

        private void WriteValueImpl(bool isListItem, NdefValueUpdateMode valueUpdateMode,
            string explicitTypeName, string scalar, string serialKey, string logicalKey)
        {
            if (isListItem)
            {
                var listItemPrefix = valueUpdateMode == NdefValueUpdateMode.SetOrAddToList ?
                    NdefConst.ListItemMarker :
                    NdefConst.ListItemToRemoveMarker;
                Write(true, listItemPrefix);
            }
            if (!string.IsNullOrEmpty(explicitTypeName))
            {
                Write(false, " ");
                Write(false, NdefConst.ExplicitTypeNamePrefix);
                Write(false, explicitTypeName);
            }
            if (!string.IsNullOrEmpty(logicalKey))
            {
                Write(false, " ");
                Write(false, NdefConst.LogicalKeyPrefix);
                Write(false, logicalKey);
            }
            if (!string.IsNullOrEmpty(serialKey))
            {
                Write(false, " ");
                Write(false, NdefConst.SerialKeyPrefix);
                Write(false, serialKey);
            }
            if (scalar != null)
            {
                bool isMultiLine = false;
                foreach (var s in SplitTextIntoLines(scalar))
                {
                    if (s != null)
                    {
                        if (isMultiLine)
                            Write(true, "");
                        else
                            Write(false, " ");
                        if (isMultiLine || s.Length == 0 || (s.Length > 0 &&
                            (Char.IsWhiteSpace(s[0]) || s[0] == NdefConst.QuotationMarker ||
                            s[0] == NdefConst.SerialKeyPrefix || s[0] == NdefConst.LogicalKeyPrefix)))
                            Write(false, NdefConst.QuotationMarker);
                        Write(false, s);
                        if (s.Length == 0 || (s.Length > 0 && Char.IsWhiteSpace(s[s.Length - 1])))
                            Write(false, NdefConst.QuotationMarker);
                        WriteLine(false, "");
                    }
                    else
                    {
                        isMultiLine = true;
                        WriteLine(false, "");
                    }
                }
            }
            else
                WriteLine(false, "");
        }

        private void GenerateObjectsFrom(INdefIterator iterator)
        {
            while (iterator.MoveToNextObjectHeader())
            {
                NdefObject o = iterator.CurrentObject;
                if (o.IsStartOfObject)
                    WriteObjectHeader(o.TypeName, o.Kind, o.SerialKey, o.LogicalKey);
                while (iterator.MoveToNextFieldOrListItem())
                {
                    NdefLine line = o.CurrentLine;
                    if (line.Field.Name != null)
                    {
                        if (line.Value.Kind == NdefValueKind.Object)
                        {
                            if (!line.Value.AsNestedObjectToDeserialize.IsEndOfObject)
                                WriteFieldNameForNestedObjectOrList(line.Field.Name);
                        }
                        else
                            WriteFieldNameAndValue(line.Field.Name, line.Value.ActualSerializableTypeName,
                                line.Value.AsScalar, line.Value.AsSerialKey, line.Value.AsLogicalKey);
                    }
                    else
                    {
                        if (line.Value.Kind == NdefValueKind.Object)
                        {
                            if (!line.Value.AsNestedObjectToDeserialize.IsEndOfObject)
                                WriteListItemForNestedObjectOrList(line.Value.ValueUpdateMode);
                        }
                        else
                            WriteListItem(line.Value.ValueUpdateMode, line.Value.ActualSerializableTypeName,
                                line.Value.AsScalar, line.Value.AsSerialKey, line.Value.AsLogicalKey);
                    }
                }
                if (o.IsEndOfObject)
                    WriteObjectFooter();
            }
        }

        private IEnumerable<string> SplitTextIntoLines(string text)
        {
            int k = 0;
            int i = text.IndexOfAny(LineFeedCharacters);
            while (i >= 0)
            {
                if (k == 0)
                    yield return null; // indicator of multi-line text
                bool is0D0A = i == k && i > 0 && text[i - 1] == '\x0D' && text[i] == '\x0A';
                if (!is0D0A)
                    yield return text.Substring(k, i - k);
                k = i + 1;
                i = text.IndexOfAny(LineFeedCharacters, k);
            }
            yield return text.Substring(k, text.Length - k);
        }

        private void Write(bool margin, string text)
        {
            if (margin)
                for (int i = 0; i < fMargin; ++i)
                    fOutput.Write("  ");
            fOutput.Write(text);
        }

        private void Write(bool margin, char text)
        {
            if (margin)
                for (int i = 0; i < fMargin; ++i)
                    fOutput.Write("  ");
            fOutput.Write(text);
        }

        private void WriteLine(bool margin, string text)
        {
            if (margin)
                for (int i = 0; i < fMargin; ++i)
                    fOutput.Write("  ");
            fOutput.WriteLine(text);
        }

        private void WriteLine(bool margin, char text)
        {
            if (margin)
                for (int i = 0; i < fMargin; ++i)
                    fOutput.Write("  ");
            fOutput.WriteLine(text);
        }
    }
}
