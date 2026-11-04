# Tek katmanlı BD-R: bayt ve bit dönemi geçiş yoğunluğu analizi

[简体中文](BLURAY_BYTE_DENSITY.md) | [繁體中文](BLURAY_BYTE_DENSITY.zh-TW.md) | [English](BLURAY_BYTE_DENSITY.en.md) | [日本語](BLURAY_BYTE_DENSITY.ja.md) | [Türkçe](BLURAY_BYTE_DENSITY.tr.md)

2026-10-24. Sağlanan ISO/IEC 30190:2021, özellikle 13.15–13.18 ve Figure 40/41 temelinde, projenin EDC, karıştırma, LDC/BIS, DC ve 17PP kodlama zinciriyle sayısal deneyler yapılmıştır. Yalnızca tek katmanlı diskler ele alınır.

**Toplam geçiş yoğunluğu hedefleniyorsa bit düzeyinde oluşturulan desenler kullanılmaya değerdir.** En iyi tekrarlanan bayt tabanının tam küme yoğunluğu yaklaşık **19.20% / 38.61%** iken fiziksel iç içe geçirme ve DC bölüm sınırları hesaba katılan bit dönemi düzenleri **15.95% / 43.37%** değerlerine ulaşır. Uçlar arasındaki fark 19.41'den 27.42 yüzde puana çıkar; artış yaklaşık 41%'tir. Bunlar sayısal kodlama sonuçlarıdır; gerçek disk yansıma parlaklığı ve siyah/beyaz yönü henüz belirlenmemiştir. Mevcut çizim, bu yoğunluk uçları yerine kullanıcının seçimiyle aşağıdaki **3T / 6T sayı eğilimini** kullanır.

<a id="按-3t--6t-数量接入绘图"></a>

## 3T / 6T sayı eşlemesini çizime katma

Aynı 958,272 kanal bitine sahip tam kümelerde ardışık modulation ONE değerleri arasındaki tam 3T veya 6T aralıklarının **mutlak sayısı** hesaplanır. Hem mark hem space sayılır. Bu, NRZI düzeyinin görev çevrimi veya hedef geçiş aralığı sayısı / tüm geçiş aralıklarının sayısı sıralaması değildir.

| Varsayılan görüntü ucu | Karıştırılmış alanda Q | Temel kümede hedef geçiş aralığı sayısı | Hedef geçiş aralığı oranı | Dört adreste hedef sayı aralığı |
| --- | --- | ---: | ---: | ---: |
| Koyu (0–127), 3T eğilimli | `AA` | 265,136 | 85.4164% | 264,946–265,186 |
| Açık (128–255), 6T eğilimli | `44` | 116,575 | 62.6862% | 116,470–116,659 |

Bu iki bayt temel PSN `0x100000` üzerindeki 256 tekrarlanan bayt arasında kendi hedef sayılarında birincidir. En yüksek 6T oranı `22` için 62.7744%'tür, ancak 116,402 adet 6T ile `44` değerinden azdır; bu yüzden sayı modu `44` seçer. `0x100000`, `0x234560`, `0x6D3A00`, `0xCA73E0` adreslerinde yeniden kontrolde `44` için ortalama 6T sayısı 116,559.5 bulunur; bu, `11` için 116,526.5 ve `22` için 116,457 değerlerinden biraz yüksektir. Her adreste optimum olduğunu göstermez.

Üretimdeki `BluRayRunLengthMapping`, 128 eşiğinde `AA` / `44` seçer ve siyah/beyaz yönü ters çevrilebilir. `BluRayImagePayloadMapping` görüntü konumlarından örnekler; akış üreticisi karıştırmayı iptal edip host yükünü yazar. Görüntü kenarları, EDC, ECC, BIS, DC ve eşzamanlama başka geçiş aralığı uzunlukları doğurur; dolayısıyla “3T / 6T eğilimi” saf 3T / 6T değildir. Varsayılan koyu/açık ataması ters çevrilebilir bir görüntü kuralıdır; gerçek disk yansıma parlaklığı kalibrasyonu değildir.

Bu değerler mevcut açgözlü DC tanılama modelini kullanır; ham sayılar `all-bytes.json` içindeki `Cluster.RunCounts` alanındadır. Mimari regresyonları tam RUB kodlamasıyla disk başı ve sonundaki sayı eğilimini doğrular, karışık görüntülerde karıştırma ve ECC konumlarını denetler. Komutlar ve kullanım için [Blu-ray kılavuzuna](ADDING_BLURAY.tr.md) bakın.

## Ölçüm tanımları

- Geçiş yoğunluğu = modulation ONE sayısı / kanal biti sayısı. ONE bir NRZI değişimini gösterir; NRZI düzeylerindeki ONE oranı bunun yerine kullanılamaz.
- Q, **karıştırma sonrası hedef yüktür**. Host yükü olarak `Q XOR scramblingMask` yazılmalıdır; ham host baytlarını doğrudan yinelemek hedef deseni kaybettirir.
- Saf 17PP sonuçları DC, ECC, BIS ve eşzamanlama içermez; bağımsız standart tablosu ve sonlu durumlu tam döngü hesabı kullanır.
- Tam küme 65,536 host baytından üretilir; EDC, LDC parity, BIS, DC ve eşzamanlama dahil `496 × 1932 = 958272` kanal biti sayılır. Run-in/out/Guard3 sayılmaz, ancak run-in sonundaki düzey ve DSV kullanılır.
- Varsayılan PSN `0x100000` değeridir; adres flags ve user control data sıfırdır. Adaylar ayrıca `0x234560`, `0x6D3A00`, `0xCA73E0` adreslerinde doğrulanır.
- Ana sonuçlar, gelecekteki kontrol bitlerini geçici sıfır yapan mevcut açgözlü DC tanılama algoritmasını kullanır. Yazıcıyla aynı kararları garanti etmez. Tümü 0, tümü 1 ve sabit tohumlu rastgele DC ek duyarlılık testleridir; gerçek kayıt talimatı değildir.

## Tüm 256 bayt için taban

Tek bir Q baytını kesintisiz yinelemek, DC/ECC olmadan kararlı durumda yalnızca dört yoğunluk düzeyi verir:

| Saf 17PP yoğunluğu | Bayt sayısı |
| --- | ---: |
| 1/6 = 16.6667% | 28 |
| 1/4 = 25% | 92 |
| 1/3 = 33.3333% | 100 |
| 5/12 = 41.6667% | 36 |

Bunların **80'i tek sayılı bit kaydırmasında yoğunluk sınıfını değiştirir**. DC ekleme iki bitlik çözümleme fazını değiştirir; bu yüzden yalnızca çıplak kodlama tablosuyla sıralama yapılamaz.

| Q | Saf 17PP | Tam küme açgözlü | Dört PSN için tam küme aralığı | Test amacı |
| --- | ---: | ---: | ---: | --- |
| `09` | 16.6667% | 19.1997% | 19.1962–19.2080% | Taramadaki en düşük tekrarlanan bayt |
| `11` | 16.6667% | 19.4820% | 19.4447–19.4820% | Saf kodlama tümü 6T; uzun aralık kontrolü |
| `22` | 16.6667% | 19.3505% | 19.3242–19.3510% | Dayanıklı düşük yoğunluk grubunda |
| `3D` | 41.6667% | 38.4602% | 38.4428–38.4739% | Dayanıklı yüksek yoğunluk baytı |
| `7A` | 41.6667% | 38.5486% | 38.5409–38.5525% | Dayanıklı yüksek yoğunluk baytı |
| `9E` | 41.6667% | 38.6143% | 38.6014–38.6260% | Taramadaki en yüksek tekrarlanan bayt |
| `00` | 33.3333% | 32.8615% | 32.8412–32.8668% | Yaygın giriş kontrolü |
| `FF` | 33.3333% | 32.5332% | 32.5332–32.5639% | `00` ile büyük yoğunluk farkı yok |

`55/AA` tam küme yoğunlukları yaklaşık 32.30% / 32.39%'dur; yoğunluk uçları olarak uygun değildir. Karıştırma iptal edilmezse test edilen 14 sabit host adayının tümü yaklaşık 29.37–29.67% aralığına düşer ve özgün farklar büyük ölçüde silinir.

Tüm sonuçlar: [CSV](../artifacts/bluray-byte-density/all-bytes.csv), [okunabilir tablo](../artifacts/bluray-byte-density/all-bytes.md), [256 bayt yoğunluk grafiği](../artifacts/bluray-byte-density/all-bytes-density.png).

## Daha ince düzeyde ulaşılabilen desenler

17PP, 2/4/6/8 giriş bitinin değişken uzunluklu kurallarıyla kodlar; önceki modülasyon bitini ve özel değiştirme kurallarını da kullanır. Baytlar taşıyıcıdır; bağımsız kod sözcükleri gibi ele alınamaz.

| Ham dönemsel bit dizisi | Dönem uzunluğu | Bayt paketleme örneği | Saf 17PP yoğunluğu | Kararlı geçiş aralıkları |
| --- | ---: | --- | ---: | --- |
| `0001` | 4 | `11` | 16.6667% | Tümü 6T |
| `00111101` | 8 | `3D` | 41.6667% | 3 adet 2T, 2 adet 3T |
| `001111` | 6 | `3C F3 CF` | 44.4444% | 3 adet 2T, 1 adet 3T |
| `0000101101110010` | 16 | `0B 72` | **12.5%** | Tümü 8T |
| `0111011110` | 10 | `77 9D E7 79 DE` | **46.6667%** | 6 adet 2T, 1 adet 3T |

Normal 17PP veri bölümlerindeki geçiş aralıkları 2T–8T'dir ve en fazla altı ardışık 2T bulunur. Bu nedenle uzun veri bölümlerinin yoğunluk alt sınırı `1/8`, üst sınırı `7/(6×2+3)=7/15` olur; yukarıdaki 16 ve 10 bitlik desenler sırasıyla bunlara ulaşır. Eşzamanlama 9T içerir; bu sınırlar eşzamanlama içeren herhangi bir kısa pencereye doğrudan uygulanamaz.

İki uç dönem tek sayılı bit fazlarında 29.1667% / 33.3333% düzeyine geriler. 6 bitlik `001111` her iki fazda 44.4444% verir; ancak çıplak NRZI görev çevrimi 4/9 veya tümleyenidir ve DC dengelemesi gerekir. En fazla 16 bitlik dönemlerin aramasında hem tek hem çift faza dayanıklı en yüksek yoğunluk desenidir.

Paketleme örnekleri yalnızca dönemin temsilini gösterir. **Bu host baytları doğrudan yinelenmemeli; Q özgün yük sırasında yinelenip iç içe geçirme göz ardı edilmemelidir.** Örneğin yük sırasıyla 6 bitlik dönem Q'ya doldurulunca tam küme yalnızca yaklaşık 33.43–34.39% olur; fiziksel ECC sırası kullanıldığında yaklaşık 40.64–40.75%'e ulaşır.

## DC telafisi ve tam küme sonuçları

Her çerçevenin 155 ECC baytı toplam 1240 ham bittir. Önce 25 veri biti bulunur, ardından her 45 veri bitinde bir DC biti eklenir; toplam 28 adettir. Bit 0 MSB olmak üzere `j=8×EccColumn+bitWithinByte` olsun:

```text
DCbefore(j) = j < 25 ? 0 : 1 + floor((j - 25) / 45)
```

DC yuvalarını ayırma:

```text
index = RecordingFrame × 1268 + j + DCbefore(j)
Qbit = pattern[(index + phase) % period]
```

DC veri bölümlerinde yeniden başlatma:

```text
index = j < 25 ? j : (j - 25) % 45
Qbit = pattern[(index + phase) % period]
```

DC eklendikten sonra her bölüm çift giriş bitinden başlar; dönemi yeniden başlatmak iki bitlik çözümleme fazını korur. Yuva ayırma düzeni yalnızca konumları ayırır; DC bit değerlerini yine kodlayıcı/yazıcı seçer. EDC, BIS ve parity de keyfî kontrol edilemez; normal üretilir.

Q bu fiziksel konumlarda oluşturulduktan sonra karşılık gelen yük baytlarına geri dağıtılır ve karıştırma iptal edilir. Mevcut `Map(luminance, position)`, RecordingFrame/EccColumn sağlar; bir çağrıda 8 biti ayrı ayrı üretmek yeterlidir, dönüş değerini sabit bir baytla sınırlamak gerekmez.

| Önerilen bit deseni | Oluşturma | Faz | Tam küme açgözlü | Dört PSN aralığı | Diğer üç DC stratejisi aralığı |
| --- | --- | ---: | ---: | ---: | ---: |
| 16 bit `0000101101110010` | DC bölümlerinde yeniden başlatma | 2 | **15.9500%** | 15.9405–15.9500% | 15.9278–17.0720% |
| 6 bit `001111` | Ayrılmış DC yuvaları | 3 | **40.8172%** | 40.8158–40.8315% | 41.0010–41.4603% |
| 10 bit `0111011110` | DC bölümlerinde yeniden başlatma | 4 | **43.3670%** | 43.3662–43.3713% | 42.1870–43.3904% |

Faz 0'dan başlar ve yukarıdaki koordinatlara eklenen dönemsel kaydırmayı gösterir. Her kümede koordinat başlangıcına dönülür. Bu fazlar yalnızca bu oluşturma yöntemine aittir; fiziksel sinyalin tamamını aynı sayıda bit kadar keyfî kaydırmak değildir.

6 bit düzeninin tüm fazlarında tam küme yoğunluğu 40.7929–40.8172%'dir; fazın etkisi küçüktür. Uç 16/10 bit düzenleri seçilen fazı korumalıdır; yanlış faz yaklaşık 30%–32%'ye dönebilir. DC duyarlılık aralıkları yalnızca test edilen bu stratejilerin aralığıdır; herhangi bir yazıcı için garantili aralık değildir.

Önerilen düşük desende tam geçiş aralıklarının **52.65%'i 8T**, önerilen yüksek desende **79.14%'ü 2T** olur. Bu, yalnızca ortalama yoğunluğa bakmaktan daha somut bir gerçek disk karşılaştırması sağlar. Açgözlü modelde her ikisinin NRZI düzey doluluğu yaklaşık 50%'dir; bit değerleri 0/1 doğrudan görsel siyah/beyaz olarak yorumlanamaz.

![Tam küme yoğunluk karşılaştırması](../artifacts/bluray-byte-density/fine-pattern-comparison.png)

## İlk gerçek disk deneyi adayları

Aynı disk, yazım hızı ve gözlem koşullarında aşağıdaki düz renk alanlarını karşılaştırıp iç, orta ve dış yarıçaplarda yineleyin:

1. Seyrek uç: 16 bitlik dönem, DC bölümlerinde yeniden başlatma, faz 2.
2. Yoğun uç: 10 bitlik dönem, DC bölümlerinde yeniden başlatma, faz 4.
3. Dayanıklı yüksek yoğunluk kontrolü: 6 bitlik dönem, ayrılmış DC yuvaları, faz 3.
4. Bayt tabanı: `09`, `11`, `9E`; `00` ayrıca sıradan giriş kontrolü olabilir.

Önce büyük alanlarla gerçek açık/koyu yönünü ve bakış açısı döndürülünce kararlılığı karşılaştırın, ardından bölgesel karışım ve görüntü eşlemesini deneyin. İki deseni bayt bayt değiştirmek kodlama bağlamını değiştirir; yoğunluk veya parlaklığın iki ucun doğrusal ortalaması olduğu varsayılmamalıdır. Bu uç yoğunluk adayları henüz gerçek diske yazılmamış ve mevcut varsayılan siyah/beyaz eşlemede kullanılmamıştır.

## Doğrulama ve yeniden üretim

- Bağımsız Python uygulaması standarttaki Figure 41 aktarımıdır; C# ile 256 dört baytlık vektörde ve 298 dönemsel vektörde bit düzeyinde aynıdır.
- Uzunluğu 2–16 olan tüm 131,068 ham bit dizisi, her biri iki önceki bit değeriyle kapsamlı taranmış; toplam 262,136 tam döngü hesabı ve 14,694 iki bit döndürme eşdeğerlik sınıfı doğrulanmıştır. Yukarıdaki faza dayanıklı optimum yalnızca bu arama kapsamı için geçerlidir.
- Tam zincir 256 baytın tümünü tarar. Yapay çerçeveler ayrıca her bayt için 8 DC mask × 31 eşzamanlama konumu × 2 önceki çerçeve termination durumunu denetler.
- İnce desenlerde 176 desen/yerleşim/faz birleşimi, ayrıca 120 adres kontrol kümesi vardır. Her kümede 65,536 kontrol edilebilir Q baytının gerçek ECC konumlarında hedefle aynı olduğu doğrulanır.
- Küme uzunluğu, geçiş aralığı sayısının korunumu ve RLL aralık denetimleri geçer; hızlı küme taraması tam RUB yoluyla eşleşir. Saf veride 2T–8T dışında tam geçiş aralığı yoktur; eşzamanlama içeren kümelerde 9T'ye izin verilir.

Araç ve komutlar için [BluRayByteProbe](../tools/BluRayByteProbe/README.tr.md) sayfasına bakın. Ham sonuçlar `artifacts/bluray-byte-density/` içindedir; artifacts dizini depo kurallarınca yok sayılır, betikler sonuçları yeniden üretebilir. Üretim, bu sayfada ayrı verilen 3T / 6T sayı eşlemesini zaten kullanır.
