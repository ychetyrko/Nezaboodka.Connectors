using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CSharp;
using System.CodeDom.Compiler;

namespace Nezaboodka
{
    public class ClientAssemblyWriter
    {
        private StringBuilder fStringBuilder;
        private int fCurrentIndent;

        // Public

        public List<string> ImportedNamespaces { get; private set; }
        public List<string> ReferencedAssemblies { get; private set; }

        public ClientAssemblyWriter()
        {
            ImportedNamespaces = new List<string>() { "System", "System.Collections", "System.Collections.Generic",
                "Nezaboodka", "Nezaboodka.Ndef" };
            ReferencedAssemblies = new List<string>() { "System.dll", "Nezaboodka.Ndef.dll", "Nezaboodka.Client.dll" };
            fStringBuilder = new StringBuilder();
        }

        // Assembly Generation

        public Assembly GenerateInMemoryAssemblyForDatabaseSchema(string assemblyName, string programNamespace,
            DatabaseSchema databaseSchema, Type rootType)
        {
            string code = GenerateDatabaseAssemblySourceCode(databaseSchema, rootType, programNamespace);
            return GenerateInMemoryAssemblyFromSourceCode(assemblyName, code);
        }

        public Assembly GenerateInMemoryAssemblyForNdefObjectFormatters(string assemblyName, string programNamespace,
            IEnumerable<TypeDefinition> typeDefinitions)
        {
            string code = GenerateNdefObjectFormattersSourceCode(typeDefinitions, programNamespace);
            return GenerateInMemoryAssemblyFromSourceCode(assemblyName, code);
        }

        // Code Generation

        public string GenerateDatabaseAssemblySourceCode(DatabaseSchema databaseSchema, Type rootType,
            string programNamespace)
        {
            var typeSystem = new ClientTypeSystem(databaseSchema.TypeDefinitions); // do it for verification purpose
            ClearStringBuilder();
            WriteGeneratorNotice();
            WriteImportedNamespaces();
            StartNewLine();
            WriteNamespaceBegin(programNamespace);
            bool isFirstTime = true;
            foreach (TypeDefinition typeDef in databaseSchema.TypeDefinitions)
            {
                string baseClassName = typeDef.BaseTypeName;
                if (baseClassName == ClientTypeSystem.DbObjectTypeDefinition.TypeName)
                    baseClassName = rootType.Name;
                if (isFirstTime)
                    isFirstTime = false;
                else
                    StartNewLine();
                WriteClass(typeDef, baseClassName);
            }
            StartNewLine();
            WriteTypeSystemClass(databaseSchema, programNamespace);
            WriteCodeBlockEnd();
            StartNewLine();
            return fStringBuilder.ToString();
        }

        public string GenerateNdefObjectFormattersSourceCode(IEnumerable<TypeDefinition> typeDefinitions, string programNamespace)
        {
            bool isFirstType = true;
            ClearStringBuilder();
            WriteGeneratorNotice();
            WriteImportedNamespaces();
            StartNewLine();
            WriteNamespaceBegin(programNamespace);
            Dictionary<string, TypeDefinition> allTypes;
            allTypes = typeDefinitions.ToDictionary<TypeDefinition, string>((TypeDefinition x) => x.TypeName);
            allTypes.Add(typeof(DbObject).Name, null);
            foreach (TypeDefinition t in typeDefinitions)
            {
                if (isFirstType)
                    isFirstType = false;
                else
                    StartNewLine();
                WriteObjectFormatterClass(t, allTypes);
            }
            WriteCodeBlockEnd();
            StartNewLine();
            return fStringBuilder.ToString();
        }

        // Internal

        private Assembly GenerateInMemoryAssemblyFromSourceCode(string assemblyName, params string[] code)
        {
            Assembly result = null;
            using (CSharpCodeProvider provider = new CSharpCodeProvider())
            {
                CompilerParameters parameters = new CompilerParameters();
                parameters.OutputAssembly = assemblyName;
                parameters.GenerateInMemory = true;
                parameters.CompilerOptions = "/optimize";
                parameters.ReferencedAssemblies.AddRange(ReferencedAssemblies.ToArray());
                CompilerResults compilerResults = provider.CompileAssemblyFromSource(parameters, code);
                if (!compilerResults.Errors.HasErrors)
                    result = compilerResults.CompiledAssembly;
                else
                    throw new NezaboodkaException(compilerResults.Errors[0].ErrorText);
            }
            return result;
        }

        private void WriteClass(TypeDefinition typeDef, string baseClassName)
        {
            WriteClassBegin(typeDef.TypeName, baseClassName);
            foreach (FieldDefinition fieldDef in typeDef.FieldDefinitions)
            {
                ChangeIndent(+1);
                StartNewLine();
                WriteField(fieldDef);
                ChangeIndent(-1);
            }
            WriteClassEnd();
        }

        private void WriteObjectFormatterClass(TypeDefinition typeDef, Dictionary<string, TypeDefinition> allTypeDefs)
        {
            string formatterClassName = typeDef.TypeName + Const.gObjectFormatterClassNameSuffix;
            WriteInternalClassBegin(formatterClassName, null /*typeDef.BaseTypeName + Const.gObjectFormatterClassNameSuffix*/);
            ChangeIndent(+1);
            IEnumerable<FieldDefinition> fieldTypes = EnumerateFieldDefinitions(typeDef, allTypeDefs)
                .Select(fd => new {FieldTypeName = fd.FieldTypeName, IsList = fd.IsList}).Distinct()
                .Select(f => new FieldDefinition() {FieldTypeName = f.FieldTypeName, IsList = f.IsList});
            WriteFormattersDeclaration(fieldTypes);
            StartNewLine();
            WriteObjectFormatterClassConstructor(formatterClassName, fieldTypes);
            bool isFirstType = true;
            foreach (FieldDefinition f in EnumerateFieldDefinitions(typeDef, allTypeDefs))
            {
                if (isFirstType)
                    StartNewLine();
                else
                    isFirstType = false;
                WriteFieldGetter(f, typeDef.TypeName);
                StartNewLine();
                WriteFieldSetter(f, typeDef.TypeName);
            }
            StartNewLine();
            WriteNdefFieldNamesMethod(typeDef, allTypeDefs);
/*
            StartNewLine();
            WriteToNdefMethod(typeDef, allTypeDefs);
            StartNewLine();
            WriteFromNdefMethod(typeDef, allTypeDefs);
*/
            ChangeIndent(-1);
            WriteClassEnd();
        }

        private IEnumerable<FieldDefinition> EnumerateFieldDefinitions(
            TypeDefinition typeDef, Dictionary<string, TypeDefinition> allTypeDefs)
        {
            foreach (TypeDefinition t in GetInheritanceChain(typeDef, allTypeDefs))
                foreach (FieldDefinition f in t.FieldDefinitions)
                    yield return f;
        }

        private IEnumerable<TypeDefinition> GetInheritanceChain(
            TypeDefinition typeDef, Dictionary<string, TypeDefinition> allTypeDefs)
        {
            var result = new List<TypeDefinition>();
            while (typeDef != null)
            {
                result.Add(typeDef);
                if (typeDef.BaseTypeName != null)
                    typeDef = allTypeDefs[typeDef.BaseTypeName];
                else
                    typeDef = null;
            }
            result.Reverse();
            return result;
        }

        private void WriteFormattersDeclaration(IEnumerable<FieldDefinition> classDistincTypes)
        {
            //private INdefFormatter<string> fStringFormatter;
            foreach (FieldDefinition fieldDef in classDistincTypes)
            {
                WriteFromNewLine("private INdefFormatter<{0}> {1};", GetTypeName(fieldDef, false),
                    GetFormatterNameByType(fieldDef));
            }
        }

        private static string GetFormatterNameByType(FieldDefinition fieldDef)
        {
            return string.Format("as_{0}", GetTypeName(fieldDef, true));
        }

        //No need???????
        private static string GetTypeName(FieldDefinition fieldDef, bool isSimplified)
        {
            string result = fieldDef.FieldTypeName;
            if (Const.SystemTypeNameByNezaboodkaTypeName.ContainsKey(result))
                result = Const.SystemTypeNameByNezaboodkaTypeName[result];
            if (fieldDef.IsList)
            {
                string typeNameFormat = "List<{0}>";
                if (isSimplified)
                    typeNameFormat = "{0}List";
                result = string.Format(typeNameFormat, fieldDef.FieldTypeName);
            }
            return result;
        }

        private static string GetTypeName(Type fieldType, bool isSimplified)
        {
            string typeName = fieldType.Name;
            if (fieldType.IsGenericType)
            {
                /*                if (fieldType.GetGenericTypeDefinition() == typeof(IList<>))
                                    typeName = string.Format("IList<{0}>", fieldType.Name);*/
                string genericTypeName = fieldType.GetGenericTypeDefinition().Name;
                string typeNameFormat = "{0}<{1}>";
                string joinParamsWithString = ", ";
                if (isSimplified)
                {
                    typeNameFormat = "{1}{0}";
                    joinParamsWithString = "";
                }
                typeName = string.Format(typeNameFormat, genericTypeName.Substring(0, genericTypeName.IndexOf("`", 0)),
                    string.Join(joinParamsWithString, fieldType.GenericTypeArguments.Select(tArg => tArg.Name)));
            }
            return typeName;
        }

        private void WriteGeneratorNotice()
        {
            WriteLine("// This code was auto-generated by Nezaboodka.Tools.");
            //WriteLine("// Generated: " + DateTimeOffset.UtcNow.ToString("o") + ".");
            WriteLine("// Changes to this file may cause incorrect behavior");
            WriteLine("// and will be lost if the code is regenerated.");
        }

        private void WriteObjectFormatterClassConstructor(string formatterClassName,
            IEnumerable<FieldDefinition> classDistinctTypes)
        {
            //public UserSerializer(INdefTypeBinder typeBinder)
            //{
            //    fStringFormatter = (INdefFormatter<string>)typeBinder.LookupTypeInfo(typeof(string)).Formatter;
            //}
            WriteFromNewLine("public {0}(INdefTypeBinder typeBinder)", formatterClassName);
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            foreach (FieldDefinition fieldDef in classDistinctTypes)
            {
                WriteFromNewLine("{0} = typeBinder.LookupFormatter<INdefFormatter<{1}>>(typeof({1}));",
                    GetFormatterNameByType(fieldDef), GetTypeName(fieldDef, false));
            }
            ChangeIndent(-1);
            WriteCodeBlockEnd();
        }

        private void WriteFieldGetter(FieldDefinition fieldDef, string className)
        {
            //public NdefLine GetName(User obj)
            //{
            //    return new NdefLine() { FieldName = "Name", Value = fStringFormatter.ToNdefValue(typeof(string), obj.Name)};
            //}
            string fieldTypeName = fieldDef.IsList ? string.Format("List<{0}>", fieldDef.FieldTypeName) : fieldDef.FieldTypeName;
            WriteFromNewLine("public NdefValue {0}({1} obj)", fieldDef.FieldName, className);
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            //WriteFromNewLine("return new NdefLine() {{ FieldName = \"{0}\", Value = {1}.ToNdefValue(typeof({2}), obj.{0})}};",
            //    fieldInfo.Name, GetFormatterNameByType(fieldInfo.FieldType), GetTypeName(fieldInfo.FieldType, false));
            WriteFromNewLine("return {0}.ToNdefValue(typeof({1}), obj.{2});",
                GetFormatterNameByType(fieldDef), fieldTypeName, fieldDef.FieldName);
            ChangeIndent(-1);
            WriteCodeBlockEnd();
        }

        private void WriteFieldSetter(FieldDefinition fieldDef, string className)
        {
            //public void SetName(User obj, NdefLine line)
            //{
            //    obj.Name = fStringFormatter.FromNdefValue(typeof(string), line.Value);
            //}
            string fieldTypeName = fieldDef.IsList ? string.Format("List<{0}>", fieldDef.FieldTypeName) : fieldDef.FieldTypeName;
            WriteFromNewLine("public void {0}({1} obj, NdefValue value)", fieldDef.FieldName, className);
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            WriteFromNewLine("obj.{0} = {1}.FromNdefValue(typeof({2}), value);",
                fieldDef.FieldName, GetFormatterNameByType(fieldDef), fieldTypeName);
            ChangeIndent(-1);
            WriteCodeBlockEnd();
        }

        private void WriteNdefFieldNamesMethod(TypeDefinition typeDef, Dictionary<string, TypeDefinition> allTypeDefs)
        {
            WriteFromNewLine("public IEnumerable<string> {0}NdefFields()", typeDef.TypeName);
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            WriteFromNewLine("string[] names = ");
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            bool first = true;
            foreach (FieldDefinition fd in EnumerateFieldDefinitions(typeDef, allTypeDefs))
            {
                if (!first)
                    Write(",");
                WriteFromNewLine("\"{0}\"", fd.FieldName);
                first = false;
            }
            ChangeIndent(-1);
            WriteCodeBlockEnd();
            Write(";");
            WriteFromNewLine("return names;");
            ChangeIndent(-1);
            WriteCodeBlockEnd();
        }

        private void WriteToNdefMethod(TypeDefinition typeDef, Dictionary<string, TypeDefinition> allTypeDefs)
        {
            WriteFromNewLine("public IEnumerable<NdefLine> {0}ToNdef(object obj, int[] fieldNumbers)", typeDef.TypeName);
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            WriteFromNewLine("var o = ({0})obj;", typeDef.TypeName);
            WriteFromNewLine("for (int i = 0; i < fieldNumbers.Length; i++)");
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            WriteFromNewLine("switch (fieldNumbers[i])");
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            int i = 0;
            foreach (FieldDefinition fd in EnumerateFieldDefinitions(typeDef, allTypeDefs))
            {
                WriteFromNewLine("case {0} /* {2} */: yield return {1}.ToNdefLine(o.{2}, {0}); break;",
                    i, GetFormatterNameByType(fd), fd.FieldName);
                i++;
            }
            ChangeIndent(-1);
            WriteCodeBlockEnd();
            ChangeIndent(-1);
            WriteCodeBlockEnd();
            ChangeIndent(-1);
            WriteCodeBlockEnd();
        }

        private void WriteFromNdefMethod(TypeDefinition typeDef, Dictionary<string, TypeDefinition> allTypeDefs)
        {
            WriteFromNewLine("public void {0}FromNdef(object obj, IEnumerable<NdefLine> lines)", typeDef.TypeName);
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            WriteFromNewLine("var o = ({0})obj;", typeDef.TypeName);
            WriteFromNewLine("foreach (NdefLine line in lines)");
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            WriteFromNewLine("switch (line.Field.Number)");
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            int i = 0;
            foreach (FieldDefinition fd in EnumerateFieldDefinitions(typeDef, allTypeDefs))
            {
                WriteFromNewLine("case {0}: o.{1} = {2}.FromNdefValue(line.Value); break;",
                    i, fd.FieldName, GetFormatterNameByType(fd));
                i++;
            }
            ChangeIndent(-1);
            WriteCodeBlockEnd();
            ChangeIndent(-1);
            WriteCodeBlockEnd();
            ChangeIndent(-1);
            WriteCodeBlockEnd();
        }

        private void WriteTypeSystemClass(DatabaseSchema databaseSchema, string programNamespace)
        {
            WriteStaticClassBegin(Const.gTypeSystemClassName);
            ChangeIndent(+1);
            // Отключена генерирование массива TypeDefinition[] TypeDefinitions. 
            // N*DEF схема включается как ресурс и не отделим от генерируемого .CS файла 
            //WriteTypeDefinitions(databaseSchema);
            WriteFromNewLine("public static Type[] DefinedTypes =");
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            foreach (TypeDefinition type in databaseSchema.TypeDefinitions)
                WriteFromNewLine(string.Format("typeof({0}),", type.TypeName));
            ChangeIndent(-1);
            WriteCodeBlockEnd();
            Write(";");
            ChangeIndent(-1);
            WriteClassEnd();
        }

        private void WriteTypeDefinitions(DatabaseSchema databaseSchema)
        {
            WriteFromNewLine("public static TypeDefinition[] TypeDefinitions =");
            WriteCodeBlockBegin();
            ChangeIndent(+1);
            foreach (TypeDefinition type in databaseSchema.TypeDefinitions)
            {
                WriteFromNewLine("new TypeDefinition()");
                WriteCodeBlockBegin();
                ChangeIndent(+1);
                WriteFromNewLine(string.Format("TypeName = \"{0}\", BaseTypeName = \"{1}\",", type.TypeName, type.BaseTypeName));
                WriteFromNewLine("FieldDefinitions = new List<FieldDefinition>()");
                WriteCodeBlockBegin();
                ChangeIndent(+1);
                foreach (FieldDefinition field in type.FieldDefinitions)
                {
                    string isListStr = "";
                    if (field.IsList)
                        isListStr = ", IsList = true";
                    string backReferenceStr = "";
                    if (!string.IsNullOrEmpty(field.BackReferenceFieldName))
                        backReferenceStr = string.Format(", BackReferenceFieldName = \"{0}\"", field.BackReferenceFieldName);
                    WriteFromNewLine("new FieldDefinition()");
                    WriteCodeBlockBegin();
                    ChangeIndent(+1);
                    WriteFromNewLine(string.Format("FieldName = \"{0}\", FieldTypeName = \"{1}\"{2}{3}",
                        field.FieldName, field.FieldTypeName, isListStr, backReferenceStr));
                    ChangeIndent(-1);
                    WriteCodeBlockEnd();
                    Write(",");
                }
                ChangeIndent(-1);
                WriteCodeBlockEnd();
                ChangeIndent(-1);
                WriteCodeBlockEnd();
                Write(",");
            }
            ChangeIndent(-1);
            WriteCodeBlockEnd();
            Write(";");
        }

        private void WriteClassBegin(string className)
        {
            ChangeIndent(+1);
            WriteFromNewLine(string.Format("public partial class {0}", className));
            WriteCodeBlockBegin();
        }

        private void WriteClassBegin(string className, string baseClassName)
        {
            ChangeIndent(+1);
            WriteFromNewLine(string.Format("public partial class {0} : {1}", className, baseClassName));
            WriteCodeBlockBegin();
        }

        private void WriteInternalClassBegin(string className, string baseClassName)
        {
            ChangeIndent(+1);
            if (baseClassName != null)
                WriteFromNewLine(string.Format("internal class {0} : {1}", className, baseClassName));
            else
                WriteFromNewLine(string.Format("internal class {0}", className));
            WriteCodeBlockBegin();
        }

        private void WriteStaticClassBegin(string className)
        {
            ChangeIndent(+1);
            WriteFromNewLine(string.Format("public static class {0}", className));
            WriteCodeBlockBegin();
        }

        private void WriteClassEnd()
        {
            WriteCodeBlockEnd();
            ChangeIndent(-1);
        }

        private void WriteField(FieldDefinition fieldDef)
        {
            string fieldTypeName = fieldDef.FieldTypeName;
            if (Const.SystemTypeNameByNezaboodkaTypeName.ContainsKey(fieldTypeName))
                fieldTypeName = Const.SystemTypeNameByNezaboodkaTypeName[fieldTypeName];
            if (fieldDef.IsList)
                Write(string.Format("public List<{0}> {1};", fieldTypeName, fieldDef.FieldName));
            else
                Write(string.Format("public {0} {1};", fieldTypeName, fieldDef.FieldName));
        }

        private void WriteNamespaceBegin(string programNamespace)
        {
            Write(string.Format("namespace {0}", programNamespace));
            WriteCodeBlockBegin();
        }

        private void WriteCodeBlockBegin()
        {
            WriteFromNewLine("{");
        }

        private void WriteCodeBlockEnd()
        {
            WriteFromNewLine("}");
        }

        private void WriteImportedNamespaces()
        {
            foreach (string name in ImportedNamespaces)
            {
                Write(string.Format("using {0};", name));
                StartNewLine();
            }
        }

        private void ClearStringBuilder()
        {
            fStringBuilder.Clear();
            fCurrentIndent = 0;
        }

        private void Write(string text)
        {
            fStringBuilder.Append(text);
        }

        private void Write(string text, params object[] args)
        {
            fStringBuilder.AppendFormat(text, args);
        }

        private void WriteLine(string text)
        {
            fStringBuilder.AppendLine(text);
            WriteIndent();
        }

        private void WriteFromNewLine(string text)
        {
            StartNewLine();
            Write(text);
        }

        private void WriteFromNewLine(string text, params object[] args)
        {
            StartNewLine();
            Write(text, args);
        }

        private void StartNewLine()
        {
            fStringBuilder.AppendLine();
            WriteIndent();
        }

        private void WriteIndent()
        {
            for (int i = 0; i < fCurrentIndent; i++)
                fStringBuilder.Append(Const.gIndentText);
        }

        private void ChangeIndent(int indentChange)
        {
            fCurrentIndent += indentChange;
        }

        private bool TypeImplementsGenericInterface(Type type, Type interfaceType)
        {
            return type.IsAssignableFrom(interfaceType)
                || (type.IsGenericType && type.GetGenericTypeDefinition() == interfaceType)
                || type.GetInterfaces().Any((Type x) => x.IsGenericType && x.GetGenericTypeDefinition() == interfaceType);
        }
    }

    public static partial class Const
    {
        public static string gIndentText = new string(' ', 4);
        public static string gObjectFormatterClassNameSuffix = "Formatter";
        public static string gTypeSystemClassName = "NezaboodkaTypeSystem";
        public static Dictionary<string, Type> SystemTypeByNezaboodkaTypeName = new Dictionary<string, Type>()
        {
            {"Boolean", typeof(bool)},
            {"SByte", typeof(sbyte)},
            {"Byte", typeof(byte)},
            {"Int16", typeof(short)},
            {"UInt16", typeof(ushort)},
            {"Int32", typeof(int)},
            {"UInt32", typeof(uint)},
            {"Int64", typeof(long)},
            {"UInt64", typeof(ulong)},
            {"Float", typeof(float)},
            {"Double", typeof(double)},
            {"Decimal", typeof(decimal)},
            {"Char", typeof(char)},
            {"String", typeof(string)},
            {"DateTime", typeof(DateTimeOffset)}
        };
        public static Dictionary<string, string> SystemTypeNameByNezaboodkaTypeName = new Dictionary<string, string>()
        {
            {"Boolean", "bool"},
            {"SByte", "sbyte"},
            {"Byte", "byte"},
            {"Int16", "short"},
            {"UInt16", "ushort"},
            {"Int32", "int"},
            {"UInt32", "uint"},
            {"Int64", "long"},
            {"UInt64", "ulong"},
            {"Float", "float"},
            {"Double", "double"},
            {"Decimal", "decimal"},
            {"Char", "char"},
            {"String", "string"},
            {"DateTime", "DateTimeOffset"}
        };
        public static Dictionary<Type, string> NullValueBySystemType = new Dictionary<Type, string>()
        {
            {typeof(sbyte), "sbyte.MinValue"},
            {typeof(byte), "byte.MaxValue"},
            {typeof(short), "short.MinValue"},
            {typeof(ushort), "ushort.MaxValue"},
            {typeof(int), "int.MinValue"},
            {typeof(uint), "uint.MaxValue"},
            {typeof(long), "long.MinValue"},
            {typeof(ulong), "ulong.MaxValue"},
            {typeof(float), "float.MinValue"},
            {typeof(double), "double.MinValue"},
            {typeof(decimal), "decimal.MinValue"},
            {typeof(char), "char.MaxValue"},
            {typeof(string), "null"},
            {typeof(DateTimeOffset), "DateTimeOffset.MinValue"}
        };
    }
}
