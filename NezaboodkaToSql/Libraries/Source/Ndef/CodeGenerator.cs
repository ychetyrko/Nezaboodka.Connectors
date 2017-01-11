using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Nezaboodka.Ndef
{
    public class CodeGenerator
    {
        private AssemblyBuilder fAssemblyBuilder;
        private ModuleBuilder fModuleBuilder;

        public CodeGenerator(AssemblyName assemblyName)
        {
            // Use Collectible Assemblies for Dynamic Type Generation
            // http://msdn.microsoft.com/en-us/library/dd554932(v=vs.110).aspx

#if !DEBUG
            fAssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                assemblyName, AssemblyBuilderAccess.RunAndCollect);
            fModuleBuilder = fAssemblyBuilder.DefineDynamicModule(assemblyName.Name);
#else
            // Разкомментировать для сохранения примера сгенерированной сборки на диск.
            //Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            //string dir = System.IO.Path.GetDirectoryName(assembly.Location);
            //string name = "CodeGeneratedFormatters.dll";
            //string path = System.IO.Path.Combine(dir, name);
            //if (!System.IO.File.Exists(path) ||
            //    System.IO.File.GetLastWriteTimeUtc(path) < System.IO.File.GetLastWriteTimeUtc(assembly.Location))
            //{
            //    fAssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
            //        new AssemblyName(name), AssemblyBuilderAccess.RunAndSave, dir);
            //    fModuleBuilder = fAssemblyBuilder.DefineDynamicModule(name, name);
            //}
            //else
            //{
                fAssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                    assemblyName, AssemblyBuilderAccess.RunAndCollect);
                fModuleBuilder = fAssemblyBuilder.DefineDynamicModule(assemblyName.Name);
            //}
#endif
        }

        public Type GenerateFieldsAccessors(INdefTypeBinder typeBinder, Type type, IEnumerable<FieldInfo> fields)
        {
            Type[] fieldTypes = fields.Select((FieldInfo x) => x.FieldType).Distinct().ToArray();
            var formatters = new Dictionary<Type, FieldInfo>();
            // Object formatter type, fields accessors and constructor
            TypeBuilder formatterTypeBuilder = fModuleBuilder.DefineType(
                fModuleBuilder.Assembly.GetName().Name + '.' + type.FullName, TypeAttributes.Public);
            for (int i = 0; i < fieldTypes.Length; i++)
            {
                Type t = fieldTypes[i];
                Type formatterType = typeBinder.LookupFormatterBase(t).GetType();
                FieldInfo formatter = formatterTypeBuilder.DefineField("as_" + t.Name, formatterType, FieldAttributes.Private);
                formatters.Add(t, formatter);
            }
            // Constuctor, field getters and setters
            ConstructorBuilder constructor = GenerateConstructor(formatterTypeBuilder, formatters);
            foreach (FieldInfo x in fields)
            {
                MethodBuilder getter = GenerateFieldGetter(formatterTypeBuilder, type, x, formatters);
                MethodBuilder setter = GenerateFieldSetter(formatterTypeBuilder, type, x, formatters);
            }
            return formatterTypeBuilder.CreateType();
        }

        public Assembly GetGeneratedAssembly()
        {
            fModuleBuilder.CreateGlobalFunctions();
#if DEBUG
            try
            {
                if (!fModuleBuilder.IsTransient())
                    fAssemblyBuilder.Save("CodeGeneratedFormatters.dll.tmp");
            }
            catch (System.IO.IOException)
            {
                // Игнорируем ошибки ввода-вывода, поскольку из-за особенностей распараллеливания сборки
                // в Visual Studio иногда получается, что одновременно несколько процессов пытаются записать
                // на диск генерируемую сборку.
            }
#endif
            return fAssemblyBuilder;
        }

        public IEnumerable<NdefFieldAccessor<T>> GetFieldAccessors<T>(INdefTypeBinder typeBinder)
        {
            Type formatterType = fAssemblyBuilder.GetType(fAssemblyBuilder.GetName().Name + "." + typeof(T).FullName);
            if (formatterType != null)
            {
                object holder = Activator.CreateInstance(formatterType, typeBinder);
                return CreateCompiledFieldAccessors<T>(holder);
            }
            else
                return null;
        }

        // Internal

        private static ConstructorBuilder GenerateConstructor(TypeBuilder targetTypeBuilder,
            Dictionary<Type, FieldInfo> formatters)
        {
            MethodInfo getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle",
                new Type[] { typeof(RuntimeTypeHandle) });
            MethodInfo lookupFormatter = typeof(NdefUtils).GetMethod("LookupFormatter",
                new Type[] { typeof(INdefTypeBinder), typeof(Type) });
            ConstructorBuilder result = targetTypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[] { typeof(INdefTypeBinder) });
            ConstructorInfo baseConstructor = targetTypeBuilder.BaseType.GetConstructor(new Type[0]);
            ILGenerator il = result.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseConstructor);
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Nop);
            foreach (KeyValuePair<Type, FieldInfo> x in formatters)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldtoken, x.Key);
                il.Emit(OpCodes.Call, getTypeFromHandle);
                MethodInfo lookupExactFormatter = lookupFormatter.MakeGenericMethod(x.Value.FieldType);
                il.Emit(OpCodes.Call, lookupExactFormatter);
                il.Emit(OpCodes.Stfld, x.Value);
            }
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ret);
            return result;
        }

        private static MethodBuilder GenerateFieldGetter(TypeBuilder targetTypeBuilder,
            Type declaringType, FieldInfo fieldInfo, Dictionary<Type, FieldInfo> formatters)
        {
            // public NdefValue MyField(MyObject obj)
            // {
            //     return as_IList.ToNdefValue(typeof(List<MyObject>), obj.MyField);
            // }
            MethodBuilder result = targetTypeBuilder.DefineMethod(fieldInfo.Name,
                MethodAttributes.Public, typeof(NdefValue), new Type[] { declaringType });
            MethodInfo getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle",
                new Type[] { typeof(RuntimeTypeHandle) });
            FieldInfo formatterField = formatters[fieldInfo.FieldType];
            MethodInfo toNdefValue = formatterField.FieldType.GetMethod("ToNdefValue");
            ILGenerator il = result.GetILGenerator();
            il.DeclareLocal(typeof(NdefValue));
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, formatterField);
            il.Emit(OpCodes.Ldtoken, fieldInfo.FieldType);
            il.Emit(OpCodes.Call, getTypeFromHandle);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldfld, fieldInfo);
            il.Emit(OpCodes.Callvirt, toNdefValue);
            il.Emit(OpCodes.Stloc_0);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ret);
            return result;
        }

        private static MethodBuilder GenerateFieldSetter(TypeBuilder targetTypeBuilder,
            Type declaringType, FieldInfo fieldInfo, Dictionary<Type, FieldInfo> formatters)
        {
            // public void MyField(MyObject obj, NdefValue value)
            // {
            //     obj.MyField = as_IList.FromNdefValue(typeof(List<DbObject>), value);
            // }
            MethodBuilder result = targetTypeBuilder.DefineMethod(fieldInfo.Name, MethodAttributes.Public,
                typeof(void), new Type[] { declaringType, typeof(NdefValue) });
            MethodInfo getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle",
                new Type[] { typeof(RuntimeTypeHandle) });
            FieldInfo formatterField = formatters[fieldInfo.FieldType];
            MethodInfo fromNdefValue = formatterField.FieldType.GetMethod("FromNdefValue");
            ILGenerator il = result.GetILGenerator();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, formatterField);
            il.Emit(OpCodes.Ldtoken, fieldInfo.FieldType);
            il.Emit(OpCodes.Call, getTypeFromHandle);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Callvirt, fromNdefValue);
            il.Emit(OpCodes.Stfld, fieldInfo);
            il.Emit(OpCodes.Ret);
            return result;
        }

        private static IEnumerable<NdefFieldAccessor<T>> CreateCompiledFieldAccessors<T>(
            object compiledFieldAccessors)
        {
            IEnumerable<string> names = compiledFieldAccessors.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where((MethodInfo x) => x.ReturnParameter.ParameterType == typeof(NdefValue))
                .Select((MethodInfo x) => x.Name);
            foreach (string name in names)
            {
                // Getter and setter have the same name and different parameters.
                // Getter: public NdefValue Age(User obj) { return as_int.ToNdefValue(obj.Age); }
                // Setter: public void Age(User obj, NdefValue value) { obj.Age = as_int.FromNdefValue(value); }
                Type t = compiledFieldAccessors.GetType();
                MethodInfo getMethodInfo = t.GetMethod(name, new Type[] { typeof(T) });
                MethodInfo setMethodInfo = t.GetMethod(name, new Type[] { typeof(T), typeof(NdefValue) });
                if (getMethodInfo != null && setMethodInfo != null)
                {
                    NdefFieldGetter<T> getter = Delegate.CreateDelegate(typeof(NdefFieldGetter<T>),
                        compiledFieldAccessors, getMethodInfo, false) as NdefFieldGetter<T>;
                    NdefFieldSetter<T> setter = Delegate.CreateDelegate(typeof(NdefFieldSetter<T>),
                        compiledFieldAccessors, setMethodInfo, false) as NdefFieldSetter<T>;
                    if (getter != null && setter != null)
                    {
                        var fieldAccessors = new NdefFieldAccessor<T>(name, getter, setter);
                        yield return fieldAccessors;
                    }
                }
            }
        }
    }
}
