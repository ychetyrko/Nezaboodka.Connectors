using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Nezaboodka.Ndef
{
    public delegate IEnumerable<MemberInfo> NdefMemberFilter(IEnumerable<MemberInfo> candidates);

    public class FormatterAssemblyGenerator
    {
        public FormatterAssemblyGenerator()
        {
        }

        public AssemblyBuilder GenerateNdefObjectFormattersAssembly(AssemblyName assemblyName,
            IEnumerable<NdefTypeInfo> types, INdefTypeBinder typeBinder)
        {
            // Use Collectible Assemblies for Dynamic Type Generation
            // http://msdn.microsoft.com/en-us/library/dd554932(v=vs.110).aspx
            AssemblyBuilder result = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName,
                AssemblyBuilderAccess.RunAndCollect);
            ModuleBuilder module = result.DefineDynamicModule(assemblyName.Name);
            foreach (NdefTypeInfo ndefTypeInfo in types)
            {
                FieldInfo[] fields = ndefTypeInfo.SystemType.GetFields()
                    .Where((FieldInfo x) => x.DeclaringType.Name != "DbObject").ToArray(); // TODO: Eliminate temporary solution
                Type[] fieldTypes = fields.Select((FieldInfo x) => x.FieldType).Distinct().ToArray();
                var formatters = new Dictionary<Type,FieldInfo>();
                // Object formatter type, fields accessors and constructor
                TypeBuilder formatterTypeBuilder = module.DefineType(
                    module.Assembly.GetName().Name + '.' + ndefTypeInfo.SystemType.FullName, TypeAttributes.Public);
                for (int i = 0; i < fieldTypes.Length; i++)
                {
                    Type t = fieldTypes[i];
                    Type formatterType = typeof(INdefValueFormatter<>).MakeGenericType(t);
                    FieldInfo formatter = formatterTypeBuilder.DefineField("as_" + t.Name, formatterType, FieldAttributes.Private);
                    formatters.Add(t, formatter);
                }
                // Constuctor, field getters and setters
                ConstructorBuilder constructor = GenerateConstructor(formatterTypeBuilder, formatters);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo f = fields[i];
                    MethodBuilder getter = GenerateFieldGetter(formatterTypeBuilder,
                        ndefTypeInfo.SystemType, f, formatters);
                    MethodBuilder setter = GenerateFieldSetter(formatterTypeBuilder,
                        ndefTypeInfo.SystemType, f, formatters);
                }
                formatterTypeBuilder.CreateType();
            }
            module.CreateGlobalFunctions();
            return result;
        }

        private ConstructorBuilder GenerateConstructor(TypeBuilder targetTypeBuilder,
            Dictionary<Type, FieldInfo> formatters)
        {
            ConstructorBuilder result = targetTypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[] { typeof(INdefTypeBinder) });
            ConstructorInfo baseConstructor = targetTypeBuilder.BaseType.GetConstructor(new Type[0]);
            ILGenerator il = result.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseConstructor);
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Nop);
            foreach (KeyValuePair<Type, FieldInfo> x in formatters)
            {
                MethodInfo lookupFormatter = typeof(NdefUtils).GetMethod("LookupFormatter",
                    new Type[] { typeof(INdefTypeBinder) });
                lookupFormatter = lookupFormatter.MakeGenericMethod(x.Key);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, lookupFormatter);
                il.Emit(OpCodes.Stfld, x.Value);
            }
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ret);
            return result;
        }

        private MethodBuilder GenerateFieldGetter(TypeBuilder targetTypeBuilder,
            Type declaringType, FieldInfo fieldInfo, Dictionary<Type, FieldInfo> formatters)
        {
            // public NdefValue MyField(MyObject obj)
            // {
            //     return as_long.ToNdefValue(obj.MyField);
            // }
            MethodBuilder result = targetTypeBuilder.DefineMethod(fieldInfo.Name,
                MethodAttributes.Public, typeof(NdefValue), new Type[] { declaringType });
            MethodInfo toNdefValue = typeof(NdefUtils).GetMethod("ToNdefValue");
            toNdefValue = toNdefValue.MakeGenericMethod(fieldInfo.FieldType);
            ILGenerator il = result.GetILGenerator();
            il.DeclareLocal(typeof(NdefValue));
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, formatters[fieldInfo.FieldType]);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldfld, fieldInfo);
            il.Emit(OpCodes.Call, toNdefValue);
            il.Emit(OpCodes.Stloc_0);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ret);
            return result;
        }

        private MethodBuilder GenerateFieldSetter(TypeBuilder targetTypeBuilder,
            Type declaringType, FieldInfo fieldInfo, Dictionary<Type, FieldInfo> formatters)
        {
            // public void MyField(MyObject obj, NdefValue value)
            // {
            //     obj.MyField = as_long.FromNdefValue(value);
            // }
            MethodBuilder result = targetTypeBuilder.DefineMethod(fieldInfo.Name, MethodAttributes.Public,
                typeof(void), new Type[] { declaringType, typeof(NdefValue) });
            MethodInfo fromNdefValue = typeof(NdefUtils).GetMethod("FromNdefValue");
            fromNdefValue = fromNdefValue.MakeGenericMethod(fieldInfo.FieldType);
            ILGenerator il = result.GetILGenerator();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, formatters[fieldInfo.FieldType]);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, fromNdefValue);
            il.Emit(OpCodes.Stfld, fieldInfo);
            il.Emit(OpCodes.Ret);
            return result;
        }
    }
}
