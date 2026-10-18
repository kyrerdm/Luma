# Luma geliştirme planı

## 0.7 — Günlük kullanım

- Genel, Görünüm, Ses, Medya ve Ekran bölümleri.
- Doğrulanan sürümlü JSON yedeği, içe/dışa aktarma ve onaylı sıfırlama.
- Yeni kurulumlarda kısa başlangıç rehberi; eski kullanıcıların tercihlerinin korunması.
- Türkçe/İngilizce arayüz seçimi.
- İsteğe bağlı, yerel ve kişisel veri içermeyen teknik destek raporu.
- Kullanıcının başlattığı indirme; GitHub sürüm varlığının boyutu ve SHA-256 özetiyle doğrulama.
- Yeni kurulumun açılışını bekleme; başarısızsa mevcut kurulumun kısayollarını/başlangıç yolunu geri yükleyip önceki sürümü açma.
- Uyku dönüşünde aygıtları yeniden sorgulama, eski ses aygıtına bekleyen yazıları iptal etme.
- Otomatik hata senaryoları ve gerçek WPF ekran görüntüleriyle doğrulama.

## 0.8 — Canlı önizleme ve hizalama

- Ses çıkışı okunun taşmayan, dengeli yerleşimi ve hafif vurgusu.
- Ses kartının sağ kenarına hizalanan, konuma göre açılan ses paneli.
- Ayarlarda mevcut ses seviyesi, sessize alma, seçili medya, kapak ve canlı ilerleme.
- Medya/aygıt yokken doğru boş durum; gizli ayarlarda durdurulan önizleme saati.
- Dört köşe ve üç ölçekte gerçek pencere koordinatlarıyla hizalama kontrolü.

## Donanımla doğrulanacaklar

[Beta test planı](BETA_TEST_PLANI.md): Bluetooth/USB değişimleri, farklı laptop Fn tuşları, uyku dönüşü, çoklu monitör/DPI, gerçek DDC/CI yazımı ve uzun süreli bellek/CPU kullanımı. Tek bir bilgisayarın sonucu tüm donanımlar için destek iddiası oluşturmaz.

## 1.0 — Dağıtım

[Dağıtım hazırlığı](DAGITIM.md): yayıncı kimliği ve dijital imza, Microsoft Store paketleme/uyumluluk denemesi, temiz Windows kurulumunda yükleme-güncelleme-kaldırma testi, dil ve erişilebilirlik son kontrolü.

ARM64 için ayrı derleme ve donanım testi bundan sonra değerlendirilir.

## Ayrı araştırma

- Windows parlaklık panelinin ilk karede de engellenmesi. Mevcut yaklaşım panel göründükten sonra gizler; tamamen engellendiği iddia edilmez.
- Her tarayıcı sekmesini ayrı kontrol etme. Windows'un sunduğu birleşik oturumları ayırmak için ek tarayıcı entegrasyonu gerekir.

Bu araştırmalar kararlı dağıtıma sistem içine yüklenen deneysel bileşen ekleme taahhüdü değildir.
