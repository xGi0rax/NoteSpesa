using Mopups.Pages;
using Mopups.Services;

namespace Ergon.Views;

public partial class GenericPopupView : PopupPage
{
    public TaskCompletionSource<bool> RispostaTask { get; set; } = new();

    public GenericPopupView(
        string titolo,
        string messaggio,
        string icona,
        Color coloreIcona,
        string testoConfirm = "OK",
        string? testoAnnulla = null)
    {
        InitializeComponent();

        TitleLabel.Text = titolo;
        MessageLabel.Text = messaggio;
        IconLabel.Text = icona;
        IconLabel.TextColor = coloreIcona;

        ImpostaTestoPulsante(testoConfirm, ConfirmHeaderLabel, ConfirmValueLabel);

        if (string.IsNullOrEmpty(testoAnnulla))
        {
            CancelButton.IsVisible = false;
            Grid.SetColumn(ConfirmButton, 0);
            Grid.SetColumnSpan(ConfirmButton, 2);
        }
        else
        {
            ImpostaTestoPulsante(testoAnnulla, CancelHeaderLabel, CancelValueLabel);
            CancelButton.IsVisible = true;
            Grid.SetColumn(ConfirmButton, 1);
            Grid.SetColumnSpan(ConfirmButton, 1);
        }
    }

    private void ImpostaTestoPulsante(string testo, Label headerLabel, Label valueLabel)
    {
        if (string.IsNullOrEmpty(testo)) return;

        if (testo.Contains('\n'))
        {
            var parti = testo.Split('\n');
            headerLabel.Text = parti[0].Trim().ToUpper();
            headerLabel.IsVisible = true;
            valueLabel.Text = parti[1].Trim();
        }
        else
        {
            headerLabel.IsVisible = false;
            valueLabel.Text = testo;
            valueLabel.FontSize = 15;
        }
    }

    private async void Cancel_Clicked(object sender, EventArgs e)
    {
        await MopupService.Instance.PopAsync();
        RispostaTask.TrySetResult(false);
    }

    private async void Confirm_Clicked(object sender, EventArgs e)
    {
        await MopupService.Instance.PopAsync();
        RispostaTask.TrySetResult(true);
    }
}