# 藍光模式（單層 BD-R）

[简体中文](ADDING_BLURAY.md) | [繁體中文](ADDING_BLURAY.zh-TW.md) | [English](ADDING_BLURAY.en.md) | [日本語](ADDING_BLURAY.ja.md) | [Türkçe](ADDING_BLURAY.tr.md)

藍光實作位於 `src/DiscImageStudio.BluRay`，參考與媒體無關的 Core 和 `src/DiscImageStudio.Iso`，不依賴 CD/DVD 編碼器。依據 ISO/IEC 30190:2021，目前範圍為 **120 mm、單層 25 GB BD-R**；不支援雙層、BDXL、BD-RE 或藍光影片製作。

## 在應用程式中的位置

藍光不是第六個工作區，而是專案的第三種光碟類型（CD / DVD / BD）。開啟 BD 專案後：

- **設計**：畫布與合成圖依 BD 產生外半徑配置；左欄「隨碟檔案」可選擇資料夾及內圈／外圈位置，與匯出頁同步；右欄影調模組支援 2 階、sRGB 線性化、亮度與 3T/6T 極性。
- **預覽**：設計投影依產生／實測幾何、影調與極性繪製 3T/6T 類別圖；成品讀回直接分類 ISO 的 AA/44 酬載位元組，不套用極性，顯示的是類別圖而非反射亮度。
- **匯出**：產生完整 BD ISO（純繪圖或 UDF 2.01 檔案）；選擇資料夾後可將檔案放在內圈或外圈，預設內圈。位置隨專案儲存，並記錄於同目錄的 `<iso>.json` 附屬檔案。
- **燒錄**：將同一 ISO 串流直接送入 MMC 循序寫入，不儲存完整映像檔；工作列塢支援進度與取消。
- **校準**：BD 目標涵蓋完整資料區，儲存磁區、半徑、極性、繪圖起訖與 ISO 時間戳記；擬合採用固定軌距，並套回 BD 參數。

BD 預設與 CD/DVD 一起存放在 `disc-presets.json`（schema 4）的 `bluRayPresets` 中。內建「標準 25GB 單層 BD」「錸德 BD-R 10x」「錸德 BD-R 12x」「Verbatim BD-R (64099)」和「Verbatim BD-R (64293)」；使用者預設可在專案設定對話框底部儲存。

## 容量與幾何

- 單層容量為 12,219,392 個 2048-byte 使用者磁區，即 25,025,314,816 bytes；32 個磁區組成 65,536-byte 叢集。
- 專案分區的磁區數必須為 32 的正整數倍且不超過單層容量；外半徑不超過 60 mm。
- 產生與燒錄共用總磁區數；燒錄一律宣告完整光碟容量，檔案與中繼資料以外的未使用磁區為零酬載。
- 標稱軌距 0.320 µm，通道位元長度 74.5 nm，起始角 0°。

## 產生與 ISO

`BluRayIsoGenerator` 先規劃 UDF 2.01 目錄與檔案 extent，再配置 32 磁區對齊的繪圖範圍。內圈模式將檔案分區放在前方，外圈模式將檔案分區放在碟尾保留區之前；前方識別資訊、錨點與尾端備份資訊仍保留於規定位置。輸出長度固定為 `TotalSectors × 2048`。

- 未選擇資料夾：完整資料區純繪圖，不含檔案系統。此 `.iso` 可由本程式讀回，但一般檔案系統工具無法瀏覽。
- 選擇資料夾：寫入 UDF 2.01，支援大於 4 GiB 的單一檔案；`--file-position inner|outer` 選擇檔案位置，預設為 `inner`，`outer` 必須搭配 `--data-dir`。檔案、填補、圖案與碟尾中繼資料在同一前向串流中寫出。
- 輸出路徑必須位於來源資料夾之外，且不得覆寫來源圖片。取消或失敗會保留既有 ISO。
- 產生成功後寫出 `<iso>.json`（`format: bd-r-image-v1`）：總磁區、內外半徑、繪圖起訖與半徑、3T/6T 極性、`ToneProfile`、磁碟區標籤、UDF 時間戳記、來源圖片與產生時間。預覽選取此 ISO 時自動還原這些參數；沒有附屬檔案的外部 ISO 仍可依目前專案幾何讀回。

檔案使用一般使用者位元組，不得進行圖案解擾；圖案沿用原始整碟磁區位址與全碟幾何，不將剩餘圖案區重新拉伸至整張光碟。UDF 分區只涵蓋檔案系統所在區域，圖案不會標為檔案系統的可用空間。實際可繪圖區由檔案占用與碟尾備份區共同決定。GUI 的設計投影與具附屬檔案的成品讀回會將檔案區（含檔案系統）標示為黃色並附圖例；沒有配置資訊的外部 ISO 不會推測檔案範圍。

`BluRayIsoGenerator.Prepare` 先掃描資料夾、規劃 UDF 2.01 目錄與檔案 extent。內圈配置將檔案結尾向上對齊至 32 磁區，外圈配置將檔案分區起點向下對齊至 32 磁區。碟尾保留 288 磁區，供備份磁碟區描述元與錨點使用。此檔案系統 API 未指定資料夾時仍保留空磁碟區；必須至少剩餘一個完整繪圖叢集，最小測試容量為 640 磁區。

檔案／串流共用前向寫入路徑。規劃後若來源檔案或目錄內容改變，便拒絕繼續使用過期配置。檔案長度採用 64 位元欄位，大檔案拆分成多個不超過 `0x3ffff800` 位元組的配置 extent，使用 128 KiB 緩衝區循序讀取；單一檔案大小受光碟剩餘容量限制，不再受限於 4 GiB。DVD 的 ISO9660/Joliet 路徑維持不變。

UDF 實作依據 [ECMA-167](https://ecma-international.org/publications-and-standards/standards/ecma-167/) 與 [ECMA TR/112-4（UDF 2.01）](https://ecma-international.org/wp-content/uploads/ECMA_TR-112-4_1st_edition_december_2023.pdf)，寫入已關閉的唯讀磁碟區。用途為資料光碟，不提供藍光影片製作。

## 影調與極性

BD 的影調在來源圖片解析度一次完成：sRGB 線性化 → 亮度偏移 → 位元組層級抖色 → 128 閾值二值化。產生、燒錄與預覽共用此二值點陣，不對約 250 億個酬載位元組逐一雜湊。

`BluRayRunLengthMapping` 只執行最後一步：暗部寫入偏向 3T 次數的 `AA`，亮部寫入偏向 6T 次數的 `44`；`InvertMapping` 交換兩端。這是影像映射慣例，不保證純 3T/6T，也不代表實測光學極性。實際明暗方向須以校準碟觀察；極性欄位與 `ToneProfile` 分開儲存。

## 3T / 6T 次數映射與擴充

圖片流程使用 `BluRayImageLoader` 解碼並將透明區合成於白底，再由 `BluRayImagePayloadMapping` 取樣。圖片保持長寬比，最長邊對應光碟外徑，空白區域為白色。取樣位置包含各訊框 EDC 預留位置、LDC 交錯、BIS 插入、DC 位元、同步與 run-in；由 2:3 調變率計算的是標稱位元組中心，而非變長 17PP 碼字的精確邊界。

內建 `BluRayRunLengthMapping` 先以 128 分界，再回傳擾碼域 `AA` 或 `44`。選擇依據是在同為 958,272 通道位元的完整叢集中，相鄰 modulation ONE 間隔為 3T / 6T 的次數，mark 與 space 皆計入，而非某一電位的工作週期。基準 PSN `0x100000` 的 256 個重複位元組掃描中，`AA` 有 265,136 次 3T，`44` 有 116,575 次 6T，各為目標次數最高者；四個位址複核仍保有強烈偏向，但不保證每個位址皆排名第一。詳見[位元組分析](BLURAY_BYTE_DENSITY.zh-TW.md#按-3t--6t-数量接入绘图)。

實作 `IBluRayLuminanceMapping.Map` 可替換策略，依來源圖片亮度與標稱位元組位置回傳所選擾碼域位元組。非圖片實驗也可直接實作 `IBluRayPayloadMapping.FillScrambledPayload`，填入磁區的 2048 個使用者位元組位置，再交給 `BluRayStreamingGenerator`；回呼接收標稱磁區、PSN 與光碟參數。

`BluRayDiscModule` 透過建構函式注入圖片解碼委派，保持不依賴 WPF；App 的 CLI 使用 `BluRayImageLoader.Load`。直接使用模組 API 產生圖片時也應提供此委派。`BluRayConstantByteMapping` 仍只用於明確的數位實驗。圖片與校準皆經由 `BluRayIsoGenerator` 產生檔案或向燒錄器供應串流。

候選篩選見[位元組與位元週期密度分析](BLURAY_BYTE_DENSITY.zh-TW.md)。`tools/BluRayByteProbe` 可重現全部 256 個位元組與細粒度週期的完整叢集統計；週期位元串屬於其他實驗策略，目前次數映射採用重複位元組。分析結果仍未指定實體光碟黑白極性。

## 命令列

```powershell
# 檢視單層光碟參數，或匯出 JSON
dotnet run --project src/DiscImageStudio.App -- bd-info --output bd-parameters.json

# 純繪圖 ISO（1024 磁區僅供快速功能驗證，並非實際全碟設定）
dotnet run --project src/DiscImageStudio.App -- bd-generate --input image.png --output bd-test.iso --total-sectors 1024 --invert true

# 含 UDF 內圈檔案的完整容量 ISO
dotnet run --project src/DiscImageStudio.App -- bd-generate --input image.png --output bd-image.iso --data-dir C:\Files --volume-label BD_IMAGE

# 影調：抖色、亮度、sRGB 線性化
dotnet run --project src/DiscImageStudio.App -- bd-generate --input image.png --output bd-tone.iso --tone-dither 0.75 --tone-gamma true --tone-brightness -4

# 原始酬載探針與單個 ECC 叢集診斷
dotnet run --project src/DiscImageStudio.App -- bd-probe --byte 0xA6 --sectors 32 --output bd-probe.bin
dotnet run --project src/DiscImageStudio.App -- bd-encode --input bd-probe.bin --psn 0x100000 --output bd-ecc.bin
```

`bd-generate` 的 `--preset` 可選 `bd-r-sl-25gb`、`bd-r-ritek-10x`、`bd-r-ritek-12x`、`bd-r-verbatim-64099` 或 `bd-r-verbatim-64293`；`--total-sectors`、`--r0`、`--r1`、`--start-angle` 可覆寫對應值。`--invert` 接受 `true`/`false`。`bd-generate` 不再接受舊的 `--start-sector` / `--sectors` 切片參數；切片僅保留於 `bd-probe`。

`bd-probe` 的 `--byte` 為 0..255 或 `0x00`..`0xFF`，範圍按 32 對齊。`bd-encode` 輸入必須恰為 65,536 bytes，輸出 76,880 bytes 的 ECC 叢集，不含 17PP、DC-control 或檔案系統，不可當作可燒錄映像檔。

## 編碼鏈

| 部分 | ISO/IEC 30190:2021 |
| --- | --- |
| 2048-byte 酬載 + 4-byte EDC | §13.2–13.3 |
| 16-bit LFSR，PSN bits 19..5 衍生種子；每個 data frame 重設 | §13.4 |
| LDC RS(248,216)，304 個碼字及交錯 | §13.5–13.8 |
| 位址、RS(9,5)、BIS RS(62,30) 及多工 | §13.9–13.14 |
| 17PP、DC-control 與同步訊框 | §13.15–13.17 |
| 記錄單元間隔 | §14 |
| 標稱軌距 0.320 µm | §15.4.3 |
| 單層 PSN 0x00100000..0x00CA73FF | §17 |
| 單層資料區 381,856 個叢集 | §19 |

圖案測試寫入的是**解擾後的使用者酬載**。實際光碟機會計算 EDC，再對完整 2052-byte data frame 擾碼；EDC、錯誤更正、位址、同步等資料不能由指定位元組探針任意覆蓋。

`BluRayRecordingFrameEncoder` 接受呼叫端明確提供的 28 個 DC 控制位元。`BluRayRecordingUnitEncoder` 另提供包含 run-in、496 個訊框、run-out 與可選 Guard3 的診斷模擬，每個調變位元／NRZI 電位以一個位元組表示。自動策略明確命名為 `BluRayGreedyDcControlEncoder`：將尚未決定的後續位元視為 0，進行前瞻貪婪選擇；後續控制位元定案後，可能改變先前的局部最佳選擇，回傳的 `SuboptimalDcControlDecisions` 會報告此情況。此策略供實驗使用，不保證標準要求的最終 DC 最佳性，也不承諾與燒錄器的實際位元序列一致。CLI 的 `bd-encode` 只匯出確定的 ECC 階段，不將此診斷模擬作為可燒錄映像檔輸出。

`BluRayTrackGeometry` 使用獨立固定軌距螺旋，以手動總磁區數對應的總軌長及內外半徑求出有效軌距；最後磁區到達設定的外半徑。從資料面看，固定光碟座標的取樣軌跡順時針向外（§9.8、§15.3）；螢幕轉換僅反轉一次 Y 軸。每個記錄單元的跨度為 `2760 + 496 × 1932 + 1104 = 962136` 個通道位元，位元長度 74.5 nm。`bd-probe` 的局部範圍沿用完整總容量的幾何。序列尾端 Guard3 不包含在每個記錄單元的重複跨度中。

預設 24–58 mm 為標稱幾何，並非特定光碟的實測參數。內建串流燒錄藉由保持 BD-R 未格式化、不配置缺陷管理備用區，來符合 `sector → PSN` 模型。實際半徑、位置與光學對比仍須實體光碟校準；外部軟體燒錄 ISO 時也應採用無備用區方式。

## 無備用區串流燒錄

`IDiscFormat2Data` 對未格式化 BD-R 回報的可用容量可能是預設備用區方案的 11,826,176 磁區，比原始 12,219,392 磁區少 768 MiB。藍光路徑透過 `IDiscRecorder2Ex.SendCommandSendDataToDevice` 從 LBA 0 循序傳送 `WRITE(10)`，每次 32 磁區（64 KiB），單一命令逾時為 200 秒，預留首次寫入初始化的等待時間。緩衝區遵循介面卡的位址對齊與最大傳輸限制。此路徑不呼叫資料燒錄介面的 `Write`、`FORMAT UNIT` 或配置備用區的操作。

公開的 `IWriteEngine2` 介面沒有可設定的命令逾時屬性，因此查詢、清空快取的逾時設定無法延長 `WriteSection` 內部的等待時間。目前明確命令路徑會回報失敗命令的 LBA、磁區數、實際耗時、HRESULT 與 Sense。逾時代表裝置端結果可能不確定，程式不會自動重送該範圍。

失敗記錄另保留最近 8 次成功寫入的時序，分別統計來源串流讀取等待、裝置命令等待及累計確認的資料量。無有效 Sense 時明確顯示「未傳回有效的裝置 Sense 資料」；全零回傳不可解釋為光碟沒有錯誤。`0x8007048F` 表示 Windows 回報裝置未連接。若標示 200 秒逾時的命令在數毫秒內失敗，隨後裝置重新列舉，須調查裝置連線、韌體及 USB 事件，不可只視為等待時間不足。本執行個體的所有裝置查詢與燒錄均排隊執行，避免未結束的查詢與寫碟重疊。

獨佔鎖定光碟後，程式以 MMC 讀取目前 profile、光碟資訊、格式容量與軌道資訊。必須是空白、未格式化的單層 BD-R SRM，首軌從 0 開始且原始可寫容量完整；未知、已使用或已格式化的配置皆拒絕，不自動降低總磁區數。介面設定的總磁區數以 2048 位元組換算為同一要求長度，UDF 前綴、繪圖區與尾端中繼資料均包含在內；較小的手動容量仍寫入相同指定數量的磁區。

寫入完成後清空快取、關閉軌道，並以 BD-R 的 finalize 功能 6 封碟。取消在目前裝置命令結束後生效；取消或寫入失敗時僅盡力清空快取及釋放裝置，不會將未完成映像檔當作成功產物封碟。各項解鎖僅成功後標記完成，失敗項會重試一次，錯誤也保留於診斷資訊；已成功的計數配對操作不重複執行。進入封碟後等待命令完成。無備用區模式沒有可供壞區塊重新映射的備用容量；中斷後同一光碟通常無法繼續寫入。

BD 即時速率依燒錄器確認接收的寫入命令計算，包含裝置快取影響，不等於雷射在碟面上的瞬時速度；最終成功仍以清空快取及封碟完成為準。4× 的標稱使用者資料速率為 18 MB/s（1× = 4.5 MB/s），介面採用十進位 MB/s，不能以要求倍速取代實際吞吐量；自動模式仍選擇目前光碟回報的最低設定。

寫入前立即記錄裝置傳回的設定速度；設定值與即時吞吐量分開保留。寫入期間每 10 秒及最後區塊後記錄一個診斷視窗，區分來源串流讀取耗時（含等待與複製）及 WRITE 命令耗時（含傳輸與裝置等待），並記錄確認接收的平均速率。低頻記錄不增加裝置查詢，不改變進度或速率時效規則；來源串流讀取耗時不可視為生產執行緒的純計算耗時，命令耗時也無法單獨區分 USB 傳輸與碟面寫入。

圖片供流採用最多 8 個工作執行緒、16 個叢集（1 MiB）的有界批次，計算完成後依原順序寫出。內建 3T/6T 閾值映射及擾碼 XOR 使用整數 SIMD，位置計算保留原精度；不再逐位元組重複檢查已驗證的光碟參數。不可變點陣圖自動啟用平行處理，內建校準圖案明確宣告取樣具執行緒安全性；自訂取樣回呼預設維持循序。預先緩衝只能暫時吸收速度差異，持續燒錄仍要求供流速度高於所選裝置速度。

通訊協定與工作流程測試僅使用假裝置及 1024 磁區（2 MiB）的 UDF 資料串流；完整 12,219,392 磁區僅驗證容量與幾何中繼資料，不產生全碟 ISO。實際燒錄結果仍須實體光碟驗證。

實作依據：[Microsoft 傳送資料與命令逾時介面](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscrecorder2ex-sendcommandsenddatatodevice)、[libburn MMC 實作](https://dev.lovelyhq.com/libburnia/libburn/src/branch/master/libburn/mmc.c)、[libburn 寫入逾時](https://dev.lovelyhq.com/libburnia/libburn/src/branch/master/libburn/spc.h)。

## 驗證範圍

自動檢查涵蓋 17PP/EDC/Reed-Solomon、完整 RUB 重放、圖片映射、平行產生、ISO/UDF、大於 4 GiB 的檔案、循序燒錄命令、繪圖範圍、附屬檔案及取消。落盤 UDF 測試僅 1024 磁區，單個 ISO 最大 2 MiB；大檔案測試先啟用並驗證稀疏屬性，再建立超過 4 GiB 的邏輯來源檔案（實際配置小於 1 MiB），向驗證後捨棄資料的串流循序輸出，不寫入大型 ISO。真實光碟容量僅做數學驗證。測試不存取真實燒錄裝置，也不證明實際碟面的可見亮度或極性。
