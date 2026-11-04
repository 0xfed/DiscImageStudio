# 国際化

[简体中文](LOCALIZATION.md) | [繁體中文](LOCALIZATION.zh-TW.md) | [English](LOCALIZATION.en.md) | [日本語](LOCALIZATION.ja.md) | [Türkçe](LOCALIZATION.tr.md)

右上に常に表示される「歯車 + 設定」ボタンをクリックし、「全般 → 言語」で「システムに従う」「简体中文」「繁體中文」「English」「日本語」「Türkçe」から選択します。保存すると直ちに反映されます。ホームとすべてのワークスペースに設定ボタンがあり、Ctrl+, でも開けます。キャンセル、Escape、設定ウィンドウを閉じる操作では言語を保存しません。言語を切り替えてもアプリは再起動せず、プロジェクトを閉じたりタスクを中断したりしません。

`settings.json` の `language` は `system`、`zh-CN`、`zh-TW`、`en`、`ja`、`tr` を使用します。言語とテーマの既定値はともにシステムに従う設定です。言語が未指定、空、未対応の場合もシステムに従い、既存の有効な言語とテーマの選択は維持されます。システムに従う場合は起動時のシステム UI 言語を使います。`zh-TW`、`zh-HK`、`zh-MO`、`zh-Hant`、明示的な `zh-Hant-*` は繁体字中国語、`zh-CN`、`zh-SG`、`zh-Hans` などその他の中国語環境は簡体字中国語、`ja`、`ja-JP` などの日本語環境は日本語、`tr`、`tr-TR` などのトルコ語環境はトルコ語、英語やフランス語などその他の環境は英語になります。実行中にシステム言語を変更した場合は、アプリの再起動が必要です。テーマは Windows のアプリの色モードからダーク／ライトを選びます。言語は `.discproj` と独立しており、プロジェクト名、ユーザープリセット名、レイヤー名、ファイルパス、数値解析規則は翻訳しません。

## 文言の追加と保守

翻訳は `src/DiscImageStudio.Localization/Catalogs/*.json` にあり、ビルド時にアセンブリへ埋め込まれます。追加の実行時依存関係はありません。各キーに `zh-CN`、`zh-TW`、`en`、`ja`、`tr` の 5 言語のテキストを格納します。繁体字中国語には専用リソースを用い、「設定」「專案」「檔案」「燒錄」などの用語を使用します。実行時にユーザーデータを変換しません：

```json
{
  "示例 {0}": { "zh-CN": "示例 {0}", "zh-TW": "範例 {0}", "en": "Example {0}", "ja": "例 {0}", "tr": "Örnek {0}" }
}
```

C# では `L.T("源文案")`、文字列補間では `L.F($"示例 {value}")` を使います。リソースキーは対応する .NET 複合書式 `示例 {0}` です。明示的なリソースキーには `L.Format(key, arguments)` を使えます。翻訳では引数の順序を変更できますが、インデックスと書式指定子を保持してください。XAML は `xmlns:loc="clr-namespace:DiscImageStudio.Localization"` と `{loc:Text ResourceKey}` を使います。`TextExtension` は読み込み時に現在の言語で書き込み、言語切り替え時に読み込み済みのコントロールとテンプレートをその場で更新します。

共有する表現は一度だけ定義し、すべてのリソースファイルを通じてキーを一意にします。現在の言語がない場合は中国語へフォールバックし、キーがない場合はキー自体を表示します。起動時にはウィンドウと ViewModel を作成する前に言語を初期化します。UI culture のみ設定し、numeric culture は維持します。プロトコル識別子、コマンド、パス、ユーザーデータを翻訳せず、翻訳済みテキストをロジックの判断に使わないでください。

目標は、本リポジトリが所有する UI、コマンドラインヘルプ、診断、ドキュメントについて、対応する 5 言語で 1:1 の意味的対応を保つことです。共有ランタイムとリソースは `src/DiscImageStudio.Localization` にあり、デスクトップと CLI で同じものを使います。CLI ヘルプ、モジュールとオプションの説明、CD モジュールのメッセージ、DVD モジュールの状態、コマンドのルーティングエラーは接続済みです。Blu-ray、画像読み込み、Core、CD、ISO/UDF が生成する例外と、Blu-ray の進行概要、説明用 JSON、写真補正の案内も翻訳済みで、機械用識別子、ジオメトリ値、バイナリ出力は維持されます。対応範囲と実施済み／未実施の検証は [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.ja.md) を参照してください。サードパーティーや OS の例外詳細と公式ライセンスの原文は変更しません。

## コマンドラインの言語

コマンドラインはグローバルオプション `--language zh-CN|zh-TW|en|ja|tr|system` に対応し、コマンドの前後に置けます。`--language=ja` も使えます。明示的なオプションは環境変数 `DISC_IMAGE_STUDIO_LANGUAGE` より優先され、どちらもない場合はシステム UI 言語に従います。明示した言語が未対応、オプションが重複、値が欠落している場合は終了コード 1 を返します。このオプションは独立した DVD プログラム、クロスプラットフォーム CLI、Windows アプリのコンソール入口で共通です。コマンドなしで GUI を起動した場合は、保存済み UI 設定を使います。

```sh
disc-image-studio --language ja --help
disc-image-studio cd-generate --language tr --input picture.png --output track.wav --sectors 2
```

翻訳するのは表示テキストのみです。コマンド名、オプション名、プロトコル識別子、ファイルパス、ユーザー入力、JSON フィールド名は翻訳しません。プロジェクトのパラメーターは既存の規則で解析され、言語選択は数値カルチャを変更しません。CLI は名前付き UI カルチャを使えるよう invariant globalization を無効にします。

キャリブレーション曲線フィッティングの点に関する案内は、UI 表示時に翻訳します。Core の戻り値は原文を維持し、自動探索の判断は翻訳に依存しません。プリセット保存時には、自動生成されたジオメトリの説明に移植可能な原文と書式リソースキーを保持します。表示時は現在の言語と数値カルチャで書式を再適用し、手動編集された説明は原文を維持します。

## 検証

WPF に依存しない整合性チェックは Windows、Linux、macOS で実行でき、CI でも各プラットフォームで実行します：

```sh
dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release
```

このチェックは 5 言語の空でないテキスト、キーの重複、複合書式と数字／日付書式、ファイルダイアログのフィルター、C# リテラルと補間呼び出し、XAML 文言とテンプレートトリガー、内蔵プリセット、動的なキャリブレーション案内、ユーザープリセットのフォールバックを検証します。キャッシュされたモジュールの説明が言語切り替えに追従すること、例外の型とパラメーター名、およびプロジェクト、ISO9660/Joliet、UDF 出力の言語間一致も確認します。翻訳の完全な意味的正確性を証明したり、Windows 上の実行時切り替えとレイアウトのスクリーンショット確認を代替したりするものではありません。

文書翻訳の段落構造、リスト、コマンド例、リンク、元ファイルのバージョンは個別に確認できます：

```sh
python3 tests/test_documentation_translations.py
python3 tests/test_documentation_translations.py --require-complete
```

文書チェックでは段落、表の行列、数値も保持し、小数点や桁区切りのローカライズは認めます。最初のコマンドはマニフェストで完了と記録された文書のみ検証し、未完了項目を報告します。2 つ目はリポジトリ全体の文書の完全性を確認し、未完了項目があれば失敗します。現在の結果と残作業は [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.ja.md) を参照してください。

CI はリポジトリ全体の完全性検査を必須とします。

マニフェストでプリセット JSON 例の `displayName` と `description` の翻訳を明示的に許可できます。フィールド名、型、数値の精度、ID、貢献者は引き続き一致する必要があり、重複フィールドや無効な数値は拒否されます。

生成されたテストレポートはソース文書に含めません。マニフェストの `generated_artifacts` で再生成可能な結果リンクを明示できます。ソースで参照済みで、無視対象の artifacts ディレクトリ内にあるパスだけを認め、その他のローカルリンク先は存在する必要があります。

マニフェストの `translated_mermaid_labels` で表示文字を翻訳できる Mermaid ノードを指定できます。ノード ID、接続、方向、その他のラベル、ラベル中の数字は一致する必要があり、欠落や空白のラベルは拒否します。

公開用の変更履歴は中国語と英語の並列小節を保持します。マニフェストの `parallel_language_sections` に両見出しを明示し、各言語の閲覧版では同じ変更を一度だけ表示できます。両小節は完全な箇条書きで項目数とインライン技術識別子が一致する必要があり、欠落小節やリスト外の追加内容は拒否します。歴史的な小見出しとその他の本文は全て保持します。

書き込み層の例外、進捗、操作説明、装置診断も共有リソースを使用します。`OpticalBurnOperation.IsFinalizing` が終了処理の状態を明示的に保持します。翻訳済みテキストの比較でファイナライズを判断してはいけません。次の回帰テストは WPF を必要とせず、実際の書き込みライブラリと模擬装置を使用して、5 言語で CD/BD のバイト列、MMC コマンドとタイムアウト、進捗段階、キャンセルトークン、クリーンアップ動作を確認します。実際のレコーダーへのアクセスや物理的な書き込みは行いません。

```sh
dotnet run --project tests/DiscImageStudio.Burning.LocalizationTests -c Release
```

PowerShell のパッケージ作成・インストールスクリプトは `packaging/Localization.ps1` で共有リソースを読み、ネイティブの `-Language` と環境設定に対応します。選択言語を子チェックと昇格後のインストーラーへ渡します。メタデータ、リリースノート本文、識別情報、パス、チェックサム、数値カルチャは変更しません。次のテストは模擬子プロセスでメタデータと事前検査の失敗経路だけを実行し、パッケージのビルド、署名、インストール、権限昇格の要求を行いません。Windows PowerShell では pwsh を powershell に置き換えられます。

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

C# リソースは言語選択後に遅延読み込みします。空カタログと重複キーの診断は独立した埋め込みエラーカタログを直接読み、主カタログ読み込み中の再帰翻訳を避けます。共有ランタイム回帰テストは五言語の診断、元のリソース名、例外型、ストリーム破棄、数値カルチャを確認します。 CLI エラーの接頭部もこの独立リソースから読み、エントリーポイントのエラー処理で失敗済みの主カタログ読み込みを再試行しません。Python のエラーラッパーも同じ方式です。

Python ツールは `tools/localization.py` を通して同じ JSON リソースを読み取り、グローバル言語フラグと環境設定を共用します。パーサーのヘルプ／エラー、解析説明、図の注記は 5 言語に対応し、数値 locale、フィールド、パス、アルゴリズムは変更しません。標準ライブラリによるチェックは全 Python ツールと argparse の書式引数を監査します。統合チェックには NumPy と matplotlib が必要です。最大周期 8 の参照探索、合成図、意図的に無効なアトラスを使い、元スクリプトの数値／スキーマ基準、入力ファイル、フォント、図のテキスト境界を照合し、生成する C# テーブルコードが言語に依存しないことも検証します。これらのテストデータは実ディスク測定や完全なプローブ走査ではありません。

```sh
python3 -m unittest discover -s tests -p test_python_localization.py
python3 tests/test_python_tool_localization.py
```

`LocalizationTests` はリソースの言語網羅性、書式引数、システム言語へのフォールバック、設定の保存とキャンセル、および保存失敗時に言語が適用されないことを確認します。`RelocalizationTests` は実行中の言語切り替え後に、プロジェクトのジオメトリ、進行中の読み込み、プレビュー結果、開いているページの文言が維持されるか正しく更新されることを確認します。

```powershell
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language en --output artifacts/settings-en.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-CN --output artifacts/settings-zh.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-TW --output artifacts/settings-tw.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language ja --output artifacts/settings-ja.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language tr --output artifacts/settings-tr.png
```

スクリーンショットは使い捨て設定ファイルを使い、ユーザー設定を変更しません。言語を追加する場合は、言語の正規化と解決規則、設定の選択肢、全リソース、テストを一緒に拡張してください。

独立した Windows CI プロセスで 5 言語の 325 配置シナリオを検査します。CD/DVD/BD の 1366×900 暗色、960×700 明色ワークスペース、展開プレビューと読み戻し、キャリブレーションパネル、設定カテゴリ、新規／編集ダイアログが対象です。言語切替後の読み込み済みタイトル、文字の幅／高さ、欠落字形を検査し、意図した省略記号は許可します。一時ストアと書き込めない偽レコーダーで隔離し、プロジェクトのバイト、幾何、数値カルチャを維持することも検査します。別モニターを模倣せず実際の DPI を記録します。

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

この Linux 環境でコンパイル済みですが、Windows 実行は push 後のリポジトリ CI 待ちです。失敗時にも PNG と JSON レポートを保存します。空プロジェクトの合成データであり、物理的な光学結果や全ての入力済みワークフロー状態は検証しません。
