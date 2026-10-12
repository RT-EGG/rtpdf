using System.Collections;

namespace Rtpdf.Core.Objects;

/// <summary>
/// An ordered, mutable PDF array.
/// </summary>
public sealed class PdfArray : PdfObject, IList<PdfObject>, IReadOnlyList<PdfObject>
{
    private readonly List<PdfObject> _items;

    public PdfArray()
    {
        _items = [];
    }

    public PdfArray(IEnumerable<PdfObject> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.Select(EnsureNotNull).ToList();
    }

    public override PdfObjectType Type => PdfObjectType.Array;

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public PdfObject this[int index]
    {
        get => _items[index];
        set => _items[index] = EnsureNotNull(value);
    }

    public void Add(PdfObject item) => _items.Add(EnsureNotNull(item));

    public void Clear() => _items.Clear();

    public bool Contains(PdfObject item) => _items.Contains(item);

    public void CopyTo(PdfObject[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public IEnumerator<PdfObject> GetEnumerator() => _items.GetEnumerator();

    public int IndexOf(PdfObject item) => _items.IndexOf(item);

    public void Insert(int index, PdfObject item) => _items.Insert(index, EnsureNotNull(item));

    public bool Remove(PdfObject item) => _items.Remove(item);

    public void RemoveAt(int index) => _items.RemoveAt(index);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static PdfObject EnsureNotNull(PdfObject? item) =>
        item ?? throw new ArgumentNullException(nameof(item), "Use PdfNull.Instance for a PDF null value.");
}
