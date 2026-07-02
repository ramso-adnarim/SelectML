using System;
using System.Collections.Generic;

namespace SelectML.Core
{
    /// <summary>
    /// Representa o resultado padronizado de uma medição, independente da máquina.
    /// </summary>
    public class MeasurementData
    {
        public string PartName { get; set; }      // Ex: EFF9105.31A
        public string BatchNumber { get; set; }   // Ex: T1
        public DateTime MeasureDate { get; set; } // Data da medição

        // Dicionário onde:
        // Key = Nome da Característica (ex: "PAQ 13.80")
        // Value = Valor Medido (ex: 13.806)
        public Dictionary<string, double> Results { get; set; } = new Dictionary<string, double>();

        // Tolerâncias e nominais para as características
        // Key = Nome da característica
        public Dictionary<string, CharacteristicTolerance> Tolerances { get; set; } = new Dictionary<string, CharacteristicTolerance>();

        public bool IsValid => !string.IsNullOrEmpty(PartName) && Results.Count > 0;
    }

    public class CharacteristicTolerance
    {
        public double Nominal { get; set; }
        public double LowerTolerance { get; set; }
        public double UpperTolerance { get; set; }
    }
}