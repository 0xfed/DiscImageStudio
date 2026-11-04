# 藍光位元組與位元週期密度分析

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

分析單層 BD-R 的數位編碼鏈，篩選用於實際光碟明暗映射測試的輸入模式。結果不定義黑白極性，也不產生整片光碟映像檔或執行燒錄。詳細結論見[分析報告](../../docs/BLURAY_BYTE_DENSITY.zh-TW.md)。

需要 .NET 10 SDK；獨立標準表分析只需 Python 標準函式庫，繪圖另需 matplotlib / numpy。在儲存庫根目錄執行：

C# 探針入口的說明、進度、錯誤與說明性 JSON 文案支援五種語言。使用 `--language zh-CN|zh-TW|en|ja|tr|system` 明確選擇；否則優先使用環境變數 `DISC_IMAGE_STUDIO_LANGUAGE`，再跟隨系統介面語言。JSON/CSV 識別碼、數值結果、位元序列與檔名不參與翻譯。

Python 參考搜尋與繪圖工具也支援相同的 `--language` 選項及環境偏好。說明、錯誤、進度、圖表標註、生成的 Markdown 及說明性 JSON/CSV 文案會翻譯；欄位名稱、欄名、模式識別碼與數值資料保留原樣。圖表優先使用可用的系統語言字型，並以倉庫隨附的 HarmonyOS 字型補足 CJK 字形。

```powershell
dotnet run --project tools/BluRayByteProbe --configuration Release -- --language ja --help
dotnet run --project tools/BluRayByteProbe --configuration Release -- artifacts/bluray-byte-density 4
dotnet run --project tools/BluRayByteProbe --configuration Release -- --fine artifacts/bluray-byte-density

python tools/BluRayByteProbe/reference/analyze.py --output-dir artifacts/bluray-byte-density/reference
python tools/BluRayByteProbe/reference/pattern_search.py --output-dir artifacts/bluray-byte-density/reference
python tools/BluRayByteProbe/reference/robust_pattern_search.py --output-dir artifacts/bluray-byte-density/reference

dotnet run --project tools/BluRayByteProbe --configuration Release -- --verify-reference artifacts/bluray-byte-density/reference/independent-finite-vectors.json
dotnet run --project tools/BluRayByteProbe --configuration Release -- --verify-patterns artifacts/bluray-byte-density/reference/independent-pattern-vectors.json
python tools/BluRayByteProbe/plot_results.py artifacts/bluray-byte-density
```

預設命令掃描全部 256 種重複 Q 位元組，包含純 17PP、完整叢集的四種 DC 策略、候選的四個 PSN、未抵消擾碼的 host 對照、隨機鄰居與混合模式。第二個參數為平行度（1–16）。`--fine` 掃描五種週期的全部相位、四種配置，並複核跨位址結果；此模式的平行度固定上限為 4。

Q 指**擾碼後的目標使用者資料**。實際 host payload 寫入 `Q XOR PSN mask`。所有輸出均位於指定目錄，允許覆寫先前同名的分析檔案。

主要輸出：

| 檔案 | 內容 |
|---|---|
| `all-bytes.csv` / `.json` / `.md` | 全部 256 位元組；密度、DC 敏感度與游程分布 |
| `all-bytes-density.png` | 16 × 16 密度圖 |
| `fine-patterns.json` / `.csv` | 176 個位元字串／配置／相位組合與位址複核 |
| `fine-pattern-comparison.png` | 位元組基準與位元級控制比較 |
| `recommended-patterns.csv` | 三種建議位元字串的完整構造參數與密度 |
| `context.json` / `periodic-context.json` | 排除 ECC 的合成框架、鄰居與 DC 邊界試驗 |
| `reference/` | 獨立標準表、精確循環與搜尋結果 |
| `method.json` / `validation.json` / `fine-validation.json` | 統計定義與驗證記錄 |

完整叢集統計的分母固定為 `496 × 1932 = 958272` 通道位元，包含同步、EDC、LDC/BIS、DC 與 17PP；不含 run-in/out/Guard3，但保留 run-in 的初始電平與 DSV。自動 DC 使用專案現有的貪婪**診斷**演算法，將未來位元暫設為零；不能保證與燒錄機的 DC 決策相同。全 0、全 1、隨機 DC 僅用於敏感度比較，不能視為實際燒錄設定。

`FinePatternAnalysis` 對每個可控 payload bit 計算其實體 ECC 位置，再填入週期位元值；驗證實際編碼後的 65,536 個 Q 位元組與目標逐位元組一致。EDC、BIS、同位檢查與 DC 均由編碼鏈產生，並未假定它們是可直接寫入的使用者位元。
