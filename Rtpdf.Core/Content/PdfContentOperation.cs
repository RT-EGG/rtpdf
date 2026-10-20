using System.Collections.ObjectModel;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Content;

/// <summary>
/// A syntactically parsed content stream operator and the operands that precede it.
/// </summary>
public sealed class PdfContentOperation
{
    private readonly ReadOnlyCollection<PdfObject> _operands;

    internal PdfContentOperation(string operatorName, IEnumerable<PdfObject> operands, int offset)
    {
        ArgumentException.ThrowIfNullOrEmpty(operatorName);
        ArgumentNullException.ThrowIfNull(operands);

        Operator = operatorName;
        _operands = Array.AsReadOnly(operands.ToArray());
        Offset = offset;
    }

    /// <summary>
    /// Gets the case-sensitive PDF operator name.
    /// </summary>
    public string Operator { get; }

    /// <summary>
    /// Gets a snapshot of the direct objects preceding the operator.
    /// </summary>
    public IReadOnlyList<PdfObject> Operands => _operands;

    /// <summary>
    /// Gets the zero-based byte offset at which the operator begins in the decoded stream.
    /// </summary>
    public int Offset { get; }

    public override string ToString() => $"{Operator} ({Operands.Count} operands) at {Offset}";
}
