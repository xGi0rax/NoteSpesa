using Ergon.Models;
using Ergon.ViewModels;

namespace Ergon.Views;

public partial class ClientiNoteListView : ContentPage
{
    // Evento per comunicare la scelta alla pagina chiamante
    public event EventHandler<Cliente>? HandlerSelezione;

    public ClientiNoteListView()
    {
        InitializeComponent();
        this.BindingContext = new ClientiNoteListViewModel();
    }

    private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Cliente cliente)
        {
            // Notifico la selezione e chiudo la pagina
            HandlerSelezione?.Invoke(this, cliente);
            await Navigation.PopAsync();
        }
    }
}