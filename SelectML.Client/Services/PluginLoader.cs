using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SelectML.Core;

namespace SelectML.Client.Services
{
    public class PluginLoader
    {
        public List<IMachineParser> LoadPlugins(string pluginPath)
        {
            var parsers = new List<IMachineParser>();

            if (!Directory.Exists(pluginPath))
            {
                Directory.CreateDirectory(pluginPath);
                return parsers;
            }

            var dllFiles = Directory.GetFiles(pluginPath, "*.dll");

            foreach (var file in dllFiles)
            {
                try
                {
                    // Carrega a DLL dinamicamente
                    var assembly = Assembly.LoadFrom(file);

                    // Procura tipos que implementam IMachineParser e não são interfaces/abstratos
                    var types = assembly.GetTypes()
                        .Where(t => typeof(IMachineParser).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in types)
                    {
                        // Cria uma instância da classe encontrada
                        var instance = (IMachineParser)Activator.CreateInstance(type);
                        parsers.Add(instance);
                    }
                }
                catch (Exception ex)
                {
                    // Em produção, logaríamos isso no Serilog
                    System.Diagnostics.Debug.WriteLine($"Erro ao carregar plugin {file}: {ex.Message}");
                }
            }

            return parsers;
        }
    }
}