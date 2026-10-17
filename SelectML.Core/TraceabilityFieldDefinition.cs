using System.Collections.Generic;

namespace SelectML.Core
{
    /// <summary>
    /// Representa a definição de um campo de rastreabilidade cadastrado no Mitutoyo MeasurLink 10,
    /// incluindo seu tipo de controle (ItemType), opções pré-definidas e regras de máscara.
    /// </summary>
    public class TraceabilityFieldDefinition
    {
        public int TraceabilityListID { get; set; }
        public string ListName { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;

        /// <summary>
        /// 1 = Selection List (Sem Adição) -> ComboBox estrito
        /// 2 = Selection List (Com Adição) -> ComboBox editável
        /// 3 = Freeform Text / Mask -> TextBox com validação Regex
        /// 4 = Freeform Text with History -> ComboBox com histórico
        /// 6 = User / Operator -> TextBox
        /// </summary>
        public int ItemType { get; set; } = 3;

        public TraceMaskInfo? Mask { get; set; }
        public List<string> PredefinedOptions { get; set; } = new();

        /// <summary>
        /// ItemType 1: Lista fechada sem adição (somente opções cadastradas).
        /// </summary>
        public bool IsStrictSelection => ItemType == 1;

        /// <summary>
        /// ItemType 2: Lista com adição (permite selecionar ou digitar livremente).
        /// </summary>
        public bool IsSelectionWithAdditions => ItemType == 2;

        /// <summary>
        /// Indica se o campo possui opções pré-definidas no catálogo do MeasurLink.
        /// </summary>
        public bool HasPredefinedOptions => PredefinedOptions.Count > 0;
    }
}
