using System;

namespace ErgonApi.Models
{
    // Modello la spesa
    public class SpesaDettaglio
    {
        public int id { get; set; }
        public int cod_dip { get; set; }
        public int cod_cli { get; set; }
        public DateTime da_data { get; set; }
        public DateTime a_data { get; set; }
        public string tipologia { get; set; }
        public string tipo_tab_tns { get; set; }
        public int nr_dip_ergon { get; set; }
        public string flag_con_cli { get; set; }
        public string foto_scontrino { get; set; }
        public double importo { get; set; }
        public string divisa { get; set; }
        public string flag_tipo_pag { get; set; }
        public string nr_doc_scontrino { get; set; }
        public DateTime? data_scontrino { get; set; }
        public string rag_soc_scontrino { get; set; }
        public string note { get; set; }
        public string flag_cont { get; set; }
        public string partita_iva { get; set; }
    }
}