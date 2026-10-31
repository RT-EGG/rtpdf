using ApplicationHelper;
using ApplicationHelper.ViewModel;
using Avalonia.Media.Imaging;
using Rtpdf.BookPack.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Rtpdf.BookPack.ViewModels;

[HotReloadTrigger]
internal class FileListViewModel : ViewModelBase
{
    public ListBoxViewModel ListBox { get; } = new();

    public async Task LoadFilesAsync(IEnumerable<string> filePathes, Action<string> onFileError)
    {
        await Task.Run(() => LoadFilesCore(filePathes, onFileError));
    }

    private void LoadFilesCore(IEnumerable<string> filePathes, Action<string> onFileError)
    {
        foreach (var filePath in filePathes)
        {
            try
            {
                Bitmap? bitmap = new Bitmap(filePath);
                ListBox.Items.Add(new FileViewModel(new ImageFileModel(filePath, bitmap)));
            }
            catch
            {
                ListBox.Items.Add(new FileViewModel(new ImageFileModel(filePath, null)));
                onFileError?.Invoke(filePath);
            }
        }
    }

    public void SortByName()
    {
        var tmp = new List<FileViewModel>(ListBox.Items);
        tmp.Sort(new NameComparer());

        ListBox.Items.Clear();
        foreach (var newItem in tmp)
        {
            ListBox.Items.Add(newItem);
        }
    }

    public void SortByNaturalOrder()
    {
        var tmp = new List<FileViewModel>(ListBox.Items);
        tmp.Sort(new NaturalOrderComparer());

        ListBox.Items.Clear();
        foreach (var newItem in tmp)
        {
            ListBox.Items.Add(newItem);
        }
    }

    public void Clear()
    {
        foreach (var item in ListBox.Items)
        {
            item.Dispose();
        }
        ListBox.Items.Clear();
    }

    public class ColumnViewModel : ColumnedListBoxColumnViewModel
    {
        public const string ID_FileName = "FileName";
        public const string ID_Resolution = "Resolution";

        public string Text { get; set; } = string.Empty;
    }

    public class ListBoxViewModel : ColumnedListBoxViewModel<ColumnViewModel, FileViewModel>
    {
        public ListBoxViewModel()
        {
            Columns = [
                new ColumnViewModel
                {
                    ID = ColumnViewModel.ID_FileName,
                    Text = "ファイル名",
                    Width = 600.0,
                },
                new ColumnViewModel
                {
                    ID = ColumnViewModel.ID_Resolution,
                    Text = "解像度",
                    Width = 100.0,
                },
            ];
        }
    }

    private class NameComparer : IComparer<FileViewModel>
    {
        private readonly IComparer<string> _stringComparer = Comparer<string>.Default;

        public int Compare(FileViewModel? x, FileViewModel? y)
        {
            return _stringComparer.Compare(x?.FileName, y?.FileName);
        }
    }

    private class NaturalOrderComparer : IComparer<FileViewModel>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public int Compare(string x, string y)
        {
            return StrCmpLogicalW(x, y);
        }

        public int Compare(FileViewModel? x, FileViewModel? y)
        {
            return Compare(x?.FileName ?? "", y?.FileName ?? "");
        }
    }

}
