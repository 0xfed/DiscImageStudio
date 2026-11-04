# 黑白遞色工具

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

獨立的 Windows 圖片處理程式，將原圖轉換為僅含 **0、255** 的黑白二階 PNG。DVD 與 CD 都使用這兩階，透過黑白像素的空間混合保留明暗。執行檔名稱維持 `FourLevelDither.exe`，以相容既有入口。

## 視窗操作

1. 執行 `FourLevelDither.exe`，按「開啟圖片」或將圖片拖入視窗。
2. 選擇「DVD 二階」或「CD 二階」，檢視對應的碼字說明；兩種類型都輸出相同黑白調色盤。勾選「100% 像素檢視」可檢查遞色細節。
3. 「sRGB 伽馬校正」預設關閉，以保留既有輸出；勾選後會自動重新處理，視窗會顯示目前狀態。
4. 按「儲存黑白 PNG」，另存處理後的圖片。關閉校正時，預設檔名以 `-黑白抖动.png` 結尾；開啟時以 `-黑白抖动-sRGB.png` 結尾，方便比較。程式拒絕覆寫目前原圖路徑。

切換光碟類型或伽馬選項，都會從快取的原圖重新計算，不會再次處理上次的遞色結果。從先前三階或四階方案改為二階時，請開啟未遞色的原圖重新處理，以保留最完整的明暗資訊。

支援 PNG、JPEG、BMP、TIFF、GIF 等系統圖片解碼器支援的格式。匯出為無損 Gray8 PNG，保留原圖像素尺寸與有效 DPI；無效 DPI 使用 96。預覽縮放不改變儲存的像素尺寸。

## 處理規則與光碟映射

預設使用 Rec.709 權重，在編碼 RGB 空間將色彩轉換為亮度，透明部分先合成至白色背景，再使用**蛇形 Floyd–Steinberg 誤差擴散**：相鄰列交替掃描，為包含擴散誤差的亮度選擇最接近的黑或白，再將亮度差分配至後續像素。

| 主程式二階分級範圍 | PNG / 預覽灰階值 | DVD 擾碼域 Q byte | CD byte |
| --- | ---: | --- | --- |
| 0–127 | 0 | `0xA5` | `0x0D` |
| 128–255 | 255 | `0x92` | `0xAA` |

預設關閉伽馬校正時，原圖灰階 128 的大區域會產生約 `128 / 255 = 50.2%` 的白色像素，其餘為黑色。遞色期間會疊加擴散誤差；無論是否開啟伽馬校正，輸出檔案都只包含 0 和 255。

### sRGB 伽馬校正

開啟後，程式先將各 RGB 通道依標準 sRGB 轉換函數解碼為線性光，再以 Rec.709 權重計算亮度，在線性空間合成白底並擴散誤差。它會改變黑白像素的分配比例，不改變光碟碼字或輸出灰階。

例如原圖灰階 128 解碼後約為 21.6% 的線性亮度，因此產生約 21.6% 的白色像素；同張圖片的中間調通常比關閉時更深。可儲存兩個版本比較實際光碟效果。關閉選項仍依原演算法計算，預設行為不變。

主程式的影調在設計頁右欄統一設定，產生、串流燒錄及設計投影共用所選影調；不必先在獨立工具中匯出 PNG。新專案預設遞色強度 75%、2 階、亮度 0，sRGB 線性化關閉；CD 可選 2/4 階，DVD/BD 使用 2 階。產生幾何在專案設定中編輯。

主程式目前依圖層進行設計合成，不再提供舊版 CD/DVD 環形複製選項。獨立工具只處理單張圖片，不負責專案圖層配置。

處理核心 `DiscImageStudio.Imaging.GrayscaleDither` 仍支援任意有序灰階調色盤。舊 `FourLevelDither` API 僅為既有碼字探針等工具保留相容行為，獨立視窗與 CLI 均使用黑白二階。

## 命令列

視窗與命令列皆支援簡體中文、繁體中文、英文、日文與土耳其文，語言於啟動時決定。使用 `--language zh-CN|zh-TW|en|ja|tr|system` 明確選擇語言；未指定時使用環境變數 `DISC_IMAGE_STUDIO_LANGUAGE`，否則跟隨系統介面語言。語言選擇不改變模式值、檔名或像素輸出。

```powershell
# 無視窗轉換，預設 DVD 二階；輸出必須是 .png，且不能已存在
.\FourLevelDither.exe --input original.jpg --output result.png

# 明確選擇 DVD 或 CD；兩者均輸出 0/255 黑白二階
.\FourLevelDither.exe --input original.jpg --output dvd-result.png --mode dvd
.\FourLevelDither.exe --input original.jpg --output cd-result.png --mode cd

# 比較關閉與開啟 sRGB 伽馬校正；省略 --gamma 等同 none
.\FourLevelDither.exe --input original.jpg --output result-none.png --gamma none
.\FourLevelDither.exe --input original.jpg --output result-srgb.png --gamma srgb

# 載入一張圖片並開啟視窗
.\FourLevelDither.exe original.jpg

# 檢視說明（也支援 --help）
.\FourLevelDither.exe -h

# 以日文開啟視窗
.\FourLevelDither.exe --language ja original.jpg

# 以土耳其文進行無視窗轉換
.\FourLevelDither.exe --language tr --input original.jpg --output result-tr.png
```

## 從原始碼執行與發佈

在儲存庫根目錄使用 .NET 10 SDK：

```powershell
dotnet run --project tools/FourLevelDither --configuration Release

dotnet publish tools/FourLevelDither/FourLevelDither.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output artifacts/FourLevelDither/win-x64
```

自包含發佈的 `FourLevelDither.exe` 內建 .NET 10 執行階段；直接使用非自包含組建時，需要安裝 .NET 10 Windows 桌面執行階段。
