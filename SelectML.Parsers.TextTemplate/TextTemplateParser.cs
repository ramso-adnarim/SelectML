using System;
using System.ComponentModel;
using System.IO;
using SelectML.Core;
using SelectML.Core.Templates;

namespace SelectML.Parsers.TextTemplate
{
    /// <summary>
    /// Plugin leitor genérico de arquivos de texto/CSV baseado em templates configuráveis.
    /// Implementa IMachineParser para integração com o carregador de plugins e IConfigurableParser para parametrização pela UI.
    /// </summary>
    public class TextTemplateParser : IConfigurableParser
    {
        private TemplateDefinition? _activeTemplate;
        private readonly TemplateRepository _repository;
        private readonly TemplateExecutionEngine _engine;

        public TextTemplateParser()
        {
            _repository = new TemplateRepository();
            _engine = new TemplateExecutionEngine();
        }

        public string MachineName => _activeTemplate != null && !string.IsNullOrWhiteSpace(_activeTemplate.Name)
            ? $"Leitor de arquivos de texto - {_activeTemplate.Name}"
            : "Leitor de arquivos de texto";

        public string ActiveTemplateName => _activeTemplate?.Name ?? string.Empty;

        public TemplateDefinition? ActiveTemplate => _activeTemplate;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetActiveTemplate(TemplateDefinition? template)
        {
            _activeTemplate = template;
            OnPropertyChanged(nameof(MachineName));
            OnPropertyChanged(nameof(ActiveTemplateName));
            OnPropertyChanged(nameof(ActiveTemplate));
        }

        public void SetActiveTemplateByName(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                SetActiveTemplate(null);
                return;
            }

            var template = _repository.GetTemplateByName(templateName);
            SetActiveTemplate(template);
        }

        public bool CanParse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            // Se nenhum template estiver ativo, aceita .csv e .txt como padrão
            if (_activeTemplate == null)
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                return ext == ".csv" || ext == ".txt" || ext == ".dat";
            }

            var targetExt = _activeTemplate.FileExtension?.Trim().ToLowerInvariant() ?? ".csv";
            if (targetExt == "*.*" || targetExt == "*")
            {
                return true;
            }

            var fileExt = Path.GetExtension(filePath).ToLowerInvariant();
            return string.Equals(fileExt, targetExt, StringComparison.OrdinalIgnoreCase);
        }

        public MeasurementData Parse(string filePath)
        {
            if (_activeTemplate == null)
            {
                // Se nenhum template foi ativado explicitamente, tenta carregar o primeiro template disponível no repositório
                var all = _repository.GetAllTemplates();
                if (all.Count > 0)
                {
                    SetActiveTemplate(all[0]);
                }
                else
                {
                    throw new InvalidOperationException("Nenhum template configurado ou ativo para o Leitor de Arquivos de Texto. Clique no ícone de engrenagem para configurar um template.");
                }
            }

            return _engine.Execute(filePath, _activeTemplate!);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
