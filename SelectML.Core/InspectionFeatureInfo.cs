namespace SelectML.Core
{
    public class InspectionFeatureInfo
    {
        public int OrderNo { get; set; }
        public int FeatureID { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public short FeatureType { get; set; }
        public string TipoDescricao { get; set; } = string.Empty;
        public double? Nominal { get; set; }
        public double? LSL { get; set; }
        public double? USL { get; set; }
        public int? DecimalLength { get; set; }

        public bool IsAttribute => FeatureType == 2 || FeatureType == 4;
    }
}
