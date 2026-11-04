# Yardımcı kalibrasyon

[简体中文](CALIBRATION.md) | [繁體中文](CALIBRATION.zh-TW.md) | [English](CALIBRATION.en.md) | [日本語](CALIBRATION.ja.md) | [Türkçe](CALIBRATION.tr.md)

Yardımcı kalibrasyon, sağ sütundaki üç karta karşılık gelen **kalibrasyon diski oluşturma → fotoğraf düzeltme → eğri kalibrasyonu** adımlarından oluşur. Fotoğrafta yalnızca fiziksel dış kenarın, merkez deliğinin ve bir artı kolunun veya geniş sektör sınırının açıkça görünmesi yeterlidir. Görünürlüğü artırmak için ışıklandırma ayarlanabilir; diski siyaha boyamak veya tam karşıdan konumlandırmak gerekmez.

## Kalibrasyon deseni ve çıktı

Proje ayarlarında çizim iç/dış yarıçaplarını, sektör sayısını, doğrusal hızı veya bit uzunluğunu ayarlayın. Ardından Yardımcı Kalibrasyon çalışma alanında “Hedef oluştur” bölümünü açıp “Geçerli parametrelerden hedef oluştur” seçeneğini kullanın. Varsayılan ince artı deseni, dokuz ince eş merkezli çember ve bir artı işaretinden oluşan yerleşik `target_slim.png` görselidir. **Önce özgün görselin tamamını oluşturma dış yarıçapına göre ölçekleyin, sonra iç ve dış yarıçaplar arasındaki yazma halkasına kırpın**; normal DVD görseli oluşturmayla aynı koordinat eşlemesi kullanılır. Özgün görselin sol/sağ ve üst/alt sınırları, oluşturma dış yarıçapının negatif ve pozitif değerlerine karşılık gelir. Çember konumları ve çizgi kalınlıkları dış yarıçapla orantılı ölçeklenir; iç yarıçap merkez bölgeyi kırpar. Eş merkezli çemberler yalnızca gözlem içindir; parametre çözümü sadece seçilen eğriyi kullanır.

Arayüzdeki 120 mm disk arka planı önizleme içindir, özgün görselin sabit fiziksel boyutu değildir. Fotoğraf düzeltmede seçilen 12 cm / 8 cm fiziksel disk boyutu da yalnızca fotoğraf ölçeğini geri elde etmek içindir; kaydedilen desen oluşturma eşlemesini değiştirmez.

Desen oluşturulurken oluşturma parametreleri ve CD serpiştirme ayarı sabitlenir; önceki kalibrasyon diskinin fotoğrafı ve girdileri temizlenir. Ana sayfada sonradan yapılan parametre değişiklikleri mevcut kaydın anlamını değiştirmez. Her disk için, oluşturulduğu sırada kaydedilen kayıt kullanılmalıdır.

- CD çıktısı `.wav` (`.cue` ile) veya `.raw`; DVD ve BD çıktısı yalnızca çizim içeren `.iso` olur. BD hedefi tek katmanlı veri alanının tamamını kapsar; çizim başlangıç/bitiş sektörlerini, 3T/6T kutupluluğunu ve ISO zaman damgasını kaydeder.
- Dosya oluşturulurken aynı adlı `.calibration.json` da kaydedilir. Akışla yazma, önce kalibrasyon kaydını kaydeder; sonra kaynağı bu desene sabitlenmiş Yazma çalışma alanına geçer. Burada aygıt seçilir ve onaylanır.
- Önizleme, PNG dışa aktarımı, dosya çıktısı ve akış çıktısı kaydedilmiş deseni ve oluşturma koordinatlarını paylaşır; ince artı özgün gri tonlu kenarları korur. PNG, kırpılmış deseni özgün 2400 × 2400 piksel boyutunda dışa aktarır; önizleme arka planı kenar boşlukları eklenmez ve önizleme ekran görüntüsü yeniden ölçeklenmez. Gerçek yazma için bu sayfanın oluşturma veya akış girişini kullanarak desenin kaydedilmiş oluşturma parametreleriyle tutarlı kalmasını sağlayın.

### Artı soluk olduğunda kaba kalibrasyon

Kalibrasyonda hedef oluşturma kartını açın, **Kaba kalibrasyon için geniş sektörler kullan** seçeneğini işaretleyip **Geçerli parametrelerden hedef oluştur** düğmesine basın. Geçerli hedef etiketi, kaydedilen ve sonraki yazımda kullanılacak deseni gösterir; yalnızca kutuyu değiştirmek mevcut hedefi değiştirmez.

Kaba hedef, sırayla siyah ve beyaz olan dört adet 90 derecelik sektörden oluşur; kılavuz çember içermez. Dört radyal kenarı ince artıyla aynı eğri modelini kullanır. Geometri hatası kenarları spirale dönüştürdüğünde geniş dolu alanlar daha büyük yerel doku oranını korur. Özgün artı ve dokuz çember varsayılan olarak kalır.

Tek bir kesintisiz siyah/beyaz kenarı içten dışa izleyin ve her dönüşü takip etmek için ara noktalar ekleyin; komşu kenara geçmeyin. Kaba sonucu uyguladıktan sonra ince ayar için yeni bir ince artı hedefi oluşturun. Seçilen desenle kayıtlı parametrelerin aynı kalması için bu sayfanın oluşturma veya akışlı yazma kontrollerini kullanın.

Bu tasarım, test edilen geometri benzetimlerinde görünürlüğü artırır; her ofseti düzeltmez, düz çizgi garanti etmez veya fiziksel disk kontrastını doğrulamaz. Testler ±0.1 mm iç yarıçap hatalarını, ±512 sektör ofsetini ve 0.1 mm radyal gözlem aralığındaki ortalamayı kapsar. Fiziksel yazım donanım doğrulaması gerektirir.

### Ölçülen halka çaplarından kalibrasyon

Kalibrasyon içinde Kalibrasyon diski oluştur bölümünü açın, **Çap kalibrasyonu için eş merkezli halkalar kullan** seçeneğini seçip geçerli parametrelerden hedef oluşturun. Yeni hedef, artı içermeyen 7 eş merkezli çizgiden oluşur. Çizgi genişliği normalde 0.15 mm olup çok dar çizim halkalarında küçülür. Halkalar, kaydedilen çizilebilir kayıt ilerlemesinin %6, %18, %34, %50, %66, %82, %94 noktalarındadır ve açısal faza bağlı değildir.

1. Bu sayfadan oluşturun veya akışla yazın ve eşleşen `.calibration.json` dosyasını saklayın. Eşleşen özgün artı kaydını açıp **Ölçülen halka çaplarını kullan** seçeneğiyle, artıyı görmeden mevcut tam halkaları da kullanabilirsiniz. Geniş sektör hedeflerinde halka yoktur.
2. Tam halkaları en içten dışa numaralandırın; kayıt alanı kenarlarını saymayın. Çapı merkezden geçerek karşılıklı çizgi ortaları arasında ölçün. Tablodaki oluşturulan çaplar yalnızca tanımaya yardımcıdır; gerçek ölçümleri mm cinsinden ondalık noktayla girin, ölçülmeyenleri boş bırakın.
3. İç ve dış halkalar dahil, kayıt ilerlemesinin en az %50 kısmını kapsayan en az 3 halka ölçün. İdeal modelde iki bilinen konum iki uç noktayı belirler; ek halkalar tutarlılığı denetler.
4. **Halkalardan iç ve dış yarıçapları hesapla** düğmesine basın. Tahmini yarıçapları, her çap kalıntısını ve RMS/en büyük kalıntıları inceleyin. Varsayılan en büyük çap kalıntısı 0.10 mm değerindedir; bu bir uyum tutarlılığı eşiğidir, ölçüm doğruluğu değildir.
5. Halka numaraları ve ölçülen çapları kontrol edip onay kutusunu seçin, parametreleri uygulayın ve projeyi kaydedin. Çap veya tolerans değişikliği eski sonucu ve onayı geçersiz kılar; kayıt yeniden açıldığında da tekrar hesaplama gerekir.

Hesaplama, kayıtlı sektör sayısını, doğrusal yoğunluğu ve iz aralığı şeklini sabit tutup yalnızca iç ve dış yarıçapları uydurur; açıyı veya yeni iz aralığı şeklini uydurmaz. Sabit iz aralığı yaklaşık olarak `r(q)^2 = ri^2 + q*(ro^2-ri^2)` bağıntısını sağlar; program sonucu kayıtlı tam iz modeliyle iyileştirir. Yapay kontroller 2 mm iç yarıçap hatasını, 1 mm dış yarıçap hatasını ve 0.01 mm değerine yuvarlanmış çapları kapsar; fiziksel disk doğruluğunu garanti etmez. Ölçüm aracı hatası, çizgi genişliği, yanlış halka eşleşmeleri ve bilinmeyen iz aralığı değişimleri sonucu etkiler.

## Birinci adım: fotoğraf düzeltme

1. Fotoğrafı veya taramayı içe aktarın; standart 12 cm disk veya 8 cm mini disk seçin. Görsel, etkileşime uygun boyuta küçültülüp kayıtta saklanır.
2. İki kontrol noktası grubunu **diskin fiziksel dış kenarına** ve **merkez deliğinin kenarına** sürükleyin. İlk konturlar yalnızca ayarlama başlangıcıdır; doğru algılandıkları anlamına gelmez. Kayıt alanı sınırını, şeffaf plastik halkayı veya gölgeyi fiziksel kenar sanmayın.
3. “Düzeltmeyi hesapla” seçeneğine tıklayıp önden görünüm önizlemesini kontrol edin. Düzeltme gerekiyorsa önizlemeyi kapatıp sınırları sürüklemeyi sürdürün; uygun olduğunda onaylayarak eğri kalibrasyonuna geçin.

Program, aynı düzlemdeki iki eş merkezli çemberin izdüşümlerini birlikte uydurarak disk merkezini, ölçeği ve perspektifi geri elde eder. Ölçek, seçilen disk boyutundan ve standart merkez deliği boyutundan gelir; elle ölçüm gerekmez. İki elipsin geometrik merkezleri doğrudan disk merkezi sayılamaz; yalnızca bir elipsi çembere dönüştürmek de perspektif bozulmasını tamamen gidermez.

Bu adımda “artı işareti düz veya dik olmalı” kısıtı kullanılmadığından kalibre edilmesi gereken kayıt eğriliği korunur. Mevcut düzeltme, çekim açısının neden olduğu perspektif bozulmasını işler; merceğin radyal bozulmasını ayrıca tahmin etmez. Ağır geniş açı bozulması, örtülme ve yanlış sınır seçimi güvenilirliği azaltır.

## İkinci adım: tek eğriden kalibrasyon

1. Açıkça görülen bir radyal eğri üzerinde içten dışa eşit aralıklarla tıklayarak iç, orta ve dış bölgeleri kapsayın. CD veya yalnızca yarıçap düzeltmesi için en az 6 nokta gerekir; 20 veya daha fazlası önerilir. DVD üçüncü derece iz aralığı uydurması için farklı yarıçaplarda en az 12 nokta ve oluşturma halkasının radyal genişliğinin %60'ını kapsamak gerekir. Noktalar çok uzak olduğunda komşu noktalar arasındaki açı yarım tura yaklaşırsa program reddeder ve ek noktalar ister. Eş merkezli çember kesişimlerini eşleştirmek veya hangi artı kolu olduğunu ayırt etmek gerekmez.
2. Mevcut noktaları sürükleyerek düzeltin, boş yere tıklayarak ekleyin; seçili noktayı silmek için Delete tuşuna basın. Tüm noktalar bu tek eğriye aittir. Halkalar ve diğer artı kolları uydurmaya katılmaz; işlem ortasında başka kola geçilmemelidir.
3. “Parametreleri çöz” seçeneğine tıklayın. Program eğriyi noktaların radyal sırasıyla izler, iç ve dış yarıçapları uydurur; diskin genel dönme açısını bağımsız bir nicelik olarak ele alır. Fotoğrafta görünür olduğu doğrulanan noktaların tamamı kullanılır; önceden belirlenen kayıt halkasına göre silinmez veya kesilmez, uç noktalar önceden belirlenen sınırlara zorla taşınmaz. Girdi noktaları yine gerçek disk yüzeyinde bulunmalıdır. Varsayılan otomatik aralık ±0,02 mm ile başlar; optimum sonuç aralık sınırına ulaştığında arama ±0,1, ±0,5 mm ve daha geniş aralıklara otomatik olarak açılıp yeniden hesaplanır. Böylece sınırda kısıtlanmış parametreler ölçüm sonucu sayılmaz. Elle belirlenen aralık, daraltılmış “Arama aralığı” öğesindedir ve yalnızca tanılama kısıtı içindir; iki mod da eğri modelini değiştirmez.
4. Bir aday seçip fotoğraf üzerine bindirilen tahmin eğrisinin tamamını kontrol edin. Camgöbeği noktalar ve kesikli çizgiler fotoğraftan alınan girdilerdir; turuncu çizgi parametre tahminidir ve gerçekten izlenen aralığı kapsar. Parametrelerin öngördüğü eğrilik korunup fotoğraftaki noktalarla karşılaştırılmalıdır; ideal bir doğru veya noktalardan geçen keyfi bir spline eğrisi, parametre uydurması sayılmamalıdır. Birkaç noktanın eşleşmesi arada fazladan tam tur olmadığını kanıtlamaz; belirgin kıvrımlarda veya büyük boşluklarda noktalar ekleyin. Tahminden açıkça sapan noktalar kırmızı gösterilir ve “Büyük sapma” açıklamasına dahil edilir. Durum satırı ortalama sapmayı, maksimum sapmayı ve otomatik genişletilen aralığı verir. Ortalama sapması 0,35 mm veya maksimum sapması 0,8 mm üzerinde olan adaylar uygulanamaz; önce kırmızı noktaları düzeltin ya da nokta ekleyin.
5. Tahmin eğrisinin eşleştiğini doğruladıktan sonra parametreleri uygulayın. Uygulama yalnızca ilgili CD / DVD / BD parametrelerini günceller (BD için iki yarıçap, toplam sektör sayısı ve kutupluluk birlikte yazılır), özgün kalibrasyon diski anlık görüntüsünü korur ve fotoğraf dönme açısını yazma başlangıç açısına aktarmaz. Uygulamadan sonra soldaki proje başlığı “Parametreler güncellendi” bildirimiyle aydınlanır; bu, projenin geometrisinin değiştiğini gösterir.

Tekerlekle yakınlaştırma ve sağ tuşla kaydırma yalnızca görünümü değiştirir, girdi verisini değiştirmez. “Yazma desenini görüntüle” özgün görseli kontrol etmek içindir; bu önizleme üzerinde fotoğraf noktaları girilemez.

Oluşturma iç ve dış yarıçapları, iz modelinin referans parametreleridir; fotoğrafta gözlenen alanın katı sınırları değildir. Tek eğri uydurması, gözlenen aralık boyunca fazı kesintisiz hesaplar. Gözlem noktaları önceden belirlenen halkayı biraz aşıyorsa, sırf programın kabul etmesi için gerçek noktaları silmek veya parametre arama aralığını genişletmek gerekmez. Gerçek yazmada yapılandırılmış iç ve dış yarıçaplar çıktı sınırları olarak kullanılmaya devam eder.

DVD, normalize yarıçap `x=(r-ri)/(ro-ri)` üzerinde üçüncü derece polinomla değişen iz aralığını destekler: `p(x)=p0*(1+c1*x+c2*x²+c3*x³)`. Tam modelin dört katsayısı vardır: `p0`, `p0*c1`, `p0*c2` ve `p0*c3`. Kapasite, bit uzunluğu ve iki yarıçap sabitlendiğinde toplam iz uzunluğu kısıtı `p0` değerini otomatik belirler; bu nedenle üç göreli şekil katsayısı ve iki yarıçap uydurulur. Sıfır katsayılar sabit iz aralığını ifade eder. Oluşturma anlık görüntüleri, tahmin bindirmeleri ve uygulanan parametreler bu katsayıları korur. BD, sabit iz aralığı (nominal 0,320 µm) kullanır ve gözlenen eğriyi pozitif yarıçaplar boyunca kesintisiz uzatır. Milimetre başına tur sayısı CD/DVD'den çok daha fazla olduğundan dar arama, yüksek artık hatalı bir iç çözüm verebilir. Arayüz artık hata eşiğiyle uygulamayı engeller ve adayı incelemek için korur.

Nokta sayısı veya kapsam yetersizse program mevcut iz aralığı şeklini korur, yalnızca yarıçapları uydurur ve ek nokta ister. Üçüncü derece uydurmada da tahmin çizgisinin tamamı, özellikle noktalar arasında fazladan tur olup olmadığı kontrol edilmelidir. Bir aday elde etmek, gerçek diskin dört fiziksel katsayısının benzersiz biçimde ölçüldüğü anlamına gelmez. Sabit doğrusal yoğunlukta fotoğraf ölçeği, yarıçaplar ve iz aralığı şekli birbirini telafi edebilir; kalan mercek bozulması da model tarafından soğurulabilir. Arayüz uyumsuz adayları incelemek için korur; açıkça yüksek artık hatalı sonuçlar uygulanamaz.

## Düzenleme ve kaydetme

Birinci adıma dönerek özgün fotoğraf incelenebilir. Sınırlar veya disk boyutu değiştirildiğinde düzeltme, izleme ve uydurma sonuçları geçersiz olur; iki adım da yeniden tamamlanmalıdır. Herhangi bir eğri noktası veya arama aralığı değiştiğinde eski uydurma ve kontur onayı da geçersiz olur.

Kalibrasyon kaydı oluşturma anlık görüntüsünü, küçültülmüş özgün fotoğrafı, kontur kontrol noktalarını, disk boyutunu ve tüm eğri noktalarını saklar; özgün fotoğraf yoluna bağımlı değildir. Yeniden açıldığında kaydedilen sınırlardan düzeltme geri yüklenir; izlemeye devam edilebilir veya yeniden hesaplanabilir. Uydurma adayları yeniden hesaplanmalıdır.

Yeni kayıtlar şema 8 kullanır; deseni, ölçülen halka çaplarını, ölçüm yöntemini ve çap toleransını saklar. Şema 7 ince artı/geniş sektör ve şema 6 ince artı kayıtları okunabilir. Eski tarak kayıtları taşınmaz. Her yazılmış disk için kendi özgün kaydını kullanın.

## Doğrulama kapsamı

Otomatik denetimler yerleşik özgün görsel ve piksel örnekleme tutarlılığını; CD/DVD/BD dosya ve akış çıktısı tutarlılığını; BD çizim aralığı ve kutupluluğunu; yapay perspektif geri elde etmeyi; dejenere sınırlar ve iptali; tek eğriden parametre çözümünü; önceden belirlenen halkayı aşan tam eğrileri; tahmin yönü ve eğriliğini; etkileşim koordinatı dönüşümlerini; nokta sürükleme ve eklemeyi; oturum geri yükleme ve geçersizleştirme kurallarını kapsar. Yazma testleri yalnızca bellek akışlarına yazar; disk yazıcılarına erişmez.

Yazılım ve yapay testler gerçek disk görünürlüğünü, mercek durumunu, nokta yerleştirme doğruluğunu veya nihai fiziksel kalibrasyon doğruluğunu kanıtlayamaz. Uygulamadan önce tahmin eğrisinin tamamını kendi fotoğrafınızla karşılaştırın.
