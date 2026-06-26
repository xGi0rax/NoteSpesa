using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ergon.Models;
using Ergon.Services;
using Ergon.Views;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Ergon.ViewModels
{
    public partial class DettaglioMeseViewModel : ObservableObject
    {
        [ObservableProperty] private string _meseAnno;
        [ObservableProperty] private ObservableCollection<SpesaDettaglio> _listaSpese = new();
        [ObservableProperty] private string? _titoloPagina;
        [ObservableProperty] private bool _isRefreshing;

        // Easter egg
        private int _counter = 0;
        private DateTime? _ultimoTrascinamento = null;

        public DettaglioMeseViewModel(string meseAnno)
        {
            MeseAnno = meseAnno;
        }

        partial void OnMeseAnnoChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            TitoloPagina = value;
            CaricaSpeseDelMese(value);
        }

        private void CaricaSpeseDelMese(string filtro)
        {
            if (string.IsNullOrEmpty(filtro)) return;

            try
            {
                var parti = filtro.Split(' ');
                if (parti.Length < 2) return;

                string nomeMese = parti[0];
                int anno = int.Parse(parti[1]);

                int meseNum = DateTime.ParseExact(nomeMese, "MMMM", CultureInfo.CurrentCulture).Month;

                if (App.Database == null) return;

                DateTime inizioMese = new DateTime(anno, meseNum, 1);
                DateTime fineMese = inizioMese.AddMonths(1).AddDays(-1);

                var spese = App.Database.GetFiltered<SpesaDettaglio>(x =>
                        x.da_data >= inizioMese &&
                        x.da_data <= fineMese)
                    .OrderByDescending(x => x.da_data)
                    .ThenByDescending(x => x.id)
                    .ToList();

                var codiciClientiUsati = spese.Select(s => s.cod_cli).Distinct().ToList();
                var clienti = App.Database.GetAll<Cliente>()
                    .Where(c => codiciClientiUsati.Contains(c.cod_cli))
                    .ToList();

                var textInfo = CultureInfo.CurrentCulture.TextInfo;

                foreach (var s in spese)
                {
                    var clienteSpesa = clienti.FirstOrDefault(c => c.cod_cli == s.cod_cli);
                    s.ClienteNome = clienteSpesa?.rag_soc ?? $"Cliente {s.cod_cli} (Non trovato)";

                    if (!string.IsNullOrWhiteSpace(s.tipologia))
                    {
                        s.tipologia = textInfo.ToTitleCase(s.tipologia.Trim().ToLower());
                    }
                }

                ListaSpese = new ObservableCollection<SpesaDettaglio>(spese);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERRORE: {ex.Message}");
            }
        }

        [RelayCommand]
        private static async Task AggiungiNotaSpesaAsync()
        {
            var paginaInserimento = new AddNotaSpesaView();
            await Shell.Current.Navigation.PushAsync(paginaInserimento);
        }

        [RelayCommand]
        private static async Task ModificaSpesaAsync(SpesaDettaglio spesa)
        {
            if (spesa == null) return;

            await Task.Delay(150);

            var editPage = new AddNotaSpesaView();
            if (editPage.BindingContext is AddNotaSpesaViewModel vm)
            {
                vm.CaricaSpesa(spesa);
            }

            await Shell.Current.Navigation.PushAsync(editPage);
        }
    }
}
