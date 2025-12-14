namespace SelectML.Core
{
    /// <summary>
    /// O contrato que toda DLL de máquina deve seguir.
    /// </summary>
    public interface IMachineParser
    {
        /// <summary>
        /// Nome amigável da máquina para aparecer no ComboBox da UI.
        /// </summary>
        string MachineName { get; }

        /// <summary>
        /// Verifica se este parser consegue ler o arquivo (validação rápida por extensão ou nome).
        /// </summary>
        bool CanParse(string filePath);

        /// <summary>
        /// Realiza a leitura do arquivo e retorna os dados estruturados.
        /// </summary>
        MeasurementData Parse(string filePath);
    }
}