using SQLite;

namespace Ergon.Models
{
    // Modello per la tabella locale SQLite
    public class SpesaTipologia
    {
        [PrimaryKey]
        public string Descrizione { get; set; }
    }

    // Modello per la tabella locale SQLite
    public class SpesaTipoPagamento
    {
        [PrimaryKey]
        public string Descrizione { get; set; }
    }

    // Classe usata dal RestService per deserializzare il JSON del backend
    public class DecodificheResponse
    {
        public List<SpesaTipologia> Tipologie { get; set; } = new();
        public List<SpesaTipoPagamento> MetodiPagamento { get; set; } = new();
    }
}