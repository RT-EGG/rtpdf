using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Writing.Content;

/// <summary>
/// Builds an uncompressed PDF content stream from operator invocations.
/// </summary>
public sealed class PdfContentStreamBuilder
{
    private readonly MemoryStream _content = new();
    private readonly PdfContentStreamBuilderOptions _options;
    private readonly PdfWriteOptions _objectWriteOptions;
    private int _graphicsStateDepth;

    public PdfContentStreamBuilder(PdfContentStreamBuilderOptions? options = null)
    {
        _options = options ?? new PdfContentStreamBuilderOptions();
        _options.Validate();
        _objectWriteOptions = new PdfWriteOptions
        {
            MaximumObjectNestingDepth = _options.MaximumObjectNestingDepth,
        };
    }

    public int OperationCount { get; private set; }

    public int Length => checked((int)_content.Length);

    public int GraphicsStateDepth => _graphicsStateDepth;

    /// <summary>Appends an operator and a snapshot of its direct operands.</summary>
    public PdfContentStreamBuilder AppendOperation(
        string operatorName,
        params PdfObject[] operands)
    {
        ArgumentNullException.ThrowIfNull(operands);
        ValidateOperator(operatorName);
        if (OperationCount >= _options.MaximumOperations)
        {
            throw new PdfWriteException(
                $"Content stream exceeds the configured limit of {_options.MaximumOperations} operations");
        }

        if (operands.Length > _options.MaximumOperandsPerOperation)
        {
            throw new PdfWriteException(
                $"Content stream operation exceeds the configured limit of {_options.MaximumOperandsPerOperation} operands");
        }

        var nextGraphicsStateDepth = GetNextGraphicsStateDepth(operatorName);
        using var operation = new MemoryStream();
        var output = new PdfOutput(operation, _objectWriteOptions);
        var activeContainers = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var operand in operands)
        {
            ArgumentNullException.ThrowIfNull(operand);
            ValidateOperand(operand, activeContainers, depth: 0);
            output.WriteObject(operand);
            output.WriteAscii(" ");
        }

        output.WriteAscii(operatorName);
        output.WriteAscii("\n");
        if (operation.Length > _options.MaximumContentLength - _content.Length)
        {
            throw new PdfWriteException(
                $"Content stream exceeds the configured length limit of {_options.MaximumContentLength} bytes");
        }

        operation.Position = 0;
        operation.CopyTo(_content);
        OperationCount++;
        _graphicsStateDepth = nextGraphicsStateDepth;
        return this;
    }

    public PdfContentStreamBuilder SaveGraphicsState() => AppendOperation("q");

    public PdfContentStreamBuilder RestoreGraphicsState() => AppendOperation("Q");

    public PdfContentStreamBuilder ConcatenateMatrix(
        decimal a,
        decimal b,
        decimal c,
        decimal d,
        decimal e,
        decimal f) =>
        AppendOperation(
            "cm",
            CreateNumber(a),
            CreateNumber(b),
            CreateNumber(c),
            CreateNumber(d),
            CreateNumber(e),
            CreateNumber(f));

    public PdfContentStreamBuilder DrawXObject(string resourceName) =>
        DrawXObject(new PdfName(resourceName));

    public PdfContentStreamBuilder DrawXObject(PdfName resourceName)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        return AppendOperation("Do", resourceName);
    }

    /// <summary>Returns a snapshot of the decoded content bytes.</summary>
    public byte[] GetContentBytes() => _content.ToArray();

    /// <summary>Creates an unfiltered PDF stream containing the current operations.</summary>
    public PdfStream Build()
    {
        if (_graphicsStateDepth != 0)
        {
            throw new PdfWriteException(
                $"Content stream has {_graphicsStateDepth} unclosed graphics state save operations");
        }

        return new PdfStream(new PdfDictionary(), _content.ToArray());
    }

    private int GetNextGraphicsStateDepth(string operatorName)
    {
        if (operatorName == "q")
        {
            return checked(_graphicsStateDepth + 1);
        }

        if (operatorName == "Q")
        {
            if (_graphicsStateDepth == 0)
            {
                throw new PdfWriteException(
                    "Content stream cannot restore the graphics state before it is saved");
            }

            return _graphicsStateDepth - 1;
        }

        return _graphicsStateDepth;
    }

    private void ValidateOperator(string operatorName)
    {
        ArgumentException.ThrowIfNullOrEmpty(operatorName);
        if (operatorName.Length > _options.MaximumOperatorLength)
        {
            throw new PdfWriteException(
                $"Content stream operator exceeds the configured limit of {_options.MaximumOperatorLength} bytes");
        }

        foreach (var character in operatorName)
        {
            if (character is < '!' or > '~' || IsDelimiter(character))
            {
                throw new PdfWriteException(
                    "Content stream operator must contain only regular printable ASCII characters");
            }
        }

        if (operatorName is "BI" or "ID" or "EI")
        {
            throw new PdfWriteException("Inline image operators are not supported");
        }
    }

    private void ValidateOperand(PdfObject operand, ISet<object> activeContainers, int depth)
    {
        if (depth > _options.MaximumObjectNestingDepth)
        {
            throw new PdfWriteException(
                $"Content stream operand nesting exceeds the configured limit of {_options.MaximumObjectNestingDepth}");
        }

        switch (operand)
        {
            case PdfIndirectReference reference:
                throw new PdfWriteException(
                    $"Content stream operand {reference.ObjectId} R is not a direct object");
            case PdfStream:
                throw new PdfWriteException("A PDF stream cannot be nested in a content stream operand");
            case PdfArray array:
                EnterContainer(array, activeContainers);
                try
                {
                    foreach (var item in array)
                    {
                        ValidateOperand(item, activeContainers, depth + 1);
                    }
                }
                finally
                {
                    activeContainers.Remove(array);
                }

                break;
            case PdfDictionary dictionary:
                EnterContainer(dictionary, activeContainers);
                try
                {
                    foreach (var item in dictionary.Values)
                    {
                        ValidateOperand(item, activeContainers, depth + 1);
                    }
                }
                finally
                {
                    activeContainers.Remove(dictionary);
                }

                break;
        }
    }

    private static void EnterContainer(object value, ISet<object> activeContainers)
    {
        if (!activeContainers.Add(value))
        {
            throw new PdfWriteException("A content stream operand graph contains a direct cycle");
        }
    }

    private static PdfNumber CreateNumber(decimal value) =>
        decimal.Truncate(value) == value && value is >= long.MinValue and <= long.MaxValue
            ? new PdfInteger(decimal.ToInt64(value))
            : new PdfReal(value);

    private static bool IsDelimiter(char value) => value is
        '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';
}
