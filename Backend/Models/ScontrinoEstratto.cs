using System;

namespace ErgonApi.Models
{
    public class ScontrinoEstratto
    {
        public string NumeroDocumento { get; set; }
        public string RagioneSociale { get; set; }
        public DateTime? DataDocumento { get; set; }
        public double? ImportoTotale { get; set; }
        public string OraDocumento { get; set; }
        public string PartitaIva { get; set; }
    }
}