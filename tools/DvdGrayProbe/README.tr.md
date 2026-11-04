# DVD 64 / E4 gri ton ve karışım testleri

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

Bu araç **Verbatim DVD-R AZO 43533** için bir karşılaştırma test diski oluşturur. Önceden test edilmiş `A5 / 66 / B9 / 92` kontrol olarak kullanılır; `66` yerine `64` veya `E4` konulduğunda düz alanları, renk taklitli karışımları ve gerçek görsel sonuçlarını karşılaştırır. Ayrıca `64` ile `E4` karışımlarını ayrı test eder. Araç imaj oluşturur ve doğrular; diske yazmaz.

Tüm byte değerleri, **DVD karıştırması sonrası hedef Q baytlarıdır**. Atlas Q değerini doğrudan belirtir. Oluşturma sırasında en yakın komşu örneklemesi kullanılır; çift doğrusal ara değerleme veya gri tonları yeniden nicemleme yapılmaz.

Yardım, ilerleme, projeye ait hatalar ve JSON açıklama metinleri beş dili destekler. Komuta `--language zh-CN|zh-TW|en|ja|tr|system` ekleyin; varsayılan dil `DISC_IMAGE_STUDIO_LANGUAGE` ile, bu değişken ayarlanmamışsa sistem diliyle belirlenir. Dil seçimi komut adlarını, JSON alanlarını ve teknik tanımlayıcıları, parametreleri, atlas ve görüntü verilerini korur.

Bağımsız piksel doluluğu doğrulayıcısı `verify_mixtures.py`, Python 3 ve NumPy gerektirir; hazırlanmış dosyalarla Windows, Linux veya macOS üzerinde çalışabilir. `--language` seçeneğini de destekler. Atlası okur ve ölçüm JSON/CSV dosyalarını yazar; alanları, panel ve satır tanımlayıcılarını korur; yalnızca yardım, hatalar ve ölçüm açıklaması çevrilir. Sonuçlar piksel doluluğunu tanımlar; optik parlaklığı veya kodlanmış ISO örneklerini değil.

```powershell
python tools/DvdGrayProbe/verify_mixtures.py --prepared "artifacts/dvd-gray-probe-64-e4" --language en
```

## Oluşturma ve doğrulama

Windows ve .NET 10 SDK gerekir. Kendi `6000 × 6000` dört düzeyli gri ton PNG dosyanızı sağlayın. Araç sol üst koordinatı `(2880, 1920)` olan sabit `640 × 640` bölgeyi okur. Gray8 dönüşümünden sonra bu bölge yalnızca `0 / 85 / 170 / 255` içerebilir; yanlış boyut veya gri düzeylerinde oluşturma reddedilir. Araç girdiyi ölçeklemez veya yeniden renk taklidi uygulamaz; depoda örnek kaynak görsel bulunmaz.

Depo kökünden çalıştırın; `four-level-6000.png` yerine kendi girdi yolunuzu yazın. `prepare` çıktı dizini boş olmalıdır:

```powershell
dotnet run --project tools/DvdGrayProbe --configuration Release -- prepare --source "four-level-6000.png" --output-dir "artifacts/dvd-gray-probe-64-e4"

dotnet run --project tools/DvdGrayProbe --configuration Release -- generate --prepared "artifacts/dvd-gray-probe-64-e4" --output "artifacts/dvd-gray-probe-64-e4/burn.iso" --parallelism 8

dotnet run --project tools/DvdGrayProbe --configuration Release -- verify --prepared "artifacts/dvd-gray-probe-64-e4" --iso "artifacts/dvd-gray-probe-64-e4/burn.iso"
```

Çıktı dosyaları:

| Dosya | Amaç |
|---|---|
| `burn.iso` | Gerçekte yazılacak tam disk imajı: 2048 baytlık 2.297.888 sektör, toplam 4.706.074.624 bayt |
| `burn.iso.json` | İmaj oluşturma kaydı ve doğrulama bilgisi |
| `probe.json` | Yerleşim, kaynak görsel ve parametre kaydı |
| `atlas.q8` | 6000 × 6000 doğrudan Q hedef atlası |
| `code-map.png` | Q baytlarını renklerle ayıran yerleşim kılavuzu; renkler gerçek disk rengini veya parlaklığını temsil etmez |
| `nominal-preview.png` | İçeriği tanımak için nominal gri tonla çizilmiş önizleme; yazma sonrası açık/koyu görünümü öngöremez |

`burn.iso` kasıtlı olarak dosya sistemi içermez; normal biçimde bağlanamaması beklenen davranıştır. Yazarken disk yazma yazılımının **“İmaj dosyasını diske yaz / Write image file to disc”** işleviyle seçin; bir veri diskine sıradan dosya olarak eklemeyin. Karşılaştırma için 43533 disk ve önceki testteki aynı yazıcı ve hızı kullanın.

**İki kılavuz PNG dosyasını eski gri tonlu görsel oluşturma akışına vermeyin.** Bu yeniden nicemleme yapar; test diskinin belirtilen `64`, `E4` değerlerini ve karışım oranlarını koruyamaz.

## Test diskini inceleme

Saat 12 yönünden başlayarak saat yönünde 12 adet 30° panel bulunur. Etiketin ikinci kısmı, panelin koyu gri adayıdır:

```text
1-66  2-64  3-E4  |  4-64  5-E4  6-66  |  7-E4  8-66  9-64  |  10-66  11-E4  12-64
```

Her üçlü panel grubu aynı girdiyi, `A5 / candidate / B9 / 92` paletleriyle kullanır. Adaylar farklı açılarda yinelenir ve sıraları değiştirilir; böylece disk konumunun karşılaştırmaya etkisi azaltılır. Aşağıdaki tüm yarıçaplar disk merkezinden ölçülür:

| Yarıçap | Test içeriği |
|---|---|
| 24,5–25,6 mm | İç düz alanlar: her 15° için bir alan; `A5、66、64、E4、B9、92` dört kez yinelenir |
| 26,0–28,1 mm | Dört düzeyli yılanvari Floyd–Steinberg geçişi; tüm paneller aynı düzey haritasını paylaşır, yalnızca koyu gri byte değiştirilir |
| 28,5–30,5 mm | Doğrudan `64 / E4` karışımı; panel başına saat yönündeki üç sütun %25, %50, %75 `64` içerir; kalanı `E4` olur |
| Merkez yarıçapı 37,5 mm | Aynı 12,8 mm kare görsel kırpımları |
| 45,8–47,5 mm | Aday ile `A5` karışımı |
| 47,8–49,5 mm | Aday ile `B9` karışımı |
| 49,8–51,5 mm | Aday ile `92` karışımı |
| 52,2–57,3 mm | İçtekilerle aynı sırada dış düz alanlar; byte etiketleriyle |

Son üç karışım satırında her panel yine saat yönünde üç sütuna ayrılır: **%25, %50, %75 aday**; kalan pikseller o satırın belirttiği diğer byte değeridir. Tüm adaylar aynı ikili FS maskelerini kullanır.

Yüzdeler hedef piksel oranlarıdır; `0 / 85 / 170 / 255` nominal gri değerleridir. İkisi de gerçek diskte eşit aralıklı parlaklık anlamına gelmez. Görsel ızgarası **20 µm** boyutundadır; tek tek Q baytlarını dönüşümlü kullanmak yerine görsel renk taklidi sonrası karışım ölçeğini test eder.

Girdi zaten dört düzeyli renk taklidi uygulanmış PNG'dir. Araç `(2880, 1920)` sol üst koordinatından `640 × 640` alanı ölçeklemeden ve yeniden renk taklidi uygulamadan alır. Özgün deney kırpımında dört düzey yaklaşık `24.13% / 23.90% / 22.74% / 29.24%` oranlarındaydı. Girdi değiştiğinde oranlar görsele bağlı değişir; tüm görsel panellerinde yalnızca özgün `85` düzeyine karşılık gelen byte farklıdır.

Bu atlas, 144 karışım hücresi ve 4.216.432 geçerli piksel üzerinde bağımsız olarak denetlenmiştir. Boş alanlar dışarıda bırakıldığında gerçek ve hedef oranlar arasındaki maksimum fark **0,501 yüzde puanı**, `64/E4` doğrudan karışım satırında maksimum fark **0,197 yüzde puanı** bulunmuş; başka Q baytları karışmamıştır. Tam kayıtlar çıktı dizinindeki `mixture-measurements.csv` ve `mixture-measurements.json` dosyalarındadır. ISO uzunluğu da kontrol edilmiş, 9 yarıçapta toplam 2.359.296 payload hedef Q değeri bayt bayt karşılaştırılmıştır. Bunlar sayısal çıktı doğrulamalarıdır; gerçek disk görünümü yazma sonrasında gözlenmelidir.

## Sonuçları kaydetme

Önce aynı ışık kaynağı ve normal bakış konumunu koruyarak her üç komşu paneli karşılaştırın. Sonra diski çevirip aynı adayın başka konumlardaki tekrarlarını inceleyin. Son olarak bakış açısını biraz değiştirerek “normal açılarda kararlı görünürlük” ile “yalnızca belirli açılarda görünürlük” durumlarını ayırın.

Şunlara odaklanın: aday hem `A5` hem `B9` değerinden ayrılabiliyor mu; karışım oranı arttıkça sıralı değişim görülüyor mu; görselin koyu bölgelerinde ayrıntı geri geliyor mu; `64 / E4` karışımı iki düz alandan daha yararlı ve açıya dayanıklı tonlar üretiyor mu? Kontrolde `B9`, `A5` ve `92` değerlerinden belirgin biçimde ayrılmaya devam etmelidir.

Basit kayıt için 0 = ayırt etmesi zor, 1 = belirli açılarda ayırt edilebilir, 2 = normal açılarda kararlı biçimde ayırt edilebilir kullanılabilir:

| Aday | A5'ten ayrılıyor | B9'dan ayrılıyor | Üç karışım düzeyi sıralı ve ayırt edilebilir | Koyu görsel ayrıntıları | Notlar |
|---|---|---|---|---|---|
| 66 | | | | | |
| 64 | | | | | |
| E4 | | | | | |

Ayrıca `64 / E4` karışımının %25, %50 ve %75 düzeylerinin birbirinden ayrılıp ayrılmadığını ve yalnızca belirli açılarda etkili olup olmadığını kaydedin. Dördüncü düzeyin kullanılıp kullanılmayacağı bu gerçek disk sonuçlarına göre belirlenmelidir.

## Sabit kalibrasyon parametreleri

Bu deney doğrulanmış 43533 kalibrasyon parametrelerini kullanır:

- Başlangıç yarıçapı: `23.991460627731737 mm`
- Bitiş yarıçapı: `57.964893657925366 mm`
- Sektör sayısı: `2297888`
- Kanal biti uzunluğu: `133.3 nm`
- İz aralığı polinom katsayıları: `[-3.9256854319227684E-05, 6.964795915429256E-05, -4.670918950137523E-05]`
- Atlas: `6000 × 6000`, kapsadığı alan `120 × 120 mm`

Bu parametreler bu diskin uzamsal eşlemesi içindir; kılavuz görseller fiziksel bir parlaklık modeli değildir.
