using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SelectML.Core;

namespace SelectML.Parsers.ViciVision
{
    public class ViciX5Parser : IMachineParser
    {
        public string MachineName => "ViciVision X5 - CSV";

        public bool CanParse(string filePath)
        {
            // Validação simples: deve ser CSV
            return Path.GetExtension(filePath).ToLower() == ".csv";
        }

        public MeasurementData Parse(string filePath)
        {
            var data = new MeasurementData();
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // 1. Nome da Peça
            var nameParts = fileName.Split('_');
            if (nameParts.Length > 0)
            {
                data.PartName = nameParts[0];
            }

            // CORREÇÃO AQUI: Forçar leitura em Latin1 (ANSI/ISO-8859-1)
            // Isso garante que Ø, °, µ e outros símbolos sejam lidos corretamente.
            var lines = File.ReadAllLines(filePath, Encoding.Latin1);

            // Validação básica de estrutura
            if (lines.Length < 5)
                throw new Exception("Arquivo ViciVision inválido ou incompleto.");

            // Localizar linhas
            string headerLine = lines[0];
            string valueLine = lines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l));

            if (valueLine == null) throw new Exception("Linha de dados não encontrada.");

            var headers = headerLine.Split(',');
            var values = valueLine.Split(',');

            // 2. Número de Lote
            if (values.Length > 2)
            {
                data.BatchNumber = values[2].Trim();
            }

            // Data da medição
            if (values.Length > 1)
            {
                if (DateTime.TryParse($"{values[0]} {values[1]}", out DateTime dt))
                    data.MeasureDate = dt;
                else
                    data.MeasureDate = DateTime.Now;
            }

            // 3 e 4. Características e Valores
            for (int i = 3; i < headers.Length; i++)
            {
                if (i >= values.Length) break;

                string characteristicName = headers[i].Trim();
                string valueString = values[i].Trim();

                if (string.IsNullOrEmpty(characteristicName)) continue;

                if (double.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out double measuredValue))
                {
                    if (!data.Results.ContainsKey(characteristicName))
                    {
                        data.Results.Add(characteristicName, measuredValue);
                    }
                }
            }

            return data;
        }
    }
}