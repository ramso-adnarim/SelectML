namespace SelectML.Client
{
    public class ResultItem
    {
        public string Characteristic { get; set; }
        public double Value { get; set; }
        public bool IsRecognized { get; set; } = true;
    }
}
