# 0.7 performans başlangıç ölçümü

Bu kayıt, geliştirme bilgisayarındaki kısa ölçümdür. Karşılaştırılabilir uzun kullanım ve farklı donanım ölçümleri beta planında ayrıca yer alır.

- Son dağıtım dosyası 164 otomatik kontrolden geçti.
- 1.200 ms geciktirilen ses sürücüsü benzetiminde ilk gösterge karesi 250 ms sınırının altında kaldı. Kesin ölçüm yerel doğrulama raporuna yazılır.
- Gerçek 0.6 → 0.7 kurulum ve açılış onayı 3,35 saniyede tamamlandı. Bu, ayrı bir soğuk açılış ölçümü değildir.
- Windows 11 x64 bilgisayarda ayarlar tepsiye gizliyken, tarayıcıda medya açıkken 15,1 saniyelik örnek: toplam işlemci kapasitesine göre ortalama **%0,284 CPU**, **146,5 MiB özel bellek**, **328,5 MiB çalışma kümesi**. Üç örnekte bellek değerleri aynı kaldı. Çalışma kümesi paylaşılan çalışma zamanı sayfalarını da içerir; özel bellekle aynı ölçü değildir.
- Kısa örnek, uzun süreli bellek sızıntısı veya tüm donanımlarda performans garantisi vermez. [Beta planındaki](BETA_TEST_PLANI.md) 30 dakika boşta/medyada kullanım ve uyku dönüşü kayıtları hâlâ gereklidir.

Sonraki optimizasyonlar ölçümlere göre seçilecek: ayarlar penceresinin kaynakları, medya sorgu sıklığı ve donanım taraması maliyeti. Telemetri veya arka planda rapor gönderimi eklenmez.
