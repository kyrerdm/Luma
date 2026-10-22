# Luma privacy policy

Last updated: 22 October 2026

Luma is an open-source Windows utility maintained by kyrerdm. It displays and controls volume, supported display brightness and media sessions exposed by Windows. This policy covers Luma's Microsoft Store and GitHub editions.

## Information used on your computer

Luma reads audio device and application session information to show output devices and application volume controls. It reads media information provided by Windows, such as the player name, track title, artist, artwork and playback position. It reads display information and, when supported and enabled, brightness information.

Luma uses a Windows keyboard hook to handle volume keys, observe standard media keys and show Caps Lock, Num Lock and Scroll Lock indicators. It does not record typed text or save a keystroke history. Media keys and ordinary typing are passed to Windows and other applications.

These features operate locally. Luma does not send your media titles, artwork, device identifiers, keyboard events or preferences to its developer. It has no advertising, analytics, telemetry service or automatic crash-report upload.

## Preferences and files

Luma saves preferences and a recovery copy on your computer. The GitHub edition uses your local application-data folder. The packaged Store edition uses its own Windows application-data folder. On its first launch, the Store edition may copy valid preferences from an existing GitHub installation; it does not change those original preference files.

A background launch replaces a small local startup-health record containing the app version, UTC time, status, retry count and an error category/code. Exception messages, stack traces, file paths and media details are not recorded. A sanitized summary may be included in a support report you choose to save. Nothing is uploaded automatically.

The GitHub edition also keeps the last announced release version locally to avoid repeating the same Windows update notification after a restart. This record contains only a version number and is not included in exported preferences or sent over the network.

Preference import/export and support reports run only when you choose them. You select where an exported file is saved. A support report contains technical information such as the app version, Windows build, architecture, display sizes, feature availability and resource use. It excludes usernames, file paths, media titles and application/device names. It is saved locally and is not sent automatically. Review a report before choosing to share it.

Uninstalling the Store edition removes its package-managed application data through Windows. Uninstalling the GitHub edition may leave preferences available for a later installation. Exported files you saved elsewhere are managed by you.

## Network access and external services

The Store edition receives updates through Microsoft Store. It does not use Luma's GitHub EXE updater. Microsoft handles Store account, download, update and diagnostic information under its own privacy policies.

In the GitHub edition, automatic update checks are enabled by default. Luma checks GitHub's release API about 15 seconds after startup and every six hours while running. Failed checks retry after one, five and fifteen minutes before returning to the six-hour interval. You can turn these checks off under General; the manual update-check action remains available. Checks request release metadata only. They do not download or install an update. Choosing Download and update connects to GitHub's download infrastructure. GitHub receives normal connection information, such as the IP address and an application-version user agent. Luma does not attach your preferences, media information or support report.

Selecting project, release or support links opens your browser or Microsoft Store. Those services process information under their own policies. Luma does not require a Luma account. If you voluntarily post an issue or attach a file to GitHub, the information you submit can be public.

## Contact and changes

For privacy questions, open an issue at [github.com/kyrerdm/Luma/issues](https://github.com/kyrerdm/Luma/issues). Do not post passwords, identity documents or other sensitive personal information in a public issue. Changes to this policy will be published in this file with an updated date.

---

# Luma gizlilik politikası

Son güncelleme: 22 Ekim 2026

Luma, kyrerdm tarafından geliştirilen açık kaynaklı bir Windows yardımcı uygulamasıdır. Ses düzeyi, desteklenen ekran parlaklığı ve Windows'un sunduğu medya oturumlarını görüntüler ve kontrol eder. Bu politika Microsoft Store ve GitHub sürümlerini kapsar.

Ses aygıtları, uygulama ses oturumları, oynatıcı adı, parça başlığı, sanatçı, kapak, oynatma konumu ve ekran bilgileri ilgili özellikleri göstermek için bilgisayarında kullanılır. Ses tuşları, standart medya tuşları ile Caps Lock, Num Lock ve Scroll Lock göstergeleri için Windows klavye kancası kullanılır. Yazdığın metin veya tuş geçmişi kaydedilmez; medya tuşları ve normal yazı girişi Windows'a ve diğer uygulamalara iletilir.

Medya bilgileri, kapaklar, aygıt kimlikleri, tuş olayları ve tercihler geliştiriciye gönderilmez. Reklam, analiz, telemetri hizmeti veya otomatik hata raporu yüklemesi bulunmaz.

Arka plan açılışında uygulama sürümü, UTC zamanı, durum, deneme sayısı ve hata kategorisi/kodunu içeren küçük bir yerel başlangıç kaydı yenilenir. Hata mesajları, çağrı yığını, dosya yolları ve medya bilgileri kaydedilmez. Kaydetmeyi seçtiğin destek raporuna güvenli bir özet eklenebilir; otomatik yükleme yapılmaz.

GitHub sürümü, aynı Windows güncelleme bildirimini yeniden açılışta tekrarlamamak için son bildirilen sürüm numarasını yerel olarak saklar. Bu kayıt yalnızca sürüm numarası içerir; dışa aktarılan tercihlere eklenmez veya ağ üzerinden gönderilmez. Başarısız otomatik denetimler, 6 saatlik aralığa dönmeden önce sırasıyla 1, 5 ve 15 dakika sonra tekrar denenir.

Tercihler ve kurtarma kopyası yerel olarak saklanır. Store sürümü kendine ait Windows uygulama verisi klasörünü kullanır; ilk açılışında mevcut GitHub kurulumundan geçerli tercihleri kopyalayabilir. Orijinal tercih dosyaları değiştirilmez. Store sürümü kaldırıldığında paket tarafından yönetilen veriler Windows tarafından kaldırılır. GitHub sürümü kaldırıldığında tercihler daha sonraki kurulum için kalabilir. Başka bir konuma dışa aktardığın dosyalar sana aittir.

Ayar dışa aktarma ve destek raporu yalnızca sen istediğinde oluşturulur. Destek raporu uygulama sürümü, Windows derlemesi, mimari, ekran boyutları, özelliklerin kullanılabilirliği ve kaynak kullanımı gibi teknik bilgiler içerir. Kullanıcı adı, dosya yolu, parça başlığı ve uygulama/aygıt adları eklenmez. Rapor otomatik gönderilmez; paylaşmadan önce inceleyebilirsin.

Store sürümünün güncellemelerini Microsoft Store yönetir. GitHub sürümünde otomatik denetim varsayılan olarak açıktır: açılıştan yaklaşık 15 saniye sonra ve uygulama çalışırken 6 saatte bir GitHub sürüm API'si denetlenir. Genel bölümünden kapatabilirsin; elle denetim kullanılabilir kalır. Bu kontroller yalnızca sürüm bilgisi ister, paket indirmez veya kurmaz. İndir ve güncelle seçildiğinde GitHub indirme altyapısına bağlanılır. Bu bağlantılarda IP adresi ve uygulama sürümünü içeren istemci başlığı gibi normal bağlantı bilgileri karşı tarafa ulaşır. Tercihler, medya bilgileri ve destek raporu eklenmez.

Proje, sürüm veya destek bağlantıları tarayıcını ya da Microsoft Store'u açar. Bu hizmetlerin kendi gizlilik politikaları geçerlidir. Luma hesabı gerekmez. GitHub'da kendi isteğinle oluşturduğun sorun kayıtları ve eklediğin dosyalar herkese açık olabilir.

Gizlilik sorularını [GitHub sorun sayfasından](https://github.com/kyrerdm/Luma/issues) iletebilirsin. Herkese açık kayıtlarda parola, kimlik belgesi veya hassas kişisel bilgi paylaşma. Politika değişiklikleri bu dosyada güncelleme tarihiyle yayımlanır.
