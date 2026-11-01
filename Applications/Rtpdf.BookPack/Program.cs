using ApplicationHelper.Theme;
using ApplicationHelper.View.Classes;
using ApplicationHelper.View.Dialogs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Themes.Fluent;
using Rtpdf.BookPack.ViewModels;
using Rtpdf.BookPack.Views;
using System;

namespace Rtpdf.BookPack;

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
        app.Styles.Add(ColumnedListBoxTheme.Create(ColumnedListBoxTheme.Palettes.Default));
        TextBlockClasses.SetupStyles(app.Styles);

        var window = new MainView(new MainViewModel())
        {
            Title = "BookPack",
            Width = 800,
            Height = 600,
        };

        DialogBase.SetMainWindow(window);

        window.Show();
        app.Run(window);
    }
}
