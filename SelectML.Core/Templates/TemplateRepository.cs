using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SelectML.Core.Templates
{
    /// <summary>
    /// Repositório centralizado para armazenamento e recuperação de arquivos de templates JSON.
    /// Armazena os templates no diretório do usuário (%APPDATA%\SelectML\Templates).
    /// </summary>
    public class TemplateRepository
    {
        private readonly string _templatesDirectory;

        public TemplateRepository(string? customDirectory = null)
        {
            if (!string.IsNullOrWhiteSpace(customDirectory))
            {
                _templatesDirectory = customDirectory;
            }
            else
            {
                _templatesDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SelectML",
                    "Templates");
            }

            EnsureDirectory();
        }

        public string TemplatesDirectory => _templatesDirectory;

        private void EnsureDirectory()
        {
            try
            {
                if (!Directory.Exists(_templatesDirectory))
                {
                    Directory.CreateDirectory(_templatesDirectory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TemplateRepository] Falha ao criar diretório {_templatesDirectory}: {ex.Message}");
            }
        }

        /// <summary>
        /// Retorna todos os templates cadastrados no repositório.
        /// </summary>
        public List<TemplateDefinition> GetAllTemplates()
        {
            EnsureDirectory();
            var list = new List<TemplateDefinition>();

            try
            {
                var files = Directory.GetFiles(_templatesDirectory, "*.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var template = JsonSerializer.Deserialize<TemplateDefinition>(json, options);
                        if (template != null)
                        {
                            list.Add(template);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TemplateRepository] Erro ao ler arquivo {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TemplateRepository] Erro ao listar templates: {ex.Message}");
            }

            return list.OrderBy(t => t.Name).ToList();
        }

        /// <summary>
        /// Busca um template pelo seu nome.
        /// </summary>
        public TemplateDefinition? GetTemplateByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var all = GetAllTemplates();
            return all.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Salva ou atualiza um template no disco.
        /// </summary>
        public void SaveTemplate(TemplateDefinition template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            EnsureDirectory();

            var safeName = SanitizeFileName(template.Name);
            var filePath = Path.Combine(_templatesDirectory, $"{safeName}.json");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            var json = JsonSerializer.Serialize(template, options);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Exclui um template do repositório pelo nome.
        /// </summary>
        public bool DeleteTemplate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            EnsureDirectory();

            var safeName = SanitizeFileName(name);
            var filePath = Path.Combine(_templatesDirectory, $"{safeName}.json");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }

            // Tentar encontrar pelo template dentro dos arquivos se o nome do arquivo foi alterado
            var allFiles = Directory.GetFiles(_templatesDirectory, "*.json");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            foreach (var file in allFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var t = JsonSerializer.Deserialize<TemplateDefinition>(json, options);
                    if (t != null && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(file);
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private static string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(ch => !invalidChars.Contains(ch)).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "Template" : sanitized;
        }
    }
}
