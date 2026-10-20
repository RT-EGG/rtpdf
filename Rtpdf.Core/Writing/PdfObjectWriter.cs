using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Writing;

/// <summary>Serializes individual PDF objects using canonical syntax.</summary>
public static class PdfObjectWriter
{
    public static void Write(
        Stream destination,
        PdfObject value,
        PdfWriteOptions? options = null,
        bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(value);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable", nameof(destination));
        }

        var validatedOptions = options ?? new PdfWriteOptions();
        validatedOptions.Validate();
        try
        {
            new PdfOutput(destination, validatedOptions).WriteObject(value);
        }
        finally
        {
            if (!leaveOpen)
            {
                destination.Dispose();
            }
        }
    }

    public static void WriteIndirectObject(
        Stream destination,
        PdfIndirectObject value,
        PdfWriteOptions? options = null,
        bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(value);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable", nameof(destination));
        }

        var validatedOptions = options ?? new PdfWriteOptions();
        validatedOptions.Validate();
        try
        {
            new PdfOutput(destination, validatedOptions).WriteIndirectObject(value);
        }
        finally
        {
            if (!leaveOpen)
            {
                destination.Dispose();
            }
        }
    }
}
