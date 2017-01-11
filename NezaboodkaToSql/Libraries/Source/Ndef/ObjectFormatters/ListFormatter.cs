using System;
using System.Collections;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public class ListFormatter : AbstractObjectFormatter<IList>
    {
        public INdefTypeBinder TypeBinder { get; private set; }

        public ListFormatter(Type formalType) : base()
        {
            FormalType = formalType;
            SerializableTypeName = null;
        }

        public override void Initialize(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            base.Initialize(typeBinder, codegen);
            TypeBinder = typeBinder;
        }

        public override NdefValue ToNdefValue(Type formalType, IList value)
        {
            NdefValue result;
            if (value != null && value.Count > 0)
            {
                result = new NdefValue() { AsNestedObjectToSerialize = value };
                Type actualType = value.GetType();
                result.ActualSerializableTypeName =
                    EmitSerializableTypeName(formalType, actualType, actualType.IsArray ? value.Count : -1);
            }
            else
                result = NdefValue.NullValue;
            return result;
        }

        public override IList FromNdefValue(Type formalType, NdefValue value)
        {
            IList result;
            if (formalType.IsArray)
            {
                IList list = (IList)value.AsNestedObjectToDeserialize.DeserializedInstance;
                var t = (Array)Activator.CreateInstance(formalType, list.Count);
                list.CopyTo(t, 0);
                result = t;
            }
            else
                result = (IList)value.AsNestedObjectToDeserialize.DeserializedInstance;
            return result;
        }

        public override IEnumerable<NdefElement> ToNdefElements(IList obj, int[] fieldNumbers)
        {
            Type elementType = NdefUtils.GetElementType(obj.GetType(), typeof(object));
            var f = TypeBinder.LookupTypeInfoByType(elementType).Formatter.Boxed;
            var el = new NdefElement();
            foreach (object x in obj)
            {
                el.Value = f.ToNdefValue(elementType, x);
                if (!el.Value.IsUndefined)
                    yield return el;
            }
        }

        public override IList CreateObjectInstance(Type formalType, NdefObjectHeader objectHeader)
        {
            // Parse
            int length;
            string elementTypeName;
            Type elementType;
            ParseSerializableTypeName(objectHeader.TypeName, out elementTypeName, out length);
            if (elementTypeName != null)
                elementType = TypeBinder.LookupTypeInfoByName(elementTypeName).SystemType;
            else
                elementType = NdefUtils.GetElementType(formalType, TypeBinder.RootTypeForObjectsWithKey);
            // Determine type of list to create
            Type type;
            if (formalType.IsArray)
                type = typeof(NdefArrayBuffer<>).MakeGenericType(elementType);
            else if (formalType.IsInterface || formalType.IsAbstract || formalType == typeof(object))
                type = TypeBinder.PreferredListType.MakeGenericType(elementType);
            else if (formalType.IsClass && !formalType.IsAbstract && !formalType.IsGenericTypeDefinition)
                type = formalType;
            else if (formalType.IsGenericTypeDefinition)
                type = formalType.MakeGenericType(elementType);
            else
                throw new NotSupportedException("list type cennot be determined");
            // Create instance
            object result;
            if (length < 0)
                result = Activator.CreateInstance(type);
            else
                result = Activator.CreateInstance(type, length);
            return (IList)result;
        }

        public override void FromNdefElements(IList obj, IEnumerable<NdefElement> elements)
        {
            Type elementType = NdefUtils.GetElementType(obj.GetType(), typeof(object));
            var f = TypeBinder.LookupTypeInfoByType(elementType).Formatter.Boxed;
            foreach (NdefElement x in elements)
            {
                object t = f.FromNdefValue(elementType, x.Value);
                obj.Add(t);
            }
        }

        private void ParseSerializableTypeName(string typeName, out string elementTypeName, out int length)
        {
            int j = typeName.LastIndexOf(NdefConst.ListTypeBraces[1]); // ]
            int i = typeName.LastIndexOf(NdefConst.ListTypeBraces[0], j); // [
            length = -1; // unknown yet
            if (i + 1 < j) // [n] - fixed size array
                length = int.Parse(typeName.Substring(i + 1, j - i - 1));
            if (i > 0) // T[]
                elementTypeName = typeName.Substring(0, i);
            else if (i == 0) // []
                elementTypeName = null;
            else
                throw new FormatException(string.Format("not a list type: {0}", typeName));
        }

        private string EmitSerializableTypeName(Type formalType, Type actualType, int length)
        {
            string result;
            // Нетипизированный массив - это массив, для которого при сериализации
            // тип данных не указывается. Такой массив может иметь разное физическое
            // представление на сервере и на клиенте. Например, на сервере массив
            // может существовать как DbObject[], а на клиенте - как List<User>/List<DbDynamic>.
            // Массив сериализуется как нетипизированный, если формальный тип элемента
            // совпадает с фактическим, либо если фактический тип элемента - это DbObject
            // (или же любой его наследник). В качестве признака нетипизированного массива
            // используется специальное имя "[]" (NdefConst.ListTypeBraces).
            Type formalElementType = NdefUtils.TryGetElementType(formalType);
            Type actualElementType = NdefUtils.TryGetElementType(actualType);
            if (formalElementType != actualElementType &&
                !TypeBinder.RootTypeForObjectsWithKey.IsAssignableFrom(actualElementType))
            {
                var f = TypeBinder.LookupTypeInfoByType(actualElementType).Formatter;
                if (actualType.IsArray)
                    result = string.Format(NdefConst.FixedArrayTypeFormat, f.SerializableTypeName, length);
                else
                    result = f.SerializableTypeName + NdefConst.ListTypeBraces;
            }
            else
                result = NdefConst.ListTypeBraces;
            return result;
        }
    }

    public class NdefArrayBuffer<T> : List<T>
    {
        public NdefArrayBuffer() : base()
        {
        }

        public NdefArrayBuffer(int capacity) : base(capacity)
        {
        }
    }
}
