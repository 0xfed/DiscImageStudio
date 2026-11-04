# CD ses izi biçimleri, bayt sırası ve çift görüntü sorununu araştırma

[简体中文](CD_AUDIO_COMPATIBILITY.md) | [繁體中文](CD_AUDIO_COMPATIBILITY.zh-TW.md) | [English](CD_AUDIO_COMPATIBILITY.en.md) | [日本語](CD_AUDIO_COMPATIBILITY.ja.md) | [Türkçe](CD_AUDIO_COMPATIBILITY.tr.md)

## Disk dosyaları ve veri diski

CD projesinde Tasarım veya Dışa Aktar sayfasında disk dosyaları klasörü seçilince otomatik olarak ISO9660/Joliet veri diskine geçilir ve çıktı uzantısı `.iso` olur. Klasör yolu temizlenince WAV ses izine dönülür. Dosyalar iç veya dış halkaya yerleştirilebilir; varsayılan iç halkadır ve ayar projeyle kaydedilir. Dosya sistemi meta verileri disk başında kalır. Dış halka modu dosya içeriğini sona koyar; çizim kalan alana sıkıştırılmadan özgün tam disk koordinatlarını korur.

```powershell
DiscImageStudio.exe cd-generate --input picture.png --output cd-image.iso `
  --data-dir "D:\随盘文件" --volume-label CD_IMAGE --file-position outer
```

ISO uzunluğu `sectorCount × 2048` olur (sectorCount sektör sayısıdır); CD-ROM Mode 1 veri diski olarak yazılır. Uygulamadaki anında yazım aynı ISO veri akışını kullanır; dış yazılımda görüntü yazma işlevi seçilmelidir. Ses iç içe geçirme anahtarı ve CUE yalnızca ses izi modu içindir. Veri diskinin çizim alanı CD-ROM karıştırmasını ve konum eşlemesini ayrı telafi eder; disk dosyaları özgün içeriğini korur.

CD veri diskinin anında yazımı, ses izleriyle DAO oturumunu ve lead-out sınır denetimini paylaşır; ATIP ile kalibre edilen sektör sayısını ve tam dosya yerleşimini korur. Uygulama ISO yükünü Mode 1 ham sektörlere kodlar ve veri izi göstergesini ayarlar. Ritek Tıbbi Su Mavisi ön ayarıyla yine 359,845 veri sektörü yazılır; TAO arayüzünün daha küçük kullanılabilir kapasitesine göre desen veya dosyalar kısaltılmaz. DAO, ses modunun en fazla 3 sektörlük sınırlı overburn denetimini korur; sınır aşılırsa yazımdan önce hata verir.

Veri izleri IMAPI küçük uçlu ana kanal kuralını kullanır. Standart Mode 1 sektörlerine EDC, P/Q hata düzeltme ve karıştırma eklenirken eşzamanlama başlığı `00 FF…FF 00` bayt sırasını korur. CD-ROM fiziksel kodlamasının komşu bayt değişimi, küçük uçluya dönüşümdeki değişimi iptal eder. Bu, cdrdao'nun [RAW kodlaması ve cihaz bayt sırası dönüşümüyle](https://github.com/cdrdao/cdrdao/blob/master/dao/dao.cc) de uyumludur. Taşıma kuralı dosyaların iç/dış halka ISO yerleşimini veya çizimin fiziksel koordinat eşlemesini değiştirmez.

Tasarım izdüşümü, küçük ton önizlemesi ve geçerli `.iso.json` yerleşim bilgili çıktının geri okuması, dosya sistemi dahil dosya alanını sarı gösterir. Aynı adlı meta veriler geometri, ton ve dosya konumunu kaydeder; ISO taşınırken birlikte taşınması önerilir. Önizleme çizim yükünün eşlemesini gösterir; gerçek disk görünümü yine yazım deneyi gerektirir.

## Önerilen kullanım

Yeni üretim işleri varsayılan olarak `cd-track.wav` ve yanında `cd-track.cue` üretir. WAV sıkıştırılmamış PCM'dir: 44.1 kHz, 16 bit, stereo ve küçük uçlu veri. İki dosyayı aynı dizinde tutun; taşıma veya yeniden adlandırmada CUE içindeki dosya adını da güncelleyin. CUE isteğe bağlıdır: Dışa Aktar alanında “CUE dosyasını da dışa aktar” seçimini kaldırın veya komut satırında `--cue false` kullanın; yalnızca WAV yazılır.

| Yazım girişi | Kullanım |
| --- | --- |
| cdrecord | `cdrecord dev=DEVICE speed=SPEED -audio "cd-track.wav"` (DEVICE cihaz, SPEED hızdır); `-swab` eklemeyin |
| ImgBurn | “Write image file to disc” içinde `cd-track.cue` açın |
| Uygulamada akışla yazım | WAV başlığı veya tam geçici iz üretmeden aynı küçük uçlu PCM verisini otomatik oluşturur |
| Eski RAW iş akışı | `.raw` çıktı seçilebilir; `cdrecord -audio "cd-track.raw"` için `-swab` eklemeyin |

Çıktı uzantısı gerçek biçimi belirler; yalnızca `.wav` ve `.raw` kabul edilir. Eski RAW yeniden adlandırılarak WAV'a çevrilemez; yeniden üretilmelidir. WAV yanındaki CUE, `BINARY` yerine `FILE "cd-track.wav" WAVE` kullanır. Unicode adlar için BOM içeren UTF-8 CUE kullanılır; boşluklar ve Çince desteklenir. ImgBurn yazarı bu CUE biçiminin Unicode dosya adı desteğini doğrulamış ve erken 2.5.0.0 sorunlarının 2.5.1.0 sürümünde düzeltildiğini belirtmiştir. [ImgBurn yazarının açıklaması](https://forum.imgburn.com/topic/13526-unicode-filename-problem-in-cue-file/)

Komut satırı üretim örneği (kendi ölçülmüş geometrinizi kullanın):

```powershell
DiscImageStudio.exe cd-generate --input picture.png --output cd-track.wav --interleave true
```

`--cue` (varsayılan `true`), WAV çıktısıyla aynı adlı CUE yazılıp yazılmayacağını belirler. `.raw` çıktısı hiçbir zaman CUE yazmaz.

WAV dosya uzunluğu `sectorCount × 2352 + 44` olur; ses uzunluğu, üretim ilerlemesi ve akışla yazım kapasitesi `sectorCount × 2352` olarak kalır. Başlık disk ses izi sektörü kullanmaz ve desen geometrisini değiştirmez.

## Ton (renk taklidi, sRGB doğrusallaştırma, düzeyler ve parlaklık)

Her CD ses baytı disk üzerinde yaklaşık 6.8 µm uzunluğa karşılık gelir. Normal bakış uzaklığında göz yüzlerce veya binlerce baytı tek bir ton olarak birleştirir. Dolayısıyla “her bayt yalnızca siyah/beyaz desen içerir” demek “görüntü yalnızca siyah/beyazdır” demek değildir; ara tonlar komşu baytların siyah/beyaz oranıyla oluşur.

| Parametre | Anlam |
| --- | --- |
| Renk taklidi gücü 0–100% | 0% sert 128 eşiğiyle çıktı verir (eski davranış); 100% her baytta komşu düzeyler arasında orantılı seçim yapar, ara tonlar siyah/beyaz baytların uzamsal oranıyla oluşur |
| Gri düzeyleri (yalnızca CD) | 2 düzeyde `0x0D` / `0xAA`; 4 düzeyde `0x0D` / `0x10` / `0x63` / `0xAA` (`0x0D` siyah, `0xAA` beyaz) |
| sRGB doğrusallaştırma (gama) | Önce sRGB aktarım işleviyle çözer, sonra renk taklidi yapar; çıktı düzeyini değil siyah/beyaz nokta yoğunluğunu değiştirir |
| Parlaklık | Griyi genel olarak −64…+64 aralığında kaydırır (0..255 biriminde) |

Komut satırı seçenekleri (hem `cd-generate` hem `cd-preview-warp` kabul eder):

```powershell
DiscImageStudio.exe cd-generate --input picture.png --output cd-track.wav `
  --tone-dither 1 --tone-levels 2 --tone-brightness 0 --tone-gamma false
```

Varsayılanlar `--tone-dither 0 --tone-levels 2 --tone-brightness 0 --tone-gamma false` şeklindedir ve eski sürümle bayt düzeyinde aynıdır. Yeni projelerde Tasarım sayfasının sağındaki Ton modülü 75% / 2 düzey kullanır. `tone` bölümü olmayan eski projeler 0% ile yüklenir ve kaydedilince `tone` yazılır. Dışa Aktar sayfasında yalnızca çıktı seçenekleri kalır; çıktı kartı tonun Tasarım'da ayarlandığını belirtir.

Renk taklidi fazını ekran koordinatları değil, **mantıksal bayt indeksi** (`globalByte`) belirler. Gecikmeli iç içe geçirme baytları dosyada taşıdıktan sonra geri okuma aynı mantıksal indeksle yeniden kurar; üretim, kalibrasyon izdüşümü ve geri okuma aynı desenleri gösterir. Kalibrasyon desenleri (`GeneratePattern`) sabit sert eşik kullanır; renk taklidi uygulanmaz.

Ses izinin yanındaki aynı adlı `.json`, `toneDitherStrength`, `toneLevels`, `toneBrightness`, `toneSrgbLinearization` kaydeder. Önizlemede dış ses izi yüklendiğinde bu değerler geometri ve iç içe geçirmeyle birlikte eşitlenir.

Geri okuma önizlemesi varsayılan olarak 1600 px ve sektör başına 16 örnek kullanır; aynı piksele düşen örnekler **toplanıp ortalanır**. Eski sabit 900 px ve sektör başına 2 örnek düzeninde sonraki yazım öncekinin üzerine geçer, seyrek örnekleme ara tonlarda gazete noktaları gibi beyaz noktalar bırakırdı. Yoğunluğu artırmak aynı diskte sürekli ton görünümünü sağlar. Önizlemenin “Tasarım izdüşümü” başlangıçta ölçülmüş geometriyi proje geometrisine eşitler; proje açıldığında nötr izdüşüm görülür. İki geometri arasındaki fark yalnızca ölçülmüş yarıçaplar girilince gösterilir.

## Doğrulanmış sorunlar

2026-09-12 tarihli kullanıcı gerçek disk karşılaştırmasında, aynı mevcut RAW cdrecord'un `-audio` ve `speed` seçenekleriyle normaldi; `-swab` eklenince çift görüntü yeniden oluştu. Bu, mevcut RAW'ın cdrecord'un varsayılan büyük uçlu ses girdisi olarak yorumlanması gerektiğini doğrular.

Önceden dosya dışa aktarma ve akışla yazım tamamen aynı `GenerateToStream` çıktısını paylaşıyor, akış yolunda bayt sırası dönüşümü yapılmıyordu. Ancak giriş yollarının kuralları farklıdır:

| Girdi | Ses verisi gereksinimi |
| --- | --- |
| cdrecord başlıksız `-audio` | Varsayılan büyük uçlu |
| cdrecord başlıksız `-audio -swab` | Küçük uçlu; seçenek değiştirilmesi gereken giriş sırasını bildirir |
| cdrecord WAV | Başlığı ayrıştırıp ses bayt sırasını otomatik tanır |
| CUE `FILE ... BINARY` | Küçük uçlu; mevcut büyük uçlu RAW doğrudan BINARY CUE içinde kullanılamaz |
| IMAPI `AddTrack(IMAPI_CD_SECTOR_AUDIO, stream)` | 16 bit stereo 44.1 kHz WAV veri alanıyla aynı örnek yerleşimi; başlıksız küçük uçlu PCM |

Yukarıdaki cdrecord davranışı ve BINARY tanımı [resmî cdrecord kılavuzundan](https://cdrtools.sourceforge.net/private/man/cdrecord/cdrecord.1.html), IMAPI ses örneği yerleşimi [Microsoft AUDIO sektör tanımından](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/ne-imapi2-imapi_cd_sector_type) gelir. ImgBurn yazarı da başarılı `FILE ... WAVE` CUE yazım örneği vermiştir. [ImgBurn WAVE CUE örneği](https://forum.imgburn.com/topic/25173-extract-only-session-two-from-audio-cd/page/2/)

Burada `AudioSectorType = 0`, IMAPI enum AUDIO değeridir; ses izini fiziksel Mode 0 olarak yazmak değildir. `IRawCDImageCreator::AddTrack` şu anda yalnızca AUDIO türünü destekler. [Microsoft AddTrack belgesi](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-irawcdimagecreator-addtrack)

## Neden iki kontur oluşur

Mevcut `CddaInterleaver`, hedef baytlara gecikme telafisi uygular. Son çıktıdaki her 16 bit örneğin iki baytını değiştirmek, farklı gecikme konumlarına ait baytları çapraz biçimde kodlama zincirine sokar. Bu yüzden değişiklik sıradan ses değeri değişimi olarak yorumlanamaz ve görüntü örneklemesinden önce tek bir değişimle iptal edilemez.

Mevcut gecikme tablosuyla yapılan tamsayı etiket permütasyonu deneyinde baş/son dolgu çıkarıldığında, ek örnek bayt değişimi ters eşleme sonrası çift konumları `+73`, tek konumları `-73` hedef bayt kaydırır. Bu çift kontur mekanizmasını açıklar; uygulamanın gecikme permütasyonu modelinden türetilmiştir, tam sürücü CIRC modeli veya fiziksel disk ölçümü değildir.

Düzeltme örneklemeyi ve gecikme tablosunu korur; telafi sonrasında 2352 baytlık sektör çıktı sınırında bayt sırasını seçer: eski RAW büyük uçlu, WAV ve IMAPI küçük uçludur. WAV başlığı dış araçların biçimi ve bayt sırasını tanımasını sağlar; akış arayüzü kendi kuralına göre yalnızca veri alanını almaya devam eder.

## Geri okuma gecikmeli iç içe geçirmeyi geri almalı

Gecikme telafisi mantıksal bayt i'yi `i + offset(i % 24)` dosya indeksine taşır. Tablo 28×D (D=4) çerçevelik gecikme hatları ve çerçeve başına 24 bayt tutar; dolayısıyla kaydırma 72 ile 2592 arasındadır (çerçeve gecikmesi ve çerçeve içi sütun döndürme toplamı). Dosya sırasıyla baytları spirale yerleştirmek deseni döndürür ve kesme dönüşümü uygular. Aynı görüntünün 60000 sektör ve 900 px karşılaştırmasında çizilen piksellerin yaklaşık 17.5%'i kaynaktan farklıdır; “hafif bozulma” gibi görünür.

Bu nedenle `cd-preview-track` varsayılan olarak ters eşleme yapar (`--deinterleave`, varsayılan `true`). Mantıksal bayt i için `i + CddaInterleaveTable.FileOffsetFor(i % 24)` dosya indeksi alınır; 2592 baytlık ileri bakış penceresi gerekir. İleri gecikme ile `CddaInterleaver` aynı `CddaInterleaveTable` kullanır; iki taraf bağımsız kayamaz.

Sondaki maliyet: dosya biterken gecikme hatları son birkaç çerçeveyi hâlâ tutar. Bu mantıksal baytlar yazılmaz (1318–1332 bayt, bir sektörden az) ve geri okumada boş bırakılır. İz en dış halkasındadır; tüm ize oranı sektör sayısı arttıkça azalır.

`cd-generate`, iz yanında `<track>.json` yazar (`format: cd-da-track-v1`; r0/r1/sektör sayısı/doğrusal hız/başlangıç açısı/görüntü dış yarıçapı/iç içe geçirme durumu). Önizleme iz değişiminde bunu okuyup kalibre edilmiş geometriyi ve iç içe geçirme anahtarını yükler; başka makinelerde üretilmiş izler de doğru modelle geri okunabilir.

## Kalibrasyon izdüşümü: kaynak görüntü ve iki geometri

`cd-preview-warp`, geri okuma yolunun hızlı karşılaştırmasıdır ve Önizlemenin “Kalibrasyon önizlemesi” modunun çağırdığı komuttur. Girdi ses izi değil kaynak görüntüdür; iki geometri kullanılır. Üretim geometrisi bayt i'nin görüntüdeki örnekleme konumunu, ölçülmüş geometri ise diskte çizileceği yeri belirler (`--actual-r0`, `--actual-r1`; varsayılan 24.3 / 56.6 mm). Geometriler aynıysa izdüşüm özgün görüntüdür; farklıysa desen ölçülmüş diske göre kayar ve yarıçap kalibrasyonunun göstermesi gereken budur. Sektör sayısı, doğrusal hız, başlangıç açısı ve görüntü dış yarıçapı ortaktır; son üçü 1200 mm/s, 0°, 57.5 mm olarak sabittir.

Yalnızca kaynak görüntü okunduğu, tüm iz taranmadığı için yarıçap veya geometri değişimi sonrası yenileme maliyeti iz boyutuna (80 dakikalık disk yaklaşık 845 MB) değil görüntü boyutuna bağlıdır. Kalibrasyon önizlemesinde parametre denemek, ardından son diski geri okuma benzetimiyle doğrulamak için uygundur.

## Doğrulama ve sınırlar

- RAW ve WAV: asimetrik desende iç içe geçirme açıkken de kapalıyken de WAV data alanı, eski RAW'ın her 16 bitlik çiftinin değiştirildiği bayt dizisiyle tam aynıdır; uzunluk değişmez.
- Akış çıktısı: Seek/Position yasaklayan ileri akışta küçük uçlu ses üretimi, WAV data alanıyla bayt bayt aynıdır ve başlık içermez.
- WAV önizlemesi: RIFF `fmt ` / `data` ayrıştırılır; bilinmeyen parçalar ve tek uzunluklu dolgu atlanır, okuma aralığı sınırlanır ve bayt sırası normalleştirilir. PCM olmayan, 44.1 kHz olmayan, 16 bit olmayan veya stereo olmayan veri reddedilir. Farklı örnekleme adımlarında karşılık gelen RAW önizlemesiyle aynıdır. Önizleme dosya baytlarının eşlemesidir; tam fiziksel CIRC/EFM benzetmez ve gerçek disk değerlendirmesinin yerine geçmez.
- Geri okuma tutarlılığı: aynı kaynaktan iç içe geçirilmiş ve geçirilmemiş izler üretilir. İlkini `--deinterleave true` ile, ikincisini dosya sırasında okumak gecikme hattının son bir sektörü içindeki pikseller dışında aynı sonucu verir (900 px asimetrik testte fark yalnızca son izdüşümdedir; çerçeve gecikmesini 24 bayt değiştiren mutasyon testi 245 uyumsuzluk bildirir).
- Kalibrasyon izdüşümü tutarlılığı: aynı kaynakla `(24.5, 56.8)` üretim geometrisinde iz üretilir; `cd-preview-warp` izdüşümü (ölçüm=üretim ve ölçüm=`24.3/56.6`), karşılık gelen geometrilerle `cd-preview-track` geri okumasıyla piksel piksel karşılaştırılır. Eşikleme sonrası 900 px'te tam aynıdır. Ölçülmüş yarıçap azalınca izdüşüm ağırlık merkezi yarıçapı da azalır; iki geometri yer değiştiremez.
- Windows bellek deneyi: mevcut `MsftRawCDImageCreator` içine 300 asimetrik ses sektörü verilip 2448 baytlık RAW sonuç sektörlerinin tamamı taranır; tüm ses baytları değişmeden korunur. Üretici çağıran adına bayt değiştirmez. Test yazıcı cihazı oluşturmaz, diske yazmaz.
- 2026-09-12'de kullanıcı bu düzeltmeden sonra gerçek disk deneyi yapıp çift görüntünün kaybolduğunu bildirmiştir. Bu kullanıcıdan gelen fiziksel disk sonucudur; akış, cdrecord ve ImgBurn yollarının ayrı ayrı ölçüm ayrıntıları kaydedilmemiştir.

Microsoft `WriteMedia` belgesi iç bayt değiştirme uygulamasını açıklamaz. Yukarıdaki sonuçlar bellek deneyini cihaz aktarımındaki değişimin gözlenmesiyle eş tutmaz. [Microsoft WriteMedia belgesi](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscformat2rawcd-writemedia)
