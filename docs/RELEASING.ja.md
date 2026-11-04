# リリースガイド

[简体中文](RELEASING.md) | [繁體中文](RELEASING.zh-TW.md) | [English](RELEASING.en.md) | [日本語](RELEASING.ja.md) | [Türkçe](RELEASING.tr.md)

四つの PowerShell パッケージ作成・インストールスクリプトは `-Language zh-CN|zh-TW|en|ja|tr|system` に対応します。未指定なら `DISC_IMAGE_STUDIO_LANGUAGE` またはシステム UI 言語を使い、明示指定を優先します。状態とプロジェクト独自の診断は共有翻訳を使い、バージョン、識別情報、パス、リリースノート本文、チェックサムは変更しません。外部ツールや Windows が返す詳細は原文を保持します。選んだ言語は子チェックと昇格後のインストーラーにも渡します。

次のチェックはメタデータと事前検査の失敗経路だけを検証し、パッケージのビルド、署名、インストール、権限昇格の要求を行いません。Windows PowerShell ではコマンド内の pwsh を powershell に置き換えられます。

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

## ブランチとバージョンの方針

日常開発では `main` と短期の機能ブランチを使い、Pull Request でマージします。修正版も `main` からリリースし、現在は長期のリリースブランチを維持しません。

バージョンはリポジトリ直下の `Directory.Build.props` で一元管理します。保守担当者は変更内容に応じて次のバージョンを選びます。

| 変更 | バージョン変更 | 例 |
| --- | --- | --- |
| 後方互換の問題修正 | patch | `1.0.26` → `1.0.27` |
| 後方互換の新機能 | minor | `1.0.26` → `1.1.0` |
| 互換性を失うインターフェース、コマンド、ファイル形式の変更 | major | `1.0.26` → `2.0.0` |

安定版は `VersionPrefix` に `A.B.C` を入れ、`VersionSuffix` を空にします。プレリリースは `VersionSuffix` に `alpha.N`、`beta.N`、`rc.N` を入れ、`N` は先頭ゼロのない正整数です。例えば `VersionPrefix=1.0.27` と `VersionSuffix=rc.1` は `1.0.27-rc.1` になります。サフィックスを `VersionPrefix` に含めてはいけません。候補版を安定版にする際は `VersionSuffix` を空にします。

GitHub Actions は設定の完全なバージョンを読み、対応する `vA.B.C` や `vA.B.C-rc.N` などのタグを自動作成します。公開 EXE の情報バージョンはプレリリースサフィックスを保持し、ファイルとアセンブリのバージョンは四つの数値 `A.B.C.0` を使います。.NET アセンブリ互換性のため、リポジトリの各数値は `0..65534` に制限します。Microsoft Store 提出では先頭要素が `0` より大きいことも必要で、MSIX は `A.B.C.0` です。

## GitHub Actions での Windows アプリとクロスプラットフォーム CLI のリリース

### リリースの準備

1. GPL-3.0-or-later ライセンス、プライバシーポリシー、サポート連絡先が今回の配布要件を満たすことを確認します。ビルドしたコミットへのタグを残し、同じ Release でその版の完全な対応ソースとビルド手順を提供して、EXE/MSIX とともに入手可能にします。GitHub の自動ソースアーカイブにビルド必須ファイルが含まれるか確認してください。依存関係や自己完結型 .NET ランタイムの更新時は[第三者通知](../THIRD-PARTY-NOTICES.txt)も更新します。プロジェクトライセンス、第三者通知、フォント契約はアプリに埋め込み、バージョン情報でオフライン閲覧できます。ライセンスの範囲は [LICENSE-NOTICE.md](../LICENSE-NOTICE.ja.md) を参照してください。
2. 機能ブランチで `Directory.Build.props` の `VersionPrefix`（プレリリースは `VersionSuffix` も）を変更し、`CHANGELOG.md` の `Unreleased` から今回の内容を対応するバージョン節へ整理します。以後の開発用に `## Unreleased` を残します。
3. バージョン節の見出しは `## [1.0.27] - YYYY-MM-DD` または `## [1.0.27-rc.1] - YYYY-MM-DD` 形式とし、日付を実際の公開日に置き換え、本文を空にしないでください。プレリリースと安定版それぞれにタグと完全一致する節が必要です。今回以降の新しい節は、以下の規約に従い中国語と英語の両方を記載します。
4. バージョン設定とリリースノートを同じリリース準備 PR に含め、`build` ワークフローの合格後に `main` にマージします。その push が両ファイルを変更し、設定の完全なバージョンを実際に変えた場合にのみ自動リリースが始まります。
5. GitHub Actions が検査を完了し、バージョンタグと Release 下書きを自動作成します。保守担当者が確認し、手動で公開します。

例えば将来の修正版では `Directory.Build.props` を次のように設定します。

```xml
<VersionPrefix>1.0.27</VersionPrefix>
<VersionSuffix></VersionSuffix>
```

同時に `CHANGELOG.md` へ実際の日付を入れた空でない `## [1.0.27] - YYYY-MM-DD` 節を追加すると、`main` へのマージ後に `v1.0.27` タグが自動作成されます。候補版は `VersionSuffix` を `rc.1`、変更履歴バージョンを `1.0.27-rc.1` にします。

### 二言語のリリースノート

今後は同じ日付付き `## [VERSION] - DATE` 節内で `### 中文`、`### English` の順に記載します。両リストは同じ変更事実、制限、検証状況を説明します。英語は対応する中国語を完全に訳し、警告を省かず、未検証の機能を主張しないでください。整理後も先頭の `## Unreleased` を次の開発用に残します。

準備時の構成例を以下に示します。バージョン、日付、項目は実際の内容に置き換えてください。

```markdown
## Unreleased

## [1.3.4] - YYYY-MM-DD

### 中文

- 在设置中增加界面语言选择。

### English

- Add interface language selection to Settings.
```

`Get-ReleaseMetadata.ps1` は次の `## ` 見出しかファイル末尾まで節全体を抽出するため、`### 中文` と `### English` の両方が Release 説明に入ります。言語見出しを `## 中文` や `## English` にすると節が早く終了するため使わないでください。現在のメタデータ検査は節の一意性、有効な日付、空でない本文を確認します。両言語の存在と項目の一致は引き続きリリースレビューで確認します。

ワークフローは二言語ノートの後に、中国語と英語のダウンロード・実行説明、SHA-256 確認コマンド、対応ソースとビルド手順を追加します。両言語で Linux/macOS CLI が実験的であることを明示し、Windows EXE の署名状態とオンザフライ書き込みの実験的制限をそれぞれ説明してください。

### 自動検査と下書き

`.github/workflows/release.yml` は `main` の `Directory.Build.props` または `CHANGELOG.md` の変更を監視しますが、その push が**バージョン設定とリリースノートの両方を変更し、完全なバージョンも変えた場合**だけリリース処理へ進みます。通常のコード push は通常ビルド検査だけです。`Unreleased` の追記だけや、版を変えない設定変更では Release を生成しません。

ワークフローは設定の完全なバージョン、形式、対応する空でない日付付き Changelog 節を検証し、公開コミットが `origin/main` に属するか確認して、`build.yml` のビルド・回帰検査を再利用します。すべてに合格してからタグと下書きを作成します。

Windows x64 は引き続き自己完結型の単一 EXE を提供します。パッケージ作成スクリプトは隔離ディレクトリで最終 EXE を動かし、自己テスト、2 セクター RAW/WAV 生成検査、GUI スクリーンショット検査を行います。

Linux/macOS の実験的オフライン CLI は各 OS の x64、Arm64 runner でビルドし、.NET ランタイムを同梱します。各 runner は作成直後の tar.gz を展開し、リポジトリ外で `--help`、DVD 自己テスト、CD 音声・DVD イメージ・Blu-ray イメージ生成を検査します。合格した元の圧縮アーカイブを Windows EXE と一緒に下書きへ添付します。例：

```text
DiscImageStudio-v1.3.6-win-x64.exe
DiscImageStudio-CLI-v1.3.6-linux-x64.tar.gz
DiscImageStudio-CLI-v1.3.6-linux-arm64.tar.gz
DiscImageStudio-CLI-v1.3.6-osx-x64.tar.gz
DiscImageStudio-CLI-v1.3.6-osx-arm64.tar.gz
SHA256SUMS.txt
```

ワークフローは各 OS の SHA-256 を統合・検証し、Release 説明と添付の `SHA256SUMS.txt` に記録します。組み込み `GITHUB_TOKEN` で **GitHub Release 下書き**を作成し、個人アクセストークン（PAT）は不要です。プレリリースタグは prerelease フラグを自動設定し、安定タグは安定版の下書きを作ります。

保守担当者は GitHub の [Releases ページ](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/releases)でバージョン、説明、各 OS の添付物、チェックサムを確認し、ダウンロードして起動を検証してから手動公開します。次で Windows ダウンロードファイルの SHA-256 を計算し、Release 説明と比較できます。

```powershell
Get-FileHash .\DiscImageStudio-v1.3.6-win-x64.exe -Algorithm SHA256
```

### 失敗時の再試行と訂正

失敗した Actions 実行を再実行するか、GitHub Actions で `main` から `release` ワークフローを手動実行できます。手動起動にタグ入力はなく、現在の `main` の設定と履歴を読み、全リリース検査を行います。再試行だけのために版ファイルを変更する必要はありません。

同じ版を再試行する場合、既存タグは今回ビルドする同じコミットを指し、既存 Release は下書きでなければなりません。公開済み Release、タグ、成果物は上書きや移動を禁止します。問題は `main` で直して新しい版を出し、プレリリースの反復なら `rc.1` → `rc.2` のように番号を増やします。`main` が進み、同版タグが古いコミットを指す場合は、元の失敗実行を再実行するか、新コミット用の新版を準備してください。説明だけの変更でも事実と訂正記録を残し、公開済みバイナリを置き換えてはいけません。

管理者には、GitHub で `main` の保護を設定して Pull Request と必須 `build` チェックを要求し、`v*` タグ規則でリリースワークフローのタグ作成を許可しつつ更新・削除を禁止することを勧めます。ワークフローファイル自体はリモート保護設定ではなく、管理者が別途設定する必要があります。

## ローカル EXE リハーサル

リポジトリ直下で GitHub Actions と同じパッケージ作成スクリプトを実行します。

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-Release.ps1
```

スクリプトは `Directory.Build.props` のバージョンを自動使用します。例えば `1.0.26` なら出力先は `artifacts/release/v1.0.26/` です。自己完結型 `win-x64` EXE をビルドして個別検証し、`DiscImageStudio-v1.0.26-win-x64.exe` とその `SHA256SUMS.txt` を生成します。正式リリースでは四つの CLI アーカイブのチェックサムを一覧に追加します。

## Microsoft Store MSIX

MSIX は独立した手動配布経路です。最初に Partner Center で製品名を予約し、Product identity から次をコピーします。

- Package/Identity/Name
- Package/Identity/Publisher
- Package/Properties/PublisherDisplayName

ローカルで実行：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-MSIX.ps1 `
  -IdentityName "Partner Center Identity Name" `
  -Publisher "CN=Partner Center Publisher" `
  -PublisherDisplayName "Publisher display name" `
  -DisplayName "Reserved product name"
```

`-Version` 未指定なら `Directory.Build.props` の安定版を `A.B.C.0` に変換します。Store 版を明示するには `-Version "1.0.26.0"` を使います。この上書きは同梱 EXE の版にも反映し、マニフェストとの不一致を防ぎます。Microsoft Store 経路は `-rc.1` などのプレリリースサフィックスを受け付けないため、Store パッケージは安定版設定で準備してください。

GitHub Actions の `package-msix` ワークフローを手動起動して同じ値を指定することもできます。空の `version` もリポジトリ設定を使います。先に `build.yml` の CI を再利用し、その後 MSIX を Actions 添付物として生成します。Store 提出や GitHub Release 添付は自動で行いません。Store MSIX は未署名でもよいですが、ローカルのサイドロード試験には Publisher と一致する証明書での署名が必要です。

## ローカル署名済みテストパッケージ

ローカル試験用に、サブジェクトがテスト Publisher と完全一致するコード署名証明書を作成し、公開鍵証明書を次へ置きます。

```text
artifacts/certificates/DiscImageStudio-Test.cer
```

`Build-MSIX.ps1 -CertificateThumbprint <thumbprint>` を上記の識別パラメーターとともに実行し、署名済みパッケージを生成します。次に `packaging/Install-TestPackage.cmd` をダブルクリックするか、次を実行します。

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Install-TestPackage.ps1
```

インストーラーは一度管理者権限を要求し、テスト証明書の公開鍵を「ローカルコンピューター／信頼されたユーザー」に取り込み、署名済み MSIX をインストールします。秘密鍵や PFX を GitHub にコミットしたり、ローカルの自己署名テスト証明書を正式 Release に添付したりしないでください。

## Windows 11 未署名の簡易テスト（任意）

未署名テストパッケージは独立した OID 発行者名前空間を使い、Store の正式識別情報と混用しないでください。ローカルテスト用パッケージを生成します。

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-MSIX.ps1 `
  -IdentityName "12345.DiscImageStudioArchitectureTest" `
  -Publisher "CN=Disc Image Studio Test" `
  -PublisherDisplayName "Disc Image Studio Test" `
  -DisplayName "Disc Image Studio Test" `
  -Version "1.0.26.0" `
  -UnsignedDevelopmentPackage
```

次に管理者 PowerShell でインストールします。

```powershell
Add-AppxPackage -Path ".\artifacts\msix\DiscImageStudio_1.0.26.0_x64_unsigned-dev.msix" -AllowUnsigned
```

`-UnsignedDevelopmentPackage` は対応する Windows 11 の簡易テスト専用です。Windows の版によっては `-AllowUnsigned` でも実行可能パッケージを拒否するため、その場合は上記の署名テストを使ってください。配布や Microsoft Store 提出にはこのオプションを使わず、正式パッケージでは実際の Partner Center 識別情報を使用します。

正式提出前：

- 実際の識別情報で再作成し、テスト Identity パッケージは提出しない。
- クリーンな Windows 装置でインストール、削除、オフライン動作を確認する。
- 最終パッケージで Windows App Certification Kit を実行する。
- 1366×768 以上の実際のデスクトップ画面を少なくとも一枚提供する。
- `PRIVACY.md` を公開 HTTPS ページへ掲載し、実際のサポート連絡先を記載する。
- 認証の注記で、大きなファイルを生成し、ログイン不要でネット接続を行わず、Windows IMAPI2 による実験的 CD/DVD 直接書き込みを含むことを説明する。適用装置、空ディスク要件、現在の検証範囲を正確に列挙する。
