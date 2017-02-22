using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Nezaboodka.Ndef
{
    public static class NdefUtils
    {
        public static int ParsePrefixedToken(string text, int start, out char prefix, out string result)
        {
            int i = FindFirstNonWhitespaceChar(text, start);
            int k = i;
            if (i >= 0)
            {
                prefix = text[i];
                switch (prefix)
                {
                    case NdefConst.QuotationMarker: // |
                        if (text[text.Length - 1] != NdefConst.QuotationMarker)
                            result = text.Substring(i + 1);
                        else
                            result = text.Substring(i + 1, text.Length - i - 1);
                        k = -1;
                        break;
                    case NdefConst.ExplicitTypeNamePrefix: // `
                    case NdefConst.ObjectKeyPrefix: // #
                    case NdefConst.ObjectNumberPrefix: // ^
                        prefix = text[i];
                        i = FindFirstNonWhitespaceChar(text, i + 1);
                        k = FindFirstWhitespaceChar(text, i);
                        if (k >= 0)
                            result = text.Substring(i, k - i);
                        else
                            result = text.Substring(i);
                        break;
                    default:
                        prefix = default(char);
                        if (text[text.Length - 1] != NdefConst.QuotationMarker)
                            result = text.Substring(i);
                        else
                            result = text.Substring(i, text.Length - i - 1);
                        k = -1;
                        break;
                }
            }
            else
            {
                prefix = default(char); // '\0'
                result = null;
            }
            return k;
        }

        public static int ParsePrefixedTokenForObjectHeader(string text, int start, out char prefix, out string result)
        {
            int i = FindFirstNonWhitespaceChar(text, start);
            int k = i;
            if (i >= 0)
            {
                prefix = text[i];
                if (prefix == NdefConst.ObjectStartMarker || prefix == NdefConst.ObjectKeyPrefix ||
                    prefix == NdefConst.ObjectNumberPrefix)
                {
                    i = FindFirstNonWhitespaceChar(text, i + 1);
                    k = FindFirstWhitespaceChar(text, i);
                    if (k >= 0)
                        result = text.Substring(i, k - i);
                    else
                        result = text.Substring(i);
                }
                else
                {
                    prefix = default(char); // '\0'
                    result = null;
                }
            }
            else
            {
                prefix = default(char); // '\0'
                result = null;
            }
            return k;
        }

        public static int FindFirstWhitespaceChar(string text, int start)
        {
            int result = start;
            int length = text != null ? text.Length : 0;
            while (result < length && !char.IsWhiteSpace(text[result]))
                result += 1;
            return result < length ? result : -1;
        }

        public static int FindFirstNonWhitespaceChar(string text, int start)
        {
            int result = start;
            int length = text != null ? text.Length : 0;
            while (result < length && char.IsWhiteSpace(text[result]))
                result += 1;
            return result < length ? result : -1;
        }

        public static void SplitAndTrim(string line, int start, out string first, char delimiter,
            out string second, bool returnDelimiterWithSecond)
        {
            int i = line.IndexOf(delimiter, start);
            if (i < 0)
                i = line.Length;
            first = TrimEx(line, start, i - start);
            if (i < line.Length)
            {
                if (returnDelimiterWithSecond)
                    second = TrimEx(line, i, line.Length - i);
                else
                    second = TrimEx(line, i + 1, line.Length - i - 1);
            }
            else
                second = null;
        }

        public static string TrimEx(string value, int start, int length)
        {
            string result = null;
            if (value != null)
                result = value.Substring(start, length).Trim();
            if (result == "")
                result = null;
            return result;
        }

        public static MemberInfo GetFieldOrProperty(object obj, string fieldName)
        {
            Type type = obj.GetType();
            var result = (MemberInfo)type.GetField(fieldName);
            if (result == null)
                result = type.GetProperty(fieldName);
            return result;
        }

        public static IEnumerable<MemberInfo> GetAllPublicFieldsAndProperties(object obj)
        {
            return GetFieldsAndProperies(obj.GetType(), BindingFlags.Instance | BindingFlags.Public);
        }

        public static IEnumerable<MemberInfo> GetFieldsAndProperies(Type type, BindingFlags bindingFlags)
        {
            foreach (FieldInfo f in type.GetFields(bindingFlags))
                yield return f;
            foreach (PropertyInfo p in type.GetProperties(bindingFlags))
                yield return p;
        }

        public static Type GetMemberType(MemberInfo member)
        {
            if (member is FieldInfo)
                return ((FieldInfo)member).FieldType;
            else if (member is PropertyInfo)
                return ((PropertyInfo)member).PropertyType;
            else
                return null;
        }

        public static Type TryGetElementType(Type type)
        {
            Type result = null;
            if (type.IsArray && type != typeof(byte[]))
                result = type.GetElementType();
            else if (type.IsGenericType)
            {
                Type elementType = type.GetGenericArguments()[0];
                Type listType = typeof(IEnumerable<>).MakeGenericType(elementType);
                if (listType.IsAssignableFrom(type))
                    result = elementType;
            }
            return result;
        }

        public static Type GetElementType(Type type, Type defaultType)
        {
            Type result = TryGetElementType(type) ?? defaultType;
            if (result == null)
                throw new ArgumentException("type is not a list or an array ({0})", type.FullName);
            return result;
        }

        public static object GetMemberValue(object obj, MemberInfo member)
        {
            object value = null;
            if (member != null)
            {
                var p = member as PropertyInfo;
                if (p == null)
                {
                    var f = member as FieldInfo;
                    if (f != null)
                        value = f.GetValue(obj);
                    else
                        throw new NotImplementedException();
                }
                else
                    value = p.GetValue(obj, null);
            }
            return value;
        }

        public static void SetMemberValue(object obj, MemberInfo member, object value)
        {
            var p = member as PropertyInfo;
            if (p == null)
            {
                var f = member as FieldInfo;
                if (f != null)
                    f.SetValue(obj, value);
                else
                    throw new NotImplementedException();
            }
            else
                p.SetValue(obj, value, null);
        }

        public static IList TryAcquireList(object obj, MemberInfo member, bool enforce,
            Type preferredListType, Type rootType, out bool listCreated)
        {
            listCreated = false;
            IList list = null;
            Type type = NdefUtils.GetMemberType(member);
            object value = NdefUtils.GetMemberValue(obj, member);
            if (value != null)
            {
                var t = value as IList;
                if (t != null)
                {
                    if (t != null && t.IsFixedSize)
                    {
                        list = CreateList(type, preferredListType, rootType, (IEnumerable<object>)value);
                        listCreated = true;
                    }
                    else
                        list = t;
                }
            }
            else if (enforce || typeof(IList).IsAssignableFrom(type))
            {
                list = CreateList(type, preferredListType, rootType, null);
                listCreated = true;
            }
            return list;
        }

        public static IList CreateList(Type formalTargetType, Type actualType, Type rootType, IEnumerable<object> init)
        {
            if (formalTargetType.IsArray)
            {
                if (actualType.IsGenericTypeDefinition)
                    actualType = actualType.MakeGenericType(formalTargetType.GetElementType());
                else if (formalTargetType.GetElementType() != actualType.GetGenericArguments()[0])
                    throw new ArgumentException("formal list type is incompatible with actual list type");
            }
            else if (actualType.IsGenericTypeDefinition)
            {
                actualType = actualType.MakeGenericType(
                    NdefUtils.GetElementType(formalTargetType, rootType));
                if (!formalTargetType.IsInterface && !formalTargetType.IsAssignableFrom(actualType))
                    actualType = formalTargetType;
            }
            var result = (IList)Activator.CreateInstance(actualType);
            if (init != null)
                foreach (var x in init)
                    result.Add(x);
            return result;
        }

        // Helpers for code generated object formatters

        public static T LookupFormatter<T>(this INdefTypeBinder typeBinder, Type type) where T: INdefFormatter
        {
            return (T)LookupFormatterBase(typeBinder, type);
        }

        public static INdefFormatter LookupFormatterBase(this INdefTypeBinder typeBinder, Type type)
        {
            NdefTypeInfo typeInfo = typeBinder.LookupTypeInfoByType(type);
            return typeInfo.Formatter;
        }
    }
}
