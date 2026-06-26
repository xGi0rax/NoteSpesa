using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Mopups.Hosting;

namespace Ergon
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcon");
                })
                .ConfigureMopups();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.ConfigureMauiHandlers(cf =>
            {
#if ANDROID
                cf.AddHandler(typeof(Picker), typeof(AndroidHandler.PickerHandlerFixAndroidFocus));
#endif
            });


            // Picker (Anno, Categorie, ecc.)
            Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
            {
            #if IOS
                handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            #endif
            });

            // DatePicker (da_data, a_data)
            Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
            {
            #if IOS
                handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            #endif
            });

            // Entry (Importo, testo a riga singola)
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
            {
            #if IOS
                handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            #endif
            });

            // Editor (Note, testo multiriga)
            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
            {
            #if IOS
                handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
            #endif
            });

            return builder.Build();
        }
    }
}
