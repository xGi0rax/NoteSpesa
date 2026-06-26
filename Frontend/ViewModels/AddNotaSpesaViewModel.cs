using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ergon.Models;
using Ergon.Services;
using Ergon.Views;
using Mopups.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Ergon.ViewModels
{
    public partial class AddNotaSpesaViewModel: ViewModel
    {
        private readonly MediaService _mediaService;
        private SpesaDettaglio? _spesaOriginale;
        private int _codCliIniziale = 0;

        [ObservableProperty]
        private bool _isLoading = false;
        [ObservableProperty]
        private string _loadingText = "Attendere...";
        public bool IsSaved { get; private set; }

        [ObservableProperty] [NotifyPropertyChangedFor(nameof(HaSpeseInLista))]
        private ObservableCollection<SpesaDettaglio> _speseInserite = new();

        // Proprietà della nota
        [ObservableProperty] private string _pageTitle = "Nuova nota spesa";
        [ObservableProperty] private int _cod_cli;

        // Gestione date
        [ObservableProperty] private DateTime _tempDaData = DateTime.Today;
        [ObservableProperty] private DateTime _tempAData = DateTime.Today;
        public DateTime DataMassima => DateTime.Today;
        [ObservableProperty] private DateTime dataMinima;

        [ObservableProperty] private string? _tempTipologia;
        [ObservableProperty] private int _tempNumDip = 1;
        [ObservableProperty] private bool _tempFlagConCli;
        [ObservableProperty] private double? _tempImporto;
        [ObservableProperty] private string _tempTipoPag = "Carta di credito aziendale";

        [ObservableProperty] private string? _tempNrDoc;
        [ObservableProperty] private DateTime? _tempDataDoc;
        [ObservableProperty] private string? _tempRagSoc;
        [ObservableProperty] private string? _tempPartitaIva;

        [ObservableProperty] private string? _tempNote;
        [ObservableProperty] private bool _tempFlagPostCaricamento;

        [ObservableProperty] private ObservableCollection<string> _opzioniTipologia;
        [ObservableProperty] private ObservableCollection<string> _opzioniTipoPag;

        [ObservableProperty] private string _clienteSelezionatoRagSoc = "Tocca per selezionare...";

        
        // Proprietà per gestione foto allegata
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasFoto), nameof(MostraPlaceholderRemoto))]
        private string? _tempFotoScontrino;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MostraImmagineLocale), nameof(MostraPlaceholderRemoto), nameof(HasFoto))]
        private string _fotoVisualizzabilePath = string.Empty;
        public bool MostraImmagineLocale =>
            !string.IsNullOrWhiteSpace(FotoVisualizzabilePath) &&
            File.Exists(FotoVisualizzabilePath);
        public bool MostraPlaceholderRemoto => !MostraImmagineLocale && !string.IsNullOrWhiteSpace(TempFotoScontrino);
        public bool HasFoto => MostraImmagineLocale || MostraPlaceholderRemoto;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LockIcon))]
        private bool _isDatiEstrattiLocked = true;
        public string LockIcon => IsDatiEstrattiLocked ? Services.Constants.LOCK_CLOSE_ICON : Services.Constants.LOCK_OPEN_ICON; 

        // Proprietà per il riepilogo note aggiunte
        [ObservableProperty] private bool _isRiepilogoVisible = false;
        [ObservableProperty] private bool _scrollInAlto;
        public bool HaSpeseInLista => SpeseInserite?.Count > 0;

        // Proprietà per la modifica
        [ObservableProperty] private bool _isEditMode = false;
        [ObservableProperty] private bool _canEdit = true;
        [ObservableProperty] private bool _isLocalSpesa;
        [ObservableProperty] private bool _isFlagPostVisibile = true;

        // Proprietà per l'eliminazione
        [ObservableProperty] private bool _canDeleteLocal = false;

        public AddNotaSpesaViewModel() 
        {
            _mediaService = new MediaService();

            IsEditMode = false;
            CanEdit = true;

            RicercaClientePlanning();
            SetDefaultDataMinima();

            OpzioniTipologia = new ObservableCollection<string>();
            OpzioniTipoPag = new ObservableCollection<string>();
            _ = CaricaDecodificheDaDbAsync();

            _codCliIniziale = Cod_cli;

            IsSaved = false;
        }

        private async Task CaricaDecodificheDaDbAsync()
        {
            try
            {
                var tipologie = await Task.Run(() => App.Database.GetAll<SpesaTipologia>().Select(x => x.Descrizione).ToList());
                var tipiPagamento = await Task.Run(() => App.Database.GetAll<SpesaTipoPagamento>().Select(x => x.Descrizione).ToList());

                var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
                var tipologieFormattate = tipologie
                    .Select(x => textInfo.ToTitleCase(x.ToLower()))
                    .ToList();

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    OpzioniTipologia = new ObservableCollection<string>(tipologieFormattate);
                    OpzioniTipoPag = new ObservableCollection<string>(tipiPagamento);

                    await Task.Delay(50);
                    if (IsEditMode && _spesaOriginale != null)
                    {
                        TempTipologia = !string.IsNullOrWhiteSpace(_spesaOriginale.tipologia)
                            ? textInfo.ToTitleCase(_spesaOriginale.tipologia.Trim().ToLower())
                            : string.Empty;

                        TempTipoPag = _spesaOriginale.flag_tipo_pag ?? "";
                    }
                    else
                    {
                        // Comportamento standard di default quando si inserisce una NUOVA nota spesa
                        if (string.IsNullOrWhiteSpace(TempTipoPag) && OpzioniTipoPag.Count > 0)
                        {
                            TempTipoPag = OpzioniTipoPag.First();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore nel caricamento opzioni: {ex.Message}");
            }
        }

        public void CaricaSpesa(SpesaDettaglio spesa)
        {
            _spesaOriginale = spesa;
            IsEditMode = true;
            IsLocalSpesa = spesa.IsLocale;
            CanDeleteLocal = spesa.IsLocale && spesa.id_server == 0;
            PageTitle = "Modifica spesa";

            var oggi = DateTime.Today;
            var dataSogliaModifica = new DateTime(oggi.AddMonths(-1).Year, oggi.AddMonths(-1).Month, 1);
            
            CanEdit = spesa.da_data >= dataSogliaModifica && !spesa.flag_cont;
            
            if (spesa.da_data < DataMinima)
                DataMinima = spesa.da_data;

            TempDaData = spesa.da_data;
            TempAData = spesa.a_data;
            TempTipologia = spesa.tipologia;
            TempNumDip = spesa.nr_dip_ergon;
            TempFlagConCli = spesa.flag_con_cli;
            TempImporto = spesa.importo;
            TempTipoPag = spesa.flag_tipo_pag ?? "";
            TempNrDoc = spesa.nr_doc_scontrino;
            TempDataDoc = spesa.data_scontrino;
            TempRagSoc = spesa.rag_soc_scontrino;
            TempPartitaIva = spesa.partita_iva;
            TempNote = spesa.note;

            TempFotoScontrino = spesa.foto_scontrino;
            FotoVisualizzabilePath = spesa.path_scontrino_loc ?? "";

            Cod_cli = spesa.cod_cli;
            _codCliIniziale = Cod_cli;

            var cliente = App.Database.GetAll<Cliente>().FirstOrDefault(c => c.cod_cli == spesa.cod_cli);
            ClienteSelezionatoRagSoc = cliente?.rag_soc ?? "Cliente non trovato";
        }

        private void SetDefaultDataMinima()
        {
            var dataTarget = DateTime.Today.AddMonths(-1);
            DataMinima = new DateTime(dataTarget.Year, dataTarget.Month, 1);
        }

        private void ResetTempFields()
        {
            SetDefaultDataMinima();
            TempTipologia = string.Empty;
            TempFlagConCli = false;
            TempFotoScontrino = string.Empty;
            FotoVisualizzabilePath = string.Empty;
            TempImporto = null;
            TempTipoPag = "Carta di credito aziendale";
            TempNumDip = 1;
            TempNrDoc = null;
            TempDataDoc = null;
            TempRagSoc = null;
            TempPartitaIva = null;
            TempNote = String.Empty;
        }

        partial void OnTempDaDataChanged(DateTime value)
        {
            if (value > TempAData)
            {
                TempAData = value;
            }

            RicercaClientePlanning();
        }

        partial void OnTempADataChanged(DateTime value)
        {
            if (TempDaData > value)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlertWarning("La data di fine non può essere precedente alla data di inizio.");

                    // Resetto la data di fine uguale a quella di inizio
                    TempAData = TempDaData;
                });
            }
        }

        [RelayCommand]
        private async Task ApriRicercaClienteAsync()
        {
            if (HaSpeseInLista)
            {
                await ShowToast("Non puoi cambiare cliente dopo aver aggiunto una spesa alla nota.");
                return;
            }

            var paginaClienti = new ClientiNoteListView();

            paginaClienti.HandlerSelezione += (sender, clienteScelto) =>
            {
                ClienteSelezionatoRagSoc = clienteScelto.rag_soc;
                Cod_cli = clienteScelto.cod_cli;
            };

            await Shell.Current.Navigation.PushAsync(paginaClienti);
        }

        [RelayCommand]
        private void IncrementaDip() => TempNumDip++;

        [RelayCommand]
        private void DecrementaDip()
        {
            if (TempNumDip > 1) TempNumDip--;
        }

        [RelayCommand]
        private void ToggleRiepilogo()
        {
            IsRiepilogoVisible = !IsRiepilogoVisible;
        }

        [RelayCommand]
        private async Task ScegliFonteFotoAsync()
        {
            const string camera = "Fotocamera";
            const string galleria = "Galleria";
            string action = await new PopupAllegato().Show();
            if (string.IsNullOrWhiteSpace(action)) return;

            FileResult? result = null;

            try
            {
                switch (action)
                {
                    case camera:
                        var status = await Permissions.RequestAsync<Permissions.Camera>();
                        if (status == PermissionStatus.Granted)
                        {
                            result = await MediaPicker.Default.CapturePhotoAsync();
                        }
                        else
                        {
                            await DisplayAlertWarning("È necessario concedere i permessi per utilizzare la fotocamera");
                            return;
                        }
                        break;

                    case galleria:
                        result = await MediaPicker.Default.PickPhotoAsync();
                        break;

                }

                if (result == null) return;

                string percorsoFinale = await _mediaService.ProcessAndSaveAttachmentAsync(result);

                TempFotoScontrino = percorsoFinale;
                FotoVisualizzabilePath = percorsoFinale;

                // Integrazione AI
                if (HasInternetConnection)
                {
                    IsLoading = true;
                    using var cts = new CancellationTokenSource();
                    _ = AnimateLoadingTextAsync("Analisi in corso", cts.Token);

                    var risultato = await RestService.AnalizzaScontrinoAsync(percorsoFinale);

                    cts.Cancel();
                    LoadingText = "Attendere...";

                    await Task.Delay(300);

                    if (risultato.IsSuccess) { 
                        // Popolamento campi
                        if (risultato.Dati != null)
                        {
                            if (risultato.Dati.ImportoTotale.HasValue && risultato.Dati.ImportoTotale > 0)
                            {
                                bool confermato = true;

                                if (TempImporto.HasValue && TempImporto.Value > 0)
                                {
                                    string btnNuovo = $"Nuovo\n{risultato.Dati.ImportoTotale.Value:F2} €";
                                    string btnAttuale = $"Attuale\n{TempImporto.Value:F2} €";

                                    var popupImporto = new GenericPopupView(
                                        "Verifica importo spesa",
                                        "Dalla foto è stato rilevato un importo differente da quello già impostato. Quale vuoi mantenere?",
                                        Constants.WARNING_ICON,
                                        Colors.Orange,
                                        btnNuovo,
                                        btnAttuale);
                                    await MopupService.Instance.PushAsync(popupImporto);
                                    confermato = await popupImporto.RispostaTask.Task;
                                }
                                if (confermato)
                                {
                                    TempImporto = risultato.Dati.ImportoTotale.Value;
                                }
                            }

                            if (risultato.Dati.DataDocumento.HasValue)
                            {
                                bool confermato = true;
                                DateTime dataSenzaOrario = risultato.Dati.DataDocumento.Value.Date;

                                if (dataSenzaOrario < DataMinima)
                                {
                                    string btnNuova = $"Nuova\n{dataSenzaOrario:dd/MM/yyyy}";
                                    string btnAttuale = $"Attuale\n{TempDaData:dd/MM/yyyy}";

                                    var popupData = new GenericPopupView(
                                        "Verifica data spesa",
                                        $"Dalla foto è stata rilevata una data precedente alla data minima consentita ({DataMinima:dd/MM/yyyy}), vuoi proseguire comunque?",
                                        Constants.WARNING_ICON,
                                        Colors.Orange,
                                        btnNuova,
                                        btnAttuale);
                                    await MopupService.Instance.PushAsync(popupData);
                                    confermato = await popupData.RispostaTask.Task;
                                }

                                if (confermato)
                                {
                                    if (dataSenzaOrario < DataMinima)
                                    {
                                        DataMinima = dataSenzaOrario;
                                    }
                                    TempDaData = dataSenzaOrario;
                                    TempAData = dataSenzaOrario;
                                }
                            }

                            TempNrDoc = risultato.Dati.NumeroDocumento;
                            TempDataDoc = risultato.Dati.DataDocumento;
                            TempRagSoc = risultato.Dati.RagioneSociale;
                            TempPartitaIva = risultato.Dati.PartitaIva;
                            IsDatiEstrattiLocked = true;

                            await ShowToast("Analisi completata. Controlla i dati precompilati.");
                        }
                        else
                        {
                            var popupVuoto = new GenericPopupView(
                                        "Estrazione non riuscita",
                                        "L'immagine è stata analizzata, ma non è stato possibile estrarre alcun dato.",
                                        Constants.ERROR_ICON,
                                        Colors.Orange,
                                        "CHIUDI",
                                        null);
                            await MopupService.Instance.PushAsync(popupVuoto);
                            await popupVuoto.RispostaTask.Task;
                        }
                    }
                    else if(risultato.IsNetworkError) // Errore di rete
                    { 
                        var popupOffline = new GenericPopupView(
                                    "Problema di connessione",
                                    "La connessione è caduta o il server è irraggiungibile. La foto è stata allegata, ma dovrai inserire i dati manualmente.",
                                    Constants.OFFLINE_ICON,
                                    Colors.Orange,
                                    "CHIUDI",
                                    null);
                        await MopupService.Instance.PushAsync(popupOffline);
                        await popupOffline.RispostaTask.Task;
                    }
                    else // Errore server o eccezione generica
                    {
                        var popupErrore = new GenericPopupView(
                                    "Errore server",
                                    "Non è stato possibile analizzare lo scontrino. La foto è stata allegata, ma dovrai inserire i dati manualmente.",
                                    Constants.OFFLINE_ICON,
                                    Colors.Orange,
                                    "CHIUDI",
                                    null);
                        await MopupService.Instance.PushAsync(popupErrore);
                        await popupErrore.RispostaTask.Task;
                    }
                }
                else
                {
                    await Task.Delay(300);
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        var popupOffline = new GenericPopupView(
                                    "Sei offline",
                                    "La foto non è stata analizzata, inserisci manualmente le informazioni.",
                                    Constants.OFFLINE_ICON,
                                    Colors.Orange,
                                    "CHIUDI",
                                    null);
                        await MopupService.Instance.PushAsync(popupOffline);
                        await popupOffline.RispostaTask.Task;
                    });
                }
            }
            catch (InvalidOperationException invEx)
            {
                await DisplayAlertWarning(invEx.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore nel salvataggio dell'allegato: {ex.Message}");
                await DisplayAlertWarning("Impossibile salvare l'allegato");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RimuoviFoto()
        {
            string fileDaEliminare = TempFotoScontrino ?? "";

            TempFotoScontrino = string.Empty;
            FotoVisualizzabilePath = string.Empty;
            TempNrDoc = null;
            TempDataDoc = null;
            TempRagSoc = null;
            TempPartitaIva = null;

            if (!string.IsNullOrEmpty(fileDaEliminare))
            {
                try
                {
                    await Task.Delay(100);
                    if (File.Exists(fileDaEliminare))
                    {
                        File.Delete(fileDaEliminare);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Impossibile eliminare il file: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task RimuoviNotaDaRiepilogo(SpesaDettaglio spesaDaRimuovere)
        {
            if (spesaDaRimuovere == null) return;

            var popupEliminazione = new GenericPopupView(
                                        "Attenzione",
                                        "Eliminare definitivamente la spesa inserita?",
                                        Constants.WARNING_ICON,
                                        Colors.Orange,
                                        "SI",
                                        "ANNULLA");
            await MopupService.Instance.PushAsync(popupEliminazione);
            bool continua = await popupEliminazione.RispostaTask.Task;

            if (!continua) return;

            SpeseInserite.Remove(spesaDaRimuovere);
            OnPropertyChanged(nameof(HaSpeseInLista));
        }

        [RelayCommand]
        private async Task PermettiModificaEstratti()
        {
            IsDatiEstrattiLocked = !IsDatiEstrattiLocked;
        }

        [RelayCommand]
        private async Task AggiungiSpesaAllaNota(bool showNotification)
        {
            await AggiungiSpesaLogica(showNotification, true);
        }

        [RelayCommand]
        private async Task AggiungiSpesaModifica(bool showNotification)
        {
            await AggiungiSpesaModificaLogica(showNotification, true);
        }

        private async Task<bool> AggiungiSpesaLogica(bool showNotification = true, bool resetFields = true)
        {
            string? isDatiMancanti = ValidaDati();
            if (!string.IsNullOrWhiteSpace(isDatiMancanti))
            {
                await DisplayAlertWarning(isDatiMancanti);
                return false;
            }

            var spesa = new SpesaDettaglio
            {
                cod_dip = Settings.CodDipendente,
                cod_cli = Cod_cli,
                da_data = TempDaData,
                a_data = TempAData,
                tipologia = TempTipologia,
                nr_dip_ergon = TempNumDip,
                flag_con_cli = TempFlagConCli,
                foto_scontrino = TempFotoScontrino,
                path_scontrino_loc = TempFotoScontrino,
                importo = TempImporto ?? 0.0,
                flag_tipo_pag = TempTipoPag,
                nr_doc_scontrino = TempNrDoc,
                data_scontrino = TempDataDoc,
                rag_soc_scontrino = TempRagSoc,
                partita_iva = TempPartitaIva,
                note = TempNote
            };

            SpeseInserite.Add(spesa);
            OnPropertyChanged(nameof(HaSpeseInLista));

            if (showNotification)
            {
                await ShowToast("Spesa aggiunta alla nota con successo.");
            }

            if (resetFields)
            {
                ResetTempFields();
                ScrollInAlto = true;
                ScrollInAlto = false;
            }

            return true;
        }

        private async Task<bool> AggiungiSpesaModificaLogica(bool showNotification = true, bool resetFields = true)
        {
            string? isDatiMancanti = ValidaDati();
            if (!string.IsNullOrWhiteSpace(isDatiMancanti))
            {
                await DisplayAlertWarning(isDatiMancanti);
                return false;
            }

            if (_spesaOriginale != null)
            {
                bool hasModifiche = HasUnsavedChanges();
                if (hasModifiche)
                {
                    IsLoading = true;
                    LoadingText = "Salvataggio modifiche...";

                    AggiornaSpesaOriginaleDaUI();

                    bool isOffline = !HasInternetConnection;

                    if (isOffline || _spesaOriginale.IsLocale)
                    {
                        _spesaOriginale.IsLocale = true;
                        await Task.Run(() => App.Database.Update(_spesaOriginale));
                    }
                    else
                    {
                        await Task.Run(() => App.Database.Update(_spesaOriginale));
                        bool success = await RestService.ModificaNotaSpesa(_spesaOriginale);
                        if (!success)
                        {
                            _spesaOriginale.IsLocale = true;
                            await Task.Run(() => App.Database.Update(_spesaOriginale));
                        }
                    }
                    IsLoading = false;
                }

                if (!SpeseInserite.Any(s => s.id == _spesaOriginale.id))
                {
                    SpeseInserite.Add(_spesaOriginale);
                    OnPropertyChanged(nameof(HaSpeseInLista));
                }

                TempFlagPostCaricamento = _spesaOriginale.IsLocale;

                if (showNotification)
                {
                    string msgToast = hasModifiche
                        ? "Modifiche salvate. Ora puoi aggiungere una nuova spesa."
                        : "Nota mantenuta. Ora puoi aggiungere una nuova spesa.";
                    await ShowToast(msgToast);
                }
            }

            if (resetFields)
            {
                IsEditMode = false;
                CanDeleteLocal = false;
                PageTitle = "Aggiungi spesa alla nota";
                _spesaOriginale = null;
                IsFlagPostVisibile = false;

                ResetTempFields();
                ScrollInAlto = true;
                ScrollInAlto = false;
            }

            return true;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            bool hasData = !string.IsNullOrWhiteSpace(TempTipologia) ||
                          TempImporto.HasValue ||
                          !string.IsNullOrWhiteSpace(TempNote) ||
                          !string.IsNullOrWhiteSpace(TempFotoScontrino) ||
                          TempFlagConCli == true;
            bool emptyList = SpeseInserite == null || SpeseInserite.Count == 0;

            bool spesaAggiuntaAlVolo = false;

            if (emptyList || hasData)
            {
                spesaAggiuntaAlVolo = await AggiungiSpesaLogica(false, false);
                if (!spesaAggiuntaAlVolo) return;
            }

            if (SpeseInserite == null || SpeseInserite.Count == 0) return;

            var speseNuove = SpeseInserite.Where(s => s.id == 0).ToList();

            if (!speseNuove.Any())
            {
                await Task.Delay(250);
                await Shell.Current.Navigation.PopAsync();
                return;
            }

            LoadingText = "Salvataggio nota spesa...";
            IsLoading = true;
            try
            {
                if (TempFlagPostCaricamento || !HasInternetConnection) // Salvo la nota solo nel db locale
                {
                    foreach (var spesa in speseNuove)
                    {
                        spesa.IsLocale = true;
                    }
                    await Task.Run(() => App.Database.InsertAll(speseNuove));
                    Settings.LastPostponedSave = DateTime.Now;

                    IsSaved = true;
                    string messaggio = !HasInternetConnection
                        ? "Sei offline. La nota è stata salvata localmente."
                        : "La nota è stata salvata localmente.";

                    await ShowToast(messaggio);
                }
                else
                {
                    var idPresenti = await Task.Run(() => App.Database.GetAll<SpesaDettaglio>()
                                    .Select(x => x.id_server)
                                    .ToHashSet());

                    var speseConFotoInRam = speseNuove
                                    .Where(s => !string.IsNullOrWhiteSpace(s.path_scontrino_loc))
                                    .ToList();

                    bool successo = await RestService.SalvaNoteSpesa(speseNuove);

                    if (successo)
                    {
                        bool syncOk = await RestService.GetNoteSpesa(true);

                        if (syncOk && speseConFotoInRam.Any())
                        {
                            var noteNuoveDalServer = await Task.Run(() => App.Database.GetAll<SpesaDettaglio>()
                                .Where(x => x.id_server > 0 && !idPresenti.Contains(x.id_server))
                                .ToList());

                            foreach (var spesaRam in speseConFotoInRam)
                            {
                                var match = noteNuoveDalServer.FirstOrDefault(n =>
                                    n.importo == spesaRam.importo &&
                                    string.Equals(n.tipologia, spesaRam.tipologia, StringComparison.OrdinalIgnoreCase) &&
                                    n.da_data.Date == spesaRam.da_data.Date &&
                                    n.cod_cli == spesaRam.cod_cli);

                                if (match != null)
                                {
                                    match.path_scontrino_loc = spesaRam.path_scontrino_loc;
                                    await Task.Run(() => App.Database.Update(match));

                                    noteNuoveDalServer.Remove(match);
                                }
                            }
                        }
                        IsSaved = true;
                        await ShowToast("Nota spesa inviata correttamente.");
                    }
                    else
                    {
                        await DisplayAlertError("Errore di comunicazione con il server. Riprova più tardi.");
                        if (spesaAggiuntaAlVolo)
                        {
                            var ultima = SpeseInserite.LastOrDefault();
                            if (ultima != null)
                            {
                                SpeseInserite.Remove(ultima);
                                OnPropertyChanged(nameof(HaSpeseInLista));
                            }
                        }
                        return;
                    }
                }
                await Task.Delay(250);
                await Shell.Current.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlertError($"Errore Tecnico: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SalvaModificheAsync()
        {
            if (!HasUnsavedChanges())
            {
                await Task.Delay(250);
                await Shell.Current.Navigation.PopAsync();
                return;
            }

            string? errore = ValidaDati();
            if (errore != null)
            {
                await DisplayAlertWarning(errore);
                return;
            }

            AggiornaSpesaOriginaleDaUI();

            if(_spesaOriginale != null)
            {
                if (_spesaOriginale.IsLocale || !HasInternetConnection)
                {
                    _spesaOriginale.IsLocale = true;
                    await Task.Run(() => App.Database.Update(_spesaOriginale));

                    string messaggio = !HasInternetConnection
                        ? "Nessuna connessione. Modifiche salvate in locale."
                        : "Modifiche salvate localmente con successo";

                    await ShowToast(messaggio);
                }
                else
                {
                    _spesaOriginale.IsLocale = true;
                    await Task.Run(() => App.Database.Update(_spesaOriginale));

                    bool successo = await RestService.ModificaNotaSpesa(_spesaOriginale);

                    if (successo)
                    {
                        _spesaOriginale.IsLocale = false;
                        await Task.Run(() => App.Database.Update(_spesaOriginale));

                        await RestService.GetNoteSpesa(true);
                        await ShowToast("Modifiche salvate e inviate in remoto");
                    }
                    else
                    {
                        await DisplayAlertError("Modifica salvata solo localmente. Il server non è stato aggiornato.");
                    }
                }
                IsSaved = true;
                await Shell.Current.Navigation.PopAsync();
            }
            else
            {
                Debug.WriteLine("Errore: _spesaOriginale è null durante il salvataggio.");
            }
        }

        [RelayCommand]
        private async Task InviaNotaSingolaAsync()
        {
            if (!HasInternetConnection)
            {
                var popupConnessione = new GenericPopupView(
                                        "Nessuna connessione",
                                        "Impossibile sincronizzare la nota in questo momento, riprova più tardi.",
                                        Constants.WARNING_ICON,
                                        Colors.Orange,
                                        "SI",
                                        "ANNULLA");
                await MopupService.Instance.PushAsync(popupConnessione);
                bool continua = await popupConnessione.RispostaTask.Task;
                if (!continua) return;
            }

            if (HasUnsavedChanges())
            {
                var popupModifiche = new GenericPopupView(
                                        "Modifiche presenti",
                                        "Hai effettuato delle modifiche alla spesa. Vuoi salvarle e proseguire con il caricamento?",
                                        Constants.WARNING_ICON,
                                        Colors.Orange,
                                        "SI",
                                        "ANNULLA");
                await MopupService.Instance.PushAsync(popupModifiche);
                bool continua = await popupModifiche.RispostaTask.Task;
                if (!continua) return;
            }

            string? errore = ValidaDati();
            if (errore != null)
            {
                await DisplayAlertWarning(errore);
                return;
            }

            if (_spesaOriginale == null) return;

            LoadingText = "Sincronizzazione col server...";
            IsLoading = true;
            try
            {
                AggiornaSpesaOriginaleDaUI();

                _spesaOriginale.IsLocale = true;
                await Task.Run(() => App.Database.Update(_spesaOriginale));

                string pathSalvatoInMemoria = _spesaOriginale.path_scontrino_loc ?? "";
                bool successo = false;

                if (_spesaOriginale.id_server > 0) // controllo se la nota esisteva già sul server
                {
                    successo = await RestService.ModificaNotaSpesa(_spesaOriginale);
                }
                else
                {
                    var notaDaInviare = new List<SpesaDettaglio> { _spesaOriginale };
                    successo = await RestService.SalvaNoteSpesa(notaDaInviare);
                }

                if (successo)
                {
                    if (_spesaOriginale.id_server > 0) // controllo se ho fatto un update
                    {
                        _spesaOriginale.IsLocale = false;
                        await Task.Run(() => App.Database.Update(_spesaOriginale));
                    }
                    else
                    {
                        await Task.Run(() => App.Database.Delete<SpesaDettaglio>(x => x.id == _spesaOriginale.id));
                    }

                    // Scatto la fotografia degli ID per il ripristino della foto (valido solo per le note nuove)
                    var idGiaPresenti = await Task.Run(() => App.Database.GetAll<SpesaDettaglio>()
                                        .Select(x => x.id_server)
                                        .ToHashSet());

                    bool syncOk = await RestService.GetNoteSpesa(true);

                    if (syncOk)
                    {
                        // Se era una nota nuova, riaggancio il path locale dello scontrino
                        if (_spesaOriginale.id_server == 0 && !string.IsNullOrWhiteSpace(pathSalvatoInMemoria))
                        {
                            var notaNuovaDalServer = await Task.Run(() => App.Database.GetAll<SpesaDettaglio>()
                                .FirstOrDefault(x => x.id_server > 0 &&
                                    !idGiaPresenti.Contains(x.id_server) &&
                                    x.importo == _spesaOriginale.importo &&
                                    string.Equals(x.tipologia, _spesaOriginale.tipologia, StringComparison.OrdinalIgnoreCase) &&
                                    x.da_data.Date == _spesaOriginale.da_data.Date &&
                                    x.cod_cli == _spesaOriginale.cod_cli));

                            if (notaNuovaDalServer != null)
                            {
                                notaNuovaDalServer.path_scontrino_loc = pathSalvatoInMemoria;
                                await Task.Run(() => App.Database.Update(notaNuovaDalServer));
                            }
                        }

                        await ShowToast("Nota inviata e dati aggiornati!");
                        IsSaved = true;
                        await Task.Delay(250);
                        await Shell.Current.Navigation.PopAsync();
                    }
                }
                else
                {
                    await DisplayAlertError("Errore di comunicazione con il server. Riprova più tardi.");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertError($"Errore Tecnico: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task EliminaNotaAsync()
        {
            var popupElimina = new GenericPopupView(
                "Conferma eliminazione",
                "Vuoi eliminare definitivamente questa nota spesa locale?",
                Constants.DELETE_ICON,
                Colors.Red,
                "SI",
                "ANNULLA");

            await MopupService.Instance.PushAsync(popupElimina);
            bool conferma = await popupElimina.RispostaTask.Task;
            
            
            if (!conferma) return;

            LoadingText = "Eliminazione...";
            IsLoading = true;
            try
            {
                if(_spesaOriginale != null)
                {
                    if (!string.IsNullOrWhiteSpace(_spesaOriginale.path_scontrino_loc) && File.Exists(_spesaOriginale.path_scontrino_loc))
                    {
                        try
                        {
                            File.Delete(_spesaOriginale.path_scontrino_loc);
                        }
                        catch (Exception exFile)
                        {
                            Debug.WriteLine($"Impossibile eliminare file scontrino durante rimozione nota: {exFile.Message}");
                        }
                    }

                    await Task.Run(() => App.Database.Delete<SpesaDettaglio>(x => x.id == _spesaOriginale.id));

                    IsSaved = true;
                    await ShowToast("Nota spesa eliminata correttamente.");
                    await Task.Delay(250);
                    await Shell.Current.Navigation.PopAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertError($"Impossibile eliminare la nota: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string? ValidaDati()
        {
            if (Cod_cli <= 0) return
                    "Seleziona un cliente.";

            if (TempDaData == DateTime.MinValue || TempAData == DateTime.MinValue)
                return "Inserisci le date della spesa.";

            if (TempAData < TempDaData)
                return "La data di fine non può essere precedente alla data di inizio.";

            if (string.IsNullOrWhiteSpace(TempTipologia))
                return "Seleziona una tipologia di spesa.";

            if (TempTipologia == "Pernottamento" && TempDaData == TempAData)
                return "La data di check-in deve essere diversa dalla data di check-out.";

            if (string.IsNullOrWhiteSpace(TempTipoPag))
                return "Seleziona il metodo di pagamento.";

            if (TempNumDip <= 0)
                return "Inserire il numero di dipendenti Ergon.";

            if (!TempImporto.HasValue || TempImporto == 0)
                return "Inserire un importo.";

            if (TempImporto > 10000 || TempImporto < 0)
                return "L'importo inserito non è valido.";

            if (TempTipologia == "Altro" && string.IsNullOrWhiteSpace(TempNote))
                return "Le note sono obbligatorie quando la tipologia è 'Altro'.";

            if (!string.IsNullOrWhiteSpace(TempNote) && TempNote.Length > 255)
                return "Le note sono troppo lunghe.";

            if (!TempFlagPostCaricamento) // Controllo che siano popolati i campi solo se il flag non è attivato
            {
                if (string.IsNullOrWhiteSpace(TempFotoScontrino))
                    return "Allega una foto dello scontrino.";

                if (string.IsNullOrWhiteSpace(TempNrDoc))
                    return "Immetti il numero del documento.";

                if (TempDataDoc == null)
                    return "Immetti la data del documento.";

                if (string.IsNullOrWhiteSpace(TempRagSoc))
                    return "Immetti la ragione sociale.";

                if (string.IsNullOrWhiteSpace(TempPartitaIva))
                    return "Immetti la partita IVA.";
            }

            return null; // Tutto OK
        }

        public bool HasUnsavedChanges()
        {
            if (!CanEdit) return false;

            // Controllo cambiamenti in modalità modifica
            if (IsEditMode && _spesaOriginale != null)
            {
                return TempTipologia != _spesaOriginale.tipologia ||
                       TempDaData != _spesaOriginale.da_data ||
                       TempAData != _spesaOriginale.a_data ||
                       TempNumDip != _spesaOriginale.nr_dip_ergon ||
                       TempFlagConCli != _spesaOriginale.flag_con_cli ||
                       TempImporto != _spesaOriginale.importo ||
                       TempTipoPag != _spesaOriginale.flag_tipo_pag ||
                       TempNote != _spesaOriginale.note ||
                       TempFotoScontrino != _spesaOriginale.foto_scontrino ||
                       Cod_cli != _spesaOriginale.cod_cli ||
                       TempNrDoc != _spesaOriginale.nr_doc_scontrino ||
                       TempDataDoc != _spesaOriginale.data_scontrino ||
                       TempRagSoc != _spesaOriginale.rag_soc_scontrino ||
                       TempPartitaIva != _spesaOriginale.partita_iva;
            }

            // Controllo cambiamenti in inserimento nota nuova
            bool headerChanged = Cod_cli != _codCliIniziale;
            bool hasLines = SpeseInserite.Count > 0;
            bool hasTempData = !string.IsNullOrWhiteSpace(TempTipologia) ||
                       TempNumDip > 1 ||
                       TempFlagConCli == true ||
                       !string.IsNullOrWhiteSpace(TempFotoScontrino) ||
                       TempImporto.HasValue ||
                       !TempTipoPag.Contains("Carta di credito aziendale") ||
                       !string.IsNullOrWhiteSpace(TempNote) ||
                       !string.IsNullOrWhiteSpace(TempNrDoc) ||
                       TempDataDoc.HasValue ||
                       !string.IsNullOrWhiteSpace(TempRagSoc) ||
                       !string.IsNullOrWhiteSpace(TempPartitaIva);

            return headerChanged || hasLines || hasTempData;
        }

        private void RicercaClientePlanning()
        {
            try
            {
                var recordPlanning = App.Database.GetAll<Planning>()
                    .FirstOrDefault(p => p.cod_dip == Settings.CodDipendente && p.data.Date == TempDaData);

                if (recordPlanning != null && recordPlanning.cod_cli != null)
                {
                    Cod_cli = recordPlanning.cod_cli.Value;

                    var cliente = App.Database.GetAll<Cliente>()
                        .FirstOrDefault(c => c.cod_cli == recordPlanning.cod_cli);

                    if (cliente != null)
                    {
                        ClienteSelezionatoRagSoc = cliente.rag_soc;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore durante la ricerca automatica planning: {ex.Message}");
            }
        }
        private void AggiornaSpesaOriginaleDaUI()
        {
            if (_spesaOriginale == null) return;

            _spesaOriginale.cod_cli = Cod_cli;
            _spesaOriginale.da_data = TempDaData;
            _spesaOriginale.a_data = TempAData;
            _spesaOriginale.tipologia = TempTipologia;
            _spesaOriginale.nr_dip_ergon = TempNumDip;
            _spesaOriginale.flag_con_cli = TempFlagConCli;
            _spesaOriginale.importo = TempImporto ?? 0;
            _spesaOriginale.flag_tipo_pag = TempTipoPag;
            _spesaOriginale.nr_doc_scontrino = TempNrDoc;
            _spesaOriginale.data_scontrino = TempDataDoc;
            _spesaOriginale.rag_soc_scontrino = TempRagSoc;
            _spesaOriginale.partita_iva = TempPartitaIva;
            _spesaOriginale.note = TempNote;
            _spesaOriginale.foto_scontrino = TempFotoScontrino;
            _spesaOriginale.path_scontrino_loc = FotoVisualizzabilePath;
        }

        private async Task AnimateLoadingTextAsync(string baseText, CancellationToken token)
        {
            int dots = 0;
            int spaces = 3;
            while (!token.IsCancellationRequested)
            {
                LoadingText = baseText + new string('.', dots) + new string(' ', spaces);

                dots = (dots + 1) % 4;
                if (spaces == 0) spaces = 3;
                else spaces--;

                try
                {
                    await Task.Delay(400, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
    }
}
