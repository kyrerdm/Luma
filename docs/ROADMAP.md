# Luma geliştirme planı

## 0.10.1 — Ses paneli düzeltmesi

- Ses çıkışı paneli açıkken oka yeniden tıklamanın paneli kapatıp geri açması giderildi.
- WPF'nin dış tıklama yakalaması ile düğmenin aynı basışı yeniden işlemesi önlendi; dışarı tıklayarak kapatma korundu.
- Gerçek fare girdileriyle dört köşe, animasyon sırasında hızlı tıklama ve yeniden açma; erişilebilir düğme çağrısıyla aç/kapat kontrolü.

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

## 0.9 — Hakkında, lisanslar ve klavye kontrolü

- Logo, sürüm/platform, kyrerdm imzası ve proje bağlantılarıyla Hakkında bölümü.
- Luma için MIT lisansı; kurulum ve ZIP paketinde aynı lisansın korunması.
- Gömülü kaynaklardan tam metinle çalışan çevrimdışı lisans okuyucu.
- Ctrl+1–6 bölüm kısayolları, odak göstergeleri ve TR/EN erişilebilir metinleri.
- Salt okunur UI Automation, ileri/geri odak ve minimum pencere yerleşimi doğrulaması.
- [1.0 öncesi klavye ve dil kontrolünün ilk turu](ERISILEBILIRLIK.md). Narrator, kontrast temaları ve gerçek cihaz testleri devam eder.

## 0.10 — Sağlamlaştırma

- Son geçerli yerel ayar yedeği, sınırlı okuma ve görülebilir kurtarma durumu.
- Kayıt hatalarında sağlam dosyaların ve geçici dosya sahipliğinin korunması.
- Salt okunur önizleme sağlayıcıları; gerçek kaydırıcıların kullanılabilirliğinin korunması.
- Sabit SDK ile GitHub'da otomatik Windows derleme/paket kontrolü.
- Gerçek çalışma zamanına göre bildirim seçimi ve paket bütünlüğü doğrulaması.
- Yerel CPU/bellek gözlem aracı ve [1.0 doğrulama takibi](KARARLI_SURUM.md).

## Donanımla doğrulanacaklar

[Beta test planı](BETA_TEST_PLANI.md): Bluetooth/USB değişimleri, farklı laptop Fn tuşları, uyku dönüşü, çoklu monitör/DPI, gerçek DDC/CI yazımı ve uzun süreli bellek/CPU kullanımı. Tek bir bilgisayarın sonucu tüm donanımlar için destek iddiası oluşturmaz.

## 1.0 — Dağıtım

[Dağıtım hazırlığı](DAGITIM.md): yayıncı kimliği ve dijital imza, Microsoft Store paketleme/uyumluluk denemesi, temiz Windows kurulumunda yükleme-güncelleme-kaldırma testi, dil ve erişilebilirlik son kontrolü.

ARM64 için ayrı derleme ve donanım testi bundan sonra değerlendirilir.

## Ayrı araştırma

- Windows parlaklık panelinin ilk karede de engellenmesi. Mevcut yaklaşım panel göründükten sonra gizler; tamamen engellendiği iddia edilmez.
- Her tarayıcı sekmesini ayrı kontrol etme. Windows'un sunduğu birleşik oturumları ayırmak için ek tarayıcı entegrasyonu gerekir.

Bu araştırmalar kararlı dağıtıma sistem içine yüklenen deneysel bileşen ekleme taahhüdü değildir.
