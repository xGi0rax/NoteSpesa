using Ergon.ViewModels;

namespace Ergon.Views;

public partial class NoteSpesaView : ContentPage
{
	private readonly NoteSpesaViewModel viewmodel;

    public NoteSpesaView()
	{
		InitializeComponent();
        viewmodel = new NoteSpesaViewModel();
        BindingContext = viewmodel;
    }

	protected override async void OnAppearing()
	{
		base.OnAppearing();
        if (BindingContext is NoteSpesaViewModel vm)
        {
            await vm.InizializzaDatiAsync();
        }
    }

    // Animazione al tocco di una card resconto mensile
    private async void OnFrameTapped(object sender, EventArgs e)
    {
        if (sender is Frame frame)
        {
            await frame.FadeTo(0.5, 50, Easing.CubicOut);
            await frame.ScaleTo(0.98, 50);
            await frame.ScaleTo(1.00, 50);
            await frame.FadeTo(1.0, 50, Easing.CubicIn);
        }
    }
}