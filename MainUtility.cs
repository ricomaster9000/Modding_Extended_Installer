using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Newtonsoft.Json;
// ReSharper disable All

namespace ExtendedInstaller
{
    public class MainUtility
    {

        private static readonly string _AssemblyLocationDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

        public static AssemblyDefinition loadAssembly(string fullPath)
        {
            var rParams = ExtendedInstaller.GetReaderParameters();
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(fullPath));
            rParams.AssemblyResolver = resolver;
            Console.WriteLine(AssemblyDefinition.ReadAssembly(fullPath, rParams).Name);
            return AssemblyDefinition.ReadAssembly(fullPath, rParams);
        }

        public static string? readTextFromFileReturnNullIfNotExists(string filePath)
        {
            string ?result = null;
            if (File.Exists(filePath))
            {
                using (StreamReader r = new StreamReader(filePath))
                {
                    result = r.ReadToEnd();
                }
            }
            return result;
        }

        public static Dictionary<K,V> readJsonFile<K,V>(string fileInCurrentWorkingDirectory)
        {
            String ?fileData = readTextFromFileReturnNullIfNotExists(fileInCurrentWorkingDirectory);
            if (fileData == null)
            {
                throw new Exception("readJsonFileIntoDictionary() -> json file does not exist");
            }
            return JsonConvert.DeserializeObject<Dictionary<K, V>>(fileData);
        }
    }
}
