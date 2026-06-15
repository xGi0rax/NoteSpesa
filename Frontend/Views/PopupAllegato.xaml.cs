using Mopups.Services;

namespace Ergon.Views;

public partial class PopupAllegato
{
    private TaskCompletionSource<bool> taskCompletionSource;

    private string Action;
    public PopupAllegato()
    {
        InitializeComponent();
        Action = string.Empty;
        taskCompletionSource = new TaskCompletionSource<bool>();

        SetupGrid();
    }
    private void SetupGrid()
    {
        TipoAllegato[] tipi ={
            new() { Text = "Fotocamera", ImagePath = "fotocamera.png", BackgroundColor = Color.FromArgb("#fd3178") },
            new() { Text = "Galleria", ImagePath = "galleria.png", BackgroundColor = Color.FromArgb("#ca60f8") },
        };

        TapGestureRecognizer tapGesture = new()
        {
            NumberOfTapsRequired = 1,
        };
        tapGesture.Tapped += TapGesture_Tapped;

        Grid grid = new();
        grid.AddRowDefinition(new RowDefinition(GridLength.Star));
        grid.AddRowDefinition(new RowDefinition(GridLength.Auto));
        for (int i = 0; i < tipi.Length; i++)
        {
            grid.AddColumnDefinition(new ColumnDefinition(GridLength.Star));

            Image image = new()
            {
                Source = ImageSource.FromFile(tipi[i].ImagePath),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Scale = 1.3,
            };
            Frame f = new()
            {
                BackgroundColor = tipi[i].BackgroundColor,
                //CornerRadius = 75,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                GestureRecognizers = { tapGesture },
                Content = image,
                AutomationId = tipi[i].Text
            };

            Label label = new()
            {
                Text = tipi[i].Text,
                HorizontalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Center,
                FontSize = 14.18d,
                Margin = new Thickness(0, 7, 0, 0)
            };

            grid.Add(f, i, 0);
            grid.Add(label, i, 1);
        }
        frame.Content = grid;
    }

    private void TapGesture_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Frame f)
        {
            Action = f.AutomationId;
            taskCompletionSource.TrySetResult(true);
        }
    }
    public async Task<string> Show()
    {
        await MopupService.Instance.PushAsync(this);
        await taskCompletionSource.Task;
        if (MopupService.Instance.PopupStack.Contains(this))
        {
            await MopupService.Instance.RemovePageAsync(this);
        }
        return Action;
    }
    private class TipoAllegato
    {
        public string? Text { get; set; }
        public string? ImagePath { get; set; }
        public Color? BackgroundColor { get; set; }
    }

    private void PopupPage_Disappearing(object sender, EventArgs e)
    {
        taskCompletionSource.TrySetResult(false);
    }
}