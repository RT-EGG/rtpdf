using ApplicationHelper.ViewModel;
using Rtpdf.Core.Documents;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;
using Rtpdf.Inspector.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Rtpdf.Inspector.ViewModels;

internal partial class MainViewModel : ViewModelBase
{
    public PdfFileViewModel? PdfFileViewModel { get; private set; }

    public async Task LoadFromFileAsync(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        stream.Seek(0, SeekOrigin.Begin);
        await LoadFromStreamAsync(stream);
    }

    public async Task LoadFromStreamAsync(Stream stream)
    {
        try
        {
            using var reader = new BinaryReader(stream);
            var source = reader.ReadBytes((int)stream.Length);

            var xref = new PdfCrossReferenceParser(source).Parse();
            var objectStore = new PdfObjectStore(source, xref);
            var catalog = new PdfCatalogReader(xref, objectStore).Read();
            var streamDecoder = new PdfStreamDecoder(objectStore);

            PdfFileViewModel = new(
                new PdfInspectionSession(
                    source,
                    xref,
                    objectStore,
                    catalog,
                    streamDecoder
                )
            );
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log the error, show a message to the user, etc.)
            Console.WriteLine($"Error loading PDF: {ex.Message}");
            PdfFileViewModel = null;
        }
    }
}
