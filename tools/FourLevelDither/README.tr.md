# Siyah-beyaz renk taklidi aracı

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

Özgün görseli yalnızca **0 ve 255** içeren iki düzeyli siyah-beyaz PNG'ye dönüştüren bağımsız bir Windows görsel işleme uygulamasıdır. DVD ve CD bu iki düzeyi kullanır; siyah-beyaz piksellerin uzamsal karışımıyla ton korunur. Mevcut girişlerle uyumluluk için yürütülebilir dosyanın adı `FourLevelDither.exe` olarak kalır.

## Pencere kullanımı

1. `FourLevelDither.exe` dosyasını çalıştırın; “Görsel aç” seçeneğine tıklayın veya görseli pencereye sürükleyin.
2. “DVD iki düzey” veya “CD iki düzey” seçerek ilgili kod sözcüğü açıklamasını görüntüleyin; iki tür de aynı siyah-beyaz paleti üretir. Renk taklidi ayrıntıları için “%100 piksel görünümü” seçeneğini etkinleştirin.
3. “sRGB gama düzeltmesi” varsayılan olarak kapalıdır ve önceki çıktı korunur. Açıldığında görsel otomatik olarak yeniden işlenir; pencere geçerli durumu gösterir.
4. İşlenmiş görseli ayrı kaydetmek için “Siyah-beyaz PNG kaydet” seçeneğine tıklayın. Düzeltme kapalıyken varsayılan dosya adı `-黑白抖动.png`, açıkken `-黑白抖动-sRGB.png` ile biter; bu karşılaştırmayı kolaylaştırır. Program geçerli özgün görsel yolunun üzerine yazmayı reddeder.

Disk türü veya gama seçeneği değiştirildiğinde önbellekteki özgün görselden yeniden hesaplanır; önceki renk taklidi sonucu yeniden işlenmez. Eski üç veya dört düzeyli şemadan iki düzeye geçerken en eksiksiz ton bilgisini korumak için renk taklidi uygulanmamış özgün görseli yeniden açıp işleyin.

PNG, JPEG, BMP, TIFF, GIF ve sistem görsel kod çözücülerinin desteklediği diğer biçimler kullanılabilir. Kayıpsız Gray8 PNG dışa aktarılır; özgün piksel boyutları ve geçerli DPI korunur. Geçersiz DPI için 96 kullanılır. Önizleme yakınlaştırması kaydedilen piksel boyutlarını değiştirmez.

## İşleme kuralları ve disk eşlemesi

Varsayılan olarak renkler, kodlanmış RGB uzayında Rec.709 ağırlıklarıyla parlaklığa dönüştürülür; saydamlık önce beyaz arka planla birleştirilir. Ardından **yılanvari Floyd–Steinberg hata yayılımı** uygulanır: bitişik satırlar dönüşümlü yönlerde taranır, yayılan hata eklenmiş parlaklığa en yakın siyah veya beyaz seçilir ve parlaklık farkı sonraki piksellere dağıtılır.

| Ana uygulamanın iki düzeyli girdi aralığı | PNG / önizleme gri değeri | DVD karıştırılmış alandaki Q byte | CD byte |
| --- | ---: | --- | --- |
| 0–127 | 0 | `0xA5` | `0x0D` |
| 128–255 | 255 | `0x92` | `0xAA` |

Gama düzeltmesi varsayılan olarak kapalıyken özgün gri değeri 128 olan geniş bir alanın yaklaşık `128 / 255 = 50.2%` kadarı beyaz piksel, kalanı siyah olur. Renk taklidi sırasında yayılım hatası eklenir; gama düzeltmesi açık veya kapalı olsa da çıktı dosyaları yalnızca 0 ve 255 içerir.

### sRGB gama düzeltmesi

Açıldığında program önce her RGB kanalını standart sRGB aktarım fonksiyonuyla doğrusal ışığa çözer; ardından Rec.709 ağırlıklarıyla parlaklığı hesaplar, doğrusal uzayda beyaz arka planla birleştirir ve hatayı yayar. Siyah-beyaz piksel oranını değiştirir; disk kod sözcüklerini veya çıktı gri düzeylerini değiştirmez.

Örneğin özgün gri değeri 128, yaklaşık %21,6 doğrusal parlaklığa çözülür ve yaklaşık %21,6 beyaz piksel üretir. Aynı görselin orta tonları genellikle kapalı duruma göre daha koyudur. İki sürümü kaydedip gerçek disk sonuçlarını karşılaştırabilirsiniz. Seçenek kapatıldığında özgün algoritma kullanılır ve varsayılan davranış değişmez.

Ana uygulamadaki ton ayarları Tasarım sayfasının sağ sütununda yönetilir; oluşturma, akışla yazma ve tasarım izdüşümü seçilen tonu paylaşır. Önce bu bağımsız araçtan PNG dışa aktarmak gerekmez. Yeni projelerde varsayılan renk taklidi gücü %75, düzey sayısı 2, parlaklık 0 ve sRGB doğrusallaştırma kapalıdır. CD için 2/4 düzey seçilebilir; DVD/BD iki düzey kullanır. Oluşturma geometrisi Proje ayarlarında düzenlenir.

Ana uygulama artık tasarımları katmanlardan birleştirir ve eski CD/DVD halka çoğaltma seçeneğini sunmaz. Bağımsız araç yalnızca tek görseli işler; proje katman yerleşimini yönetmez.

İşleme çekirdeği `DiscImageStudio.Imaging.GrayscaleDither`, sıralı herhangi bir gri ton paletini desteklemeye devam eder. Eski `FourLevelDither` API'si yalnızca mevcut kod sözcüğü deneme araçları gibi araçlar için uyumlu davranışı korur; bağımsız pencere ve CLI iki düzeyli siyah-beyaz kullanır.

## Komut satırı

Pencere ve komut satırı Basitleştirilmiş Çince, Geleneksel Çince, İngilizce, Japonca ve Türkçeyi destekler; dil başlangıçta seçilir. `--language zh-CN|zh-TW|en|ja|tr|system` ile açıkça seçin; belirtilmezse `DISC_IMAGE_STUDIO_LANGUAGE` ortam değişkeni kullanılır, o da yoksa sistem arayüz dili izlenir. Dil seçimi mod değerlerini, dosya adlarını veya piksel çıktısını değiştirmez.

```powershell
# Pencere açmadan dönüştürme; varsayılan DVD iki düzeydir. Çıktı, henüz var olmayan bir .png olmalıdır.
.\FourLevelDither.exe --input original.jpg --output result.png

# DVD veya CD modunu açıkça seçin; ikisi de 0/255 siyah/beyaz iki düzey üretir.
.\FourLevelDither.exe --input original.jpg --output dvd-result.png --mode dvd
.\FourLevelDither.exe --input original.jpg --output cd-result.png --mode cd

# sRGB gama düzeltmesi kapalı ve açık durumları karşılaştırın; --gamma verilmezse none kullanılır.
.\FourLevelDither.exe --input original.jpg --output result-none.png --gamma none
.\FourLevelDither.exe --input original.jpg --output result-srgb.png --gamma srgb

# Bir görselle pencereyi açın.
.\FourLevelDither.exe original.jpg

# Yardımı gösterin (--help de desteklenir).
.\FourLevelDither.exe -h

# Pencereyi Japonca aç
.\FourLevelDither.exe --language ja original.jpg

# Türkçe olarak pencere açmadan dönüştür
.\FourLevelDither.exe --language tr --input original.jpg --output result-tr.png
```

## Kaynaktan çalıştırma ve yayımlama

Depo kökünde .NET 10 SDK kullanın:

```powershell
dotnet run --project tools/FourLevelDither --configuration Release

dotnet publish tools/FourLevelDither/FourLevelDither.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/FourLevelDither/win-x64
```

Kendi kendine yeterli `FourLevelDither.exe`, .NET 10 çalışma zamanını içerir. Kendi kendine yeterli olmayan derleme doğrudan kullanılacaksa .NET 10 Windows Masaüstü Çalışma Zamanı yüklenmelidir.
