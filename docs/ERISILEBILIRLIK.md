# Klavye ve dil kontrolü

1.0'da klavye, UI Automation ve beş dilde metin/yerleşim kontrolleri bulunur. Bu belge bir erişilebilirlik sertifikası veya bütün yardımcı teknolojiler için uyumluluk iddiası değildir.

## Hazır olanlar

- Ayarlar bölümleri `Ctrl+1`–`Ctrl+6` ile seçilir. Odak seçilen bölümün üzerinde kalır; `Tab` ile içeriğe geçilir.
- Sekme sırası, ileri/geri gezinme ve standart düğme etkinleştirme kullanılır. `Alt+F4` ayarları tepsiye gizler; Luma'yı sonlandırmaz.
- Gezinme düğmelerinin odak çerçevesi yerleşimi kaydırmaz. Ayar kaydırıcılarının klavye odağı görünürdür.
- Yeni Hakkında işlemlerinin, lisans seçiminin ve metninin Türkçe/İngilizce erişilebilir adları vardır. Yardım metni dil değişimini izler.
- Lisans okuyucu seçilebilir ve kopyalanabilir; UI Automation üzerinden de salt okunurdur. Metinler özgün dilinde korunur.
- 0.10'da canlı önizleme kaydırıcıları salt okunur ilerleme göstergesi olarak sunulur. Şablondaki düğme/başparmak alt kontrolleri erişilebilirlik ağacına açılmaz; gerçek kontrol kaydırıcıları çalışmayı sürdürür. İsim ve açıklamalar dil seçimini izler.
- Yeni bölümün koyu/açık görünümü ve en küçük ayar penceresindeki yatay yerleşimi otomatik kontrol edilir.

`scripts/verify.ps1` komutunda gerçek WPF kontrolleri, UI Automation sağlayıcısı, ileri/geri odak geçişi ve kısayolların komutları kontrol edilir. Genel ses veya parlaklık komutu göndererek klavye testi yapılmaz. Yayın görselleri kurgusal medya kullanır.

## Sonraki gerçek kullanıcı kontrolleri

- Narrator ile bütün ayarların, açılır ses panelinin ve medya kaynaklarının duyurularını dinleme.
- Windows kontrast temaları ve %125–%200 metin/ekran ölçeğinde kontrol.
- Klavyeyle yedek alma, geri yükleme, güncelleme ve kurulum akışlarının baştan sona denenmesi.
- Beş dilde uzun aygıt ve parça adlarıyla gerçek kullanım kontrolü.

Bu kontroller gerçekten yapılmadan tamamlandı işaretlenmez. Çoklu monitör ve farklı cihaz kontrolü [beta planında](BETA_TEST_PLANI.md) devam eder.

## Kaynaklar

- [Microsoft: erişilebilir ad ve yardım metinleri](https://learn.microsoft.com/en-us/accessibility-tools-docs/items/wpf/control_automationproperties)
- [Microsoft: WPF odak ve klavye gezinmesi](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/focus-overview)
