using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ergon.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace Ergon.ViewModels
{
    public partial class ClientiNoteListViewModel : ViewModel
    {
        private List<Cliente> _allClientiCache = new();
        private CancellationTokenSource? _searchCts;

        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Cliente> clientiFiltrati = new();
        [ObservableProperty] private bool isNoResultsVisible = false;

        public ClientiNoteListViewModel()
        {
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var dati = await Task.Run(() =>  App.Database.GetAll<Cliente>().OrderBy(x => x.rag_soc).ToList());
                _allClientiCache = dati;

                UpdateVisualList(dati.Take(200).ToList());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore caricamento clienti: {ex.Message}");
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            // DEBOUNCE: Se l'utente scrive velocemente, annullo la ricerca precedente
            _searchCts?.Cancel();
            _searchCts?.Dispose();      
            _searchCts = new CancellationTokenSource();

            var token = _searchCts.Token;

            Task.Delay(300, token).ContinueWith(t =>
            {
                if (!t.IsCanceled) ApplyFilter();
            }, TaskScheduler.Default);
        }

        [RelayCommand]
        public void ApplyFilter()
        {
            var token = _searchCts?.Token ?? CancellationToken.None;
            var query = SearchText?.Trim().ToUpper() ?? "";

            if (token.IsCancellationRequested) return;

            var risultati = string.IsNullOrWhiteSpace(query)
                ? _allClientiCache
                : _allClientiCache
                    .Where(x =>
                    {
                        if (token.IsCancellationRequested) return false;

                        return (x.descrizione != null && x.descrizione.Contains(query, StringComparison.OrdinalIgnoreCase))
                            || x.cod_cli.ToString() == query;
                    })
                    .ToList();

            if (token.IsCancellationRequested) return;

            UpdateVisualList(risultati);
        }

        private void UpdateVisualList(List<Cliente> nuoviDati)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ClientiFiltrati = new ObservableCollection<Cliente>(nuoviDati.Take(200));
                IsNoResultsVisible = nuoviDati.Count == 0 && !string.IsNullOrWhiteSpace(SearchText);
            });
        }
    }
}