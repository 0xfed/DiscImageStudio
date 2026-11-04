# Güvenlik Politikası

[简体中文](SECURITY.md) | [繁體中文](SECURITY.zh-TW.md) | [English](SECURITY.en.md) | [日本語](SECURITY.ja.md) | [Türkçe](SECURITY.tr.md)

Kötüye kullanılabilecek güvenlik sorunlarını herkese açık Issue kayıtlarında açıklamayın. GitHub deposunun özel **Security → Report a vulnerability** bildirim özelliğini kullanın ve etkilenen sürümü, yeniden üretme adımlarını ve etki kapsamını belirtin.

Masaüstü uygulaması ve CLI kendiliğinden ağ bağlantısı başlatmaz; ancak kullanıcıların seçtiği görselleri, dizinleri ve disk imajı dosyalarını ayrıştırır. Dosya ayrıştırma, yol işleme, paket bildirimleri veya güvenilmeyen girdilerle ilgili sorunların tümü güvenlik sorunu olarak ele alınmalıdır.

## Bağımlılık ve derleme güven sınırları

CI, CPython `3.14.10` sürümünü `.python-version` ile seçer; SDK `10.0.403` ve çalışma zamanı `10.0.14` kullanır. `packaging/Install-DotNetSdk.py`, SDK arşivini sabit bir resmî URL’den indirir, açmadan önce SHA512 değerini `eng/dependencies/dotnet-sdk.json` içindeki incelenmiş değerle karşılaştırır ve yeni bir dizine kurar. NuGet geri yüklemeleri onaylı depo imzaları gerektirir, Python bağımlılıkları `requirements-ci.lock` içindeki wheel karmalarını kullanır ve Actions commit’lere sabitlenir; derleme kaynağı kanıtı için `actions/attest` `4.2.2` kullanılır. Bu denetimler derleme girdilerinin kimliğini sınırlar, üst kaynak kodunun zararsız olduğunu kanıtlamaz; Python önyüklemesi, barındırılan çalıştırıcılar ve sistem araçları dış güven sınırlarını korur.

Python araştırma/CI araçlarının kullandığı Pillow `13.0.0` wheel dosyaları, `CVE-2026-111497` açığından etkilenen Brotli `1.2.0` içerir; doğrudan yerine konabilecek doğrulanmış daha yeni bir sürüm wheel’i henüz yoktur. Mevcut Agg/PNG yolunda doğrudan ImageFont/WOFF2 yükleme çağrısı bulunmadı ve kullanılabilir bir saldırı yolu doğrulanmadı; Pillow, .NET masaüstü uygulaması veya CLI ile dağıtılmaz. Ayrı Python Brotli paketini yükseltmek, Pillow’un gömülü yerel kitaplığını değiştirmez. Actions’ın paketlenmiş bağımlılıklarındaki kalan güvenlik duyurusu eşleşmeleri de gerçek çağrı yollarına göre incelenmelidir; bu yükseltme hepsini gidermez.
