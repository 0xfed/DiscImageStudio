# DVD NRZI 圖像約束求解器

[简体中文](DVD_ENGINE.md) | [繁體中文](DVD_ENGINE.zh-TW.md) | [English](DVD_ENGINE.en.md) | [日本語](DVD_ENGINE.ja.md) | [Türkçe](DVD_ENGINE.tr.md)

本專案將連續 16 個 DVD 資料磁區視為一個 ECC Block，依二維圖片或指定 NRZI pit/land 約束，搜尋可讀取的使用者資料。

固定慣例：

- 單層光碟 Data Zone，Sector Information 為 `0x00`；
- DVD-R Data Frame 的 RSV 為六個 `0x00`；
- 僅輸入 ECC Block 起始 LBA，後續磁區自動使用 `LBA + 1 ... LBA + 15`；
- `PSN = PSN offset + LBA`，預設 offset 為 `0x30000`；
- DSV 絕對值相同時選擇 Stream 1；
- `pit = 0`（黑色），`land = 1`（白色）；
- 每次輸出 32768 位元組，即 16 個連續的 2048-byte payload。

編碼鏈包含 ID、IED、全零 RSV、EDC、資料擾碼、RS-PC PO/PI、PO 列交錯、EFMPlus 主表／替代表、SYNC/DSV 控制與 NRZI 轉換。

獨立[灰階實驗工具](../tools/DvdGrayProbe/README.zh-TW.md)使用 `FastDispersionImageWriter.WriteScrambledCodewordPatternStream`，在相同 payload 碼字中心座標系直接指定擾碼 Q，再寫出 `P = Q XOR mask`。介面不經固定灰階分檔，適合測試 `64/E4` 等候選碼字與離散抖色圖集，不影響一般圖片產生的調色盤。快速產生不重播全碟 ECC/EFM，也不預測光學亮度。迴歸測試對混合碼字執行完整 ECC/EFM 小樣本重播，另驗證實際 payload、幾何、平行一致性與取消。

## 建置與自我檢查

下列命令使用 Windows 獨立入口，需要 .NET 10 SDK；直接執行其非獨立部署產物需要 .NET 10 Windows 桌面執行階段。Linux/macOS 可透過共用 CLI 執行 DVD 命令，見 [README.md](../README.zh-TW.md)：

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
```

自我檢查涵蓋：

- ECMA-267 調變表抽樣；
- ID、IED、RSV 與 EDC；
- 擾碼 XOR 往返；
- 208 列 PI 與 172 欄 PO 的零餘數；
- 619008 個實體 NRZI 電平的數量、確定性與 RLL(2,10) 資料游程；
- 16-bit 碼字在 pit/land 兩種初始電平下的預算灰階、結束電平與 DSV 增量；
- 隨機 SYNC Frame 的碼字重播灰階、SYNC 選擇、最終 State/DSV/電平與逐位元編碼器一致；
- 雙串流 EFMPlus 前綴狀態在無須備用 SYNC 重播時與逐位元編碼器完全一致；
- state-control 最終邊界與完整 DVD 正向編碼一致，求解時不實體化 619008-bit 電平陣列；
- dispersion 模式的 `0x92/0xA5` 擾碼域對應、反擾碼 ISO payload 與最終邊界符合完整編碼；
- 最小 ISO9660 描述元、Path Table、根目錄、payload LBA 與範本替換結果；
- 半徑校準 PNG 預覽與連續多 ECC Block 原始映像輸出；
- 內圈 ISO9660/Joliet 檔案、Unicode 子目錄與外圈繪圖資料的隔離保護。

## 目標檔案

目標檔案以字元表示實體 NRZI 電平：

| 符號 | 意義 |
| --- | --- |
| `0` 或 `P` | pit，黑色 |
| `1` 或 `L` | land，白色 |
| `?` | 不約束 |

忽略空白與底線。檔案必須包含以下其中一種：

- 38688 個符號：約束一個實體磁區，透過 `--target-sector 0..15` 指定其在 ECC Block 中的位置；
- 619008 個符號：約束完整 ECC Block。

SYNC、RLL、EDC 與 ECC 會限制可實現的圖案，因此任意逐位元圖像通常沒有零誤差解。`full` 以不匹配的 NRZI 位元為目標；圖片的 `state-control` 將目標二值化為黑或白，先計算 pit/land 多數方向是否錯誤，再計算實際 land 數到純黑 0 或純白 16 的灰階距離。搜尋目標僅使用可控 payload 碼字，最終可見度則統計全部通道碼字。

`dispersion` 不控制絕對 pit/land 多數，而以 EFMPlus 跳變密度形成黑白二階紋理。擾碼後位元組 `Q=0xA5/0x92` 在所有狀態表示中分別固定為 5/2 次跳變。圖片亮度 0–127 對應 A5、128–255 對應 92，預覽亮度 0/255。新產生圖片不再以 66 或 B9 作獨立灰階。NRZI 整體反色不改變跳變位置。

已抖色的全不透明灰階圖片若僅含 `0/255`（允許全黑或全白），產生與校準預覽採最近像素取樣，保留原始階數；連續灰階、彩色或有透明度圖片仍採雙線性取樣。主程式在設計頁完成縮放、旋轉與圖層合成，產生、串流燒錄與預覽共用該合成圖及 `ToneProfile`，不再額外進行環形複製或前置處理。`full` 與 `state-control` 的二值目標維持原有取樣規則。

## 圖片對應

可直接輸入 PNG、JPEG、BMP、TIFF 或 GIF：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-output solved-disc.iso `
  --output payload.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --start-angle-deg 0 `
  --spiral-direction ccw `
  --image-threshold 128 `
  --alpha-threshold 1 `
  --iterations 10000 `
  --constraint-output mapped-target.txt `
  --channel-output channel.bin
```

`--total-sectors` 是光碟回報的 2048-byte 資料區塊總數，也接受別名 `--total-blocks`。它與每磁區 38688 通道位元及 `--channel-bit-nm` 共同決定總軌道長度，再依內外半徑與軌距曲線反解軌距尺度。

### 隨半徑變化的三次軌距

DVD 產生、串流輸出、圖片預覽、ISO 讀回與輔助校準共用 `DvdTrackGeometry`。設正規化半徑 `x=(r-ri)/(ro-ri)`，局部軌距為：

```text
p(x) = a0 + a1*x + a2*x² + a3*x³                 [mm]
     = p0 * (1 + c1*x + c2*x² + c3*x³)
dr/dθ = p(x)/(2π)
```

完整三次多項式有四個係數。輸入的 `--pitch-linear`、`--pitch-quadratic`、`--pitch-cubic` 為三個無因次相對係數 `c1,c2,c3`；常數項 `a0=p0` 由固定總軌長自動求解，其餘三項為 `ai=p0*ci`。因此四項須滿足容量與內外半徑的長度約束，而非全部獨立。介面以 μm 顯示完整四項，變數為 `x`，並非以 mm 計的絕對半徑。

軌長使用 `L=∫sqrt(1+(2πr/p(r))²)dr`，角度使用 `θ(r)=∫2π/p(r)dr`；變動軌距不可再以恆定螺距的 `(r-ri)/b` 算角度。程式驗證整個產生環帶內軌距嚴格為正，包含多項式內部極值。三項皆零時使用原恆定軌距模型；舊參數與預設省略欄位時預設為零。

例如在原 `solve` 命令後加入 `--pitch-linear 0.02 --pitch-quadratic -0.03 --pitch-cubic 0.015`。這僅示範參數格式，並非任何光碟實測值。`calibrate` 以相同選項指定產生曲線，另以 `--actual-pitch-linear`、`--actual-pitch-quadratic`、`--actual-pitch-cubic` 指定實際曲線，也接受 `--generated-pitch-*` 別名。`simulate` 的 `--pitch-*` 指實際光碟曲線。

映像與預覽 JSON 記錄相對係數及基礎軌距；校準工作階段儲存相對係數、內外半徑、磁區數及位元長度，可重建完整四項。相片整體轉角仍是獨立擬合量。

座標與取樣慣例：

- 圖片中心對應碟心，短邊對應外徑，長邊多餘區域保留；
- 軌道在內半徑由 0° 開始，0° 指向圖片右方；預設依數學方向逆時針，可用 `--spiral-direction cw` 改為順時針；
- 圖片黑色對應 pit、白色對應 land；灰階低於 `--image-threshold` 視為黑色；
- alpha 低於 `--alpha-threshold` 的透明區域不施加約束；
- `--lba` 從軌道起始資料區塊由零計數，僅對應由該 LBA 開始的連續 16 個磁區；
- 選用 `--constraint-output` 匯出實際產生的 619008-symbol 線性約束以供檢查。

## 求解

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --target target.txt `
  --output payload.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land `
  --target-sector 0 `
  --iterations 10000 `
  --mutation-bytes 4 `
  --seed 1 `
  --channel-output channel.bin
```

重要參數：

- `--lba`：ECC Block 起始 LBA，並非 16 個 LBA；
- `--psn-offset`：十進位或 `0x` 十六進位，預設 `0x30000`；
- `--initial-state`：EFMPlus 狀態 1 至 4；
- `--initial-dsv`：進入目前 ECC Block 前的累積 DSV；
- `--initial-level`：進入目前區塊前的實體狀態 `pit` 或 `land`；
- `--seed-payload`：選用 32768-byte 初始解；未提供時產生確定性隨機初始解；
- `--initial zero`：改用全零初始 payload；
- `--channel-output`：額外輸出最終 NRZI 電平，每 8 個電平依 MSB-first 打包。

`--target` 與 `--image` 二擇一；圖片模式另須提供 `--total-sectors` 或 `--total-blocks`。

### State-control 碼字灰階求解模式

原本不限制 payload 位置的演算法保留為 `--algorithm full`，亦接受 `exact`。舊空間控制位元組池演算法已移除；新模式以下列參數啟用：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2297888 `
  --output payload.bin `
  --algorithm state-control `
  --bridge-bytes 2 `
  --beam-width 8 `
  --retry-beam-width 32 `
  --retry-frame-fraction 0.25 `
  --batch-frames 8 `
  --batch-candidates 4 `
  --search-lanes 1 `
  --iterations 3
```

state-control 將每個 91-byte Sync Frame 作為一個求解區段。EDC、PI、PO、固定欄位與 SYNC 仍精確產生，參與 State、DSV 與 pit/land 邊界傳播；不參與自由位元組搜尋目標，但參與最終整碟可見度評分。程式依 `EFMPlus state × initialLevel × currentDsv`（EFMPlus 狀態、輸入電平、目前累積 DSV）計算最佳黑／白位元組對。目標位置對每條作用中 Stream 僅嘗試目前精確狀態對應的黑或白位元組，每個前綴僅保留最佳 `--beam-width` 條路徑。含圖像目標的自由區段另將首尾各 `--bridge-bytes` 個 payload 位元組視為控制位置，但仍僅從目前 DSV 位元組對選擇，不再列舉全部 256 個值。

程式啟動時預先處理全部 65536 個 16-bit 碼字在初始 pit 與 land 時的：

- 16 個 NRZI 位置中的 land 數，即 0 至 16 的 17 階灰度；
- 碼字結束後的 pit/land；
- 精確 DSV 增量；
- 用於 RLL 邊界檢查的前導與尾隨零數。

位元組對針對每種可能 EFMPlus 表示，同時計算灰階誤差與寫入後 `|DSV|`；聯合成本為 `worstGrayError * 16 + worstOutputAbsDsv`，即最壞灰階誤差乘以 16，加上最壞輸出 DSV 絕對值。因此一個灰階仍重於 15 個 DSV 單位，而目前 DSV 方向與大小可改變相近灰階位元組間的選擇。常用 DSV -256..256 的位元組對會預算，超出範圍則依精確 DSV 即時計算，不截斷。此處指擾碼後進入 EFMPlus 的 recording-frame 位元組；ISO 原始 payload 透過擾碼 XOR 反向對應，因此不一定僅含這些數值。

內層候選以 EFMPlus 碼字評分，不展開 16 個通道電平。每條 beam 路徑同時保留 Stream 1 與 Stream 2，不提前錯誤捨棄其中之一。SYNC 主／副碼、Stream 1 平手選擇、累積 DSV 與最終邊界仍依原編碼器整數規則逐碼字精確重播；每個 Sync Frame 的存活序列再以完整 91 位元組字級編碼器重算，以處理 DSV 越界時的備用 SYNC。最終僅做碼字級多數、灰階與邊界複核；僅明確要求 `--channel-output` 才展開逐位元資料。

state-control 搜尋採嚴格字典序：先最小化自由 payload 的 `controlled-majority-error`，多數判錯數相同才比較 `controlled-gray-error`，仍相同才比較邊界與 DSV。主控台與 JSON 的主要 `majority-error`/`score` 表示全部最終通道碼字可見誤差：每個 Sync Frame 含 2 個 SYNC 與 91 個資料碼字，不透明圖片的一個 ECC Block 分母固定為 `416 × 93 = 38688`。主要 `gray-error` 同樣涵蓋這 38688 個碼字。自由位元組搜尋分數另存為 `controlledScore` 與 `controlledGrayScore`，所有誤差皆越低越好。

每個外層 pass 先以目前完整 ECC Block 精確計算各 Sync Frame 的 controlled majority/gray 誤差，再選出最差的 `--retry-frame-fraction`（預設 0.25）部分。先將這些 frame 作跨區塊 warm-start 候選，以快速離開隨機初始解；無論局部 EFM 評分多好，都須更新 EDC、PO、PI 並完成整塊精確重播才接受。候選 ECC 更新重用目前 recording frames，僅重建 payload 改變的磁區、受影響 PO 欄與 PI 列；自我檢查逐字確認與完整 ECC 重建一致。接著按 `--batch-frames`（預設 8）組小批次，各組平行產生與複核 `--batch-candidates`（預設 4）個不同 EFM 序列。僅整塊 controlled 評分嚴格改善才接受。批次失敗便拆為更小組，直到單 frame；單 frame 失敗可用 `--retry-beam-width`（預設 32）重試。一次拆分僅沿第一條成功分支繼續，避免窮舉整棵樹。一個 pass 無候選接受即提前停止。`--retry-frame-fraction 0` 表示每 pass 處理全部錯誤 frame；`--retry-beam-width 0` 關閉單 frame 寬束重試。

`--beam-width`、`--retry-beam-width`、`--batch-candidates` 越大，越容易找到 ECC 回饋後仍有效的候選，但時間大致隨候選複核次數增加。`--batch-frames` 越大，單次局部改善更強，也更易因 PO/PI 回饋遭整塊驗收拒絕；較小則穩定但須更多次完整重建。`--iterations` 或範圍模式的 `--iterations-per-block` 為閉環 pass 數，預設 3。相容別名 `--algorithm control` 仍可用；舊 `--frame-candidates`、`--parallel-candidates`、`--parallelism`、`--word-candidates`、`--control-bytes` 皆拒絕。`--mutation-bytes` 僅屬 `full`。

`--search-lanes 1..8` 用以降低局部極小值造成的接近 50% 誤差。lane 1 使用傳入初始 payload 並嚴格單調驗收；其餘 lane 由 `--seed` 衍生確定性獨立 payload 重新開始，允許依 `--initial-temperature` 至 `--final-temperature` 的退火溫度暫時接受較差的整塊精確分數，以跨越 PO/PI 障礙。各 lane 另存完整 ECC 精確重播後的歷史最佳，全部平行執行，最後依 controlled majority、gray、最終 `|DSV|` 選優，因此探索不會使最終輸出劣於 lane 1。預設 1 無額外成本。品質優先可先在少量磁區測試 `--search-lanes 4 --iterations-per-block 10`；獨立重啟會改變無圖像約束的 payload，須保留指定內容時應維持 1。

`full/exact` 指不限制可變 payload 位置，並非窮舉 `256^32768` 或保證全域最佳。圖片先以 `--image-threshold` 逐像素二值化，再由 state-control 對各 16-bit payload 碼字內樣本作黑白多數投票；目標僅純黑 0 或純白 16，8:8 平手由碼字中心樣本決定。求解先最小化多數方向判錯數，再以 land 數到 0/16 的距離作灰階誤差，不逐一匹配 16 個位置的位元圖案；需要逐通道位元位置完全對應時使用 `full`。

### Dispersion 單位元組紋理模式

`--algorithm dispersion`（別名 `byte-pool`）直接在擾碼域產生 payload，不執行隨機搜尋：

| 圖片亮度 | 擾碼後位元組 Q | 固定跳變數 | 名義預覽亮度 |
| --- | --- | --- | --- |
| 0..127 | `0xA5` | 5 | 0 |
| 128..255 | `0x92` | 2 | 255 |

ISO payload 依 `P = Q XOR sector scrambler byte` 計算。

每個 payload 碼字依其 16 個通道位元的碟面中心取樣，再按上述固定區間分檔，不受 `--constraint-step` 或 `--image-threshold` 影響；`--image-threshold` 僅控制 `full/state-control` 二值目標。ID、IED、全零 RSV、EDC、PI、PO、SYNC 仍依標準產生；不展開 619008 個 NRZI 位元，但以完整 recording frames 逐碼字重播，精確傳遞最終 EFMPlus state、DSV、pit/land 至下一 ECC Block。

#### 影調抖色

兩種紋理僅有黑白，但圖片中間調可在每個 payload 位元組按比例混合：

```powershell
--tone-dither 1 --tone-levels 2 --tone-brightness 0
```

- `--tone-dither 0..1`：0 保留原 `--image-threshold` 硬閾值；1 按位元組抖動閾值，以黑白碼字空間比例還原灰階。
- `--tone-levels`：DVD 僅兩種紋理，只接受 2。
- `--tone-brightness -64..64`：整體平移灰階。

抖色相位由邏輯 payload 位元組索引決定，與預覽取樣無關；`simulate` 讀取 ISO 僅依 `0xA5/0x92` 分類，讀回自然反映抖色結果。省略這些參數時，輸出與舊版逐位元組一致。

僅需快速產生可燒錄映像、不需最終 EFMPlus 邊界時，可加 `--fast-output true`。快速輸出仍在相同 payload 碼字中心取樣，產生與一般 dispersion 相同的 2048-byte 磁區；改用預算 32 KiB 擾碼遮罩從 `Q` 直接反推 ISO 位元組 `P`，不建構 Data Frame、EDC、PI、PO、Recording Frame，也不重播 EFMPlus、DSV 或 pit/land。JSON 的 `exactEccEfmReplay` 為 `false`，`finalBoundary` 為 `null`。這不省略燒錄所需資料，燒錄機仍由 2048-byte 磁區自行產生衍生欄位。

```powershell
& ".\src\DvdImageSolver\bin\Release\net10.0-windows\DvdImageSolver.exe" solve `
  --image target.png `
  --total-sectors 2297888 `
  --iso-output "R:\full-disc-fast.iso" `
  --fill-sectors 2297888 `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --algorithm dispersion `
  --fast-output true `
  --fast-parallelism 0 `
  --initial zero
```

`--fast-parallelism 0` 依邏輯處理器數量平行產生獨立 ECC Block payload，再按 LBA 順序寫入；可設 `1..64` 限制 CPU 使用。平行不改變位元組選擇或輸出順序。

快速檔案與串流輸出在有硬體加速時預設啟用四點雙精確度 SIMD 取樣。各組仍取四個實際 payload 碼字中心，不將跨 SYNC/ECC 位置視為連續等距樣本。三次軌距同段求逆共用查段，向量化原有四次 Newton/Hermite 計算；跨段與邊界回退純量幾何。全不透明黑白圖使用向量正餘弦，接近半像素或圖片邊緣時以原純量路徑重算；一般灰階、彩色與透明圖保留純量正餘弦及內插，僅加速三次軌距幾何。恆定軌距、解析圖案回呼與無 SIMD 機器維持純量取樣。恆定軌距四點路徑在目前實測有性能退步，暫不啟用。全程保留 `double`，不用單精確度座標或近似螺旋公式。

呼叫產生 API 時可設 `MultiBlockSolveOptions.UseSimdSampling = false` 使用純量參考路徑，以比較效能與輸出；預設 `true`，無須改 GUI 或命令列。迴歸涵蓋兩路徑 payload／計數一致、三次軌距分段、半像素邊界、透明度閾值、順逆時針及平行輸出順序。吞吐應以相同輸入與 LBA 範圍的實際 payload 位元組計時，單獨三角函數加速比不等於整碟加速比。

恆定軌距快速求逆會快取求解後固定的內半徑積分值、半徑平方與軌距係數，並在一次 Newton 修正重用相同平方根。保留原 `double` 運算順序、除法與邊界，不以近似積分或乘倒數代除法。獨立舊公式迴歸逐位元比較半徑、角度、短螺旋邊界；效能比較須使用優化前後獨立引擎，因 `UseSimdSampling` 兩側皆使用此純量優化。

`payload-transition-class-error=0` 表示全部可控 payload 正確分配至二階 `0xA5/0x92` 類別，不表示固定欄位、ECC、SYNC 有相同跳變密度。輸出 JSON 的 `dispersionPool.levels` 記錄兩檔輸入區間、Q 位元組、跳變數與名義預覽亮度；預覽值不等於實碟反射率。

輸出包含：

- `payload.bin`：16 個 2048-byte 磁區順序串接；
- `payload.bin.json`：PSN、初始／結束邊界、誤差、迭代次數及執行時間；
- 選用 `channel.bin`：77376-byte 打包 NRZI 電平。

## 輸出可燒錄 ISO

`solve` 可僅輸出 ISO，或同時保留 32768-byte payload：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-output solved-disc.iso `
  --lba 0 `
  --iterations 10000
```

圖片模式直接以 `--total-sectors` 作 ISO 邏輯磁區數，文字約束另須 `--iso-sectors`：

```powershell
--target target.txt --iso-output solved-disc.iso --iso-sectors 2295104
```

新映像含最小 ISO9660 檔案系統與 `SOLVED.BIN`，求解的 16 個 2048-byte 磁區寫至 `--lba ... --lba+15`。LBA 為 0 時位於 ISO9660 system area，故檔案系統資料區另放可見的 `SOLVED.BIN` 副本。NTFS 支援稀疏檔案時盡量使用稀疏儲存；邏輯長度仍為 `total-sectors × 2048`，燒錄軟體讀到的空洞全為零。

亦可基於既有 ISO 替換連續 16 個磁區：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-template original.iso `
  --iso-output solved-disc.iso `
  --lba 32 `
  --iterations 10000
```

範本大小須為 2048 整數倍，圖片模式還須磁區數等於 `--total-sectors`。替換已配置給目錄、檔案或 UDF/ISO 中繼資料的磁區會破壞原檔案系統，因此應選保留區或目標檔案已佔用的 extent。新建最小 ISO 拒絕覆蓋固定 ISO9660 volume descriptor 磁區 16、17。

ISO 儲存 2048-byte 使用者資料，並非 619008-bit 原始通道串流。標準 DVD 燒錄機重建 ID、EDC、ECC、EFMPlus、NRZI，因此實際圖案取決於光碟機是否採相同編碼選擇，以及傳入求解器的初始 state、DSV、pit/land 是否符合實際燒錄邊界。

### 連續鋪滿光碟或指定磁區數

`--fill-sectors` 大於 16 時，由 `--lba` 逐一求解連續 ECC Block，將每塊最終 state、DSV、pit/land 傳給下一塊。磁區數須為 16 倍數；容量 2297888 磁區的整碟命令為：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2297888 `
  --iso-output full-disc.iso `
  --fill-sectors 2297888 `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --constraint-step 16 `
  --algorithm state-control `
  --bridge-bytes 2 `
  --beam-width 8 `
  --retry-beam-width 32 `
  --retry-frame-fraction 0.25 `
  --batch-frames 8 `
  --batch-candidates 4 `
  --search-lanes 1 `
  --iterations-per-block 3 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land
```

僅做前 1600 磁區時改為：

```powershell
--fill-sectors 1600
```

輸出包含 LBA 0 至 1599 共 1600 磁區；`--total-sectors` 仍為整碟容量，用來算軌道幾何。`--lba` 非零時保留前方全零磁區，映像長度為 `(lba + fill-sectors) × 2048`。

範圍模式覆蓋全部指定磁區，含 ISO9660 描述元；輸出是可直接燒錄的原始 2048-byte/sector 映像，而非可掛載 ISO9660 檔案系統。`--constraint-step` 是自由 payload 搜尋的圖片取樣間隔；state-control 對同一 payload 碼字內二值樣本多數投票。最終可見度不受稀疏步長限制，依每個 16-bit 通道碼字中心重取樣，含衍生、固定、SYNC 碼字。二值圖若要增加明暗差，建議 `--constraint-step 16`，每 payload 碼字皆有目標；`32` 更快但密度減半，`256` 僅約束約 1/16 payload 碼字，整體 majority error 常接近隨機。整碟有 143618 ECC Blocks，正式執行前先以 160–1600 磁區測速度與效果。

dispersion 固定在每 payload 碼字中心取樣，不採 `--constraint-step` 稀疏搜尋意義，可省略該參數。

## 檔案與圖案混合模式

`--data-dir` 啟用混合模式，遞迴寫出目錄及子目錄為標準 ISO9660 + Joliet。`--file-position inner`（預設）將檔案放內圈、圖案在外；`--file-position outer` 將檔案內容置碟尾，圖案在前端中繼資料之後、檔案之前。圖案起訖依 16-sector ECC Block 對齊，始終保留全碟幾何，不將剩餘繪圖區拉伸至整碟。最終 ISO 可在 Windows 等系統當一般資料碟開啟，Joliet 保留中文檔名。

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --data-dir ".\disc-files" `
  --file-position outer `
  --iso-output hybrid-disc.iso `
  --total-sectors 2297888 `
  --volume-label MY_DISC `
  --inner-radius-mm 24 `
  --outer-radius-mm 58
```

混合模式預設 `--algorithm dispersion --fast-output true`，適合直接產生整個外圈。明確加 `--fast-output false` 時先編碼重播內圈所有區塊，取得進入外圈的 EFMPlus state、DSV、pit/land，再依精確邊界繪圖；內圈資料越多，精確重播越耗時。

GUI 設計頁「隨碟檔案」可選資料夾及內圈／外圈位置，與匯出頁共用設定；隨專案儲存，也用於串流燒錄。串流固定快速 dispersion：掃描目錄並固定配置，依碟面順序輸出中繼資料、檔案、圖案，不建完整暫存 ISO；外圈模式先圖案後檔案。配置後來源檔大小改變便拒絕續寫。預覽以黃色標出檔案系統佔區，顏色不進寫碟資料。

不接受 `--lba` 或 `--fill-sectors`，由配置與容量自動計算。`--drawing-start-lba N` 可在預設起點後多留空，但須位於可繪區，並與 `--psn-offset` 一同對齊 16 磁區。`--file-position outer` 必須搭配 `--data-dir`。

ISO9660 寫入器支援遞迴目錄與 Joliet Unicode 名稱，單檔不得超過 4 GiB；為避免連結環與誤引目錄外資料，`--data-dir` 不接受重新剖析點／符號連結。

## 半徑校準預覽

校準借鑑 CD 正向投影：依全域通道位置順序取樣，先在「產生時假設螺旋」讀目標，再將同一位置投至「猜測實際螺旋」。多個軌道樣本落同一像素時取平均，保留軌距差累積的真實相位，也避免逐輸出像素反求最近軌道造成氣泡摩爾紋。

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- calibrate `
  --image picture.png `
  --output calibration-preview.png `
  --total-sectors 2297888 `
  --generated-inner-radius-mm 24 `
  --generated-outer-radius-mm 58 `
  --actual-inner-radius-mm 24.2 `
  --actual-outer-radius-mm 57.8 `
  --channel-bit-nm 133.3 `
  --spiral-direction ccw `
  --image-threshold 128 `
  --alpha-threshold 1 `
  --preview-size 2048 `
  --samples-per-sector 16
```

`--samples-per-sector` 控制正向投影密度，範圍 1–4096、預設 16。DVD 每磁區約 5.16 mm 軌長，16 約每 0.32 mm 一次取樣；增加至 32 或 64 可減空像素、增細節，時間近線性增加。不提供任意旋轉偏移；產生與實際螺旋共用 0° 起點，整體旋轉不影響半徑校準。另輸出 `calibration-preview.png.json`，`mappingMode` 為 `forward-channel-splat-average`，記錄密度、兩組螺距、總圈數、軌長；不含求解器剩餘誤差或燒錄機實作差異。

預設預覽整碟；僅產生部分映像時可給與求解相同的 `--lba`、`--fill-sectors`，未寫區保持透明。

## 已產生 ISO 的讀回模擬

`calibrate` 輸入來源圖片，呈現「依產生幾何寫入、依實測幾何讀回」理想效果；使用與產生相同的二階分界 128，`--image-threshold` 僅保留相容，不改預覽。`simulate` 讀實際 ISO，把取樣 payload 經 DVD 擾碼還原後歸類為 `0xA5/0x92`，顯示 0/255，再用實測半徑螺旋將碼字中心投至碟面。舊多階映像仍將 `0x66` 顯為 0、`0xB9` 顯為 85；新產生不使用這些值。這與 cdimage 校準對話框同構：產生幾何已固定於 ISO，模擬僅需實測幾何。

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- simulate `
  --iso drawing.iso `
  --output readback-preview.png `
  --inner-radius-mm 24.2 `
  --outer-radius-mm 57.8 `
  --preview-size 2048 `
  --samples-per-sector 16
```

省略 `--total-sectors`、`--lba`、`--fill-sectors` 時讀求解產生的 `drawing.iso.json`（繪圖起始 LBA 與磁區數）；無附屬檔案則由 LBA 0 讀整個 ISO。`--samples-per-sector` 為每磁區取樣 payload 位元組數（1–2048，預設 16）。擾碼後非二階碼表或舊 66/B9 的位元組略過。檔案系統前綴可能偶然命中碼表，應用附屬檔繪圖範圍排除；ISO 本身不含 PI/PO 更正碼。輸出旁寫 `readback-preview.png.json`，`mappingMode` 為 `payload-scramble-classify-splat`，記錄分類命中數、螺距、總圈數。

取樣在碼字中心：產生端 `FastDispersionImageWriter` 依 `PayloadDirectChannelOffset + 8`（16 位元碼字一半）放置目標，讀回使用同位，兩方向不相差 8 通道位元（約 1 µm 軌長）。兩者還共用螺旋、方向、擾碼遮罩，故產生與預覽參數一致時，逐樣本重現產生圖。

預覽切換 ISO 時讀附屬檔 `imageMapping.totalSectors`、`innerRadiusMm`、`outerRadiusMm` 載入校準幾何，避免外部映像沿用上次尺寸壓成細環；後續微調以載入參數起算。

## 預覽工作區的兩種模式

預覽輸入開關提供兩條路徑，共用防抖、取消、匯出動作：

- **成品讀回**（讀回模擬，`simulate`）：輸入已產生 ISO，僅一組實測幾何，顯示映像實際寫下的位元組。
- **設計投影**（校準預覽，預設，`calibrate`）：輸入來源圖片與產生／實測幾何。不讀 ISO，改參數後更快更新；畫面僅來源圖在實測幾何下的投影，不代表實際碼字，無求解器誤差與檔案系統前綴。

校準預覽直接用設計合成圖，與產生映像來源相同，故兩側圖案一致。

## 僅執行正向編碼

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- encode `
  --input payload.bin `
  --output channel.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land
```

首個 PSN 須為 16 倍數；否則輸入未由 ECC Block 邊界起始，程式拒絕編碼。

## 重新產生調變表

`Encoding/EfmPlusTables.Generated.cs` 由 ECMA-267 Annex G 機械擷取產生。由規範 PDF 重新產生：

```powershell
python tools/generate_efm_tables.py ECMA-267.pdf src/DvdImageSolver/Encoding/EfmPlusTables.Generated.cs
```
