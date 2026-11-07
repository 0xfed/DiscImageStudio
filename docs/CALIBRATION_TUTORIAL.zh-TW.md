# 上游整合後的標定流程

[简体中文](CALIBRATION_TUTORIAL.md) | [繁體中文](CALIBRATION_TUTORIAL.zh-TW.md) | [English](CALIBRATION_TUTORIAL.en.md) | [日本語](CALIBRATION_TUTORIAL.ja.md) | [Türkçe](CALIBRATION_TUTORIAL.tr.md)

輔助標定現在提供粗標、邊界標定和精標三個階段，並可獨立於繪圖專案使用。先確認盤片容量，再為每張空白盤新增對應階段的標靶；每張已燒錄盤都必須使用產生時儲存的記錄和照片。

圓環直徑、寬扇區和 DVD 目視選擇器繼續可用。新記錄明確儲存圖案來源；本分支原有 schema 7–9 無圖案來源欄位的記錄按原始圖案解讀。不要透過手改版本號轉換上游舊記錄。

粗標使用三條寬帶及角度標記，取得下一張盤的半徑初值。邊界標定沿清楚的黑白交界分段取點；精標沿同一條十字臂描點，必要時擬合三次軌距。每輪核對預測曲線後再套用結果，並儲存記錄。

照片校正使用實體盤片外緣和中心孔。12 cm 盤的實體外半徑為 60 mm，不能用燒錄區外半徑替代。每輪使用新的空白盤；重複計算同一張照片不會產生新的實體測量。

## 可開啟的 Verbatim AZO DVD-R 範例

這些記錄包含照片、校正參數和測量點。它們展示一次實盤流程，不能保證其他盤片、燒錄機或批次的精度。

- [AngleSweepBands](examples/verbatim-azo-20261104/calibration-4f0228e6.calibration.json)
- [WavyBoundary](examples/verbatim-azo-20261104/calibration-3f2ce8c2.calibration.json)
- [ReferenceCross](examples/verbatim-azo-20261104/calibration-14580aac.calibration.json)

[完整的上游中文實作教程和歷史數值見此固定版本。](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/blob/c720fd45b76dc4d9257316474dd646a91589ffdb/docs/CALIBRATION_TUTORIAL.md)
