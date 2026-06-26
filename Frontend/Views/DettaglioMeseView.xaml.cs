namespace Ergon.Views;
using Ergon.ViewModels;

public partial class DettaglioMeseView : ContentPage
{
	public DettaglioMeseView(string meseAnno)
	{
		InitializeComponent();
		BindingContext = new DettaglioMeseViewModel(meseAnno);
	}

    private async void OnFrameTapped(object sender, EventArgs e)
    {
        if (sender is VisualElement elementoVisuale)
        {
            await elementoVisuale.FadeTo(0.5, 50, Easing.CubicOut);
            await elementoVisuale.ScaleTo(0.98, 50);
            await elementoVisuale.ScaleTo(1.00, 50);
            await elementoVisuale.FadeTo(1.0, 50, Easing.CubicIn);
        }
    }
}