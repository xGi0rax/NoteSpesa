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

    private async void OnFrameTapped(object sender, EventArgs e)
    {
        if (sender is Frame frame)
        {
            await frame.FadeTo(0.5, 80, Easing.CubicOut);
            await frame.ScaleTo(0.98, 80);
            await frame.ScaleTo(1.00, 80);
            await frame.FadeTo(1.0, 80, Easing.CubicIn);
        }
    }
}