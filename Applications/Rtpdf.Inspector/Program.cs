using Avalonia;
using Avalonia.Controls;
using Avalonia.Themes.Fluent;
using Rtpdf.Inspector.ViewModels;
using Rtpdf.Inspector.Views;
using System;

namespace Rtpdf.Inspector;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppBuilder
            .Configure<Application>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace()
            .Start(AppMain, args);
    }

    private static void AppMain(Application app, string[] args)
    {
        app.Styles.Add(new FluentTheme());

        var window = new MainView
        {
            DataContext = new MainViewModel(),
            Title = "Inspector",
            Width = 300,
            Height = 150,
        };

        window.Show();
        app.Run(window);
    }
}
