# Luma'yı Microsoft Store'a gönderme

Hazırlık sürümü: **1.0.5 / MSIX 1.0.5.0**. Henüz Microsoft Store'a gönderilmedi veya mağaza tarafından onaylanmadı. İstanbul'a döndüğünde bu rehberden devam edebilirsin. GitHub dağıtımı 1.0.5 sürümüdür; Store gönderimi ayrı bir adımdır.

## 1. Geliştirici hesabını tamamla

[Microsoft Store geliştirici sayfasından](https://storedeveloper.microsoft.com/) ücretsiz kayıt akışını başlat. Microsoft hesabıyla oturum açma, kimlik doğrulaması ve geliştirici sözleşmesini sen tamamlamalısın. Şifreni, tek kullanımlık kodlarını veya kimlik belgeni Codex'e ya da GitHub'a gönderme.

## 2. Uygulama adını ayır ve paket kimliğini al

Partner Center'da yeni Windows uygulaması oluşturup **Luma** adını ayırmayı dene. Bu adın müsaitliği henüz doğrulanmadı. Doluysa görünen mağaza adını birlikte belirleyelim.

Uygulamanın **Product management → Product identity** bölümünden şu üç alanı kopyala:

1. `Package/Identity/Name`
2. `Package/Identity/Publisher`
3. `Package/Properties/PublisherDisplayName`

Bunlar paket için gereken yayıncı bilgileridir; parola değildir. Ayırdığın görünen uygulama adını da belirt. Bu bilgiler gelmeden doğru kimlikle son Store paketi oluşturulamaz.

## 3. Son paketi ve kurulum testlerini tamamla

Codex'e “Store hesabım hazır, paket kimlikleri bunlar; Luma Store hazırlığından devam et” yazabilirsin. Tamamlanan kaynak kodu ana daldadır.

Kod ve derleme adımları [gönderim rehberinde](SUBMISSION.md) bulunur. Gerçek yayıncı kimliği `.local/StoreIdentity.json` dosyasına girilip üretim MSIX'i oluşturulur. Ardından gerçek paketli kurulum, başlangıç ayarı, ses/medya/parlaklık erişimi, güncelleme/kaldırma ve Windows App Certification Kit kontrolleri tamamlanır.

`Development` yazan MSIX dosyası ayrı kimlikli bir **yerel test paketidir; Store'a yüklenmez**. Normal çift tıklamayla kurulacak imzalı dağıtım değildir. Microsoft'un Windows 11 için belgelediği imzasız test kimliği OID'sini içerir. `Add-AppxPackage -AllowUnsigned` ile çalıştırılabilir test kurulumu yönetici yetkisi gerektirir ve diğer kullanıcıları etkileyebilir; mevcut hazırlık scripti bunu otomatik yapmaz. Windows güvenlik ayarlarını kapatma veya sertifika uyarılarını aşma.

## 4. Mağaza sayfasını doldur

- Açıklamalar ve özellik listeleri: [LISTING.md](LISTING.md), İngilizce öncelikli beş dil.
- Gizlilik politikası: [PRIVACY.md](../PRIVACY.md). Gönderimden önce internette erişilebilen bağlantısı doğrulanır.
- Destek: <https://github.com/kyrerdm/Luma/issues>.
- Görseller: `images/01-overview.png`, `images/02-audio.png`, `images/03-personalization.png` ve `images/store-tile-300.png`.
- Önerilen ilk fiyat: ücretsiz. Yaş derecelendirmesi, pazarlar ve yayın tarihi gerçek Partner Center sorularına göre tamamlanır.

Görseller gerçek Luma ekranlarından, hayalî parça ve aygıt bilgileriyle üretildi. Kişisel müzik geçmişin veya masaüstü görüntün kullanılmadı. Teknik inceleme ekibi için İngilizce notlar `SUBMISSION.md` içindedir.

## 5. İncelemeye gönder ve sonucu doğrula

Üretim paketi, açıklamalar ve kontroller tamamlandığında Partner Center'dan gönderim yapılır. Microsoft incelemesi bitmeden uygulamanın Store'da yayımlandığı söylenmez. Onaydan sonra mağaza bağlantısı ve Store'dan gerçek indirme/kurulum doğrulanır.

İmzalama için MSIX Store yolunda ayrıca sertifika satın alınmaz; onaylanan paketi Microsoft imzalar. Bu işlem GitHub'dan indirilen ayrı EXE dosyasını kendiliğinden imzalamaz.
