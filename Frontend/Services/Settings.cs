using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ergon.Services
{
    public class Settings
    {
        public static bool ResetCompleto
        {
            get => Preferences.Get(nameof(ResetCompleto), true);
            set => Preferences.Set(nameof(ResetCompleto), value);
        }
        public static bool IsLogged
        {
            get => Preferences.Get(nameof(IsLogged), false);
            set => Preferences.Set(nameof(IsLogged), value);
        }
        public static int CodDipendente
        {
            get => Preferences.Get(nameof(CodDipendente), -1);
            set => Preferences.Set(nameof(CodDipendente), value);
        }
        public static string DesDipendente
        {
            get => Preferences.Get(nameof(DesDipendente), string.Empty);
            set => Preferences.Set(nameof(DesDipendente), value);
        }
        public static TimeSpan DaOraNotturno
        {
            // dato che nelle preferences NON posso salvare un timespan, uso data qualsiasi con il timespan desiderato
            get => Preferences.Get(nameof(DaOraNotturno), new DateTime(2000, 7, 12, 19, 0, 0)).TimeOfDay;
            set => Preferences.Set(nameof(DaOraNotturno), new DateTime(2000, 7, 12, value.Hours, value.Minutes, value.Seconds));
        }
        public static TimeSpan AOraNotturno
        {
            // dato che nelle preferences NON posso salvare un timespan, uso data qualsiasi con il timespan desiderato
            get => Preferences.Get(nameof(AOraNotturno), new DateTime(2000, 7, 12, 7, 0, 0)).TimeOfDay;
            set => Preferences.Set(nameof(AOraNotturno), new DateTime(2000, 7, 12, value.Hours, value.Minutes, value.Seconds));
        }
        public static string Username
        {
            get => Preferences.Get(nameof(Username), string.Empty);
            set => Preferences.Set(nameof(Username), value);
        }

        public static string Password
        {
            get => Preferences.Get(nameof(Password), string.Empty);
            set => Preferences.Set(nameof(Password), value);
        }
        public static DateTime LastSync
        {
            get => Preferences.Get(nameof(LastSync), DateTime.MinValue);
            set => Preferences.Set(nameof(LastSync), value);
        }
        //public static DateTime LastSyncLibroPresenze
        //{
        //    get => Preferences.Get(nameof(LastSyncLibroPresenze), DateTime.MinValue);
        //    set => Preferences.Set(nameof(LastSyncLibroPresenze), value);
        //}
        // Serve per aggiornare le prenotazioni, potrebbe essere diverso da LastSync
        // se ad esempio si è verificato un conflitto e sono state rimandate tutte le 
        // prenotazioni per sync e per uno stato coerente
        public static DateTime LastSyncPrenotazioni
        {
            get => Preferences.Get(nameof(LastSyncPrenotazioni), DateTime.MinValue);
            set => Preferences.Set(nameof(LastSyncPrenotazioni), value);
        }
        public static DateTime LastSyncNoteSpesa
        {
            get => Preferences.Get(nameof(LastSyncNoteSpesa), DateTime.MinValue);
            set => Preferences.Set(nameof(LastSyncNoteSpesa), value);
        }
        public static DateTime LastPostponedSave
        {
            get => Preferences.Get(nameof(LastPostponedSave), DateTime.MinValue);
            set => Preferences.Set(nameof(LastPostponedSave), value);
        }
        public static bool AutoStartCheck
        {
            get => Preferences.Get(nameof(AutoStartCheck), false);
            set => Preferences.Set(nameof(AutoStartCheck), value);
        }
        public static bool BackgroundCheck
        {
            get => Preferences.Get(nameof(BackgroundCheck), false);
            set => Preferences.Set(nameof(BackgroundCheck), value);
        }
        public static string FaqHtml
        {
            get => Preferences.Get(nameof(FaqHtml), string.Empty);
            set => Preferences.Set(nameof(FaqHtml), value);
        }
        public static string CalendarioHtml
        {
            get => Preferences.Get(nameof(CalendarioHtml), string.Empty);
            set => Preferences.Set(nameof(CalendarioHtml), value);
        }
        public static string UltimaVersioneAndroid
        {
            get => Preferences.Get(nameof(UltimaVersioneAndroid), string.Empty);
            set => Preferences.Set(nameof(UltimaVersioneAndroid), value);
        }
        public static string UltimaVersioneIOS
        {
            get => Preferences.Get(nameof(UltimaVersioneIOS), string.Empty);
            set => Preferences.Set(nameof(UltimaVersioneIOS), value);
        }
        public static bool ControllaVersioneApp
        {
            get => Preferences.Get(nameof(ControllaVersioneApp), true);
            set => Preferences.Set(nameof(ControllaVersioneApp), value);
        }
    }
}
