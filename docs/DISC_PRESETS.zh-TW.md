# 可編輯的光碟預設

[简体中文](DISC_PRESETS.md) | [繁體中文](DISC_PRESETS.zh-TW.md) | [English](DISC_PRESETS.en.md) | [日本語](DISC_PRESETS.ja.md) | [Türkçe](DISC_PRESETS.tr.md)

## 可用容量與儲存後的套用

幾何總磁區數決定完整螺旋曲線；可選 `usableSectors` 僅限制輸出長度、檔案系統容量及可用區域外徑。明確值不再扣保留量。舊預設或專案缺少此欄位時，CD 預設總數減 2，DVD 預設總數減 608 後向下對齊 16，BD 向下對齊 32。內建預設已提供可用數，不重複扣減。DVD 幾何總數可不對齊，輸出必須對齊。

在既有專案中儲存預設後，立即套用並選取新預設，同步預覽實測半徑與軌距。之後取消設定視窗不撤銷已儲存的參數，但不套用後續未確認的編輯。僅改專案名稱不重設手動實測值；新增專案對話框儲存預設不修改原專案，同名預設仍使用不同 ID。

光碟預設分為兩層：

- 內建層隨應用程式發行與更新，儲存官方預設。
- 使用者層位於以下檔案，僅儲存使用者新增、覆寫及停用設定：

```text
%LOCALAPPDATA%\DiscImageStudio\disc-presets.json
```

程式首次啟動時，會在使用者層加入「自訂 CD 參數」「自訂 DVD 參數」及「自訂藍光參數」三個可編輯預設。內建預設不會複製到使用者檔案，因此應用程式更新後的參數修正與新增預設會自動生效。

新增使用者預設的入口在專案設定對話框：「將這組參數儲存為預設」先在獨立視窗確認名稱，再將目前參數附加至使用者層；新建專案的範本清單會列出目錄中的全部非手動預設，依「最近」「CD」「DVD」「藍光」分類顯示。

專案設定對話框也會列出目前光碟類型的預設：選擇其他預設會覆寫對話框中的幾何參數，因此切換前會先要求確認，取消則保留原參數且不改變選取項目。新建專案與專案設定中的「自訂」預設均可透過右鍵選單刪除，刪除會立即寫回使用者層並重新整理清單；直接編輯 JSON（新增、改名、停用）後仍需重新啟動程式才生效。

## 合併規則

- 使用者預設使用新的 `id` 時，會附加在內建預設之後。
- 使用者預設使用與內建預設相同的 `id` 時，會完整覆寫該內建預設。
- `disabledCdPresetIds`、`disabledDvdPresetIds` 及 `disabledBluRayPresetIds` 可隱藏不需要的內建或使用者預設。
- 內建預設更新時，未被同 ID 使用者項目覆寫的參數會自動更新。

## 新增 DVD 預設

在 `dvdPresets` 陣列中複製初始化的使用者物件，修改 `id`、名稱及參數。例如：

```json
{
  "id": "verbatim-dvd-r-azo-43533",
  "displayName": "威寶 DVD-R AZO (43533)（實測）",
  "description": "威寶 DVD-R AZO 43533 已驗證標定參數：23.9914606277–57.9648936579 mm，共 2,297,888 個磁區；採用三次軌距分布。",
  "totalSectors": 2297888,
  "innerRadiusMm": 23.991460627731737,
  "outerRadiusMm": 57.964893657925366,
  "channelBitLengthNm": 133.3,
  "pitchLinear": -3.9256854319227684E-05,
  "pitchQuadratic": 6.964795915429256E-05,
  "pitchCubic": -4.670918950137523E-05
}
```

DVD 的 `totalSectors` 必須大於 0 且為 16 的倍數，以符合 ECC Block 與串流燒錄配置要求。`channelBitLengthNm` 為保留欄位，詳見下節。

此威寶 DVD 預設來自使用者確認已驗證的 `calibration-f2f851f7.calibration.json`，保留記錄中的完整參數精度。

選用欄位 `contributor` 記錄這組實測參數的貢獻者。填寫後，預設卡片左上角會顯示幼苗圖示，滑鼠停留時立即顯示「貢獻者：…」；省略欄位或留空則不顯示圖示，僅含空白的值會被驗證拒絕。例如內建的 `philips-dvd-plus-r`：

```json
{
  "id": "philips-dvd-plus-r",
  "displayName": "PHILIPS DVD+R（實測）",
  "description": "PHILIPS DVD+R 實測標定參數：24.157724–57.881039 mm，共 2,295,104 個磁區；採用三次軌距分布。",
  "totalSectors": 2295104,
  "innerRadiusMm": 24.157723798864616,
  "outerRadiusMm": 57.881039159257099,
  "channelBitLengthNm": 133.3,
  "pitchLinear": -0.00015487792452410765,
  "pitchQuadratic": 0.0001962455822194293,
  "pitchCubic": -8.6683602888291885E-05,
  "contributor": "DBPolp"
}
```

`pitchLinear`、`pitchQuadratic`、`pitchCubic` 為三次軌距的三個相對形狀係數，預設為 0，以相容舊預設。模型為 `p(x)=p0*(1+pitchLinear*x+pitchQuadratic*x²+pitchCubic*x³)`，`x=(r-innerRadiusMm)/(outerRadiusMm-innerRadiusMm)`。完整多項式有四項；`p0` 根據總軌長自動計算，介面顯示四項的實際 μm 數值。整個 `0≤x≤1` 區間內軌距必須嚴格為正，不能僅檢查內外邊界。修改任一係數會切換至未儲存的自訂狀態，重新選擇內建等軌距預設會恢復零係數。

內建 DVD 實測預設還包括 `verbatim-dvd-plus-r-azo-43498`（威寶 DVD+R AZO (43498)，貢獻者 `Disappear9`）、`ritek-dvd-plus-r-e-series`（錸德 DVD+R E時代系列，貢獻者 `EternalEve`）與 `ritek-dvd-r-e-series`（錸德 DVD-R E時代系列，貢獻者 `bczhc`）。

`verbatim-dvd-plus-r-azo-43498` 的參數為：內半徑 `23.954070288547207` mm、外半徑 `57.981456053451275` mm、`2,295,104` 磁區，三次軌距形狀係數 `-1.1055153059816393E-05`、`-1.7233535436312236E-06`、`5.55123860136712E-06`。`ritek-dvd-r-e-series` 的參數為：內半徑 `24.063109184084034` mm、外半徑 `58.23373375457525` mm、`2,298,496` 磁區，三次軌距形狀係數 `-3.2229430281730407E-06`、`1.0507210681810583E-05`、`-8.562064227346657E-06`。兩者均保留來源數值的完整精度。

## 新增 CD 預設

在 `cdPresets` 陣列中複製一個現有物件，修改 `id`、名稱、磁區數及內外半徑。例如：

```json
{
  "id": "cd-74",
  "displayName": "標準 74min CD",
  "description": "ECMA-130／ISO/IEC 10149 標準速度 1200 mm/s；120 mm 光碟、約 74 分鐘，333,000 磁區（24.5–56.8 mm）。",
  "sectors": 333000,
  "innerRadiusMm": 24.5,
  "outerRadiusMm": 56.8,
  "linearVelocityMmPerSecond": 1200
}
```

## 新增藍光預設

在 `bluRayPresets` 陣列中複製一個現有物件，修改 `id`、名稱及參數。例如：

```json
{
  "id": "bd-r-ritek-10x",
  "displayName": "錸德 BD-R 10x（實測）",
  "description": "實測參數：內半徑 23.988221 mm、外半徑 58.023235 mm、12,219,392 磁區。",
  "totalSectors": 12219392,
  "innerRadiusMm": 23.98822105616562,
  "outerRadiusMm": 58.023234731100764,
  "invertMapping": false
}
```

BD 的 `totalSectors` 必須大於 0、按 32 對齊且不超過單層容量 12,219,392；外半徑不超過 60 mm。`invertMapping` 為預設的 3T/6T 極性，套用至專案後仍可在設計頁色調模組中修改。

內建 `bd-r-verbatim-64099`（威寶 BD-R (64099)）採用使用者檔案 `disc-presets.json` 中已儲存的「Verbatim 64099」預設，保留完整精度：內半徑 `23.987042157339335` mm、外半徑 `57.987960341783236` mm、`12,219,392` 磁區、`invertMapping: false`。GUI 與命令列使用相同參數。

內建 `bd-r-verbatim-64293`（威寶 BD-R (64293)）由貢獻者 `bczhc` 提供，參數與 64099 相同，保留相同精度。

內建 `bd-r-ritek-12x`（錸德 BD-R 12x）由貢獻者 `DeepAQ` 提供：內半徑 `23.98795` mm、外半徑 `58.02236` mm、`12,219,392` 磁區、`invertMapping: false`。GUI 與命令列使用相同參數。

## 固定參數與保留欄位

掃描速度、起始角、影像外半徑及 DVD 通道位元長度為此方案固定的物理量，介面不提供對應表單，程式生成時一律採用 1200 mm/s（ECMA-130）、0°、57.5 mm 與 133.3 nm。

它們在 JSON 中仍保留為欄位，因為舊版設定檔需要這些參數，必須確保舊版寫出的使用者檔案可直接使用。這些保留值不參與生成計算。

## i18n 相容欄位

桌面應用程式優先解析已註冊的 `displayNameResourceKey` 與 `descriptionResourceKey`；資源鍵不存在時，以現有文字作為回復值。未修改的內建名稱與說明也會翻譯，使用者自行填寫的名稱與說明保持原文。應用程式儲存的幾何說明使用 `DiscPreset.UserDescription` 格式鍵，根據預設半徑與磁區數，在顯示時翻譯；舊預設中與自動生成格式完全一致的說明也會被辨識。手動修改說明後保留原文，即使仍保留該格式鍵。使用者預設無需填寫資源鍵，`fallbackLanguage` 仍為選用的來源語言中繼資料，不改變介面語言選擇。 初始使用者預設在光碟類型與 ID 相符且文字未改時，也會在顯示時翻譯預設文案。

## 編輯規則

- 目前頂層為 `"schemaVersion": 4`，包含 `cdPresets`、`dvdPresets`、`bluRayPresets`、三個停用清單與選用 `fallbackLanguage`。程式也接受 `"schemaVersion": 2` 與 `3`；缺少 BD 目錄時由內建 BD 範本補齊，儲存時升級為 4。
- 每組預設中的 `id` 必須唯一；`__manual__` 為介面「未儲存的自訂參數」狀態的保留 ID，不可寫入 JSON。
- 僅可填寫範本中列出的生成參數；其他欄位會被拒絕，以免將影像配置或校正參數混入光碟預設。
- 半徑、`linearVelocityMmPerSecond` 及 `channelBitLengthNm` 必須為大於 0 的有限數值，生成外半徑必須大於生成內半徑。
- 檔案允許 `//` 或 `/* ... */` 註解及陣列末尾逗號。
- 若格式或參數無效，程式會指出問題並繼續保留上一次成功載入的預設，不會自動覆寫使用者檔案。
