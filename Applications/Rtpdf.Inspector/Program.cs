using ApplicationHelper.Theme;
using ApplicationHelper.View.Dialogs;
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
        app.Styles.Add(ColumnedListBoxTheme.Create(ColumnedListBoxTheme.Palettes.Default));

        var viewModel = new MainViewModel();
        var window = new MainView(viewModel)
        {
            Title = "Inspector",
            Width = 600,
            Height = 400,
        };

        DialogBase.SetMainWindow(window);

        window.Show();
        app.Run(window);
    }
}
