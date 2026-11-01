namespace Rtpdf.Core.Writing;

/// <summary>Configures canonical PDF output.</summary>
public sealed class PdfWriteOptions
{
    /// <summary>Gets the PDF header version. Supported values are 1.0 through 1.7 and 2.0.</summary>
    public string Version { get; init; } = "1.7";

    /// <summary>Gets the maximum nesting depth accepted for arrays and dictionaries.</summary>
    public int MaximumObjectNestingDepth { get; init; } = 100;

    internal void Validate()
    {
        if (!IsSupportedVersion(Version))
        {
            throw new ArgumentException(
                "PDF version must be 1.0 through 1.7 or 2.0.",
                nameof(Version));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumObjectNestingDepth, 0);
    }

    private static bool IsSupportedVersion(string? version) =>
        version is { Length: 3 } &&
        version[1] == '.' &&
        ((version[0] == '1' && version[2] is >= '0' and <= '7') ||
         (version[0] == '2' && version[2] == '0'));
}
