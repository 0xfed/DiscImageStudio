# Disc Image Studio'ya katkıda bulunma

[简体中文](CONTRIBUTING.md) | [繁體中文](CONTRIBUTING.zh-TW.md) | [English](CONTRIBUTING.en.md) | [日本語](CONTRIBUTING.ja.md) | [Türkçe](CONTRIBUTING.tr.md)

Disc Image Studio'ya katkılarınızı bekliyoruz. Lütfen önce bu belgeyi okuyun; sorun bildirimini, geliştirme ortamı kurulumunu, modül sınırı kurallarını, doğrulama gerekliliklerini ve katkı sürecini kapsar.

## Sorun bildirme

Genel sorunları GitHub Issues üzerinden bildirin ve açıklamaya şunları ekleyin:

- Uygulama sürümü ve edinme yöntemi (kaynaktan derleme / MSIX paketi) ile Windows sürümü.
- Güvenilir biçimde yeniden üretme adımları, beklenen davranış ve gerçekleşen davranış.
- İlgili çalışma günlükleri veya ekran görüntüleri.

Disk yazmayla ilgili sorunlarda ayrıca yazıcı modeli, ürün yazılımı sürümü ve disk markası/modelini ekleyin. Görünür sonuçlar ve ölçülen kalibrasyon bu donanımlara büyük ölçüde bağlıdır; bu bilgiler olmadan sorunun nedeni genellikle belirlenemez.

Kötüye kullanılabilecek güvenlik sorunlarını herkese açık Issue kayıtlarında **açıklamayın**. [Güvenlik politikasına](SECURITY.tr.md) uyun ve GitHub'ın özel bildirim özelliğini kullanın.

Özellik önerileri de Issues üzerinden yapılır: çözülmek istenen sorunu, beklenen davranışı ve kullanılabilir bir geçici çözüm bulunup bulunmadığını açıklayın.

## Geliştirme ortamı

- Windows 10 2004+ veya Windows 11.
- .NET 10 SDK. Depo, `global.json` ile `10.0.403` sürümüne sabitlenmiştir; kurulumdan sonra `dotnet` eşleşen SDK'yı otomatik olarak seçer.
- Testlerin üçüncü taraf çatı bağımlılığı yoktur; tümü depoyla gelen konsol regresyon programlarıdır.

```powershell
git clone <your-fork-url>
cd DiscImageStudio
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DiscImageStudio.App --configuration Release
```

## Bağımlılık ve derleme girdilerinin incelenmesi

Derlemeler SDK `10.0.403` ve çalışma zamanı `10.0.14` kullanır; kilitli geri yükleme varsayılan olarak etkindir. İncelenen NuGet bağımlılık grafikleri `eng/dependencies/{portable|windows}/{optionalRID}/{ProjectName}.lock.json` altında tutulur. `VersionPrefix`, proje başvuruları, paket sürümleri veya SDK/çalışma zamanı değiştiğinde, kilit dosyalarını aynı değişiklik içinde yeniden oluşturup inceleyin.

Beş CI platformunun tamamı için yalnızca wheel içeren eksiksiz Python kilidini yenilemek üzere Python `3.14` kullanın; desteklenen tüm hedef ve RID grafiklerini yenilemek için NuGet güncelleme betiğini çalıştırın. Depo kökünde çalıştırın:

```powershell
python packaging/Update-NuGetLocks.py
python packaging/Update-NuGetLocks.py --check
python packaging/Update-PythonLock.py
python packaging/Update-PythonLock.py --check
```

`NuGet.Config` yalnızca resmî NuGet kaynağına ve onaylanmış depo imzalama sertifikalarına izin verir. Paket kaynağı veya sertifika değişimlerini açıkça inceleyin ve mevcut kilitli paketleri doğrulamak için gereken sertifikaları koruyun. CI, kaydedilmiş kilitleri denetler; bunları otomatik olarak yeniden yazmamalıdır.

## Modül sınırları

Kodu değiştirmeden önce [mimari açıklamasını](docs/ARCHITECTURE.tr.md) okuyun. Temel tasarım tercihi, kabuğun herhangi bir disk kodlamasına bağımlı olmaması ve ortama özgü mantığın ilgili modülde kalmasıdır. İncelemelerde özellikle şu kurallar denetlenir:

1. CD/DVD/gelecekteki Blu-ray için fiziksel modülasyon, hata düzeltme, sektör düzeni veya dosya sistemi ayrıntılarını `DiscImageStudio.Core` ya da paylaşılan katmana koymayın.
2. Modüller GUI denetimlerinden durum okuyamaz; tüm girdiler `DiscJobRequest` üzerinden gelir. Modüller doğrudan pencere gösteremez.
3. Alt düzey algoritma güvenle iptal edilemiyorsa `SupportsCancellation`, `false` olmalıdır.
4. Modül kimlikleri ve komut adları genel olarak benzersiz olmalıdır. Katalog, başlangıçta çakışmaları reddeder.
5. Yeni komutlar `DiscCommandDescriptor` içine kaydedilmeli; katalog yönlendirme testleri ve asgari bir çıktı duman testi eklenmelidir.

## Kodlama kuralları

- Çevredeki kodun adlandırmasını, yapısını ve yorum yoğunluğunu izleyin. Derleme çıktısında uyarı ve hata olmamalıdır.
- Derlemeyi geçirmek için işlevleri kaldırmayın veya azaltmayın. Kendi inisiyatifinizle uyumluluk katmanları ya da yinelenen uygulamalar eklemeyin.
- Kasıtlı göründüğü düşünülen işlevleri veya kodu kaldırmadan önce amacınızı bir Issue veya PR açıklamasında belirtin.
- Derleme çıktılarını, ikili kaynakları veya özel anahtar / PFX dosyalarını depoya göndermeyin.

## Test ve doğrulama

Pull Request öncesinde aşağıdakilerin tümü geçmelidir:

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
dotnet run --project tests/DiscImageStudio.LocalizationTests --configuration Release
dotnet run --project tests/DiscImageStudio.ImageIOTests --configuration Release
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release
```

Değişikliğe uygun ek doğrulamalar yapın ve sonuçları açıklamada belirtin:

- Görsel yerleşimi, akışla disk yazma veya disk önayarı ayrıştırması değiştiğinde [VALIDATION.md](docs/VALIDATION.tr.md) belgesindeki ilgili regresyon maddeleriyle sonuçları karşılaştırın.
- GUI değişikliklerinde önemli sayfaları ekran dışında işlenen ekran görüntüleriyle kontrol edin; gerektiğinde `docs/images/` dizinini güncelleyin.
- Gecikmeli serpiştirme, EFMPlus veya ECC gibi kodlama zincirleri değiştiğinde `selftest` kapsamı aynı kaldıysa nedenini de açıklayın.

## Commit ve Pull Request

1. Fork üzerinden veya `main` dalından bir özellik dalı oluşturun. Her dal yalnızca bir konuyu çözmeli ve fark incelenebilir kalmalıdır.
2. Commit başlığı ve gövdesi İngilizce olmalıdır. Başlık değişikliği kısaca açıklamalı; gövde, gerektiğinde davranış değişikliklerini, doğrulama yöntemini ve büyük dosyalara etkisini belirtmelidir. Her sürümün Release notes metninde Çince ve İngilizce bölümleri bulunmalıdır.
3. PR açıklamasında gerekçeyi, değişiklik kapsamını ve doğrulama sonuçlarını açıkça belirtin. Kullanıcının gördüğü davranış değiştiğinde `CHANGELOG.md` dosyasının Unreleased bölümünü ve ilgili `docs/` belgelerini de güncelleyin.
4. Gönderdikten sonra GitHub Actions `build` iş akışının başarılı olduğunu doğrulayın.

Büyük değişikliklerde (yeni modül, yeni UI sayfası veya disk yazma katmanı sözleşmesi değişikliği) uygulamaya geçmeden önce yaklaşımı tartışmak üzere bir Issue veya Draft PR açılması önerilir.

## Sürümler ve yayımlama

Kısa ömürlü özellik dallarıyla birlikte `main` kullanılır ve anlamsal sürüm numarası düzeltmelere, özelliklere ve uyumsuz değişikliklere göre artırılır. Günlük katkılardaki değişiklikler `CHANGELOG.md` dosyasının `Unreleased` bölümüne yazılır. Bakımcılar aynı sürüm hazırlık PR'ında `Directory.Build.props` sürüm yapılandırmasını ve ilgili sürüm kaydını günceller. `main` dalına birleştirildikten sonra GitHub Actions sürüm etiketini ve Release taslağını otomatik oluşturur. Normal kod push işlemleri Release oluşturmaz. Tam tetikleme koşulları, ön sürüm son ekleri, taslak incelemesi ve yeniden deneme kuralları için [yayımlama kılavuzuna](docs/RELEASING.tr.md) bakın.

## Lisans

Proje [GNU GPL v3.0 or later](LICENSE) (`GPL-3.0-or-later`) lisansını kullanır. Katkı göndermek, ilgili içeriği gönderme hakkına sahip olduğunuz ve katkıyı bu lisans kapsamında lisanslamayı kabul ettiğiniz anlamına gelir. Üçüncü taraf kod, yazı tipi veya görsel eklerken özgün yazarları ve lisansları koruyun, projenin dağıtım yöntemiyle uyumluluğunu doğrulayın ve [Üçüncü taraf bildirimlerini](THIRD-PARTY-NOTICES.txt) güncelleyin. Üçüncü taraf lisans sınırları için [LICENSE-NOTICE.md](LICENSE-NOTICE.tr.md) belgesine bakın.
