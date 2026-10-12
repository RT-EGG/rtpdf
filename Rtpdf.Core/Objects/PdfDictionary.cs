using System.Collections;

namespace Rtpdf.Core.Objects;

/// <summary>
/// A mutable PDF dictionary. Unrecognized entries are retained like any other entry.
/// </summary>
public sealed class PdfDictionary : PdfObject, IDictionary<PdfName, PdfObject>, IReadOnlyDictionary<PdfName, PdfObject>
{
    private readonly Dictionary<PdfName, PdfObject> _entries;

    public PdfDictionary()
    {
        _entries = [];
    }

    public PdfDictionary(IEnumerable<KeyValuePair<PdfName, PdfObject>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [];
        foreach (var (key, value) in entries)
        {
            Add(key, value);
        }
    }

    public override PdfObjectType Type => PdfObjectType.Dictionary;

    public int Count => _entries.Count;

    public bool IsReadOnly => false;

    public ICollection<PdfName> Keys => _entries.Keys;

    IEnumerable<PdfName> IReadOnlyDictionary<PdfName, PdfObject>.Keys => _entries.Keys;

    public ICollection<PdfObject> Values => _entries.Values;

    IEnumerable<PdfObject> IReadOnlyDictionary<PdfName, PdfObject>.Values => _entries.Values;

    public PdfObject this[PdfName key]
    {
        get => _entries[key ?? throw new ArgumentNullException(nameof(key))];
        set => _entries[key ?? throw new ArgumentNullException(nameof(key))] = EnsureNotNull(value);
    }

    public PdfObject this[string key]
    {
        get => this[new PdfName(key)];
        set => this[new PdfName(key)] = value;
    }

    public void Add(PdfName key, PdfObject value) =>
        _entries.Add(key ?? throw new ArgumentNullException(nameof(key)), EnsureNotNull(value));

    public void Add(KeyValuePair<PdfName, PdfObject> item) => Add(item.Key, item.Value);

    public void Clear() => _entries.Clear();

    public bool Contains(KeyValuePair<PdfName, PdfObject> item) =>
        ((ICollection<KeyValuePair<PdfName, PdfObject>>)_entries).Contains(item);

    public bool ContainsKey(PdfName key) => _entries.ContainsKey(key ?? throw new ArgumentNullException(nameof(key)));

    public bool ContainsKey(string key) => ContainsKey(new PdfName(key));

    public void CopyTo(KeyValuePair<PdfName, PdfObject>[] array, int arrayIndex) =>
        ((ICollection<KeyValuePair<PdfName, PdfObject>>)_entries).CopyTo(array, arrayIndex);

    public IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() => _entries.GetEnumerator();

    public bool Remove(PdfName key) => _entries.Remove(key ?? throw new ArgumentNullException(nameof(key)));

    public bool Remove(string key) => Remove(new PdfName(key));

    public bool Remove(KeyValuePair<PdfName, PdfObject> item) =>
        ((ICollection<KeyValuePair<PdfName, PdfObject>>)_entries).Remove(item);

    public bool TryGetValue(PdfName key, out PdfObject value) =>
        _entries.TryGetValue(key ?? throw new ArgumentNullException(nameof(key)), out value!);

    public bool TryGetValue(string key, out PdfObject value) => TryGetValue(new PdfName(key), out value);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static PdfObject EnsureNotNull(PdfObject? value) =>
        value ?? throw new ArgumentNullException(nameof(value), "Use PdfNull.Instance for a PDF null value.");
}
