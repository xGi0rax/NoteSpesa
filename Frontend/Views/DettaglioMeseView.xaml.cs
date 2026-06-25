namespace Ergon.Views;
using Ergon.ViewModels;

public partial class DettaglioMeseView : ContentPage
{
	public DettaglioMeseView(string meseAnno)
	{
		InitializeComponent();
		BindingContext = new DettaglioMeseViewModel(meseAnno);
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is DettaglioMeseViewModel vm)
        {
            vm.RefreshDataCommand.Execute(null);
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