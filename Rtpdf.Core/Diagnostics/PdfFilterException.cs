namespace Rtpdf.Core.Diagnostics;

/// <summary>
/// The exception thrown when a PDF stream filter cannot decode its input.
/// </summary>
public sealed class PdfFilterException : IOException
{
    public PdfFilterException(string message, string? filterName = null, Exception? innerException = null)
        : base(FormatMessage(message, filterName), innerException)
    {
        FilterName = filterName;
    }

    public string? FilterName { get; }

    private static string FormatMessage(string message, string? filterName) =>
        filterName is null ? message : $"{message} (filter /{filterName}).";
}
