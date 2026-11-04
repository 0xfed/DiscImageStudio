# Blu-ray bayt ve bit periyodu yoğunluk analizi

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

Tek katmanlı BD-R'nin sayısal kodlama zincirini analiz eder ve gerçek diskte açık/koyu eşleme testleri için girdi desenlerini seçer. Sonuçlar siyah/beyaz kutupluluğunu tanımlamaz; tam disk imajı oluşturmaz veya diske yazmaz. Ayrıntılı sonuçlar için [analiz raporuna](../../docs/BLURAY_BYTE_DENSITY.tr.md) bakın.

.NET 10 SDK gerekir. Bağımsız standart tablosu analizi yalnızca Python standart kitaplığını kullanır; grafik çizimi ayrıca matplotlib / numpy gerektirir. Depo kökünden çalıştırın:

C# probunun yardım, ilerleme, hata ve açıklayıcı JSON metinleri beş dili destekler. `--language zh-CN|zh-TW|en|ja|tr|system` ile açıkça seçin; aksi durumda önce `DISC_IMAGE_STUDIO_LANGUAGE` ortam değişkeni, ardından sistem arayüz dili kullanılır. JSON/CSV tanımlayıcıları, sayısal sonuçlar, bit dizileri ve dosya adları çevrilmez.

Python referans aramaları ve çizim aracı da aynı `--language` seçeneğini ve ortam tercihini destekler. Yardım, hatalar, ilerleme, grafik açıklamaları, üretilen Markdown ve açıklayıcı JSON/CSV metinleri çevrilir; alan ve sütun adları, desen tanımlayıcıları ve sayısal veriler korunur. Grafikler seçilen dil için mevcut sistem yazı tiplerini tercih eder ve CJK yedeği olarak depoyla gelen HarmonyOS yazı tipini kullanır.

```powershell
dotnet run --project tools/BluRayByteProbe --configuration Release -- --language ja --help
dotnet run --project tools/BluRayByteProbe --configuration Release -- artifacts/bluray-byte-density 4
dotnet run --project tools/BluRayByteProbe --configuration Release -- --fine artifacts/bluray-byte-density

python tools/BluRayByteProbe/reference/analyze.py --output-dir artifacts/bluray-byte-density/reference
python tools/BluRayByteProbe/reference/pattern_search.py --output-dir artifacts/bluray-byte-density/reference
python tools/BluRayByteProbe/reference/robust_pattern_search.py --output-dir artifacts/bluray-byte-density/reference

dotnet run --project tools/BluRayByteProbe --configuration Release -- --verify-reference artifacts/bluray-byte-density/reference/independent-finite-vectors.json
dotnet run --project tools/BluRayByteProbe --configuration Release -- --verify-patterns artifacts/bluray-byte-density/reference/independent-pattern-vectors.json
python tools/BluRayByteProbe/plot_results.py artifacts/bluray-byte-density
```

Varsayılan komut, saf 17PP, tam kümeler için dört DC stratejisi, adaylar için dört PSN, karıştırması giderilmemiş ana makine kontrolü, rastgele komşular ve karışık desenler dahil olmak üzere yinelenen 256 Q baytının tamamını tarar. İkinci bağımsız değişken paralellik derecesidir (1–16). `--fine`, beş periyodun tüm fazlarını ve dört yerleşimi tarar, ayrıca sonuçları farklı adreslerde yeniden doğrular; bu modda paralellik en fazla 4'tür.

Q, **karıştırma sonrası hedef kullanıcı verisidir**. Gerçekte yazılan ana makine yükü `Q XOR PSN mask` olur. Tüm çıktılar belirtilen dizine yazılır ve aynı adlı önceki analiz dosyalarının üzerine yazılabilir.

Başlıca çıktılar:

| Dosya | İçerik |
|---|---|
| `all-bytes.csv` / `.json` / `.md` | 256 baytın tamamı; yoğunluk, DC duyarlılığı ve dizi dağılımı |
| `all-bytes-density.png` | 16 × 16 yoğunluk haritası |
| `fine-patterns.json` / `.csv` | 176 bit dizisi/yerleşim/faz birleşimi ve adres doğrulamaları |
| `fine-pattern-comparison.png` | Bayt temel ölçümleriyle bit düzeyinde kontrolün karşılaştırılması |
| `recommended-patterns.csv` | Önerilen üç bit dizisinin tam oluşturma parametreleri ve yoğunluğu |
| `context.json` / `periodic-context.json` | ECC dışarıda bırakılmış yapay çerçeveler, komşu ve DC sınırı deneyleri |
| `reference/` | Bağımsız standart tabloları, tam döngüler ve arama sonuçları |
| `method.json` / `validation.json` / `fine-validation.json` | İstatistik tanımları ve doğrulama kayıtları |

Tam küme istatistiklerinin paydası, eşzamanlama, EDC, LDC/BIS, DC ve 17PP dahil olmak üzere `496 × 1932 = 958272` kanal biti olarak sabittir. run-in/out/Guard3 dışarıda bırakılır; ancak run-in başlangıç seviyesi ve DSV'si korunur. Otomatik DC, gelecekteki bitleri geçici olarak sıfır alan, projenin mevcut açgözlü **tanılama** algoritmasını kullanır; yazıcının DC kararlarıyla eşit olması garanti edilmez. Tamamı 0, tamamı 1 ve rastgele DC yalnızca duyarlılık karşılaştırmaları içindir; gerçek disk yazma yapılandırmaları olarak kullanılamaz.

`FinePatternAnalysis`, denetlenebilir her payload bit için fiziksel ECC konumunu hesaplar, ardından periyodik bit değerlerini yerleştirir. Gerçek kodlama sonrası 65.536 Q baytının hedefle bayt bayt eşleştiğini doğrular. EDC, BIS, eşlik ve DC değerlerinin tümü kodlama zinciri tarafından üretilir; bunların doğrudan yazılabilir kullanıcı bitleri olduğu varsayılmaz.
