using System.Globalization;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Writing;

/// <summary>Writes a newly built PDF using a conventional cross-reference table.</summary>
public static class PdfDocumentWriter
{
    private const long MaximumConventionalXrefOffset = 9_999_999_999;

    public static void Write(
        string path,
        PdfDocumentBuilder document,
        PdfWriteOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);
        var validatedOptions = options ?? new PdfWriteOptions();
        validatedOptions.Validate();
        ValidateDocument(document);

        using var destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        WriteCore(destination, document, validatedOptions);
    }

    public static void Write(
        Stream destination,
        PdfDocumentBuilder document,
        PdfWriteOptions? options = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(document);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable", nameof(destination));
        }

        var validatedOptions = options ?? new PdfWriteOptions();
        validatedOptions.Validate();
        ValidateDocument(document);

        try
        {
            WriteCore(destination, document, validatedOptions);
        }
        finally
        {
            if (!leaveOpen)
            {
                destination.Dispose();
            }
        }
    }

    private static void WriteCore(
        Stream destination,
        PdfDocumentBuilder document,
        PdfWriteOptions options)
    {
        var initialPosition = destination.CanSeek ? destination.Position : 0;
        EnsureConventionalXrefOffset(initialPosition);
        var output = new PdfOutput(destination, options, initialPosition);
        output.WriteAscii($"%PDF-{options.Version}\n");
        output.WriteBytes([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);

        var availableReferences = document.Objects.Select(item => item.ObjectId).ToHashSet();
        var offsets = new long[document.Objects.Count];
        foreach (var indirectObject in document.Objects)
        {
            EnsureConventionalXrefOffset(output.Position);
            offsets[indirectObject.ObjectId.ObjectNumber - 1] = output.Position;
            output.WriteIndirectObject(indirectObject, availableReferences);
        }

        EnsureConventionalXrefOffset(output.Position);
        var crossReferenceOffset = output.Position;
        output.WriteAscii($"xref\n0 {document.Objects.Count + 1}\n");
        output.WriteAscii("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            output.WriteAscii(offset.ToString("D10", CultureInfo.InvariantCulture));
            output.WriteAscii(" 00000 n \n");
        }

        var trailer = new PdfDictionary
        {
            ["Size"] = new PdfInteger(document.Objects.Count + 1),
            ["Root"] = document.Root!,
        };
        if (document.Info is not null)
        {
            trailer["Info"] = document.Info;
        }

        output.WriteAscii("trailer\n");
        output.WriteObject(trailer, availableReferences);
        output.WriteAscii($"\nstartxref\n{crossReferenceOffset.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
    }

    private static void ValidateDocument(PdfDocumentBuilder document)
    {
        if (document.Root is null)
        {
            throw new PdfWriteException("A new PDF document requires a trailer /Root reference");
        }

        if (document.Objects.Count == 0)
        {
            throw new PdfWriteException("A new PDF document requires at least one indirect object");
        }

        var references = document.Objects.Select(item => item.ObjectId).ToHashSet();
        if (!references.Contains(document.Root.ObjectId))
        {
            throw new PdfWriteException("The trailer /Root reference is not registered in the document");
        }

        if (document.Info is not null && !references.Contains(document.Info.ObjectId))
        {
            throw new PdfWriteException("The trailer /Info reference is not registered in the document");
        }
    }

    private static void EnsureConventionalXrefOffset(long offset)
    {
        if (offset > MaximumConventionalXrefOffset)
        {
            throw new PdfWriteException(
                "The output is too large for a conventional 10-digit cross-reference offset");
        }
    }
}
