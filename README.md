# rtpdf

PDFの内部構造を外部PDFライブラリに依存せず解析するための、開発中の.NETライブラリです。

> [!IMPORTANT]
> 現在の`Rtpdf.Core`は部分実装です。任意のPDFを開ける完成済みReaderや、PDFへの書き込み・描画機能はまだ提供していません。

## 動作環境

- .NET 10
- `Rtpdf.Core`は.NET BCL以外のPDFライブラリに依存しません

## PDFバージョンの扱い

現時点では、ファイル先頭の`%PDF-x.y`ヘッダーを解析してバージョン適合性を判定する機能はありません。テストでは主に`%PDF-1.7`の入力を使用していますが、これは「PDF 1.7の全機能に対応している」という意味ではありません。

実際に読み込めるかどうかは、PDFのバージョン番号よりも、下表の構造やFilterだけでファイルが構成されているかによって決まります。例えば、PDF 1.5以降でも従来形式のxref tableを使うファイルは対応範囲に入る可能性があります。一方、PDF 1.5のxref streamやobject streamを使うファイルは現在読み込めません。PDF 2.0への適合性も未検証です。

| PDFヘッダーバージョン | 互換性の目安 | 制限 |
| --- | :---: | --- |
| PDF 1.0～1.4 | ⚠️ 部分対応 | 下記の実装済み構造だけを使用するファイル。各バージョンへの完全適合性は未検証 |
| PDF 1.5～1.7 | ⚠️ 部分対応 | xref tableまたはxref streamと、object stream外の間接オブジェクトを使用するファイルに限定 |
| PDF 2.0 | ⚠️ 未検証 | 古い構文だけのファイルを解析できる可能性はあるが、互換性は保証しない |

## 対応状況

凡例:

- ✅ 対応
- ⚠️ 部分対応または制限あり
- ❌ 未対応

| 領域 | 状況 | 現在の範囲 |
| --- | :---: | --- |
| PDFオブジェクトモデル | ✅ | null、真偽値、整数、実数、Name、Literal/Hex String、Array、Dictionary、Stream、間接参照 |
| Lexer・直接オブジェクトParser | ✅ | 対応済みオブジェクトの字句解析と構文解析 |
| PDF日時文字列 | ✅ | `D:YYYYMMDDHHmmSSOHH'mm'`。年以降の末尾フィールドとPDF 1.xの省略可能な`D:`に対応 |
| 間接オブジェクト | ✅ | object番号・generation番号の検証、キャッシュ、参照チェーンと循環の検出 |
| xref table・trailer | ✅ | 従来形式のxref tableを解析。incremental updateの`/Prev`チェーンをたどり、最新revisionを優先して統合 |
| xref stream | ⚠️ | PDF 1.5形式、`/W`・`/Index`、type 0/1/2、Flate圧縮、xref tableとの混在・`/XRefStm`に対応。Filter制限あり |
| object stream | ❌ | 未対応 |
| Catalog | ✅ | trailerの`/Root`からCatalogを読み込み、`/Type`と`/Pages`を検証 |
| Page Tree | ✅ | 階層とページ順を読み込み、循環・重複・`/Parent`・`/Count`を検証 |
| ページ継承属性 | ✅ | `MediaBox`、`CropBox`、`Resources`、`Rotate` |
| Stream境界 | ✅ | 直接・間接`/Length`に対応。データ中の`endstream`文字列には依存しない |
| Stream Filter | ⚠️ | `/FlateDecode`のみ。詳細は次節を参照 |
| Content Streamの命令解析 | ❌ | Operatorやgraphics stateの解析は未対応 |
| 暗号化PDF | ❌ | Security Handler未対応 |
| Writer・保存 | ❌ | 新規保存、完全保存、incremental updateのいずれも未対応 |
| 描画・レンダリング | ❌ | 対象外。将来は別レイヤーとして検討 |

### Incremental Update

`PdfCrossReferenceParser.Parse()`は、末尾の`startxref`からxref tableまたはxref streamを読み、`/Prev`をたどって過去のrevisionを統合します。同じobject番号が複数のrevisionにある場合は最新のentryが優先されるため、更新されたobjectだけでなく、最新revisionでfreeになったobjectも反映されます。統合後のentryは`PdfCrossReferenceTable.Entries`、物理的な各xref table・streamは新しい順に`PdfCrossReferenceTable.Sections`から参照できます。

対象はxref tableまたはxref streamを使ったincremental updateの**読み込み**です。両形式がrevision間で混在するファイルと、同一revisionで`/XRefStm`を使うhybrid-reference fileにも対応します。object streamとincremental updateの書き込み・保存には対応していません。`/Prev`の循環、設定された最大revision数・最大entry数、最新trailerの`/Size`と統合結果の不整合は`PdfCrossReferenceException`になります。

### xref stream

`PdfCrossReferenceParser`は、`startxref`が従来形式のxref tableまたはxref streamのどちらを指しているかを判別します。xref streamの`/W`は各fieldが0～8 bytes、`/Index`は非重複かつobject番号が昇順の範囲に対応します。`/Index`省略時は`[0 /Size]`として扱います。entry type 0（free）、type 1（ファイル内offset）、type 2（object stream番号とindex）を保持し、未知のtypeは将来拡張との互換性のため未登録entryとして扱います。

stream dataは無Filterまたは`/FlateDecode`を読み込めます。複数Filter、未対応Filter、FlateのPredictorなどの範囲は後述の「StreamとFilterの対応」と同じです。type 2 entryの位置情報は`PdfObjectLocation.ObjectStreamNumber`と`ObjectStreamIndex`から参照できますが、object stream内のobject解決はまだ未対応で、`PdfObjectStore`は`PdfObjectResolutionException`を返します。

### PDF日時文字列

`PdfDateParser.TryParse`は、PDFへ記録されたローカル日時を`DateTimeKind.Unspecified`の`DateTime`として返します。UTCオフセットは`DateTime`に保持できないため、必要な場合は3引数overloadで`TimeSpan?`として別途取得できます。タイムゾーンが省略された場合のoffsetはnull、`Z`の場合は`TimeSpan.Zero`です。

```csharp
if (PdfDateParser.TryParse(
    "D:20261019153045+09'00'",
    out var localDateTime,
    out var utcOffset))
{
    // localDateTime: 2026-10-19 15:30:45 (Kind = Unspecified)
    // utcOffset:    +09:00
}
```

月・日を省略した場合は`01`、時・分・秒を省略した場合は`00`を使用します。不正な暦日、途中のフィールドだけを飛ばした形式、仕様どおりのapostropheを持たないUTC offsetはfalseになります。

## StreamとFilterの対応

`PdfStream`はファイル中の圧縮済みデータを保持します。展開は`PdfStreamDecoder`を明示的に呼び出したときだけ行われ、元の圧縮済みデータは変更されません。

### Filter

| Filter | 状況 | 備考 |
| --- | :---: | --- |
| `FlateDecode` | ⚠️ | zlib形式のDEFLATEを展開。Predictorには制限あり |
| `ASCIIHexDecode` | ❌ | 未対応 |
| `ASCII85Decode` | ❌ | 未対応 |
| `LZWDecode` | ❌ | 未対応 |
| `RunLengthDecode` | ❌ | 未対応 |
| `CCITTFaxDecode` | ❌ | 未対応 |
| `JBIG2Decode` | ❌ | 未対応 |
| `DCTDecode` | ❌ | 未対応 |
| `JPXDecode` | ❌ | 未対応 |
| `Crypt` | ❌ | 未対応 |

`/Filter`は単一NameとArrayの両方に対応し、Arrayの場合は宣言順にFilterを適用します。現在は登録済みFilterが`FlateDecode`だけなので、それ以外を含む場合は`PdfFilterException`になります。

`/Filter`と`/DecodeParms`の間接参照は、`PdfObjectStore`を渡して`PdfStreamDecoder`を作成した場合に解決できます。`/DecodeParms`はDictionary、またはFilter Arrayと同数のDictionary/null Arrayを受け付けます。

### FlateDecode Predictor

| `/Predictor` | 方式 | 状況 |
| ---: | --- | :---: |
| 省略または`1` | Predictorなし | ✅ |
| `2` | TIFF Predictor | ❌ |
| `10`～`15` | PNG Predictor | ❌ |

未対応Predictorはデータを誤って返さず、`PdfFilterException`を送出します。`Colors`、`BitsPerComponent`、`Columns`はPredictor実装と合わせて今後対応する予定です。

## 現在の主要な安全制限

信頼できないPDFによる過剰なメモリ消費や無限再帰を避けるため、各ParserとReaderには設定可能な上限があります。主な既定値は次のとおりです。

| 設定 | 既定値 | 対象 |
| --- | ---: | --- |
| `MaximumEncodedStreamLength` | 256 MiB | 読み込む圧縮済みStream |
| `MaximumDecodedStreamLength` | 256 MiB | Filter展開後のStream。展開処理中にも検査 |
| `MaximumFilterCount` | 16 | 1つのStreamへ適用するFilter数 |
| `MaximumReferenceDepth` | 100 | 間接参照チェーン |
| `MaximumCachedObjects` | 100,000 | ObjectStoreのキャッシュ |
| `MaximumPageTreeDepth` | 100 | Page Treeの深さ |
| `MaximumPageTreeNodes` | 1,000,000 | Page Treeの要素数 |
| `MaximumPages` | 1,000,000 | ページ数 |

これらは対応可能な最大PDF仕様を表す値ではなく、現在の既定の防御上限です。入力元や用途に応じてOptionsから変更できます。

## 未対応機能に遭遇した場合

未対応形式を別形式として推測して読み進めることはせず、原則として次の例外で明示的に失敗します。

- PDF構文の不正: `PdfParseException`
- xref/trailerの不正: `PdfCrossReferenceException`
- 間接参照の解決失敗: `PdfObjectResolutionException`
- Catalog/Page Treeの構造不正: `PdfDocumentStructureException`
- Stream Filter・Predictorの未対応または展開失敗: `PdfFilterException`

機能を追加・変更した際は、このREADMEの対応表と制限事項も同じコミットで更新します。
