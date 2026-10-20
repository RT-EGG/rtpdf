using ApplicationHelper.ViewModel;
using Rtpdf.Core.Documents;
using Rtpdf.Inspector.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Rtpdf.Inspector.ViewModels;

internal partial class MainViewModel : ViewModelBase
{
    public PdfFileViewModel? PdfFileViewModel { get; private set; }

    public Task LoadFromFileAsync(string filePath)
    {
        Load(() => PdfDocumentReader.Open(filePath));
        return Task.CompletedTask;
    }

    public Task LoadFromStreamAsync(Stream stream)
    {
        Load(() => PdfDocumentReader.Open(stream, leaveOpen: true));
        return Task.CompletedTask;
    }

    private void Load(Func<PdfDocument> open)
    {
        PdfInspectionSession? session = null;
        try
        {
            session = new PdfInspectionSession(open());
            var next = new PdfFileViewModel(session);
            session = null;
            PdfFileViewModel?.Dispose();
            PdfFileViewModel = next;
        }
        catch (Exception ex)
        {
            session?.Dispose();
            // Handle exceptions (e.g., log the error, show a message to the user, etc.)
            Console.WriteLine($"Error loading PDF: {ex.Message}");
            PdfFileViewModel?.Dispose();
            PdfFileViewModel = null;
        }
    }
}
