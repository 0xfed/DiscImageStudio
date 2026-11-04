# CD 音軌格式、位元組順序與重影排查

[简体中文](CD_AUDIO_COMPATIBILITY.md) | [繁體中文](CD_AUDIO_COMPATIBILITY.zh-TW.md) | [English](CD_AUDIO_COMPATIBILITY.en.md) | [日本語](CD_AUDIO_COMPATIBILITY.ja.md) | [Türkçe](CD_AUDIO_COMPATIBILITY.tr.md)

## 隨碟檔案與資料光碟

CD 專案在設計頁或匯出頁選擇隨碟資料夾後，自動切換為 ISO9660/Joliet 資料光碟，輸出副檔名切換為 `.iso`；清空資料夾路徑後恢復 WAV 音軌。檔案位置可選內圈或外圈，預設內圈，設定隨專案儲存。檔案系統中繼資料仍位於碟首，外圈模式將檔案內容放在碟尾，繪圖維持原有的整碟座標，不壓縮至剩餘區域。

```powershell
DiscImageStudio.exe cd-generate --input picture.png --output cd-image.iso `
  --data-dir "D:\随盘文件" --volume-label CD_IMAGE --file-position outer
```

ISO 長度為 `sectorCount × 2048`（sectorCount 為磁區數），以 CD-ROM Mode 1 資料光碟燒錄。程式內即時燒錄使用相同 ISO 資料串流；外部燒錄軟體應選擇燒錄映像檔功能。音訊交錯開關與 CUE 僅用於音軌模式。資料光碟的繪圖區另行補償 CD-ROM 擾碼及位置映射，隨碟檔案保留原始內容。

CD 資料光碟即時燒錄與音軌共用 DAO 工作階段及 lead-out 邊界檢查，保留 ATIP 校準的磁區數與完整檔案配置。程式將 ISO 酬載編碼為 Mode 1 原始磁區，並設定資料軌標記；使用錸德醫療水藍片預設時仍寫出 359,845 個資料磁區，不依 TAO 介面較小的可用容量縮短圖案或檔案。DAO 沿用音軌模式最多 3 磁區的受限 overburn 檢查，超限會在寫入前報錯。

資料軌使用 IMAPI 小端主通道慣例：在標準 Mode 1 磁區補齊 EDC、P/Q 錯誤更正碼並擾碼，保持同步標頭 `00 FF…FF 00` 的位元組順序。CD-ROM 實體編碼的相鄰位元組交換，與轉為小端的交換互相抵消；此點亦與 cdrdao 的 [RAW 編碼及裝置位元組順序轉換](https://github.com/cdrdao/cdrdao/blob/master/dao/dao.cc)一致。檔案內外圈的 ISO 配置與繪圖實體座標映射，不隨此傳輸慣例改變。

設計投影、影調小預覽及具有有效 `.iso.json` 配置資訊的成品讀回，以黃色標示檔案區（含檔案系統）。同名中繼資料記錄幾何、影調與檔案位置；移動 ISO 時建議一併移動。預覽顯示繪圖酬載的映射，實體光碟效果仍須燒錄驗證。

## 建議用法

新產生工作預設輸出 `cd-track.wav`，同時產生 `cd-track.cue`。WAV 為未壓縮 PCM：44.1 kHz、16 位元、雙聲道，資料區為小端。兩個檔案應放在同一目錄，移動或重新命名時也須同步更新 CUE 中的檔名。CUE 為選用：在匯出工作區取消勾選「同時匯出 CUE 檔案」，或命令列傳入 `--cue false`，便只寫出 WAV。

| 燒錄入口 | 使用方式 |
| --- | --- |
| cdrecord | `cdrecord dev=DEVICE speed=SPEED -audio "cd-track.wav"`（DEVICE 為裝置，SPEED 為速度），不要額外加入 `-swab` |
| ImgBurn | 在「Write image file to disc」中開啟 `cd-track.cue` |
| 程式內串流燒錄 | 自動產生相同小端 PCM 資料，不含 WAV 標頭，不產生完整暫存音軌 |
| 舊 RAW 工作流程 | 輸出仍可選擇 `.raw`；`cdrecord -audio "cd-track.raw"` 不加 `-swab` |

輸出副檔名決定實際格式，只接受 `.wav` 與 `.raw`。不能藉由重新命名將舊 RAW 變為 WAV，必須重新產生。WAV 配套 CUE 使用 `FILE "cd-track.wav" WAVE`，不使用 `BINARY`。Unicode 檔名採用含 BOM 的 UTF-8 CUE，支援空格與中文。ImgBurn 作者曾驗證此類 CUE 的 Unicode 檔名支援，並指出早期 2.5.0.0 的相關問題已於 2.5.1.0 修復。[ImgBurn 作者說明](https://forum.imgburn.com/topic/13526-unicode-filename-problem-in-cue-file/)

命令列產生範例（幾何參數應採用自己的實測設定）：

```powershell
DiscImageStudio.exe cd-generate --input picture.png --output cd-track.wav --interleave true
```

`--cue`（預設 `true`）控制 WAV 輸出是否同時寫出同名 CUE；`.raw` 輸出一律不寫 CUE。

WAV 檔案長度為 `sectorCount × 2352 + 44`；音訊長度、產生進度與串流燒錄容量仍為 `sectorCount × 2352`。檔案標頭不占光碟音軌磁區，也不改變圖案幾何。

## 影調（抖色、sRGB 線性化、灰階與亮度）

CD 每個音訊位元組對應約 6.8 µm 的碟面長度；正常觀看距離下，眼睛會將成百上千個位元組整合為一種色調。因此「每個位元組只有黑白兩種圖案」不等於「畫面只有黑白兩色」，中間調靠相鄰位元組的黑白比例還原。

| 參數 | 含義 |
| --- | --- |
| 抖色強度 0–100% | 0% 依 128 硬閾值輸出（舊行為）；100% 逐位元組在相鄰階數間按比例選擇，中間調由黑白位元組的空間比例形成 |
| 灰階階數（僅 CD） | 2 階用 `0x0D` / `0xAA`；4 階用 `0x0D` / `0x10` / `0x63` / `0xAA`（`0x0D` 為黑、`0xAA` 為白） |
| sRGB 線性化（伽瑪） | 先依 sRGB 轉移函數解碼再抖色；改變黑白點密度，不改變輸出電位 |
| 亮度 | 整體平移灰階，範圍 −64…+64（0..255 單位） |

命令列參數（`cd-generate` 與 `cd-preview-warp` 皆接受）：

```powershell
DiscImageStudio.exe cd-generate --input picture.png --output cd-track.wav `
  --tone-dither 1 --tone-levels 2 --tone-brightness 0 --tone-gamma false
```

預設值為 `--tone-dither 0 --tone-levels 2 --tone-brightness 0 --tone-gamma false`，與舊版逐位元組一致。新專案在設計頁右欄「影調」模組預設 75% / 2 階；沒有 `tone` 區段的舊專案以 0% 載入，儲存後寫入 `tone` 區段。匯出頁僅保留輸出選項，並在輸出卡片提示影調屬於設計頁。

抖色相位由**邏輯位元組索引**決定（`globalByte`），而非螢幕座標。延遲交錯將位元組移動至檔案其他位置後，讀回預覽仍依相同邏輯索引還原，因此產生、校準投影與讀回看到的是同一套圖案。校準圖案（`GeneratePattern`）固定使用硬閾值，不參與抖色。

音軌旁的同名 `.json` 記錄 `toneDitherStrength`、`toneLevels`、`toneBrightness`、`toneSrgbLinearization`；預覽頁載入外部音軌時，會連同幾何、交錯一起同步這些值。

讀回預覽預設 1600 px、每磁區 16 個取樣，並將落於同一像素的取樣**累加平均**。舊版固定 900 px、每磁區 2 個取樣且後寫覆蓋，稀疏取樣會在中間調留下白點，看起來像報紙網點；提高取樣密度後，同一碟面才能呈現連續影調。預覽頁的「設計投影」預設將實測幾何設為與專案幾何相同，開啟專案即得到中性投影；填入實測半徑後才會顯示兩套幾何的差異。

## 已確認問題

2026-09-12 的使用者實體光碟對照：同一份既有 RAW，以 cdrecord 的 `-audio` 與 `speed` 正常；加入 `-swab` 即可重現重影。這確認既有 RAW 應依 cdrecord 預設的大端音訊輸入解讀。

先前檔案匯出與串流燒錄共用完全相同的 `GenerateToStream` 輸出，串流路徑未執行位元組順序轉換。但不同輸入入口的慣例不同：

| 輸入 | 音訊資料要求 |
| --- | --- |
| cdrecord 無標頭 `-audio` | 預設大端 |
| cdrecord 無標頭 `-audio -swab` | 小端；此選項宣告需要交換的輸入順序 |
| cdrecord WAV | 解析標頭，自動辨識音訊位元組順序 |
| CUE 的 `FILE ... BINARY` | 小端；不能將既有大端 RAW 直接套用於 BINARY CUE |
| IMAPI `AddTrack(IMAPI_CD_SECTOR_AUDIO, stream)` | 與 16 位元雙聲道 44.1 kHz WAV 資料區相同的樣本配置，即無標頭小端 PCM |

上述 cdrecord 行為與 BINARY 定義來自 [cdrecord 官方手冊](https://cdrtools.sourceforge.net/private/man/cdrecord/cdrecord.1.html)；IMAPI 音訊樣本配置來自 [Microsoft 的 AUDIO 磁區定義](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/ne-imapi2-imapi_cd_sector_type)。ImgBurn 作者也提供過成功燒錄 `FILE ... WAVE` CUE 的範例。[ImgBurn WAVE CUE 範例](https://forum.imgburn.com/topic/25173-extract-only-session-two-from-audio-cd/page/2/)

此處 `AudioSectorType = 0` 是 IMAPI 列舉的 AUDIO 值，並非將音軌寫為實體 Mode 0。`IRawCDImageCreator::AddTrack` 目前只支援 AUDIO 類型。[Microsoft AddTrack 文件](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-irawcdimagecreator-addtrack)

## 為何會形成兩組輪廓

既有 `CddaInterleaver` 為目標位元組執行延遲補償。交換最終輸出中各 16 位元樣本的兩個位元組，會將原本屬於不同延遲位置的位元組交叉送入編碼鏈。因此不能將變化視為一般音訊數值變化，也不能在圖片取樣前先交換一次來抵消。

以既有延遲表進行整數標籤排列實驗，排除頭尾填補後，額外的樣本位元組交換會使逆映射後的偶數位置偏移 `+73` 個目標位元組，奇數位置偏移 `-73` 個目標位元組。這解釋雙輪廓機制；它是程式延遲排列模型的推導，並非完整光碟機 CIRC 模型或實體光碟測量。

修正保留取樣與延遲表，在補償完成後的 2352 位元組磁區輸出邊界選擇位元組順序：舊 RAW 大端，WAV 與 IMAPI 小端。WAV 標頭解決外部工具對格式與位元組順序的辨識，串流介面仍依其慣例只接收資料區。

## 讀回必須抵消延遲交錯

延遲補償將邏輯位元組 i 推至檔案索引 `i + offset(i % 24)`：交錯表依 28×D（D=4）訊框的延遲線儲存，每訊框 24 位元組，因此偏移介於 72 與 2592 之間（對應訊框延遲與訊框內欄位旋轉之和）。直接依檔案順序將位元組鋪到螺旋線上，等於將圖案整體旋轉並剪切——同一圖片在 60000 磁區、900 px 對照下，約 17.5% 的繪製像素與來源不符，呈現「輕微扭曲」。

因此 `cd-preview-track` 預設執行逆映射（`--deinterleave`，預設 `true`）：邏輯位元組 i 取檔案索引 `i + CddaInterleaveTable.FileOffsetFor(i % 24)`，需要 2592 位元組的前瞻視窗。此表的正向延遲與 `CddaInterleaver` 共用 `CddaInterleaveTable`，兩側不會各自漂移。

尾端代價：延遲線在檔案結束時仍持有最後數個訊框，這些邏輯位元組未寫出（1318–1332 位元組，不足一個磁區），讀回時只能留白。它位於音軌最外圈，占整條音軌的比例隨磁區數增加而下降。

`cd-generate` 現在會在音軌旁寫出 `<track>.json`（`format: cd-da-track-v1`，含 r0/r1/磁區數/線速度/起始角/圖片外半徑/是否交錯）。預覽工作區切換音軌時讀取它並載入校準幾何與交錯開關，因此匯入其他機器產生的音軌也能依正確模型讀回。

## 校準投影：來源圖片與兩套幾何

`cd-preview-warp` 是讀回路徑的快速對照，也是預覽工作區「校準預覽」模式呼叫的命令：輸入為來源圖片而非音軌，參數為兩套幾何——產生幾何決定第 i 個位元組從圖片何處取樣，實測幾何決定此位元組畫在碟面何處（`--actual-r0`、`--actual-r1`，預設 24.3 / 56.6 mm）。兩套幾何相同時投影即原圖；不同時，圖案依實測碟面偏移，正是半徑校準要觀察的現象。磁區數、線速度、起始角與圖片外半徑由兩套幾何共用，後三者固定為 1200 mm/s、0°、57.5 mm。

由於只讀來源圖片、不掃描整條音軌，修改半徑或幾何後的更新成本取決於圖片尺寸，而非音軌尺寸（80 分鐘光碟約 845 MB），適合在校準預覽中試參數，再以讀回模擬確認最終碟面。

## 驗證與邊界

- RAW 與 WAV：對非對稱圖案，交錯開啟與關閉時，WAV 的 data 區皆嚴格等於舊 RAW 每 16 位元交換後的位元組序列；長度不變。
- 串流輸出：在禁止 Seek/Position 的前向串流中產生小端音訊，結果逐位元組等於 WAV 的 data 區，且不含標頭。
- WAV 預覽：解析 RIFF 的 `fmt ` / `data`，跳過未知區塊與奇數長度填補，限制讀取範圍並正規化位元組順序；拒絕非 PCM、非 44.1 kHz、非 16 位元或非雙聲道資料。不同取樣步長下與對應 RAW 預覽一致。預覽仍為檔案位元組映射，未模擬完整實體 CIRC/EFM，不能取代實體光碟判定。
- 讀回一致性：同一來源圖片分別產生交錯與不交錯音軌；以 `--deinterleave true` 讀回交錯音軌，與依檔案順序讀回不交錯音軌的結果，除延遲線尾端一個磁區內的像素外逐像素相同（900 px、非對稱測試圖的差異僅落於尾端投影；變異測試將訊框延遲改動 24 位元組即回報 245 處不一致）。
- 校準投影一致性：同一來源圖片先依產生幾何 `(24.5, 56.8)` 產生音軌，再將 `cd-preview-warp` 投影（實測=產生、實測=`24.3/56.6`）與 `cd-preview-track` 以對應幾何讀回的結果逐像素比較，閾值化後完全一致（900 px）；縮小實測半徑時，投影重心半徑隨之減小，因此兩套幾何不會混用。
- Windows 記憶體實驗：向既有 `MsftRawCDImageCreator` 輸入 300 個非對稱音訊磁區，掃描完整的 2448 位元組 RAW 結果磁區，全部音訊位元組原樣保留。產生器不會代呼叫端交換位元組。測試不建立燒錄裝置、不寫入光碟。
- 2026-09-12 使用者在此修正後完成實體光碟試驗，回報已無重影。這是使用者提供的實體光碟結果；串流、cdrecord、ImgBurn 三個入口的逐項實測細節未分別記錄。

Microsoft 的 `WriteMedia` 文件未說明內部位元組交換實作；上述結論並未將記憶體實驗等同於觀察到裝置傳輸時的交換。[Microsoft WriteMedia 文件](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscformat2rawcd-writemedia)
