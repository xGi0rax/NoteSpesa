namespace Ergon.Models
{
    public class ResocontoMensile
    {
        public string? MeseNome { get; set; }
        public double Totale { get; set; }
        public int NumeroSpese { get; set; }

        public bool HasLocale { get; set; }

        public string NumeroSpeseTesto => NumeroSpese == 1
            ? "1 nota spesa"
            : $"{NumeroSpese} note spesa";
    }
}