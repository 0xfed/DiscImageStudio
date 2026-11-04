# 參與 Disc Image Studio 開發

[简体中文](CONTRIBUTING.md) | [繁體中文](CONTRIBUTING.zh-TW.md) | [English](CONTRIBUTING.en.md) | [日本語](CONTRIBUTING.ja.md) | [Türkçe](CONTRIBUTING.tr.md)

歡迎參與 Disc Image Studio。請先通讀本文；內容涵蓋問題回報、開發環境建置、模組邊界規則、驗證要求與提交流程。

## 回報問題

一般問題請透過 GitHub Issues 提交，並在描述中附上：

- 應用程式版本與取得方式（原始碼建置 / MSIX 套件），以及 Windows 版本。
- 可穩定重現的步驟、預期行為與實際行為。
- 適用的執行記錄或螢幕擷取畫面。

燒錄相關問題請額外附上燒錄機型號、韌體版本與光碟品牌／型號：可見效果和實測校準與這些硬體密切相關，缺少這些資訊的問題通常無法定位。

可被利用的安全性問題**不要**在公開 Issue 中揭露。請依[安全性政策](SECURITY.zh-TW.md)使用 GitHub 私密回報功能提交。

功能建議也透過 Issues 提交：說明要解決的問題、預期行為，以及是否已有可行的替代方式。

## 開發環境

- Windows 10 2004+ 或 Windows 11。
- .NET 10 SDK；儲存庫透過 `global.json` 鎖定 `10.0.403`，安裝後 `dotnet` 會自動選擇相符版本。
- 測試不相依於第三方框架，全部使用儲存庫內附的主控台回歸程式。

```powershell
git clone <your-fork-url>
cd DiscImageStudio
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DiscImageStudio.App --configuration Release
```

## 相依套件與建置輸入審查

建置使用 SDK `10.0.403` 和執行階段 `10.0.14`，預設啟用鎖定還原。已審查的 NuGet 相依圖儲存在 `eng/dependencies/{portable|windows}/{optionalRID}/{ProjectName}.lock.json`。修改 `VersionPrefix`、專案參考、套件版本或 SDK／執行階段後，應在同一次變更中重新產生並審查鎖定檔。

使用 Python `3.14` 更新涵蓋五個 CI 平台、僅允許 wheel 的完整 Python 鎖定檔；使用 NuGet 更新指令碼更新所有支援目標和 RID 的相依圖。在儲存庫根目錄執行：

```powershell
python packaging/Update-NuGetLocks.py
python packaging/Update-NuGetLocks.py --check
python packaging/Update-PythonLock.py
python packaging/Update-PythonLock.py --check
```

`NuGet.Config` 僅允許官方 NuGet 來源和已核准的儲存庫簽署憑證。套件來源或憑證輪替必須經過明確審查，並保留驗證現有鎖定套件所需的憑證。CI 檢查已提交的鎖定檔，不應自動改寫它們。

## 模組邊界

修改程式碼前請先閱讀[架構說明](docs/ARCHITECTURE.zh-TW.md)。核心取捨：外殼不相依於任何一種光碟編碼，媒體專用邏輯留在對應模組。提交審查會重點檢查以下規則：

1. 不要將 CD/DVD／未來藍光的實體調變、錯誤更正、磁區組織或檔案系統細節放入 `DiscImageStudio.Core` 或共用層。
2. 模組不得從 GUI 控制項讀取狀態，所有輸入均來自 `DiscJobRequest`；模組不得直接顯示視窗。
3. 底層演算法無法安全取消時，`SupportsCancellation` 必須為 `false`。
4. 模組 ID 與命令名稱必須全域唯一，目錄會在啟動時拒絕衝突。
5. 新命令必須註冊至 `DiscCommandDescriptor`，並補上目錄路由測試與最小輸出煙霧測試。

## 程式碼規範

- 遵循周圍程式碼的命名、結構與註解密度；建置輸出應維持零警告、零錯誤。
- 不要為了通過建置而刪減或降級功能，也不要自行新增相容性墊片或重複實作。
- 刪除看似有意保留的功能或程式碼前，先在 Issue 或 PR 描述中說明目的。
- 不要提交建置產物、二進位資源或任何私密金鑰 / PFX 檔案。

## 測試與驗證

Pull Request 前必須全部通過：

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
dotnet run --project tests/DiscImageStudio.LocalizationTests --configuration Release
dotnet run --project tests/DiscImageStudio.ImageIOTests --configuration Release
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release
```

依改動內容補充驗證，並在描述中寫明結果：

- 修改圖片版面配置、串流燒錄或光碟預設剖析時，對照 [VALIDATION.md](docs/VALIDATION.zh-TW.md) 的對應回歸項目複核結論。
- 修改 GUI 時，使用離屏算繪的螢幕擷取畫面自行檢查關鍵頁面，必要時更新 `docs/images/`。
- 修改延遲交錯、EFMPlus、ECC 等編碼鏈後，若 `selftest` 涵蓋項目未變，也請說明原因。

## 提交與 Pull Request

1. Fork（或從 `main` 建立）一個功能分支；每個分支只處理一件事，讓差異保持可審查。
2. 提交訊息的標題與內文均使用英文。標題簡短說明改動，內文依需要說明行為變化、驗證方式與大型檔案影響。每次發佈的 Release notes 都提供中文與英文兩節。
3. PR 描述應寫清楚動機、改動範圍與驗證結果；涉及使用者可見行為時，同步更新 `CHANGELOG.md` 的 Unreleased 段落與相關 `docs/` 文件。
4. 提交後確認 GitHub Actions 的 `build` 工作流程為綠色。

大型改動（新模組、新 UI 頁面、燒錄層契約變更）建議先開 Issue 或 Draft PR 討論方案，再進行實作。

## 版本與發佈

採用 `main` 搭配短期功能分支，並依修正、功能與不相容變更遞增語意化版本。日常貢獻將變更記錄於 `CHANGELOG.md` 的 `Unreleased`；維護者在同一個發佈準備 PR 中更新 `Directory.Build.props` 的版本設定與對應版本記錄，合併至 `main` 後由 GitHub Actions 自動產生版本標籤與 Release 草稿。一般程式碼 push 不會產生 Release。完整觸發條件、預先發行版本後綴、草稿審查與重試規則，請見[發佈指南](docs/RELEASING.zh-TW.md)。

## 授權條款

專案採用 [GNU GPL v3.0 or later](LICENSE)（`GPL-3.0-or-later`）。提交貢獻即表示你有權提交相關內容，並同意依該授權條款授權該貢獻。引入第三方程式碼、字型或圖片時，請保留原作者與授權條款，確認與專案散布方式相容，並補充[第三方聲明](THIRD-PARTY-NOTICES.txt)；第三方授權邊界見 [LICENSE-NOTICE.md](LICENSE-NOTICE.zh-TW.md)。
