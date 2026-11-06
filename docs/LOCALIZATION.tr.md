# Uluslararasılaştırma

[简体中文](LOCALIZATION.md) | [繁體中文](LOCALIZATION.zh-TW.md) | [English](LOCALIZATION.en.md) | [日本語](LOCALIZATION.ja.md) | [Türkçe](LOCALIZATION.tr.md)

Sağ üstte sürekli görünen “dişli + Ayarlar” düğmesine tıklayın; “Genel → Dil” altında “Sistemi izle”, “简体中文”, “繁體中文”, “English”, “日本語” veya “Türkçe” seçin. Kaydettiğinizde hemen uygulanır. Ayarlar düğmesi ana sayfada ve tüm çalışma alanlarında görünür; Ctrl+, ile de açılabilir. İptal, Escape veya ayarlar penceresini kapatma dili kaydetmez. Dil değiştirmek uygulamayı yeniden başlatmaz, projeyi kapatmaz veya görevleri kesmez.

`settings.json` içindeki `language`, `system`, `zh-CN`, `zh-TW`, `en`, `ja` veya `tr` kullanır. Dil ve tema varsayılan olarak sistemi izler; eksik, boş veya desteklenmeyen dil değerleri de sistemi izlemeye döner. Mevcut yapılandırmadaki geçerli dil ve tema seçimleri korunur. Sistemi izleme, başlangıçta alınan sistem arayüz dilini kullanır: `zh-TW`, `zh-HK`, `zh-MO`, `zh-Hant` ve açık `zh-Hant-*` etiketleri Geleneksel Çinceyi; `zh-CN`, `zh-SG`, `zh-Hans` ve diğer Çince sistemler Basitleştirilmiş Çinceyi; `ja`, `ja-JP` gibi Japonca sistemler Japoncayı; `tr`, `tr-TR` gibi Türkçe sistemler Türkçeyi; İngilizce, Fransızca ve diğer sistemler İngilizceyi seçer. Çalışırken değişen sistem dilini uygulamak için uygulamayı yeniden başlatın. Tema, Windows uygulama renk modundan koyu veya açık modu seçer. Dil `.discproj` dosyasından bağımsızdır; proje adları, kullanıcı ön ayarı adları, katman adları, dosya yolları ve sayısal ayrıştırma kuralları çevrilmez.

## Metin ekleme ve bakımı

Çeviriler `src/DiscImageStudio.Localization/Catalogs/*.json` konumundadır ve derleme sırasında ek çalışma zamanı bağımlılığı olmadan derlemeye gömülür. Her anahtar `zh-CN`, `zh-TW`, `en`, `ja` ve `tr` metinlerini içerir. Geleneksel Çince, “設定”, “專案”, “檔案”, “燒錄” gibi terimler kullanan ayrı kaynaklara sahiptir; kullanıcı verileri çalışma sırasında dönüştürülmez:

```json
{
  "示例 {0}": { "zh-CN": "示例 {0}", "zh-TW": "範例 {0}", "en": "Example {0}", "ja": "例 {0}", "tr": "Örnek {0}" }
}
```

C# içinde `L.T("源文案")`, dize enterpolasyonu için `L.F($"示例 {value}")` kullanın; kaynak anahtarı karşılık gelen .NET bileşik biçimi `示例 {0}` olur. Açık kaynak anahtarı için `L.Format(key, arguments)` kullanılabilir. Çeviriler bağımsız değişkenlerin sırasını değiştirebilir ancak indekslerini ve biçim belirteçlerini korumalıdır. XAML, `xmlns:loc="clr-namespace:DiscImageStudio.Localization"` ve `{loc:Text ResourceKey}` kullanır. `TextExtension` yükleme sırasında geçerli dilde metin yazar; dil değiştiğinde yüklü denetimleri ve şablonları yerinde günceller.

Ortak ifadeleri yalnızca bir kez tanımlayın; anahtarlar tüm kaynak dosyaları arasında benzersiz olmalıdır. Geçerli dilde çeviri bulunamazsa Çinceye dönülür; anahtar yoksa anahtarın kendisi gösterilir. Başlangıçta pencereleri ve ViewModel nesnelerini oluşturmadan önce dili başlatın. Yalnızca UI culture ayarlayın, numeric culture değerini koruyun. Protokol tanımlayıcılarını, komutları, yolları veya kullanıcı verilerini çevirmeyin; çevrilmiş metni mantıksal kararlarda kullanmayın.

Amaç, depoya ait arayüz, komut satırı yardımı, tanılar ve belgelerde desteklenen beş dil arasında 1:1 anlamsal eşdeğerlik sağlamaktır. Ortak çalışma zamanı ve kaynaklar `src/DiscImageStudio.Localization` konumundadır; masaüstü ve CLI aynı kaynakları kullanır. CLI yardımı, modül ve seçenek açıklamaları, CD modül mesajları, DVD modül durumu ve komut yönlendirme hataları bağlanmıştır. Blu-ray, görsel yükleme, Core, CD ve ISO/UDF projelerine ait özel durumlar ile Blu-ray ilerleme özetleri, açıklayıcı JSON ve fotoğraf düzeltme yönlendirmeleri de çevrilmiştir; makine tanımlayıcıları, geometri değerleri ve ikili çıktı korunur. Kapsam ve tamamlanan/bekleyen doğrulama için [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.tr.md) belgesine bakın. Üçüncü taraf ve işletim sistemi özel durum ayrıntıları ile resmî lisansların özgün metinleri değişmez.

## Komut satırı dili

Komut satırı, komuttan önce veya sonra genel `--language zh-CN|zh-TW|en|ja|tr|system` seçeneğini ve `--language=ja` biçimini destekler. Açık seçenek `DISC_IMAGE_STUDIO_LANGUAGE` ortam değişkeninden önceliklidir; aksi durumda sistem arayüz dili izlenir. Açıkça belirtilen desteklenmeyen diller, yinelenen seçenekler ve eksik değerler çıkış kodu 1 döndürür. Bağımsız DVD programı, platformlar arası CLI ve Windows uygulamasının konsol giriş noktası bu seçeneği paylaşır. GUI komutsuz başlatıldığında kayıtlı arayüz ayarlarını kullanmaya devam eder.

```sh
disc-image-studio --language ja --help
disc-image-studio cd-generate --language tr --input picture.png --output track.wav --sectors 2
```

Yalnızca görüntülenen metni çevirin; komut adlarını, seçenek adlarını, protokol tanımlayıcılarını, dosya yollarını, kullanıcı girdisini veya JSON alan adlarını çevirmeyin. Proje parametreleri mevcut ayrıştırma kurallarını korur; dil seçimi sayısal kültürü değiştirmez. CLI, adlandırılmış arayüz kültürlerini desteklemek için invariant globalization özelliğini kapatır.

Kalibrasyon eğrisi uydurmanın nokta yönlendirmeleri arayüzde gösterilirken çevrilir. Core dönüş değerleri özgün metni korur; otomatik arama kararları çevirilere dayanmaz. Ön ayarlar kaydedilirken otomatik geometri açıklamaları taşınabilir özgün metni ve bir biçim kaynak anahtarını korur. Gösterimde geçerli dil ve sayısal kültürle yeniden biçimlendirilir; elle düzenlenen açıklamalar özgün hâlinde kalır.

## Doğrulama

WPF'ye bağlı olmayan bütünlük kontrolleri Windows, Linux ve macOS üzerinde çalışır; CI bunları her platformda yürütür:

```sh
dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release
```

Kontrol; beş dilde boş olmayan metinleri, yinelenen anahtarları, bileşik ve sayı/tarih biçimlerini, dosya iletişim kutusu filtrelerini, C# sabit ve enterpolasyon çağrılarını, XAML metinlerini ve şablon tetikleyicilerini, yerleşik ön ayarları, dinamik kalibrasyon yönlendirmelerini ve kullanıcı ön ayarı geri dönüşlerini doğrular. Önbellekteki modül açıklamalarının dil değişimini izlemesini, özel durum türlerini ve parametre adlarını, proje, ISO9660/Joliet ve UDF çıktılarının diller arasında tutarlılığını da kontrol eder. Çevirilerin anlamsal olarak tamamen doğru olduğunu kanıtlayamaz; Windows üzerinde çalışma zamanı dil değişimi ve düzen ekran görüntüsü kontrollerinin yerini alamaz.

Belge çevirilerinin paragraf yapısı, listeleri, komut örnekleri, bağlantıları ve kaynak sürümleri ayrıca kontrol edilebilir:

```sh
python3 tests/test_documentation_translations.py
python3 tests/test_documentation_translations.py --require-complete
```

Belge kontrolü paragrafları, tablo satır ve sütunlarını ve sayısal değerleri de korur; yerelleştirilmiş ondalık ve binlik ayırıcılarına izin verir. İlk komut yalnızca bildirimde tamamlanmış olarak kaydedilen belgeleri doğrular ve eksikleri bildirir; ikincisi depo genelindeki belge bütünlüğünü kontrol eder ve eksik varsa başarısız olur. Güncel sonuçlar ve kalan işler için [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.tr.md) belgesine bakın.

CI, deponun tam bütünlük kontrolünü zorunlu kılar.

Bildirim, önayar JSON örneklerinde `displayName` ve `description` çevirisine açıkça izin verebilir; alan adları, türler, sayısal hassasiyet, ID ve katkıda bulunanlar yine eşleşmelidir; yinelenen alanlar ve geçersiz sayılar reddedilir.

Üretilen test raporları kaynak belge envanterine dahil edilmez. Bildirim, yeniden üretilebilir sonuç bağlantılarını `generated_artifacts` ile açıkça kaydedebilir; yalnızca kaynakta başvurulan ve yok sayılan artifacts dizini altındaki yollar kabul edilir. Diğer yerel bağlantılar yine mevcut olmalıdır.

Bildirim, görünen metni çevrilebilen Mermaid düğümlerini `translated_mermaid_labels` içinde listeleyebilir; düğüm kimlikleri, bağlantılar, yön, diğer etiketler ve etiketlerdeki rakamlar eşleşmelidir; eksik veya boş etiketler reddedilir.

Yayın kaynağı değişiklik günlüğü paralel Çince ve İngilizce alt bölümleri korur. Bildirim iki başlığı `parallel_language_sections` ile açıkça tanımlayarak her dilin okuma kopyasında aynı değişikliği bir kez gösterebilir. İki sürüm de aynı madde sayısına ve satır içi teknik tanımlayıcılara sahip tam madde listesi olmalıdır; eksik alt bölüm veya liste dışı ek içerik reddedilir. Tarihsel alt başlıklar ve diğer tüm metin korunur.

Diske yazma katmanının istisnaları, ilerlemesi, işlem açıklamaları ve cihaz tanılaması da ortak kaynakları kullanır. `OpticalBurnOperation.IsFinalizing` sonlandırma durumunu açıkça taşır; sonlandırma kararını çevrilmiş metni karşılaştırarak vermeyin. Aşağıdaki regresyon WPF gerektirmez; gerçek disk yazım kitaplığını sahte cihazlarla kullanarak beş dilde CD/BD baytlarını, MMC komutlarını ve zaman aşımlarını, ilerleme aşamalarını, iptal belirteçlerini ve temizleme davranışını kontrol eder. Gerçek disk yazıcılarına erişmez veya fiziksel disk yazımı yapmaz.

```sh
dotnet run --project tests/DiscImageStudio.Burning.LocalizationTests -c Release
```

PowerShell paketleme ve kurulum betikleri ortak kaynakları `packaging/Localization.ps1` üzerinden okur; yerel `-Language` parametresini ve ortam tercihini destekler. Seçilen dili alt denetimlere ve yükseltilmiş yükleyiciye iletir. Meta veriler, sürüm notu metni, kimlikler, yollar, sağlama toplamları ve sayısal kültür değişmez. Bu testler yalnızca meta veri ve ön denetim hata yollarını, benzetilmiş alt süreçle çalıştırır; paket derlemez, imzalamaz, yüklemez veya yetki yükseltme istemez. Windows PowerShell için pwsh yerine powershell kullanın.

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

C# kaynakları dil seçiminden sonra gecikmeli yüklenir. Boş katalog ve yinelenen anahtar tanılaması ayrı gömülü hata kataloğunu doğrudan okur; ana katalog yüklenirken özyinelemeli çeviri yapılmaz. Ortak çalışma zamanı regresyonları beş dilde tanılama, özgün kaynak adları, istisna türleri, akış kapatma ve sayısal kültürü kontrol eder. CLI hata öneki de bu bağımsız kaynağı okur; böylece giriş noktası hata işleyicileri başarısız ana katalog yüklemesini yeniden denemez. Python hata sarmalayıcıları da aynı yöntemi kullanır.

Python araçları aynı JSON kaynaklarını `tools/localization.py` üzerinden okur; genel dil seçeneklerini ve ortam tercihini paylaşır. Ayrıştırıcı yardımı/hataları, analiz açıklamaları ve grafik metinleri beş dili destekler; sayısal locale, alanlar, yollar veya algoritmalar değişmez. Standart kitaplık kontrolleri tüm Python araçlarını ve argparse biçim bağımsız değişkenlerini denetler. NumPy ve matplotlib gerektiren entegrasyon kontrolleri; en büyük periyodu 8 olan referans aramalarını, sentetik grafikleri ve kasıtlı geçersiz atlası kullanarak özgün betiklerin sayısal/şema referanslarını, girdi dosyalarını, yazı tiplerini ve grafik metni sınırlarını karşılaştırır; üretilen C# tablo kodunun dilden bağımsızlığını da doğrular. Bu test verileri gerçek disk ölçümleri veya tam prob taraması değildir.

```sh
python3 -m unittest discover -s tests -p test_python_localization.py
python3 tests/test_python_tool_localization.py
```

`LocalizationTests`; kaynak dil bütünlüğünü, biçim bağımsız değişkenlerini, sistem diline geri dönüşü, ayar kaydetme ve iptal davranışını ve kaydetme başarısızsa dilin uygulanmamasını kontrol eder. `RelocalizationTests`, çalışma sırasında dil değiştikten sonra proje geometrisinin, devam eden okumaların, önizleme sonuçlarının ve açık sayfa metinlerinin korunmasını veya doğru yenilenmesini kontrol eder.

```powershell
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language en --output artifacts/settings-en.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-CN --output artifacts/settings-zh.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-TW --output artifacts/settings-tw.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language ja --output artifacts/settings-ja.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language tr --output artifacts/settings-tr.png
```

Ekran görüntüleri, kullanıcı yapılandırmasını değiştirmeyen tek kullanımlık ayar dosyası kullanır. Dil eklerken normalleştirme ve çözümleme kurallarını, ayar açılır listesini, tüm kaynakları ve testleri birlikte genişletin.

Ayrı Windows CI süreci beş dilde 325 düzen senaryosu denetler: CD/DVD/BD için 1366×900 koyu ve 960×700 açık çalışma alanları, genişletilmiş önizleme/geri okuma, kalibrasyon panelleri, ayar kategorileri ve yeni/düzenle proje pencereleri. Dil değişimi sonrası yüklü başlıklar, metin genişliği/yüksekliği ve eksik glifler kontrol edilir; kasıtlı üç nokta kabul edilir. Geçici depolar ve yazamayan sahte disk yazıcısı verileri yalıtır; kayıtlı proje baytları, geometri ve sayısal kültür de denetlenir. Başka monitör taklit edilmez, gerçek DPI kaydedilir.

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

Çalıştırıcı bu Linux ortamında derlendi; Windows çalışması bir push sonrası depo CI çalıştırmasını bekliyor. Denetim başarısız olsa da PNG ve JSON raporu yüklenir. Boş projeler sentetik test verileridir; matris fiziksel optik sonucu veya tüm dolu iş akışı durumlarını doğrulamaz.

Üst projeden gelen yeni aygıt tanıları aşamalı olarak çevrilebilir; seçili dil eksikse İngilizceye dönülür. Tamamlanmış arayüz kaynaklarında beş dil zorunluluğu sürer. Aygıt alan adları, sayı biçimleri ve özgün işletim sistemi hataları korunur.
