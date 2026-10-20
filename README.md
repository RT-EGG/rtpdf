# rtpdf

PDFの内部構造を外部PDFライブラリに依存せず解析するための、開発中の.NETライブラリです。

> [!IMPORTANT]
> 現在の`Rtpdf.Core`は部分実装です。任意のPDFを開ける完成済みReaderや、PDFへの書き込み・描画機能はまだ提供していません。

## 動作環境

- .NET 10
- `Rtpdf.Core`は.NET BCL以外のPDFライブラリに依存しません

## PDFを開く

`PdfDocumentReader.Open(...)`はファイルパス、`Stream`、`ReadOnlyMemory<byte>`、`PdfByteSource`を受け取り、xref、ObjectStore、Catalog、StreamDecoderをまとめた`PdfDocument`を返します。

```csharp
using var document = PdfDocumentReader.Open("sample.pdf");

foreach (var page in document.Catalog.PageTree.Pages)
{
    // page.Dictionary、page.MediaBoxなどを参照
}
```

暗号化PDFには`new PdfDocumentReaderOptions { Password = "..." }`を渡します。パスワードを省略した場合は空のパスワードで認証します。

`Stream` overloadは現在位置をPDFの先頭として扱います。seek可能なStreamは必要なobjectのoffsetへ移動して読み、`PdfDocument.Dispose()`時に閉じます。`leaveOpen: true`を指定するとDocumentを破棄した後も元のStreamを開いたままにします。seekできないStreamは、設定された上限まで一時ファイルへ退避してrandom accessします。

`PdfByteSource`は長さとoffset指定読み込みを提供する入力抽象です。`FromMemory`、`FromStream`、`OpenFile`のfactoryに加え、派生型を実装して独自の入力元を渡せます。ObjectStoreはsource全体を保持せず、xref offsetから必要な間接objectの範囲だけを読み込み、解析結果をキャッシュします。`PdfByteSource`を直接渡した場合は呼び出し側がsourceの寿命を管理します。

現時点ではxref parserだけが`Open`中にsource全体の一時bufferを使用します。このbufferはOpen完了後には保持されません。xrefの末尾探索とsection解析を完全なrandom accessへ移行する作業、および2 GiBを超えるsourceへの対応は未完了です。

## PDFバージョンの扱い

現時点では、ファイル先頭の`%PDF-x.y`ヘッダーを解析してバージョン適合性を判定する機能はありません。テストでは主に`%PDF-1.7`の入力を使用していますが、これは「PDF 1.7の全機能に対応している」という意味ではありません。

実際に読み込めるかどうかは、PDFのバージョン番号よりも、下表の構造やFilterだけでファイルが構成されているかによって決まります。PDF 1.5で導入されたxref streamとobject streamも下記の制限内で読み込めます。PDF 2.0への適合性は未検証です。

| PDFヘッダーバージョン | 互換性の目安 | 制限 |
| --- | :---: | --- |
| PDF 1.0～1.4 | ⚠️ 部分対応 | 下記の実装済み構造だけを使用するファイル。各バージョンへの完全適合性は未検証 |
| PDF 1.5～1.7 | ⚠️ 部分対応 | xref table、xref stream、object streamを下記の制限内で読み込み。各バージョンへの完全適合性は未検証 |
| PDF 2.0 | ⚠️ 未検証 | 古い構文だけのファイルを解析できる可能性はあるが、互換性は保証しない |

## 対応状況

凡例:

- ✅ 対応
- ⚠️ 部分対応または制限あり
- ❌ 未対応

| 領域 | 状況 | 現在の範囲 |
| --- | :---: | --- |
| Document Reader・ByteSource | ⚠️ | ObjectStoreはoffset単位で遅延読み込み。xref parserはOpen時の一時全量bufferを使用 |
| PDFオブジェクトモデル | ✅ | null、真偽値、整数、実数、Name、Literal/Hex String、Array、Dictionary、Stream、間接参照。文字列はUTF-16 BOMとPDFDocEncodingをデコード |
| Lexer・直接オブジェクトParser | ✅ | 対応済みオブジェクトの字句解析と構文解析 |
| PDF日時文字列 | ✅ | `D:YYYYMMDDHHmmSSOHH'mm'`。年以降の末尾フィールドとPDF 1.xの省略可能な`D:`に対応 |
| 間接オブジェクト | ✅ | object番号・generation番号の検証、キャッシュ、参照チェーンと循環の検出 |
| xref table・trailer | ✅ | 従来形式のxref tableを解析。incremental updateの`/Prev`チェーンをたどり、最新revisionを優先して統合 |
| xref stream | ⚠️ | PDF 1.5形式、`/W`・`/Index`、type 0/1/2、Flate圧縮、xref tableとの混在・`/XRefStm`に対応。Filter制限あり |
| object stream | ⚠️ | PDF 1.5形式の`/ObjStm`、xref type 2 entry、無Filter・Flate圧縮に対応。Filter制限あり |
| Catalog | ✅ | trailerの`/Root`からCatalogを読み込み、`/Type`と`/Pages`を検証 |
| Page Tree | ✅ | 階層とページ順を読み込み、循環・重複・`/Parent`・`/Count`を検証 |
| ページ継承属性 | ✅ | `MediaBox`、`CropBox`、`Resources`、`Rotate` |
| Stream境界 | ✅ | 直接・間接`/Length`に対応。データ中の`endstream`文字列には依存しない |
| Stream Filter | ⚠️ | Flate、LZW、ASCIIHex、ASCII85、RunLengthに対応。画像Codec系は未対応 |
| Content Streamの命令解析 | ⚠️ | 展開済みStreamから直接operandとoperatorを命令単位に解析。inline imageとoperator semanticsは未対応 |
| 暗号化PDF | ⚠️ | Standard Security Handler Revision 2～4、RC4、AES-128、user/owner passwordに対応 |
| Writer・保存 | ❌ | 新規保存、完全保存、incremental updateのいずれも未対応 |
| 描画・レンダリング | ❌ | 対象外。将来は別レイヤーとして検討 |

### Incremental Update

`PdfCrossReferenceParser.Parse()`は、末尾の`startxref`からxref tableまたはxref streamを読み、`/Prev`をたどって過去のrevisionを統合します。同じobject番号が複数のrevisionにある場合は最新のentryが優先されるため、更新されたobjectだけでなく、最新revisionでfreeになったobjectも反映されます。統合後のentryは`PdfCrossReferenceTable.Entries`、物理的な各xref table・streamは新しい順に`PdfCrossReferenceTable.Sections`から参照できます。

対象はxref tableまたはxref streamを使ったincremental updateの**読み込み**です。両形式がrevision間で混在するファイルと、同一revisionで`/XRefStm`を使うhybrid-reference fileにも対応します。xref type 2 entryが参照するobject streamも解決できます。incremental updateの書き込み・保存には対応していません。`/Prev`の循環、設定された最大revision数・最大entry数、最新trailerの`/Size`と統合結果の不整合は`PdfCrossReferenceException`になります。

### xref stream

`PdfCrossReferenceParser`は、`startxref`が従来形式のxref tableまたはxref streamのどちらを指しているかを判別します。xref streamの`/W`は各fieldが0～8 bytes、`/Index`は非重複かつobject番号が昇順の範囲に対応します。`/Index`省略時は`[0 /Size]`として扱います。entry type 0（free）、type 1（ファイル内offset）、type 2（object stream番号とindex）を保持し、未知のtypeは将来拡張との互換性のため未登録entryとして扱います。

stream dataは無Filterまたは後述の対応済みFilterを読み込めます。複数Filter、未対応Filter、Predictorなどの範囲は「StreamとFilterの対応」と同じです。type 2 entryの位置情報は`PdfObjectLocation.ObjectStreamNumber`と`ObjectStreamIndex`から参照でき、`PdfObjectStore`がobject stream内のobjectを解決します。

### object stream

`PdfObjectStore`はxref streamのtype 2 entryをたどり、`/Type /ObjStm`のstreamを展開して、`/N`個のobject番号・offset pairと`/First`以降の直接オブジェクトを読み込みます。object stream本体と展開結果はキャッシュされるため、同じstream内の複数objectを解決しても再展開しません。Inspectorからもtype 2 entryを選択して内容を表示できます。

object stream本体はgeneration 0の非圧縮間接オブジェクトである必要があります。格納objectもgeneration 0に限定し、xref entryのindexとheaderのobject番号が一致すること、offsetが昇順で範囲内にあることを検証します。stream object、格納object全体が単独の間接参照となる形式、暗号化辞書をobject streamへ格納する形式は受け付けません。利用できるFilterとPredictorは後述の「StreamとFilterの対応」と同じです。

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

### PDF文字列のEncoding

`PdfString.GetText()`は先頭にUTF-16 BOMがある場合はUTF-16BEまたはUTF-16LEとして、それ以外はPDFDocEncodingとしてデコードします。元のbyte列は`PdfString.Bytes`に保持されます。

PDFDocEncodingで未定義の`0x7F`と`0x9F`は、`GetText()`および`PdfDocEncoding.Decode()`ではUnicode replacement character（`U+FFFD`）になります。未定義byteを受け入れたくない場合は`PdfDocEncoding.TryDecode()`を使用できます。現在は読み込み用のデコードのみを提供しており、PDFDocEncodingへのエンコードは未対応です。

### Content Stream Parser

`PdfContentStreamParser`は、展開済みcontent streamを読み、operatorの直前に並ぶ直接オブジェクトを`PdfContentOperation.Operands`としてまとめます。標準operatorだけでなく拡張operatorも名前を保ったまま返します。operatorごとのoperand数・型の検証、graphics stateの更新、テキスト抽出や描画は行いません。

```csharp
var decoder = new PdfStreamDecoder(objectStore);
var operations = new PdfContentStreamParser(contentStream, decoder).Parse();

foreach (var operation in operations)
{
    Console.WriteLine($"{operation.Operator}: {operation.Operands.Count} operands");
}
```

ArrayやDictionaryを含む直接オブジェクト、コメント、PDFの空白文字に対応します。複合operand内に現れる間接参照と、operatorを伴わず末尾に残ったoperandは`PdfContentStreamException`になります。content stream直下の`1 0 R`は、文法どおり2つの数値operandとoperator `R`として扱います。

inline imageの`BI ... ID ... EI`は、画像データ中に現れる`EI`との境界判定が必要なため未対応です。`BI`、または単独の`ID`・`EI`を検出した場合は、別のoperatorとして誤って扱わず`PdfContentStreamException`で明示的に失敗します。

## StreamとFilterの対応

`PdfStream`はファイル中の圧縮済みデータを保持します。展開は`PdfStreamDecoder`を明示的に呼び出したときだけ行われ、元の圧縮済みデータは変更されません。

### Filter

| Filter | 状況 | 備考 |
| --- | :---: | --- |
| `FlateDecode` | ✅ | zlib形式のDEFLATEを展開。TIFF Predictor 2とPNG Predictor 10～15に対応 |
| `ASCIIHexDecode` | ✅ | PDF空白を無視。`>`またはStream末尾で終了し、奇数nibbleは下位を0で補完 |
| `ASCII85Decode` | ✅ | PDF形式の`~>`終端、`z`短縮、最終partial groupに対応。`<~`prefixは使用しない |
| `LZWDecode` | ✅ | 9～12 bit code、clear/EOD code、`EarlyChange` 0/1、TIFF/PNG Predictorに対応 |
| `RunLengthDecode` | ✅ | literal/repeated runとEOD markerに対応 |
| `CCITTFaxDecode` | ❌ | 未対応 |
| `JBIG2Decode` | ❌ | 未対応 |
| `DCTDecode` | ❌ | 未対応 |
| `JPXDecode` | ❌ | 未対応 |
| `Crypt` | ❌ | 未対応 |

`/Filter`は単一NameとArrayの両方に対応し、Arrayの場合は宣言順にFilterを適用します。上表の未対応Filterを含む場合は`PdfFilterException`になります。短縮名の`/Fl`、`/LZW`、`/AHx`、`/A85`、`/RL`はinline image用であるため、通常のStream Filter名としては受け付けません。

`/Filter`と`/DecodeParms`の間接参照は、`PdfObjectStore`を渡して`PdfStreamDecoder`を作成した場合に解決できます。`/DecodeParms`はDictionary、またはFilter Arrayと同数のDictionary/null Arrayを受け付けます。

### FlateDecode・LZWDecode Predictor

| `/Predictor` | 方式 | 状況 |
| ---: | --- | :---: |
| 省略または`1` | Predictorなし | ✅ |
| `2` | TIFF Predictor | ✅ |
| `10`～`14` | 固定PNG Predictor（None、Sub、Up、Average、Paeth） | ✅ |
| `15` | 行ごとに選択するPNG Predictor | ✅ |

`FlateDecode`と`LZWDecode`の両方で、`Colors`（既定値1）、`BitsPerComponent`（既定値8）、`Columns`（既定値1）を解釈します。`BitsPerComponent`は1、2、4、8、16に対応します。行の途中で終わるデータ、不正なPNG filter byte、範囲外または未対応のparameterは`PdfFilterException`になります。展開上限はPNGの行先頭filter byteを除いた最終データ長に適用しつつ、中間データにも算出した上限を適用します。

`LZWDecode`の`EarlyChange`は既定値1で、0も指定できます。0または1以外、EOD codeの欠落、未定義code、切り詰められたbit列は`PdfFilterException`になります。

## 暗号化PDF

`PdfEncryptionDetector`はtrailerの`/Encrypt`を検査し、認証を行わずにSecurity Handler、`/V`、`/R`、鍵長を返します。`PdfStandardSecurityHandler.Authenticate()`へ同じdocument data、xref table、passwordを渡すと、認証済み`PdfDecryptionContext`を作成できます。このcontextを`PdfObjectStoreOptions.DecryptionContext`へ設定すると、読み込まれるStringとStreamがobject番号・generation番号に対応する鍵で復号されます。

```csharp
var crossReferences = new PdfCrossReferenceParser(documentData).Parse();
var encryption = PdfEncryptionDetector.Inspect(documentData, crossReferences);

PdfDecryptionContext? decryption = encryption.IsEncrypted
    ? PdfStandardSecurityHandler.Authenticate(documentData, crossReferences, password)
    : null;

var objectStore = new PdfObjectStore(
    documentData,
    crossReferences,
    new PdfObjectStoreOptions { DecryptionContext = decryption });
```

Standard Security HandlerのRevision 2～4に対応し、Revision 2の40-bit RC4、Revision 3の40～128-bit RC4、Revision 4のCrypt FilterによるRC4またはAES-128（`V2`、`AESV2`、`Identity`）を扱います。user passwordとowner passwordを判別し、permission値は`PdfDecryptionContext.Permissions`から参照できます。permissionの強制、変更、暗号化して保存する機能はありません。

StreamはFilter展開前に復号され、object streamは外側のStreamだけを復号します。暗号化辞書とxref streamは復号対象外です。`EncryptMetadata false`のMetadata Streamにも対応します。

未対応範囲はRevision 5/6のAES-256、公開鍵Security Handler、明示的なStream `/Crypt` Filter、embedded-file専用Crypt Filter、暗号化PDFの書き出しです。Revision 2～4のpasswordはPDFDocEncodingへ変換し32 bytesへ切り詰めます。変換できない文字を含むpasswordは`PdfEncryptionException`になります。

## 現在の主要な安全制限

信頼できないPDFによる過剰なメモリ消費や無限再帰を避けるため、各ParserとReaderには設定可能な上限があります。主な既定値は次のとおりです。

| 設定 | 既定値 | 対象 |
| --- | ---: | --- |
| `MaximumSourceLength` | 256 MiB | `PdfDocumentReader`が受け付けるsourceと非seekable Streamの一時ファイル |
| `MaximumIndirectObjectLength` | 256 MiB | ByteSourceから一度に読み込む間接object範囲 |
| `MaximumEncodedStreamLength` | 256 MiB | 読み込む圧縮済みStream |
| `MaximumDecodedStreamLength` | 256 MiB | Filter展開後のStream。展開処理中にも検査 |
| `MaximumFilterCount` | 16 | 1つのStreamへ適用するFilter数 |
| `MaximumReferenceDepth` | 100 | 間接参照チェーン |
| `MaximumCachedObjects` | 100,000 | ObjectStoreのキャッシュ |
| `MaximumObjectsPerObjectStream` | 1,000,000 | 1つのobject streamに格納できるobject数 |
| `MaximumCachedObjectStreams` | 1,024 | ObjectStoreが保持する展開済みobject stream数 |
| `MaximumCachedObjectStreamBytes` | 256 MiB | ObjectStoreが保持する展開済みobject streamの合計サイズ |
| `MaximumPageTreeDepth` | 100 | Page Treeの深さ |
| `MaximumPageTreeNodes` | 1,000,000 | Page Treeの要素数 |
| `MaximumPages` | 1,000,000 | ページ数 |
| `MaximumOperations` | 1,000,000 | 1つのContent Streamの命令数 |
| `MaximumOperandsPerOperation` | 100,000 | 1つのContent Stream命令のoperand数 |
| `MaximumOperatorLength` | 127 bytes | Content Streamのoperator名 |

これらは対応可能な最大PDF仕様を表す値ではなく、現在の既定の防御上限です。入力元や用途に応じてOptionsから変更できます。

## 未対応機能に遭遇した場合

未対応形式を別形式として推測して読み進めることはせず、原則として次の例外で明示的に失敗します。

- PDF構文の不正: `PdfParseException`
- xref/trailerの不正: `PdfCrossReferenceException`
- 間接参照の解決失敗: `PdfObjectResolutionException`
- Catalog/Page Treeの構造不正: `PdfDocumentStructureException`
- Stream Filter・Predictorの未対応または展開失敗: `PdfFilterException`
- Content Streamの構文不正または未対応inline image: `PdfContentStreamException`
- 暗号化設定、password認証、復号の失敗: `PdfEncryptionException`

機能を追加・変更した際は、このREADMEの対応表と制限事項も同じコミットで更新します。
