# 1.0 yayın ve doğrulama takibi

1.0.0, Windows 11 x64 için ilk kararlı GitHub sürümüdür. İngilizce yeni kurulumların varsayılan dilidir; Türkçe, Almanca, İspanyolca ve İtalyanca da sunulur. Mevcut dil tercihleri korunur. Sürüm etiketi, aşağıda açıkça bekleyen farklı donanım ve dağıtım kanalı kontrollerinin tamamlandığı anlamına gelmez.

## Bu aşamanın kapsamı

- Beş dilde canlı arayüz, dilin ayarlar/yedeklerde korunması ve İngilizce başlangıç. Eski dil alanı olmayan dosyaların Türkçe davranışının korunması.
- Her dilde 264 anahtarın, biçim parametrelerinin, erişilebilir adların ve küçük pencere yerleşiminin kontrolü; uygulama/cihaz/parça adları ve lisans gövdelerinin özgün kalması.

- Ayar dosyası bozulur veya kaybolursa son geçerli yerel yedeğe dönme; iki dosya da okunamazsa kullanıcıya varsayılanlara dönüşü açıklama.
- Başarılı kayıtlarda atomik dosya değiştirme, tek geçerli yedek ve benzersiz geçici dosyalar. Bozuk ana dosya sağlam yedeğin üzerine döndürülmez. Okumak kendiliğinden ayar kaydetmez.
- Aşırı büyük/derin dosyalar ve hatalı JSON türleri için sınırlı okuma; mevcut kullanıcıların yeniden başlangıç rehberine gönderilmemesi.
- Önizleme değerlerinin UI Automation'da salt okunur gösterge olması; görsel şablonun etkileşimli alt düğmelerinin sunulmaması. Gerçek kontrol kaydırıcıları çalışmaya devam eder.
- Sabit SDK ve commit kimlikleriyle Windows derleme kontrolü. GitHub işi kendiliğinden sürüm yayımlamaz veya kullanıcıya kurulum yapmaz.
- Gerçekte geri yüklenen çalışma zamanı paketine ait lisansları seçme; bunların gömülü bildirimlerle eşleşmemesi halinde paketlemeyi durdurma.
- ZIP, kurucu, sürüm bilgisi, lisans/rehber dosyaları ve SHA-256 dosyaları için ortak paket doğrulama komutu.

## Tekrarlanabilir kontroller

1.0.0 için yerel WPF doğrulamasında **272 kontrol** geçti. Beş dil, İngilizce varsayılanı, eski tercihlerin korunması, ses panelinde tekrarlı/hızlı tıklama, medya/ses/parlaklık servisleri, ayar kurtarma ve kurulum/güncelleme hata senaryoları kapsanır. Bu sayı gerçek donanım beta planının tamamlandığı anlamına gelmez.

```powershell
.\scripts\test-localization.ps1
.\scripts\build.ps1 -Publish -SelfContained
.\scripts\verify.ps1 -Published -TimeoutSeconds 240
.\scripts\package.ps1
.\scripts\test-package.ps1
```

Derleme SDK'sı `global.json` ile sabitlenir. `verify.ps1` gerçek Windows masaüstünde ses, medya, WPF ve klavye bütünleşmesini de kontrol eder. GitHub işi ise ses aygıtı/etkileşimli masaüstü varmış gibi davranmadan derleme ve paket doğrulamasını çalıştırır.

## Performans kaydı

```powershell
.\scripts\measure.ps1 -Minutes 30 -OutputFile .\artifacts\performance-idle.json
```

Komut çalışan Luma'yı gözler; ses, medya, parlaklık ve arayüz durumunu değiştirmez. İşlemci kullanımı toplam mantıksal işlemci kapasitesine göre hesaplanır. Özel bellek ve çalışma kümesi farklı ölçümlerdir. Rapor dosya yolları, kullanıcı adı, parça bilgisi veya aygıt kimlikleri içermez. Birden fazla Luma işlemi varsa belirli işlem `-ProcessId` ile seçilir.

Gözlem arayüzün açık/gizli olmasını veya müziğin çalıp çalmadığını sınıflandırmaz; test eden kişi koşulu ayrıca not eder. Kısa gözlem bellek sızıntısı olmadığını kanıtlamaz. 30 dakika boşta ve 30 dakika medya kullanımı ayrı koşullarda yapılmalıdır.

## Açık doğrulama alanları

- [Gerçek donanım beta planı](BETA_TEST_PLANI.md): Bluetooth/USB, uyku dönüşü, farklı Fn tuşları, çoklu monitör ve DPI, DDC/CI.
- Temiz bir Windows hesabı/makinesinde kurulum, Başlat araması, güncelleme, geri dönüş ve kaldırma.
- [Erişilebilirlik ve dil kontrolü](ERISILEBILIRLIK.md): Narrator, kontrast temaları, uzun isimler ve farklı ekran ölçekleri.
- İki ayrı uzun performans kaydı ve görülen sorunların yeniden denenmesi.
- [İmza/Store hazırlığı](DAGITIM.md): gerçek yayıncı kimliği ve sertifikayla imzalama; Store kanalı ayrı doğrulama gerektirir.

Bu maddeler yalnızca gerçekten tamamlandıkları ortam ve sürümle birlikte işaretlenir. Otomatik test sonuçları çoklu donanım testi yerine geçirilmez.
