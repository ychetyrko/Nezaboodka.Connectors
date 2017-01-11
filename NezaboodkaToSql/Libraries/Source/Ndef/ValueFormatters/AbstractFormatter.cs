using System;
using System.Collections.Generic;

namespace Nezaboodka.Ndef
{
    public abstract class AbstractFormatter<T> : INdefFormatter<T>
    {
        public INdefFormatter<object> Boxed { get; protected set; }
        public Type FormalType { get; protected set; }
        public string SerializableTypeName { get; protected set; }

        public abstract T FromNdefValue(Type formalType, NdefValue value);
        public abstract NdefValue ToNdefValue(Type formalType, T value);
        public abstract void FromNdefElements(T obj, IEnumerable<NdefElement> elements);
        public abstract IEnumerable<NdefElement> ToNdefElements(T obj, int[] fieldNumbers);

        public AbstractFormatter()
        {
            FormalType = typeof(T);
            SerializableTypeName = typeof(T).FullName;
            Boxed = new BoxedFormatter<T>(this);
        }

        public virtual void Initialize(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            // ничего не делать
        }

        public virtual void Configure(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            // ничего не делать
        }

        public virtual T CreateObjectInstance(Type formalType, NdefObjectHeader objectHeader)
        {
            return Activator.CreateInstance<T>();
        }
    }

    public class BoxedFormatter<T> : INdefFormatter<object>
    {
        public INdefFormatter<T> Unboxed { get; private set; }
        public Type FormalType { get { return Unboxed.FormalType; } }
        public string SerializableTypeName { get { return Unboxed.SerializableTypeName; } }
        public INdefFormatter<object> Boxed { get { return this; } }

        public BoxedFormatter(INdefFormatter<T> unboxed)
        {
            Unboxed = unboxed;
        }

        public virtual void Initialize(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            Unboxed.Initialize(typeBinder, codegen);
        }

        public virtual void Configure(INdefTypeBinder typeBinder, CodeGenerator codegen)
        {
            Unboxed.Configure(typeBinder, codegen);
        }

        public virtual NdefValue ToNdefValue(Type formalType, object value)
        {
            if (value != null)
                return Unboxed.ToNdefValue(formalType, (T)value);
            else
                return NdefValue.NullValue;
        }

        public virtual object FromNdefValue(Type formalType, NdefValue value)
        {
            if (!value.IsUndefined && !value.IsNull)
                return Unboxed.FromNdefValue(formalType, value);
            else
                return null;
        }

        public virtual IEnumerable<NdefElement> ToNdefElements(object obj, int[] fieldNumbers)
        {
            return Unboxed.ToNdefElements((T)obj, fieldNumbers);
        }

        public virtual void FromNdefElements(object obj, IEnumerable<NdefElement> elements)
        {
            Unboxed.FromNdefElements((T)obj, elements);
        }

        public virtual object CreateObjectInstance(Type formalType, NdefObjectHeader objectHeader)
        {
            return Unboxed.CreateObjectInstance(formalType, objectHeader);
        }
    }
}
