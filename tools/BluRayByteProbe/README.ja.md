# Blu-ray のバイトおよびビット周期密度の分析

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

単層 BD-R のディジタル符号化チェーンを分析し、実ディスクの明暗マッピング試験に使う入力パターンを選別します。結果は黒白の極性を定義せず、ディスク全体のイメージの生成や書き込みも行いません。詳細な結論は[分析レポート](../../docs/BLURAY_BYTE_DENSITY.ja.md)を参照してください。

.NET 10 SDK が必要です。独立した規格テーブルの分析には Python 標準ライブラリだけを使い、グラフ作成には matplotlib / numpy も必要です。リポジトリのルートから実行します。

C# プローブのヘルプ、進行状況、エラー、説明用 JSON テキストは 5 言語に対応します。`--language zh-CN|zh-TW|en|ja|tr|system` で明示的に選択できます。未指定の場合は環境変数 `DISC_IMAGE_STUDIO_LANGUAGE` が優先され、なければシステム UI 言語に従います。JSON/CSV の識別子、数値結果、ビット列、ファイル名は翻訳しません。

Python の参照探索と描画ツールも、同じ `--language` オプションと環境設定に対応しています。ヘルプ、エラー、進捗、図の注記、生成する Markdown、JSON/CSV の説明文を翻訳し、フィールド名、列名、パターン識別子、数値データは保持します。図では選択言語の利用可能なシステムフォントを優先し、リポジトリ付属の HarmonyOS フォントを CJK のフォールバックに使用します。

```powershell
dotnet run --project tools/BluRayByteProbe --configuration Release -- --language ja --help
dotnet run --project tools/BluRayByteProbe --configuration Release -- artifacts/bluray-byte-density 4
dotnet run --project tools/BluRayByteProbe --configuration Release -- --fine artifacts/bluray-byte-density

python tools/BluRayByteProbe/reference/analyze.py --output-dir artifacts/bluray-byte-density/reference
python tools/BluRayByteProbe/reference/pattern_search.py --output-dir artifacts/bluray-byte-density/reference
python tools/BluRayByteProbe/reference/robust_pattern_search.py --output-dir artifacts/bluray-byte-density/reference

dotnet run --project tools/BluRayByteProbe --configuration Release -- --verify-reference artifacts/bluray-byte-density/reference/independent-finite-vectors.json
dotnet run --project tools/BluRayByteProbe --configuration Release -- --verify-patterns artifacts/bluray-byte-density/reference/independent-pattern-vectors.json
python tools/BluRayByteProbe/plot_results.py artifacts/bluray-byte-density
```

既定のコマンドは、反復 Q バイト全 256 種を調べます。素の 17PP、完全なクラスターでの 4 種の DC 方式、候補の 4 個の PSN、スクランブルを相殺しないホスト対照、ランダムな隣接値、混合パターンを含みます。第 2 引数は並列度（1–16）です。`--fine` は、5 種の周期の全位相と 4 種の配置を走査し、異なるアドレスで結果を再検証します。このモードの並列度は最大 4 です。

Q は**スクランブル後の目標ユーザーデータ**です。実際に書き込むホストペイロードは `Q XOR PSN mask` です。すべての出力は指定ディレクトリに保存され、同名の既存分析ファイルは上書きされます。

主な出力：

| ファイル | 内容 |
|---|---|
| `all-bytes.csv` / `.json` / `.md` | 全 256 バイト、密度、DC 感度、ランの分布 |
| `all-bytes-density.png` | 16 × 16 密度マップ |
| `fine-patterns.json` / `.csv` | ビット列／配置／位相の組み合わせ 176 個とアドレス間の再検証 |
| `fine-pattern-comparison.png` | バイト基準とビット単位制御の比較 |
| `recommended-patterns.csv` | 推奨する 3 種のビット列の完全な構成パラメーターと密度 |
| `context.json` / `periodic-context.json` | ECC を除外した合成フレーム、隣接値、DC 境界の試験 |
| `reference/` | 独立した規格テーブル、正確なサイクル、探索結果 |
| `method.json` / `validation.json` / `fine-validation.json` | 統計の定義と検証記録 |

完全なクラスターの統計の分母は、同期、EDC、LDC/BIS、DC、17PP を含む `496 × 1932 = 958272` チャネルビットに固定します。run-in/out/Guard3 は含みませんが、run-in からの初期レベルと DSV は保持します。自動 DC には、将来のビットを一時的にゼロとする、プロジェクト既存の貪欲な**診断**アルゴリズムを使用します。書き込みドライブの DC 決定と一致する保証はありません。全 0、全 1、ランダム DC は感度比較のみを目的とし、実際の書き込み設定として扱うことはできません。

`FinePatternAnalysis` は、制御可能な各 payload bit の物理 ECC 位置を計算し、周期的なビット値を設定します。実際の符号化後の 65,536 個の Q バイトが目標とバイト単位で一致することを検証します。EDC、BIS、パリティ、DC はすべて符号化チェーンで生成され、直接書き込めるユーザービットとは仮定していません。
