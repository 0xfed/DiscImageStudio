# Blu-ray modu (tek katmanlı BD-R)

[简体中文](ADDING_BLURAY.md) | [繁體中文](ADDING_BLURAY.zh-TW.md) | [English](ADDING_BLURAY.en.md) | [日本語](ADDING_BLURAY.ja.md) | [Türkçe](ADDING_BLURAY.tr.md)

Blu-ray uygulaması `src/DiscImageStudio.BluRay` içindedir; ortamdan bağımsız Core ile `src/DiscImageStudio.Iso` projelerine başvurur ve CD/DVD kodlayıcılarına bağımlı değildir. ISO/IEC 30190:2021 temelinde mevcut kapsam **120 mm, tek katmanlı 25 GB BD-R** ile sınırlıdır. Çift katman, BDXL, BD-RE ve Blu-ray video hazırlama desteklenmez.

## Uygulamadaki yeri

Blu-ray altıncı bir çalışma alanı değil, projenin üçüncü disk türüdür (CD / DVD / BD). Bir BD projesi açıldığında:

- **Tasarım**: tuval ve bileşik görüntü, BD üretim dış yarıçapına göre yerleştirilir. Sol paneldeki “Disk dosyaları” klasörü ve iç/dış halka konumunu seçer, Dışa Aktar sayfasıyla eşitlenir. Sağdaki ton modülü 2 düzey, sRGB doğrusallaştırma, parlaklık ve 3T/6T kutupluluğunu destekler.
- **Önizleme**: tasarım izdüşümü, üretim/ölçüm geometrisi, ton ve kutuplulukla bir 3T/6T kategori haritası oluşturur. Çıktıyı geri okuma, kutupluluğu uygulamadan ISO içindeki AA/44 yük baytlarını doğrudan sınıflandırır; yansıyan parlaklığı değil, kategorileri gösterir.
- **Dışa Aktar**: tam BD ISO üretir (yalnızca çizim veya UDF 2.01 dosyaları). Klasör seçildiğinde dosyalar iç ya da dış halkaya yerleştirilebilir; varsayılan iç halkadır. Konum projeyle kaydedilir ve aynı dizindeki `<iso>.json` yan dosyasına yazılır.
- **Diske Yaz**: aynı ISO akışını, tam görüntüyü kaydetmeden doğrudan MMC sıralı yazımına gönderir. İş yuvası ilerlemeyi ve iptali destekler.
- **Kalibrasyon**: BD hedefi tüm veri alanını kapsar; sektörleri, yarıçapları, kutupluluğu, çizim sınırlarını ve ISO zaman damgasını kaydeder. Uydurma işlemi sabit iz aralığı kullanır ve sonucu BD parametrelerine uygular.

BD ön ayarları CD/DVD ile birlikte `disc-presets.json` (şema 4) içindeki `bluRayPresets` bölümünde saklanır. Yerleşik seçenekler “Standart 25GB tek katmanlı BD”, “Ritek BD-R 10x”, “Ritek BD-R 12x”, “Verbatim BD-R (64099)” ve “Verbatim BD-R (64293)” şeklindedir. Kullanıcı ön ayarları Proje Ayarları iletişim kutusunun altından kaydedilebilir.

## Kapasite ve geometri

- Tek katman kapasitesi 2048 baytlık 12,219,392 kullanıcı sektörü, yani 25,025,314,816 bayttır. 32 sektör, 65,536 baytlık bir küme oluşturur.
- Proje bölümünün sektör sayısı 32'nin pozitif katı olmalı ve tek katman kapasitesini aşmamalıdır; dış yarıçap en fazla 60 mm olmalıdır.
- Üretim ve yazım toplam sektör sayısını paylaşır. Yazım daima tam disk kapasitesini bildirir; dosyalar ve meta veriler dışındaki kullanılmayan sektörlerin yükü sıfırdır.
- Nominal iz aralığı 0.320 µm, kanal bit uzunluğu 74.5 nm, başlangıç açısı 0°'dir.

## Üretim ve ISO

`BluRayIsoGenerator` önce UDF 2.01 dizinlerini ve dosya extent alanlarını planlar, sonra 32 sektör hizalı çizim aralığını ayırır. İç halka modu dosya bölümünü başa, dış halka modu ise disk sonundaki ayrılmış alanın önüne yerleştirir. Baştaki tanıma bilgileri, dayanak noktaları ve sondaki yedek bilgiler öngörülen yerlerinde kalır. Çıktı uzunluğu `TotalSectors × 2048` olarak sabittir.

- Klasör seçilmemişse: tüm veri alanı yalnızca çizim içerir, dosya sistemi içermez. Bu `.iso` uygulamayla geri okunabilir; sıradan dosya sistemi araçlarıyla gezilemez.
- Klasör seçilmişse: UDF 2.01 yazılır ve 4 GiB'dan büyük tek dosyalar desteklenir. `--file-position inner|outer` konumu seçer; varsayılan `inner` değeridir, `outer` için `--data-dir` gerekir. Dosyalar, dolgu, desen ve son meta veriler tek bir ileri akışta yazılır.
- Çıktı yolu kaynak klasörün dışında olmalı, kaynak görüntünün üzerine yazmamalıdır. İptal veya hata, mevcut ISO'yu korur.
- Başarılı üretimden sonra `<iso>.json` (`format: bd-r-image-v1`) yazılır: toplam sektörler, iç/dış yarıçaplar, çizim sınırları ve yarıçapları, 3T/6T kutupluluğu, `ToneProfile`, birim etiketi, UDF zaman damgası, kaynak görüntü ve üretim zamanı. Önizlemede bu ISO seçilince parametreler otomatik geri yüklenir; yan dosyası olmayan dış ISO, mevcut proje geometrisiyle yine geri okunabilir.

Dosyalar sıradan kullanıcı baytları kullanır; desen için ters karıştırma uygulanmamalıdır. Desenler özgün tam disk sektör adreslerini ve geometrisini korur; kalan desen alanı tüm diski kaplayacak şekilde yeniden gerilmez. UDF bölümü yalnızca dosya sisteminin bulunduğu alanı kapsar ve desenler dosya sisteminde kullanılabilir alan olarak işaretlenmez. Gerçek çizilebilir alanı dosya kullanımı ile sondaki yedek alan birlikte belirler. GUI tasarım izdüşümü ve yan dosyalı çıktının geri okuması, dosya sistemi dahil dosya alanını sarı ve bir açıklama anahtarıyla gösterir. Yerleşim bilgisi olmayan dış ISO'lar için dosya aralıkları tahmin edilmez.

`BluRayIsoGenerator.Prepare` önce klasörü tarayıp UDF 2.01 dizinlerini ve dosya extent alanlarını planlar. İç halka yerleşimi dosyaların sonunu yukarıya doğru 32 sektör sınırına, dış halka yerleşimi dosya bölümünün başlangıcını aşağıya doğru 32 sektör sınırına hizalar. Disk sonunda yedek birim tanımlayıcıları ve dayanak noktaları için 288 sektör ayrılır. Bu dosya sistemi API'si klasör belirtilmediğinde bile boş birimi korur. En az bir tam çizim kümesi kalmalıdır; en küçük test kapasitesi 640 sektördür.

Dosya ve akış çıktısı aynı ileri yazım yolunu kullanır. Planlamadan sonra kaynak dosyalar veya dizin içeriği değişirse eski yerleşim reddedilir. Dosya uzunlukları 64 bit alanlarda tutulur; büyük dosyalar en fazla `0x3ffff800` baytlık ayırma extent alanlarına bölünür ve 128 KiB tamponla sıralı okunur. Tek dosya boyutunu 4 GiB yerine diskte kalan kapasite sınırlar. DVD'nin ISO9660/Joliet yolu değişmez.

UDF uygulaması [ECMA-167](https://ecma-international.org/publications-and-standards/standards/ecma-167/) ve [ECMA TR/112-4 (UDF 2.01)](https://ecma-international.org/wp-content/uploads/ECMA_TR-112-4_1st_edition_december_2023.pdf) temelinde kapatılmış, salt okunur birimler yazar. Veri diskleri içindir; Blu-ray video hazırlama sağlamaz.

## Ton ve kutupluluk

BD ton işlemesi kaynak görüntü çözünürlüğünde bir kez yapılır: sRGB doğrusallaştırma → parlaklık kaydırma → bayt düzeyinde renk taklidi → 128 eşiğinde ikilileştirme. Üretim, yazım ve önizleme aynı ikili rasteri paylaşır; yaklaşık 25 milyar yük baytının her biri için ayrı karma hesaplanmaz.

`BluRayRunLengthMapping` yalnızca son adımı yapar: koyu alanlara 3T sayısını artırma eğilimindeki `AA`, açık alanlara 6T sayısını artırma eğilimindeki `44` yazılır. `InvertMapping` uçları değiştirir. Bu bir görüntü eşleme kuralıdır; saf 3T/6T veya ölçülmüş optik kutupluluk taahhüdü değildir. Gerçek açık/koyu yönü kalibrasyon diskiyle gözlemlenmelidir. Kutupluluk alanı `ToneProfile` dışında kaydedilir.

## 3T / 6T sayı eşlemesi ve genişletme

Görüntü yolu `BluRayImageLoader` ile kod çözer ve saydamlığı beyaz zemin üzerine birleştirir, sonra `BluRayImagePayloadMapping` üzerinden örnekler. En boy oranı korunur, uzun kenar disk dış çapına karşılık gelir, boş alanlar beyazdır. Örnek konumları her çerçevenin EDC yer tutucusunu, LDC iç içe geçirmesini, BIS eklemesini, DC bitlerini, eşzamanlamayı ve run-in bölümünü içerir. 2:3 modülasyon oranıyla bulunan konum nominal bayt merkezidir; değişken uzunluklu 17PP kod sözcüklerinin tam sınırı değildir.

Yerleşik `BluRayRunLengthMapping` 128'de ayırır ve karıştırılmış alandaki `AA` veya `44` değerini döndürür. Seçim, aynı 958,272 kanal bitine sahip tam kümelerde ardışık modulation ONE değerleri arasındaki 3T / 6T aralıklarının sayısına dayanır. Hem mark hem space sayılır; tek bir düzeyin görev çevrimi değildir. Temel PSN `0x100000` üzerinde 256 tekrarlanan baytın taramasında `AA` için 265,136 adet 3T, `44` için 116,575 adet 6T bulunur; her biri hedef sayıda en yüksektir. Dört adreste yeniden kontrol güçlü eğilimi korur, ancak her adreste birincilik garanti edilmez. Ayrıntılar için [bayt analizine](BLURAY_BYTE_DENSITY.tr.md#按-3t--6t-数量接入绘图) bakın.

`IBluRayLuminanceMapping.Map` uygulayarak strateji değiştirilebilir; kaynak parlaklığı ve nominal bayt konumu için seçilen karıştırılmış alan baytı döndürülür. Görüntü dışı deneylerde `IBluRayPayloadMapping.FillScrambledPayload` doğrudan uygulanıp sektörün 2048 kullanıcı bayt konumu doldurulabilir, ardından `BluRayStreamingGenerator` öğesine verilir. Geri çağırım nominal sektörü, PSN'yi ve disk parametrelerini alır.

`BluRayDiscModule`, WPF'den bağımsız kalmak için kurucudan bir görüntü çözme temsilcisi alır. App CLI, `BluRayImageLoader.Load` kullanır. Doğrudan modül API'siyle görüntü üretenler de bu temsilciyi sağlamalıdır. `BluRayConstantByteMapping` yalnızca açıkça seçilen sayısal deneyler içindir. Görüntüler ve kalibrasyon, dosya üretmek veya yazıcıya akış sağlamak için `BluRayIsoGenerator` kullanır.

Aday elemesi için [bayt ve bit dönemi yoğunluk analizine](BLURAY_BYTE_DENSITY.tr.md) bakın. `tools/BluRayByteProbe`, 256 baytın tümü ve ince dönemler için tam küme istatistiklerini yeniden üretir. Dönemsel bit dizileri başka deney stratejileridir; mevcut sayı eşlemesi tekrarlanan baytları kullanır. Analiz sonuçları hâlâ gerçek diskin siyah/beyaz kutupluluğunu belirlemez.

## Komut satırı

```powershell
# Tek katman disk parametrelerini inceleyin veya JSON dışa aktarın
dotnet run --project src/DiscImageStudio.App -- bd-info --output bd-parameters.json

# Yalnızca çizim ISO (1024 sektör hızlı işlev kontrolü içindir, gerçek tam disk yapılandırması değildir)
dotnet run --project src/DiscImageStudio.App -- bd-generate --input image.png --output bd-test.iso --total-sectors 1024 --invert true

# İç halkada UDF dosyaları içeren tam kapasiteli ISO
dotnet run --project src/DiscImageStudio.App -- bd-generate --input image.png --output bd-image.iso --data-dir C:\Files --volume-label BD_IMAGE

# Ton: renk taklidi, parlaklık, sRGB doğrusallaştırma
dotnet run --project src/DiscImageStudio.App -- bd-generate --input image.png --output bd-tone.iso --tone-dither 0.75 --tone-gamma true --tone-brightness -4

# Ham yük probu ve tek ECC kümesi tanılaması
dotnet run --project src/DiscImageStudio.App -- bd-probe --byte 0xA6 --sectors 32 --output bd-probe.bin
dotnet run --project src/DiscImageStudio.App -- bd-encode --input bd-probe.bin --psn 0x100000 --output bd-ecc.bin
```

`bd-generate` için `--preset`, `bd-r-sl-25gb`, `bd-r-ritek-10x`, `bd-r-ritek-12x`, `bd-r-verbatim-64099` veya `bd-r-verbatim-64293` olabilir. `--total-sectors`, `--r0`, `--r1`, `--start-angle` karşılık gelen değerleri geçersiz kılar. `--invert`, `true`/`false` kabul eder. `bd-generate` artık eski `--start-sector` / `--sectors` dilim parametrelerini kabul etmez; dilimleme yalnızca `bd-probe` içinde kalır.

`bd-probe` için `--byte`, 0..255 veya `0x00`..`0xFF` aralığındadır ve aralıklar 32'ye hizalanır. `bd-encode` girdisi tam 65,536 bayt olmalıdır; çıktı 17PP, DC-control ve dosya sistemi içermeyen 76,880 baytlık ECC kümesidir ve yazılabilir disk görüntüsü olarak kullanılamaz.

## Kodlama zinciri

| Bileşen | ISO/IEC 30190:2021 |
| --- | --- |
| 2048-byte yük + 4-byte EDC | §13.2–13.3 |
| 16-bit LFSR, PSN bits 19..5 üzerinden tohum; her data frame için sıfırlama | §13.4 |
| LDC RS(248,216), 304 kod sözcüğü ve iç içe geçirme | §13.5–13.8 |
| Adres, RS(9,5), BIS RS(62,30) ve çoklama | §13.9–13.14 |
| 17PP, DC-control ve eşzamanlama çerçeveleri | §13.15–13.17 |
| Kayıt birimi aralığı | §14 |
| Nominal iz aralığı 0.320 µm | §15.4.3 |
| Tek katman PSN 0x00100000..0x00CA73FF | §17 |
| Tek katman veri alanı 381,856 küme | §19 |

Desen testlerinde **ters karıştırılmış kullanıcı yükü** yazılır. Gerçek sürücü EDC'yi hesaplayıp 2052-byte data frame bütününü karıştırır. EDC, hata düzeltme, adres ve eşzamanlama gibi veriler, seçili bayt probuyla keyfî olarak değiştirilemez.

`BluRayRecordingFrameEncoder`, çağıranın açıkça sağladığı 28 DC kontrol bitini alır. `BluRayRecordingUnitEncoder` ayrıca run-in, 496 çerçeve, run-out ve isteğe bağlı Guard3 içeren tanılama benzetimi sağlar; her modülasyon biti / NRZI düzeyi bir baytla gösterilir. Otomatik stratejinin adı açıkça `BluRayGreedyDcControlEncoder` olarak belirlenmiştir: henüz kararlaştırılmamış sonraki bitleri 0 varsayan ileri bakışlı açgözlü seçim yapar. Sonraki kontrol bitleri kesinleştiğinde önceki yerel optimumlar değişebilir; döndürülen `SuboptimalDcControlDecisions` bunu bildirir. Bu deneysel strateji, standardın istediği nihai DC optimumunu veya yazıcının gerçek bit dizisiyle eşleşmeyi garanti etmez. CLI `bd-encode` yalnızca belirli ECC aşamasını dışa aktarır; bu tanılama benzetimini yazılabilir görüntü olarak dışa aktarmaz.

`BluRayTrackGeometry` bağımsız, sabit aralıklı bir spiral kullanır; elle girilen toplam sektör sayısının karşılığı olan toplam iz uzunluğu ile iç/dış yarıçaplardan etkin iz aralığını hesaplar. Son sektör ayarlanan dış yarıçapa ulaşır. Veri yüzünden bakıldığında sabit disk koordinatlarındaki örnekleme yolu saat yönünde dışarı ilerler (§9.8, §15.3); ekran dönüşümü Y eksenini yalnızca bir kez ters çevirir. Her kayıt biriminin uzunluğu `2760 + 496 × 1932 + 1104 = 962136` kanal bitidir; bit uzunluğu 74.5 nm'dir. Yerel `bd-probe` aralıkları tam kapasite geometrisini kullanır. Dizinin sonundaki Guard3, her kayıt biriminin yinelenen uzunluğuna dahil edilmez.

Varsayılan 24–58 mm nominal geometridir; belirli bir diskin ölçülmüş parametreleri değildir. Yerleşik akışla yazım, BD-R'yi biçimlendirmeden ve kusur yönetimi yedek alanı ayırmadan bırakarak `sector → PSN` modeline uyar. Gerçek yarıçap, konum ve optik karşıtlık fiziksel disk kalibrasyonu gerektirir. Dış yazılımla ISO yazarken de yedek alansız mod kullanılmalıdır.

## Yedek alansız akışla yazım

`IDiscFormat2Data`, biçimlendirilmemiş BD-R için varsayılan yedek alan düzeninde 11,826,176 kullanılabilir sektör bildirebilir; bu, ham 12,219,392 sektörden 768 MiB azdır. Blu-ray yolu, `IDiscRecorder2Ex.SendCommandSendDataToDevice` üzerinden LBA 0'dan sıralı `WRITE(10)` gönderir; her komut 32 sektör (64 KiB) yazar ve ilk yazımın başlatılmasına zaman tanımak için 200 saniye zaman aşımı kullanır. Tamponlar bağdaştırıcının adres hizalama ve en büyük aktarım sınırlarına uyar. Bu yol veri yazım arayüzünün `Write`, `FORMAT UNIT` veya yedek alan ayırma işlemlerini çağırmaz.

Açık `IWriteEngine2` arayüzünde ayarlanabilir komut zaman aşımı özelliği yoktur; bu nedenle sorgu ve önbellek boşaltma zaman aşımları `WriteSection` içindeki beklemeyi uzatamaz. Mevcut açık komut yolu başarısız komutun LBA'sını, sektör sayısını, geçen süreyi, HRESULT ve Sense verilerini bildirir. Zaman aşımı cihaz tarafındaki sonucu belirsiz bırakabilir; uygulama bu aralığı otomatik yeniden göndermez.

Hata günlükleri son 8 başarılı yazımın zamanlamasını da tutar; kaynak akışı okuma beklemesini, cihaz komutu beklemesini ve birikimli alındığı onaylanan veri miktarını ayrı sayar. Geçerli Sense yoksa “Geçerli cihaz Sense verisi döndürülmedi” açıkça belirtilir; tümü sıfır bir sonuç diskte hata olmadığı anlamına gelmez. `0x8007048F`, Windows'un cihazın bağlantısının kesildiğini bildirdiğini gösterir. 200 saniyelik zaman aşımı olan bir komut milisaniyeler içinde başarısız olup cihaz ardından yeniden listeleniyorsa bağlantı, üretici yazılımı ve USB olayları incelenmelidir; yalnızca bekleme yetersizliği olarak ele alınmamalıdır. Bu örnekteki tüm cihaz sorguları ve yazımlar, bitmemiş sorguyla yazımın çakışmasını önlemek için sıraya alınır.

Disk özel erişimle kilitlendikten sonra MMC ile mevcut profile, disk bilgisi, biçim kapasitesi ve iz bilgisi okunur. Boş, biçimlendirilmemiş, tek katmanlı BD-R SRM, ilk izin 0'dan başlaması ve tam ham yazılabilir kapasite gerekir. Bilinmeyen, kullanılmış veya biçimlendirilmiş yerleşimler reddedilir; toplam sektör sayısı otomatik azaltılmaz. Arayüzdeki toplam sektör sayısı 2048 bayt üzerinden aynı istek uzunluğuna çevrilir; UDF öneki, çizim alanı ve son meta veriler buna dahildir. Daha küçük elle girilmiş kapasite için de belirtilen aynı sayıda sektör yazılır.

Yazım bitince önbellek boşaltılır, iz kapatılır ve BD-R finalize işlevi 6 ile disk sonlandırılır. İptal, mevcut cihaz komutu tamamlandıktan sonra uygulanır. İptal veya yazım hatasında yalnızca önbelleği boşaltmak ve cihazı serbest bırakmak için çaba gösterilir; eksik görüntü başarılı bir çıktı olarak sonlandırılmaz. Her kilit açma yalnızca başarıdan sonra tamamlandı sayılır; başarısız işlem bir kez yeniden denenir ve hatası tanılamada tutulur. Başarılı olmuş eşlenik sayaç işlemleri tekrarlanmaz. Sonlandırma başladıktan sonra komutun bitmesi beklenir. Yedek alansız modda bozuk blokların yeniden eşlenmesi için yedek kapasite yoktur; kesilen aynı diske genellikle devam edilemez.

BD anlık hızı, yazıcının alındığını onayladığı komutlardan hesaplanır ve cihaz önbelleğinin etkisini içerir. Lazerin disk üzerindeki anlık hızı değildir; nihai başarı için yine önbellek boşaltma ve sonlandırmanın bitmesi gerekir. 4× nominal kullanıcı veri hızı 18 MB/s'dir (1× = 4.5 MB/s). Arayüz ondalık MB/s kullanır; istenen hız çarpanı gerçek aktarım hızının yerine geçmez. Otomatik mod hâlâ mevcut diskin bildirdiği en düşük profili seçer.

Yazımdan hemen önce cihazın döndürdüğü ayarlanmış hız kaydedilir; ayar ile anlık aktarım hızı ayrı tutulur. Yazım sırasında her 10 saniyede ve son bloktan sonra bir tanılama penceresi kaydedilir. Kaynak akışı okuma süresi (bekleme ve kopyalama dahil), WRITE komut süresinden (aktarım ve cihaz beklemesi dahil) ayrılır; alındığı onaylanan ortalama hız da yazılır. Bu düşük sıklıklı günlükler cihaz sorgusu eklemez, ilerleme veya hızın güncellik kurallarını değiştirmez. Kaynak okuma süresi üretici iş parçacığının salt hesaplama süresi değildir; komut süresi de tek başına USB aktarımını fiziksel yazımdan ayıramaz.

Görüntü akışı en fazla 8 çalışan ve 16 kümelik (1 MiB) sınırlı gruplar kullanır; hesaplamalar bittikten sonra özgün sırayla yazar. Yerleşik 3T/6T eşik eşlemesi ile karıştırma XOR işlemi tamsayı SIMD kullanırken konum hesaplamaları özgün hassasiyeti korur. Doğrulanmış disk parametreleri artık her bayt için yeniden kontrol edilmez. Değişmez raster görüntülerde paralellik otomatik açılır; yerleşik kalibrasyon desenleri örneklemenin iş parçacığı güvenliğini açıkça bildirir. Özel örnekleme geri çağırımları varsayılan olarak sıralı kalır. Ön tamponlama yalnızca geçici hız farklarını karşılar; sürekli yazımda besleme hızı seçili cihaz hızını aşmalıdır.

Protokol ve iş akışı testleri yalnızca sahte cihazlar ve 1024 sektörlük (2 MiB) UDF akışı kullanır. Tam 12,219,392 sektör kapasitesi yalnızca kapasite ve geometri meta verileriyle doğrulanır; tam disk ISO üretilmez. Fiziksel yazım sonuçları yine gerçek disk deneyi gerektirir.

Uygulama dayanakları: [Microsoft veri gönderimi ve komut zaman aşımı arayüzü](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscrecorder2ex-sendcommandsenddatatodevice), [libburn MMC uygulaması](https://dev.lovelyhq.com/libburnia/libburn/src/branch/master/libburn/mmc.c), [libburn yazım zaman aşımları](https://dev.lovelyhq.com/libburnia/libburn/src/branch/master/libburn/spc.h).

## Doğrulama kapsamı

Otomatik denetimler 17PP/EDC/Reed-Solomon, tam RUB yeniden yürütme, görüntü eşleme, paralel üretim, ISO/UDF, 4 GiB'dan büyük dosyalar, sıralı yazım komutları, çizim aralıkları, yan dosyalar ve iptali kapsar. Diske kaydedilen UDF testleri yalnızca 1024 sektördür; her ISO en fazla 2 MiB'dir. Büyük dosya testleri önce seyrek dosya özelliğini açıp doğrular, ardından 4 GiB'dan büyük mantıksal kaynak (gerçekte ayrılan alan 1 MiB'dan az) oluşturur ve büyük ISO yazmak yerine veriyi doğrulayıp atan bir akışa sıralı çıktı verir. Gerçek disk kapasitesi yalnızca matematiksel olarak doğrulanır. Testler gerçek yazıcıya erişmez ve fiziksel diskteki görünür parlaklığı veya kutupluluğu kanıtlamaz.
