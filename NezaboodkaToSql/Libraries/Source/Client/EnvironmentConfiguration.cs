using System.IO;
using System.Collections.Generic;

namespace Nezaboodka
{
    public class EnvironmentConfiguration
    {
        public string Version;
        public string ConfigurationId;
        public string Name;
        public int IdGeneratorBitsPerNodeNumber;
        public int IdGeneratorBitsPerContinuousIdRange;
        public List<NodeTemplateConfiguration> NodeTemplates;
        public List<NodeConfiguration> Nodes;

        public EnvironmentConfiguration()
        {
            Version = "1.0";
        }

        public EnvironmentConfiguration(EnvironmentConfiguration existing)
        {
            Version = existing.Version;
            ConfigurationId = existing.ConfigurationId;
            Name = existing.Name;
            IdGeneratorBitsPerContinuousIdRange = existing.IdGeneratorBitsPerContinuousIdRange;
            IdGeneratorBitsPerNodeNumber = existing.IdGeneratorBitsPerNodeNumber;
            NodeTemplates = existing.NodeTemplates;
            Nodes = existing.Nodes;
        }

        public static EnvironmentConfiguration CreateFromNdefText(string ndefText)
        {
            return (EnvironmentConfiguration)NdefText.LoadFromNdefText(ndefText, ClientTypeBinder.Default);
        }

        public static EnvironmentConfiguration LoadFromNdefFile(string filePath)
        {
            return (EnvironmentConfiguration)NdefText.LoadFromNdefFile(filePath, ClientTypeBinder.Default);
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

    public class NodeTemplateConfiguration
    {
        public string Name;
        public DatabaseServiceConfiguration DatabaseService;
        public NodeServiceConfiguration NodeService;
    }

    public class NodeConfiguration
    {
        public int Number;
        public string Host;
        public string TemplateName;
    }

    public class ServiceConfiguration
    {
        public string Binding;
        public string Address;
    }

    public class DatabaseServiceConfiguration : ServiceConfiguration
    {
    }

    public class NodeServiceConfiguration : ServiceConfiguration
    {
    }
}
