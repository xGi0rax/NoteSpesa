namespace Ergon.Services
{
    public class Constants
    {
        // TEST
        //public const string URL = "https://test3.ergon.it/"; // srv-web3 esterno
        //public const string URL = "http://172.30.1.42:35424"; // srv-web3

        //public const string URL = "https://ergteam.ergon.it"; // in linea

        // TEST LOCALE (NGROK)
        public const string URL = "https://maia-brachistochronic-brutely.ngrok-free.dev";

        public const string AES_CHECKCODE = "rqdRAuL6yQ";
        public const string BASIC_AUTH = "ergonMobile:pW0K8ZI1DzayIXQPSJOvBb3ThjUYMAFlEFAIVBiiASkQMiqlv0FosiJ8gTtW7wyb";
        public const string DB_NAME = "local_db_ergon.db3";
        public const int USER_STORE = 997; // utente di test degli store
        public static readonly TimeSpan MIN_ORARIO_SYNC = new(8, 0, 0);

        #region ENDPOINT
        public const string LOGIN = "api/auth/login";
        public const string TIMBRATURA = "api/timbrature";
        public const string TIMBRATURE_PERSONALI = "api/timbrature/personali";
        public const string PLANNING = "api/planning";
        public const string PLANNING_SALVA = "api/planning/salva";
        public const string FAQ = "api/faq";
        public const string LIBRO_PRESENZE = "api/presenze";
        public const string ANAVOCI = "api/presenze/legenda";
        public const string DIPENDENTI = "api/dipendenti";
        public const string TABGEN = "api/tabgen";
        public const string CLIENTI = "api/clienti";
        public const string CALENDARIO = "api/calendario";
        public const string PLANNING_COPIA = "api/planning/copia";
        public const string PLANNING_SPOSTA = "api/planning/sposta";
        public const string PRENOTAZIONI_RISORSE = "api/prenotazioni";
        public const string INSERISCI_PRENOTAZIONE= "api/prenotazioni/add";

        public const string NOTE_SPESA = "api/note-spesa";
        public const string NOTE_SPESA_SALVA = "api/note-spesa/add";
        public const string NOTE_SPESA_MODIFICA = "api/note-spesa/update";
        public const string NOTE_SPESA_ANALIZZA = "api/note-spesa/analyze";
        #endregion

        #region ICONS
        public const string USER_ICON = "\ue7fd";
        public const string PASSWORD_ICON = "\uf042";
        public const string DESCRIPTION_ICON = "\ue873";
        public const string EVENT_ICON = "\ue878";
        public const string LEFT_ARROW_ICON = "\uf1e6";
        public const string RIGHT_ARROW_ICON = "\uf1df";
        public const string CALL_ICON = "\ue0b0";
        public const string PLAY_ICON = "\ue1c4";
        public const string STOP_ICON = "\uef71";
        public const string TIME_ICON = "\ue8b5";
        public const string MORE_TIME_ICON = "\uea5d";
        public const string DOOR_OPEN_ICON = "\ueb4f";
        public const string CAR_ICON = "\ue531";
        public const string MOON_ICON = "\ue51c";
        public const string QUESTION_ICON = "\ue887";
        public const string NOTES_ICON = "\ue873";
        public const string LIST_ICON = "\ue896";
        public const string REFER_ICON = "\ue8d3";
        public const string COLLAPSE_ICON = "\ue5ce";
        public const string EXPANDE_ICON = "\ue5cf";
        public const string CLOSE_ICON = "\ue5cd";
        public const string GPS_ICON = "\ue87a";
        public const string GSM_ICON = "\ue32b";

        public const string CALENDAR_ICON = "\uebcc";
        public const string RESOURCE_ICON = "\ueb3f";
        public const string GROUPS_ICON = "\uf233";

        public const string ADD_PHOTO_ICON = "\ue439";
        public const string PAYMENTS_ICON = "\uef63";
        public const string WALLET_ICON = "\uf8ff";
        public const string RECEIPT_ICON = "\uef6e";
        public const string NOT_SYNC_ICON = "\ue2c1";
        public const string SYNC_ALERT_ICON = "\ue629";
        public const string UPLOAD_ICON = "\ue2c3";
        public const string DELETE_ICON = "\ue872";
        public const string WARNING_ICON = "\ue002";
        public const string ERROR_ICON = "\ue000";
        public const string OFFLINE_ICON = "\ue648";
        public const string SAVE_ICON = "\ue161";
        public const string ADD_EXPENSE_ICON = "\ue89c";
        #endregion
    }
}
