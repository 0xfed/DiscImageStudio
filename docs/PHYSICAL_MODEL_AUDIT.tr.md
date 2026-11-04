# CD / DVD üretim fiziksel modelinin incelemesi

[简体中文](PHYSICAL_MODEL_AUDIT.md) | [繁體中文](PHYSICAL_MODEL_AUDIT.zh-TW.md) | [English](PHYSICAL_MODEL_AUDIT.en.md) | [日本語](PHYSICAL_MODEL_AUDIT.ja.md) | [Türkçe](PHYSICAL_MODEL_AUDIT.tr.md)

İnceleme tarihi: 2026-09-21. İncelenen konu, henüz işlenmemiş kalibrasyon uygulamasını içeren, `6c55a18` işlemini temel alan mevcut `DiscImageStudio-GitHub` çalışma alanıdır. Bu incelemede yalnızca standart karşılaştırması, kaynak kodu denetimi ve bağımsız sayısal doğrulama yapıldı; üreticiler, önayarlar veya yazdırma davranışı değiştirilmedi.

## Sonuçlar

Temel spiral formülünde ve DVD sektör uzunluğunda hesaplama hatası bulunmadı. Ancak mevcut model, yalnızca standartların nominal parametreleriyle gerçek disk yüzeyindeki açıları kesin olarak öngöremez: varsayılan CD parametrelerinde geometrik tutarlılık sorunu vardır; sabit doğrusal yoğunlukla yapılan tek çizgili kalibrasyon, yoğunluk hatalarını yarıçaplara yansıtır; CD serpiştirmesinin sonlu akış sınırları ve referans zamanlaması da geometri modelinde yer almaz. Yazdırma ortamı türünün üretim modeliyle eşleştiğini denetleyen bir kontrol de eksiktir.

Yalnızca bu denetimle belirli bir gerçek diskteki eğriliğin tek nedeni belirlenemez. DVD bit uzunluğunu doğrudan başka bir nominal değere değiştirmek de çözüm olarak görülmemelidir.

## Standartlar ve kapsam

- Resmî [ECMA-359, birinci baskı, 2004](https://ecma-international.org/wp-content/uploads/ECMA-359_1st_edition_december_2004.pdf): tek katmanlı DVD-R; başlıca §§10.7–10.9, 18–23 ve 24.2 incelendi. PDF'nin 28, 29, 52 ve 56. özgün sayfaları kontrol edildi.
- Resmî [ECMA-267, üçüncü baskı, 2001](https://ecma-international.org/wp-content/uploads/ECMA-267_3rd_edition_april_2001.pdf): DVD-ROM; başlıca §§10.6 ve 18–21 incelendi. Kaydedilebilir ortamların tüm kurallarının yerine kullanılamaz.
- Resmî [ECMA-130, ikinci baskı, 1996](https://ecma-international.org/wp-content/uploads/ECMA-130_2nd_edition_june_1996.pdf): CD-ROM geometrisi ve ortak CIRC kodlama referansı; §§8.6, 11.3–11.4, 19.4 ve Ek C incelendi. Ses izi, CD-ROM veri iziyle aynı değildir.
- Resmî [ECMA-394, birinci baskı, 2010](https://ecma-international.org/wp-content/uploads/ECMA-394_1st_edition_december_2010.pdf): CD-R; çok hızlı kayıt, önceden açılmış oluklar ve Red Book uyumluluğu; §§1.2 ve 2.4–2.5 incelendi. Belge, bazı geometrik gereksinimleri Red Book'a atıfla belirtir; tam CD geometri tablosunu doğrudan yeniden vermez.

ECMA-359'un temel kısıtları aşağıdadır. Bunların tümü standart gereksinimleridir; belirli bir diskin ölçülmüş değerleri değildir:

| Öğe | Kısıt | Madde |
|---|---|---|
| Veri başlangıç yarıçapı | 23,9–24,0 mm | 10.7.1.2; özgün metin 48,0 mm çap ve −0,2/+0,0 mm tolerans belirtir |
| 12 cm diskte veri bitiş yarıçapı | En fazla 58,0 mm | 10.7.1.2 |
| Ortalama iz aralığı | 0,74 ± 0,01 μm | 10.8 |
| Yerel iz aralığı sapması | 0,74 μm'ye göre en fazla ±0,03 μm | 10.8 |
| Ortalama kanal bit uzunluğu | 133,3 ± 1,4 nm | 10.9 |
| Veri alanının ilk PSN değeri | 0x030000; fiziksel sektörler boşluksuz süreklidir | 24.2 |

ECMA-267'nin veri başlangıç çapının alt sınırı DVD-R'den farklıdır: §10.6.1, 48,0 mm çap ve −0,4/+0,0 mm tolerans belirtir. İki standardın başlangıç toleransları karıştırılmamalıdır.

## Doğru olduğu doğrulanan bölümler

DVD'nin sektör başına kanal bit sayısı `13 × 2 × (32 + 1456) = 38688`, 16 sektörlük ECC bloğu başına bit sayısı ise 619008'dir. `DvdEccBlockEncoder.cs:8` içindeki değer doğrudur; sektör ofsetlerinde, eşzamanlama çerçevelerinde ve PO satırı serpiştirmesinde genel bir uzunluk eksikliği bulunmadı.

Mevcut iz uzunlukları şöyledir:

```text
CD:  L = (N / 75) × v                         [mm]
DVD: L = N × 38688 × channelBitLengthNm × 10^-6 [mm]
```

`CdDiscParameters.cs:59–75` sabit iz aralığına dayalı alan yaklaşımını, `ArchimedeanSpiral.cs:33–70,115–120` ise kesin yay uzunluğunu kullanır. Yaklaşık bağıntılar `pitch = π(ro²−ri²)/L` ve `θ = 2L(r−ri)/(ro²−ri²)` şeklindedir. CD yaklaşımıyla kesin spiral arasındaki tüm disk açısal farkı yaklaşık 0,000135°'dir; bu, gözle belirgin eğriliği açıklayamaz. DVD'nin kesin yay uzunluğundan ters çözümünde, nanometreden milimetreye dönüşümünde ve varsayılan görsel koordinatı yönünde de hata bulunmadı.

DVD-R linking, belirli artımlı ekleme durumlarında kullanılır; §24.2 sektörlerin sürekli olduğunu açıkça belirtir. Her ECC bloğuna aynı sözde gizli boşluğu eklemek için dayanak yoktur.

## Bulgu 1: varsayılan 80 dakikalık CD önayarının parametre birleşimi tutarsızdır

`src/DiscImageStudio.Core/Presets/built-in-disc-presets.json:6–12`, 359849 sektör, 24,5–56,8 mm ve 1200 mm/s değerlerini birleştirir. Gerçek üretim formülünden ters hesaplama şu sonucu verir:

| Önayar | Örtük iz aralığı (μm) |
|---|---:|
| Varsayılan 80 dakikalık CD | 1,432856 |
| Varsayılan 74 dakikalık CD | 1,548384 |
| Ritek camgöbeği CD ölçülmüş yarıçap önayarı | 1,492598 |
| Verbatim CD ölçülmüş yarıçap önayarı | 1,500257 |
| Varsayılan 12 cm DVD | 0,739837 |
| Verbatim DVD-R ölçülmüş yarıçap önayarı | 0,738304 |

ECMA-130 §11.3, 1,5–1,7 μm iz aralığı; §11.4 ise 1200–1400 mm/s referans tarama hızı aralığı belirtir. Varsayılan CD80 birleşimi, bu geometri aralığına uyan genel ve kesin bir önayar olarak kullanılamaz. 24,5 mm başlangıcı da §8.6'daki kullanıcı veri alanı başlangıcıyla (24,8–25,0 mm) doğrudan eş tutulamaz.

Bu, yazılımın parametre birleşimini inceler; kullanıcının CD-R diskinin uygunsuz olduğunu göstermez. Ölçülmüş olarak işaretlenen önayarlar da hızı 1200 mm/s'de sabitler; bu, doğrusal yoğunluğun da ölçüldüğü anlamına gelmez. Kullanıcının ölçtüğü yarıçaplar yalnızca standart aralığına uydurmak için keyfî olarak değiştirilmemelidir.

## Bulgu 2: standart toleransları çizim için gereken birikimli açı hassasiyetinden çok büyüktür

`ImageTargetMapper.cs:58–77` ve `FastDispersionImageWriter.cs:345–365`, kapasite, iki yarıçap ve sabit bit uzunluğundan sabit iz aralığını ters hesaplar. Varsayılan DVD parametreleri yaklaşık 45956,06 tur üretir. Standart yerel iz aralığı değişimine izin verir ve 58 mm bir üst sınırdır; dolayısıyla bu girdiler yalnızca başlangıç modeli oluşturabilir.

Sayısal duyarlılık; yalnızca bir girdinin değiştirildiği, diğerlerinin sabit tutulduğu model karşılaştırmaları:

- DVD bit uzunluğunun 10 ppm değişmesi, tüm diskteki birikimli açıyı yaklaşık 165,44° değiştirir.
- DVD bit uzunluğunun 133,33'ten 133,3 nm'ye değişmesi, birikimli açıyı yaklaşık 10,34037 tur değiştirir.
- DVD iç veya dış yarıçapının 0,001 mm artırılması, birikimli açıyı yaklaşık −201,76° değiştirir.

Bu değerler ölçülmüş gerçek disk hataları değil, model faz farklarıdır. 133,33 nm standart toleransı içindedir; 133,3 nm de her diskin gerçek ve kesin bit uzunluğu değildir. Nominal değeri körlemesine değiştirmek üretim sonucunu önemli ölçüde değiştirebilir.

## Bulgu 3: tek bir radyal çizgi yarıçapları ve doğrusal yoğunluğu bağımsız olarak belirleyemez

`CalibrationTarget.cs:468–473` doğrusal hızı / bit uzunluğunu sabitler; `CalibrationCrossFitter.cs:73` yalnızca iki yarıçapı ve genel dönüşü uyarlar. Mevcut akış, eş merkezli çemberlerin kaynak yarıçapı eşleşmelerini kullanmaz.

Bağımsız karşı örnek: üretim modeli DVD `(ri=24, ro=58, bit=133.33)`, varsayılan gerçek model ise `(24,58,133.3)` olsun. Uyarlama bit uzunluğu 133,33'te sabit tutulup yalnızca aday yarıçaplar yaklaşık `(24.00540257,58.01305620)` olarak değiştirilir ve genel dönüş ayarlanırsa, 25–57 mm aralığında aynı eğri neredeyse tam olarak yeniden üretilebilir (2049 noktalı kesin yay uzunluğu hesabında en büyük faz artığı yaklaşık 2,04×10^-10 rad). Aday, varsayılan ±0,02 mm arama aralığındadır. Bu, analitik olarak oluşturulmuş adayın ileri yönde doğrulanmasıdır; mevcut eniyileyicinin mutlaka bu adayı döndüreceği iddia edilmemektedir.

Dolayısıyla küçük artık, bit uzunluğu hatasının yalnızca yarıçap hatası olarak açıklanması olabilir. Mevcut özellik yararlı telafi parametreleri sağlayabilir, ancak sonuç “sabit doğrusal yoğunluk varsayımı altında eşdeğer parametreler” olarak anlaşılmalıdır. Gerçek fiziksel yarıçap ve yoğunluğu belirlemek için kaynak yarıçapı bilinen halkalar, bilinen veri konumları veya bağımsız sınır ölçümleri eklenmelidir. Bir uyarlama parametresini daha serbest bırakmak bilgi eksikliğini gidermez.

## Bulgu 4: CD serpiştirme telafisinde sonlu akış sınırı etkileri vardır; fiziksel zamanlama başlangıcı henüz doğrulanmamıştır

`CddaInterleaver.cs:8–16` içindeki gecikme tablosu ECMA-130 Şekil C.4'ün veri yollarıyla eşleşir; hatalı yinelenen serpiştirme değildir. Bağımsız etiket simülasyonu, mevcut telafiden geçmiş PCM'yi standart ileri CIRC'nin 24 veri yolundan geçirir: 10 sektörlük testte kararlı durumdaki 20856 veri konumunun tümü eşleşir, ancak birleşik sonuç tüm hedef deseni 111 çerçeve geciktirir.

İki konu kesin olarak ayrılmalıdır:

- **Kesin sonlu akış davranışı:** başlatma, 1332 sıfır yer tutucu bayt üretir; son 108 çerçeveye dağılmış 1332 hedef bayt çıkışa verilmez. `CddaInterleaver.cs:46–55` içindeki Flush yalnızca sektör sonunu işler; tam sektör girdisinde halka arabelleğini boşaltmaz. Bu, desen sınırlarını etkiler.
- **Koşullu fiziksel ofset:** 111 çerçeve, Şekil C.4'ün referans zamanlamasındaki birleşik gecikmedir; yaklaşık 15,102 ms veya 1200 mm/s'de 18,122 mm yay uzunluğudur. Yazdırma zincirinin zamanlama başlangıcı bunu dengelemezse açısal fark yarıçapla değişir ve yalnızca başlangıç açısıyla giderilemez. ATIP, INDEX 01 ve gerçek diskin veri başlangıcına göre ilişkisi ölçülmediğinden doğrudan kanıtlanmış fiziksel disk ofseti olarak sunulamaz.

`CdTrackGenerator.cs:229–246`, yukarıdaki zamanlamayı içermeyen globalByte üzerinden örnekler; geometri kalibrasyonu da bu kodlama aşamasını içermez. Mevcut uyumluluk belgesi de önizlemenin tam CIRC/EFM benzetimi yapmadığını belirtir. Uçtan uca fiziksel zamanlama doğrulaması eklenmelidir; aynı modele dayanan üretim ve önizlemenin uyuşması tek başına disk yüzeyindeki konumların doğruluğunu kanıtlamaz.

## Bulgu 5: yazdırma ortamı türü modelle eşleştirilmiyor

`WindowsImapiBurner.cs:151–166,377–390` yalnızca IMAPI desteğini, boş durumunu ve kapasiteyi kontrol eder; gerçek fiziksel ortam türünü sorgulamaz. Buna rağmen üretici tek katman kimliği (`DvdDataFrameBuilder.cs:41`) ve tek katmanlı, tek yönlü spiral kullanır.

Çift katmanlı DVD ve DVD-RAM gibi farklı fiziksel biçimlerin önceden engellenmesi eksiktir. IMAPI'nin bir ortama yazabilmesi, mevcut çizim modelinin uygun olduğu anlamına gelmez; yazdırmanın gerçekten başlayabilmesi yine cihaza ve duruma bağlıdır. Çift katmanda katman geçişi, ikinci katmanın yönü ve bit uzunluğu modellenmemiştir.

Eşleştirme kontrolü için Microsoft'un [CurrentPhysicalMediaType](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscformat2data-get_currentphysicalmediatype) özelliği ve [fiziksel ortam numaralandırması](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/ne-imapi2-imapi_media_physical_type) kullanılabilir.

## Önerilen düzeltme sırası

1. Önayarlara örtük iz aralığı kontrolü ekleyin; standart başlangıç değerlerini, ölçülmüş değerleri ve telafiyle elde edilen değerleri ayırın. Varsayılan CD80 birleşimine öncelik verin. Başlangıç ve bitiş yarıçaplarının hangi veri konumlarına karşılık geldiğini belirtin; çizim kırpma aralığını iz referans noktalarından ayırın.
2. CD serpiştirmesi, iz başlangıcı ve yazdırma zincirinin ortak zamanlamasını doğrulayın; ardından nihai sektör sayısını değiştirmeden örnekleme ofsetlerini ve baş/son stratejisini düzeltin.
3. Kalibrasyona bağımsız radyal veya veri konumu kısıtları ekleyin. Ölçülmüş artıklarda hâlâ sistematik yerel eğrilik varsa değişken iz aralığı / faz düzeltme modellerini değerlendirin.
4. Yazdırmadan önce gerçek ortam türünün ve kapasitesinin seçilen modelle eşleştiğini kontrol edin.

Bu incelemede fiziksel disk yazdırması veya optik parlaklık ölçümü yapılmadı. Doğrulama; standartların özgün metin ve sayfalarını, formüllerin yeniden hesaplanmasını, kesin spiral sayısal karşılaştırmalarını, parametrelerin ayırt edilememesine ilişkin karşı örneği ve CIRC veri yolu etiket simülasyonunu içerir. Bu kontrollerin tam optik benzetim veya fiziksel disk hassasiyeti sertifikası oluşturduğu iddia edilmemektedir.

Denetimi yeniden üretme malzemeleri çalışma alanı kökündeki `tmp/standards-audit/` dizinindedir: `circ-audit.py`, `circ-audit-result.json`, `geometry-audit.py`, `geometry-audit-result.json` ve kullanıcıya ait iki PDF'den çıkarılan metinlerle önemli özgün sayfalar.
