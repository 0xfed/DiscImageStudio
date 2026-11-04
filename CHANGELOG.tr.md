# Değişiklik günlüğü

[简体中文](CHANGELOG.md) | [繁體中文](CHANGELOG.zh-TW.md) | [English](CHANGELOG.en.md) | [日本語](CHANGELOG.ja.md) | [Türkçe](CHANGELOG.tr.md)

## Henüz yayımlanmayan

## [1.3.7] - 2026-11-04

- Windows bağımsız EXE yayımlamasındaki yinelenen dosya hatası (`NETSDK1152`) giderildi: uygulamada barındırılan DVD çözücüsünün yinelenen başlatıcıları ve çalışma zamanı bildirimleri dışlandı, `DvdImageSolver.dll` korundu. CI artık çalışma zamanını içeren EXE'yi derler ve duman testinden geçirir.

- Derleme kaynağı kanıtı için `actions/attest` `4.2.2` sürümüne geçildi, CI, `.python-version` ile CPython `3.14.10` sürümüne sabitlendi. SDK, `packaging/Install-DotNetSdk.py` ile yeni bir dizine kurulur; indirilen arşivler açılmadan önce `eng/dependencies/dotnet-sdk.json` içindeki incelenmiş SHA512 değerleriyle doğrulanır.

- Kalibrasyon önizlemeleri dahil DVD canlı önizlemeleri oluşturulurken seçilen arayüz dili korunur; hem dil tercihinin hem de uygulanan dilin değişmediğini doğrulayan regresyon kontrolleri eklendi.

- Eski `StbImageWriteSharp` bağımlılığı, .NET `ZLibStream` ve sabit boyutlu arabellekler kullanan bir PNG yazıcısıyla değiştirildi; çözülen pikseller korundu ve macOS üzerinde katı paket imzası doğrulaması sürdürüldü.

- PNG açma çıktısının bildirilen görüntü boyutunu aşması sorunu giderildi; `RasterCodec` varsayılan girdi sınırları `256 MiB` ve `64 Mi` piksel olarak belirlendi. İncelenen derleme bağımlılıkları sabitlenip kilitlendi, çıktı denetimi yayımlama yazma izinlerinden ayrıldı ve sürüm dosyaları için SBOM ile derleme kökeni doğrulamaları yapılandırıldı.

- Ayrı Windows CI düzen denetimi eklendi: beş dilde 325 çalışma alanı, önizleme, kalibrasyon ve pencere senaryosunda metin sınırları/eksik glifler denetlenir; hata olsa da görüntü/rapor kaydedilir. Geçici depolar ve yazamayan sahte yazıcı ile değişen başlıklar ve proje verisinin korunması kontrol edilir. Windows CI üzerinde 325 senaryonun tamamı geçti; metin taşması veya eksik glif saptanmadı.

- DVD akış üretiminin yedi hata yolu ve boş devre dışı önayar bağlamları çevrildi; kaynak korumaları ve beş dil regresyonları genişletildi. Tam derleme ile 2,053 kaynak ve 2,077 başvuru kontrolü geçti. Türkçe README eklendi, diğer sürümler karşılaştırıldı; DVD motor kılavuzu formüller, komutlar ve tarihsel doğrulama sınırları korunarak beş dilde tamamlandı.

- Workflows, Workspace ve XAML içindeki 595 mevcut arayüz girdisi beş dilde anlamsal olarak incelendi. Disk yazıcısının uyguladığı hız ayarı, yansıyan parlaklık, kapsama sınırları, kalibrasyon kayıtları ve eylem talimatları düzeltildi. Uzun etiketler, başlıklar ve kalibrasyon düğmeleri satır kaydırıp büyüyebilir. Mimari kılavuzu beş dilde tamamlandı; belge kontrolü yalnızca belirtilen diyagram etiketlerini çevirirken düğüm, bağlantı ve sayıları korur.

- Dört PowerShell paketleme/kurulum betiğine beş dil tanıları, yerel dil parametresi ve alt süreç dil aktarımı eklendi; sürüm meta verileri, günlük metni, yollar ve sağlama değerleri korundu. Derleme, imzalama, kurulum veya yetki yükseltme yapmayan ön kontroller eklendi. Bozuk ana kataloğu özyinelemeli yüklememek için katalog hataları ve CLI hata sarmalayıcısı bağımsız çevrilir. Ortak arayüzün beş dil anlam incelemesi; tıklamalar, sınırlar, kayıt sonrası anlık uygulama, yol kapsamı, önbellek geçiş kısıtları ve dil öz adlarını düzeltti. Yayın kılavuzu beş dilde tamamlandı.

- Altı Python aracına ortak beş dil kaynakları bağlandı; yardım, ayrıştırma hataları, ilerleme, analiz açıklamaları ve grafik metni çevrildi, sayılar/alanlar/üretilen C# tabloları korundu. Özgün betik referansları ve sentetik beş dil grafik regresyonları yazı tipleri ile metin sınırlarını doğrular. Blu-ray uygulama, bayt yoğunluğu ve CD uyumluluk kılavuzları tamamlandı; belge denetimi kaynakları üretilmiş raporlardan ayırır, yeniden üretilebilir bağlantıları açıkça kaydeder.

- İlk kullanıcı önayarının değiştirilmemiş ad/açıklaması ortam ailesi ve ID ile tanınıp yalnızca gösterimde çevrilir; kullanıcı değişiklikleri ve kayıtlı veriler korunur. Düzenlenebilir önayar kılavuzu ve JSON örnek metni beş dilde tamamlandı; alanlar, türler, katkıcılar ve sayısal hassasiyet denetlendi.

- Yazma katmanının CD/DVD/Blu-ray hata, ilerleme, işlem, temizleme ve aygıt zamanlama tanıları beş dilde tamamlandı. Sonlandırma, Çince metin yerine açık durumla belirlenir. WPF gerektirmeyen sahte aygıt regresyonları Linux'ta baytlar, MMC komutları, zaman aşımı, iptal ve temizliğin beş dilde aynı olduğunu; ham aygıt ayrıntıları ve sayısal kültürün korunduğunu doğrular.

- Windows genişletilmiş yardım, bağımsız DVD yardımı, motor günlükleri, öz sınama başarısızlıkları ve kalan masaüstü özel durumları çevrildi; komut, parametre, formül ve varsayılanlar korundu. Beş dilde EFMPlus, kodlama JSON'u ve ISO baytları aynı. DVD gri seviye probuna ortak dil seçimi, açıklayıcı JSON çevirisi ve Windows atlas/önizleme eşitlik regresyonu eklendi. Fiziksel model incelemesi tarihsel sonuç ve sayılar korunarak beş dilde tamamlandı.

- Bağımsız renk taklidi aracının arayüz, iletişim kutusu, durum ve CLI metni beş dile açıldı; ortak seçenek/ortam tercihi kullanılır, mod değerleri, paletler ve dosya adları korunur. C# Blu-ray probunun ilerleme, hata ve açıklayıcı JSON'u çevrildi; bağımsız Python 17PP vektörleriyle CLI doğrulandı. Windows arayüz/PNG bayt eşitliği regresyonları ve beş dil doğrulama kılavuzu eklendi.

- Modül, komut, seçenek meta verileri ve Core, CD/WAV, ISO/UDF doğrulama ile fotoğraf düzeltme mesajları çevrildi. Önbellekteki açıklamalar dil değişimini izler; protokol parametreleri/kullanıcı verisi korunur. Proje kaydı ve iç/dış halka ISO9660/Joliet/UDF bayt eşitlik regresyonları eklendi. Renk taklidi aracı, DVD gri seviye probu ve yerelleştirme bakım kılavuzu beş dilde tamamlandı.

- Blu-ray/görüntü yükleme özel durumları, ilerleme, sonuçlar ve açıklayıcı JSON beş dilde tamamlandı; teknik tanımlayıcı ve değerler korundu. Platformlar arası kodlama/bayt eşitliği denetimleri eklendi. Yardımcı kalibrasyon, sabit referans desen, Blu-ray bayt probu ve bağımsız 17PP belgeleri beş dilde tamamlanıp paragraf, formül, tablo sayıları ve komut eşitliği denetlendi.

- Yerelleştirme çalışma zamanı ortak projeye taşındı; platformlar arası CLI ve Windows/DVD girişleri genel dil seçeneği ile ortam değişkeni aldı. Beş dil CLI yardımı, yönlendirme hataları ve CD/DVD mesajları tamamlandı; Türkçe renk taklidi terimi düzeltildi. Gizlilik, güvenlik, lisans bildirimi ve katkı kılavuzunun İngilizce, Geleneksel Çince, Japonca ve Türkçe tam metinleri ile belge bazlı kontroller eklendi.

- Beş arayüz dilinin kalibrasyon uydurma yönlendirmesi, önizleme sınır denetimi ve üretilen kullanıcı önayar açıklamaları tamamlandı; kaynak anahtarları çözülür, elle düzenlenen metin korunur. Platformlar arası katalog bütünlüğü ve kaynak başvuru kontrolü CI'ye eklendi.

- 64099 ile aynı parametreleri kullanan yerleşik Verbatim BD-R (64293) önayarı eklendi; katkıcı bczhc.

- Yerleşik Verbatim DVD+R AZO (43498) önayarı eklendi; katkıcı Disappear9.

- Yerleşik Ritek DVD-R E-Series önayarı eklendi; katkıcı bczhc.

- Ritek BD-R 12x önayarı eklendi: iç yarıçap 23.98795 mm, dış 58.02236 mm; katkıcı DeepAQ.

- Yerleşik Ritek DVD+R E-Series önayarı eklendi.

- Yeni proje adı şablondan doldurulmaz, boş kalır; oluşturmak için ad girilmelidir.

- Kenar çubuğu proje başlık kartının sağına disket Kaydet düğmesi eklendi.

## [1.3.6] - 2026-11-02

- **Deneysel: Linux/macOS çevrimdışı CLI.** Görüntüden CD sesi (WAV+CUE/RAW), CD veri diski, DVD/Blu-ray ISO üretir. İki sistem için x64/ARM64 çalışma zamanı dahil paketler vardır. Açtıktan sonra `./disc-image-studio --help` komutları gösterir.

- Windows masaüstü WIC kullanmaya devam eder. Platformlar arası CLI, StbImageSharp ile okuyup StbImageWriteSharp ile PNG yazar; disk kodlama algoritmaları ortaktır.

- Japonca arayüz eklendi: Ayarlar → Genel → Dil içinde 日本語 seçilebilir; sistem `ja`/`ja-JP` ise sistemi izle de Japonca kullanır. Her kaynak `ja` metni aldı; `ui-snapshot --language`, `ja` kabul eder.

- Japonca README (`README.ja.md`) ve tüm README dil çubuklarına 日本語 bağlantısı eklendi.

## [1.3.4] - 2026-11-02

- Basitleştirilmiş Çince, Geleneksel Çince ve İngilizce arayüz eklendi. Ayarlar → Genel → Dil ile sistem veya dil seçilir; kayıt ve yeniden başlatma sonrası uygulanır, İptal değiştirmez. Tayvan/Hong Kong/Makao/`zh-Hant` Geleneksel, diğer Çince Basitleştirilmiş, diğer sistemler İngilizcedir. Gezinme, alanlar, pencereler ve durum ortak gömülü kaynakları kullanır; proje/sayı biçimi değişmez, alt modül/OS tanıları özgün kalır.

- Dil ve tema varsayılan sistemi izler; eksik/geçersiz tercih sisteme döner, geçerli kayıt korunur. Ayarlar `%LOCALAPPDATA%\DiscImageStudio\settings.json` içinde tutulur; bozuk/yeni şema varsayılana döner, `.discproj` içine yazılmaz.

- Sağ üstte kalıcı dişli+Ayarlar düğmesi ana sayfa ve tüm alanlardan erişilir; Ctrl+, korunur. Uzun proje adı kısaltılır; … menüsü Hakkında sunar, yinelenen Ayarlar ve eski kenar Hakkında girişi kaldırılır.

- Genel ve Depolama ayarları eklendi. Koyu/açık/sistem teması kayıt sonrası tüm pencereler, kenar çubuğu, bölümlü denetimler, disk tuvali ve iş panelinde anında yenilenir.

- Varsayılan proje/önbellek konumları eklendi; boşsa Belgeler/geçici dizin kullanılır. Kullanım görüntüleme, temizleme ve uygulama veri dizinini açma vardır; temizleme yalnızca izinli önbellek görüntüleri/sidecar dosyalarını siler, proje/çıktıyı korur ve iş sırasında kapalıdır.

- CD/DVD/Blu-ray tasarım izdüşümü, çıktı geri okuma ve dışa aktarılan önizleme sınırı 8192×8192 oldu; türlerin varsayılanları değişmedi.

- Tam İngilizce/Geleneksel Çince README eklendi; üç sürümün üstünde dil bağlantıları, dil ayarı ve çeviri bakımı açıklamaları vardır.

- Geliştirici `ui-snapshot` komutuna `--language zh-CN|zh-TW|en|system`, `--theme dark|light|system`, `--settings-dialog`, `--settings-view general|storage` eklendi. Dil/tema/ayar kategorisi düzeni doğrulanabilir. Ayar görüntüsü varsayılan 760×600, tek kullanımlık ayarla gerçek tercihleri değiştirmez.

## [1.3.3] - 2026-10-28

- Verbatim BD-R (64099) önayarı, kullanıcı kayıtlı ölçülen yarıçap/sektör/kutupluluk ile GUI ve CLI'ye eklendi.

- Blu-ray kalibrasyon akış kaynağının CD görünmesi düzeltildi; kayda göre bit uzunluğu, 3T/6T kutupluluk ve çizim aralığı gösterilir.

- Akış yazmasının altında aygıt yazılan/toplam, anlık hız ve kat hız sabit gösterilir; hazırlık, aktarım, sonlandırma ve tanılar günlüğe yazılır. Aktarım miktarı gerçek ilerlemeyi ezmez; sonlandırma/uzun hareketsizlik eski hızı temizler.

- BD kalibrasyon eşlemesi normal üretimin sınırlı paralel grupları ve dört nokta örneklemesini kullanır; özgün geri çağrı, aralık, kutupluluk ve çıktı sırası korunur. Özel eşlemeler hâlâ açık paralellik izni ister.

- CD kalibrasyonu dört nokta görüntü örneklemesini, DVD kalibrasyon/sözcük geri çağrısı değişken aralık SIMD yolunu paylaşır. Nokta bazlı skaler trigonometri, çağrı sırası ve ses serpiştirmesi korunur; sabit DVD aralığı skaler kalır.

- CD dosya modu, her baytın değiştirme/CIRC sonrası fiziksel konumunu kullanan dört nokta örnekleme aldı; dosya alanı, palet indeksi, karıştırma ve disk sonu sınırları korundu.

## [1.3.2] - 2026-10-27

- Proje Ayarları mevcut disk türünün başka önayarına doğrudan geçebilir; üzerine yazma onaylanır, İptal değer/seçimi korur.

- Tema mavisi koyu `#3385FF`, açık vurgu `#74C0FF` oldu. Bölümlü seçimin dolgusu tema rengi, yüksekliği girdiye eşit; sağ açma oku küçültüldü.

- Yeni Proje/Proje Ayarları içindeki Özel önayarı sağ tıklamayla silmek, son şablon kaydını da temizler.

- Tasarım yakınlaştırması sağ altta kalıcıdır; −/+ ve %25–%1200 (önceden yalnızca büyütme) aralığı eklendi. Pencereye Sığdır yenileme simgesi kullanır. Ctrl/Alt+tekerlek yakınlaştırır, orta düğmeyle sürükleme kaydırır.

- Ana sayfa şablonunun gri açıklaması CD/DVD/Blu-ray türünü gösterir.

- Önayar onayının sıradan tuşlara tepki vermesi, Blu-ray değişimi sonrası eski kutuplulukla kaydetme ve son önayar silinince tür/sonraki seçimin ayrışması düzeltildi.

## [1.3.1] - 2026-10-26

- CD dosya modu eklendi: klasör yoksa WAV, varsa ISO9660/Joliet Mode 1 veri diski üretir. İç/dış yerleşim, proje kaydı, ISO ve akış yazmasında ortaktır.

- CD izdüşümü ve yerleşim bilgili geri okuma, tam koordinatlarla dosya alanını sarı gösterir; Çince dizin/dosya desteklenir, içerik baytları korunur.

- CD veri akışı, sesle ortak DAO oturumu/kapasite sınırı kullanır; Ritek tıbbi su mavisi önayarının yanlış yetersiz kapasitesi düzeltildi. ATIP kalibre 359,845 sektör ve son sınırlar korundu.

- Mode 1 sağlama kodları, DAO veri izi kimliği, ileri akış ve iç/dış ISO tam kurtarma regresyonları eklendi. CD veri DAO yazılım/yerel bellek görüntüsüyle doğrulandı; fiziksel yazma henüz doğrulanmadı. Anlık yazma deneyseldir.

## [1.3.0] - 2026-10-26

- Tüm uygulama/modül/araçlar .NET 10'a, CI/EXE/MSIX .NET 10 SDK'ya geçti. Çalışma zamanı dahil EXE ayrıca kurulum gerektirmez.

- DVD/Blu-ray Tasarımına dosyalar bölümü, doğrudan klasör ve iç/dış konum seçimi eklendi; Dışa aktarma ile paylaşılır.

- DVD/Blu-ray karma moduna iç/dış dosya konumu eklendi; varsayılan içtir. Kayıt/ISO/akış aynı düzeni kullanır; dış dosyalar gerekli dosya sistemini korur, çizim dosya alanını atlar.

- DVD/Blu-ray izdüşümü ve yerleşimli geri okuma dosya alanını, dosya sistemi dahil, sarı ve açıklamayla gösterir. Renk yalnızca önizlemedir; harici yerleşimsiz görüntüde dosya alanı tahmin edilmez.

## [1.2.0] - 2026-10-26

### Proje ve tasarım

- Tasarım, Önizleme, Dışa aktarma, Diske yazma ve Yardımcı kalibrasyon olarak beş çalışma alanına ayrıldı; CD/DVD/BD mevcut proje/parametreleri paylaşır.

- `.discproj` kayıt/açma, son proje küçük resimleri, kaydedilmemiş değişiklik kontrolü ve Proje Ayarları eklendi; mevcut parametreler kullanıcı önayarı olarak kaydedilebilir.

- Birden çok görüntü içe aktarma, taşıma, ölçekleme, döndürme, sıralama ve görünürlük için çok katmanlı tuval eklendi. Çıktı/yazma yüksek çözünürlüklü bileşiği paylaşır; eski otomatik halka kopyalama kaldırıldı, yerleşimi tuval yönetir.

- Günlük, ilerleme, iptal ve çıktı konumunu birleştiren iş paneli eklendi. Çıktı/yazma aynı proje kaynağını gösterir.

### Ton, Blu-ray ve kalibrasyon

- Ton Tasarımda toplandı: %0–%100 renk taklidi, parlaklık, sRGB doğrusallaştırma, CD iki/dört seviye. Yeni proje %75 kullanır; eski tonsuz proje ikili eşlemeyi korur.

- CD/DVD/BD üretimine isteğe bağlı ton eklendi. CD/DVD belirlenimci mantıksal bayt renk taklidi, BD kaynak çözünürlüğünü kullanır; üretim, akış ve izdüşüm ayarları ortaktır.

- BD ISO JSON meta verileri geometri, çizim aralığı, kutupluluk, ton ve UDF zamanını kaydeder; Önizleme açılışında geri yüklenebilir.

- Yardımcı uydurma aramayı otomatik genişletir; Blu-ray büyük artıkta sürdürür. Büyük hatayı uygulamamak için en büyük sapma, aykırı işaretleri ve eşikler eklendi.

### Uyumluluk ve geçiş

- Proje/önayarlar CD/DVD/BD için birleşti; proje schema 1, önayar schema 2/3 ve eski CD izleri okunur. Mevcut üretim parametreleri korunur, yeni tonlar isteğe bağlıdır.

- Geliştirici `ui-snapshot` sayfaları 0 Ana, 1 Tasarım, 2 Önizleme, 3 Çıktı, 4 Yazma, 5 Kalibrasyon oldu. Hakkında `--about-dialog` kullanır; `--live-processing` kaldırıldı.

- Blu-ray/doğrudan yazma deneysel kalır. Donanım kapsamı `docs/VALIDATION.md` içindedir; otomatik test yeni fiziksel doğrulama sayılmaz.

## [1.1.0] - 2026-10-25

### Eklenenler

- Tek katmanlı 25 GB BD-R deney modu: kaynak yerleşimi, 3T/6T eşleme/tersi, parametre/önayar, canlı önizleme, ISO ve yardımcı kalibrasyon GUI/CLI'de eklendi. Çift katman yoktur; gerçek parlaklık doğrulanmalıdır.

- Blu-ray iç dosyaları UDF 2.01 ve 4 GiB üstü tek dosya destekler; üretim/akış tam geometri, bölümlü dosya yerleşimi ve sınırlı akış paylaşır.

- Yeni biçimsiz BD-R için yedek alansız sıralı yazma tüm 12,219,392 sektörü korur. Kapasite/boşluk/başlangıç önceden denetlenir; otomatik küçültme veya hata sonrası tekrar yoktur. Blok zaman aşımı/temizlik tanısı eklendi.

- CD/DVD/BD üretimi ve canlı önizlemede siyah beyaz renk taklidi ve bağımsız sRGB gama açılabilir; varsayılan kapalıdır. Dosya, akış ve kaynak önizlemesi aynı seçenekleri kullanır.

- Yardımcı kalibrasyon perspektif düzeltme, eğri noktası uydurma, parametre anlık kaydı/uygulama destekler. DVD kübik iz aralığı kullanır; doğrulanmış Verbatim DVD-R AZO (43533), öncesi/sonrası fotoğraf ve kayıtları içerir.

- Bağımsız siyah beyaz renk taklidi, DVD gri seviye deneyi ve Blu-ray bayt analiz araçları, talimat ve yeniden üretim adımlarıyla eklendi.

### İyileştirmeler ve düzeltmeler

- Canlı önizlemeye CD/DVD/BD izdüşüm/geri okuma, çözünürlük, pencereye sığdırma, 1:1, fare yakınlaştırma/sürükleme eklendi; önayar eşitleme, göz atma komutları, DVD örnek konumu düzeltildi.

- CD/DVD ortak iki siyah beyaz seviye kullanır. İkili girdi ayrık pikselleri korur, halka kopyaları ölçekleme sonrası yeniden renk taklidi uygular. Eski çok seviyeli çıktılar geri okumada eski seviyeleri tanır.

- CD/DVD örneklemesine SIMD ve sınır yedeği; Blu-ray'e sınırlı paralel üretim/tamsayı SIMD eklendi. Regresyonlar skaler, paralel ve akış eşitliğini doğrular.

- Yazma hızı aygıt onaylı miktardan hesaplanır; sorgu ve yazma sıralanır. Hazırlık/sonlandırmada eski hız gösterilmez; gerçek yazma deneysel kalır.

- Çok monitör, DPI ve görev çubuğu konumu için büyütme sınırları düzeltildi, ondalık hassasiyet birleştirildi. Ana/son işler Blu-ray destekler; CD WAV için CUE isteğe bağlıdır.

- Kalibrasyon kayıtları schema 6 kullanır, eski desen kayıtları reddedilir. Önayar/eski iz/görüntü uyumluluğu ilgili belgelerde; gerçek geometri ve parlaklık aygıt/ortamla doğrulanmalıdır.

### Açık kaynak ve belgeler

- CDImage türevi serpiştirmeyle uyumlu GPL-3.0-or-later benimsendi, özgün yazar telifleri ve teşekkürler tamamlandı.

- Üçüncü taraf ve HarmonyOS Sans lisanslarının tam metni korundu; Hakkında çevrimdışı gösterir, tek dosya EXE de içerir.

- README'ye ECMA-267, ECMA-359, ISO/IEC 30190:2021 ve resmî bağlantılar eklendi; araçlar, doğrulama, önbellek hariç tutma ve kişisel yol örnekleri düzenlendi.

## [1.0.26] - 2026-09-12

- İlk GitHub Release: Windows x64 tek EXE .NET içerir, indirince çalışır.

- CD WAV + CUE/RAW, DVD çizim ISO/karma disk, GUI, CLI, canlı önizleme, halka kopyalama ve düzenlenebilir önayarlar içerir.

- CD WAV/akış sesinin bayt sırası düzeltildi, eski RAW korundu; fiziksel kullanıcı geri bildirimi düzeltme sonrası gölgelenmenin bittiğini doğruladı.

- Deneysel CD/DVD doğrudan yazma içerir; fiziksel geri bildirim vardır, farklı aygıt/ortam/girişlerde kapsam sınırlıdır.

- GitHub Actions derler; modül regresyonları, DVD öz sınama, son tek dosya RAW/WAV ve GUI görüntü kontrollerini çalıştırır. Notlarda SHA-256 sağlama değerleri bulunur.

- Ortak sürüm kaynağı eklendi; sürüm ve ilgili günlük değişince paket üretilir. Ön yayın/elle tekrar desteklenir, ayrı MSIX süreci korunur.

## İlk GitHub yayınından önceki geliştirme kayıtları

Bunlar birikimli geliştirme kayıtlarıdır. Doğrulama durumu her kaydın dönemini yansıtır; güncel kapsam için `docs/VALIDATION.md` geçerlidir.

- GitHub Actions stratejisinde sürüm `Directory.Build.props` içinde tutulur. Yalnızca `main` birleştirmesinde sürüm/günlük birlikte ve tam sürüm değişince CI/son tek dosya kontrolü sonrası etiket, çalışma zamanı dahil Windows x64 EXE ve Release taslağı üretilir. alpha/beta/rc ve SHA-256 desteklenir; bakımcı inceleyip yayımlar, herkese açık varlıklar ezilmez. MSIX elle kalır, CI ve varsayılan sürümü paylaşır.

- CD varsayılan çıktısı standart WAV (44.1 kHz, 16 bit, stereo PCM) ve `FILE ... WAVE` CUE oldu. cdrecord WAV'ı, ImgBurn CUE'yu açar; eski `.raw` büyük uçludur. WAV gecikmeli serpiştirme sonrası küçük uçluya çevrilir; akış aynı başlıksız PCM'yi açık seçer ve IMAPI uyuşmazlığı giderilir. Önizleme RIFF ayrıştırıp bayt sırasını normalleştirir; asimetrik ses ve Windows RAW bayt koruma regresyonu eklendi.

- DVD akışına karma klasör eklendi: ISO9660/Joliet planından sonra geri dönmeyen akış iç dosya, ECC dolgu ve dış deseni sırayla yazar; tam geçici ISO gerekmez.

- Halka kopyalama üretim ara görüntüsü 2048×2048'den 8192×8192'ye çıktı; kayıt önizlemesi 4096×4096, canlı 2048×2048. Günlük her kopyanın gerçek piksel boyutunu ekleyerek düşük çözünürlükte ayrıntı kaybını önler.

- 80/74 dakika CD, 12 cm tek DVD ve 8 cm tek Mini DVD önayarları eklendi. Üretim/canlı önizleme çift yönlü eşitlenir; elle değişiklik Özel olur.

- Ölçülen Ritek tıbbi su mavisi CD önayarı eklendi: 24.911275–57.931155 mm, 359845 sektör.

- Ölçülen Verbatim CD-R AZO (43438) eklendi: 24.837775–58.020875 mm, 359848 sektör.

- Ölçülen Verbatim DVD-R AZO (43533) eklendi: 23.9968875–57.9779875 mm, 2,297,888 sektör.

- CD/DVD önayarları katmanlı JSON oldu. Yerleşikler uygulamayla güncellenir; kullanıcı katmanı ilk özel CD/DVD değerlerini üretip yalnızca ekleme, aynı ID geçersiz kılma ve devre dışı girdilerini saklar. Yalnızca kapasite/üretim geometrisi bulunur, tuval/ölçülen kalibrasyon yoktur. Varsayılan metin dili ve ad/açıklama anahtarları gelecekte i18n için ayrılmıştır, JSON bozulmaz. Arayüz kullanıcı katmanını açar/yeniler; hatalı düzenleme açık hata verir, son geçerli önayar korunur.

- Ondalık önayarlar 3/6 haneye kesilmez, gerçek tam hassasiyetle gösterilir.

- CD yerleşik/GUI/CLI tarama hızı eski 1100 mm/s'den ECMA-130 alt sınırı 1200 mm/s'ye düzeltildi; standart aralık testi eklendi.

- Deneysel CD/yalnızca çizim DVD, 64 MiB sınırlı tamponla Windows IMAPI2'ye sürekli üretir; tam ISO/RAW tutmaz. Otomatik testler geçti, o aşamada fiziksel doğrulama bekliyordu.

- Gereksiz `DoNotFinalizeMedia` nedeniyle bazı CD yazıcılarında oluşan `0xC0AA0502` düzeltildi. Kapatma/boşalma koruması güvenli IMAPI2 varsayılanlarını kullanır; temizleme `ReleaseMedia` hatası ilk gerçek yazma hatasını örtemez.

- İki ek sektör isteyen ve sesi reddeden Track-at-Once yerine Raw CD/DAO kullanıldı. Windows RAW oluşturucusu lead-in, Cooked R-W alt kanalları ve lead-out'u akışla ekler; ölçülen 359,845 sektör aynı diskin yasal DAO sınırına sığar. En çok 3 DAO sektörlük sınırlı son overburn korunur, fazlası yazmadan reddedilir. Hız raporlanmazsa donanımda doğrulanmış yaklaşık 16× kararlı yedek ayar seçilir.

- Mevcut diskin yazma hızlarını okuma/seçme eklendi. Otomatik mod CD/DVD'yi yazmadan tekrar okuyup hatayı azaltmak için en düşük hızı seçer; elle seçim kalır, aygıtın benimsediği ayar kaydedilir.

- Fiziksel DVD sonucuyla dispersion kutupluluğu ters çevrildi: siyah karıştırılmış `0xA5`, beyaz `0x92`; çizim/karma akış aynıdır.

- Sınırlı tampon 16 MiB'den 64 MiB'ye, ön dolum 8 MiB'den 32 MiB'ye artırılarak 16× DVD üretim dalgalanmasına pay sağlandı.

- MSIX öncesi RID önbelleği silinir, yayın sırasında kaynak değişimi ve pakette eski `DoNotFinalizeMedia` kaldırılması denetlenir; yeni ana uygulamada eski yazma DLL'i kalmaz.

- Ayrı `DiscImageStudio.Burning`, yazıcı listesi, boşluk/kapasite, ikinci onay, ilerleme ve iptal eklendi; eski görüntü dosyası akışı korunur.

- Otomatik sayılı halka kopyalama, kaynağı diskin dışına bakacak şekilde çoğaltır; iç/dış güvenlik sınırları ayarlanır.

- Halka yerleşimi düzeltildi: sayı otomatik, yer azsa tüm kopyalar aynı oranda küçülür; bozulma, kırpma, üst üste binme olmaz.

- CD/DVD ortak `DiscImageStudio.Imaging` ön işleme katmanı eklendi, gelecekte Blu-ray tarafından kullanılabilir.

- Ayrı canlı önizleme CD/DVD destekler; parametreler çift yönlü eşitlenir, 450ms gecikmeyle otomatik yenilenir.

- CD geometri önizlemesi DVD ile aynı örnek ortalamalı gri seviyeye geçti.

- DVD arayüz/modülü hızlı doku, hızlı çıktı ve saat yönüne (CW) sabitlendi; algoritma/yön seçimi kaldırıldı.

- Arayüze PNG yükleme/gösterme eklendi; genel giriş stilinin günlük yüksekliğini sınırlaması düzeltildi.

- .NET 9 WPF içinde `StreamSource` ile PNG yüklerken yanlış URI önbellek atlama nedeniyle önizleme hatası düzeltildi.

- İmzalı MSIX'te CD üretim/önizleme günlüğünün WPF ve invariant globalization çatışmasıyla çökmesi düzeltildi.

- Ortak WPF GUI eklendi.

- CD-DA/DVD iş akışları birleştirildi, DVD motoru yalıtıldı.

- `DiscImageStudio.Core` disk modülü soyutlaması eklendi.

- CD/DVD uyarlayıcıları ve gelecekte Blu-ray için genişletme noktaları eklendi.

- MSIX paketleme, mağaza kaynakları, GitHub CI ve yayın belgeleri eklendi.
