# Yerelleştirme denetimi

[简体中文](LOCALIZATION_AUDIT.zh-CN.md) | [繁體中文](LOCALIZATION_AUDIT.zh-TW.md) | [English](LOCALIZATION_AUDIT.md) | [日本語](LOCALIZATION_AUDIT.ja.md) | [Türkçe](LOCALIZATION_AUDIT.tr.md)

Güncelleme: 2026-11-04. Denetim; depoya ait arayüz, CLI, tanılar ve belgeleri Basitleştirilmiş Çince, Geleneksel Çince, İngilizce, Japonca ve Türkçe için kapsar. Belirlenen çeviri eksikleri tamamlandı; Windows CI artık çalışma zamanı yerelleştirmesini ve düzen matrisini doğruluyor. Katalog varlığı ve yapısal denetim tek başına anlamsal doğruluğu kanıtlamaz.

## Kapsam ve inceleme

| Alan | Kanıt |
| --- | --- |
| Ortak kaynaklar | 2053 girdinin beş dilde metni dolu; yinelenen anahtar/alan yok, gömülü kaynaklar kaynak dosyayla aynı |
| Kaynak başvuruları | Önayarlar, aşamalar, önizleme başlıkları, kalibrasyon ve dosya filtreleri dahil 2077 statik/dinamik C#/XAML başvurusu çözülür |
| Arayüz anlamı | Mevcut 749 girdi beş dilde incelendi: Common 154, Workflows 239, Workspace 151, Xaml 205 |
| CLI ve tanılar | Masaüstü/taşınabilir CLI, bağımsız DVD motoru, Core, CD/DVD/BD, ImageIO, Imaging, ISO/UDF ve yazma katmanı, projeye ait yardım/hata/ilerleme/sonuç için ortak kaynak kullanır |
| Araçlar | Üç C#, altı Python aracı ve dört PowerShell paketleme/kurulum betiği beş dil metni ve dil seçimi destekler |
| Belgeler | README, tüm değişiklik geçmişi, teknik kılavuzlar, politikalar ve bu denetim dahil 23 kaynak belgenin tümü beş dilde kapsanır |

Anlam incelemesi eylem fiillerini, sınırlayıcıları, yazıcının benimsediği hız ayarını, yansıyan parlaklığı, %60 dahil kapsama sınırını, kalibrasyon kaydı terimini, yalnızca seçili noktayı silmeyi, çıktı klasörü kapsamını ve iki yönde yakınlaştırmayı düzeltti. Geleneksel Çince serpiştirme terimleri ve dil öz adları tutarlıdır. Uzun etiket/başlık/kalibrasyon düğmeleri satır kaydırıp büyür, Compact en küçük düğme yüksekliğini korur; Windows CI düzen denetimleri artık geçiyor.

Ayrı kaynak envanteri, çeviriyle sarılmamış CJK/İngilizce ifade adaylarını ve XAML gösterim özelliklerini üretildikleri yere kadar izledi. Core uydurma uyarıları gösterimde çevrilir; ham sınıflandırma metni korunur. Kayıtlı kullanıcı varsayılanları, test verisi adları, protokol alanları ve aygıt ayrıntıları açıklayıcı metinden ayrıldı. Yedi DVD akış hatası ve boş devre dışı önayar bağlamı bulundu/düzeltildi; çalışma zamanı regresyonları tüm dilleri kapsar. Kaynak korumaları DVD uyarlayıcısı özel durum sabitlerini ve masaüstü/araç XAML gösterim özelliklerini de denetler.

İncelenmiş arayüz katalog parmak izleri UTF-8 ve normalize satır sonlarını kullanır. İncelenen sürümü belirtir, anlamı otomatik kanıtlamaz.

| Katalog | SHA-256 |
| --- | --- |
| `Common.json` | `7c304b1575bf7d3494c0f73032f45524e4439ce43f8bef24225041fabac971bf` |
| `Workflows.json` | `2a27cd6337ff55c4841271ad851da0b7c4bb6abede365d710714a210d9f32112` |
| `Workspace.json` | `b389bbf7a35a094f1b405e6e475b7af953a0ff045d32bfb096d465fc7063ba07` |
| `Xaml.json` | `f3728ada2da11e6be5614e2733e94e9b2e8ae0d448eedd7b6bb5bfae2250ed6f` |

## Korunan davranış

Genel dil seçenekleri ve ortam tercihi komut satırı girişlerinde ortaktır. Bölgesel/sistem yedek seçimi, geçersiz/eksik/yinelenen seçenekler, UTF-8 ve canlı arayüz dil seçimi kapsanır. UI culture değişirken sayısal kültür ve ayrıştırma korunur; önbellekteki modül açıklamaları dili izler. Başlangıç hata kaynakları bağımsız yüklenir, katalog başarısızlığı ve CLI hata sarmalayıcısı özyinelemeye giremez.

Komutlar, seçenek adları, şema anahtarları, teknik ID'ler, seçenek değerleri, varsayılan dosya adları, katkıcılar, kullanıcı ad/açıklamaları, yollar ve kayıtlı geometri değişmez. Özgün OS/üçüncü taraf ayrıntıları ve resmî lisans metinleri korunur. İlk kullanıcı önayarları yalnızca aile, ID ve değiştirilmemiş metin eşleşirse gösterimde çevrilir. İspanyolcanın etkin olmayan 24 kısmi girdisi, desteklenen beş dilin dışındadır.

Kodlama kararları ve ikili çıktı çevrilmiş metne bağlı değildir; yazma açık sonlandırma bayrağı taşır. Testler diller arasında proje verisini, CD ses/veri, DVD EFMPlus/ISO, BD ISO/prob, ISO9660/Joliet/UDF baytlarını, sayısal analizi ve üretilen C# tablolarını korur; fiziksel disk sonucu çıkarılmaz.

## Belge bütünlüğü denetimi

Bildirim, ana kaynak karmasını ve dil yollarını kaydeder. Denetim başlıkları, paragrafları, maddeleri, tablo boyut/sayılarını, satır içi kodu, komut/formülü, başvuru hedefini ve yerel bağlantıyı korur. Açık istisnalar önayar örneği ad/açıklaması ile iki görünür Mermaid etiketini çevirir; JSON tür/hassasiyet/ID/katkıcıları ve diyagram düğüm/bağlantı/yön/rakamları korunur. Eksik/boş etiket, yinelenen JSON alanı ve geçersiz sayı başarısız olur.

İki dilli yayın kaynağı günlüğü korunur. Açıkça belirtilen paralel listeler aynı madde sayısı ve teknik belirteçlere sahip olmalıdır; okuma kopyası her değişikliği bir kez gösterir. Tüm 122 farklı girdi, tarih ve özgün doğrulama sınırları korunur. Üretilen rapor kaynak envanterine girmez; yok sayılan artifacts altında, kaynakta başvurulan ve açıkça kaydedilen yalnızca dört çıktı temiz kopyada eksik olabilir. Diğer yerel bağlantılar mevcut olmalıdır.

Tam belge denetimi 23/23 bildirir, CI için zorunludur. Yedi kural regresyonu bu kısıtları korur. Yapısal denetim dil incelemesini tamamlar; kaynak değişirse yeniden inceleme gerekir. Bakım ve Windows görüntü komutları için [LOCALIZATION.md](LOCALIZATION.tr.md) belgesine bakın.

## Doğrulama ve sınırlar

Windows hedeflemesi açık Release çözümü sıfır uyarı/hata ile derlenir. Gerçek beş dil DVD akış hataları, özel durum tür/parametreleri, kayıtlı veri, katalog hata kaynaklarının bırakılması ve sayısal kültür dahil ortak regresyonlar geçer. Taşınabilir CLI paket duman testi, DVD öz sınama ve C# Blu-ray probu geçti; prob 256 bağımsız bayt vektörü ve sonlu desenle eşleşir. Sahte yazıcılar donanıma erişmeden bayt, MMC komut/zaman aşımı, ilerleme, iptal ve temizliği korur.

PowerShell, 45 eski yayın testi ve 69 güvenli dil ön kontrolünden geçti; 47 anahtar, değişmeyen meta veri, Unicode yollar ve alt süreç dili kapsanır. Paket derlenmez, imzalanmaz, kurulmaz, yetki yükseltilmez. Python 134 anahtarı denetler; altı araç özgün sayı/şema referanslarıyla entegrasyonu geçer. Referans aramaları periyot 8'e kadar, üç grafik türü beş dilde glif/metin sınırını geçer; beş örnek grafik incelendi. Kasıtlı geçersiz atlas dahil veriler sentetiktir; fiziksel ölçüm veya tam üretim prob taraması değildir.

Windows CI; WPF dil değişimi, glif/düzen görüntüleri, renk taklidi UI/PNG, DVD atlas/önizleme ve Windows PowerShell ön kontrollerini geçti. Paket kurulum yolları henüz çalıştırılmadı. Testler gerçek yazıcı performansını veya optik parlaklığı kanıtlamaz. Bunlar doğrulama sınırlarıdır, ek donanım doğrulaması iddiası değildir.

## Kontrolleri yeniden çalıştırma

```sh
dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release
dotnet run --project tests/DiscImageStudio.Burning.LocalizationTests -c Release
python3 tests/test_cli_package.py /absolute/path/to/disc-image-studio
python3 tests/test_probe_localization.py /absolute/path/to/BluRayByteProbe.dll
python3 -m unittest discover -s tests -p test_python_localization.py
python3 tests/test_python_tool_localization.py
python3 -m unittest discover -s tests -p test_documentation_translation_rules.py
python3 tests/test_documentation_translations.py --require-complete
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

Windows'ta çalışma zamanı paketini de çalıştırın ve dil görüntülerini inceleyin:

```powershell
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release
```

Ayrı Windows CI süreci beş dilde 325 düzen senaryosu denetler: CD/DVD/BD için 1366×900 koyu ve 960×700 açık çalışma alanları, genişletilmiş önizleme/geri okuma, kalibrasyon panelleri, ayar kategorileri ve yeni/düzenle proje pencereleri. Dil değişimi sonrası yüklü başlıklar, çizilmiş metnin sınırları ile görünür kırpma alanları ve eksik glifler kontrol edilir; kasıtlı üç nokta ve son projelerin yollarını kaydırma kabul edilir. Geçici depolar ve yazamayan sahte disk yazıcısı verileri yalıtır; kayıtlı proje baytları, geometri ve sayısal kültür de denetlenir. Başka monitör taklit edilmez, gerçek DPI kaydedilir.

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

Çalıştırıcı bu Linux ortamında da derlenir; Windows CI tüm 325 senaryoyu sıfır sorunla tamamladı. Denetim başarısız olsa da PNG ve JSON raporu yüklenir. Boş projeler sentetik test verileridir; matris fiziksel optik sonucu veya tüm dolu iş akışı durumlarını doğrulamaz.
