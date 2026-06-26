using Ergon.Services;
using Ergon.Views;
namespace Ergon
{
    public partial class AppShell : Shell
    {
        private bool ConfermaUscita = true;
        public AppShell()
        {
            InitializeComponent();

            btn_oggi.Icon = $"day{DateTime.Today.Day}";
        }
        private async void Logout(object sender, EventArgs e)
        {
            var ris = await DisplayAlert("Attenzione", "Sei sicuro di voler uscire dall'applicazione?", "Ok", "Annulla");
            if (ris)
            {
                Settings.IsLogged = false;
                if(Application.Current != null) Application.Current.MainPage = new LoginView();
            }
        }
        protected override async void OnNavigating(ShellNavigatingEventArgs args)
        {
            if (args.Current != null)
            {
                if ((App.Current?.MainPage as AppShell)?.CurrentPage is PlanningView page && page.ChiediConferma)// && !args.Target.Location.OriginalString.Contains("ClientiListView"))
                {
                    if (ConfermaUscita)
                    {
                        args.Cancel();
                        if (await DisplayAlert("ATTENZIONE", "Uscire senza salvare?", "SI", "NO"))
                        {
                            ConfermaUscita = false;
                            await Shell.Current.GoToAsync(args.Target.Location.OriginalString);
                        }
                    }
                }
            }
        }
        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            if (args.Previous != null)
            {
                if (args.Previous.Location.OriginalString.Contains(nameof(PlanningView)))
                    ConfermaUscita = true;
            }
            base.OnNavigated(args);
        }
    }
}
