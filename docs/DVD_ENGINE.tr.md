# DVD NRZI görüntü kısıtı çözücüsü

[简体中文](DVD_ENGINE.md) | [繁體中文](DVD_ENGINE.zh-TW.md) | [English](DVD_ENGINE.en.md) | [日本語](DVD_ENGINE.ja.md) | [Türkçe](DVD_ENGINE.tr.md)

Proje, ardışık 16 DVD veri sektörünü bir ECC Block olarak ele alır; iki boyutlu görüntüye veya belirtilen NRZI pit/land kısıtlarına göre okunabilir kullanıcı verisi arar.

Sabit kurallar:

- Tek katmanlı disk Data Zone alanı; Sector Information değeri `0x00`;
- DVD-R Data Frame içindeki RSV, altı `0x00` baytıdır;
- Yalnızca ECC Block başlangıç LBA değeri girilir; sonraki sektörler otomatik olarak `LBA + 1 ... LBA + 15` kullanır;
- `PSN = PSN offset + LBA`, varsayılan offset `0x30000`;
- Mutlak DSV değerleri eşitse Stream 1 seçilir;
- `pit = 0` (siyah), `land = 1` (beyaz);
- Her çıktı 32768 bayttır; ardışık 16 adet 2048-byte payload içerir.

Kodlama zinciri ID, IED, tümü sıfır RSV, EDC, veri karıştırma, RS-PC PO/PI, PO satırı serpiştirme, EFMPlus ana/alternatif tabloları, SYNC/DSV denetimi ve NRZI dönüşümünü içerir.

Bağımsız [gri seviye deney aracı](../tools/DvdGrayProbe/README.tr.md), aynı payload kod sözcüğü merkez koordinatlarında karıştırılmış Q değerini doğrudan belirtmek için `FastDispersionImageWriter.WriteScrambledCodewordPatternStream` kullanır, ardından `P = Q XOR mask` yazar. Sabit gri seviye aralıklarını atlayan bu arayüz, normal görüntü üretim paletini değiştirmeden `64/E4` gibi aday sözcükleri ve ayrık renk taklidi atlaslarını test eder. Hızlı üretim tüm diskin ECC/EFM zincirini yeniden yürütmez, optik parlaklığı tahmin etmez. Regresyonlar, karışık sözcüklerin küçük örneklerinde tam ECC/EFM yürütür; gerçek payload, geometri, paralel tutarlılık ve iptali ayrıca doğrular.

## Derleme ve öz sınama

Aşağıdaki komutlar bağımsız Windows girişini kullanır ve .NET 10 SDK gerektirir. Çalışma zamanı dahil olmayan derlemeyi doğrudan çalıştırmak için .NET 10 Windows Desktop Runtime gerekir. Linux/macOS, ortak CLI üzerinden DVD komutlarını çalıştırabilir; [README.md](../README.tr.md) belgesine bakın:

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
```

Öz sınama kapsamı:

- ECMA-267 modülasyon tablosu örnekleri;
- ID, IED, RSV ve EDC;
- Karıştırma XOR gidiş-dönüşü;
- 208 PI satırı ve 172 PO sütunu için sıfır kalan;
- 619008 fiziksel NRZI seviyesinin sayısı, belirlenimciliği ve RLL(2,10) veri koşuları;
- Hem pit hem land başlangıcında 16-bit kod sözcüklerinin önceden hesaplanan gri seviyesi, bitiş seviyesi ve DSV artışı;
- Rastgele SYNC Frame için sözcükle hesaplanan gri seviye, SYNC seçimi ve son State/DSV/seviyenin bit düzeyindeki kodlayıcıyla eşleşmesi;
- Alternatif SYNC yeniden yürütmesi gerekmediğinde çift akışlı EFMPlus önek durumunun bit düzeyindeki kodlayıcıyla tam eşleşmesi;
- State-control son sınırının tam DVD ileri kodlamasıyla eşleşmesi; çözüm sırasında 619008-bit seviye dizisinin oluşturulmaması;
- Dispersion için `0x92/0xA5` karıştırılmış alan eşlemesi, ters karıştırılmış ISO payload ve son sınırın tam kodlamayla eşleşmesi;
- En küçük ISO9660 tanımlayıcıları, Path Table, kök dizin, payload LBA ve şablon değiştirme sonuçları;
- Yarıçap kalibrasyon PNG önizlemesi ve birden çok ECC Block için kesintisiz ham görüntü çıktısı;
- İç halka ISO9660/Joliet dosyaları, Unicode alt dizinleri ve dış halka çizim verisinin yalıtılarak korunması.

## Hedef dosyaları

Hedef dosyaları fiziksel NRZI seviyelerini karakterlerle gösterir:

| Sembol | Anlam |
| --- | --- |
| `0` veya `P` | pit, siyah |
| `1` veya `L` | land, beyaz |
| `?` | kısıtsız |

Boşluklar ve alt çizgiler yok sayılır. Dosya şunlardan birini içermelidir:

- 38688 sembol: bir fiziksel sektörü kısıtlar; ECC Block içindeki konum `--target-sector 0..15` ile belirlenir;
- 619008 sembol: bütün ECC Block'u kısıtlar.

SYNC, RLL, EDC ve ECC, elde edilebilir desenleri sınırlar; dolayısıyla rastgele bit düzeyindeki görüntüler için genellikle sıfır hatalı çözüm yoktur. `full`, uyuşmayan NRZI bitlerini hedefler. Görüntü `state-control` modu hedefi siyah/beyaz yapar; önce yanlış pit/land çoğunluk yönünü, sonra gerçek land sayısının tam siyah 0 veya tam beyaz 16'ya gri seviye uzaklığını hesaplar. Arama hedefi yalnızca denetlenebilir payload sözcüklerini kullanır; son görünürlük tüm kanal sözcüklerini sayar.

`dispersion`, mutlak pit/land çoğunluğunu denetlemek yerine EFMPlus geçiş yoğunluğuyla iki siyah/beyaz doku oluşturur. Karıştırılmış `Q=0xA5/0x92` baytları bütün durum gösterimlerinde sırasıyla sabit 5/2 geçişe sahiptir. Görüntü parlaklığı 0–127, A5'e; 128–255, 92'ye eşlenir; önizleme parlaklığı 0/255'tir. Yeni görüntüler 66 veya B9'u ayrı gri seviyeler olarak kullanmaz. Tüm NRZI seviyelerini ters çevirmek geçiş konumlarını değiştirmez.

Tam opak, önceden renk taklidi uygulanmış gri görüntü yalnızca `0/255` içeriyorsa (tam siyah veya beyaz olabilir), üretim ve kalibrasyon önizlemesi özgün seviyeleri korumak için en yakın pikseli örnekler. Sürekli gri, renkli veya saydam görüntüler çift doğrusal örnekleme kullanır. Ana uygulama Tasarım sayfasında ölçekler, döndürür ve katmanları birleştirir; üretim, akışla yazma ve önizleme aynı bileşiği ve `ToneProfile` kullanır, ayrıca halka kopyalama veya ön işleme yapmaz. `full` ve `state-control` ikili hedefleri eski örnekleme kurallarını korur.

## Görüntü eşlemesi

PNG, JPEG, BMP, TIFF veya GIF doğrudan girilebilir:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-output solved-disc.iso `
  --output payload.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --start-angle-deg 0 `
  --spiral-direction ccw `
  --image-threshold 128 `
  --alpha-threshold 1 `
  --iterations 10000 `
  --constraint-output mapped-target.txt `
  --channel-output channel.bin
```

`--total-sectors`, diskin bildirdiği toplam 2048-byte veri bloğu sayısıdır; `--total-blocks` takma adı da kabul edilir. Sektör başına 38688 kanal biti ve `--channel-bit-nm` ile toplam iz uzunluğunu belirler. Program iç/dış yarıçap ve iz aralığı eğrisinden aralık ölçeğini çözer.

### Yarıçapla değişen kübik iz aralığı

DVD üretimi, akış çıktısı, görüntü önizlemesi, ISO geri okuma ve yardımcı kalibrasyon, `DvdTrackGeometry` paylaşır. Normalize yarıçap `x=(r-ri)/(ro-ri)` için yerel aralık:

```text
p(x) = a0 + a1*x + a2*x² + a3*x³                 [mm]
     = p0 * (1 + c1*x + c2*x² + c3*x³)
dr/dθ = p(x)/(2π)
```

Tam kübik polinomun dört katsayısı vardır. `--pitch-linear`, `--pitch-quadratic`, `--pitch-cubic`, boyutsuz üç göreli katsayıdır: `c1,c2,c3`. Sabit `a0=p0`, sabit toplam iz uzunluğundan otomatik çözülür; diğerleri `ai=p0*ci` olur. Dört terim birbirinden bağımsız değil, kapasite ve iç/dış yarıçap uzunluk kısıtına bağlıdır. Arayüz dört terimi μm olarak gösterir; değişken `x` değeridir, mm cinsinden mutlak yarıçap değildir.

İz uzunluğu `L=∫sqrt(1+(2πr/p(r))²)dr`, açı `θ(r)=∫2π/p(r)dr` kullanır. Değişken aralıkta sabit aralık açısı `(r-ri)/b` kullanılamaz. Polinomun iç uç değerleri dahil tüm üretim halkasında aralığın kesin pozitif olduğu doğrulanır. Üç terim de sıfırsa özgün sabit aralık modeli kullanılır; eski parametre ve önayarlarda eksik alanların varsayılanı sıfırdır.

Örneğin özgün `solve` komutuna `--pitch-linear 0.02 --pitch-quadratic -0.03 --pitch-cubic 0.015` ekleyin. Bu yalnızca parametre biçimi örneğidir, herhangi bir diskin ölçümü değildir. `calibrate`, üretim eğrisi için aynı seçenekleri; gerçek eğri için `--actual-pitch-linear`, `--actual-pitch-quadratic`, `--actual-pitch-cubic` kullanır; `--generated-pitch-*` takma adlarını da kabul eder. `simulate` içindeki `--pitch-*`, gerçek disk eğrisini tanımlar.

Görüntü ve önizleme JSON'u göreli katsayılar ile temel aralığı kaydeder. Kalibrasyon oturumu göreli katsayıları, iç/dış yarıçapları, sektör sayısını ve bit uzunluğunu saklayarak dört terimi yeniden kurabilir. Fotoğrafın genel dönüş açısı bağımsız uydurulan bir değer olarak kalır.

Koordinat ve örnekleme kuralları:

- Görüntü merkezi disk merkezidir; kısa kenar dış çapa karşılık gelir, uzun kenardaki fazlalık korunur;
- İz iç yarıçapta 0° ile başlar; 0° görüntünün sağıdır. Varsayılan matematiksel yön saat yönünün tersidir; `--spiral-direction cw` saat yönüne çevirir;
- Siyah pit'e, beyaz land'e eşlenir; `--image-threshold` altı siyah sayılır;
- Alpha değeri `--alpha-threshold` altında olan saydam alanlar kısıt oluşturmaz;
- `--lba`, izin ilk veri bloğundan sıfır tabanlıdır; yalnızca oradan başlayan ardışık 16 sektör eşlenir;
- İsteğe bağlı `--constraint-output`, gerçek 619008-symbol doğrusal kısıtı inceleme için dışa aktarır.

## Çözme

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --target target.txt `
  --output payload.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land `
  --target-sector 0 `
  --iterations 10000 `
  --mutation-bytes 4 `
  --seed 1 `
  --channel-output channel.bin
```

Temel parametreler:

- `--lba`: ECC Block başlangıç LBA değeri; 16 LBA listesi değildir;
- `--psn-offset`: ondalık veya `0x` onaltılık; varsayılan `0x30000`;
- `--initial-state`: EFMPlus durumu 1–4;
- `--initial-dsv`: mevcut ECC Block öncesi birikmiş DSV;
- `--initial-level`: mevcut blok öncesi fiziksel `pit` veya `land` seviyesi;
- `--seed-payload`: isteğe bağlı 32768-byte başlangıç çözümü; yoksa belirlenimci rastgele çözüm üretilir;
- `--initial zero`: tümü sıfır başlangıç payload kullanır;
- `--channel-output`: son NRZI seviyelerini de verir; her 8 seviye MSB-first paketlenir.

`--target` veya `--image` seçeneklerinden tam biri kullanılır. Görüntü modu ayrıca `--total-sectors` veya `--total-blocks` gerektirir.

### State-control kod sözcüğü gri seviye çözümü

Payload konumunu sınırlamayan özgün algoritma `--algorithm full` olarak kalır; `exact` de kabul edilir. Eski uzamsal denetim baytı havuzu algoritması kaldırılmıştır. Yeni modu şu parametrelerle açın:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2297888 `
  --output payload.bin `
  --algorithm state-control `
  --bridge-bytes 2 `
  --beam-width 8 `
  --retry-beam-width 32 `
  --retry-frame-fraction 0.25 `
  --batch-frames 8 `
  --batch-candidates 4 `
  --search-lanes 1 `
  --iterations 3
```

State-control, her 91-byte Sync Frame'i bir çözüm parçası sayar. EDC, PI, PO, sabit alanlar ve SYNC tam olarak üretilir; State, DSV ve pit/land sınırlarını iletir. Serbest bayt arama hedefine katılmaz, son tüm disk görünürlük puanına katılır. En iyi siyah/beyaz bayt çiftleri `EFMPlus state × initialLevel × currentDsv` (EFMPlus durumu, giriş seviyesi ve mevcut birikmiş DSV) için hesaplanır. Hedef konumlarda etkin Stream yalnızca kendi tam durumuna uygun siyah veya beyaz baytı dener; her önek yalnızca en iyi `--beam-width` yolu tutar. Görüntü hedefi içeren her serbest parçanın baş ve sonundaki `--bridge-bytes` payload baytı denetim konumudur, ancak bunlar da 256 değeri taramak yerine mevcut DSV çiftinden seçer.

Başlangıçta tüm 65536 adet 16-bit sözcük, hem pit hem land başlangıcında şu değerler için ön işlenir:

- 16 NRZI konumunda land sayısı: 0–16 arasında 17 gri seviye;
- Sözcük sonundaki pit/land;
- Tam DSV artışı;
- RLL sınır denetimi için baştaki ve sondaki sıfır sayısı.

Bayt çifti, olası her EFMPlus gösterimi için gri seviye hatasını ve yazım sonrası `|DSV|` değerini hesaplar. Birleşik maliyet `worstGrayError * 16 + worstOutputAbsDsv` olur: en kötü gri seviye hatasının 16 katı artı en kötü mutlak çıktı DSV değeri. Böylece bir gri seviye 15 DSV biriminden daha önemlidir; mevcut DSV yönü ve büyüklüğü yakın gri seviyeli baytlar arasındaki seçimi değiştirebilir. Yaygın -256..256 DSV çiftleri ön hesaplanır; giriş veya çalışma sırasında sınır dışı değerler kırpılmadan tam DSV ile anında hesaplanır. Buradaki baytlar EFMPlus'a giren karıştırılmış recording-frame baytlarıdır; ISO'nun özgün payload'ı XOR ile ters eşlendiğinden yalnızca bu değerlere sınırlı değildir.

İç adaylar 16 kanal seviyesi açılmadan EFMPlus sözcüğü bazında puanlanır. Her beam yolu hem Stream 1 hem Stream 2'yi tutar; biri erken ve yanlış atılmaz. Ana/ikincil SYNC, eşitlikte Stream 1, birikmiş DSV ve son sınır özgün kodlayıcının tamsayı kurallarıyla sözcük bazında tam yürütülür. Her Sync Frame'in kalan dizisi, DSV sınır aşımındaki alternatif SYNC için tam 91 baytlık sözcük kodlayıcısıyla tekrar hesaplanır. Sonuçta yalnızca sözcük çoğunluğu, gri seviye ve sınırlar denetlenir; bit verisi sadece açıkça `--channel-output` istendiğinde açılır.

Arama kesin sözlüksel sıra kullanır: önce serbest payload için `controlled-majority-error` en aza indirilir; eşitse `controlled-gray-error`, yine eşitse sınır ve DSV karşılaştırılır. Konsol/JSON ana `majority-error`/`score`, son tüm kanal sözcüklerinin görünür hatasıdır. Her Sync Frame 2 SYNC ve 91 veri sözcüğü içerir; opak görüntüde ECC Block paydası `416 × 93 = 38688` sabittir. Ana `gray-error` da bu 38688 sözcüğü kapsar. Serbest bayt puanları ayrıca `controlledScore` ve `controlledGrayScore` içinde saklanır; tüm hatalarda düşük daha iyidir.

Her dış pass, mevcut tam ECC Block ile her Sync Frame'in controlled majority/gray hatasını kesin hesaplar ve en kötü `--retry-frame-fraction` (varsayılan 0.25) oranındaki frame'leri seçer. Önce bunları bloklar arası warm-start adayı olarak deneyip rastgele başlangıçtan hızla çıkar. Yerel EFM puanı ne kadar iyi olursa olsun kabul için EDC, PO, PI güncellenmeli ve tüm blok tam yürütülmelidir. Aday ECC güncellemeleri mevcut recording frames'i kullanır; yalnızca payload'ı değişen sektörler, etkilenen PO sütunları ve PI satırları yeniden kurulur. Öz sınamalar tam ECC yeniden kurulumuyla sözcük sözcük eşitliği doğrular. Sonra `--batch-frames` (varsayılan 8) boyutlu küçük gruplarda `--batch-candidates` (varsayılan 4) farklı EFM dizisi paralel üretilip denetlenir. Yalnızca tam blok controlled puanı kesin iyileşirse kabul edilir. Başarısız grup tek frame'e kadar bölünür; tek frame `--retry-beam-width` (varsayılan 32) ile tekrar denenebilir. Bölünmede tüm ağaç taranmaz, yalnızca ilk başarılı dal sürdürülür. Bir pass hiç aday kabul etmezse erken biter. `--retry-frame-fraction 0`, her pass'te tüm hatalı frame'leri işler; `--retry-beam-width 0` tek frame geniş beam tekrarını kapatır.

`--beam-width`, `--retry-beam-width`, `--batch-candidates` arttıkça ECC geri bildirimi sonrası geçerli aday bulmak kolaylaşır, fakat süre yaklaşık aday denetimi sayısıyla artar. Büyük `--batch-frames` daha güçlü yerel iyileşme sağlayıp PO/PI sonrası daha sık reddedilebilir; küçük gruplar kararlıdır ama daha çok tam yeniden kurulum ister. `--iterations` veya aralık modundaki `--iterations-per-block`, kapalı döngü pass sayısıdır; varsayılan 3. `--algorithm control` takma adı kabul edilir; eski `--frame-candidates`, `--parallel-candidates`, `--parallelism`, `--word-candidates`, `--control-bytes` reddedilir. `--mutation-bytes` yalnızca `full` içindir.

`--search-lanes 1..8`, yerel minimumların oluşturduğu yaklaşık %50 hatayı azaltır. Lane 1 verilen başlangıç payload'ını kullanır ve kesin monoton kabul uygular. Diğerleri `--seed` ile türetilen belirlenimci bağımsız payload'larla yeniden başlar; PO/PI engellerini aşmak için `--initial-temperature` ile `--final-temperature` arasındaki tavlama sıcaklığında geçici olarak daha kötü tam blok puanlarını kabul edebilir. Her lane, tam ECC yürütülmüş geçmiş en iyisini ayrı tutar. Tüm lane'ler paraleldir; sonuç controlled majority, gray, son `|DSV|` sırasıyla seçilir, dolayısıyla keşif son çıktıyı lane 1'den kötü yapmaz. Varsayılan 1 ek maliyet getirmez. Kalite için önce az sektörde `--search-lanes 4 --iterations-per-block 10` denenebilir. Bağımsız başlangıçlar görüntüyle kısıtlanmayan payload'ı da değiştirir; belirtilen içeriği korumak gerekiyorsa 1 kalmalıdır.

`full/exact`, değişebilir payload konumlarının sınırsız olduğunu belirtir; `256^32768` uzayını tam tarama veya küresel optimum garantisi değildir. Görüntü önce `--image-threshold` ile piksel bazında ikili yapılır; state-control her 16-bit payload sözcüğündeki örneklerin siyah/beyaz çoğunluğunu alır. Hedef yalnızca tam siyah 0 veya tam beyaz 16'dır; 8:8 eşitliği merkez örneği çözer. Önce yanlış çoğunluk sayısı, sonra land sayısının 0/16'ya uzaklığı olan gri seviye hatası azaltılır. 16 ayrı bit konumunun deseni eşleştirilmez; tam kanal biti konum eşleşmesi için `full` kullanılır.

### Dispersion tek bayt doku modu

`--algorithm dispersion` (`byte-pool` takma adı), rastgele arama yapmadan karıştırılmış alanda doğrudan payload üretir:

| Görüntü parlaklığı | Karıştırılmış bayt Q | Sabit geçiş sayısı | Nominal önizleme parlaklığı |
| --- | --- | --- | --- |
| 0..127 | `0xA5` | 5 | 0 |
| 128..255 | `0x92` | 2 | 255 |

ISO payload, `P = Q XOR sector scrambler byte` ile hesaplanır.

Her payload sözcüğü, 16 kanal bitinin disk yüzeyi merkezinde örneklenip sabit aralığa atanır. `--constraint-step` ve `--image-threshold` etkilemez; `--image-threshold` yalnızca `full/state-control` ikili hedefini denetler. ID, IED, sıfır RSV, EDC, PI, PO, SYNC standarda göre üretilir. 619008 NRZI biti açılmaz; tam recording frames sözcük bazında yürütülerek son EFMPlus state, DSV ve pit/land kesin olarak sonraki ECC Block'a aktarılır.

#### Ton için renk taklidi

İki doku yalnızca siyah/beyaz olsa da ara tonlar her payload baytında orantılı karıştırılabilir:

```powershell
--tone-dither 1 --tone-levels 2 --tone-brightness 0
```

- `--tone-dither 0..1`: 0 özgün `--image-threshold` sabit eşiğini korur; 1 bayt bazında eşiğe renk taklidi uygular, gri seviyeyi siyah/beyaz sözcüklerin uzamsal oranıyla yeniden kurar.
- `--tone-levels`: DVD yalnızca iki dokuya sahiptir, sadece 2 kabul eder.
- `--tone-brightness -64..64`: tüm gri seviyeyi kaydırır.

Renk taklidi fazı mantıksal payload bayt indeksinden belirlenir, önizleme örneklemesinden bağımsızdır. `simulate`, ISO'yu yalnızca `0xA5/0x92` ile sınıflandırdığı için sonucu doğal olarak gösterir. Bu parametreler verilmezse çıktı eski sürümle bayt bayt aynıdır.

Son EFMPlus sınırı gerekmiyorsa ve yazılabilir görüntü hızlı isteniyorsa `--fast-output true` eklenebilir. Aynı payload merkezleri örneklenir ve normal dispersion ile aynı 2048-byte sektörler üretilir. Farkı, ön hesaplı 32 KiB maske ile `Q` değerinden doğrudan ISO baytı `P` türetilmesidir. Data Frame, EDC, PI, PO, Recording Frame kurulmaz; EFMPlus, DSV, pit/land yürütülmez. JSON'da `exactEccEfmReplay`, `false`; `finalBoundary`, `null` olur. Yazma için gerekli veri eksilmez: disk yazıcısı bu türetilmiş alanları 2048-byte sektörlerden kendisi üretir.

```powershell
& ".\src\DvdImageSolver\bin\Release\net10.0-windows\DvdImageSolver.exe" solve `
  --image target.png `
  --total-sectors 2297888 `
  --iso-output "R:\full-disc-fast.iso" `
  --fill-sectors 2297888 `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --algorithm dispersion `
  --fast-output true `
  --fast-parallelism 0 `
  --initial zero
```

`--fast-parallelism 0`, mantıksal işlemci sayısına göre bağımsız ECC Block payload'larını paralel üretip LBA sırasıyla yazar. CPU kullanımını sınırlamak için `1..64` seçilebilir. Paralellik bayt seçimini veya çıktı sırasını değiştirmez.

Hızlı dosya ve akış çıktısı, donanım hızlandırması varsa varsayılan olarak dört noktalı çift hassasiyetli SIMD örneklemesini açar. Her grup dört gerçek payload sözcüğü merkezini kullanır; SYNC/ECC sınırından geçen konumları eşit aralıklı ardışık örnek saymaz. Aynı segmentte kübik aralık ters çözümü tek segment aramasını paylaşır, özgün dört Newton/Hermite hesabını vektörleştirir; segment geçişleri ve sınırlar skaler geometriye döner. Tam opak siyah/beyaz görüntü vektör sinüs/kosinüs kullanır; yarım piksel veya görüntü kenarına yakın noktalar özgün skaler yolda tekrar hesaplanır. Sürekli gri, renkli ve saydam görüntüler skaler trigonometri ve enterpolasyonu korur, yalnızca kübik geometri hızlanır. Sabit aralık, analitik desen geri çağrıları ve SIMD olmayan makineler skaler kalır. Sabit aralığın dört nokta yolu mevcut ölçümlerde yavaşladığı için kapalıdır. Tüm işlem `double` kalır; tek hassasiyetli koordinat veya yaklaşık spiral formülü kullanılmaz.

Üretim API'sinde `MultiBlockSolveOptions.UseSimdSampling = false`, performans ve çıktı karşılaştırması için skaler referansı seçer. Varsayılan `true` değeridir; GUI veya CLI değişmez. Regresyonlar iki yolun payload/sayaç eşitliğini, kübik segment sınırlarını, yarım pikselleri, alpha eşiklerini, iki dönüş yönünü ve paralel çıktı sırasını kapsar. Hız aynı girdi/LBA aralığının gerçek payload baytlarıyla ölçülmelidir; tek başına trigonometri hızlanması tüm disk hızlanmasını göstermez.

Sabit aralığın hızlı ters çözümü, çözüm sonrası değişmeyen iç yarıçap integralini, yarıçap karesini ve aralık katsayısını önbellekler; bir Newton düzeltmesinde aynı karekökü yeniden kullanır. Özgün `double` işlem sırası, bölme ve sınır davranışı korunur; yaklaşık integral veya tersle çarpma kullanılmaz. Bağımsız eski formül regresyonu yarıçap, açı ve kısa spiral sınırlarını bit bit karşılaştırır. `UseSimdSampling` seçeneğinin iki tarafı da bu skaler iyileştirmeyi kullandığından performans için önceki/sonraki bağımsız motorlar karşılaştırılmalıdır.

`payload-transition-class-error=0`, denetlenebilir tüm payload'ın doğru ikili `0xA5/0x92` sınıfına atanmasıdır; sabit alanlar, ECC ve SYNC aynı geçiş yoğunluğunda demek değildir. JSON `dispersionPool.levels`, iki giriş aralığını, Q baytlarını, geçiş sayılarını ve nominal önizleme parlaklığını kaydeder; bunlar gerçek disk yansıtırlığı değildir.

Çıktılar:

- `payload.bin`: ardışık birleştirilmiş 16 adet 2048-byte sektör;
- `payload.bin.json`: PSN, başlangıç/son sınır durumları, hatalar, yineleme sayısı ve geçen süre;
- İsteğe bağlı `channel.bin`: 77376-byte paketlenmiş NRZI seviyeleri.

## Yazılabilir ISO çıktısı

`solve`, yalnızca ISO verebilir veya 32768-byte payload'ı da saklayabilir:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-output solved-disc.iso `
  --lba 0 `
  --iterations 10000
```

Görüntü modu ISO mantıksal sektör sayısı olarak doğrudan `--total-sectors` kullanır; metin kısıtları ayrıca `--iso-sectors` gerektirir:

```powershell
--target target.txt --iso-output solved-disc.iso --iso-sectors 2295104
```

Yeni görüntü en küçük ISO9660 dosya sistemi ve `SOLVED.BIN` içerir. Çözülen 16 adet 2048-byte sektör `--lba ... --lba+15` konumuna yazılır. LBA 0, ISO9660 system area içinde olduğundan dosya sistemi veri alanına görünür bir `SOLVED.BIN` kopyası da konur. Seyrek dosya destekli NTFS'te mümkünse seyrek depolama kullanılır. Mantıksal uzunluk `total-sectors × 2048` kalır; yazma yazılımı boşlukları sıfır okur.

Mevcut ISO'da ardışık 16 sektör de değiştirilebilir:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-template original.iso `
  --iso-output solved-disc.iso `
  --lba 32 `
  --iterations 10000
```

Şablon boyutu 2048'in tam katı olmalı; görüntü modunda sektör sayısı `--total-sectors` ile eşleşmelidir. Dizin, dosya veya UDF/ISO meta verisine ayrılmış sektörleri değiştirmek dosya sistemini bozar; ayrılmış boş alan veya hedef dosyanın mevcut extent alanı seçilmelidir. Yeni en küçük ISO, sabit ISO9660 volume descriptor sektörleri 16 ve 17'nin üzerine yazmayı reddeder.

ISO, 619008-bit ham kanal akışı yerine 2048-byte kullanıcı verisi saklar. Standart DVD yazıcısı ID, EDC, ECC, EFMPlus ve NRZI'yi yeniden üretir. Gerçek desen ayrıca sürücünün aynı kodlama seçimlerini yapmasına ve çözücü başlangıç state, DSV, pit/land değerlerinin gerçek kayıt sınırıyla eşleşmesine bağlıdır.

### Tüm diski veya belirli sektörleri kesintisiz üretme

`--fill-sectors` 16'dan büyükse `--lba` konumundan ardışık ECC Block'lar çözülür; her son state, DSV ve pit/land sonraki bloğa geçer. Sektör sayısı 16'nın katı olmalıdır. 2297888 sektörlü disk için tam komut:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2297888 `
  --iso-output full-disc.iso `
  --fill-sectors 2297888 `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --constraint-step 16 `
  --algorithm state-control `
  --bridge-bytes 2 `
  --beam-width 8 `
  --retry-beam-width 32 `
  --retry-frame-fraction 0.25 `
  --batch-frames 8 `
  --batch-candidates 4 `
  --search-lanes 1 `
  --iterations-per-block 3 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land
```

Yalnızca ilk 1600 sektör için şöyle değiştirin:

```powershell
--fill-sectors 1600
```

Çıktı LBA 0–1599 arasında 1600 sektör içerir. `--total-sectors` geometri hesabı için tam disk kapasitesi kalır. `--lba` sıfır değilse önceki sıfır sektörler korunur; uzunluk `(lba + fill-sectors) × 2048` olur.

Aralık modu ISO9660 tanımlayıcıları dahil belirtilen tüm sektörlerin üzerine yazar; çıktı bağlanabilir ISO9660 değil, doğrudan yazılabilir ham 2048-byte/sector görüntüdür. `--constraint-step`, serbest payload aramasının görüntü örnekleme aralığıdır; state-control aynı payload sözcüğündeki ikili örnekler için çoğunluk oyu alır. Son görünürlük seyrek aralıkla sınırlanmaz; türetilmiş, sabit ve SYNC sözcükleri dahil her 16-bit kanal sözcüğü merkezi yeniden örneklenir. İkili görüntüde kontrast için her sözcüğe hedef veren `--constraint-step 16` önerilir. `32` daha hızlı yarı yoğunluktur; `256`, payload sözcüklerinin yalnızca yaklaşık 1/16'sını kısıtlar, genel majority error rastgele düzeye yaklaşır. Tam diskte 143618 ECC Blocks bulunur; önce 160–1600 sektörde hız ve sonuç ölçülmelidir.

Dispersion her payload merkezini örnekler, `--constraint-step` seyrek arama anlamını yok sayar; seçenek atlanabilir.

## Dosya ve desen karma modu

`--data-dir`, dizini ve alt dizinleri standart ISO9660 + Joliet olarak özyinelemeli yazan karma modu açar. `--file-position inner` (varsayılan), dosyaları iç halkaya, deseni dışına koyar. `--file-position outer`, dosya içeriğini disk sonuna; deseni baş meta verileri ile dosyalar arasına koyar. Desen sınırları 16-sector ECC Block'a hizalanır; tam disk geometrisi korunur, kalan çizim alanı tüm diske esnetilmez. Son ISO, Windows gibi sistemlerde normal veri diski olarak açılır; Joliet Çince dosya adlarını korur.

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --data-dir ".\disc-files" `
  --file-position outer `
  --iso-output hybrid-disc.iso `
  --total-sectors 2297888 `
  --volume-label MY_DISC `
  --inner-radius-mm 24 `
  --outer-radius-mm 58
```

Varsayılan `--algorithm dispersion --fast-output true`, tüm dış halkayı doğrudan üretmeye uygundur. `--fast-output false`, önce iç halka bloklarını tam kodlayıp yürüterek dış halka girişindeki EFMPlus state, DSV ve pit/land sınırını bulur, sonra bu kesin sınırla çizer. İç veri büyüdükçe tam yürütme süresi artar.

GUI Tasarım sayfasının Diskle birlikte dosyalar bölümü klasör ve iç/dış konumu seçer; Dışa aktarma aynı ayarları paylaşır. Ayar projeyle kaydedilir ve akışla yazmada da kullanılır. Akış yolu hızlı dispersion kullanır: dizini tarayıp yerleşimi sabitler, meta veri/dosya/deseni disk sırasıyla yazar; tam geçici ISO oluşturmaz. Dış konumda önce desen sonra dosya yazılır. Plan sonrası kaynak boyutu değişirse devam reddedilir. Önizleme dosya sistemi alanını sarı gösterir; renk diske yazılan veriye girmez.

`--lba` ve `--fill-sectors` kabul edilmez, yerleşim ve kapasiteden hesaplanır. `--drawing-start-lba N`, varsayılan başlangıç sonrası ek boşluk bırakabilir; çizim alanında kalmalı ve `--psn-offset` ile birlikte 16 sektöre hizalanmalıdır. `--file-position outer`, `--data-dir` gerektirir.

ISO9660 yazıcısı özyinelemeli dizinleri ve Joliet Unicode adlarını destekler. Tek dosya 4 GiB'ı aşamaz. Bağlantı döngüsü ve dizin dışı veriye yanlış başvuruyu önlemek için `--data-dir`, yeniden ayrıştırma noktalarını/sembolik bağlantıları reddeder.

## Yarıçap kalibrasyonu önizlemesi

Kalibrasyon CD sürümünün ileri izdüşümünü kullanır: genel kanal konumlarını sırayla örnekler, aynı konumu varsayılan üretim spiralinde görüntüden okuyup tahmin edilen gerçek spirale yansıtır. Aynı çıktı pikseline gelen iz örnekleri ortalanır; aralık farkının biriktirdiği gerçek faz korunur ve her çıktı pikselinden en yakın izi ters bulmanın oluşturduğu kabarcık moiré önlenir.

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- calibrate `
  --image picture.png `
  --output calibration-preview.png `
  --total-sectors 2297888 `
  --generated-inner-radius-mm 24 `
  --generated-outer-radius-mm 58 `
  --actual-inner-radius-mm 24.2 `
  --actual-outer-radius-mm 57.8 `
  --channel-bit-nm 133.3 `
  --spiral-direction ccw `
  --image-threshold 128 `
  --alpha-threshold 1 `
  --preview-size 2048 `
  --samples-per-sector 16
```

`--samples-per-sector` ileri izdüşüm yoğunluğunu denetler: 1–4096, varsayılan 16. DVD sektöründe yaklaşık 5.16 mm iz vardır; 16, yaklaşık her 0.32 mm'de örnektir. 32/64 boş pikselleri azaltıp ayrıntıyı iyileştirir, süre yaklaşık doğrusal artar. Keyfî dönüş ofseti yoktur; üretim ve gerçek spiral 0° başlangıcını paylaşır, genel dönüş yarıçap kalibrasyonunu etkilemez. Ek `calibration-preview.png.json` çıktısında `mappingMode`, `forward-channel-splat-average` olur; yoğunluk, iki aralık, toplam tur ve iz uzunluğu kaydedilir. Kalan çözücü hatası veya yazıcı uygulama farkı içerilmez.

Varsayılan tüm disk önizlenir. Kısmi üretimde çözümle aynı `--lba` ve `--fill-sectors` verilebilir; yazılmayan yüzey saydam kalır.

## Üretilen ISO'nun geri okuma simülasyonu

`calibrate`, kaynak görüntüden üretim geometrisiyle yazıp ölçülen geometriyle okumanın ideal sonucunu gösterir. Üretimle aynı 128 ikili eşiğini kullanır; `--image-threshold` uyumluluk için kalır ama bu önizlemeyi değiştirmez. `simulate`, gerçek ISO'yu okur; örnek payload baytını DVD karıştırmasıyla `0xA5/0x92` olarak sınıflandırıp 0/255 gösterir ve ölçülen yarıçap spiraliyle merkezi yüzeye yansıtır. Eski çok seviyeli görüntülerde `0x66`, 0; `0xB9`, 85 gösterilir; yeni üretim bu değerleri kullanmaz. cdimage kalibrasyon penceresiyle aynı kapalı döngüdür: üretim geometrisi ISO'ya sabitlenmiştir, simülasyon yalnızca ölçülen geometri ister.

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- simulate `
  --iso drawing.iso `
  --output readback-preview.png `
  --inner-radius-mm 24.2 `
  --outer-radius-mm 57.8 `
  --preview-size 2048 `
  --samples-per-sector 16
```

`--total-sectors`, `--lba`, `--fill-sectors` yoksa çözümün yazdığı `drawing.iso.json` (çizim başlangıç LBA ve sektör sayısı) okunur; sidecar yoksa LBA 0'dan tüm ISO kullanılır. `--samples-per-sector`, sektör başına örneklenen payload bayt sayısıdır (1–2048, varsayılan 16). Karıştırma sonrası ikili tablo veya eski 66/B9 dışında kalanlar atlanır. Dosya sistemi öneki tesadüfen tabloya uyabilir; sidecar çizim aralığıyla dışlanmalıdır. ISO, PI/PO düzeltme kodlarını saklamaz. `readback-preview.png.json` yanında yazılır; `mappingMode`, `payload-scramble-classify-splat` olur; sınıflandırma eşleşmeleri, aralık ve toplam turlar kaydedilir.

Örnek merkezi kullanılır: üretimde `FastDispersionImageWriter`, `PayloadDirectChannelOffset + 8` (16 bit sözcüğün yarısı) ile hedefi merkeze koyar; geri okuma aynı konumdadır. Böylece yönler arasında 8 kanal biti (yaklaşık 1 µm iz) farkı yoktur. Spiral, yön ve maske de ortaktır; üretim ve önizleme parametreleri eşitse görüntü örnek örnek yeniden üretilir.

Önizleme ISO değiştirince sidecar `imageMapping.totalSectors`, `innerRadiusMm`, `outerRadiusMm` alanlarını okuyup kalibrasyon geometrisini yükler. Önceki disk boyutu yüzünden harici görüntü ince halkaya sıkışmaz; sonraki ince ayarlar yüklenen değerlerden başlar.

## Önizleme çalışma alanının iki modu

Girdi anahtarı iki yolu sunar; değişiklikleri birleştiren geciktirme, iptal ve dışa aktarma ortaktır:

- **Üretilmiş çıktıyı geri okuma** (geri okuma simülasyonu, `simulate`), üretilen ISO ve tek ölçülen geometriyi alır; görüntüye gerçekten yazılan baytları gösterir.
- **Tasarım izdüşümü** (kalibrasyon önizlemesi, varsayılan, `calibrate`), kaynak görüntü ve üretim/ölçülen geometrileri alır. ISO okumadığından değişiklikten sonra çok daha hızlı yenilenir; ancak gerçek sözcükleri değil, kaynağın ölçülen geometride izdüşümünü gösterir. Çözücü hataları ve dosya sistemi öneki görünmez.

Kalibrasyon önizlemesi doğrudan Tasarım bileşiğini kullanır; görüntü üretimiyle kaynak aynı olduğu için iki desen uyuşur.

## Yalnızca ileri kodlama

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- encode `
  --input payload.bin `
  --output channel.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land
```

İlk PSN 16'nın katı olmalıdır; değilse giriş ECC Block sınırında başlamadığından kodlama reddedilir.

## Modülasyon tablolarını yeniden üretme

`Encoding/EfmPlusTables.Generated.cs`, ECMA-267 Annex G'den mekanik çıkarılır. Şartname PDF'sinden yeniden üretmek için:

```powershell
python tools/generate_efm_tables.py ECMA-267.pdf src/DvdImageSolver/Encoding/EfmPlusTables.Generated.cs
```
