using System.ComponentModel;
using SelectML.Core.Templates;

namespace SelectML.Core
{
    /// <summary>
    /// Contrato para plugins/parsers que suportam configuração dinâmica via templates.
    /// </summary>
    public interface IConfigurableParser : IMachineParser, INotifyPropertyChanged
    {
        /// <summary>
        /// Nome do template atualmente ativo para o parser.
        /// </summary>
        string ActiveTemplateName { get; }

        /// <summary>
        /// Template atualmente carregado (ou nulo se nenhum selecionado).
        /// </summary>
        TemplateDefinition? ActiveTemplate { get; }

        /// <summary>
        /// Define o template ativo diretamente pelo objeto de definição.
        /// </summary>
        void SetActiveTemplate(TemplateDefinition? template);

        /// <summary>
        /// Define o template ativo pelo nome cadastrado.
        /// </summary>
        void SetActiveTemplateByName(string templateName);
    }
}
