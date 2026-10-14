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
| PDF 1.5～1.7 | ⚠️ 部分対応 | 従来形式のxref tableと非圧縮の間接オブジェクトを使用するファイルに限定 |
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
| 間接オブジェクト | ✅ | object番号・generation番号の検証、キャッシュ、参照チェーンと循環の検出 |
| xref table・trailer | ⚠️ | 従来形式のxref tableを解析。最終xrefの`/Prev`値は保持するが、incremental updateのxrefチェーン統合は未対応 |
| xref stream | ❌ | 未対応 |
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
