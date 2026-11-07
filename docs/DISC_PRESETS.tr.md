# Düzenlenebilir disk önayarları

[简体中文](DISC_PRESETS.md) | [繁體中文](DISC_PRESETS.zh-TW.md) | [English](DISC_PRESETS.en.md) | [日本語](DISC_PRESETS.ja.md) | [Türkçe](DISC_PRESETS.tr.md)

## Kullanılabilir kapasite ve kayıtlı önayarların uygulanması

Geometri sektörleri tam spirali belirler; isteğe bağlı `usableSectors` çıktı uzunluğunu, dosya sistemi kapasitesini ve kullanılabilir dış yarıçapı sınırlar. Açık değerlerden yeniden pay düşülmez. Alanı olmayan eski önayar/projelerde CD toplamdan 2 çıkarır, DVD toplamdan 608 çıkarıp 16’ya aşağı hizalar, BD ise 32’ye aşağı hizalar. Yerleşik önayarlar açık kullanılabilir sınır içerir. DVD geometri sektörleri hizasız olabilir; çıktı hizalı olmalıdır.

Mevcut projede önayar kaydetmek onu hemen uygular, yeni önayarı seçer ve ölçülen önizleme yarıçapı/iz aralığını eşitler. Ardından ayar penceresini iptal etmek kaydedilen değerleri korur, sonraki onaylanmamış düzenlemeleri atar. Yalnızca ad değiştirmek elle ölçülen değerleri korur. Yeni Proje’den kayıt mevcut projeyi değiştirmez; aynı adlı önayarlar farklı ID’ler taşır.

Disk önayarları iki katmandan oluşur:

- Yerleşik katman uygulamayla dağıtılır ve güncellenir; resmî önayarları saklar.
- Kullanıcı katmanı aşağıdaki dosyadadır; yalnızca kullanıcı eklemelerini, geçersiz kılmalarını ve devre dışı bırakma ayarlarını saklar:

```text
%LOCALAPPDATA%\DiscImageStudio\disc-presets.json
```

İlk açılışta uygulama kullanıcı katmanına üç düzenlenebilir önayar ekler: “Özel CD parametreleri”, “Özel DVD parametreleri” ve “Özel Blu-ray parametreleri”. Yerleşik önayarlar kullanıcı dosyasına kopyalanmaz; böylece uygulama güncellemelerindeki parametre düzeltmeleri ve yeni önayarlar otomatik olarak uygulanır.

Kullanıcı önayarı ekleme noktası proje ayarları iletişim kutusudur: “Bu parametreleri önayar olarak kaydet” önce ayrı pencerede adı onaylatır, sonra geçerli parametreleri kullanıcı katmanına ekler. Yeni proje şablon listesi katalogdaki tüm elle giriş dışındaki önayarları “Son”, “CD”, “DVD” ve “Blu-ray” gruplarında gösterir.

Proje ayarları iletişim kutusu geçerli disk türünün önayarlarını da listeler. Başka bir önayar seçmek iletişim kutusundaki geometri parametrelerinin üzerine yazar; bu nedenle geçişten önce onay istenir. İptal, özgün parametreleri ve seçimi korur. Hem yeni proje hem proje ayarlarındaki “Özel” önayarlar sağ tıklama menüsünden silinebilir; silme kullanıcı katmanına hemen kaydedilir ve liste yenilenir. JSON doğrudan düzenlenerek ekleme, yeniden adlandırma veya devre dışı bırakma yapıldığında yine uygulamayı yeniden başlatmak gerekir.

## Birleştirme kuralları

- Yeni bir `id` kullanan kullanıcı önayarı yerleşik önayarların sonuna eklenir.
- Yerleşik önayarla aynı `id` değerini kullanan kullanıcı önayarı, yerleşik önayarın tamamını geçersiz kılar.
- `disabledCdPresetIds`, `disabledDvdPresetIds` ve `disabledBluRayPresetIds`, istenmeyen yerleşik veya kullanıcı önayarlarını gizleyebilir.
- Yerleşik önayar güncellendiğinde, aynı ID'ye sahip kullanıcı girdisiyle geçersiz kılınmamış parametreler otomatik güncellenir.

## DVD önayarı ekleme

`dvdPresets` dizisinde başlatılmış kullanıcı nesnesini kopyalayın; `id`, ad ve parametrelerini değiştirin. Örneğin:

```json
{
  "id": "verbatim-dvd-r-azo-43533",
  "displayName": "Verbatim DVD-R AZO (43533) (ölçülmüş)",
  "description": "Doğrulanmış Verbatim DVD-R AZO 43533 kalibrasyon parametreleri: 23,9914606277–57,9648936579 mm, 2.297.888 sektör; kübik iz aralığı dağılımı.",
  "totalSectors": 2297888,
  "innerRadiusMm": 23.991460627731737,
  "outerRadiusMm": 57.964893657925366,
  "channelBitLengthNm": 133.3,
  "pitchLinear": -3.9256854319227684E-05,
  "pitchQuadratic": 6.964795915429256E-05,
  "pitchCubic": -4.670918950137523E-05
}
```

DVD `totalSectors` değeri, ECC Block ve akışla yazdırma düzeni gereksinimleri için 0'dan büyük ve 16'nın katı olmalıdır. `channelBitLengthNm` ayrılmış bir alandır; aşağıdaki bölüme bakın.

Bu Verbatim DVD önayarı, kullanıcının doğrulandığını onayladığı `calibration-f2f851f7.calibration.json` kaydından gelir ve kayıttaki tam parametre hassasiyetini korur.

İsteğe bağlı `contributor` alanı bu ölçülmüş parametreleri sağlayan katkıda bulunanı kaydeder. Doldurulduğunda önayar kartının sol üstünde filiz simgesi görünür; üzerine gelindiğinde hemen “Katkıda bulunan: …” gösterilir. Alan atlanır veya boş bırakılırsa simge görünmez; yalnızca boşluk içeren değerler doğrulamada reddedilir. Örneğin yerleşik `philips-dvd-plus-r`:

```json
{
  "id": "philips-dvd-plus-r",
  "displayName": "PHILIPS DVD+R (ölçülmüş)",
  "description": "Ölçülmüş PHILIPS DVD+R kalibrasyon parametreleri: 24,157724–57,881039 mm, 2.295.104 sektör; kübik iz aralığı dağılımı.",
  "totalSectors": 2295104,
  "innerRadiusMm": 24.157723798864616,
  "outerRadiusMm": 57.881039159257099,
  "channelBitLengthNm": 133.3,
  "pitchLinear": -0.00015487792452410765,
  "pitchQuadratic": 0.0001962455822194293,
  "pitchCubic": -8.6683602888291885E-05,
  "contributor": "DBPolp"
}
```

`pitchLinear`, `pitchQuadratic` ve `pitchCubic`, kübik iz aralığının üç göreli şekil katsayısıdır. Varsayılanları 0'dır; eski önayarlarla uyumludur. Model `p(x)=p0*(1+pitchLinear*x+pitchQuadratic*x²+pitchCubic*x³)`, konum ise `x=(r-innerRadiusMm)/(outerRadiusMm-innerRadiusMm)` şeklindedir. Tam polinom dört terimlidir; `p0` toplam iz uzunluğundan otomatik hesaplanır ve arayüz dört terimin gerçek μm değerlerini gösterir. İz aralığı tüm `0≤x≤1` aralığında kesinlikle pozitif olmalıdır; yalnızca iç ve dış sınırları kontrol etmek yeterli değildir. Herhangi bir katsayıyı değiştirmek kaydedilmemiş özel duruma geçer; yerleşik sabit iz aralığı önayarını yeniden seçmek katsayıları sıfıra döndürür.

Yerleşik ölçülmüş DVD önayarları arasında `verbatim-dvd-plus-r-azo-43498` (Verbatim DVD+R AZO (43498), katkıda bulunan `Disappear9`), `ritek-dvd-plus-r-e-series` (Ritek DVD+R E Serisi, katkıda bulunan `EternalEve`) ve `ritek-dvd-r-e-series` (Ritek DVD-R E Serisi, katkıda bulunan `bczhc`) de vardır.

`verbatim-dvd-plus-r-azo-43498` parametreleri: iç yarıçap `23.954070288547207` mm, dış yarıçap `57.981456053451275` mm, `2,295,104` sektör; kübik şekil katsayıları `-1.1055153059816393E-05`, `-1.7233535436312236E-06`, `5.55123860136712E-06`. `ritek-dvd-r-e-series` parametreleri: iç yarıçap `24.063109184084034` mm, dış yarıçap `58.23373375457525` mm, `2,298,496` sektör; kübik şekil katsayıları `-3.2229430281730407E-06`, `1.0507210681810583E-05`, `-8.562064227346657E-06`. İkisi de kaynak değerlerin tam hassasiyetini korur.

## CD önayarı ekleme

`cdPresets` dizisinde mevcut bir nesneyi kopyalayın; `id`, ad, sektör sayısı ve iç/dış yarıçaplarını değiştirin. Örneğin:

```json
{
  "id": "cd-74",
  "displayName": "Standart 74min CD",
  "description": "ECMA-130 / ISO/IEC 10149 standart hızı 1200 mm/s; 120 mm disk, yaklaşık 74 dakika, 333.000 sektör (24,5–56,8 mm).",
  "sectors": 333000,
  "innerRadiusMm": 24.5,
  "outerRadiusMm": 56.8,
  "linearVelocityMmPerSecond": 1200
}
```

## Blu-ray önayarı ekleme

`bluRayPresets` dizisinde mevcut bir nesneyi kopyalayın; `id`, ad ve parametrelerini değiştirin. Örneğin:

```json
{
  "id": "bd-r-ritek-10x",
  "displayName": "Ritek BD-R 10x (ölçülmüş)",
  "description": "Ölçülmüş parametreler: iç yarıçap 23,988221 mm, dış yarıçap 58,023235 mm, 12.219.392 sektör.",
  "totalSectors": 12219392,
  "innerRadiusMm": 23.98822105616562,
  "outerRadiusMm": 58.023234731100764,
  "invertMapping": false
}
```

BD `totalSectors` değeri 0'dan büyük, 32'ye hizalı ve en fazla tek katman kapasitesi olan 12.219.392 olmalıdır; dış yarıçap 60 mm'yi aşmamalıdır. `invertMapping`, önayarlı 3T/6T kutupluluğudur; projeye uygulandıktan sonra tasarım sayfasının ton modülünde yine değiştirilebilir.

Yerleşik `bd-r-verbatim-64099` (Verbatim BD-R (64099)), kullanıcının `disc-presets.json` dosyasında kayıtlı “Verbatim 64099” önayarını kullanır ve tam hassasiyeti korur: iç yarıçap `23.987042157339335` mm, dış yarıçap `57.987960341783236` mm, `12,219,392` sektör, `invertMapping: false`. GUI ve komut satırı aynı parametreleri kullanır.

Yerleşik `bd-r-verbatim-64293` (Verbatim BD-R (64293)), katkıda bulunan `bczhc` tarafından sağlanmıştır; parametreleri 64099 ile aynıdır ve aynı hassasiyeti korur.

Yerleşik `bd-r-ritek-12x` (Ritek BD-R 12x), katkıda bulunan `DeepAQ` tarafından sağlanmıştır: iç yarıçap `23.98795` mm, dış yarıçap `58.02236` mm, `12,219,392` sektör, `invertMapping: false`. GUI ve komut satırı aynı parametreleri kullanır.

## Sabit parametreler ve ayrılmış alanlar

Tarama hızı, başlangıç açısı, görsel dış yarıçapı ve DVD kanal bit uzunluğu bu yaklaşımda sabit fiziksel büyüklüklerdir. Arayüz bunlar için form alanı sunmaz; üretim her zaman 1200 mm/s (ECMA-130), 0°, 57,5 mm ve 133,3 nm kullanır.

Eski yapılandırma dosyaları bu parametreleri gerektirdiği ve eski sürümlerin yazdığı kullanıcı dosyalarının doğrudan kullanılabilmesi gerektiği için alanlar JSON içinde korunur. Bu ayrılmış değerler üretim hesaplamasına katılmaz.

## i18n uyumluluk alanları

Masaüstü uygulaması önce kayıtlı `displayNameResourceKey` ve `descriptionResourceKey` değerlerini çözümler; kaynak anahtarı yoksa mevcut metne döner. Değiştirilmemiş yerleşik ad ve açıklamalar da çevrilir; kullanıcıların yazdığı ad ve açıklamalar özgün metin olarak korunur. Uygulamanın kaydettiği geometri açıklamaları `DiscPreset.UserDescription` biçim anahtarını kullanır; görüntülenirken önayar yarıçapları ve sektör sayısına göre çevrilir. Eski önayarlardaki açıklamalar da otomatik üretilen biçimle tam eşleşiyorsa tanınır. Açıklama elle değiştirildikten sonra, biçim anahtarı hâlâ bulunsa bile özgün metin korunur. Kullanıcı önayarları kaynak anahtarı belirtmek zorunda değildir; `fallbackLanguage` isteğe bağlı kaynak dili meta verisi olarak kalır ve arayüz dili seçimini değiştirmez. İlk kullanıcı önayarları da ortam ailesi ve ID eşleştiğinde ve metin düzenlenmediğinde varsayılan metinlerini görüntüleme sırasında çevirir.

## Düzenleme kuralları

- Geçerli üst düzey `"schemaVersion": 4` şeklindedir; `cdPresets`, `dvdPresets`, `bluRayPresets`, üç devre dışı bırakma listesi ve isteğe bağlı `fallbackLanguage` içerir. Uygulama `"schemaVersion": 2` ve `3` değerlerini de kabul eder; BD kataloğu yoksa yerleşik BD şablonlarından tamamlanır ve kaydetmede 4'e yükseltilir.
- Her önayar grubundaki `id` değerleri benzersiz olmalıdır. `__manual__`, arayüzün “kaydedilmemiş özel parametreler” durumu için ayrılmış ID'dir ve JSON'a yazılamaz.
- Yalnızca şablonda listelenen üretim parametrelerine izin verilir; görsel yerleşimi veya kalibrasyon parametrelerinin disk önayarlarına karışmasını önlemek için diğer alanlar reddedilir.
- Yarıçaplar, `linearVelocityMmPerSecond` ve `channelBitLengthNm`, 0'dan büyük sonlu değerler olmalıdır; üretim dış yarıçapı üretim iç yarıçapından büyük olmalıdır.
- Dosya `//` veya `/* ... */` yorumlarını ve dizi sonundaki virgülleri kabul eder.
- Biçim veya parametreler geçersizse uygulama sorunu bildirir ve son başarıyla yüklenen önayarları korur; kullanıcı dosyasının üzerine otomatik yazmaz.
