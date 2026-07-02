using System;
using System.IO;
using System.Text.Json;
using SelectML.Core;

namespace SelectML.Parsers.ViciVisionJson
{
    public class ViciJsonParser : IMachineParser
    {
        public string MachineName => "ViciVision X5 - JSON";

        public bool CanParse(string filePath)
        {
            return Path.GetExtension(filePath).ToLower() == ".json";
        }

        public MeasurementData Parse(string filePath)
        {
            var data = new MeasurementData();
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var nameParts = fileName.Split('_');

            if (nameParts.Length > 0)
            {
                data.PartName = nameParts[0].Trim();
            }

            if (nameParts.Length > 1)
            {
                data.BatchNumber = nameParts[1].Trim();
            }

            string jsonContent = File.ReadAllText(filePath);
            
            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("Measurement Cycle", out JsonElement measureCycle))
                {
                    if (measureCycle.TryGetProperty("General", out JsonElement general))
                    {
                        if (general.TryGetProperty("Date", out JsonElement dateEl) && 
                            general.TryGetProperty("Hour", out JsonElement hourEl))
                        {
                            string dateStr = dateEl.GetString() ?? string.Empty;
                            string hourStr = hourEl.GetString() ?? string.Empty;
                            if (DateTime.TryParse($"{dateStr} {hourStr}", out DateTime dt))
                            {
                                data.MeasureDate = dt;
                            }
                            else
                            {
                                data.MeasureDate = DateTime.Now;
                            }
                        }
                    }

                    if (measureCycle.TryGetProperty("Measurements", out JsonElement measurements))
                    {
                        foreach (JsonProperty measurementProp in measurements.EnumerateObject())
                        {
                            var measurementNode = measurementProp.Value;
                            if (measurementNode.TryGetProperty("Name", out JsonElement nameEl) &&
                                measurementNode.TryGetProperty("Result", out JsonElement resultEl))
                            {
                                string name = nameEl.GetString() ?? string.Empty;
                                if (resultEl.TryGetDouble(out double value))
                                {
                                    if (!string.IsNullOrEmpty(name) && !data.Results.ContainsKey(name))
                                    {
                                        data.Results.Add(name, value);

                                        // Read validation / tolerances
                                        if (measurementNode.TryGetProperty("Validation", out JsonElement validationEl))
                                        {
                                            double nominal = 0;
                                            double lower = 0;
                                            double upper = 0;

                                            if (validationEl.TryGetProperty("Nominal value", out JsonElement nomEl))
                                                nomEl.TryGetDouble(out nominal);
                                            if (validationEl.TryGetProperty("Lower tolerance", out JsonElement lowEl))
                                                lowEl.TryGetDouble(out lower);
                                            if (validationEl.TryGetProperty("Upper tolerance", out JsonElement upEl))
                                                upEl.TryGetDouble(out upper);

                                            data.Tolerances[name] = new CharacteristicTolerance
                                            {
                                                Nominal = nominal,
                                                LowerTolerance = lower,
                                                UpperTolerance = upper
                                            };
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return data;
        }
    }
}
