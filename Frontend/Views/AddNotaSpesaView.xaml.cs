using Ergon.ViewModels;
namespace Ergon.Views;

public partial class AddNotaSpesaView : ContentPage
{
    private bool _isNavigatingAway = false;
    private readonly AddNotaSpesaViewModel _viewModel;
    public AddNotaSpesaView()
    {
        InitializeComponent();
        _viewModel = new();
        BindingContext = _viewModel;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.ScrollInAlto) && _viewModel.ScrollInAlto)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(150);
                    await MainScrollView.ScrollToAsync(0, 0, false);
                });
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.Current.Navigating += OnShellNavigating;
        if (BindingContext is AddNotaSpesaViewModel vm && vm.IsEditMode && vm.TempImporto.HasValue)
        {
            ImportoEntry.Text = vm.TempImporto.Value.ToString("F2");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Shell.Current.Navigating -= OnShellNavigating;
    }

    private async void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        if (e.Target.Location.OriginalString.Contains("ClientiNoteListView"))
        {
            return;
        }

        if (_isNavigatingAway || _viewModel.IsSaved || !_viewModel.HasUnsavedChanges()) return;

        var deferral = e.GetDeferral();
        e.Cancel();

        bool leave = await DisplayAlert("Attenzione", "Hai dati non salvati. Vuoi uscire?", "Esci", "Rimani");

        if (leave)
        {
            _isNavigatingAway = true;
            Dispatcher.Dispatch(async () => await Shell.Current.GoToAsync(e.Target.Location));
        }
        deferral.Complete();
    }

    protected override bool OnBackButtonPressed()
    {
        Shell.Current.GoToAsync("..");
        return true;
    }

    private void OnImportoTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry || string.IsNullOrEmpty(e.NewTextValue)) return;

        string processedText = e.NewTextValue.Replace('.', ',');
        processedText = NonDigitCommaRegex().Replace(processedText, "");

        int indiceVirgola = processedText.IndexOf(',');

        if (indiceVirgola != -1)
        {
            string interi = processedText[..indiceVirgola];
            string decimali = processedText[(indiceVirgola + 1)..].Replace(",", "");

            if (interi.Length > 5) interi = interi[..5];

            if (decimali.Length > 2) decimali = decimali[..2];

            processedText = $"{interi},{decimali}";
        }
        else
        {
            if (processedText.Length > 5)
            {
                processedText = processedText[..5];
            }
        }
        if (entry.Text != processedText)
        {
            entry.Text = processedText;
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"[^0-9,]")]
    private static partial System.Text.RegularExpressions.Regex NonDigitCommaRegex();

    private void OnImportoUnfocused(object sender, FocusEventArgs e)
    {
        var entry = (Entry)sender;
        if (string.IsNullOrWhiteSpace(entry.Text)) return;

        string valorePulito = entry.Text.Replace('.', ',');

        if (double.TryParse(valorePulito, out double risultato))
        {
            entry.Text = risultato.ToString("F2");
        }
    }

    private void OnBackgroundTapped(object sender, EventArgs e)
    {
        if (ImportoEntry.IsFocused)
        {
            ImportoEntry.Unfocus();

            ImportoEntry.IsEnabled = false;
            ImportoEntry.IsEnabled = true;
        }
    }
}
