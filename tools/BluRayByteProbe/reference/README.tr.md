# Bağımsız 17PP referansı

[简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [English](README.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

Kullanıcının sağladığı ISO/IEC 30190:2021 standardının §13.17 bölümü ve Şekil 41'den aktarılmış, Python standart kitaplığı araçlarıdır. Bu uygulama, üretimde kullanılan C# kodlayıcısını içe aktarmaz. Sonlu referans, dört baytlık yinelenen 256 girdinin tamamı ve 298 sonlu bit deseni vektörü için C# ile karşılaştırılmış; tümü eşleşmiştir. Beklenen vektörler çıktı dosyalarına dahildir.

Depo kökünden çalıştırın:

```powershell
python tools/BluRayByteProbe/reference/analyze.py --output-dir artifacts/bluray-byte-density/reference
python tools/BluRayByteProbe/reference/pattern_search.py --output-dir artifacts/bluray-byte-density/reference
python tools/BluRayByteProbe/reference/robust_pattern_search.py --output-dir artifacts/bluray-byte-density/reference
```

`--output-dir` varsayılan olarak geçerli dizini kullanır. Her iki desen araması da `--max-period` seçeneğini kabul eder (varsayılan 16). Harici Python paketleri gerekmez.

Üç Python aracının yardım, ilerleme, hata ve JSON açıklama metinleri beş dili destekler; `--language zh-CN|zh-TW|en|ja|tr|system` ve `DISC_IMAGE_STUDIO_LANGUAGE` kullanılır. Açık seçenek önceliklidir; belirtilmezse sistem arayüz dili kullanılır. Dil seçimi alan adlarını, CSV sütunlarını, kesin oranları, bit dizilerini ve sayısal ayrıştırmayı korur. Referans algoritması C# kodlayıcısından bağımsız kalır.

```powershell
python tools/BluRayByteProbe/reference/analyze.py --language ja --help
```

## Ölçüm alanı

Bunlar, açıkça modellenen herhangi bir DC eklemesinden önce, **17PP girişindeki karıştırma sonrası girdilerdir**. Ham ana makine yük desenleri değildir. Yoğunluk, modülasyon 1 bitlerinin sayısının kanal biti sayısına oranıdır: her modülasyon 1 biti NRZI kayıt seviyesini değiştirir. *Modülasyon bitleri arasındaki* değişimleri saymak farklı bir niceliği ölçer.

`analyze.py`, önceki modülasyon bitinin her iki değerinden başlayarak her yinelenen bayt ve sekiz girdi biti fazının tamamı için tam yinelenen döngüleri hesaplar. Bayt sonuçlarını JSON/CSV olarak ve dört baytlık sonlu vektörleri kaydeder. Temel ölçüm EDC, RS eşliği, LDC serpiştirmesi, BIS, eşzamanlama ve DC bitlerini dışarıda bırakır. Sabit DC sonuçlarında her 45 kaynak bitinden sonra süresiz olarak bir kontrol biti eklenir; bunlar duyarlılık tanılamalarıdır, geçerli tam kayıt çerçeveleri veya DC dengeli çıktılar değildir.

`pattern_search.py`, önceki modülasyon bitinin her iki olasılığını kullanarak uzunluğu 2 ile 16 arasındaki 131.068 ikili dizenin tamamını açıkça değerlendirir (262.136 tam döngü değerlendirmesi). Yoğunluğun iki girdi bitiyle döndürme ve önceki bit seçimi altında değişmediğini doğrular, ardından eşleşen 14.694 döndürme sınıfını gruplar. Tek uzunluklu periyotların bir döndürme yörüngesi vardır; çift uzunluklu periyotlar ayrı tek/çift hizalama sınıflarını korur. Uzunluk başına uç değerler raporlanırken eşdeğer desenler ve daha kısa desenlerin tekrarları korunur; dolayısıyla bunlar periyot uzunluklarıdır, mutlaka temel periyotlar değildir.

`robust_pattern_search.py`, tüm bit döndürmelerini gruplar ve her iki bit çifti fazını karşılaştırır. **Aranan periyot uzunlukları içinde**, faza dayanıklı en iyi minimumu 1/6, maksimumu 4/9'dur. Bu iki dayanıklı sonucun daha uzun periyotlar için genel sınırlar olduğu iddia edilmez.

NRZI seviye 1 doluluk oranı, başlangıç seviyesi sıfır alınarak hesaplanır; başlangıç seviyesi ters çevrildiğinde doluluk oranı tümleyenine dönüşür. %50 görev oranı, optik sonucun siyah mı beyaz mı olacağını öngörmez.

## Erişilebilir kanal yoğunluğu sınırları

Keyfi ölçüde uzun bir 17PP modülasyonlu bir veri bölümünde her geçişler arası dizi en fazla 8T olduğundan geçiş yoğunluğu en az 1/8'dir. 16 bitlik girdi döngüsü `0000101101110010` (yinelenen `0B 72`), üç 8T dizisinden oluşan `000100000001000000010000` çıktı döngüsüyle bu sınıra ulaşır.

Diğer uçta, en fazla altı ardışık 2T dizisi bulunabilir. En fazla altı 2T dizisinin her grubunu ardından gelen en az 3T uzunluğundaki diziyle birleştirmek, yoğunluğun en fazla 7/15 olmasını sağlar. 10 bitlik girdi döngüsü `0111011110` (yinelenen `77 9D E7 79 DE` olarak paketlenir), altı 2T dizisi ve bir 3T dizisinden oluşan `101010101001010` çıktı döngüsüyle buna ulaşır. Bunlar yalnızca RLL sınırları değil, tablodan gerçekten üretilmiş döngülerdir. Her iki uç değerin de tam seviye döngülerinde NRZI doluluğu %50'dir.

Bu sınırlar uzun veri bölümleri için geçerlidir. Çerçeve eşzamanlamasının özel 9T dizileri vardır ve normal 17PP kurallarının dışındadır; sonlu sonlandırma ve diğer fiziksel çerçeveleme ayrı sayılmalıdır.

| Girdi deseni | Bu fazdaki paketleme | Yoğunluk | Çıktı döngüsündeki diziler | Tek girdi biti fazı |
|---|---|---:|---|---:|
| `0001` | yinelenen `11` | 1/6 = %16,6667 | 6T | 1/6 |
| `00111101` | yinelenen `3D` | 5/12 = %41,6667 | üç 2T + iki 3T | 5/12 |
| `001111` | yinelenen `3C F3 CF` | 4/9 = %44,4444 | üç 2T + bir 3T | 4/9 |
| `0000101101110010` | yinelenen `0B 72` | 1/8 = %12,5 | üç 8T | 7/24 |
| `0111011110` | yinelenen `77 9D E7 79 DE` | 7/15 = %46,6667 | altı 2T + bir 3T | 1/3 |

Altı bitlik yüksek yoğunluk adayının DC dengelemesi öncesi NRZI görev oranı, kutupluluğa bağlı olarak 4/9 veya 5/9'dur; DC dengelemesi bunu değiştirir. Bit çifti fazına dayanıklılığı, onu yinelenen baytlardan sonra değerlendirilecek yararlı bir aday yapar. İki uç desene körlemesine DC bitleri eklemek, elverişli hizalamalarını bozar.

## Fiziksel yerleşim boyunca desenleri koruma

Yararlı bir ince taneli eşleme, denetlenebilir her kullanıcı bitini nihai fiziksel konumuna göre seçer. DC eklenmeden önceki çerçeve biti indeksi `j` için, önüne eklenen DC konumu sayısı `j < 25` olduğunda `0`, aksi halde `1 + floor((j - 25) / 45)` olur. Eklemenin amaçlanan deseni kaydırmasına izin vermek yerine DC konumlarını ayırarak `P[(j + dcBefore(j) + phase) mod period]` desen bitini seçin. Gerçek DC değeri hâlâ sürücünün seçimidir ve komşu kod sözcüklerini etkilemeye devam eder. Bir DC bloğunda fazı sıfırlamak, deneysel olarak karşılaştırılmaya değer ayrı bir stratejidir.

Bu diziyi kayıt çerçevesi/ECC bayt sırasına göre oluşturun, ardından mevcut LDC permütasyonunun tersini kullanarak denetlenebilir kullanıcı baytlarını karıştırma sonrası yüke yerleştirin. Ana makine baytlarını geri elde etmek için uygun karıştırıcı maskesini uygulayın. Ana makine veya kaynak sektör sırasıyla yinelenen bir bayt deseni, genellikle serpiştirmeden sonra periyodik kalmaz. EDC, LDC eşliği, BIS, eşzamanlama ve DC değerleri bağımsız olarak ayarlanabilen yük bitleri değildir; her çerçeve bitini denetlenebilir saymak yerine bunları tam işlem hattı ölçümlerine dahil edin.

Ne yoğunluk ne de NRZI görev oranı nihai siyah/beyaz kutupluluğunu belirler. Yazılmış örnek alanların yansıtması ve görünürlüğü deneysel olarak ölçülmeye devam etmelidir.
