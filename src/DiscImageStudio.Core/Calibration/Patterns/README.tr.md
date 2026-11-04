# Sabit kalibrasyon referans görseli

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

`target_slim.png`, kullanıcının belirttiği görselin değiştirilmemiş 2400×2400 RGB kopyasıdır. Dosyanın SHA-256 değeri:

```text
c98ffe87db7cf9c3f132352814e1ce611c44f704de5b7212da4083188257c829
```

Özgün görselin üç renk kanalı her pikselde aynıdır. `target_slim.gray.gz`, görselin ikilileştirilmemiş 8 bit gri tonlu piksellerini satır sırasıyla, GZip ile sıkıştırılmış olarak saklar. Açıldığında 5.760.000 bayttır; SHA-256 değeri:

```text
3d351bdfb817b943c1d1b4aedc97fc4f0a111e7e1537748f70cb5b180ba96852
```

Görsel, normal görsel kodlayıcılarıyla aynı piksel koordinatı kurallarını kullanarak oluşturma dış yarıçapına göre ölçeklenir: DVD piksel merkezi referansı `1199.5`, CD referansı `1200` değeridir. Disk merkezine göre koordinatlar
`((x-centre)*outerRadius/centre, (y-centre)*outerRadius/centre)` mm şeklindedir; Y aşağıya doğrudur. Özgün görseldeki 9 çemberin referans yarıçapları
490, 570, 650, 730, 810, 890, 970, 1050 ve 1130 pikseldir. Çizgi kalınlığı yaklaşık 10 pikseldir; gerçek milimetre boyutları oluşturma dış yarıçapıyla orantılı ölçeklenir.

Kodlama, önizleme ve PNG dışa aktarımı bu görseli paylaşır: önce dış yarıçapa göre ölçeklenir, ardından çizim alanı iç ve dış yarıçaplara göre kırpılır. Önizlemedeki 120 mm disk arka planı, görsel kodlama ölçeğini belirlemez. Görsel ayrıştırılıp yeniden çizilmemiştir. `CalibrationEncoderTests`, özgün PNG özetini, her gri tonlu pikseli, yerel önizleme ve dışa aktarımları, ayrıca CD/DVD dosyalarıyla akışların tutarlılığını denetler.
