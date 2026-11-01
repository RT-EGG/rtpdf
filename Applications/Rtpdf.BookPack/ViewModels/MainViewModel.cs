using ApplicationHelper.ViewModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Rtpdf.BookPack.Core;
using System.Threading.Tasks;

namespace Rtpdf.BookPack.ViewModels;

internal partial class MainViewModel : ViewModelBase
{
    public FileListViewModel FileListViewModel { get; } = new();

    [ObservableProperty]
    public partial bool IsShowExportOption { get; set; } = false;
    public bool IsSaving => SaveProgressPercent.HasValue;
    [ObservableProperty]
    public partial double? SaveProgressPercent { get; private set; } = null;

    public async Task SaveToFileAsync(string filePath, BookPackOptions options)
    {
        UpdateSaveProgressPercent(0.0);
        try
        {
            await FileListViewModel.SaveToFileAsync(filePath, options, progress =>
            {
                UpdateSaveProgressPercent(progress);
            });
        }
        finally
        {
            UpdateSaveProgressPercent(null);
        }
    }

    private void UpdateSaveProgressPercent(double? progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SaveProgressPercent = progress;
        });
    }

    partial void OnSaveProgressPercentChanged(double? oldValue, double? newValue)
    {
        if (oldValue.HasValue != newValue.HasValue)
        {
            OnPropertyChanged(nameof(IsSaving));
        }
    }
}
