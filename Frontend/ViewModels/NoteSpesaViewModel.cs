using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ergon.Models;
using Ergon.Services;
using Ergon.Views;
using Mopups.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;

namespace Ergon.ViewModels
{
    public partial class NoteSpesaViewModel: ViewModel
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MostraMessaggioVuoto))]
        private bool _isLoading;
        private bool _isInitialized = false;
        private bool _isFirstOpening = true;
        private bool _popupSincroMostrato = false;

        private List<SpesaDettaglio> _noteDaSincronizzare = [];

        public bool MostraMessaggioVuoto => 
            _isInitialized && 
            !IsLoading && 
            (ResocontiMensili == null || ResocontiMensili.Count == 0);

        // Lista anni da mostrare nel menu a tendina
        [ObservableProperty] private ObservableCollection<string> _anniDisponibili;

        // Anno attuale selezionato
        [ObservableProperty] private string? _annoSelezionato;

        // Lista dei resoconti mostrati
        [ObservableProperty] private ObservableCollection<ResocontoMensile> _resocontiMensili;

        public bool HasLocaleNotes => ResocontiMensili?.Any(r => r.HasLocale) ?? false;

        public NoteSpesaViewModel()
        {
            ResocontiMensili = [];
            AnniDisponibili = [];
            IsLoading = false;
        }

        public async Task InizializzaDatiAsync()
        {
            if (IsLoading) return;

            try
            {
                if (ResocontiMensili.Count == 0) IsLoading = true;

                // Carico i dati già presenti sul dispositivo
                await AggiornaAnniDisponibili();
                await AggiornaListaUIAsync(showLoader: false);
                _isInitialized = true;

                // Controllo presenza note non sincronizzate
                if (_isFirstOpening)
                {
                    _isFirstOpening = false;
                    _ = ControllaNoteNonSincronizzateAsync();
                }

                if (HasInternetConnection)
                {
                    // Provo ad aggiornare dal server
                    bool ok = await RestService.GetNoteSpesa(true);

                    if (ok)
                    {
                        // Se ci sono nuovi dati, aggiorno
                        await AggiornaAnniDisponibili();
                        await AggiornaListaUIAsync(showLoader: false);
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Errore init: {e.Message}");
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(MostraMessaggioVuoto));
            }
        }

        private async Task AggiornaAnniDisponibili()
        {
            var anniPresenti = await Task.Run(() =>
            {
                return App.Database.GetAll<SpesaDettaglio>()
                    .Select(x => x.da_data.Year.ToString())
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToList();
            });

            anniPresenti.Insert(0, "Tutti");

            if (!AnniDisponibili.SequenceEqual(anniPresenti))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AnniDisponibili = new ObservableCollection<string>(anniPresenti);
                });
            }

            string annoCorrente = DateTime.Today.Year.ToString();
            if (string.IsNullOrEmpty(AnnoSelezionato))
            {
                if (AnniDisponibili.Contains(annoCorrente))
                {
                    AnnoSelezionato = annoCorrente;
                }
                else
                {
                    // Se non ci sono spese nell'anno in corso, mostro "Tutti"
                    AnnoSelezionato = "Tutti";
                }
            }
        }

        private async Task AggiornaListaUIAsync(bool showLoader = true)
        {
            if (showLoader) IsLoading = true;

            var raggruppamentoMensile = await Task.Run(() =>
            {
                var spese = App.Database.GetAll<SpesaDettaglio>();

                return spese
                    .Where(x => AnnoSelezionato == "Tutti" || x.da_data.Year.ToString() == AnnoSelezionato)
                    .GroupBy(x => new { x.da_data.Year, x.da_data.Month })
                    .OrderByDescending(g => g.Key.Year)
                    .ThenByDescending(g => g.Key.Month)
                    .Select(gruppo => {
                        string nomeMese = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(gruppo.Key.Month);
                        nomeMese = char.ToUpper(nomeMese[0]) + nomeMese.Substring(1);

                        return new ResocontoMensile
                        {
                            MeseNome = $"{nomeMese} {gruppo.Key.Year}",
                            Totale = gruppo.Sum(s => s.importo),
                            NumeroSpese = gruppo.Count(),
                            HasLocale = gruppo.Any(s => s.IsLocale)
                        };
                    })
                    .ToList();
            });

            ResocontiMensili = new ObservableCollection<ResocontoMensile>(raggruppamentoMensile);

            OnPropertyChanged(nameof(HasLocaleNotes));
            if (showLoader) IsLoading = false;
            OnPropertyChanged(nameof(MostraMessaggioVuoto));
        }

        partial void OnAnnoSelezionatoChanged(string? value)
        {
            if (!_isInitialized || value == null) return;
            _ = AggiornaListaUIAsync();
        }

        [RelayCommand]
        private static async Task AggiungiNotaSpesaAsync()
        {
            var paginaInserimento = new AddNotaSpesaView();
            await Shell.Current.Navigation.PushAsync(paginaInserimento);
        }

        [RelayCommand]
        private static async Task ApriDettaglioMeseAsync(ResocontoMensile resoconto)
        {
            if (resoconto == null || string.IsNullOrEmpty(resoconto.MeseNome)) return;

            await Task.Delay(150);

            var paginaDettaglioMese = new DettaglioMeseView(resoconto.MeseNome);
            await Shell.Current.Navigation.PushAsync(paginaDettaglioMese);
        }

        private async Task ControllaNoteNonSincronizzateAsync()
        {
            if (_popupSincroMostrato || !HasInternetConnection) return;

            try
            {
                _popupSincroMostrato = true;

                _noteDaSincronizzare = await Task.Run(() =>
                    App.Database.GetAll<SpesaDettaglio>().Where(x => x.IsLocale).ToList());

                if (_noteDaSincronizzare == null || !_noteDaSincronizzare.Any()) return;
                
                var popupSincro = new GenericPopupView(
                    "Sincronizzazione",
                    "Hai delle note in locale non ancora inviate al server. \n\nVuoi procedere con la sincronizzazione?",
                    Constants.SYNC_ALERT_ICON,
                    Colors.Orange,
                    "SINCRONIZZA",
                    "NON ORA");
                await MopupService.Instance.PushAsync(popupSincro);

                bool sincronizza = await popupSincro.RispostaTask.Task;

                if (sincronizza)
                {
                    await SincronizzaInUscitaAsync();
                }
                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore durante il controllo sincronizzazione: {ex.Message}");
            }
            finally
            {
                _popupSincroMostrato = false;
            }
        }
        private async Task SincronizzaInUscitaAsync()
        {
            if (_noteDaSincronizzare == null || !_noteDaSincronizzare.Any()) return;

            IsLoading = true;
            try
            {
                var noteNuove = _noteDaSincronizzare.Where(x => x.id_server == 0).ToList();
                var noteModificate = _noteDaSincronizzare.Where(x => x.id_server > 0).ToList();

                var mappaFotoInUscita = noteNuove
                    .Where(x => !string.IsNullOrWhiteSpace(x.path_scontrino_loc))
                    .Select(x => new
                    {
                        Importo = x.importo,
                        Tipologia = x.tipologia,
                        Data = x.da_data.Date,
                        CodCli = x.cod_cli,
                        PathLocale = x.path_scontrino_loc
                    })
                    .ToList();

                var idServerGiaPresenti = await Task.Run(() => App.Database.GetAll<SpesaDettaglio>()
                                            .Select(x => x.id_server)
                                            .ToHashSet());

                bool tuttoOk = true;

                // Gestione note nuove
                if (noteNuove.Count > 0)
                {
                    bool esitoNuove = await RestService.SalvaNoteSpesa(noteNuove);
                    if (esitoNuove)
                    {
                        await Task.Run(() => App.Database.Delete<SpesaDettaglio>(x => x.IsLocale == true && x.id_server == 0));
                    }
                    else
                    {
                        tuttoOk = false;
                    }
                }

                // Gestione note modificate
                if (noteModificate.Count > 0)
                {
                    bool esitoModifica = await RestService.ModificaNotaSpesaBatch(noteModificate);

                    if (esitoModifica)
                    {
                        foreach (var nota in noteModificate)
                        {
                            nota.IsLocale = false;
                        }
                        await Task.Run(() => App.Database.UpdateAll(noteModificate));
                    }
                    else
                    {
                        tuttoOk = false;
                    }
                }

                if (tuttoOk)
                {
                    bool syncOk = await RestService.GetNoteSpesa(true);

                    if (syncOk)
                    {
                        if (mappaFotoInUscita.Count > 0)
                        {
                            var noteAppenaScaricate = await Task.Run(() => App.Database.GetAll<SpesaDettaglio>()
                                .Where(x => x.id_server > 0 && !idServerGiaPresenti.Contains(x.id_server))
                                .ToList());

                            foreach (var snapshot in mappaFotoInUscita)
                            {
                                var match = noteAppenaScaricate.FirstOrDefault(n =>
                                    n.importo == snapshot.Importo &&
                                    n.tipologia == snapshot.Tipologia &&
                                    n.da_data.Date == snapshot.Data &&
                                    n.cod_cli == snapshot.CodCli);

                                if (match != null)
                                {
                                    match.path_scontrino_loc = snapshot.PathLocale;
                                    await Task.Run(() => App.Database.Update(match));

                                    noteAppenaScaricate.Remove(match);
                                }
                            }
                        }

                        await ShowToast("Note inviate e database aggiornato!");
                        await AggiornaAnniDisponibili();
                        await AggiornaListaUIAsync();
                    }
                }
                else
                {
                    await DisplayAlertError("Alcune note non sono state sincronizzate. Riprova più tardi.");
                    await AggiornaAnniDisponibili();
                    await AggiornaListaUIAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore durante sincro in uscita: {ex.Message}");
                await DisplayAlertError("Errore di comunicazione con il server.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CaricaTutteSpeseAsync()
        {
            if (!HasInternetConnection)
            {
                await DisplayAlertWarning("Nessuna connessione internet. Impossibile sincronizzare le note in questo momento.");
                return;
            }

            var noteLocali = await Task.Run(() =>
                App.Database.GetAll<SpesaDettaglio>().Where(x => x.IsLocale).ToList());

            if (!(noteLocali.Count > 0)) return;

            var popup = new PopupRiepilogoSincroView(noteLocali);
            await MopupService.Instance.PushAsync(popup);

            bool conferma = await popup.Risultato.Task;

            if (conferma)
            {
                _noteDaSincronizzare = noteLocali;
                await SincronizzaInUscitaAsync();
            }
        }
    }
}
