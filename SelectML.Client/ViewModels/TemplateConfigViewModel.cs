using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using SelectML.Client.MVVM;
using SelectML.Client.Views;
using SelectML.Core;
using SelectML.Core.Templates;

namespace SelectML.Client.ViewModels
{
    public class TemplateConfigViewModel : INotifyPropertyChanged
    {
        private readonly TemplateRepository _repository;
        private readonly IConfigurableParser? _parser;
        private readonly TemplateExecutionEngine _engine = new TemplateExecutionEngine();

        private TemplateDefinition? _selectedTemplate;
        private string _sampleFilePath = string.Empty;
        private string _sampleFileContent = string.Empty;

        // Propriedades observáveis da Prévia do Resultado
        private string _previewPartName = "-";
        private string _previewBatchNumber = "-";
        private int _previewItemCount = 0;
        private string _previewStatusMessage = "Mapeie características e valores para visualizar o resultado.";
        private bool _hasPreviewResults = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<bool>? RequestClose;

        public ObservableCollection<TemplateDefinition> Templates { get; } = new ObservableCollection<TemplateDefinition>();
        public ObservableCollection<TemplateLineRule> CurrentRules { get; } = new ObservableCollection<TemplateLineRule>();
        public ObservableCollection<TemplatePreviewItem> PreviewResults { get; } = new ObservableCollection<TemplatePreviewItem>();

        public List<string> AvailableExtensions { get; } = new List<string> { ".csv", ".txt", ".dat", "*.*" };
        public List<string> AvailableSeparators { get; } = new List<string> { ";", ",", "\\t", " ", "|" };

        public event Action<string>? SampleFileLoaded;
        public event Action? RulesChanged;

        public ICommand NewTemplateCommand { get; }
        public ICommand DuplicateTemplateCommand { get; }
        public ICommand DeleteTemplateCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand BrowseSampleFileCommand { get; }
        public ICommand SaveAndApplyCommand { get; }
        public ICommand CancelCommand { get; }

        public string PreviewPartName
        {
            get => _previewPartName;
            private set { if (_previewPartName != value) { _previewPartName = value; OnPropertyChanged(); } }
        }

        public string PreviewBatchNumber
        {
            get => _previewBatchNumber;
            private set { if (_previewBatchNumber != value) { _previewBatchNumber = value; OnPropertyChanged(); } }
        }

        public int PreviewItemCount
        {
            get => _previewItemCount;
            private set
            {
                if (_previewItemCount != value)
                {
                    _previewItemCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PreviewItemCountDisplay));
                }
            }
        }

        public string PreviewItemCountDisplay => PreviewItemCount == 1 ? "1 cota" : $"{PreviewItemCount} cotas";

        public string PreviewStatusMessage
        {
            get => _previewStatusMessage;
            private set { if (_previewStatusMessage != value) { _previewStatusMessage = value; OnPropertyChanged(); } }
        }

        public bool HasPreviewResults
        {
            get => _hasPreviewResults;
            private set { if (_hasPreviewResults != value) { _hasPreviewResults = value; OnPropertyChanged(); } }
        }

        public TemplateConfigViewModel(IConfigurableParser? parser = null, TemplateRepository? repository = null)
        {
            _parser = parser;
            _repository = repository ?? new TemplateRepository();

            NewTemplateCommand = new RelayCommand(ExecuteNewTemplate);
            DuplicateTemplateCommand = new RelayCommand(ExecuteDuplicateTemplate, CanExecuteOnSelected);
            DeleteTemplateCommand = new RelayCommand(ExecuteDeleteTemplate, CanExecuteOnSelected);
            RemoveRuleCommand = new RelayCommand(ExecuteRemoveRule);
            BrowseSampleFileCommand = new RelayCommand(ExecuteBrowseSampleFile);
            SaveAndApplyCommand = new RelayCommand(ExecuteSaveAndApply, CanExecuteOnSelected);
            CancelCommand = new RelayCommand(ExecuteCancel);

            LoadTemplates();
        }

        public TemplateDefinition? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (_selectedTemplate != value)
                {
                    _selectedTemplate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSelectedTemplate));
                    OnPropertyChanged(nameof(TemplateName));
                    OnPropertyChanged(nameof(FileExtension));
                    OnPropertyChanged(nameof(DefaultSeparator));
                    RefreshCurrentRules();
                    UpdateValidationStatus();
                    UpdatePreview();
                }
            }
        }

        public bool HasSelectedTemplate => SelectedTemplate != null;

        public string TemplateName
        {
            get => SelectedTemplate?.Name ?? string.Empty;
            set
            {
                if (SelectedTemplate != null && SelectedTemplate.Name != value)
                {
                    SelectedTemplate.Name = value;
                    OnPropertyChanged();
                    UpdateValidationStatus();
                    UpdatePreview();
                }
            }
        }

        public string FileExtension
        {
            get => SelectedTemplate?.FileExtension ?? ".csv";
            set
            {
                if (SelectedTemplate != null && SelectedTemplate.FileExtension != value)
                {
                    SelectedTemplate.FileExtension = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DefaultSeparator
        {
            get => SelectedTemplate?.DefaultSeparator ?? ";";
            set
            {
                if (SelectedTemplate != null && SelectedTemplate.DefaultSeparator != value)
                {
                    SelectedTemplate.DefaultSeparator = value;
                    OnPropertyChanged();
                    UpdatePreview();
                }
            }
        }

        public string SampleFilePath
        {
            get => _sampleFilePath;
            set
            {
                if (_sampleFilePath != value)
                {
                    _sampleFilePath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSampleFile));
                }
            }
        }

        public bool HasSampleFile => !string.IsNullOrWhiteSpace(SampleFilePath);

        public string SampleFileContent
        {
            get => _sampleFileContent;
            set
            {
                if (_sampleFileContent != value)
                {
                    _sampleFileContent = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _validationMessage = "Selecione ou crie um template.";
        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (_validationMessage != value)
                {
                    _validationMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isTemplateValid = false;
        public bool IsTemplateValid
        {
            get => _isTemplateValid;
            private set
            {
                if (_isTemplateValid != value)
                {
                    _isTemplateValid = value;
                    OnPropertyChanged();
                }
            }
        }

        private void LoadTemplates()
        {
            Templates.Clear();
            var all = _repository.GetAllTemplates();

            foreach (var t in all)
            {
                Templates.Add(t);
            }

            if (Templates.Count == 0)
            {
                // Criar template padrão inicial se a biblioteca estiver vazia
                var defaultTemplate = CreateInitialDefaultTemplate();
                _repository.SaveTemplate(defaultTemplate);
                Templates.Add(defaultTemplate);
            }

            // Seleciona o template ativo do parser ou o primeiro da lista
            if (_parser != null && !string.IsNullOrEmpty(_parser.ActiveTemplateName))
            {
                SelectedTemplate = Templates.FirstOrDefault(t => string.Equals(t.Name, _parser.ActiveTemplateName, StringComparison.OrdinalIgnoreCase)) 
                                   ?? Templates.FirstOrDefault();
            }
            else
            {
                SelectedTemplate = Templates.FirstOrDefault();
            }
        }

        private TemplateDefinition CreateInitialDefaultTemplate()
        {
            return new TemplateDefinition
            {
                Name = "Template Padrão (CSV)",
                FileExtension = ".csv",
                DefaultSeparator = ";",
                UseFileNameForPartName = true,
                UseFileNameForBatchNumber = true,
                LineRules = new List<TemplateLineRule>
                {
                    new TemplateLineRule { LineIndex = 1, Role = LineRuleRole.CharacteristicNames, Separator = ";" },
                    new TemplateLineRule { LineIndex = 2, Role = LineRuleRole.MeasuredValues, Separator = ";" }
                }
            };
        }

        private void RefreshCurrentRules()
        {
            CurrentRules.Clear();
            if (SelectedTemplate?.LineRules != null)
            {
                foreach (var rule in SelectedTemplate.LineRules.OrderBy(r => r.LineIndex))
                {
                    CurrentRules.Add(rule);
                }
            }
        }

        public void UpdateValidationStatus()
        {
            if (SelectedTemplate == null)
            {
                IsTemplateValid = false;
                ValidationMessage = "Nenhum template selecionado.";
                return;
            }

            if (SelectedTemplate.IsValid(out string error))
            {
                IsTemplateValid = true;
                bool noPart = SelectedTemplate.UseFileNameForPartName;
                bool noBatch = SelectedTemplate.UseFileNameForBatchNumber;

                if (noPart && noBatch)
                {
                    ValidationMessage = "Template válido (Rotina e Corrida usarão o nome do arquivo).";
                }
                else if (noPart)
                {
                    ValidationMessage = "Template válido (Rotina usará o nome do arquivo).";
                }
                else if (noBatch)
                {
                    ValidationMessage = "Template válido (Corrida usará o nome do arquivo).";
                }
                else
                {
                    ValidationMessage = "Template válido e pronto para uso.";
                }
            }
            else
            {
                IsTemplateValid = false;
                ValidationMessage = error;
            }
        }

        private bool CanExecuteOnSelected(object? obj) => SelectedTemplate != null;

        private void ExecuteNewTemplate(object? obj)
        {
            int nextNumber = Templates.Count + 1;
            var newTemplate = new TemplateDefinition
            {
                Name = $"Novo Template {nextNumber}",
                FileExtension = ".csv",
                DefaultSeparator = ";",
                UseFileNameForPartName = true,
                UseFileNameForBatchNumber = true
            };

            Templates.Add(newTemplate);
            SelectedTemplate = newTemplate;
        }

        private void ExecuteDuplicateTemplate(object? obj)
        {
            if (SelectedTemplate == null) return;

            var json = JsonSerializer.Serialize(SelectedTemplate);
            var clone = JsonSerializer.Deserialize<TemplateDefinition>(json);

            if (clone != null)
            {
                clone.Id = Guid.NewGuid().ToString();
                clone.Name = $"{SelectedTemplate.Name} (Cópia)";
                Templates.Add(clone);
                SelectedTemplate = clone;
            }
        }

        private void ExecuteDeleteTemplate(object? obj)
        {
            if (SelectedTemplate == null) return;

            var result = ModernDialogWindow.Show(
                $"Deseja realmente excluir o template '{SelectedTemplate.Name}'?",
                "Excluir Template",
                ModernDialogButtons.YesNo,
                ModernDialogIcon.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var templateToDelete = SelectedTemplate;
                _repository.DeleteTemplate(templateToDelete.Name);
                Templates.Remove(templateToDelete);

                SelectedTemplate = Templates.FirstOrDefault();
            }
        }

        private void ExecuteRemoveRule(object? obj)
        {
            if (obj is TemplateLineRule rule && SelectedTemplate != null)
            {
                SelectedTemplate.LineRules.Remove(rule);
                RefreshCurrentRules();
                UpdateValidationStatus();
                RulesChanged?.Invoke();
                UpdatePreview();
            }
        }

        private void ExecuteBrowseSampleFile(object? obj)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Selecionar Arquivo de Amostra",
                Filter = "Arquivos de Dados (*.csv;*.txt;*.dat)|*.csv;*.txt;*.dat|Todos os Arquivos (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSampleFile(dialog.FileName);
            }
        }

        public void LoadSampleFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                SampleFilePath = filePath;
                SampleFileContent = EncodingDetector.DetectAndReadAllText(filePath);
                SampleFileLoaded?.Invoke(SampleFileContent);
                UpdatePreview();
            }
            catch (Exception ex)
            {
                ModernDialogWindow.Show(
                    $"Erro ao ler o arquivo de amostra:\n\n{ex.Message}",
                    "Erro de Leitura",
                    ModernDialogButtons.OK,
                    ModernDialogIcon.Error);
            }
        }

        public void ApplyRule(LineRuleRole role, int lineIndex, string fullLine, string selectedText, int selectionStart, int selectionLength)
        {
            if (SelectedTemplate == null) return;

            var rule = TemplateRuleGenerator.GenerateRule(
                role, lineIndex, fullLine, selectedText, selectionStart, selectionLength, SelectedTemplate);

            TemplateRuleGenerator.ApplyRule(SelectedTemplate, rule);

            RefreshCurrentRules();
            UpdateValidationStatus();
            RulesChanged?.Invoke();
            UpdatePreview();
        }

        public void RemoveRuleForLine(int lineIndex)
        {
            if (SelectedTemplate == null) return;

            TemplateRuleGenerator.RemoveRulesForLine(SelectedTemplate, lineIndex);

            RefreshCurrentRules();
            UpdateValidationStatus();
            RulesChanged?.Invoke();
            UpdatePreview();
        }

        public void UpdatePreview()
        {
            if (!HasSampleFile || string.IsNullOrWhiteSpace(SampleFilePath) || !File.Exists(SampleFilePath) || SelectedTemplate == null)
            {
                PreviewPartName = "-";
                PreviewBatchNumber = "-";
                PreviewResults.Clear();
                PreviewItemCount = 0;
                HasPreviewResults = false;
                PreviewStatusMessage = "Carregue um arquivo de amostra para visualizar o resultado.";
                return;
            }

            try
            {
                var data = _engine.Execute(SampleFilePath, SelectedTemplate);

                PreviewPartName = string.IsNullOrWhiteSpace(data.PartName) ? "-" : data.PartName;
                PreviewBatchNumber = string.IsNullOrWhiteSpace(data.BatchNumber) ? "-" : data.BatchNumber;

                PreviewResults.Clear();
                if (data.Results != null)
                {
                    foreach (var kvp in data.Results)
                    {
                        PreviewResults.Add(new TemplatePreviewItem
                        {
                            FeatureName = kvp.Key,
                            Value = kvp.Value
                        });
                    }
                }

                PreviewItemCount = PreviewResults.Count;
                HasPreviewResults = PreviewResults.Count > 0;
                PreviewStatusMessage = HasPreviewResults ? string.Empty : "Nenhuma característica ou valor mapeado com as regras atuais.";
            }
            catch (Exception ex)
            {
                PreviewPartName = "-";
                PreviewBatchNumber = "-";
                PreviewResults.Clear();
                PreviewItemCount = 0;
                HasPreviewResults = false;
                PreviewStatusMessage = $"Prévia pendente ou incompleta: {ex.Message}";
            }
        }

        /// <summary>
        /// Ação customizável para exibição de avisos de validação (útil para testes unitários automatizados).
        /// </summary>
        public Action<string, string>? ShowWarningAction { get; set; }

        private void ExecuteSaveAndApply(object? obj)
        {
            if (SelectedTemplate == null) return;

            if (!SelectedTemplate.IsValid(out string error))
            {
                if (ShowWarningAction != null)
                {
                    ShowWarningAction(error, "Validação de Template");
                }
                else
                {
                    ModernDialogWindow.Show(
                        $"Não foi possível salvar o template:\n\n{error}",
                        "Validação de Template",
                        ModernDialogButtons.OK,
                        ModernDialogIcon.Warning);
                }
                return;
            }

            // Persiste todos os templates gerenciados no disco
            foreach (var template in Templates)
            {
                if (string.IsNullOrWhiteSpace(template.Name))
                {
                    template.Name = "Novo Template";
                }
                _repository.SaveTemplate(template);
            }

            // Aplica no parser em memória
            _parser?.SetActiveTemplate(SelectedTemplate);

            RequestClose?.Invoke(true);
        }

        private void ExecuteCancel(object? obj)
        {
            RequestClose?.Invoke(false);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
