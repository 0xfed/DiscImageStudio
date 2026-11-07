# Doğrulama notları

[简体中文](VALIDATION.md) | [繁體中文](VALIDATION.zh-TW.md) | [English](VALIDATION.en.md) | [日本語](VALIDATION.ja.md) | [Türkçe](VALIDATION.tr.md)

## Üst kaynak özellik eşitleme regresyonları

Üst kaynak `62efb5c` denetimleri `UsableSectorGenerationTests`, `UsableSectorPersistenceTests`, `StreamBurnCapacityTests`, `PresetApplicationTests` ve `CalibrationConfirmedFitTests` içerir. UDF/VAT güncellemeleri, zamanlama sonucu kullanılabilir sınırları, görsel seçici kırpması ve eski/yeni kuralın yeniden üretimi de kapsanır. Yedi dilli yerleşim matrisi korunur; bu testler yeni fiziksel yazma doğrulaması sağlamaz.

Bu sayfa geçerli kaynak kod için yeniden üretilebilir kontrolleri listeler. Otomatik regresyonlar algoritmaları, dosya yerleşimlerini ve uygulama iş akışlarını doğrular; fiziksel disk yazma sonuçları yazıcıya, donanım yazılımına ve diske bağlıdır.

## Yerel doğrulama

En son tam mimari regresyon 2026-10-28 tarihinde, arayüz düzeltmeleri, kalibrasyon oluşturma optimizasyonları ve Verbatim BD-R (64099) ön ayarı için (`3088802`, `49a0e4d`, `46e2c53` commit'leri) Windows ve .NET SDK 10.0.400 (çalışma zamanı 10.0.11), Release yapılandırmasıyla yürütüldü. Çözümün tamamının derlemesi (0 uyarı, 0 hata) ve tüm mimari regresyonlar geçti. Testler özel yürütülebilir test düzeneğini doğrudan çalıştırır; `dotnet test` kullanmaz.

Kalibrasyon diski yazımı bittikten sonra oluşturma optimizasyonlarının derlemesi, tam mimari regresyon ve DVD motoru öz testi tamamlandı. Verbatim ön ayarı eklendikten sonra derleme ve tam mimari regresyon yeniden geçti; GUI/CLI yarıçap, sektör sayısı ve kutupluluk tutarlılığı, ön ayar birleştirme ve komut satırı geçersiz kılmaları doğrulandı. v1.3.3 sürüm/değişiklik günlüğü doğrulaması ve 45 yayımlama aracı kontrolü geçti; yeni performans ölçümü veya fiziksel disk yazma sonucu eklenmedi.

Geçen yeni regresyonlar şunları içerir: BD kalibrasyonu paralellik anahtarı, varsayılan sıralı geri çağırımlar, kutupluluk ve çizim aralığı, toplu iş sırası ve iptal; CD kalibrasyonu dört noktalı örneklemenin koordinat bit eşitliği, iç içe geçirme ve bayt sırası, CD dosya modunun iç/dış halka skaler/SIMD bayt eşitliği; DVD analitik ve kod sözcüğü örneklemelerinin koordinat, indeks, bayt çıktısı ve paralel son toplu iş eşitliği.

Depo kökünden çalıştırın:

```powershell
dotnet restore DiscImageStudio.slnx
dotnet build DiscImageStudio.slnx --configuration Release --no-restore
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release --no-build
dotnet run --project src/DvdImageSolver --configuration Release --no-build -- selftest
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- selftest
./tests/Test-ReleaseTooling.ps1
```

Mimari regresyonların kapsamı:

- CD/DVD: siyah/beyaz eşleme, renk taklidi, sRGB gama, dosya/akış tutarlılığı ve SIMD/skaler örnekleme tutarlılığı.
- CD dosya modu: ISO9660/Joliet Çince dosyaların bağımsız okunması, iç/dış halka konumu ve proje kaydı, dosya bölgesi önizlemeleri, ham sektör geometrisi ve dosya/ileri akış çıktısı eşitliği.
- CD veri DAO: Mode 1 EDC ve tüm P/Q eşlik denklemleri, ana kanal bayt sırası, lead-in ve pregap adresleri, veri izi Q tanımlayıcıları ve CRC, ileri Seek ve kısa okumalar, iç/dış halka ISO'nun tam geri elde edilmesi. Ritek ön ayarının 359.845 sektörü korunur; ses izleriyle paylaşılan kapasite sınırı benzetilmiş yazıcıyla doğrulanır. Yerel IMAPI testleri yalnızca bellek içi imaj oluşturur, yazma aygıtı oluşturmaz.
- BD: 17PP/EDC/Reed-Solomon, 3T/6T eşleme ve kutupluluk, çizim aralığı dışını sıfırlama, UDF 2.50, 4 GiB'den büyük dosyalar, sidecar gidiş dönüşü ve geçersiz sınırların reddi.
- DVD/BD dosya konumu: varsayılan iç halka uyumluluğu, dış halka ISO9660/Joliet ve UDF dizinlerinin bağımsız ayrıştırılması, Çince dosya içerikleri, tam disk koordinatları, Seek yapılamayan akış/dosya çıktısı eşitliği, kapasite ve iptal korumaları, projedeki konum seçeneklerinin gidiş dönüşü.
- Üç disk türü modeli: proje schema 2 gidiş dönüşü ve schema 1 yükseltmesi, ön ayar schema 4 ile eski schema birleştirmesi, `ProjectSession` BD apply/capture işlemleri.
- Disk yazma: işlem dışlama, ilerleme, sıralı yazma, zaman aşımı, iptal ve kaynak temizleme; gerçek yazıcılar yerine benzetilmiş arayüzlerle.
- Akışla yazma gösterimi: ilerlemeyi yalnızca aygıtın onayladığı yazma miktarı ilerletir; CD ses/veri, DVD ve BD hız katı dönüşümleri, bilinmeyen veya geçersiz hızlar, 3 saniyede geçerlilik sonu, sonlandırma ve bitiş temizliği, aktarım günlüğü sınırlaması ve geç gelen olay koruması.
- Kalibrasyon yazma kaynağı: CD/DVD/BD etiketlerinin kayıtlı parametrelerle tutarlılığı, BD bit uzunluğu, kutupluluk ve çizim aralığı, disk türleri arasında geçiş, hız sorguları, benzetilmiş yazma istekleri ve onay metni.
- Kalibrasyon: kübik iz aralığı, BD çizim aralığı ve kutupluluk, fotoğraf düzeltmesi, eğri uydurma, oturum kaydetme/geri yükleme, disk ön ayarı eşitleme ve parametre hassasiyeti.
- Önizleme ve pencereler: CD/DVD/BD izdüşümü ve geri okuma, çoklu monitör ve görev çubuğu sınırları.
- Dosya bölgesi önizlemesi: iç/dış halka tasarım izdüşümü ve sidecar içeren çıktı geri okumada sarı vurgulama, DVD kübik iz aralığı sınırları, saf çizim veya geçersiz yerleşim bilgisinde vurgu olmaması, hatalı dizin/kapasite mesajları, geçiş ve sıfırlamada temizleme. Sarı katman ISO verisini değiştirmez.

GUI, mevcut komutlarla ekran dışında görüntü oluşturabilir:

```powershell
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/home.png --tab 0
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/export-bd.png --tab 3 --project sample.discproj
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/preview-bd.png --tab 2 --project sample.discproj --live-input image.png --live-disc bd --live-mode calibrate
dotnet run --project src/DiscImageStudio.App --configuration Release --no-build -- ui-snapshot --output artifacts/validation/burn-bd.png --tab 4 --project sample.discproj --burn-source calibration --burn-progress write
```

`--burn-progress`, `write`, `buffer`, `finalize` değerlerini kabul eder; gerçek disk yazmayı başlatmadan benzetilmiş ilerlemeyle arayüz oluşturur. 2026-10-28 tarihinde BD kalibrasyonu geniş ekran ve dar 800×650 pencere, hazırlık ve sonlandırma durumlarında kontrol edildi; alttaki yazılan miktar, hız, hız katı ve ilerleme çubuğu görünürdü. O oturumda DVD motoru öz testi de geçti; fiziksel disk yazılmadı.

`build` iş akışı ayrıca modül kataloğunun DVD öz testini, iki sektörlük CD çıktı kontrolünü ve ana sayfa ekran görüntüsünü yürütür. Yayımlanan tek dosyalı EXE'nin ek kontrolleri için [yayımlama kılavuzuna](RELEASING.tr.md) bakın.

O oturumda DVD ve BD'nin her iki dosya konumunun tasarım izdüşümleri, dış halka dışa aktarma sayfaları ve dış halka çıktı geri okuması toplam 8 ekran dışı görüntüyle kontrol edildi; DVD/uygulama öz testleri ve yayımlama araçlarının 45 regresyonu geçti.

Tasarım sayfası dosya seçim girişi ayrıca 7 arayüz durumunda kontrol edildi: DVD, BD, CD, klasör seçilmemiş, 18 katman, dar pencere ve düşük pencere yüksekliği. Geniş ekranda katman listesi kendi içinde kayar, dosya seçim alanı erişilebilir kalır; derleme 0 uyarı ve 0 hatayla tamamlandı.

v1.3.0 ayrıca `Build-Release.ps1` üzerinden .NET 10 kendine yeterli tek dosya doğrulamasını geçti: yalıtılmış dizinde yalnızca EXE varken yazıcı arayüzü derleme bilgisi, DVD öz testi, iki sektörlük RAW/WAV/CUE, CUE kapatma ve WPF arayüz oluşturma kontrolleri geçti. Son sürüm .NET / Windows Desktop 10.0.11'i içerir ve üst kaynak lisans bildirimlerini eksiksiz korur.

v1.3.2 tam mimari regresyonu, DVD motoru ve uygulama modülü öz testlerini, 45 yayımlama aracı kontrolünü geçti. Yeni regresyonlar, Blu-ray ön ayarı değiştirildikten sonra Farklı Kaydet ile proje onayında kutupluluğun tutarlı olduğunu ve son ön ayar silindikten sonra disk türleri arası seçimin ve geometrinin eşitlendiğini doğrular. Tuval yakınlaştırması %25–%1200 sınırlarını, artı/eksi düğmelerini ve pencereye sığdırma sıfırlamasını kapsar. Yeni CD, Blu-ray ve DVD proje ayarları ile 18 katmanlı tasarım sayfası ayrıca toplam 4 ekran dışı görüntüyle kontrol edildi.

## Çeviri ön denetimi

Paketleme çevirisi regresyonları derleme, imzalama, yükleme veya yetki yükseltme yapmadan meta verileri, tanılamayı, dil önceliğini, Unicode yollarını ve alt denetimlere dil iletimini kontrol eder; sayısal kültür değişmez. Ortak çalışma zamanı ayrıca kaynak yükleme hatalarının beş dildeki açıklamalarını, istisna türlerini ve akış kapatmayı doğrular. Windows PowerShell için aşağıda pwsh yerine powershell kullanın. Gerçek Windows kurulumu ve paketlemesi yine Windows üzerinde doğrulanmalıdır.

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

## Fiziksel disk doğrulamasının sınırları

Bu dosya konumu ve önizleme değişiklikleri fiziksel disk yazılarak test edilmedi. Otomatik testler aygıt yazma stratejisini, gerçek yansıyan parlaklığı veya disk yazma başarı oranını kanıtlamaz; Blu-ray işlevleri deneysel kalır.

v1.3.1 CD veri DAO yolu da fiziksel diske yazılmadı; tam mimari regresyonu, bağımsız tek dosyalı EXE başlatmayı, ses/CUE kontrollerini, iç/dış halka ISO dosya içeriklerini ve çıktı geri okumasını geçti.

Kayıtlı DVD kübik iz aralığı fotoğrafları ve parametreleri için [README öncesi/sonrası karşılaştırmasına](../README.tr.md#辅助标定展示) bakın. Geçmiş fiziksel model denetimi [PHYSICAL_MODEL_AUDIT.md](PHYSICAL_MODEL_AUDIT.tr.md) belgesindedir; sonuçlar belirtilen tarih ve kod temelini korur, geçerli kaynak kod doğrulamasının yerini alamaz.

Gelecekteki fiziksel disk kayıtları sürüm veya commit, yazıcı ve donanım yazılımı, disk modeli, ön ayar parametreleri, hız, oluşturma/yazma giriş noktası ve özgün fotoğrafları içermelidir. Tek bir aygıtın sonuçlarını tüm disklere genellemekten kaçının.

Yardımlı kalibrasyon artık kaba, sınır ve hassas aşamalarını içerir ve çizim projesinden bağımsız kullanılabilir. Disk kapasitesini doğrulayın, ardından her boş disk için ilgili aşamanın hedefini oluşturun. Yazılmış her diski kendi üretim kaydı ve fotoğrafıyla inceleyin. [Üst proje birleştirmesinden sonra kalibrasyon](CALIBRATION_TUTORIAL.tr.md).
