
using System;
using System.IO;
using Newtonsoft.Json;
using NLog;
using YamlDotNet.Serialization;

namespace Ghosts.Domain.Code.Helpers
{
    public static class ConfigManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public enum FileFormat { Json, Yaml }
        private static ISerializer yamlSerializer = new SerializerBuilder()
        .Build();
        private static IDeserializer yamlDeserializer = new DeserializerBuilder()
        .Build();


        public static (T, FileFormat) DeserializeConfig<T>(string raw)
        {
            if (raw.Length < 1) 
            {
                throw new ArgumentException("Configuration data cannot be empty");
            }
            try {
                try 
                {
                    return (JsonConvert.DeserializeObject<T>(raw), FileFormat.Json);
                }
                catch (Exception ex)
                {
                    _log.Debug("Input was not valid JSON: " + ex.Message);
                    _log.Debug("raw data: ", raw);
                    return (yamlDeserializer.Deserialize<T>(raw), FileFormat.Yaml);
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Passed data was not valid JSON or YAML: " + ex.Message, ex);
            }
        }

        public static string SerializeConfig(object obj, FileFormat format = FileFormat.Json) {
            switch(format) {
                case FileFormat.Json:
                    return JsonConvert.SerializeObject(obj);
                case FileFormat.Yaml:
                    return yamlSerializer.Serialize(obj);
                default:
                    return JsonConvert.SerializeObject(obj);
            }

        }

        public static (T, FileFormat) LoadConfig<T>(string path) 
        {
            var extension = Path.GetExtension(path);
            var content = File.ReadAllText(path);

            if (content.Length < 1) 
            {
                throw new ArgumentException("Configuration data cannot be empty");
            }

            if (extension == ".yaml" || extension == ".yml") {
                return (yamlDeserializer.Deserialize<T>(content), FileFormat.Yaml);
            }
            else if (extension == ".json") {
               return (JsonConvert.DeserializeObject<T>(content), FileFormat.Json);
            } else {
                throw new ArgumentException($"{0} is not a valid config extension. Use .yaml, .yml or .json", extension);
            }
        }

        public static void SaveConfig(object obj, string path, Formatting formatting = Formatting.None) 
        {
            var extension = Path.GetExtension(path);
            string content;
            if (extension == ".yaml" || extension == ".yml") {
                content = yamlSerializer.Serialize(obj);
            }
            else if (extension == ".json") {
               content = JsonConvert.SerializeObject(obj, formatting);
            } else {
                throw new ArgumentException($"{0} is not a valid config extension. Use .yaml, .yml or .json", extension);
            }
            File.WriteAllText(path, content);
        }
    }
}