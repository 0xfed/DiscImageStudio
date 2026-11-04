# 國際化稽核

[简体中文](LOCALIZATION_AUDIT.zh-CN.md) | [繁體中文](LOCALIZATION_AUDIT.zh-TW.md) | [English](LOCALIZATION_AUDIT.md) | [日本語](LOCALIZATION_AUDIT.ja.md) | [Türkçe](LOCALIZATION_AUDIT.tr.md)

更新於 2026-11-04。本稽核涵蓋儲存庫自有介面、CLI、診斷與文件，語言為簡體中文、繁體中文、英文、日文、土耳其文。已補齊識別的翻譯缺口；Windows CI 現已驗證執行階段國際化與配置矩陣。僅資源存在與結構檢查不能證明語義準確。

## 涵蓋範圍與複核

| 範圍 | 證據 |
| --- | --- |
| 共用資源 | 2053 條資源五語皆非空，無重複鍵或欄位；嵌入資源符合來源 |
| 來源參考 | 2077 處靜態／動態 C# 與 XAML 參考可解析，包含預設、階段、預覽標題、校準指導與對話框篩選規則 |
| 介面語義 | 全部 749 條既有介面資源已逐語複核：Common 154、Workflows 239、Workspace 151、Xaml 205 |
| CLI 與診斷 | 桌面／跨平台 CLI、獨立 DVD 引擎、Core、CD/DVD/BD、ImageIO、Imaging、ISO/UDF、燒錄層的自有說明、錯誤、進度、結果使用共用資源 |
| 工具 | 三個 C#、六個 Python 工具與四個 PowerShell 封裝／安裝腳本支援五語及語言選擇 |
| 文件 | 全部 23 份來源文件有五語涵蓋，包括 README、完整變更歷史、技術指南、政策與本稽核 |

語義複核恢復動作詞、限定條件、燒錄機採用速度設定、反射亮度、包含 60% 邊界的涵蓋率、校準記錄用語、僅刪選取點、輸出資料夾用途與雙向縮放。繁體中文交錯術語及語言自稱已一致。長標籤、標題、校準按鈕可換行增高並保留緊湊按鈕最小高度；Windows CI 配置檢查現已通過。

另一次清點追蹤未包裝的中日韓／英文片語候選與 XAML 顯示屬性至其產生位置。Core 擬合警告在顯示時翻譯，原始分類文字不變。已區分儲存的使用者預設值、測試資料名稱、通訊協定欄位、裝置詳細資料與說明文。發現並修復七條 DVD 串流錯誤及空白停用預設上下文；執行階段迴歸涵蓋每種語言，來源守衛涵蓋 DVD 介接層例外字面量與桌面／工具 XAML 顯示屬性。

已複核介面資源指紋使用 UTF-8 與統一換行，標識已複核版本，不是語義的自動證明。

| 資源檔案 | SHA-256 |
| --- | --- |
| `Common.json` | `7c304b1575bf7d3494c0f73032f45524e4439ce43f8bef24225041fabac971bf` |
| `Workflows.json` | `2a27cd6337ff55c4841271ad851da0b7c4bb6abede365d710714a210d9f32112` |
| `Workspace.json` | `b389bbf7a35a094f1b405e6e475b7af953a0ff045d32bfb096d465fc7063ba07` |
| `Xaml.json` | `f3728ada2da11e6be5614e2733e94e9b2e8ae0d448eedd7b6bb5bfae2250ed6f` |

## 保留行為

各命令列入口共用全域語言選項與環境偏好，涵蓋地區／系統回退、無效／缺值／重複選項、UTF-8 輸出與執行中介面選擇。僅變更 UI culture，不變更數值文化或參數解析；快取模組說明隨語言更新。啟動錯誤資源獨立載入，避免主要資源失敗與 CLI 錯誤包裝遞迴。

命令、選項名稱、schema 鍵、技術 ID、選項值、預設檔名、貢獻者、使用者自填名稱／說明、路徑、已存幾何維持不變。作業系統／第三方詳細資訊及官方授權原文保留。首次使用者預設僅在類型、ID、未改文字皆符合時於顯示階段翻譯。西班牙語有 24 條未啟用的部分資源，不屬五種支援語言。

編碼決策與二進位輸出不依賴翻譯；燒錄顯式攜帶收尾旗標。測試保留跨語言的專案資料、CD 音訊／資料、DVD EFMPlus／ISO、BD ISO／探針、ISO9660/Joliet/UDF 位元組、數值分析與生成 C# 表，不據此推論實碟結果。

## 文件完整性檢查

清單記錄標準來源雜湊與語言路徑。檢查保留標題、段落、清單項、表格尺寸／數值、行內程式碼、命令／公式、參考目標及本機連結。明確例外允許預設範例名稱／說明與兩個 Mermaid 顯示標籤翻譯，仍保留 JSON 型別／精度／ID／貢獻者、圖示節點／連線／方向／數字。缺少或空白標籤、重複 JSON 欄位、無效數值會失敗。

雙語發布來源日誌保持原結構。明確登記的並列清單須匹配項目數與技術識別碼，閱讀版每項變更只顯示一次；全部 122 條獨立記錄、歷史日期、原驗證限制保留。產生報告不計來源文件，僅四條明確登記、來源已參考且位於忽略 artifacts 目錄的輸出可於乾淨取出時缺少；其他本機連結須存在。

完整文件檢查回報 23/23，CI 必須通過。七項規則迴歸保護限制，結構檢查輔助語言複核，來源變更須重審。維護及 Windows 截圖命令見 [LOCALIZATION.md](LOCALIZATION.zh-TW.md)。

## 驗證與限制

啟用 Windows 目標的 Release 解決方案建置零警告／錯誤。共用國際化迴歸通過，涵蓋實際五語 DVD 串流失敗、例外型別／參數名、儲存資料、載入失敗資源釋放與數值文化。跨平台 CLI 套件冒煙、DVD 自檢、C# 藍光探針通過；對照 256 條獨立位元組向量與有限模式。假燒錄機測試保留位元組、MMC 命令／逾時、進度、取消、清理，未存取硬體。

PowerShell 通過 45 項既有發布迴歸與 69 項安全國際化預檢，涵蓋 47 個鍵、原樣中繼資料、Unicode 路徑與子處理程序語言傳遞；不建置、簽署、安裝、提權。Python 檢查 134 個鍵，六工具均對照原數值／schema 基準通過整合。參考搜尋至週期 8，三類圖表五語通過字形／文字邊界檢查，人工檢視五幅代表圖。測試資料皆合成，含故意無效圖集，不是物理量測或完整正式探針掃描。

Windows CI 已通過 WPF 語言切換、字形／配置截圖、抖色介面／PNG 整合、DVD 圖集／預覽整合及 Windows PowerShell 預檢。套件安裝路徑仍未執行。測試不確立真實燒錄機效能或光學亮度；這些為驗證限制，不宣稱新增硬體驗證。

## 重現檢查

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

Windows 另執行執行階段套件並檢視各語言截圖：

```powershell
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release
```

獨立 Windows CI 處理程序檢查五語共 325 個配置場景：CD/DVD/BD 工作區的 1366×900 深色、960×700 淺色配置，展開的預覽參數與讀回、校準面板、設定分類及新增／編輯專案對話框。檢查語言切換後已載入的頁面標題、實際繪製的文字邊界與可見裁剪區域及缺少字形，允許有意省略和最近專案路徑捲動。暫存存放區與不可寫碟的假燒錄機隔離測試資料，並檢查專案位元組、幾何及數值文化不變。報告記錄實際 DPI，不模擬其他螢幕。

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

執行器也可在本 Linux 工作區建置；Windows CI 已完成全部 325 個場景，問題數為零。即使檢查失敗也上傳 PNG 與 JSON 報告。空專案為合成測試資料，矩陣不驗證實體光學效果或全部填入資料後的工作流程狀態。
