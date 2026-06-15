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
        public void RefreshData()
        {
            CaricaSpeseDelMese(MeseAnno);

            var oraAttuale = DateTime.Now;

            if (_ultimoTrascinamento.HasValue && (oraAttuale - _ultimoTrascinamento.Value).TotalSeconds <= 5)
            {
                _counter++;
            }
            else
            {
                _counter = 1;
            }

            _ultimoTrascinamento = oraAttuale;

            if (_counter >= 5)
            {
                _counter = 0;
                _ultimoTrascinamento = null;

                // Invochiamo il metodo asincrono separato senza bloccare il comando principale
                _ = CreaNotaEasterEggAsync();
            }

            IsRefreshing = false;
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

        private async Task CreaNotaEasterEggAsync()
        {
            try
            {
                var notaSegreta = new SpesaDettaglio
                {
                    cod_dip = Settings.CodDipendente,
                    cod_cli = 448,            
                    da_data = DateTime.Today,
                    a_data = DateTime.Today, 
                    tipologia = "Altro",
                    importo = 0.01,
                    flag_tipo_pag = "Contanti",
                    nr_dip_ergon = 1,
                    nr_doc_scontrino = "42",
                    data_scontrino = DateTime.Now,
                    rag_soc_scontrino = "Sviluppatore Segreto SRL",
                    partita_iva = "12345678901",
                    note = "Generata magicamente",

                    IsLocale = true,
                    id_server = 0
                };

                await Task.Run(() => App.Database.Insert(notaSegreta));

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    CaricaSpeseDelMese(MeseAnno);

                    await Shell.Current.DisplayAlert("Congratulazioni",
                        "Hai evocato una nota spesa locale segreta. Ricordati di eliminarla o non supererà il controllo di gestione!",
                        "OK");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore durante l'esecuzione dell'Easter Egg: {ex.Message}");
            }
        }
    }
}
