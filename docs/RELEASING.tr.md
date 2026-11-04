# Sürüm yayımlama kılavuzu

[简体中文](RELEASING.md) | [繁體中文](RELEASING.zh-TW.md) | [English](RELEASING.en.md) | [日本語](RELEASING.ja.md) | [Türkçe](RELEASING.tr.md)

Dört PowerShell paketleme ve kurulum betiği `-Language zh-CN|zh-TW|en|ja|tr|system` destekler. Seçenek verilmezse `DISC_IMAGE_STUDIO_LANGUAGE` veya sistem arayüz dili kullanılır; açık seçenek önceliklidir. Durum ve projeye ait tanılama ortak çevirileri kullanırken sürüm, kimlik, yol, sürüm notu metni ve sağlama toplamı değişmez. Üçüncü taraf araçlar ve Windows'un döndürdüğü ayrıntılar özgün dilinde kalır. Seçilen dil alt denetimlere ve yükseltilmiş yükleyiciye iletilir.

Aşağıdaki denetimler yalnızca meta verileri ve ön denetim hata yollarını doğrular. Paket derlemez, imzalamaz, yüklemez veya yetki yükseltme istemez. Windows PowerShell için komutlardaki pwsh yerine powershell kullanabilirsiniz:

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

## Dal ve sürüm politikası

Günlük geliştirme, Pull Request ile birleştirilen kısa ömürlü özellik dalları ve `main` üzerinde yapılır. Düzeltme sürümleri de `main` üzerinden yayımlanır; şu anda uzun ömürlü sürüm dalları tutulmaz.

Sürümler depo kökündeki `Directory.Build.props` içinde merkezi olarak yönetilir. Bakımcılar değişikliğe göre sonraki sürümü seçer:

| Değişiklik | Sürüm değişimi | Örnek |
| --- | --- | --- |
| Geriye uyumlu hata düzeltmesi | patch | `1.0.26` → `1.0.27` |
| Geriye uyumlu yeni özellik | minor | `1.0.26` → `1.1.0` |
| Uyumsuz arayüz, komut veya dosya biçimi değişikliği | major | `1.0.26` → `2.0.0` |

Kararlı sürümde `VersionPrefix` içine `A.B.C` yazılır, `VersionSuffix` boş bırakılır. Ön sürümde `VersionSuffix` için `alpha.N`, `beta.N` veya `rc.N` kullanılır; `N` başında sıfır olmayan pozitif tamsayıdır. Örneğin `VersionPrefix=1.0.27` ve `VersionSuffix=rc.1`, `1.0.27-rc.1` verir. Soneki `VersionPrefix` içine yazmayın. Aday kararlıya çevrilirken `VersionSuffix` temizlenir.

GitHub Actions tam yapılandırılmış sürümü okuyup ilgili `vA.B.C`, `vA.B.C-rc.N` gibi etiketi otomatik oluşturur. Yayımlanan EXE'nin bilgi sürümü ön sürüm sonekini korur; dosya ve derleme sürümleri dört sayısal bileşenli `A.B.C.0` kullanır. .NET derleme sürümü uyumluluğu için depodaki sayısal bileşenler `0..65534` ile sınırlıdır. Microsoft Store gönderiminde ilk bileşen ayrıca `0` değerinden büyük olmalı; MSIX sürümü `A.B.C.0` olmalıdır.

## GitHub Actions ile Windows uygulaması ve platformlar arası CLI yayımlama

### Sürümü hazırlama

1. GPL-3.0-or-later lisansı, gizlilik politikası ve destek iletişiminin bu dağıtımın gerekliliklerini karşıladığını doğrulayın. Derleme commit'ine işaret eden etiketi tutun; aynı Release içinde o sürümün tam karşılık gelen kaynak kodunu ve derleme yönergelerini sağlayarak kaynağın EXE/MSIX yanında edinilebilmesini sağlayın. GitHub'ın otomatik kaynak arşivinde derleme için gereken dosyaların bulunduğunu kontrol edin. Bağımlılıklar veya kendi kendine yeterli .NET çalışma zamanı yükseltilirken [üçüncü taraf bildirimlerini](../THIRD-PARTY-NOTICES.txt) güncelleyin. Proje lisansı, üçüncü taraf bildirimleri ve yazı tipi anlaşmaları uygulamada gömülüdür; Hakkında sayfasından çevrimdışı okunabilir. Lisans sınırları için [LICENSE-NOTICE.md](../LICENSE-NOTICE.tr.md) belgesine bakın.
2. Özellik dalında `Directory.Build.props` içindeki `VersionPrefix` değerini (ön sürüm için `VersionSuffix` de) değiştirin; bu sürümün içeriğini `CHANGELOG.md` içindeki `Unreleased` bölümünden ilgili sürüm bölümüne taşıyın. Sonraki geliştirme için `## Unreleased` kalsın.
3. Sürüm başlıkları `## [1.0.27] - YYYY-MM-DD` veya `## [1.0.27-rc.1] - YYYY-MM-DD` biçiminde olmalı; tarih yer tutucusu gerçek yayımlanma tarihiyle değiştirilmeli ve içerik boş olmamalıdır. Ön sürüm ve kararlı sürümün her biri etiketle tam eşleşen bölüm gerektirir. Bu sürümden başlayarak yeni bölümler aşağıdaki kurala göre hem Çince hem İngilizce günlük içerir.
4. Sürüm yapılandırması ve notlarını aynı sürüm hazırlık PR'ına koyun; `build` iş akışı geçince `main` ile birleştirin. Otomatik yayımlama ancak bu push her iki dosyayı içeriyor ve yapılandırılmış tam sürümü gerçekten değiştiriyorsa başlar.
5. GitHub Actions denetimleri tamamlar, sürüm etiketini otomatik oluşturur ve Release taslağı üretir. Bakımcı inceleyip elle yayımlar.

Örneğin gelecekteki bir düzeltme için `Directory.Build.props` şöyle ayarlanır:

```xml
<VersionPrefix>1.0.27</VersionPrefix>
<VersionSuffix></VersionSuffix>
```

Aynı zamanda `CHANGELOG.md` içine gerçek tarihli, boş olmayan `## [1.0.27] - YYYY-MM-DD` bölümü eklenir. `main` ile birleştirilince `v1.0.27` etiketi otomatik üretilir. Aday sürüm için `VersionSuffix`, `rc.1`; günlük sürümü `1.0.27-rc.1` olur.

### İki dilli sürüm notları

Sonraki sürümler aynı tarihli `## [VERSION] - DATE` içinde önce `### 中文`, sonra `### English` kullanır. İki liste aynı değişiklikleri, sınırlamaları ve doğrulama durumunu anlatır. İngilizce karşılık gelen Çinceyi eksiksiz çevirmeli; uyarı atlamamalı veya doğrulanmamış yetenek iddiası eklememelidir. Sürüm içeriği düzenlendikten sonra üstteki `## Unreleased` sonraki geliştirme için korunur.

Hazırlık yapısına örnek aşağıdadır. Sürüm, tarih ve maddeleri gerçek sürüm içeriğiyle değiştirin:

```markdown
## Unreleased

## [1.3.4] - YYYY-MM-DD

### 中文

- 在设置中增加界面语言选择。

### English

- Add interface language selection to Settings.
```

`Get-ReleaseMetadata.ps1` tüm sürüm bölümünü alır; yalnızca sonraki `## ` başlığında veya dosya sonunda durur. Böylece hem `### 中文` hem `### English` Release açıklamasına girer. Dil başlıklarını `## 中文` veya `## English` yapmayın; bölüm erken biter. Mevcut meta veri denetimi bölümün tekliğini, tarihin geçerliliğini ve boş olmayan içeriği doğrular; iki dilin eksiksizliği ve maddelerin eşleşmesi sürüm incelemesinde ayrıca onaylanmalıdır.

İş akışı iki dilli notların ardından Çince ve İngilizce indirme/çalıştırma yönergeleri, SHA-256 doğrulama komutları, karşılık gelen kaynak ve derleme açıklaması ekler. İki dil de Linux ve macOS CLI'nin deneysel olduğunu açıkça belirtmeli; Windows EXE imza durumunu ve anında disk yazımının deneysel sınırlarını ayrı açıklamalıdır.

### Otomatik denetimler ve taslak

`.github/workflows/release.yml`, `main` üzerindeki `Directory.Build.props` veya `CHANGELOG.md` değişikliklerini izler; ancak yalnızca push **hem sürüm yapılandırmasını hem notlarını değiştiriyor ve tam sürümü de değiştiriyorsa** yayımlamaya geçer. Sıradan kod push'ları normal derleme denetimlerini çalıştırır. Yalnızca `Unreleased` eklemek veya sürüm değişmeden yapılandırma düzenlemek Release üretmez.

İş akışı tam sürümü okur, biçimi ve eşleşen boş olmayan tarihli Changelog bölümünü doğrular, yayımlama commit'inin `origin/main` içinde olduğunu kontrol eder ve `build.yml` derleme/regresyonlarını yeniden kullanır. Ancak tümü geçince ilgili etiket ve taslak oluşturulur.

Windows x64 kendi kendine yeterli tek EXE sunmaya devam eder. Paketleme betiği son EXE'yi yalıtılmış dizinde çalıştırıp öz testleri, iki sektörlük RAW/WAV üretim denetimlerini ve GUI ekran görüntüsü denetimini yapar.

Linux ve macOS deneysel çevrimdışı CLI, her sistemin x64 ve Arm64 runner'ında derlenir; .NET çalışma zamanı pakete dahildir. Her runner yeni tar.gz dosyasını açıp depo dışında `--help`, DVD öz testi, CD ses izi, DVD görüntüsü ve Blu-ray görüntüsü üretim testlerini çalıştırır. Başarılı özgün arşivler Windows EXE ile birlikte taslağa eklenir. Örneğin:

```text
DiscImageStudio-v1.3.6-win-x64.exe
DiscImageStudio-CLI-v1.3.6-linux-x64.tar.gz
DiscImageStudio-CLI-v1.3.6-linux-arm64.tar.gz
DiscImageStudio-CLI-v1.3.6-osx-x64.tar.gz
DiscImageStudio-CLI-v1.3.6-osx-arm64.tar.gz
SHA256SUMS.txt
```

İş akışı platformların SHA-256 toplamlarını birleştirip doğrular; Release notlarına ve `SHA256SUMS.txt` ekine yazar. Yerleşik `GITHUB_TOKEN` ile **GitHub Release taslağı** oluşturur; kişisel erişim belirteci (PAT) gerekmez. Ön sürüm etiketleri prerelease bayrağını otomatik ayarlar; kararlı etiketler kararlı sürüm taslağı oluşturur.

Bakımcı GitHub [Releases sayfasında](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/releases) sürümü, notları, platform eklerini ve sağlama toplamlarını inceler; indirip açılışı doğruladıktan sonra elle yayımlar. Windows indirmesinin SHA-256 değerini bu komutla hesaplayıp Release notlarıyla karşılaştırabilirsiniz:

```powershell
Get-FileHash .\DiscImageStudio-v1.3.6-win-x64.exe -Algorithm SHA256
```

### Derleme kökeni ve SBOM'lar

Yayımlama iş akışı, beş sürüm ikili dosyası/arşivinin her biri için `<artifact>.cdx.json` adlı bir CycloneDX SBOM oluşturacak ve commit'e sabitlenmiş `actions/attest` üzerinden derleme kökeni ve SBOM doğrulamaları üretecek şekilde yapılandırılmıştır. SBOM'daki paket ve çalışma zamanı sürümlerini kaydedilmiş kilitlerle karşılaştırın; dosya bütünlüğü için `SHA256SUMS.txt`, üretici kimliği için köken doğrulamalarını kullanın.

Yayımlanmış bir Release için dosyayı ve SBOM'unu indirin, ardından GitHub CLI ile her iki dosyayı doğrulayın. Örnek, bu deponun yayımlama iş akışına güvenir; fork'larda iki depo adını da kendi sahip/depo adınızla değiştirin:

```powershell
gh attestation verify DiscImageStudio-v1.3.6-win-x64.exe --repo JiaFeiMiao-K-Cat/DiscImageStudio --signer-workflow JiaFeiMiao-K-Cat/DiscImageStudio/.github/workflows/release.yml
gh attestation verify DiscImageStudio-v1.3.6-win-x64.exe.cdx.json --repo JiaFeiMiao-K-Cat/DiscImageStudio --signer-workflow JiaFeiMiao-K-Cat/DiscImageStudio/.github/workflows/release.yml
```

Başarılı bir yerel derleme GitHub köken doğrulamaları oluşturmaz; bunlar yalnızca yapılandırılmış GitHub yayımlama iş akışı başarıyla çalıştıktan sonra kullanılabilir. Platform kod imzalamanın yerini almazlar: gerçek kimlik bilgileri ve imzalama adımları yapılandırılana kadar Windows EXE'leri Authenticode imzasız, macOS paketleri ise noter onaysız kalır.

### Yeniden deneme ve düzeltme

Başarısız Actions çalışması yeniden yürütülebilir veya GitHub Actions içinden `main` üzerinde `release` elle başlatılabilir. Elle başlatma etiket girdisi almaz; güncel `main` yapılandırması ve notlarını okuyup tüm sürüm denetimlerini yapar. Yeniden denemek için sürüm dosyasını tekrar değiştirmek gerekmez.

Aynı sürüm yeniden denenirken mevcut etiket bu derlemenin aynı commit'ine işaret etmeli, mevcut Release hâlâ taslak olmalıdır. Yayımlanmış Release, etiket ve çıktılar üzerine yazılamaz veya taşınamaz. Sorunları `main` üzerinde düzeltip yeni sürüm yayımlayın; ön sürüm yinelemesinde numarayı artırın, örneğin `rc.1` → `rc.2`. `main` ilerlemiş ve aynı sürüm etiketi eski commit'i gösteriyorsa özgün başarısız çalışmayı yeniden yürütün veya yeni commit için yeni sürüm hazırlayın. Yalnızca açıklama değişse de gerçekler ve düzeltme kaydı korunmalı; yayımlanan ikili dosyalar değiştirilmemelidir.

Depo yöneticilerinin GitHub'da `main` korumasını, Pull Request ve zorunlu `build` denetimini yapılandırması; `v*` etiketleri için yayımlama iş akışının oluşturmasına izin veren, güncelleme/silmeyi engelleyen kural kümesi kurması önerilir. İş akışı dosyaları uzak koruma ayarları değildir; bunları yöneticiler ayrıca yapılandırmalıdır.

## Yerel EXE provası

Depo kökünde GitHub Actions ile aynı paketleme betiğini çalıştırın:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-Release.ps1
```

Betik `Directory.Build.props` sürümünü otomatik kullanır. Örneğin `1.0.26` yapılandırmasında çıktı dizini `artifacts/release/v1.0.26/` olur. Kendi kendine yeterli `win-x64` EXE derlenip ayrı doğrulanır; `DiscImageStudio-v1.0.26-win-x64.exe` ve onun `SHA256SUMS.txt` dosyası üretilir. Resmî yayımlamada dört platform CLI arşivinin toplamları listeye eklenir.

## Microsoft Store MSIX

MSIX ayrı bir elle dağıtım kanalıdır. Önce Partner Center'da ürün adını ayırın; Product identity sayfasından şunları kopyalayın:

- Package/Identity/Name
- Package/Identity/Publisher
- Package/Properties/PublisherDisplayName

Yerelde çalıştırın:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-MSIX.ps1 `
  -IdentityName "Partner Center Identity Name" `
  -Publisher "CN=Partner Center Publisher" `
  -PublisherDisplayName "Publisher display name" `
  -DisplayName "Reserved product name"
```

`-Version` verilmezse `Directory.Build.props` kararlı sürümü okunup `A.B.C.0` biçimine çevrilir. Mağaza sürümünü açıkça belirtmek için `-Version "1.0.26.0"` kullanın; bu geçersiz kılma paket EXE sürümüne de uygulanır ve bildirimle eşleşmesini sağlar. Microsoft Store, `-rc.1` gibi ön sürüm soneklerini kabul etmez; Store paketi için kararlı sürüm yapılandırması kullanın.

GitHub Actions içinde `package-msix` elle çalıştırılıp aynı değerler girilebilir; boş `version` yine depo yapılandırmasını okur. İş akışı önce `build.yml` CI'sini yeniden kullanır, ardından MSIX'i Actions eki olarak üretir. Mağazaya otomatik göndermez veya GitHub Release'e otomatik eklemez. Store MSIX imzasız kalabilir; yerel yandan yükleme testi için Publisher ile eşleşen sertifikayla imzalanmalıdır.

## Yerelde imzalı test paketi

Yerel test için konusu test Publisher ile tam aynı olan kod imzalama sertifikası oluşturun; açık anahtar sertifikasını buraya koyun:

```text
artifacts/certificates/DiscImageStudio-Test.cer
```

Sertifika parmak iziyle `Build-MSIX.ps1 -CertificateThumbprint <thumbprint>` çalıştırın; yukarıdaki kimlik parametrelerini de doldurun. İmzalı paket oluşunca `packaging/Install-TestPackage.cmd` dosyasına çift tıklayın veya şunu çalıştırın:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Install-TestPackage.ps1
```

Kurulum betiği bir kez yönetici yetkisi ister, test sertifikasının açık anahtarını Yerel Bilgisayar / Güvenilen Kişiler alanına aktarır ve imzalı MSIX'i yükler. Özel anahtar veya PFX dosyasını GitHub'a göndermeyin; yerel kendinden imzalı test sertifikasını resmî Release eki yapmayın.

## Windows 11 imzasız hızlı test (isteğe bağlı)

İmzasız test paketi ayrı OID yayımcı ad alanı kullanmalı, resmî Store kimliğiyle karıştırılmamalıdır. Yerel test paketini üretin:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/Build-MSIX.ps1 `
  -IdentityName "12345.DiscImageStudioArchitectureTest" `
  -Publisher "CN=Disc Image Studio Test" `
  -PublisherDisplayName "Disc Image Studio Test" `
  -DisplayName "Disc Image Studio Test" `
  -Version "1.0.26.0" `
  -UnsignedDevelopmentPackage
```

Ardından yönetici PowerShell ile yükleyin:

```powershell
Add-AppxPackage -Path ".\artifacts\msix\DiscImageStudio_1.0.26.0_x64_unsigned-dev.msix" -AllowUnsigned
```

`-UnsignedDevelopmentPackage` yalnızca bunu destekleyen Windows 11 sistemlerinde hızlı test içindir. Bazı Windows sürümleri `-AllowUnsigned` olsa da yürütülebilir paketi reddedebilir; o durumda yukarıdaki imzalı yöntemi kullanın. Dağıtım veya Microsoft Store gönderiminde bu seçeneği kullanmayın; resmî paket gerçek Partner Center kimliğini kullanmalıdır.

Resmî gönderimden önce:

- Gerçek kimlikle yeniden paketleyin; test Identity paketi göndermeyin.
- Temiz Windows cihazında yükleme, kaldırma ve çevrimdışı çalışmayı test edin.
- Son paket üzerinde Windows App Certification Kit çalıştırın.
- En az bir adet 1366×768 veya daha büyük gerçek masaüstü ekran görüntüsü sağlayın.
- `PRIVACY.md` belgesini herkese açık HTTPS sayfasında yayımlayıp gerçek destek iletişimi girin.
- Sertifikasyon notlarında uygulamanın büyük dosyalar ürettiğini, oturum açma ve ağ gerektirmediğini, Windows IMAPI2 ile deneysel doğrudan CD/DVD yazımı içerdiğini açıklayın. Uygun cihazları, boş disk gereksinimlerini ve mevcut doğrulama kapsamını doğru listeleyin.
