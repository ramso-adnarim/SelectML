using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SelectML.Core
{
    /// <summary>
    /// Encapsula e decodifica a máscara binária (TraceMask) configurada
    /// nas definições de rastreabilidade do Mitutoyo MeasurLink 10.
    /// </summary>
    public class TraceMaskInfo
    {
        public bool IsEnabled { get; set; }
        public string? RegexPattern { get; set; }
        public string? SampleMask { get; set; }

        /// <summary>
        /// Decodifica o payload binário (.NET BinaryFormatter) armazenado na coluna TraceabilityList.Pattern.
        /// </summary>
        public static TraceMaskInfo? Parse(byte[]? bytes)
        {
            if (bytes == null || bytes.Length < 10)
                return null;

            var info = new TraceMaskInfo();
            int len = bytes.Length;

            // IsEnabled fica localizado antes do byte de encerramento 0x0B do protocolo BinaryFormatter
            if (bytes[len - 1] == 0x0B)
            {
                info.IsEnabled = bytes[len - 2] == 0x01;
            }

            // Extração das strings serializadas (marcador 0x06 do protocolo binário do .NET BinaryFormatter)
            var extractedStrings = new List<string>();
            for (int i = 0; i < len - 6; i++)
            {
                if (bytes[i] == 0x06)
                {
                    int pos = i + 5;
                    if (pos < len)
                    {
                        int strLen = bytes[pos];
                        if (strLen > 0 && pos + 1 + strLen <= len)
                        {
                            try
                            {
                                string text = Encoding.UTF8.GetString(bytes, pos + 1, strLen);
                                extractedStrings.Add(text);
                                i = pos + strLen;
                            }
                            catch
                            {
                                // Continua a busca caso haja erro de encoding em bloco parcial
                            }
                        }
                    }
                }
            }

            if (extractedStrings.Count >= 2)
            {
                info.RegexPattern = extractedStrings[0];
                info.SampleMask = extractedStrings[1];
            }
            else if (extractedStrings.Count == 1)
            {
                info.RegexPattern = extractedStrings[0];
            }

            return info;
        }

        /// <summary>
        /// Valida o texto digitado pelo operador contra a expressão regular configurada.
        /// Retorna true se a máscara estiver desativada, ou se o texto cumprir estritamente o Regex.
        /// </summary>
        public bool Validate(string? input)
        {
            if (!IsEnabled || string.IsNullOrEmpty(RegexPattern))
                return true;

            if (string.IsNullOrEmpty(input))
                return false;

            try
            {
                string cleanPattern = RegexPattern.TrimStart('^').TrimEnd('$');
                return Regex.IsMatch(input, $"^{cleanPattern}$");
            }
            catch
            {
                // Se a expressão regular for inválida ou corrompida, não bloqueia o operador
                return true;
            }
        }
    }
}
