using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ErgonApi.Models
{
    public class Parametro
    {
        // credenziali obbligatorie, per validare l'operazione
        public Credenziali credenziali { get; set; }
        // parametri opzionali, utilizzati in base alla richiesta fatta
        // quelli non utilizzati sono a 'null' di default
        public List<Timbratura> timbrature { get; set; }
        public QueryPlanning query_planning { get; set; }
        public DateTime? from { get; set; }
        public List<Planning> planning { get; set; }
        public Prenotazione prenotazione { get; set; }
        public int? idPrenotazione { get; set; }
        public List<SpesaDettaglio> note_spesa { get; set; }
        public RichiestaAnalisi richiesta_analisi { get; set; }
    }
}