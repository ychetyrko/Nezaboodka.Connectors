using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public static class NdefText
    {
        public static string SaveToNdefText(object anObject, ClientTypeBinder typeBinder)
        {
            return SaveToNdefText(anObject, typeBinder, true);
        }

        public static string SaveToNdefText(object anObject, ClientTypeBinder typeBinder, bool stepIntoNestedDbObjects)
        {
            var roots = new object[1] { anObject };
            return SaveToNdefText(roots, typeBinder, stepIntoNestedDbObjects);
        }

        public static string SaveToNdefText(IEnumerable roots, ClientTypeBinder typeBinder)
        {
            return SaveToNdefText(roots, typeBinder, true);
        }

        public static string SaveToNdefText(IEnumerable roots, ClientTypeBinder typeBinder, bool stepIntoNestedDbObjects)
        {
            using (var stream = new MemoryStream())
            {
                using (var ndefWriter = new NdefWriter(stream))
                {
                    var objectsReader = new ObjectsReader(typeBinder, stepIntoNestedDbObjects, roots);
                    NdefSerializer.WriteObjects(objectsReader, ndefWriter);
                }
                stream.Position = 0;
                var reader = new TextAndBinaryReader(stream);
                string result = reader.ReadToEnd();
                return result;
            }
        }

        public static void SaveToNdefFile(object anObject, ClientTypeBinder typeBinder, string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
            {
                var roots = new object[1] { anObject };
                SaveToNdefStream(roots, typeBinder, stream);
            }
        }

        public static void SaveToNdefStream(IEnumerable roots, ClientTypeBinder typeBinder, Stream stream)
        {
            using (var ndefWriter = new NdefWriter(stream))
            {
                var objectsReader = new ObjectsReader(typeBinder, true, roots);
                NdefSerializer.WriteObjects(objectsReader, ndefWriter);
            }
        }

        public static object LoadFromNdefFile(string filePath, ClientTypeBinder typeBinder)
        {
            string ndefText = File.ReadAllText(filePath);
            return LoadFromNdefText(ndefText, typeBinder);
        }

        public static object LoadFromNdefText(string ndefText, ClientTypeBinder typeBinder)
        {
            object result = null;
            var roots = new List<object>();
            LoadFromNdefText(ndefText, typeBinder, roots);
            if (roots.Count > 0)
                result = roots[0];
            return result;
        }

        public static void LoadFromNdefText(string ndefText, ClientTypeBinder typeBinder, IList result)
        {
            LoadFromNdefStream(new MemoryStream(NdefConst.Encoding.GetBytes(ndefText)), typeBinder, result);
        }

        public static void LoadFromNdefStream(Stream stream, ClientTypeBinder typeBinder, IList result)
        {
            var deserializer = new NdefDeserializer(stream, NdefLinkingMode.OneWayLinkingAndOriginalOrder);
            deserializer.SetTypeBinder(typeBinder);
            if (deserializer.MoveToNextDataSet())
                foreach (object x in deserializer.ReadObjects())
                    result.Add(x);
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
