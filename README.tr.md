<div align="center">

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

![Afiş](docs/images/DiscImageStudio_Banner_2.png)

<h2>Optik disklerde görünür görüntü üretimi ve yazma için hepsi bir arada araç</h2>

Disc Image Studio, herhangi bir disk yazıcısıyla herhangi bir DVD / CD / tek katmanlı Blu-ray diskin yüzeyine çıplak gözle görülebilen görüntüler işlemenizi sağlayan, çok platformlu bir disk yüzeyi görüntü üretme ve yazma aracıdır.

![.NET sürümü](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078d6?style=flat-square)
![Programlama dili](https://img.shields.io/badge/Language-C%23-68217A?style=flat-square)
![GitHub depo boyutu](https://img.shields.io/github/repo-size/JiaFeiMiao-K-Cat/DiscImageStudio?style=flat-square&color=3cb371)
![Katkılara açık](https://img.shields.io/badge/PRs-Welcome-brightgreen?style=flat-square)
[![Lisans: GPL-3.0-or-later](https://img.shields.io/badge/License-GPL--3.0--or--later-blue?style=flat-square)](LICENSE)

</div>

---

## Örnek sonuçlar

![](docs/images/case1.jpg)

![](docs/images/case2.jpg)

---

## Genel bakış

Disc Image Studio, bir görüntüyü CD, DVD ve tek katmanlı BD-R disklerin fiziksel katman kısıtlarına uyan verilere dönüştürür. Yazıldıktan sonra diskin yüzeyinde çıplak gözle görülebilen bir desen oluşur.

## Arayüz önizlemesi

![Ana sayfa: şablonlar ve son projeler](docs/images/ui-home.png)

![Tasarım: katman tuvali ve ton](docs/images/ui-design.png)

![Dışa aktarma: açık tema ve çıktı görüntüsü](docs/images/ui-export.png)

---

## Hızlı başlangıç

Windows'ta `win-x64.exe` dosyasını indirip doğrudan çalıştırın. Linux veya macOS'ta CPU'nuzla eşleşen **deneysel CLI** arşivini indirin, açın ve `./disc-image-studio --help` çalıştırın. Tüm yayın paketleri .NET çalışma zamanını içerir. [GitHub Releases](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/releases)

### 1. Proje oluşturun

Ana sayfada CD, DVD veya Blu-ray seçin, çıktı konumunu belirtip projeyi oluşturun.

### 2. Disk yüzeyini tasarlayın

Tasarım sayfasında görüntü içe aktarın, katmanlarla yerleşimi düzenleyin ve sağdaki Ton panelinde renk taklidi, gama, parlaklık ve gri seviye sayısını ayarlayın.

### 3. Önizleyin ve dışa aktarın

Önizleme sayfasında disk izdüşümünü kontrol edin; Dışa aktarma sayfasında ISO / RAW / WAV+CUE üretin veya anlık yazma özelliğiyle doğrudan boş diske yazın.

Anlık yazma deneysel bir özelliktir. Yalnızca gözden çıkarılabilir test ortamları kullanın.

## Özellikler

### Disk verisi üretimi

- **CD desteği**: Herhangi bir CD'yi destekler. cdrecord ve ImgBurn gibi yazma yazılımlarıyla uyumlu, biçim başlıklı WAV ses izi ve eşlik eden CUE dosyası dışa aktarabilirsiniz; RAW ses izi de dışa aktarılabilir. [CD yazma uyumluluğu](docs/CD_AUDIO_COMPATIBILITY.tr.md) belgesine bakın.
- **DVD desteği**: Herhangi bir DVD'yi destekler. ISO görüntüsü dışa aktarabilirsiniz.
- **Blu-ray desteği**: Herhangi bir tek katmanlı 25 GB BD-R'yi, yalnızca çizim ve UDF 2.50 dosyalarıyla birlikte çizim modlarında destekler. [ADDING_BLURAY.md](docs/ADDING_BLURAY.tr.md) belgesine bakın.
- **Diskle birlikte dosyalar**: CD/DVD/Blu-ray için kaydedilecek klasör ve iç/dış halka konumu Tasarım sayfasında doğrudan seçilebilir, Dışa aktarma sayfasında da değiştirilebilir; ayarlar projeyle kaydedilir.
- **Dosya alanı önizlemesi**: Karma disklerin tasarım izdüşümü ve üretilmiş çıktıyı geri okuma önizlemesi, dosya sistemini de içeren dosya alanını sarı gösterir.
- **Ortama göre gri tonlama**: CD ve DVD siyah beyaz iki seviye kullanır; CD için `0D / AA`, DVD'nin karıştırılmış alanında `A5 / 92` kullanılır. [PHYSICAL_MODEL_AUDIT.md](docs/PHYSICAL_MODEL_AUDIT.tr.md) belgesine bakın.
- **Gri tonlama için renk taklidi aracı**: Bağımsız program, kaynak görüntüyü `0/255` siyah beyaz iki seviyeli PNG'ye dönüştürebilir; görüntüyü açma veya sürükleyip bırakma, yan yana önizleme ve kayıpsız PNG dışa aktarma desteklenir. [Kullanım ve derleme talimatlarına](tools/FourLevelDither/README.tr.md) bakın.
- **Disk önayarları**: Çeşitli markaların CD/DVD/BD parametreleri yerleşiktir. Kendi kalibre ettiğiniz verilerle de katkıda bulunabilirsiniz; [önayar bakım kılavuzuna](docs/DISC_PRESETS.tr.md) bakın.

### Yardımcı kalibrasyon

Disc Image Studio akıllı ve hassas bir yardımcı kalibrasyon süreci sunar. Elle gözle yapılan kalibrasyona göre yardımcı kalibrasyon algoritması, belirlenebilen disk yüzeyi fiziksel verilerinin hassasiyetini 10<sup>-6</sup> düzeyinden 10<sup>-14</sup> düzeyine yükseltir. Kalibrasyon öncesinde sabit iz aralığı kullanılır ve artının radyal çizgileri belirgin biçimde eğrilir. Yardımcı kalibrasyondan elde edilen kübik iz aralığı parametreleriyle yeniden üretip yazınca çizgiler radyal doğrulara yaklaşır.

| Kalibrasyon öncesi · Sabit iz aralığı | Kalibrasyon sonrası · Kübik iz aralığı |
| --- | --- |
| ![Kalibrasyon öncesi DVD fotoğrafı: radyal artı çizgileri belirgin eğri](docs/images/verbatim-dvd-calibration-before.jpg) | ![Kalibrasyon sonrası DVD fotoğrafı: radyal artı çizgileri daha düz](docs/images/verbatim-dvd-calibration-after.jpg) |
| `P20261004-133519.jpg` | `P20261004-135416.jpg` |


Süreç ve parametreler için [yardımcı kalibrasyon kılavuzuna](docs/CALIBRATION.tr.md) bakın.

### Diske yazma

- **Disk görüntüsü çıktısı**: Tam ISO / RAW dosyaları dışa aktarabilir ve uygun yazılımla elle diske yazabilirsiniz.
- **Anlık akışla yazma (deneysel)**: CD, DVD ve BD-R verilerini üretirken diske yazar; tam bir görüntü kaydetmez. BD-R, MMC sıralı yazma kullanır.
- **İlk çizim yazımı boş ortam gerektirir. Dosya güncellemeleri özgün açık UDF VAT diskini gerektirir.**

### UDF 2.50 ve sonraki dosya güncellemeleri

CD ve DVD veri projelerinde **Tasarım → Disk dosyaları** altında **UDF 2.50 (VAT) kullan** seçeneği bulunur; Blu-ray otomatik olarak UDF 2.50 kullanır. CD/DVD için varsayılan biçim ISO9660/Joliet olarak kalır.

1. Diskte olmasını istediğiniz tüm dosyaları içeren klasörü seçin.
2. İlk kayıttan önce **Yazdırma** sayfasında **Tasarım birleşik görseli** kaynağını seçin, **Sonraki dosya güncellemeleri için alan ayır** seçeneğini işaretleyin ve **Güncellemeler için alan (MiB)** değerini ayarlayın. CD/DVD için UDF otomatik seçilir ve ayrılan alana yazılmaz.
3. Sonraki değişiklikler için bu klasörü düzenleyin, özgün açık diski takın ve Yazdırma sayfasında **Disk dosyalarını güncelle** seçeneğini kullanın. Eksik dosyalar geçerli listeden kaldırılır; eklenen ve değiştirilen dosyalar yeni sektörlere yazılır.
4. Güncellemeler değişmeyen dosya verilerini doğrulayıp yeniden kullanır, yeni üstveri ile bağlantılı VAT ekler ve özgün çizimi korur. İptal edilen veya başarısız güncellemeler otomatik tekrarlanmaz.

Güncellemeler CD-R, tek katmanlı DVD-R/+R (4.7 GB M-DISC dahil) ve tek katmanlı BD-R ortamlarını destekler. CD-RW, DVD-RW, DVD+RW, DVD-RAM ve BD-RE bu VAT akışını kullanamaz. Sonlandırılmış veya başka yazılımlarla oluşturulmuş diskler burada güncellenemez. Silinen dosyalar yazılmış alanı geri kazandırmaz; her güncelleme yeni veri, üstveri ve oturum ek yükü için boş alan gerektirir. CD ses parçasının dosya sistemi yoktur.

CLI, `--file-system udf250` ile CD/DVD UDF kalıpları dışa aktarabilir; `--file-system iso9660` uyumlu biçimi korur. Sonraki fiziksel disk güncellemeleri için Windows Yazdırma sayfasını kullanın.

## Sistem ve aygıt gereksinimleri

- Masaüstü sürümü Windows 10 2004 (2004/20H1, 19041) veya üzerini ya da Windows 11'i gerektirir.
- Derleme: .NET 10 SDK (`global.json`, 10.0.403 sürümünü sabitler). Kaynaktan, çalışma zamanı dahil edilmeden derlenen GUI için .NET 10 Windows Desktop Runtime gerekir. Release EXE çalışma zamanını içerir; ayrıca .NET yüklemek gerekmez.
- Anlık yazma için Windows'un tanıdığı bir CD / DVD / BD disk yazıcısı ve boş disk gerekir. BD-R için sıralı yazmayı destekleyen boş tek katmanlı disk gerekir.

## Arayüz dili

Windows masaüstü uygulaması 简体中文, 繁體中文, English, 日本語 ve Türkçe dillerini destekler. Sağ üstte Ayarlar → Genel → Dil bölümünden bir dil seçin veya sistemi izleyin; kaydettiğinizde yeniden başlatmadan hemen uygulanır. İptal, tercihleri değiştirmez. Dil ve tema varsayılan olarak sistemi izler.

Dil değişimi proje adlarını, katman adlarını, kullanıcı önayarlarını, yolları, geometriyi veya sayısal ayrıştırmayı değiştirmez. Sistem dili eşlemesi, komut satırı dili ve bakım için [LOCALIZATION.md](docs/LOCALIZATION.tr.md) belgesine bakın.

## Derleme ve çalıştırma

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DiscImageStudio.App --configuration Release
```

Derleme regresyon kontrolleri:

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release
```

Sürüm kuralları, CI ile EXE üretimi, Release taslağı kontrolleri, MSIX paketleme ve imzalama için [yayın kılavuzuna](docs/RELEASING.tr.md) bakın.

## Komut satırı

Linux (glibc) için `linux-x64` veya `linux-arm64` seçin. Apple Silicon Mac için `osx-arm64`, Intel Mac için `osx-x64` seçin. CLI yerel olarak görüntüler üretir; gerçek diske yazma işlemini uygun görüntü veya ses izini destekleyen yazma yazılımıyla yapabilirsiniz.

CLI, genel `--language zh-CN|zh-TW|en|ja|tr|system` seçeneğini (`--language=ja` biçimini de) destekler. Açık seçenek `DISC_IMAGE_STUDIO_LANGUAGE` ortam değişkeninden önceliklidir; ikisi de yoksa sistem arayüz dili izlenir. Komutlar, seçenek adları, yollar ve sayısal ayrıştırma dile göre değişmez.

.NET 10 SDK ile CLI'yi kaynaktan çalıştırma

```sh
dotnet run --project src/DiscImageStudio.Cli -c Release -- --help
```

CLI, PNG, JPEG, BMP ve GIF okuyabilir; hareketli görüntülerin ilk karesini kullanır. Aşağıdaki örnek kurulumu doğrulamak için iki sektörlük ses izi ve eşlik eden CUE dosyası üretir.

```sh
./disc-image-studio cd-generate --input picture.png --output sample.wav --sectors 2
```

| Komut | Açıklama |
| --- | --- |
| `solve` | Sabit hızlı doku eşlemesiyle (CW yönünde) DVD görüntüsü üretir; siyah beyaz dokular isteğe bağlı renk taklidiyle karıştırılır |
| `encode` | Tek DVD ECC bloğunu NRZI kanal seviyelerine kodlar |
| `calibrate` | Kalibrasyon izdüşümü: DVD kaynak görüntüsünü üretim geometrisiyle örnekleyip ölçülen geometriyle disk yüzeyine yansıtır |
| `simulate` | Üretilen DVD ISO'sunun geri okunmasını ölçülen yarıçaplarla simüle eder |
| `selftest` | DVD motorunun belirlenimci regresyon öz sınamalarını çalıştırır |
| `cd-generate` | Ek dosya yoksa WAV + CUE / RAW ses izi, `--data-dir` verilirse CD veri diski ISO'su üretir |
| `cd-preview-warp` | Kalibrasyon izdüşümü: CD kaynak görüntüsünü ölçülen geometriyle yansıtır |
| `cd-preview-track` | Mevcut WAV veya RAW CD-DA ses izini kalibre edilmiş geometriyle işler (geri okuma simülasyonu) |
| `bd-generate` | Tam BD-R ISO'su üretir ve `<iso>.json` sidecar dosyası yazar |
| `bd-info` / `bd-probe` / `bd-encode` | BD parametrelerini, ham yük probunu veya tek ECC kümesi tanılarını dışa aktarır |

Her komutun tüm parametreleri için ilgili belgelere bakın.

## Kaynak yapısı

| Yol | Sorumluluk |
| --- | --- |
| `src/DiscImageStudio.Cli/` | Linux ve macOS çevrimdışı komut satırı girişi |
| `src/DiscImageStudio.ImageIO/` | WIC ve platformlar arası görüntü kodlayıcıları |
| `src/DiscImageStudio.Localization/` | Arayüz, CLI ve modüllerin ortak beş dil kaynakları, dil seçimi ve biçimlendirme |
| `src/DiscImageStudio.Core/` | Ortamdan bağımsız modül sözleşmeleri, proje/önayar modelleri, iş ve komut kataloğu |
| `src/DiscImageStudio.Imaging/` | CD/DVD/BD ortak görüntü ön işleme ve renk taklidi yardımcıları |
| `src/DiscImageStudio.Burning/` | Windows IMAPI2 aygıt listeleme, akış tamponlama ve güvenli yazma katmanı |
| `src/DiscImageStudio.Cd/` | CD-DA üretim çekirdeği ve CD modülü |
| `src/DiscImageStudio.Dvd/` | DVD motoru uyarlayıcı modülü |
| `src/DiscImageStudio.BluRay/` | BD-R geometrisi, 17PP/ECC, 3T/6T eşlemesi, akış üretimi ve ISO |
| `src/DiscImageStudio.Iso/` | UDF 2.50 / ISO9660 yazıcıları |
| `src/DiscImageStudio.App/` | WPF GUI, ortak komut satırı ve mağaza kaynakları |
| `src/DvdImageSolver/` | Yalıtılmış DVD kodlama/çözüm motoru |
| `tests/` | Üçüncü taraf test çatısı kullanmayan mimari regresyon testleri |
| `packaging/` | MSIX bildirimi ve paketleme betikleri |
| `tools/` | Bağımsız yardımcı araçlar ve varlık üretim betikleri |
| `docs/` | Mimari, motor, önayar, yayın ve diğer belgeler |

## Belgeler

| Belge | İçerik |
| --- | --- |
| [ARCHITECTURE.md](docs/ARCHITECTURE.tr.md) | Modül soyutlamaları, katman sınırları ve genişletme kuralları |
| [LOCALIZATION.md](docs/LOCALIZATION.tr.md) | Arayüz dili ayarları, sistem dili yedek seçimi ve çeviri kaynaklarının bakımı |
| [DVD_ENGINE.md](docs/DVD_ENGINE.tr.md) | DVD NRZI görüntü kısıtı çözücüsünün kuralları, kodlama zinciri ve öz sınama kapsamı |
| [CD_AUDIO_COMPATIBILITY.md](docs/CD_AUDIO_COMPATIBILITY.tr.md) | CD ses izi biçimleri, bayt sırası ve gölgelenme sorunlarını giderme |
| [ADDING_BLURAY.md](docs/ADDING_BLURAY.tr.md) | Blu-ray modülü fiziksel modeli, üretim/yazma zinciri ve genişletme noktaları |
| [DISC_PRESETS.md](docs/DISC_PRESETS.tr.md) | Düzenlenebilir disk önayarları için katmanlı JSON biçimi ve birleştirme kuralları |
| [CALIBRATION.md](docs/CALIBRATION.tr.md) | Fotoğraf düzeltme (diskin en dış fiziksel kenarını ve merkez deliği kenarını işaretleme), eğri kalibrasyonu ve DVD kübik iz aralığı uydurması |
| [PHYSICAL_MODEL_AUDIT.md](docs/PHYSICAL_MODEL_AUDIT.tr.md) | CD / DVD üretiminin fiziksel model incelemesi |
| [VALIDATION.md](docs/VALIDATION.tr.md) | Derleme, regresyon ve gerçek disk testlerinin yerel doğrulama kayıtları |
| [RELEASING.md](docs/RELEASING.tr.md) | GitHub Release, MSIX paketleme, imza testleri ve mağazaya gönderim süreci |
| [CHANGELOG.md](CHANGELOG.tr.md) | Sürüm değişiklik geçmişi |

## Başvurulan standartlar

Disc Image Studio uygulaması ve fiziksel modelleri ağırlıklı olarak şu şartnameleri temel alır:

| Ortam | Standart ve baskı | Projenin kullandığı kapsam |
| --- | --- | --- |
| CD-R | [ECMA-394, 1. baskı, Aralık 2010](https://ecma-international.org/publications-and-standards/standards/ecma-394/) — *Recordable Compact Disc Systems CD-R Multi-Speed* ([resmî PDF](https://ecma-international.org/wp-content/uploads/ECMA-394_1st_edition_december_2010.pdf)) | CD-R ortamları, çok hızlı kayıt ve CD-DA uyumluluğu; proje CD-DA ses izleri üretir. |
| DVD ortak biçimi / DVD-ROM | [ECMA-267, 3. baskı, Nisan 2001](https://ecma-international.org/publications-and-standards/standards/ecma-267/) — *120 mm DVD – Read-only disk* ([resmî PDF](https://ecma-international.org/wp-content/uploads/ECMA-267_3rd_edition_april_2001.pdf)) | DVD veri çerçeveleri, ECC, EFMPlus kodlaması ve Ek G dönüştürme tabloları; kaydedilebilir ortam kuralları için ayrıca ECMA-359 kullanılır. |
| DVD-R | [ECMA-359, 1. baskı, Aralık 2004](https://ecma-international.org/publications-and-standards/standards/ecma-359/) — *80 mm (1,46 Gbytes per side) and 120 mm (4,70 Gbytes per side) DVD Recordable Disk (DVD-R)* ([resmî PDF](https://ecma-international.org/wp-content/uploads/ECMA-359_1st_edition_december_2004.pdf)) | Tek katmanlı DVD-R geometrisi, sektörleri, hata düzeltmesi ve kodlaması; standart tek ve çift yüzlü ortamları kapsar. Çift yüz, çift katman demek değildir. |
| BD-R | [ISO/IEC 30190:2021, 3. baskı, Ocak 2021](https://www.iso.org/standard/80770.html) — 120 mm tek katmanlı (25.0 GB) ve çift katmanlı (50.0 GB) kaydedilebilir BD için bilgi alışverişi ve depolama şartnamesi | Deneysel mod, tek katmanlı 25 GB diskin iz ve sektör yerleşimini, hata düzeltmesini ve kodlamasını kullanır; çift katmanlı diskler şu anda desteklenmez. |

CD ortak kodlama kısmında ayrıca [ECMA-130, 2. baskı (CD-ROM, 1996)](https://ecma-international.org/publications-and-standards/standards/ecma-130/) temel alınır.

## Yazma ve ortam güvenliği

- Disc Image Studio sürücü veya hizmet yüklemez; anlık yazma Windows'un yerleşik IMAPI2 bileşenini kullanır ve boş olmayan ortamı reddeder.
- Anlık yazma için gerçek disklerden bazı geri bildirimler alınmış olsa da farklı aygıt, ortam ve yazma girişlerine ilişkin doğrulama kapsamı sınırlıdır. Özellik deneyseldir; yalnızca gözden çıkarılabilir test ortamları kullanın. Yazmanın kesilmesi, elektrik kesintisi veya yetersiz üretim hızı diski kullanılamaz hâle getirebilir!
- Görünür sonuç diske, disk yazıcısına, ürün yazılımına ve yazma stratejisine bağlıdır.
- Karma klasörler yazma başlamadan önce taranır ve yerleşim sabitlenir; işlem sırasında kaynak dosya boyutu değişirse iş güvenle durdurulur, ancak yazılmaya başlamış disk yine de kullanılamaz olabilir.
- BD-R sıralı yazma, yedek alana yeniden eşleme sunmaz; kesinti sonrası aynı diske genellikle devam edilemez.

## Gizlilik ve güvenlik

- Disc Image Studio ağa bağlanmaz, dosya yüklemez ve telemetri toplamaz; [gizlilik politikasına](PRIVACY.tr.md) bakın.
- Güvenlik sorunlarını herkese açık Issue içinde paylaşmayın; [güvenlik politikasına](SECURITY.tr.md) göre GitHub özel bildirimini kullanın.

## Katkıda bulunma

Sorun bildirimleri ve kod katkıları kabul edilir. Geliştirme ortamı, modül sınırı kuralları, test gereksinimleri ve Pull Request süreci için [katkı kılavuzuna](CONTRIBUTING.tr.md) bakın.

## Lisans

Proje [GNU GPL v3.0 or later](LICENSE) (`GPL-3.0-or-later`) lisansını kullanır. CD serpiştirme uygulaması CDImage'dan taşınmış ve değiştirilmiş kod içerir; bütün dağıtım uyumlu GPL lisansıyla sunulur. İkili dosya veya değiştirilmiş sürüm dağıtımında karşılık gelen kaynak kodu sunma gibi yükümlülüklere uyulmalıdır. Üçüncü taraf yazı tipleri ve bileşenler kendi lisanslarına tabidir; [lisans bildirimine](LICENSE-NOTICE.tr.md) ve [üçüncü taraf bildirimlerine](THIRD-PARTY-NOTICES.txt) bakın. Tam metinler uygulamanın Hakkında sayfasında çevrimdışı da görüntülenebilir.

## Teşekkürler

CD ses izleri üreterek disk yüzeyinde görünür görüntüler oluşturma yöntemini gösteren [arduinocelentano'nun CDImage projesine](https://github.com/arduinocelentano/cdimage) teşekkür ederiz. Bu projenin CD serpiştirme uygulaması ilgili koddan taşınmış ve değiştirilmiştir; CD yüzeyine çizim fikri de bu projeden esinlenmiştir. CDImage'ın özgün yazarı ve katkıda bulunanların çalışmaları bu proje için önemli bir temel sağlamıştır.

---

## Yıldız geçmişi

<a href="https://www.star-history.com/?repos=jiafeimiao-k-cat%2Fdiscimagestudio&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=jiafeimiao-k-cat/discimagestudio&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=jiafeimiao-k-cat/discimagestudio&type=date&legend=top-left" />
   <img alt="Yıldız geçmişi grafiği" src="https://api.star-history.com/chart?repos=jiafeimiao-k-cat/discimagestudio&type=date&legend=top-left" />
 </picture>
</a>
