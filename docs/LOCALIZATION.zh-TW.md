# 國際化

[简体中文](LOCALIZATION.md) | [繁體中文](LOCALIZATION.zh-TW.md) | [English](LOCALIZATION.en.md) | [日本語](LOCALIZATION.ja.md) | [Türkçe](LOCALIZATION.tr.md)

點擊右上角常駐的「齒輪 + 設定」按鈕，在「一般 → 語言」選擇「跟隨系統」「简体中文」「繁體中文」「English」「日本語」或「Türkçe」，儲存後立即生效。首頁與所有工作區都顯示設定按鈕，也可按 Ctrl+, 開啟；取消、Escape 或關閉設定視窗不會儲存語言。切換語言不會自動重新啟動應用程式，也不會關閉專案或中斷工作。

`settings.json` 中的 `language` 使用 `system`、`zh-CN`、`zh-TW`、`en`、`ja` 或 `tr`。語言與佈景主題預設皆為跟隨系統；缺少、空值與不支援的語言也回復為跟隨系統，既有設定中有效的語言與佈景主題選擇維持不變。跟隨系統使用啟動時的系統 UI 語言：`zh-TW`、`zh-HK`、`zh-MO`、`zh-Hant` 及明確指定的 `zh-Hant-*` 使用繁體中文；`zh-CN`、`zh-SG`、`zh-Hans` 等其他中文系統使用簡體中文；`ja`、`ja-JP` 等日文系統使用日文；`tr`、`tr-TR` 等土耳其語系統使用土耳其語；英文、法文等其他系統統一使用英文。執行期間修改系統語言，須重新啟動應用程式才生效。佈景主題依 Windows 的應用程式色彩模式選擇深色或淺色。語言獨立於 `.discproj`，專案名稱、使用者預設名稱、圖層名稱、檔案路徑與數值剖析規則不參與翻譯。

## 新增與維護文案

翻譯位於 `src/DiscImageStudio.Localization/Catalogs/*.json`，建置時嵌入組件，無額外執行階段相依性。每個鍵包含 `zh-CN`、`zh-TW`、`en`、`ja` 與 `tr` 五種文字；繁體中文使用獨立資源，採用「設定」「專案」「檔案」「燒錄」等用語，不在執行期間轉換使用者資料：

```json
{
  "示例 {0}": { "zh-CN": "示例 {0}", "zh-TW": "範例 {0}", "en": "Example {0}", "ja": "例 {0}", "tr": "Örnek {0}" }
}
```

C# 用 `L.T("源文案")`，插值用 `L.F($"示例 {value}")`，資源鍵為其 .NET 複合格式 `示例 {0}`；需要明確資源鍵時可用 `L.Format(key, arguments)`。翻譯可調整參數順序，但必須保留索引與格式規範。XAML 使用 `xmlns:loc="clr-namespace:DiscImageStudio.Localization"` 與 `{loc:Text ResourceKey}`；`TextExtension` 在載入時依目前語言寫入，並在切換語言時就地重寫已載入的控制項與範本。

共用片語僅需定義一次，所有資源檔中的鍵必須唯一。找不到目前語言時回復為中文；鍵不存在時顯示鍵本身。啟動時先初始化語言，再建立視窗及 ViewModel。僅設定 UI culture，保留 numeric culture；不要翻譯通訊協定識別碼、命令、路徑、使用者資料，或將翻譯後的文字用於邏輯判斷。

目標是讓五種受支援語言對本儲存庫自有的介面、命令列說明、診斷與文件保持 1:1 語義對應。共用執行階段與資源位於 `src/DiscImageStudio.Localization`，桌面與 CLI 使用同一份資源。CLI 說明、模組與選項說明、CD 模組提示、DVD 模組狀態及命令路由錯誤已接入；藍光、圖片載入、Core、CD 與 ISO/UDF 的自有例外，以及藍光進度摘要、說明性 JSON 與照片校正提示也已翻譯，機器識別碼、幾何數值與二進位輸出維持不變；涵蓋範圍及已執行／待執行驗證見 [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.zh-TW.md)。第三方、作業系統例外詳細資訊及官方授權條款原文維持不變。

## 命令列語言

命令列支援全域選項 `--language zh-CN|zh-TW|en|ja|tr|system`，可放在命令前後，也可使用 `--language=ja`。明確選項優先於環境變數 `DISC_IMAGE_STUDIO_LANGUAGE`，否則跟隨系統介面語言。不支援的明確語言、重複選項及缺少值會傳回結束代碼 1。獨立 DVD 程式、跨平台 CLI 與 Windows 應用程式的命令列入口共用此選項；GUI 無命令啟動時仍使用已儲存的介面設定。

```sh
disc-image-studio --language ja --help
disc-image-studio cd-generate --language tr --input picture.png --output track.wav --sectors 2
```

僅翻譯顯示文案，不翻譯命令名稱、選項名稱、通訊協定識別碼、檔案路徑、使用者輸入或 JSON 欄位名稱。專案參數仍依各自既有規則剖析；選擇語言不會修改數值文化。CLI 關閉 invariant globalization，以支援具名 UI 文化。

標定擬合的曲線點指導資訊在介面顯示時翻譯，Core 傳回值仍保留原文，自動搜尋判斷不依賴翻譯。儲存預設時，自動產生的幾何說明保留可攜的原始文字與格式資源鍵；顯示時依目前語言與數值文化重新格式化，手動編輯的說明保留原文。

## 驗證

不依賴 WPF 的完整性檢查可在 Windows、Linux 與 macOS 上執行，CI 的各平台皆會執行：

```sh
dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release
```

此檢查驗證五種語言的非空文字、重複鍵、複合格式與數字／日期格式、檔案對話方塊篩選規則、C# 常值與插值呼叫、XAML 文案與範本觸發程序、內建預設、標定動態指導資訊，以及使用者預設回復。也會檢查快取模組說明隨語言切換、例外類型與參數名稱，以及專案、ISO9660/Joliet 與 UDF 輸出的語言間一致性。此檢查無法證明翻譯語義完全準確，也不能取代 Windows 上的執行階段切換與版面截圖檢查。

文件翻譯的段落結構、清單、命令範例、連結與來源檔案版本可另行檢查：

```sh
python3 tests/test_documentation_translations.py
python3 tests/test_documentation_translations.py --require-complete
```

文件檢查同時保留段落、表格列欄與數值（允許在地化小數與千分位分隔符號）。第一條僅驗證清單中已完成的文件並報告未完成項；第二條是全儲存庫文件完整性檢查，存在未完成項時失敗。目前檢查結果與待辦見 [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.zh-TW.md)。

CI 強制執行全儲存庫完整性檢查。

清單可明確允許預設 JSON 範例翻譯 `displayName` 與 `description`；欄位名稱、型別、數值精度、ID 及貢獻者仍須一致，重複欄位及無效數值會被拒絕。

產生的測試報告不計入來源文件。清單可用 `generated_artifacts` 明確登記可重建的結果連結；僅允許來源中已引用且位於忽略的 artifacts 目錄下的路徑，其他本機連結仍須存在。

清單可用 `translated_mermaid_labels` 列出可翻譯顯示文字的 Mermaid 節點；節點 ID、連線、方向、其他標籤與標籤中的數字須一致，缺少或空白的標籤會被拒絕。

發布來源日誌保留中文與英文並列小節；清單可用 `parallel_language_sections` 明確登記兩個標題，讓各語言閱讀版僅顯示一次相同變更。並列版本須為完整項目清單，且項目數、行內技術識別碼一致；缺少小節或額外未列項目會被拒絕，歷史子標題與其他正文仍完整保留。

燒錄層的例外、進度、操作說明與裝置診斷也使用共用資源。`OpticalBurnOperation.IsFinalizing` 明確攜帶收尾狀態，不可透過翻譯後的文字判斷是否封片。以下迴歸測試不依賴 WPF，使用實際燒錄程式庫搭配假裝置，在五種語言下檢查 CD/BD 位元組、MMC 命令與逾時、進度階段、取消權杖及清理行為；不存取真實燒錄機，也不執行實體燒錄。

```sh
dotnet run --project tests/DiscImageStudio.Burning.LocalizationTests -c Release
```

PowerShell 封裝與安裝指令碼透過 `packaging/Localization.ps1` 讀取共用資源，支援原生 `-Language` 參數與環境偏好，並將選定語言傳給子檢查與提升權限後的安裝程序。中繼資料、記錄正文、身分、路徑、總和檢查碼與數值文化維持不變。以下測試僅執行中繼資料與預先檢查失敗路徑，使用模擬子程序，不建置、簽署或安裝套件，也不要求提升權限。Windows PowerShell 可將 pwsh 改為 powershell。

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

C# 資源在選定語言後延遲載入；空資源與重複鍵診斷直接讀取獨立的嵌入式錯誤資源，避免在主要資源載入期間遞迴呼叫翻譯器。共用執行階段回歸測試驗證五語診斷、原始資源名稱、例外型別、串流釋放與數值文化。 CLI 錯誤前綴亦從此獨立資源讀取，確保入口錯誤處理不會重試已失敗的主要資源載入；Python 錯誤包裝使用相同方式。

Python 工具透過 `tools/localization.py` 讀取同一份 JSON 資源，也共用全域語言選項及環境偏好。剖析器的說明與錯誤、分析說明及圖表標註支援五語，不修改數值 locale、欄位名稱、路徑或演算法。標準函式庫檢查稽核所有 Python 工具及 argparse 格式參數；整合檢查需要 NumPy 與 matplotlib，使用最大週期 8 的參考搜尋、合成圖表及刻意無效的圖集，核對原始腳本的數值與 schema 基準、來源檔案、字型及圖表文字邊界，也驗證生成的 C# 表程式碼不隨語言改變。這些測試資料不是實體光碟測量或完整探針掃描。

```sh
python3 -m unittest discover -s tests -p test_python_localization.py
python3 tests/test_python_tool_localization.py
```

`LocalizationTests` 檢查資源語言完整性、格式參數、系統語言回復、設定儲存與取消行為，以及儲存失敗不會套用語言；`RelocalizationTests` 檢查執行期間切換語言後，專案幾何、進行中的讀取、預覽結果與已開啟頁面文案維持不變或正確更新。

```powershell
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language en --output artifacts/settings-en.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-CN --output artifacts/settings-zh.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-TW --output artifacts/settings-tw.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language ja --output artifacts/settings-ja.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language tr --output artifacts/settings-tr.png
```

截圖使用一次性設定檔，不變更使用者設定。新增語言時，同步擴充語言正規化與解析規則、設定下拉選項、各資源及測試。

獨立 Windows CI 處理程序檢查五語共 325 個配置場景：CD/DVD/BD 工作區的 1366×900 深色、960×700 淺色配置，展開的預覽參數與讀回、校準面板、設定分類及新增／編輯專案對話框。檢查語言切換後已載入的頁面標題、文字寬高、缺少字形，允許有意省略。暫存存放區與不可寫碟的假燒錄機隔離測試資料，並檢查專案位元組、幾何及數值文化不變。報告記錄實際 DPI，不模擬其他螢幕。

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

執行器已在本 Linux 工作區編譯；Windows 執行仍待推送後的儲存庫 CI。即使檢查失敗也上傳 PNG 與 JSON 報告。空專案為合成測試資料，矩陣不驗證實體光學效果或全部填入資料後的工作流程狀態。

新的上游裝置診斷資源可分批翻譯，缺少目前語言時回退至英文。已完成的介面資源繼續要求五語涵蓋；裝置欄位名稱、數值格式和原始作業系統錯誤保持不變。
