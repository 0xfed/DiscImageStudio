# 驗證說明

[简体中文](VALIDATION.md) | [繁體中文](VALIDATION.zh-TW.md) | [English](VALIDATION.en.md) | [日本語](VALIDATION.ja.md) | [Türkçe](VALIDATION.tr.md)

## 上游功能同步回歸

同步至上游 `62efb5c` 的檢查包含 `UsableSectorGenerationTests`、`UsableSectorPersistenceTests`、`StreamBurnCapacityTests`、`PresetApplicationTests` 及 `CalibrationConfirmedFitTests`。另涵蓋既有 UDF/VAT 更新、計時結果保留可用容量、目視選擇器截尾及新舊保護規則重放。配置仍由七語言矩陣檢查；這些測試不構成新的實際燒錄驗證。

本頁列出目前原始碼的可重現檢查。自動化回歸驗證演算法、檔案配置與應用程式工作流程；實際燒錄效果仍取決於燒錄機、韌體與光碟。

## 本機驗證

最近一次完整架構回歸：2026-10-28，涵蓋介面修正、標定產生最佳化與 Verbatim BD-R (64099) 預設（對應提交 `3088802`、`49a0e4d`、`46e2c53`），Windows、.NET SDK 10.0.400（執行階段 10.0.11），Release 組態。完整 solution 建置（0 警告、0 錯誤）與架構回歸全部通過；測試入口直接執行自製 Exe harness，不使用 `dotnet test`。

標定燒錄結束後已完成產生最佳化的建置、完整架構回歸與 DVD 引擎自我測試。加入 Verbatim 預設後，再次通過建置與完整架構回歸，驗證 GUI 與 CLI 的半徑、磁區數、極性一致，以及預設合併與命令列覆寫。v1.3.3 的版本／更新記錄驗證與發行工具 45 項檢查通過；未新增效能測速或實碟燒錄結論。

已通過的新增回歸包含：BD 標定平行開關、預設循序回呼、極性與繪圖範圍、批次順序與取消；CD 標定四點取樣的座標位元一致、交錯與位元組順序，CD 檔案模式內外圈的純量／SIMD 位元組一致性；DVD 解析取樣與碼字取樣的座標、索引、逐位元組輸出及平行尾批一致性。

在儲存庫根目錄執行：

```powershell
dotnet restore DiscImageStudio.slnx
dotnet build DiscImageStudio.slnx --configuration Release --no-restore
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release --no-build
dotnet run --project src/DvdImageSolver --configuration Release --no-build -- selftest
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- selftest
./tests/Test-ReleaseTooling.ps1
```

架構回歸涵蓋：

- CD/DVD：黑白映射、抖動、sRGB 伽瑪、檔案與串流一致性、SIMD 與純量取樣一致性。
- CD 檔案模式：ISO9660/Joliet 中文檔案獨立讀取、內外圈位置及專案儲存、檔案區預覽、原始磁區幾何、檔案與前向串流輸出一致。
- CD 資料 DAO：Mode 1 EDC 與全部 P/Q 同位檢查方程式、主通道位元組順序、導入區與預間隙位址、資料軌 Q 識別碼與 CRC、前向 Seek 與短讀、內外圈 ISO 完整還原；保留錸德預設 359,845 磁區，並使用模擬燒錄機驗證與音軌共用的容量限制。原生 IMAPI 測試僅建立記憶體映像，不建立燒錄裝置。
- BD：17PP/EDC/Reed-Solomon、3T/6T 映射與極性、繪圖範圍外清零、UDF 2.50、大於 4 GiB 檔案、sidecar 往返與無效邊界拒絕。
- DVD/BD 檔案位置：預設內圈相容、外圈 ISO9660/Joliet 與 UDF 目錄獨立解析、中文檔案內容、完整碟面座標、不可 Seek 串流與檔案輸出一致、容量與取消保護，以及專案中位置選項往返。
- 三態模型：專案 schema 2 往返與 schema 1 升級、預設 schema 4 與舊 schema 合併、`ProjectSession` 的 BD apply/capture。
- 燒錄：操作互斥、進度、循序寫入、逾時、取消與資源清理，皆使用模擬介面而非真實燒錄機。
- 串流燒錄顯示：僅裝置確認寫入量推進進度，CD 音軌／資料、DVD、BD 倍速換算，未知或無效速率、3 秒到期、封片與結束清理，傳輸記錄節流及遲到事件保護。
- 標定燒錄來源：CD/DVD/BD 標籤與儲存參數一致，BD 位元長度、極性與繪圖範圍，跨光碟類型切換、速度查詢，以及模擬燒錄請求與確認文案。
- 標定：三次軌距、BD 繪圖範圍與極性、照片校正、曲線擬合、工作階段儲存還原、光碟預設同步與參數精度。
- 預覽與視窗：CD/DVD/BD 投影與讀回、多顯示器及工作列邊界。
- 檔案區預覽：內外圈設計投影與帶 sidecar 的成品讀回標黃、DVD 三次軌距邊界、純繪圖及無效配置資訊不標黃、錯誤目錄／容量提示、切換與重設清理。黃色疊加不改變 ISO 資料。

GUI 可透過既有命令離屏產生截圖：

```powershell
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/home.png --tab 0
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/export-bd.png --tab 3 --project sample.discproj
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/preview-bd.png --tab 2 --project sample.discproj --live-input image.png --live-disc bd --live-mode calibrate
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/burn-bd.png --tab 4 --project sample.discproj --burn-source calibration --burn-progress write
```

`--burn-progress` 接受 `write`、`buffer`、`finalize`，使用模擬進度產生介面，不啟動實際燒錄。2026-10-28 核對了 BD 標定寬螢幕與 800×650 窄視窗、準備與封片狀態；底部寫入量、速率、倍速及進度列皆可見。此次另通過 DVD 引擎自我測試，未執行實碟燒錄。

`build` 工作流程另執行模組目錄的 DVD 自我測試、兩磁區 CD 輸出檢查及首頁截圖。發行單一檔案 EXE 的額外檢查見[發行指南](RELEASING.zh-TW.md)。

本次另核對了 DVD/BD 各兩種檔案位置的設計投影、外圈匯出頁與外圈成品讀回，共 8 張離屏截圖；DVD／應用程式自我測試與發行工具的 45 項回歸通過。

設計頁檔案選擇入口另檢查了 DVD、BD、CD、未選資料夾、18 個圖層、窄視窗與低視窗高度 7 種介面狀態；寬螢幕圖層清單內部捲動，檔案選擇區域保持可達，建置為 0 警告、0 錯誤。

v1.3.0 另通過 `Build-Release.ps1` 的 .NET 10 自封裝單一檔案驗證：隔離目錄僅放 EXE，燒錄介面建置資訊、DVD 自我測試、兩磁區 RAW/WAV/CUE、關閉 CUE 及 WPF 介面轉譯皆通過。最終內建 .NET / Windows Desktop 10.0.11，並同步保留其完整上游授權聲明。

v1.3.2 通過完整架構回歸、DVD 引擎與應用程式模組自我測試、發行工具 45 項檢查。新增回歸驗證藍光切換預設後另存與專案確認的極性一致，以及刪除最近預設後跨光碟類型選擇與幾何同步；畫布縮放涵蓋 25%–1200% 邊界、加減按鈕及適應視窗重設。另核對新建 CD、藍光、DVD 專案設定及 18 圖層設計頁共 4 張離屏截圖。

## 翻譯預先檢查

封裝翻譯回歸測試在不建置、簽署、安裝或提升權限的情況下檢查中繼資料、診斷、語言優先順序、Unicode 路徑及子檢查語言傳遞，數值文化維持不變。共用執行階段另驗證資源載入失敗的五語說明、例外型別與串流釋放。Windows PowerShell 可將下列 pwsh 改為 powershell；實際 Windows 安裝與封裝仍須在 Windows 驗證。

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

## 實碟驗證邊界

此次檔案位置與預覽變更未執行實碟燒錄。自動化測試不證明裝置寫入策略、實際反射亮度或燒錄成功率；藍光功能仍為實驗性。

v1.3.1 的 CD 資料碟 DAO 也尚未執行實碟燒錄；已通過完整架構回歸、獨立單一檔案 EXE 啟動、音軌／CUE、內外圈 ISO 檔案內容與成品讀回驗證。

已記錄的 DVD 三次軌距實拍與參數見 [README 中的前後對比](../README.zh-TW.md#辅助标定展示)。物理模型歷史稽核見 [PHYSICAL_MODEL_AUDIT.md](PHYSICAL_MODEL_AUDIT.zh-TW.md)，其中結論保留其標明的日期與程式碼基準，不能取代目前原始碼驗證。

後續實碟記錄應包含版本或提交、燒錄機與韌體、光碟型號、預設參數、速度、產生／燒錄入口及原始照片，避免將某一裝置的結果泛化至所有光碟。

輔助標定現在提供粗標、邊界標定和精標三個階段，並可獨立於繪圖專案使用。先確認盤片容量，再為每張空白盤新增對應階段的標靶；每張已燒錄盤都必須使用產生時儲存的記錄和照片。 [上游整合後的標定流程](CALIBRATION_TUTORIAL.zh-TW.md).
