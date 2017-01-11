﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Nezaboodka.Ndef
{
    public abstract class AbstractNdefReader : INdefReader
    {
        private NdefDataSet BufferDataSet = new NdefDataSet();
        protected NdefObject BufferObject = new NdefObject(); // TODO: Eliminate protected
        private List<NdefObject> fObjectPath = new List<NdefObject>();
        private int fObjectLevel;

        // Public

        public NdefDataSet CurrentDataSet { get; private set; }
        public NdefObject CurrentObject { get; private set; }
        public NdefElement CurrentElement { get { return CurrentObject.CurrentElement; } }
        public Func<string, string> SubstituteObjectKey { get; set; }

        protected AbstractNdefReader()
        {
            fObjectPath.Add(null); // root
            CurrentDataSet = new NdefDataSet();
            CurrentObject = new NdefObject();
        }

        protected abstract bool MoveToNextLine();

        public virtual bool MoveToNextDataSet()
        {
            bool result = true;
            if (CurrentDataSet.IsEndOfDataSet)
                CurrentDataSet.Clear();
            if (IsBufferEmpty)
                result = MoveToNextLine();
            if (BufferDataSet.Header != null)
            {
                CurrentDataSet.Header = Take(ref BufferDataSet.Header);
                CurrentDataSet.IsStartOfDataSet = Take(ref BufferDataSet.IsStartOfDataSet);
            }
            return result;
        }

        public virtual bool MoveToNextObject()
        {
            if (CurrentObject.IsEndOfObject)
                CurrentObject = SwitchToParentObject();
            else if (fObjectPath.Count > fObjectLevel + 1 && fObjectPath[fObjectLevel + 1] != null && fObjectPath[fObjectLevel + 1].IsStartOfObject)
                CurrentObject = SwitchToNestedObject();
            if (IsBufferEmpty)
                MoveToNextLine();
            if (!BufferObject.Header.IsEmpty)
            {
                bool isListItem = Take(ref BufferObject.Header.IsListItem);
                string typeName = Take(ref BufferObject.Header.TypeName);
                NdefObjectKind kind = Take(ref BufferObject.Header.Kind);
                string number = Take(ref BufferObject.Header.Number);
                string key = Take(ref BufferObject.Header.Key);
                InitializeNestedObject(isListItem, typeName, kind, number, key);
                CurrentObject = SwitchToNestedObject();
            }
            return !CurrentObject.Header.IsEmpty;
        }

        public virtual bool MoveToNextElement()
        {
            bool result = CurrentObject.IsBackFromNestedObject;
            if (!result)
            {
                if (IsBufferEmpty)
                {
                    CurrentElement.Value.AsStream?.Close();
                    MoveToNextLine();
                }
                result = BufferObject.CurrentElement.Field.Name != null ||
                    !BufferObject.CurrentElement.Value.IsUndefined;
                if (result)
                {
                    CurrentObject.CurrentElement = Take(ref BufferObject.CurrentElement);
                    NdefObject nestedObject = CurrentObject.CurrentElement.Value.AsNestedObjectToDeserialize;
                    if (nestedObject != null)
                    {
                        BufferObject.Header.TypeName = nestedObject.Header.TypeName;
                        BufferObject.Header.Kind = nestedObject.Header.Kind;
                        BufferObject.Header.Number = nestedObject.Header.Number;
                        BufferObject.Header.Key = nestedObject.Header.Key;
                    }
                }
            }
            else
                CurrentObject.IsBackFromNestedObject = false;
            return result;
        }

        // Protected

        protected virtual bool IsBufferEmpty
        {
            get
            {
                return
                    !CurrentDataSet.IsEndOfDataSet &&
                    BufferDataSet.Header == null &&
                    !CurrentObject.IsEndOfObject &&
                    BufferObject.Header.IsEmpty &&
                    BufferObject.CurrentElement.Field.Name == null &&
                    BufferObject.CurrentElement.Value.IsUndefined;
            }
        }

        protected void PutDataSetStartToBuffer(string header)
        {
            if (BufferDataSet.Header != null)
                throw new FormatException("cannot read data set: buffer is full");
            if (!CurrentDataSet.IsEndOfDataSet && CurrentDataSet.Header != null)
            {
                if (!IsBufferEmpty)
                    throw new FormatException("cannot read value: buffer is full");
                throw new FormatException("data set cannot be nested");
            }
            else
            {
                BufferDataSet.Header = header;
                BufferDataSet.IsStartOfDataSet = true;
            }
        }

        protected void PutDataSetEndToBuffer()
        {
            if (CurrentDataSet.IsEndOfDataSet ||
                BufferObject.CurrentElement.Field.Name != null ||
                !BufferObject.CurrentElement.Value.IsUndefined)
                throw new FormatException("cannot write end of data set: buffer is full");
            CurrentDataSet.IsEndOfDataSet = true;
        }

        protected void PutObjectStartToBuffer(bool isListItem, string type, NdefObjectKind kind,
            string number, string key)
        {
            if (!BufferObject.Header.IsEmpty)
                throw new FormatException("cannot read object: buffer is full");
            if (SubstituteObjectKey != null)
                key = SubstituteObjectKey(key);
            if (!CurrentObject.IsEndOfObject && !CurrentObject.Header.IsEmpty)
            {
                if (!BufferObject.CurrentElement.Value.IsUndefined)
                    throw new FormatException("cannot read value: buffer is full");
                //if (isList && string.IsNullOrEmpty(type) && string.IsNullOrEmpty(key) &&
                //    string.IsNullOrEmpty(BufferObject.CurrentFieldOrListItem.FieldName))
                //    throw new FormatException("cannot read list item: current object is not a list");
                BufferObject.CurrentElement.Field.Kind = NdefFieldKind.SetOrAdd;
                BufferObject.CurrentElement.Value.AsNestedObjectToDeserialize =
                    InitializeNestedObject(isListItem, type, kind, number, key);
                if (BufferObject.CurrentElement.Value.Kind != NdefValueKind.Object)
                    throw new ArgumentException("cannot put value to the buffer as a nested object");
            }
            else
            {
                BufferObject.Header.TypeName = type;
                BufferObject.Header.Kind = kind;
                BufferObject.Header.Number = number;
                BufferObject.Header.Key = key;
            }
        }

        protected void PutObjectEndToBuffer()
        {
            if (CurrentObject.IsEndOfObject ||
                BufferObject.CurrentElement.Field.Name != null ||
                !BufferObject.CurrentElement.Value.IsUndefined)
                throw new FormatException("cannot write end of object: buffer is full");
            CurrentObject.IsEndOfObject = true;
        }

        protected void PutFieldNameToBuffer(string name)
        {
            if (BufferObject.CurrentElement.Field.Name != null)
                throw new FormatException("cannot read field: buffer is full");
            BufferObject.CurrentElement.Field.Number = -1; // not implemented yet
            BufferObject.CurrentElement.Field.Name = name;
        }

        protected void PutListItemToBuffer(bool removed)
        {
            if (CurrentObject.Header.Kind != NdefObjectKind.List)
                throw new FormatException("cannot read list item: current object is not a list");
            if (!BufferObject.CurrentElement.Value.IsUndefined)
                throw new FormatException("cannot read value: buffer is full");
            BufferObject.CurrentElement.Field.Kind = removed ? NdefFieldKind.Remove : NdefFieldKind.SetOrAdd;
            if (BufferObject.CurrentElement.Value.Kind == NdefValueKind.Object)
                throw new ArgumentException("cannot put object or list to the buffer as an item");
        }

        protected void PutValueToBuffer(string explicitTypeName, string scalar, string number, string key)
        {
            if (!BufferObject.CurrentElement.Value.IsUndefined)
                throw new FormatException("cannot read value: buffer is full");
            if (SubstituteObjectKey != null)
                key = SubstituteObjectKey(key);
            BufferObject.CurrentElement.Value.ActualSerializableTypeName = explicitTypeName;
            BufferObject.CurrentElement.Value.AsScalar = scalar;
            BufferObject.CurrentElement.Value.AsObjectNumber = number;
            BufferObject.CurrentElement.Value.AsObjectKey = key;
            if (BufferObject.CurrentElement.Value.Kind == NdefValueKind.Object)
                throw new ArgumentException("cannot put object or list to the buffer");
        }

        protected void PutBinaryDataToBuffer(Stream binaryStream)
        {
            BufferObject.CurrentElement.Value.AsStream = binaryStream;
        }

        protected void PutCommentToBuffer(string comment)
        {
            BufferObject.CurrentElement.Comment = comment;
        }

        // Internal

        private NdefObject InitializeNestedObject(bool isListItem, string typeName, NdefObjectKind kind,
            string number, string key)
        {
            while (fObjectPath.Count <= fObjectLevel + 1)
                fObjectPath.Add(null);
            var result = new NdefObject();
            result.Header.IsListItem = isListItem;
            result.Header.TypeName = typeName;
            result.Header.Kind = kind;
            result.Header.Number = number;
            result.Header.Key = key;
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

