using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public class DatabaseConfiguration
    {
        public string ConfigurationId;
        public int RedundancyLevel;
        public bool? InMemoryOnly;
        public long? MaxPatchFileSize;
        public DatabaseSchema DatabaseSchema;
        public List<int> Nodes;
        public List<PrimaryIndexDistribution> PrimaryIndexDistributions;
        public List<SecondaryIndexDistribution> SecondaryIndexDistributions;
        public List<TextIndexCacheDistribution> TextIndexCacheDistributions;
        public int? FileBufferSize;
        public List<FileStorageDistribution> FileStorageDistributions;

        public DatabaseConfiguration()
        {
            ConfigurationId = string.Empty;
            RedundancyLevel = int.MinValue;
            InMemoryOnly = null;
            MaxPatchFileSize = null;
            DatabaseSchema = new DatabaseSchema();
            Nodes = new List<int>();
            PrimaryIndexDistributions = new List<PrimaryIndexDistribution>();
            SecondaryIndexDistributions = new List<SecondaryIndexDistribution>();
            TextIndexCacheDistributions = new List<TextIndexCacheDistribution>();
            FileBufferSize = null;
            FileStorageDistributions = new List<FileStorageDistribution>();
        }

        public DatabaseConfiguration(DatabaseConfiguration existing)
        {
            ConfigurationId = existing.ConfigurationId;
            RedundancyLevel = existing.RedundancyLevel;
            InMemoryOnly = existing.InMemoryOnly;
            MaxPatchFileSize = existing.MaxPatchFileSize;
            DatabaseSchema = existing.DatabaseSchema;
            Nodes = existing.Nodes;
            PrimaryIndexDistributions = existing.PrimaryIndexDistributions;
            SecondaryIndexDistributions = existing.SecondaryIndexDistributions;
            TextIndexCacheDistributions = existing.TextIndexCacheDistributions;
            FileBufferSize = existing.FileBufferSize;
            FileStorageDistributions = existing.FileStorageDistributions;
        }

        public static DatabaseConfiguration CreateFromNdefText(string ndefText)
        {
            return (DatabaseConfiguration)NdefText.LoadFromNdefText(ndefText, ClientTypeBinder.Default);
        }

        public static DatabaseConfiguration LoadFromNdefFile(string filePath)
        {
            return (DatabaseConfiguration)NdefText.LoadFromNdefFile(filePath, ClientTypeBinder.Default);
        }

        public string ToNdefText()
        {
            return NdefText.SaveToNdefText(this, ClientTypeBinder.Default);
        }

        public void SaveToNdefFile(string filePath)
        {
            string ndefText = ToNdefText();
            File.WriteAllText(filePath, ndefText);
        }
    }

    public class DatabaseSchema
    {
        public List<TypeDefinition> TypeDefinitions;
        public List<SecondaryIndexDefinition> SecondaryIndexDefinitions;
        public List<ReferentialIndexDefinition> ReferentialIndexDefinitions;
        public List<TextIndexDefinition> TextIndexDefinitions;
        public List<TypeAndFields> TextIndexTypesAndFields;

        public DatabaseSchema()
        {
            TypeDefinitions = new List<TypeDefinition>();
            SecondaryIndexDefinitions = new List<SecondaryIndexDefinition>();
            ReferentialIndexDefinitions = new List<ReferentialIndexDefinition>();
            TextIndexDefinitions = new List<TextIndexDefinition>();
            TextIndexTypesAndFields = new List<TypeAndFields>();
        }
    }

    public class TypeDefinition
    {
        public string TypeName;
        public string BaseTypeName;
        public List<FieldDefinition> FieldDefinitions;

        public TypeDefinition()
        {
            FieldDefinitions = new List<FieldDefinition>();
        }
    }

    public class FieldDefinition
    {
        public string FieldName;
        public string FieldTypeName;
        public bool IsList;
        public CompareOptions CompareOptions;
        public string BackReferenceFieldName;

        public static FieldDefinition Parse(string str)
        {
            var result = new FieldDefinition()
            {
                FieldName = string.Empty,
                FieldTypeName = string.Empty,
                IsList = false,
                CompareOptions = CompareOptions.None,
                BackReferenceFieldName = null
            };
            bool error = false;
            if (!string.IsNullOrEmpty(str))
            {
                string attr;
                NdefUtils.SplitAndTrim(str, 0, out result.FieldName, ':', out attr, false);
                if (attr != null && !string.IsNullOrEmpty(result.FieldName) && !result.FieldName.ContainsWhitespace())
                {
                    bool ignoreCase = false;
                    List<string> attrs = attr.SplitWithSeparatorsIncluded(new char[] { '@' });
                    for (int i = 0; i < attrs.Count; i++)
                    {
                        string s = attrs[i];
                        if (i == 0)
                        {
                            NdefUtils.SplitAndTrim(s, 0, out result.FieldTypeName, '[', out s, true);
                            if (s == "[]")
                                result.IsList = true;
                            if (string.IsNullOrEmpty(result.FieldTypeName) || result.FieldTypeName.ContainsWhitespace())
                                error = true;
                        }
                        else if (!ignoreCase && s == "@IgnoreCase")
                        {
                            result.CompareOptions |= CompareOptions.IgnoreCase;
                            ignoreCase = true;
                        }
                        else if (result.BackReferenceFieldName == null && s.StartsWith("@BackReference("))
                        {
                            NdefUtils.SplitAndTrim(s, "@BackReference(".Length, out result.BackReferenceFieldName, ')', out s, true);
                            if (string.IsNullOrEmpty(result.BackReferenceFieldName) ||
                                result.BackReferenceFieldName.ContainsWhitespace() || s != ")")
                                error = true;
                        }
                        else
                            error = true;
                    }
                }
                else
                    error = true;
            }
            if (error)
                throw new NezaboodkaException(string.Format("invalid field definition format '{0}'", str));
            return result;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(6);
            sb.Append(FieldName);
            sb.Append(": ");
            sb.Append(FieldTypeName);
            if (IsList)
                sb.Append("[]");
            if (CompareOptions.HasFlag(CompareOptions.IgnoreCase))
                sb.Append(" @IgnoreCase");
            if (BackReferenceFieldName != null)
            {
                sb.Append(" @BackReference(");
                sb.Append(BackReferenceFieldName);
                sb.Append(')');
            }
            string result = sb.ToString();
            return result;
        }
    }

    public class SecondaryIndexDefinition
    {
        public string TypeName;
        public List<IndexFieldDefinition> OrderBy;
        public bool IsUnique;

        public static SecondaryIndexDefinition Parse(string str)
        {
            var result = new SecondaryIndexDefinition()
            {
                TypeName = string.Empty,
                OrderBy = new List<IndexFieldDefinition>(),
                IsUnique = false
            };
            string uniqueMark = null;
            string empty = null;
            if (!string.IsNullOrEmpty(str))
            {
                string orderBy;
                NdefUtils.SplitAndTrim(str, 0, out result.TypeName, '[', out orderBy, false);
                if (!string.IsNullOrEmpty(orderBy))
                {
                    NdefUtils.SplitAndTrim(orderBy, 0, out orderBy, ']', out uniqueMark, false);
                    if (!string.IsNullOrEmpty(orderBy))
                    {
                        try
                        {
                            foreach (string s in orderBy.SplitWithSeparatorsIncluded(new char[] { '+', '-' }))
                                result.OrderBy.Add(IndexFieldDefinition.Parse(s));
                        }
                        catch (NezaboodkaException)
                        {
                            result.OrderBy.Clear();
                        }
                        if (uniqueMark == "!")
                            result.IsUnique = true;
                        else
                            empty = uniqueMark;
                    }
                }
                if (string.IsNullOrEmpty(result.TypeName) || result.OrderBy.Count == 0 || !string.IsNullOrEmpty(empty))
                    throw new NezaboodkaException(string.Format(
                        "invalid index definition format '{0}', the expected formats are " +
                        "'TypeName[+Field1+Field2 ... +/-FieldN]' - for index with duplicate keys, " +
                        "'TypeName[+Field1+Field2 ... +/-FieldN]!' - for index with unique keys", str));
            }
            return result;
        }

        public override string ToString()
        {
            string result = string.Format("{0}[{1}]{2}", TypeName, string.Join("", OrderBy), IsUnique ? "!" : "");
            return result;
        }
    }

    public class IndexFieldDefinition
    {
        public string FieldName;
        public FieldValuesOrdering Ordering;

        public static IndexFieldDefinition Parse(string str)
        {
            var result = new IndexFieldDefinition();
            if (str.StartsWith("+"))
            {
                result.Ordering = FieldValuesOrdering.Straight;
                result.FieldName = str.Substring(1, str.Length - "+".Length);
            }
            else if (str.StartsWith("-"))
            {
                result.Ordering = FieldValuesOrdering.Reverse;
                result.FieldName = str.Substring(1, str.Length - "-".Length);
            }
            else
                throw new NezaboodkaException(string.Format("invalid index field definition format '{0}'", str));
            return result;
        }

        public override string ToString()
        {
            string result = (Ordering == FieldValuesOrdering.Straight) ? "+" : "-";
            result = result + FieldName;
            return result;
        }
    }

    public enum FieldValuesOrdering
    {
        Straight = 0,
        Reverse = 1
    }

    public class ReferentialIndexDefinition
    {
        public string TypeName;
        public string FieldName;
        public List<IndexFieldDefinition> OrderBy;

        public static ReferentialIndexDefinition Parse(string str)
        {
            var result = new ReferentialIndexDefinition()
            {
                TypeName = string.Empty,
                FieldName = string.Empty,
                OrderBy = new List<IndexFieldDefinition>()
            };
            if (!string.IsNullOrEmpty(str))
            {
                string[] parts = str.Split('.');
                if (parts != null && parts.Length == 2)
                {
                    result.TypeName = parts[0];
                    parts = parts[1].Split('[');
                    if (parts != null && parts.Length == 2)
                    {
                        result.FieldName = parts[0];
                        parts = parts[1].Split(']');
                        if (parts != null && parts.Length == 2 && 
                            !string.IsNullOrEmpty(parts[0]) && string.IsNullOrEmpty(parts[1]))
                        {
                            try
                            {
                                foreach (string s in parts[0].SplitWithSeparatorsIncluded(new char[] { '+', '-' }))
                                    result.OrderBy.Add(IndexFieldDefinition.Parse(s));
                            }
                            catch (NezaboodkaException)
                            {
                                result.OrderBy.Clear();
                            }
                        }
                    }
                }
                if (string.IsNullOrEmpty(result.TypeName) || string.IsNullOrEmpty(result.FieldName))
                    throw new NezaboodkaException(string.Format(
                        "invalid referential index definition format '{0}', the expected format is " +
                        "'TypeName.ListFieldName[+Field1+Field2 ... +/-FieldN]'", str));
            }
            return result;
        }

        public override string ToString()
        {
            string result = string.Format("{0}.{1}[{2}]", TypeName, FieldName, string.Join("", OrderBy));
            return result;
        }
    }

    public class TextIndexDefinition
    {
        public string TypeName;
        public List<IndexFieldDefinition> OrderBy;

        public static TextIndexDefinition Parse(string str)
        {
            var result = new TextIndexDefinition()
            {
                TypeName = string.Empty,
                OrderBy = new List<IndexFieldDefinition>()
            };
            string textMark = null;
            if (!string.IsNullOrEmpty(str))
            {
                string orderBy;
                NdefUtils.SplitAndTrim(str, 0, out result.TypeName, '[', out orderBy, false);
                if (!string.IsNullOrEmpty(orderBy))
                {
                    NdefUtils.SplitAndTrim(orderBy, 0, out orderBy, ']', out textMark, false);
                    if (!string.IsNullOrEmpty(orderBy) && textMark == "@")
                    {
                        try
                        {
                            foreach (string s in orderBy.SplitWithSeparatorsIncluded(new char[] { '+', '-' }))
                                result.OrderBy.Add(IndexFieldDefinition.Parse(s));
                        }
                        catch (NezaboodkaException)
                        {
                            result.OrderBy.Clear();
                        }
                    }
                }
                if (string.IsNullOrEmpty(result.TypeName) || textMark != "@")
                    throw new NezaboodkaException(string.Format(
                        "invalid index definition format '{0}', the expected format is " +
                        "'TypeName[+Field1+Field2 ... +/-FieldN]@'", str));
            }
            return result;
        }

        public override string ToString()
        {
            string result = string.Format("{0}[{1}]@", TypeName, string.Join("", OrderBy));
            return result;
        }
    }

    public class PrimaryIndexDistribution
    {
        public long? InclusiveUpperBound;
        public List<int> Nodes;
        public List<PrimaryIndexDistributionRange> Ranges;

        public PrimaryIndexDistribution()
        {
            InclusiveUpperBound = null;
            Nodes = new List<int>();
            Ranges = new List<PrimaryIndexDistributionRange>();
        }
    }

    public class PrimaryIndexDistributionRange
    {
        public List<int> MirrorNodes;

        public PrimaryIndexDistributionRange()
        {
            MirrorNodes = new List<int>();
        }
    }

    public class SecondaryIndexDistribution
    {
        public SecondaryIndexDefinition IndexDefinition;
        public List<int> Nodes;
        public List<SecondaryIndexDistributionRange> Ranges;

        public SecondaryIndexDistribution()
        {
            IndexDefinition = new SecondaryIndexDefinition();
            Nodes = new List<int>();
            Ranges = new List<SecondaryIndexDistributionRange>();
        }
    }

    public class SecondaryIndexDistributionRange
    {
        public List<string> InclusiveUpperBound;
        public List<int> MirrorNodes;

        public SecondaryIndexDistributionRange()
        {
            InclusiveUpperBound = new List<string>();
            MirrorNodes = new List<int>();
        }
    }

    public class TextIndexCacheDistribution
    {
        public TextIndexDefinition IndexDefinition;
        public List<int> Nodes;
        public List<TextIndexCacheDistributionRange> Ranges;

        public TextIndexCacheDistribution()
        {
            IndexDefinition = new TextIndexDefinition();
            Nodes = new List<int>();
            Ranges = new List<TextIndexCacheDistributionRange>();
        }
    }

    public class TextIndexCacheDistributionRange
    {
        public string InclusiveUpperBound;
        public List<int> MirrorNodes;

        public TextIndexCacheDistributionRange()
        {
            MirrorNodes = new List<int>();
        }
    }

    public class FileStorageDistribution
    {
        public int FileBlockSize;
        public List<int> Nodes;
        public List<FileStorageDistributionRange> Ranges;

        public FileStorageDistribution()
        {
            FileBlockSize = Const.DefaultFileBlockSize;
            Nodes = new List<int>();
            Ranges = new List<FileStorageDistributionRange>();
        }
    }

    public class FileStorageDistributionRange
    {
        public List<int> MirrorNodes;

        public FileStorageDistributionRange()
        {
            MirrorNodes = new List<int>();
        }
    }
}
