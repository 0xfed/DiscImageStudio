# 固定校準參考圖

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

`target_slim.png` 是使用者指定圖片的原樣副本，2400×2400 RGB。檔案 SHA-256：

```text
c98ffe87db7cf9c3f132352814e1ce611c44f704de5b7212da4083188257c829
```

原圖的三個色彩通道逐像素相同。`target_slim.gray.gz` 儲存其未二值化的 8 位元灰階像素，依列排列，以 GZip 壓縮；解壓縮後為 5,760,000 位元組，SHA-256：

```text
3d351bdfb817b943c1d1b4aedc97fc4f0a111e7e1537748f70cb5b180ba96852
```

影像依產生外半徑縮放，採用與一般圖片編碼器相同的像素座標慣例：DVD 的像素中心基準為 `1199.5`，CD 為 `1200`。相對盤心的座標為
`((x-centre)*outerRadius/centre, (y-centre)*outerRadius/centre)` mm，Y 向下。9 條圓的原圖參考半徑為
490、570、650、730、810、890、970、1050、1130 像素；線寬約 10 像素，實際毫米尺寸隨產生外半徑等比例縮放。

編碼、預覽與 PNG 匯出共用這份影像，先依外半徑縮放，再依內外半徑裁切繪圖區。預覽的 120 mm 光碟背景不決定影像編碼比例。圖片未經解析後重繪。`CalibrationEncoderTests` 檢查原始 PNG 雜湊、每個灰階像素、原生預覽與匯出，以及 CD/DVD 檔案和串流的一致性。
