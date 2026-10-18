# İmza ve Microsoft Store hazırlığı

0.8 dağıtımı kullanıcı hesabına kurulan, çalışma zamanı dahil Windows 11 x64 EXE ve taşınabilir ZIP olarak devam eder. Henüz dijital imza veya Store yayını yapılmış değildir.

## İmzalı GitHub sürümü

1. Yayıncıya ait üretimde kullanılabilir bir kod imzalama sertifikası veya yönetilen imzalama hizmeti seçilir. Anahtarlar ve parolalar repoya eklenmez.
2. `scripts/build.ps1 -Publish -SelfContained` çalıştırılır.
3. Yerel sertifika deposu kullanılıyorsa `scripts/sign.ps1 -CertificateThumbprint <parmak-izi>` çalıştırılır. İmza aracının SHA-256 dosya özeti ve zaman damgası doğrulanır.
4. **İmzalanan EXE** üzerinde `scripts/verify.ps1 -Published` çalıştırılır.
5. `scripts/package.ps1` ile aynı imzalı dosyanın Setup kopyası, ZIP ve son SHA-256 özetleri oluşturulur. İmzadan sonra dosya değiştirilmez.
6. Temiz bir Windows makinesinde kurulum, Başlat menüsü, başlangıç tercihi, güncelleme ve kaldırma denenir.

İmzalama scripti sertifika oluşturmaz, satın almaz veya Windows güven ayarlarını değiştirmez. Sertifika/hizmet sahibi tarafından sağlanınca kullanılabilir.

## Microsoft Store denemesi

- Yayıncı hesabında Luma adı, yayıncı kimliği ve paket kimliği alınır.
- MSIX için ayrı deneme paketi hazırlanır. Mevcut EXE kurucusu, PowerShell kaldırıcı ve uygulama içi güncelleme Store paketinin içine aynen taşınmaz.
- Paket kimliği altında sistem tepsisi, CoreAudio/medya/WMI/DDC erişimi, klavye kancası ve otomatik başlatma davranışları gerçek Windows'ta doğrulanır.
- Eski GitHub kurulumuyla yan yana çalışma ve ayar taşıma davranışı belirlenir; aynı anda iki Luma'nın çalışması önlenir.
- Store dağıtımında güncellemeler mağazaya bırakılır; GitHub güncelleyicisi o kanalda kapatılır.
- Türkçe ve İngilizce mağaza açıklamaları, gerçek uygulama görselleri, destek ve gizlilik bilgileri hazırlanır.
- Windows App Certification Kit ve mağaza incelemesi tamamlanır.

Hesap ve yayıncı kimliği mevcut olmadan sahte paket kimliğiyle yayımlama yapılmaz. Gerçek Store onayı ve imzalama, bu repodaki kod değişikliklerinden ayrı bir dağıtım adımıdır.

## Resmi kaynaklar

- [Dağıtım yolu seçimi](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/choose-distribution-path)
- [Kod imzalama seçenekleri](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)
- [Microsoft Store başlangıcı](https://learn.microsoft.com/en-us/windows/apps/publish/get-started)
- [GitHub sürüm varlıkları ve digest alanı](https://docs.github.com/en/rest/releases/assets)
