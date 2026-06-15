namespace Ergon.Models
{
    public class RisultatoAnalisi
    {
        public bool IsSuccess { get; set; }
        public ScontrinoEstratto? Dati { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsNetworkError { get; set; }
        public bool IsServerError { get; set; }
    }
}
