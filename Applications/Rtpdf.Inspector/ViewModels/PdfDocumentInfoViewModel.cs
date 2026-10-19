using ApplicationHelper.Utility;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Inspector.Models;
using Rtpdf.Inspector.ViewModels.PdfStructure;

namespace Rtpdf.Inspector.ViewModels;

internal class PdfDocumentInfoViewModel
{
    private readonly PdfDictionary _model;

    public PdfDictionaryViewModel Dictionary { get; }
    /// <summary>
    /// 文書タイトル
    /// </summary>
    public string? Title { get; }
    /// <summary>
    /// 文書作成者
    /// </summary>
    public string? Author { get; }
    /// <summary>
    /// 文書概要
    /// </summary>
    public string? Subject { get; }
    /// <summary>
    /// 検索用キーワード
    /// </summary>
    public string? Keywords { get; }
    /// <summary>
    /// 文書作成元アプリ
    /// </summary>
    public string? Creator { get; }
    /// <summary>
    /// 文書変換元アプリ
    /// </summary>
    public string? Producer { get; }
    /// <summary>
    /// 作成日時
    /// </summary>
    public string? CreationDate { get; }
    /// <summary>
    /// 最終更新日時
    /// </summary>
    public string? ModDate { get; }
    /// <summary>
    /// トラッピング
    /// </summary>
    public string? Trapped { get; }

    public PdfDocumentInfoViewModel(PdfInspectionSession session, PdfDictionary model)
    {
        _model = model;
        Dictionary = new(session.ObjectStore, _model);

        var objectStore = session.ObjectStore;

        if (_model.TryGetValueAs<PdfString>("Title", objectStore, true, out var title))
        {
            Title = title.GetText();
        }

        if (_model.TryGetValueAs<PdfString>("Author", objectStore, true, out var author))
        {
            Author = author.GetText();
        }

        if (_model.TryGetValueAs<PdfString>("Subject", objectStore, true, out var subject))
        {
            Subject = subject.GetText();
        }

        if (_model.TryGetValueAs<PdfString>("Keywords", objectStore, true, out var keywords))
        {
            Keywords = keywords.GetText();
        }

        if (_model.TryGetValueAs<PdfString>("Creator", objectStore, true, out var creator))
        {
            Creator = creator.GetText();
        }

        if (_model.TryGetValueAs<PdfString>("Producer", objectStore, true, out var producer))
        {
            Producer = producer.GetText();
        }

        if (_model.TryGetValueAs<PdfString>("CreationDate", objectStore, true, out var creationDate))
        {
            var text = creationDate.GetText();
            if (PdfDateParser.TryParse(text, out var dateTime))
            {
                CreationDate = dateTime.ToString("yyyy/MM/dd HH:mm:ss");
            }
            else
            {
                CreationDate = text;
            }
        }

        if (_model.TryGetValueAs<PdfString>("ModDate", objectStore, true, out var modDate))
        {
            var text = modDate.GetText();
            if (PdfDateParser.TryParse(text, out var dateTime))
            {
                ModDate = dateTime.ToString("yyyy/MM/dd HH:mm:ss");
            }
            else
            {
                ModDate = text;
            }
        }

        if (_model.TryGetValueAs<PdfName>("Trapped", objectStore, true, out var trapped))
        {
            Trapped = trapped.ToString();
        }
    }
}
