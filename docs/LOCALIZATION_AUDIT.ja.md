# 国際化監査

[简体中文](LOCALIZATION_AUDIT.zh-CN.md) | [繁體中文](LOCALIZATION_AUDIT.zh-TW.md) | [English](LOCALIZATION_AUDIT.md) | [日本語](LOCALIZATION_AUDIT.ja.md) | [Türkçe](LOCALIZATION_AUDIT.tr.md)

更新日 2026-11-04。本監査はリポジトリ所有の UI、CLI、診断、文書を対象とし、簡体字、繁体字、英語、日本語、トルコ語を扱います。特定した翻訳漏れは補い、Windows CI で実行時の多言語対応と配置マトリクスを検証しました。リソースの存在と構造検査だけで意味の正確さは証明できません。

## 対応範囲と確認

| 対象 | 証拠 |
| --- | --- |
| 共有リソース | 2053 項目すべてに 5 言語の非空テキストがあり、キー/フィールドの重複なし。埋め込みとソースが一致 |
| ソース参照 | プリセット、段階、プレビュー名、キャリブレーション案内、ファイルフィルターを含む 2077 個の静的/動的 C#/XAML 参照が解決 |
| UI の意味 | 既存 749 項目を 5 言語で確認：Common 154、Workflows 239、Workspace 151、Xaml 205 |
| CLI と診断 | デスクトップ/移植可能 CLI、独立 DVD エンジン、Core、CD/DVD/BD、ImageIO、Imaging、ISO/UDF、書き込みの本プロジェクトのヘルプ、エラー、進捗、結果が共通リソースを使用 |
| ツール | C# 3、Python 6、PowerShell パッケージ/インストール 4 スクリプトが 5 言語と選択に対応 |
| 文書 | README、全変更履歴、技術ガイド、方針、本監査を含む 23 ソース文書が全て 5 言語対応 |

意味確認で操作動詞、条件、ドライブ採用速度設定、反射輝度、60% を含むカバレッジ、キャリブレーション記録の用語、選択点だけの削除、出力フォルダーの用途、両方向ズームを復元しました。繁体字インターリーブ用語と言語の自称は統一しました。長いラベル/タイトル/キャリブレーションボタンは折り返して伸び、Compact 最小ボタン高を保ち、Windows CI の配置検査に合格しました。

別のソース棚卸しで未ラップの CJK/英語句候補と XAML 表示属性を生成元まで追跡しました。Core のフィット警告は表示時に翻訳し、分類用原文を保ちます。保存済み既定値、テストデータ名、プロトコルフィールド、デバイス詳細を説明文から区別しました。7 つの DVD ストリーミングエラーと空の無効化プリセット文脈の漏れを修正し、全言語の実行回帰を追加しました。ソースガードは DVD アダプター例外リテラルとデスクトップ/ツール XAML 表示属性も検査します。

確認済み UI カタログの指紋は UTF-8 と正規化改行を使います。確認した版の記録であり、意味を自動証明するものではありません。

| カタログ | SHA-256 |
| --- | --- |
| `Common.json` | `7c304b1575bf7d3494c0f73032f45524e4439ce43f8bef24225041fabac971bf` |
| `Workflows.json` | `2a27cd6337ff55c4841271ad851da0b7c4bb6abede365d710714a210d9f32112` |
| `Workspace.json` | `b389bbf7a35a094f1b405e6e475b7af953a0ff045d32bfb096d465fc7063ba07` |
| `Xaml.json` | `f3728ada2da11e6be5614e2733e94e9b2e8ae0d448eedd7b6bb5bfae2250ed6f` |

## 維持する動作

全 CLI 入口はグローバル言語オプションと環境設定を共有します。地域/システムフォールバック、不正/不足/重複オプション、UTF-8、実行中 UI 選択を検証します。UI culture だけを変え、数値カルチャ/解析は保ちます。キャッシュしたモジュール説明も切替に追従します。起動エラー資源は独立読み込みで、主カタログ失敗や CLI エラーラッパーの再帰を防ぎます。

コマンド、オプション名、schema キー、技術 ID、選択値、既定ファイル名、貢献者、ユーザーの名前/説明、パス、保存幾何は不変です。OS/第三者詳細と公式ライセンス原文を維持します。初回プリセットは種別、ID、未編集テキストが一致する場合だけ表示時に翻訳します。スペイン語の未有効化 24 項目は 5 対応言語の対象外です。

符号化判断/バイナリは翻訳に依存せず、書き込み終了は明示フラグを使います。言語間でプロジェクト、CD 音声/データ、DVD EFMPlus/ISO、BD ISO/プローブ、ISO9660/Joliet/UDF バイト、数値分析、生成 C# 表を維持するテストがあり、実ディスク結果は推定しません。

## 文書の完全性ゲート

マニフェストは正本ハッシュと翻訳先を記録します。見出し、段落、リスト、表の寸法/数値、インラインコード、コマンド/式、参照先、ローカルリンクを保ちます。明示例外ではプリセット例の名前/説明と 2 Mermaid 表示ラベルだけを訳せ、JSON 型/精度/ID/貢献者、図のノード/接続/方向/数字は保ちます。欠落/空白ラベル、重複 JSON、無効数値は失敗します。

公開用の中英並列履歴は維持します。指定した並列リストの項目数と技術トークンは一致し、閲覧版は同じ変更を一度だけ示します。独立 120 項目、日付、元の検証限界を全て保ちます。生成報告はソース一覧に入れず、無視対象 artifacts 下で元文書に参照済みの明示 4 出力だけは新規取得時に不在でも許可し、他のローカルリンクは存在が必要です。

完全ゲートは 23/23 で、CI でも必須です。7 規則回帰が制約を保護します。構造検査は言語確認を補完し、ソース変更時は再確認が必要です。保守と Windows 画像コマンドは [LOCALIZATION.md](LOCALIZATION.ja.md) を参照してください。

## 検証と限界

Windows ターゲット有効の Release 全体ビルドは警告/エラーなしです。実際の 5 言語 DVD ストリーム失敗、例外型/引数名、保存データ、失敗時ストリーム解放、数値カルチャを含む共通回帰は合格しました。移植可能 CLI パッケージ、DVD セルフテスト、C# Blu-ray プローブは合格し、256 独立バイトベクトルと有限パターンに一致します。偽レコーダーはハードウェアに触れず、バイト、MMC コマンド/時間制限、進捗、キャンセル、清掃を保ちます。

PowerShell は既存 45 回帰と安全な言語事前検査 69 件に合格し、47 キー、元のメタデータ、Unicode パス、子への言語伝達を検証します。ビルド/署名/導入/昇格はしません。Python は 134 キーと全 6 ツールを元の数値/schema 基準に照合しました。参照探索は周期 8 まで、3 種の図が全 5 言語の字形/境界検査に合格し、代表 5 図を目視しました。意図的に無効なアトラスを含む合成データであり、物理測定や全プローブ走査ではありません。

Windows CI では WPF 言語切替、字形/配置画像、ディザ UI/PNG、DVD アトラス/プレビュー、Windows PowerShell の事前検査に合格しました。パッケージ導入経路は未実行です。実ドライブ性能や光学輝度は確立しません。これらは検証限界であり、新規ハードウェア検証の主張ではありません。

## 検査の再現

```sh
dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release
dotnet run --project tests/DiscImageStudio.Burning.LocalizationTests -c Release
python3 tests/test_cli_package.py /absolute/path/to/disc-image-studio
python3 tests/test_probe_localization.py /absolute/path/to/BluRayByteProbe.dll
python3 -m unittest discover -s tests -p test_python_localization.py
python3 tests/test_python_tool_localization.py
python3 -m unittest discover -s tests -p test_documentation_translation_rules.py
python3 tests/test_documentation_translations.py --require-complete
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

Windows では実行時スイートも実行し、言語別画像を確認してください：

```powershell
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release
```

独立した Windows CI プロセスで 5 言語の 325 配置シナリオを検査します。CD/DVD/BD の 1366×900 暗色、960×700 明色ワークスペース、展開プレビューと読み戻し、キャリブレーションパネル、設定カテゴリ、新規／編集ダイアログが対象です。言語切替後の読み込み済みタイトル、描画された文字の境界と可視クリップ領域、欠落字形を検査し、意図した省略記号と最近使ったプロジェクトのパスのスクロールは許可します。一時ストアと書き込めない偽レコーダーで隔離し、プロジェクトのバイト、幾何、数値カルチャを維持することも検査します。別モニターを模倣せず実際の DPI を記録します。

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

この Linux 環境でもビルドでき、Windows CI では全 325 シナリオが問題ゼロで完了しました。失敗時にも PNG と JSON レポートを保存します。空プロジェクトの合成データであり、物理的な光学結果や全ての入力済みワークフロー状態は検証しません。
