using System.Globalization;
using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Writing;

internal sealed class PdfOutput
{
    private static readonly Encoding Ascii = Encoding.ASCII;
    private readonly HashSet<object> _activeContainers = new(ReferenceEqualityComparer.Instance);
    private readonly Stream _destination;
    private readonly PdfWriteOptions _options;

    public PdfOutput(Stream destination, PdfWriteOptions options, long initialPosition = 0)
    {
        _destination = destination;
        _options = options;
        Position = initialPosition;
    }

    public long Position { get; private set; }

    public void WriteObject(PdfObject value, IReadOnlySet<PdfObjectId>? availableReferences = null) =>
        WriteObject(value, depth: 0, allowStream: false, availableReferences);

    public void WriteIndirectObject(
        PdfIndirectObject value,
        IReadOnlySet<PdfObjectId>? availableReferences = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteAscii(value.ObjectId.ObjectNumber.ToString(CultureInfo.InvariantCulture));
        WriteByte((byte)' ');
        WriteAscii(value.ObjectId.GenerationNumber.ToString(CultureInfo.InvariantCulture));
        WriteAscii(" obj\n");
        WriteObject(value.Value, depth: 0, allowStream: true, availableReferences);
        WriteAscii("\nendobj\n");
    }

    public void WriteAscii(string value)
    {
        var bytes = Ascii.GetBytes(value);
        _destination.Write(bytes);
        Position += bytes.Length;
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        _destination.Write(value);
        Position += value.Length;
    }

    private void WriteObject(
        PdfObject value,
        int depth,
        bool allowStream,
        IReadOnlySet<PdfObjectId>? availableReferences)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (depth > _options.MaximumObjectNestingDepth)
        {
            throw new PdfWriteException(
                $"PDF object nesting exceeds the configured limit of {_options.MaximumObjectNestingDepth}");
        }

        switch (value)
        {
            case PdfNull:
                WriteAscii("null");
                break;
            case PdfBoolean boolean:
                WriteAscii(boolean.Value ? "true" : "false");
                break;
            case PdfInteger integer:
                WriteAscii(integer.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case PdfReal real:
                WriteReal(real.Value);
                break;
            case PdfName name:
                WriteName(name);
                break;
            case PdfString text:
                WriteString(text);
                break;
            case PdfArray array:
                WriteArray(array, depth, availableReferences);
                break;
            case PdfDictionary dictionary:
                WriteDictionary(dictionary, depth, availableReferences);
                break;
            case PdfStream stream when allowStream:
                WriteStream(stream, depth, availableReferences);
                break;
            case PdfStream:
                throw new PdfWriteException("A PDF stream must be the direct value of an indirect object");
            case PdfIndirectReference reference:
                WriteReference(reference, availableReferences);
                break;
            default:
                throw new PdfWriteException($"Unsupported PDF object type: {value.GetType().FullName}");
        }
    }

    private void WriteReal(decimal value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        WriteAscii(text.Contains('.') ? text : $"{text}.0");
    }

    private void WriteName(PdfName name)
    {
        WriteByte((byte)'/');
        foreach (var value in name.Bytes.Span)
        {
            if (value is >= 33 and <= 126 && value != (byte)'#' && !IsDelimiter(value))
            {
                WriteByte(value);
            }
            else
            {
                WriteByte((byte)'#');
                WriteAscii(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
    }

    private void WriteString(PdfString value)
    {
        if (value.Kind == PdfStringKind.Hexadecimal)
        {
            WriteByte((byte)'<');
            WriteAscii(Convert.ToHexString(value.Bytes.Span));
            WriteByte((byte)'>');
            return;
        }

        WriteByte((byte)'(');
        foreach (var character in value.Bytes.Span)
        {
            switch (character)
            {
                case (byte)'(':
                case (byte)')':
                case (byte)'\\':
                    WriteByte((byte)'\\');
                    WriteByte(character);
                    break;
                case (byte)'\n':
                    WriteAscii("\\n");
                    break;
                case (byte)'\r':
                    WriteAscii("\\r");
                    break;
                case (byte)'\t':
                    WriteAscii("\\t");
                    break;
                case (byte)'\b':
                    WriteAscii("\\b");
                    break;
                case (byte)'\f':
                    WriteAscii("\\f");
                    break;
                case < 32 or >= 127:
                    WriteByte((byte)'\\');
                    WriteAscii(Convert.ToString(character, 8).PadLeft(3, '0'));
                    break;
                default:
                    WriteByte(character);
                    break;
            }
        }

        WriteByte((byte)')');
    }

    private void WriteArray(
        PdfArray value,
        int depth,
        IReadOnlySet<PdfObjectId>? availableReferences)
    {
        EnterContainer(value);
        try
        {
            WriteByte((byte)'[');
            for (var index = 0; index < value.Count; index++)
            {
                if (index > 0)
                {
                    WriteByte((byte)' ');
                }

                WriteObject(value[index], depth + 1, allowStream: false, availableReferences);
            }

            WriteByte((byte)']');
        }
        finally
        {
            _activeContainers.Remove(value);
        }
    }

    private void WriteDictionary(
        PdfDictionary value,
        int depth,
        IReadOnlySet<PdfObjectId>? availableReferences,
        int? streamLength = null)
    {
        EnterContainer(value);
        try
        {
            WriteAscii("<<");
            var entries = value
                .Where(entry => streamLength is null || entry.Key.Value != "Length")
                .OrderBy(entry => entry.Key, PdfNameByteComparer.Instance);
            foreach (var (key, item) in entries)
            {
                WriteByte((byte)' ');
                WriteName(key);
                WriteByte((byte)' ');
                WriteObject(item, depth + 1, allowStream: false, availableReferences);
            }

            if (streamLength is not null)
            {
                WriteAscii(" /Length ");
                WriteAscii(streamLength.Value.ToString(CultureInfo.InvariantCulture));
            }

            WriteAscii(" >>");
        }
        finally
        {
            _activeContainers.Remove(value);
        }
    }

    private void WriteStream(
        PdfStream value,
        int depth,
        IReadOnlySet<PdfObjectId>? availableReferences)
    {
        EnterContainer(value);
        try
        {
            WriteDictionary(value.Dictionary, depth, availableReferences, value.EncodedLength);
            WriteAscii("\nstream\n");
            WriteBytes(value.EncodedData.Span);
            WriteAscii("\nendstream");
        }
        finally
        {
            _activeContainers.Remove(value);
        }
    }

    private void WriteReference(
        PdfIndirectReference value,
        IReadOnlySet<PdfObjectId>? availableReferences)
    {
        if (availableReferences is not null && !availableReferences.Contains(value.ObjectId))
        {
            throw new PdfWriteException($"Indirect reference {value.ObjectId} R is not registered in the document");
        }

        WriteAscii(value.ObjectId.ObjectNumber.ToString(CultureInfo.InvariantCulture));
        WriteByte((byte)' ');
        WriteAscii(value.ObjectId.GenerationNumber.ToString(CultureInfo.InvariantCulture));
        WriteAscii(" R");
    }

    private void EnterContainer(object value)
    {
        if (!_activeContainers.Add(value))
        {
            throw new PdfWriteException("A direct PDF object graph contains a cycle");
        }
    }

    private void WriteByte(byte value)
    {
        _destination.WriteByte(value);
        Position++;
    }

    private static bool IsDelimiter(byte value) => value is
        (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or
        (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    private sealed class PdfNameByteComparer : IComparer<PdfName>
    {
        public static PdfNameByteComparer Instance { get; } = new();

        public int Compare(PdfName? left, PdfName? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            return left.Bytes.Span.SequenceCompareTo(right.Bytes.Span);
        }
    }
}
