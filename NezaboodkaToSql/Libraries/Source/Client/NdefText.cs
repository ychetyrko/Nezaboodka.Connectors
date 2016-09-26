using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public static class NdefText
    {
        public static string SaveToNdefText(object anObject, INdefTypeBinder typeBinder)
        {
            return SaveToNdefText(anObject, typeBinder, true);
        }

        public static string SaveToNdefText(object anObject, INdefTypeBinder typeBinder, bool stepIntoNestedDbObjects)
        {
            var roots = new object[1] { anObject };
            return SaveToNdefText(roots, typeBinder, stepIntoNestedDbObjects);
        }

        public static string SaveToNdefText(IEnumerable roots, INdefTypeBinder typeBinder)
        {
            return SaveToNdefText(roots, typeBinder, true);
        }

        public static string SaveToNdefText(IEnumerable roots, INdefTypeBinder typeBinder, bool stepIntoNestedDbObjects)
        {
            using (var buffer = new StringWriter() { NewLine = "\n" })
            {
                var serializer = new NdefSerializer(typeBinder, stepIntoNestedDbObjects, roots);
                var writer = new NdefTextWriter(buffer);
                writer.WriteObjectsFrom(serializer);
                buffer.Flush();
                return buffer.ToString();
            }
        }

        public static void SaveToNdefFile(object anObject, INdefTypeBinder typeBinder, string filePath)
        {
            string ndefText = SaveToNdefText(anObject, typeBinder);
            File.WriteAllText(filePath, ndefText);
        }

        public static void SaveToNdefStream(IEnumerable roots, INdefTypeBinder typeBinder, Stream stream)
        {
            using (var textWriter = new StreamWriter(stream, Encoding.UTF8))
            {
                SaveToNdefStream(roots, typeBinder, textWriter, true);
            }
        }

        public static void SaveToNdefStream(IEnumerable roots, INdefTypeBinder typeBinder,
            StreamWriter streamWriter, bool flush)
        {
            var serializer = new NdefSerializer(typeBinder, true, roots);
            var writer = new NdefTextWriter(streamWriter);
            writer.WriteObjectsFrom(serializer);
            if (flush)
                streamWriter.Flush();
        }

        public static object LoadFromNdefFile(string filePath, INdefTypeBinder typeBinder)
        {
            string ndefText = File.ReadAllText(filePath);
            return LoadFromNdefText(ndefText, typeBinder);
        }

        public static object LoadFromNdefText(string ndefText, INdefTypeBinder typeBinder)
        {
            object result = null;
            var roots = new List<object>();
            LoadFromNdefText(ndefText, false, false, typeBinder, roots);
            if (roots.Count > 0)
                result = roots[0];
            return result;
        }

        public static void LoadFromNdefText(string ndefText, bool ignoreUnknownFields,
            bool skipOtherTypes, INdefTypeBinder typeBinder, IList result)
        {
            using (var stringReader = new StringReader(ndefText))
            {
                var reader = new NdefTextReader(stringReader);
                var deserializer = new NdefDeserializer(reader, ignoreUnknownFields);
                foreach (object x in deserializer.ReadAllBlocks<object>(typeBinder,
                    NdefLinkingMode.OneWayLinkingAndOriginalOrder, skipOtherTypes))
                {
                    result.Add(x);
                }
            }
        }

        public static void LoadFromNdefTextWithTwoWayLinking(string ndefText, bool ignoreUnknownFields,
            bool skipOtherTypes, INdefTypeBinder typeBinder, IList result)
        {
            using (var stringReader = new StringReader(ndefText))
            {
                var reader = new NdefTextReader(stringReader);
                var deserializer = new NdefDeserializer(reader, ignoreUnknownFields);
                foreach (object x in deserializer.ReadAllBlocks<object>(typeBinder,
                    NdefLinkingMode.TwoWayLinkingAndNormalizedOrder, skipOtherTypes))
                {
                    result.Add(x);
                }
            }
        }

        public static Stream LoadFromNdefStream(Stream stream, bool ignoreUnknownFields,
            bool skipOtherTypes, INdefTypeBinder typeBinder, IList result, out long binaryDataLength)
        {
            using (var streamReader = new StreamReader(stream))
            {
                var reader = new NdefTextReader(streamReader);
                return LoadFromNdefIterator(reader, ignoreUnknownFields, skipOtherTypes, typeBinder, result,
                    out binaryDataLength);
            }
        }

        public static Stream LoadFromNdefIterator(INdefIterator iterator, bool ignoreUnknownFields,
            bool skipOtherTypes, INdefTypeBinder typeBinder, IList result, out long binaryDataLength)
        {
            var deserializer = new NdefDeserializer(iterator, ignoreUnknownFields);
            foreach (object x in deserializer.ReadAllBlocks<object>(typeBinder,
                NdefLinkingMode.OneWayLinkingAndOriginalOrder, skipOtherTypes))
            {
                result.Add(x);
            }
            binaryDataLength = 0;
            return null;
        }

        public static string LoadNdefTextFromResource(Assembly assembly, string resourceName)
        {
            string result;
            Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using (StreamReader reader = new StreamReader(stream))
                    result = reader.ReadToEnd();
            }
            else
                throw new NezaboodkaException(string.Format("invalid resource name '{0}'", resourceName));
            return result;
        }
    }
}
