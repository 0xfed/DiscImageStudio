# Disc Image Studio への貢献

[简体中文](CONTRIBUTING.md) | [繁體中文](CONTRIBUTING.zh-TW.md) | [English](CONTRIBUTING.en.md) | [日本語](CONTRIBUTING.ja.md) | [Türkçe](CONTRIBUTING.tr.md)

Disc Image Studio へのご参加を歓迎します。まず本書をお読みください。問題の報告、開発環境の構築、モジュール境界のルール、検証要件、貢献の手順を説明します。

## 問題の報告

通常の問題は GitHub Issues に投稿し、次の情報を記載してください。

- アプリケーションのバージョンと入手方法（ソースからのビルド / MSIX パッケージ）、および Windows のバージョン。
- 確実に再現できる手順、期待する動作、実際の動作。
- 関連する実行ログまたはスクリーンショット。

書き込みに関する問題には、書き込みドライブの機種、ファームウェアのバージョン、ディスクのブランドと型番も記載してください。見た目の結果と実測キャリブレーションはこれらのハードウェアに強く依存しており、情報がなければ通常は原因を特定できません。

悪用可能なセキュリティ上の問題を公開 Issue で開示**しないでください**。[セキュリティポリシー](SECURITY.ja.md)に従い、GitHub の非公開報告機能を使用してください。

機能の提案にも Issues を使用してください。解決したい問題、期待する動作、利用可能な回避策の有無を説明してください。

## 開発環境

- Windows 10 2004 以降、または Windows 11。
- .NET 10 SDK。リポジトリは `global.json` で `10.0.403` に固定しており、インストール後は `dotnet` が一致する SDK を自動選択します。
- テストに第三者フレームワークへの依存はありません。すべてリポジトリに含まれるコンソール回帰プログラムです。

```powershell
git clone <your-fork-url>
cd DiscImageStudio
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DiscImageStudio.App --configuration Release
```

## 依存関係とビルド入力のレビュー

ビルドでは SDK `10.0.403` とランタイム `10.0.14` を使用し、既定でロック済みの復元を適用します。レビュー済みの NuGet 依存グラフは `eng/dependencies/{portable|windows}/{optionalRID}/{ProjectName}.lock.json` に保存します。`VersionPrefix`、プロジェクト参照、パッケージのバージョン、SDK／ランタイムを変更したら、同じ変更内でロックファイルを再生成し、レビューしてください。

CI は `.python-version` に従って CPython `3.14.10` をセットアップした後、`python packaging/Install-DotNetSdk.py` を実行して固定された公式 SDK アーカイブをダウンロードし、展開前に `eng/dependencies/dotnet-sdk.json` のレビュー済み SHA512 値と照合して、新しいディレクトリにインストールします。SDK 更新時は 5 プラットフォームの URL とハッシュを `global.json`、依存関係ロックとまとめてレビューします。既存ディレクトリが同じバージョンを報告するだけでは再利用しません。

Python `3.14.10` を使用して、五つの CI プラットフォームを対象とする wheel のみの完全な Python ロックファイルを更新します。NuGet 更新スクリプトでは、サポート対象のすべてのターゲットと RID の依存グラフを更新します。リポジトリのルートで実行してください。

```powershell
python packaging/Update-NuGetLocks.py
python packaging/Update-NuGetLocks.py --check
python packaging/Update-PythonLock.py
python packaging/Update-PythonLock.py --check
```

`NuGet.Config` は公式 NuGet フィードと承認済みのリポジトリ署名証明書のみを許可します。パッケージソースや証明書のローテーションは明示的にレビューし、既存のロック済みパッケージの検証に必要な証明書を残してください。CI はコミット済みのロックファイルを検査し、自動的に書き換えてはいけません。

## モジュール境界

コードを変更する前に[アーキテクチャの説明](docs/ARCHITECTURE.ja.md)をお読みください。中心となる設計上の選択は、シェルを特定の光ディスク符号化に依存させず、メディア固有のロジックを対応するモジュールに置くことです。レビューでは次のルールを重点的に確認します。

1. CD/DVD/将来の Blu-ray に関する物理変調、誤り訂正、セクター構成、ファイルシステムの詳細を `DiscImageStudio.Core` や共有層に入れないでください。
2. モジュールは GUI コントロールから状態を読み取ってはいけません。入力はすべて `DiscJobRequest` から受け取り、モジュールが直接ウィンドウを表示してはいけません。
3. 下位のアルゴリズムを安全にキャンセルできない場合、`SupportsCancellation` は `false` にしてください。
4. モジュール ID とコマンド名は全体で一意でなければなりません。カタログは起動時に競合を拒否します。
5. 新しいコマンドは `DiscCommandDescriptor` に登録し、カタログのルーティングテストと最小限の出力スモークテストを追加してください。

## コーディング規約

- 周囲のコードの命名、構造、コメント密度に合わせてください。ビルドの警告とエラーはゼロに保ってください。
- ビルドを通すために機能を削除または縮小しないでください。独断で互換性シムや重複実装を追加しないでください。
- 意図的に存在していると思われる機能やコードを削除する前に、Issue または PR の説明で目的を述べてください。
- ビルド成果物、バイナリリソース、秘密鍵 / PFX ファイルをコミットしないでください。

## テストと検証

Pull Request の前に、次のすべてを通過させてください。

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
dotnet run --project tests/DiscImageStudio.LocalizationTests --configuration Release
dotnet run --project tests/DiscImageStudio.ImageIOTests --configuration Release
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release
```

変更内容に応じた検証を追加し、説明に結果を記載してください。

- 画像レイアウト、ストリーミング書き込み、ディスクプリセットの解析を変更する場合、[VALIDATION.md](docs/VALIDATION.ja.md) の該当する回帰項目と照合して結果を確認してください。
- GUI を変更する場合、オフスクリーンでレンダリングしたスクリーンショットで主要ページを確認し、必要に応じて `docs/images/` を更新してください。
- 遅延インターリーブ、EFMPlus、ECC などの符号化チェーンを変更した後に `selftest` の対象項目が変わらない場合も、その理由を説明してください。

## コミットと Pull Request

1. Fork から、または `main` から機能ブランチを作成してください。各ブランチは 1 つの課題に絞り、差分をレビュー可能な状態に保ってください。
2. コミットの件名と本文は英語で記述してください。件名は簡潔に変更内容を説明し、必要に応じて本文に動作の変化、検証方法、大きなファイルへの影響を記載してください。各リリースの Release notes には中国語と英語の 2 節を用意してください。
3. PR の説明には動機、変更範囲、検証結果を明記してください。ユーザーに見える動作の変更では、`CHANGELOG.md` の Unreleased 節と関連する `docs/` 文書も更新してください。
4. 投稿後、GitHub Actions の `build` ワークフローが成功していることを確認してください。

大きな変更（新モジュール、新 UI ページ、書き込み層の契約変更）では、実装の前に Issue または Draft PR を開いて方針を議論することを推奨します。

## バージョンとリリース

`main` と短期の機能ブランチを使用し、修正、機能追加、互換性のない変更に応じてセマンティックバージョンを上げます。通常の貢献は `CHANGELOG.md` の `Unreleased` に記録してください。メンテナーは同じリリース準備 PR で `Directory.Build.props` のバージョン設定と対応するバージョン履歴を更新し、`main` へのマージ後に GitHub Actions がバージョンタグと Release の下書きを自動生成します。通常のコードの push では Release は作成されません。完全なトリガー条件、プレリリース接尾辞、下書きのレビュー、再試行ルールは[リリースガイド](docs/RELEASING.ja.md)を参照してください。

## ライセンス

本プロジェクトは [GNU GPL v3.0 or later](LICENSE)（`GPL-3.0-or-later`）を採用しています。貢献の提出は、該当する内容を提出する権利があり、そのライセンスに従って貢献を許諾することへの同意を意味します。第三者のコード、フォント、画像を導入する場合、元の作者とライセンスを保持し、プロジェクトの配布方法との互換性を確認して、[第三者に関する通知](THIRD-PARTY-NOTICES.txt)を更新してください。第三者のライセンスの境界については [LICENSE-NOTICE.md](LICENSE-NOTICE.ja.md) を参照してください。
