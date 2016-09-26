using System;
using System.Collections.Generic;
using System.IO;

namespace Nezaboodka.Ndef
{
    public abstract class NdefIterator : INdefIterator
    {
        // Fields
        private NdefBlock BufferBlock = new NdefBlock();
        private List<NdefBlock> fBlockStack = new List<NdefBlock>();
        private int fCurrentBlockLevel;
        protected NdefObject BufferObject = new NdefObject(); // TODO: Eliminate protected
        private List<NdefObject> fObjectPath = new List<NdefObject>();
        private int fObjectLevel;

        // Public

        public NdefBlock CurrentBlock { get; private set; }
        public NdefObject CurrentObject { get; private set; }
        public Func<string, string> SubstituteLogicalKey { get; set; }

        protected NdefIterator()
        {
            fBlockStack.Add(new NdefBlock());
            CurrentBlock = fBlockStack[fCurrentBlockLevel];
            fObjectPath.Add(null); // root
            CurrentObject = new NdefObject();
        }

        public virtual bool MoveToNextBlockHeader()
        {
            bool result = true;
            if (CurrentBlock.IsEndOfBlock)
                CurrentBlock = SwitchToParentBlock();
            else if (fBlockStack.Count > fCurrentBlockLevel + 1 && fBlockStack[fCurrentBlockLevel + 1].IsStartOfBlock)
                CurrentBlock = SwitchToNestedBlock();
            if (IsBufferEmpty)
                result = NeedMoreData();
            if (BufferBlock.Header != null)
            {
                CurrentBlock.Header = Take(ref BufferBlock.Header);
                CurrentBlock.IsStartOfBlock = true;
            }
            return result;
        }

        public virtual bool MoveToNextObjectHeader()
        {
            if (CurrentObject.IsEndOfObject)
                CurrentObject = SwitchToParentObject();
            else if (fObjectPath.Count > fObjectLevel + 1 && fObjectPath[fObjectLevel + 1] != null && fObjectPath[fObjectLevel + 1].IsStartOfObject)
                CurrentObject = SwitchToNestedObject();
            if (IsBufferEmpty)
                NeedMoreData();
            if (!BufferObject.IsHeaderEmpty)
            {
                bool isListItem = Take(ref BufferObject.IsListItem);
                string typeName = Take(ref BufferObject.TypeName);
                NdefObjectKind kind = Take(ref BufferObject.Kind);
                string serialKey = Take(ref BufferObject.SerialKey);
                string logicalKey = Take(ref BufferObject.LogicalKey);
                InitializeNestedObject(isListItem, typeName, kind, serialKey, logicalKey);
                CurrentObject = SwitchToNestedObject();
            }
            return !CurrentObject.IsHeaderEmpty;
        }

        public virtual bool MoveToNextFieldOrListItem()
        {
            bool result = CurrentObject.IsBackFromNestedObject;
            if (!result)
            {
                if (IsBufferEmpty)
                    NeedMoreData();
                result = BufferObject.CurrentLine.Field.Name != null ||
                    !BufferObject.CurrentLine.Value.IsUndefined;
                if (result)
                {
                    CurrentObject.CurrentLine = Take(ref BufferObject.CurrentLine);
                    NdefObject nestedObject = CurrentObject.CurrentLine.Value.AsNestedObjectToDeserialize;
                    if (nestedObject != null)
                    {
                        BufferObject.TypeName = nestedObject.TypeName;
                        BufferObject.Kind = nestedObject.Kind;
                        BufferObject.SerialKey = nestedObject.SerialKey;
                        BufferObject.LogicalKey = nestedObject.LogicalKey;
                    }
                }
            }
            else
                CurrentObject.IsBackFromNestedObject = false;
            return result;
        }

        public virtual Stream OpenReadBlockFooter()
        {
            return null; // not implemented yet
        }

        protected virtual bool IsBufferEmpty
        {
            get
            {
                return
                    !CurrentBlock.IsEndOfBlock &&
                    BufferBlock.Header == null &&
                    !CurrentObject.IsEndOfObject &&
                    BufferObject.IsHeaderEmpty &&
                    BufferObject.CurrentLine.Field.Name == null &&
                    BufferObject.CurrentLine.Value.IsUndefined;
            }
        }

        protected abstract bool NeedMoreData();

        protected void PutBlockStartToBuffer(string header)
        {
            if (BufferBlock.Header != null)
                throw new FormatException("cannot read block: buffer is full");
            if (!CurrentBlock.IsEndOfBlock && CurrentBlock.Header != null)
            {
                if (!IsBufferEmpty)
                    throw new FormatException("cannot read value: buffer is full");
                BufferBlock.Header = InitializeNestedBlock(header).Header;
            }
            else
                BufferBlock.Header = header;
        }

        protected void PutBlockEndToBuffer()
        {
            if (CurrentBlock.IsEndOfBlock ||
                BufferObject.CurrentLine.Field.Name != null ||
                !BufferObject.CurrentLine.Value.IsUndefined)
                throw new FormatException("cannot write end of block: buffer is full");
            CurrentBlock.IsEndOfBlock = true;
        }

        protected void PutObjectStartToBuffer(bool isListItem, string type, NdefObjectKind kind,
            string serialKey, string logicalKey)
        {
            if (!BufferObject.IsHeaderEmpty)
                throw new FormatException("cannot read object: buffer is full");
            if (SubstituteLogicalKey != null)
                logicalKey = SubstituteLogicalKey(logicalKey);
            if (!CurrentObject.IsEndOfObject && !CurrentObject.IsHeaderEmpty)
            {
                if (!BufferObject.CurrentLine.Value.IsUndefined)
                    throw new FormatException("cannot read value: buffer is full");
                //if (isList && string.IsNullOrEmpty(type) && string.IsNullOrEmpty(key) &&
                //    string.IsNullOrEmpty(BufferObject.CurrentFieldOrListItem.FieldName))
                //    throw new FormatException("cannot read list item: current object is not a list");
                BufferObject.CurrentLine.Value.ValueUpdateMode = NdefValueUpdateMode.SetOrAddToList;
                BufferObject.CurrentLine.Value.AsNestedObjectToDeserialize =
                    InitializeNestedObject(isListItem, type, kind, serialKey, logicalKey);
                if (BufferObject.CurrentLine.Value.Kind != NdefValueKind.Object)
                    throw new ArgumentException("cannot put value to the buffer as a nested object");
            }
            else
            {
                BufferObject.TypeName = type;
                BufferObject.Kind = kind;
                BufferObject.SerialKey = serialKey;
                BufferObject.LogicalKey = logicalKey;
            }
        }

        protected void PutObjectEndToBuffer()
        {
            if (CurrentObject.IsEndOfObject ||
                BufferObject.CurrentLine.Field.Name != null ||
                !BufferObject.CurrentLine.Value.IsUndefined)
                throw new FormatException("cannot write end of object: buffer is full");
            CurrentObject.IsEndOfObject = true;
        }

        protected void PutFieldToBuffer(string name)
        {
            if (BufferObject.CurrentLine.Field.Name != null)
                throw new FormatException("cannot read field: buffer is full");
            BufferObject.CurrentLine.Field.Number = -1; // not implemented yet
            BufferObject.CurrentLine.Field.Name = name;
        }

        protected void PutValueToBuffer(string explicitTypeName, string scalar, string serialKey, string logicalKey)
        {
            if (!BufferObject.CurrentLine.Value.IsUndefined)
                throw new FormatException("cannot read value: buffer is full");
            if (SubstituteLogicalKey != null)
                logicalKey = SubstituteLogicalKey(logicalKey);
            BufferObject.CurrentLine.Value.ValueUpdateMode = NdefValueUpdateMode.SetOrAddToList;
            BufferObject.CurrentLine.Value.ActualSerializableTypeName = explicitTypeName;
            BufferObject.CurrentLine.Value.AsScalar = scalar;
            BufferObject.CurrentLine.Value.AsSerialKey = serialKey;
            BufferObject.CurrentLine.Value.AsLogicalKey = logicalKey;
            if (BufferObject.CurrentLine.Value.Kind == NdefValueKind.Object)
                throw new ArgumentException("cannot put object or list to the buffer");
        }

        protected void PutListItemToBuffer(NdefValueUpdateMode valueUpdateMode, string explicitTypeName,
            string scalar, string serialKey, string logicalKey)
        {
            if (CurrentObject.Kind != NdefObjectKind.List)
                throw new FormatException("cannot read list item: current object is not a list");
            if (!BufferObject.CurrentLine.Value.IsUndefined)
                throw new FormatException("cannot read value: buffer is full");
            if (SubstituteLogicalKey != null)
                logicalKey = SubstituteLogicalKey(logicalKey);
            BufferObject.CurrentLine.Value.ValueUpdateMode = valueUpdateMode;
            BufferObject.CurrentLine.Value.ActualSerializableTypeName = explicitTypeName;
            BufferObject.CurrentLine.Value.AsScalar = scalar;
            BufferObject.CurrentLine.Value.AsSerialKey = serialKey;
            BufferObject.CurrentLine.Value.AsLogicalKey = logicalKey;
            if (BufferObject.CurrentLine.Value.Kind == NdefValueKind.Object)
                throw new ArgumentException("cannot put object or list to the buffer as an item");
        }

        // Internal

        private NdefBlock InitializeNestedBlock(string blockHeader)
        {
            while (fBlockStack.Count <= fCurrentBlockLevel + 1)
                fBlockStack.Add(new NdefBlock());
            NdefBlock result = fBlockStack[fCurrentBlockLevel + 1];
            result.Header = blockHeader;
            result.Parent = fBlockStack[fCurrentBlockLevel]; // CurrentObject
            result.IsStartOfBlock = true;
            return result;
        }

        private NdefBlock SwitchToNestedBlock()
        {
            BufferBlock.Clear();
            fBlockStack[fCurrentBlockLevel].IsStartOfBlock = false;
            NdefBlock result = fBlockStack[++fCurrentBlockLevel];
            return result;
        }

        private NdefBlock SwitchToParentBlock()
        {
            fBlockStack[fCurrentBlockLevel].Clear(); // clear block for further reuse
            if (fCurrentBlockLevel > 0)
                fCurrentBlockLevel -= 1;
            return fBlockStack[fCurrentBlockLevel];
        }

        private NdefObject InitializeNestedObject(bool isListItem, string typeName, NdefObjectKind kind,
            string serialKey, string logicalKey)
        {
            while (fObjectPath.Count <= fObjectLevel + 1)
                fObjectPath.Add(null);
            var result = new NdefObject();
            result.IsListItem = isListItem;
            result.TypeName = typeName;
            result.Kind = kind;
            result.SerialKey = serialKey;
            result.LogicalKey = logicalKey;
            result.Parent = fObjectPath[fObjectLevel]; // CurrentObject
            result.IsStartOfObject = true;
            fObjectPath[fObjectLevel + 1] = result;
            return result;
        }

        private NdefObject SwitchToNestedObject()
        {
            BufferObject.Clear();
            if (fObjectLevel > 0)
                fObjectPath[fObjectLevel].IsStartOfObject = false;
            fObjectLevel++;
            NdefObject result = fObjectPath[fObjectLevel];
            return result;
        }

        private NdefObject SwitchToParentObject()
        {
            fObjectPath[fObjectLevel] = null;
            fObjectLevel--;
            NdefObject result = fObjectPath[fObjectLevel];
            if (result != null)
                result.IsBackFromNestedObject = true;
            else
                result = new NdefObject();
            return result;
        }

        private T Take<T>(ref T variable)
        {
            var result = variable;
            variable = default(T);
            return result;
        }
    }
}
