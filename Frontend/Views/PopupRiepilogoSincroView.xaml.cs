using Mopups.Pages;
using Mopups.Services;
using Ergon.Models;

namespace Ergon.Views;

public partial class PopupRiepilogoSincroView : PopupPage
{
    public TaskCompletionSource<bool> Risultato { get; set; } = new();

    public PopupRiepilogoSincroView(List<SpesaDettaglio> note)
    {
        InitializeComponent();
        BindingContext = note;

        if(note.Count == 1)
        {
            SottotitoloLabel.Text = "Verrà inviata la seguente nota";
        }
        else
        {
            SottotitoloLabel.Text = $"Verranno inviate le seguenti {note.Count} note"; 
        }
    }

    private async void Annulla_Clicked(object sender, EventArgs e)
    {
        await MopupService.Instance.PopAsync();
        Risultato.TrySetResult(false);
    }

    private async void Sincronizza_Clicked(object sender, EventArgs e)
    {
        await MopupService.Instance.PopAsync();
        Risultato.TrySetResult(true);
    }
}