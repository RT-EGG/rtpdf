using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;
using System.Diagnostics.CodeAnalysis;

namespace ApplicationHelper.Utility;

public static class PdfObjectHelper
{
    /// <summary>
    /// 指定したキーの値を型 T として取得し、成功すれば true を返す。間接参照はオプションでストアから解決できる。
    /// </summary>
    /// <remarks>PdfIndirectReference を検出した場合、recursive が true なら store
    /// を使ってキャッシュ内のオブジェクトを解決する。解決に失敗するか型が一致しない場合は取得失敗となる。</remarks>
    /// <typeparam name="T">取得対象の PdfObject の型。</typeparam>
    /// <param name="dict">値を検索する PdfDictionary。</param>
    /// <param name="key">検索するキー名。</param>
    /// <param name="store">間接参照を解決するための PdfObjectStore。</param>
    /// <param name="recursive">間接参照を解決してキャッシュから値を取得する場合は true。</param>
    /// <param name="value">見つかった値を格納する出力パラメーター。見つからない場合は既定値になる。</param>
    /// <returns>キーが存在し、値が T 型にキャストできた、または recursive が true の場合に間接参照を解決して T 型の値を取得できた場合に true。そうでなければ false。</returns>
    public static bool TryGetValueAs<T>(
        this PdfDictionary dict,
        string key,
        PdfObjectStore store,
        bool recursive,
        [NotNullWhen(true)] out T? value) where T : PdfObject
    {
        value = default;
        if (!dict.TryGetValue(key, out var obj))
        {
            return false;
        }

        if (obj is T)
        {
            value = (T)obj;
            return true;
        }

        if (recursive && obj is PdfIndirectReference indirectRef)
        {
            if (store.TryGetIndirectObject(indirectRef.ObjectId, out var indirectObj) && indirectObj.Value is T result)
            {
                value = result;
                return true;
            }
            return false;
        }

        return false;
    }
}
