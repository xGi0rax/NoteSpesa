using Ergon.Services;
using Ergon.Views;

namespace Ergon
{
    public partial class App : Application
    {
        private static ErgDatabase? _database;
        public static ErgDatabase Database
        {
            get
            {
                _database ??= new ErgDatabase();
                return _database;
            }
        }
        public App()
        {
            InitializeComponent();
            Settings.ControllaVersioneApp = true;

            // forzo un reset completo al 08/10/2025
            if (DateTime.Now.TimeOfDay >= Constants.MIN_ORARIO_SYNC)
            {
                // forzo il reset dopo le 8 di mattina, altrimenti non 
                if (Settings.ResetCompleto)
                {
                    Settings.ResetCompleto = false;
                    App.Database.DropAllTables();
                    Settings.LastSync = DateTime.MinValue;
                }
            }

            if (Settings.IsLogged)
            {
                if(Settings.LastSync == DateTime.MinValue)
                {
                    // prima sincronizzazione
                    MainPage = new LoadView(false);
                }
                else if(Settings.LastSync.ToLocalTime() < DateTime.Now.AddHours(-1))
                {
                    // è passata un'ora dall'ultima sincronizzazione => sincronizzo
                    MainPage = new LoadView(true);
                }
                else
                {
                    MainPage = new AppShell();
                }
            }
            else
            {
                MainPage = new LoginView();
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (Current?.MainPage is AppShell { CurrentPage: OggiView view })
            {
                view.CheckDataPagina();
            }
        }
    }
}
