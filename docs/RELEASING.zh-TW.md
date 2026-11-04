# 發行指南

[简体中文](RELEASING.md) | [繁體中文](RELEASING.zh-TW.md) | [English](RELEASING.en.md) | [日本語](RELEASING.ja.md) | [Türkçe](RELEASING.tr.md)

四個 PowerShell 封裝與安裝指令碼支援 `-Language zh-CN|zh-TW|en|ja|tr|system`；未指定時採用 `DISC_IMAGE_STUDIO_LANGUAGE` 或系統介面語言，明確參數優先。狀態與本專案診斷使用共用翻譯資源，版本、身分、路徑、記錄正文與總和檢查碼維持原樣；第三方工具與 Windows 傳回的詳細資訊保留原文。選定語言會傳給子檢查及提升權限後的安裝程序。

以下檢查僅驗證中繼資料與預先檢查失敗路徑，不建置、簽署或安裝套件，也不要求提升權限。Windows PowerShell 可將命令中的 pwsh 改為 powershell：

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

## 分支與版本政策

日常開發使用 `main` 加短期功能分支，透過 Pull Request 合併。修正版本亦從 `main` 發行；目前不維護長期發行分支。

版本統一在儲存庫根目錄的 `Directory.Build.props` 維護。維護者依改動選擇下一版本：

| 改動 | 版本變更 | 範例 |
| --- | --- | --- |
| 向下相容的問題修正 | patch | `1.0.26` → `1.0.27` |
| 向下相容的新功能 | minor | `1.0.26` → `1.1.0` |
| 不相容的介面、命令或檔案格式變更 | major | `1.0.26` → `2.0.0` |

正式版在 `VersionPrefix` 填寫 `A.B.C`，將 `VersionSuffix` 留空；預發行在 `VersionSuffix` 填寫 `alpha.N`、`beta.N` 或 `rc.N`，其中 `N` 為不含前置零的正整數。例如 `VersionPrefix=1.0.27`、`VersionSuffix=rc.1` 對應 `1.0.27-rc.1`，不要將後綴寫入 `VersionPrefix`。候選版轉正式版時清空 `VersionSuffix`。

GitHub Actions 從設定讀取完整版本，自動建立對應的 `vA.B.C`、`vA.B.C-rc.N` 等標籤。發佈 EXE 的資訊版本保留預發行後綴；檔案版本與組件版本使用四段數字 `A.B.C.0`。儲存庫數字段限制為 `0..65534`，以相容 .NET 組件版本；提交 Microsoft Store 時第一段還必須大於 `0`，MSIX 版本為 `A.B.C.0`。

## 透過 GitHub Actions 發行 Windows 應用程式與跨平台 CLI

### 準備發行

1. 確認 GPL-3.0-or-later 授權、隱私權政策與支援聯絡方式符合本次散佈要求。發行時保留指向建置提交的標籤，並在同一 Release 提供該版本完整對應原始碼與建置說明，確保原始碼可與 EXE/MSIX 一同取得；核對 GitHub 自動產生的原始碼封存含建置所需檔案。升級相依項或自包含 .NET 執行階段時，同步更新[第三方聲明](../THIRD-PARTY-NOTICES.txt)。專案授權、第三方聲明與字型協議均嵌入應用程式，可在「關於」頁離線檢視；授權範圍見 [LICENSE-NOTICE.md](../LICENSE-NOTICE.zh-TW.md)。
2. 在功能分支修改 `Directory.Build.props` 的 `VersionPrefix`（預發行同時設定 `VersionSuffix`），並將本次內容從 `CHANGELOG.md` 的 `Unreleased` 整理至對應版本區段。保留 `## Unreleased` 供後續開發。
3. 版本區段標題必須採用 `## [1.0.27] - YYYY-MM-DD` 或 `## [1.0.27-rc.1] - YYYY-MM-DD` 形式，將日期預留位置改為實際發行日期，且內容不可為空。預發行與正式版各須有與標籤完全對應的版本區段；從本次發行起，新增版本區段依下文約定同時提供中文與英文記錄。
4. 將版本設定與發行記錄放入同一個發行準備 PR，確認 `build` 工作流程通過後合併至 `main`。該次 push 的變更範圍必須同時包含這兩個檔案，且設定中的完整版本確實變更，才會啟動自動發行。
5. GitHub Actions 完成檢查、自動建立版本標籤並產生 Release 草稿；維護者檢查後手動公開。

例如未來修正版本在 `Directory.Build.props` 設定：

```xml
<VersionPrefix>1.0.27</VersionPrefix>
<VersionSuffix></VersionSuffix>
```

同時在 `CHANGELOG.md` 新增非空的 `## [1.0.27] - YYYY-MM-DD` 版本區段並填入實際日期，合併至 `main` 後自動產生 `v1.0.27` 標籤。候選版則將 `VersionSuffix` 設為 `rc.1`，記錄版本寫為 `1.0.27-rc.1`。

### 雙語發行記錄

後續發行在同一個含日期的 `## [VERSION] - DATE` 區段內，依序使用 `### 中文` 與 `### English`。兩組條目描述相同的改動事實、限制與驗證情況；英文應完整翻譯對應中文內容，不省略警告，也不增加未驗證的能力宣稱。整理發行內容後保留頂端 `## Unreleased`，供下一輪開發使用。

例如，發行準備結構如下；版本、日期與條目皆應替換為本次發行的實際內容：

```markdown
## Unreleased

## [1.3.4] - YYYY-MM-DD

### 中文

- 在设置中增加界面语言选择。

### English

- Add interface language selection to Settings.
```

`Get-ReleaseMetadata.ps1` 擷取整個版本區段，僅在下一個 `## ` 標題或檔案結尾停止，因此 `### 中文` 與 `### English` 皆會完整進入 Release 說明。不要將語言標題寫成 `## 中文` 或 `## English`，否則會提前結束版本區段。目前中繼資料檢查驗證版本區段唯一、日期有效且內容非空；兩種語言是否齊全、條目是否一致仍須發行審查確認。

工作流程在雙語記錄後附加中英文下載與執行說明、SHA-256 核對命令，以及對應原始碼與建置說明。下載說明須以兩種語言明確標示 Linux 與 macOS CLI 為實驗性功能，並分別說明 Windows EXE 的簽署狀態及即時燒錄的實驗性限制。

### 自動檢查與草稿

`.github/workflows/release.yml` 關注 `main` 上 `Directory.Build.props` 或 `CHANGELOG.md` 的變更，但僅在該次 push **同時修改版本設定與發行記錄，且完整版本變更**時進入發行流程。一般程式碼 push 僅執行常規建置檢查；只補充 `Unreleased` 或未改版本的設定調整不會產生 Release。

工作流程從設定讀取完整版本、驗證格式與對應的非空含日期 Changelog 區段，確認發行提交屬於 `origin/main`，並重用 `build.yml` 的建置與回歸檢查。全部通過後才建立對應版本標籤與草稿。

Windows x64 繼續提供自包含的單一檔案 EXE。封裝指令碼在隔離目錄執行最終 EXE，進行自我測試、兩磁區 RAW/WAV 產生檢查及 GUI 截圖檢查。

Linux 與 macOS 的實驗性離線 CLI 在各自的 x64、Arm64 runner 建置，發行套件含 .NET 執行階段。每個 runner 解開剛產生的 tar.gz，在儲存庫目錄外執行 `--help`、DVD 自我測試，以及 CD 音軌、DVD 映像檔和藍光映像檔產生測試。發行流程收集通過測試的原始壓縮檔，與 Windows EXE 一起附加至草稿。例如：

```text
DiscImageStudio-v1.3.6-win-x64.exe
DiscImageStudio-CLI-v1.3.6-linux-x64.tar.gz
DiscImageStudio-CLI-v1.3.6-linux-arm64.tar.gz
DiscImageStudio-CLI-v1.3.6-osx-x64.tar.gz
DiscImageStudio-CLI-v1.3.6-osx-arm64.tar.gz
SHA256SUMS.txt
```

工作流程合併並驗證各平台 SHA-256 總和檢查碼，寫入 Release 說明與附件 `SHA256SUMS.txt`。工作流程使用內建 `GITHUB_TOKEN` 建立 **GitHub Release 草稿**，不需設定個人存取權杖（PAT）。預發行標籤自動設定 prerelease 旗標，正式標籤建立正式版草稿。

維護者在 GitHub 的 [Releases 頁面](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/releases)檢查草稿版本、發行說明、各平台附件與總和檢查碼，下載並驗證啟動後，再手動公開。可用下列命令計算 Windows 下載檔案的 SHA-256，與 Release 說明中的值比較：

```powershell
Get-FileHash .\DiscImageStudio-v1.3.6-win-x64.exe -Algorithm SHA256
```

### 失敗重試與更正

可重新執行失敗的 Actions 執行，也可在 GitHub Actions 從 `main` 手動執行 `release` 工作流程。手動觸發不輸入標籤，而是讀取 `main` 目前設定及發行記錄，執行全部發行檢查，不須再次修改版本檔案才能重試。

同版本重試時，既有標籤必須指向本次建置的同一提交，既有 Release 也必須仍為草稿。已公開的 Release、標籤與產物不得覆寫或移動；發現問題時，從 `main` 修正並發行新版本，預發行迭代則增加序號，例如 `rc.1` → `rc.2`。若 `main` 已前進且同版本標籤指向舊提交，應重新執行原本失敗的執行，或為新提交準備新版本。僅修改發行說明也應保留事實與更正記錄，不得替換已公開的二進位檔。

建議儲存庫管理員在 GitHub 設定 `main` 分支保護，要求 Pull Request 與 `build` 必要檢查；為 `v*` 標籤設定規則集，允許發行工作流程建立標籤並禁止更新、刪除。工作流程檔案不等同於遠端保護設定，這些保護須由管理員另外設定。

## 本機 EXE 演練

在儲存庫根目錄執行與 GitHub Actions 相同的封裝指令碼：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-Release.ps1
```

指令碼自動使用 `Directory.Build.props` 版本。例如設定為 `1.0.26` 時，輸出目錄為 `artifacts/release/v1.0.26/`。指令碼建置並個別驗證自包含 `win-x64` EXE，產生 `DiscImageStudio-v1.0.26-win-x64.exe` 及該檔案的 `SHA256SUMS.txt`。正式發行時，工作流程將四個平台 CLI 壓縮檔的總和檢查碼附加至清單。

## Microsoft Store MSIX

MSIX 是獨立的手動散佈通道。先在 Partner Center 保留產品名稱，再從 Product identity 頁面複製：

- Package/Identity/Name
- Package/Identity/Publisher
- Package/Properties/PublisherDisplayName

本機執行：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-MSIX.ps1 `
  -IdentityName "Partner Center Identity Name" `
  -Publisher "CN=Partner Center Publisher" `
  -PublisherDisplayName "Publisher display name" `
  -DisplayName "Reserved product name"
```

未填 `-Version` 時，從 `Directory.Build.props` 讀取正式版本並轉為 `A.B.C.0`。需明確指定商店版本時，使用 `-Version "1.0.26.0"`；此覆寫值也同步至套件內 EXE 版本，避免資訊清單與程式版本不一致。Microsoft Store 通道不接受 `-rc.1` 等預發行後綴；準備 Store 套件應使用正式版本設定。

也可在 GitHub Actions 手動執行 `package-msix` 工作流程並填寫相同值，`version` 留空時同樣讀取儲存庫設定。此工作流程先重用 `build.yml` 的 CI，再產生 MSIX 作為 Actions 附件；不會自動提交商店或附加至 GitHub Release。產生的 Store MSIX 可維持未簽署；本機側載測試需用符合 Publisher 的憑證簽署。

## 本機簽署測試套件

為本機測試建立主體與測試 Publisher 完全一致的程式碼簽署憑證，將公開金鑰憑證放在：

```text
artifacts/certificates/DiscImageStudio-Test.cer
```

使用憑證指紋執行 `Build-MSIX.ps1 -CertificateThumbprint <thumbprint>`（並填寫上文身分參數），產生已簽署套件。再按兩下 `packaging/Install-TestPackage.cmd`，或執行：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Install-TestPackage.ps1
```

安裝指令碼會要求一次系統管理員權限，將測試憑證公開金鑰匯入「本機電腦／受信任的人」，並安裝已簽署 MSIX。不要將私密金鑰或 PFX 檔案提交 GitHub，也不要將本機自我簽署測試憑證作為正式 Release 附件。

## Windows 11 未簽署快速測試（選用）

未簽署測試套件必須使用獨立 OID 發行者命名空間，不得與商店正式身分混用。產生本機測試套件：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-MSIX.ps1 `
  -IdentityName "12345.DiscImageStudioArchitectureTest" `
  -Publisher "CN=Disc Image Studio Test" `
  -PublisherDisplayName "Disc Image Studio Test" `
  -DisplayName "Disc Image Studio Test" `
  -Version "1.0.26.0" `
  -UnsignedDevelopmentPackage
```

再用系統管理員 PowerShell 安裝：

```powershell
Add-AppxPackage -Path ".\artifacts\msix\DiscImageStudio_1.0.26.0_x64_unsigned-dev.msix" -AllowUnsigned
```

`-UnsignedDevelopmentPackage` 僅供支援此功能的 Windows 11 系統快速測試；部分 Windows 版本即使提供 `-AllowUnsigned` 仍可能拒絕可執行套件，此時使用上述簽署測試方案。準備散佈或提交 Microsoft Store 時不要使用此選項，正式套件仍須使用 Partner Center 真實身分。

正式提交前：

- 使用真實身分重新封裝，不可提交測試 Identity 套件。
- 在乾淨的 Windows 裝置測試安裝、解除安裝與離線執行。
- 對最終套件執行 Windows App Certification Kit。
- 提供至少一張 1366×768 或更大的真實桌面截圖。
- 將 `PRIVACY.md` 發佈至公開 HTTPS 頁面，並填入真實支援聯絡方式。
- 在認證備註說明應用程式產生大型檔案、不需登入、不連網，並包含使用 Windows IMAPI2 的實驗性 CD/DVD 直接燒錄功能；如實列出適用裝置、空白光碟要求與目前驗證範圍。
