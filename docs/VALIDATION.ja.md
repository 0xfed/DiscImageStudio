# 検証について

[简体中文](VALIDATION.md) | [繁體中文](VALIDATION.zh-TW.md) | [English](VALIDATION.en.md) | [日本語](VALIDATION.ja.md) | [Türkçe](VALIDATION.tr.md)

## 上流機能同期の回帰検証

上流 `62efb5c` の検証に `UsableSectorGenerationTests`、`UsableSectorPersistenceTests`、`StreamBurnCapacityTests`、`PresetApplicationTests`、`CalibrationConfirmedFitTests` を含みます。UDF/VAT 更新、時間測定結果の使用可能上限、目視セレクターの切詰め、新旧ルールの再現も検証します。七言語のレイアウト行列を維持しますが、新しい実機記録検証を意味しません。

このページは現在のソースで再現可能なチェックを示します。自動回帰テストはアルゴリズム、ファイル配置、アプリのワークフローを検証します。実際の書き込み結果は引き続きレコーダー、ファームウェア、ディスクに依存します。

`TimingMeasurementBurnTests` は CD/DVD のランダムデータ測定ディスクの直接書き込みを検証します。データメディアの選択、ISO とストリームの全バイト一致、固定ソース、ドライブ容量と整列、確認、キャンセルを対象とします。レイアウト行列は七言語で測定ディスク書き込み画面を取得します。実機への書き込みは行いません。

読み取り時間の回帰検証ではゾーンサンプルに一時的なジッターを加え、再試行後の形状回復を確認し、継続ノイズと可変回転は引き続き拒否します。失敗情報ではキャッシュ検出、ゾーンアドレス、再試行回数、フィット品質を確認します。ネイティブ読み取りテストは速度要求の結果、コマンドのフォールバック、読み取り専用コマンドの境界を保持します。

## ローカル検証

直近の全アーキテクチャ回帰テストは 2026-10-28 に実施しました。UI 修正、キャリブレーション生成の最適化、Verbatim BD-R (64099) プリセット（コミット `3088802`、`49a0e4d`、`46e2c53`）を対象に、Windows、.NET SDK 10.0.400（ランタイム 10.0.11）、Release 構成で確認しました。ソリューション全体のビルド（警告 0、エラー 0）と全アーキテクチャ回帰テストに合格しました。テストは独自の実行可能ハーネスを直接起動し、`dotnet test` は使いません。

キャリブレーションの書き込み終了後、生成最適化のビルド、全アーキテクチャ回帰テスト、DVD エンジンのセルフテストを完了しました。Verbatim プリセット追加後もビルドと全アーキテクチャ回帰テストに合格し、GUI と CLI の半径、セクター数、極性の一致、およびプリセットのマージとコマンドラインによる上書きを確認しました。v1.3.3 のバージョン／変更履歴検証とリリースツールの 45 項目に合格しました。新しい性能測定や実ディスク書き込みの結論は追加していません。

追加して合格した回帰テストには、BD キャリブレーションの並列切り替え、既定の逐次コールバック、極性と描画範囲、バッチ順序とキャンセル、CD キャリブレーションの 4 点サンプリングの座標ビット一致、インターリーブとバイト順、CD ファイルモードの内外周におけるスカラー／SIMD バイト一致、DVD の解析サンプリングとコードワードサンプリングの座標、インデックス、バイト出力、並列末尾バッチの一致が含まれます。

リポジトリのルートで実行します：

```powershell
dotnet restore DiscImageStudio.slnx
dotnet build DiscImageStudio.slnx --configuration Release --no-restore
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release --no-build
dotnet run --project src/DvdImageSolver --configuration Release --no-build -- selftest
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- selftest
./tests/Test-ReleaseTooling.ps1
```

アーキテクチャ回帰テストの対象：

- CD/DVD：白黒マッピング、ディザリング、sRGB ガンマ、ファイルとストリームの一致、SIMD とスカラーサンプリングの一致。
- CD ファイルモード：ISO9660/Joliet の中国語ファイルの独立読み取り、内外周配置とプロジェクト保存、ファイル領域プレビュー、生セクターのジオメトリ、ファイルと前方ストリーム出力の一致。
- CD データ DAO：Mode 1 EDC と全 P/Q パリティ方程式、主チャネルのバイト順、リードインとプリギャップのアドレス、データトラックの Q 識別子と CRC、前方シークと短い読み取り、内外周 ISO の完全復元。Ritek プリセットの 359,845 セクターを保持し、模擬レコーダーで音声トラックと共通の容量制限を検証します。ネイティブ IMAPI テストはメモリー内イメージのみを構築し、書き込みデバイスを作りません。
- BD：17PP/EDC/Reed-Solomon、3T/6T マッピングと極性、描画範囲外のゼロ化、UDF 2.50、4 GiB を超えるファイル、sidecar の往復、無効な境界の拒否。
- DVD/BD ファイル配置：既定の内周との互換性、外周 ISO9660/Joliet と UDF ディレクトリの独立解析、中国語ファイル内容、ディスク全体の座標、シーク不可ストリームとファイル出力の一致、容量とキャンセルの保護、プロジェクト配置オプションの往復。
- 3 盤種モデル：プロジェクト schema 2 の往復と schema 1 からの更新、プリセット schema 4 と旧 schema のマージ、`ProjectSession` の BD apply/capture。
- 書き込み：操作の排他、進行状況、順次書き込み、タイムアウト、キャンセル、リソース解放。すべて実レコーダーではなく模擬インターフェースを使います。
- ストリーミング書き込み表示：デバイスが確認した書き込み量のみで進捗を進め、CD 音声／データ、DVD、BD の倍速換算、不明または無効なレート、3 秒での失効、ファイナライズと終了時の解放、転送ログの抑制、遅延イベントの保護を確認します。
- キャリブレーション書き込み元：CD/DVD/BD ラベルと保存パラメーターの一致、BD ビット長、極性、描画範囲、盤種間の切り替え、速度問い合わせ、模擬書き込み要求と確認文言。
- キャリブレーション：3 次トラックピッチ、BD の描画範囲と極性、写真補正、曲線フィッティング、セッション保存／復元、ディスクプリセットの同期、パラメーター精度。
- プレビューとウィンドウ：CD/DVD/BD の投影と読み戻し、複数モニター、タスクバーの境界。
- ファイル領域プレビュー：内外周のデザイン投影と sidecar 付き成果物の読み戻しでの黄色強調、DVD 3 次ピッチ境界、純描画と無効な配置情報では強調しないこと、不正ディレクトリ／容量の案内、切り替えとリセット時の消去。黄色のオーバーレイは ISO データを変更しません。

既存のコマンドで GUI のオフスクリーンスクリーンショットを生成できます：

```powershell
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/home.png --tab 0
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/export-bd.png --tab 3 --project sample.discproj
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/preview-bd.png --tab 2 --project sample.discproj --live-input image.png --live-disc bd --live-mode calibrate
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/burn-bd.png --tab 4 --project sample.discproj --burn-source calibration --burn-progress write
```

`--burn-progress` は `write`、`buffer`、`finalize` を受け付け、実際の書き込みを開始せず、模擬進捗で UI を描画します。2026-10-28 に BD キャリブレーションのワイド画面と 800×650 の狭いウィンドウ、準備状態とファイナライズ状態を確認し、下部の書き込み量、レート、倍速、進捗バーはすべて表示されていました。同時に DVD エンジンのセルフテストにも合格しましたが、実ディスクの書き込みは行っていません。

`build` ワークフローでは、モジュールカタログの DVD セルフテスト、2 セクターの CD 出力確認、ホームのスクリーンショットも実施します。公開する単一ファイル EXE の追加チェックは[リリースガイド](RELEASING.ja.md)を参照してください。

同時に DVD と BD それぞれの 2 つのファイル配置のデザイン投影、外周のエクスポートページ、外周成果物の読み戻しを計 8 枚のオフスクリーン画像で確認しました。DVD／アプリのセルフテストとリリースツールの 45 項目にも合格しました。

デザインページのファイル選択入口は、DVD、BD、CD、フォルダー未選択、18 レイヤー、狭いウィンドウ、低いウィンドウの 7 状態でも確認しました。ワイド画面ではレイヤーリストが内部スクロールし、ファイル選択領域に到達でき、ビルドは警告 0、エラー 0 でした。

v1.3.0 は `Build-Release.ps1` による .NET 10 自己完結型の単一ファイル検証にも合格しました。隔離ディレクトリに EXE のみを置き、書き込みインターフェースのビルド情報、DVD セルフテスト、2 セクター RAW/WAV/CUE、CUE 無効化、WPF 描画を確認しました。最終版は .NET / Windows Desktop 10.0.11 を内蔵し、上流のライセンス通知全文も保持します。

v1.3.2 は全アーキテクチャ回帰テスト、DVD エンジンとアプリモジュールのセルフテスト、リリースツールの 45 項目に合格しました。追加回帰テストは、Blu-ray のプリセット切り替え後に「名前を付けて保存」とプロジェクト確認で極性が一致すること、最近のプリセットを削除した後も盤種間選択とジオメトリが同期することを検証します。キャンバスのズームは 25%–1200% の境界、加減ボタン、ウィンドウに合わせるリセットを網羅します。新規 CD、Blu-ray、DVD のプロジェクト設定と 18 レイヤーのデザインページを、追加の 4 枚のオフスクリーン画像で確認しました。

## 翻訳の事前検査

パッケージ翻訳の回帰テストはビルド、署名、インストール、昇格をせず、メタデータ、診断、言語の優先順位、Unicode パス、子チェックへの言語伝達を確認し、数値カルチャを維持します。共有ランタイムではリソース読み込み失敗の五言語説明、例外型、ストリーム破棄も確認します。Windows PowerShell では下記の pwsh を powershell に置き換えられます。実際の Windows インストールとパッケージ作成は Windows 上での検証が必要です。

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

## 実ディスク検証の限界

このファイル配置とプレビューの変更では実ディスクの書き込みを行っていません。自動テストはデバイスの書き込み戦略、実際の反射輝度、書き込み成功率を証明しません。Blu-ray 機能は引き続き実験的です。

v1.3.1 の CD データ DAO も実ディスクで書き込んではいません。全アーキテクチャ回帰テスト、独立した単一ファイル EXE の起動、音声／CUE、内外周 ISO のファイル内容と成果物の読み戻しの検証には合格しています。

記録済みの DVD 3 次トラックピッチの実写とパラメーターは [README の前後比較](../README.ja.md#辅助标定展示)を参照してください。物理モデルの歴史的監査は [PHYSICAL_MODEL_AUDIT.md](PHYSICAL_MODEL_AUDIT.ja.md) にあります。結論には記載の日付とコード基準が適用され、現在のソースの検証に代わるものではありません。

今後の実ディスク記録には、バージョンまたはコミット、レコーダーとファームウェア、ディスク型番、プリセットパラメーター、速度、生成／書き込みの入口、元の写真を含め、特定のデバイスの結果をすべてのディスクへ一般化しないでください。

補助校正には粗校正、境界校正、精密校正の段階があり、描画プロジェクトとは独立して使用できます。ディスク容量を確認し、空のディスクごとに対応する段階のターゲットを作成してください。記録済みディスクの解析には、その生成時の記録と写真を使います。 [上流の統合後の校正手順](CALIBRATION_TUTORIAL.ja.md).
