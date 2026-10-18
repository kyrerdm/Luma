using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Luma.Services;

internal static class Strings
{
    public static string Language { get; private set; } = "tr";
    public static event Action? Changed;
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["Genel"] = "General", ["Görünüm"] = "Appearance", ["Ses"] = "Sound", ["Medya"] = "Media", ["Ekran"] = "Display", ["AYARLAR"] = "SETTINGS",
        ["Hakkında"] = "About", ["Luma'nın imzası, bağlantıları ve lisansları."] = "The people, links and licenses behind Luma.",
        ["Windows için küçük bir ışık."] = "A little light for Windows.", ["Ses, medya ve parlaklık. İhtiyacın kadar, tam yerinde."] = "Volume, media and brightness. Just enough, just where you need it.",
        ["TASARLAYAN VE GELİŞTİREN"] = "DESIGNED AND DEVELOPED BY", ["Sürümü kopyala"] = "Copy version", ["Sürüm bilgisini kopyala"] = "Copy version information",
        ["Sürüm bilgisi kopyalandı."] = "Version information copied.", ["Panoya erişilemedi. Tekrar deneyebilirsin."] = "Couldn't access the clipboard. Try again.",
        ["GitHub projesini aç"] = "Open GitHub project", ["Sürüm notları"] = "Release notes", ["Bu sürümün notlarını aç"] = "Open notes for this release", ["Sorun bildir"] = "Report an issue", ["GitHub sorun sayfasını aç"] = "Open GitHub issues",
        ["LİSANSLAR VE TEŞEKKÜRLER"] = "LICENSES AND ACKNOWLEDGMENTS", ["Luma MIT lisanslıdır. Birlikte dağıtılan bileşenlerin kendi lisans ve bildirimleri aşağıda yer alır."] = "Luma is licensed under MIT. Bundled components have their own licenses and notices below.",
        [".NET · Üçüncü taraf bildirimleri"] = ".NET · Third-party notices", ["Windows SDK · Bildirim"] = "Windows SDK · Notice", ["Lisansı görüntülenecek bileşen"] = "Component license to view", ["Lisans metni"] = "License text", ["Salt okunur. Seçip kopyalayabilirsin."] = "Read-only. You can select and copy the text.",
        ["Lisans metinleri özgün dilinde sunulur ve çevrimdışı kullanılabilir."] = "License texts are available offline in their original language.",
        ["Kopyala"] = "Copy", ["Tümünü seç"] = "Select all",
        ["ModernFlyouts fikrinden ilham alan bağımsız bir proje. ModernFlyouts'un kodunu veya görsellerini içermez."] = "An independent project inspired by ModernFlyouts. It contains no ModernFlyouts code or artwork.",
        ["Ctrl+1–6 ile bölümler arasında geç. Tab ile ilerle, Shift+Tab ile geri dön."] = "Switch sections with Ctrl+1–6. Move forward with Tab and back with Shift+Tab.",
        ["Bağlantı açılamadı. github.com/kyrerdm/Luma adresini kullanabilirsin."] = "Couldn't open the link. You can visit github.com/kyrerdm/Luma.",
        ["Günlük kullanım, yedekler ve güncellemeler."] = "Daily use, backups and updates.",
        ["Masaüstüne uyacak küçük dokunuşlar."] = "Small touches to suit your desktop.",
        ["Ses tuşları ve ses değişiklikleri."] = "Volume keys and audio changes.",
        ["Çalan parçaya yer aç."] = "Make room for your music.",
        ["Monitörler ve ekran parlaklığı."] = "Monitors and screen brightness.",
        ["Küçük. Akıcı. Yerinde."] = "Small. Fluid. Just right.",
        ["CANLI ÖNİZLEME"] = "LIVE PREVIEW", ["Henüz medya yok"] = "No media yet", ["Bir şey oynatıldığında burada görünür."] = "Your media will appear here when you play something.", ["Başlık yok"] = "Untitled", ["Canlı"] = "Live",
        ["İhtiyaç duyduğunda görünür, sonra çekilir."] = "There when you need it, then out of the way.",
        ["ÖRNEK GÖRÜNÜM"] = "LIVE PREVIEW", ["Önizleme"] = "Preview", ["Arka planda hazır"] = "Ready in the background",
        ["Tercihlerin otomatik kaydedilir."] = "Your preferences are saved automatically.",
        ["Kaydedildi. Tercihlerin bir sonraki açılışta da burada."] = "Saved. Your preferences will be here next time.",
        ["Ses tuşları"] = "Volume keys", ["Ses açma, kısma ve sessize alma."] = "Volume up, volume down and mute.",
        ["Ses tuşlarını Luma ile kullan"] = "Use Luma for volume keys",
        ["Medya kartı"] = "Media card", ["Çalan parça, albüm kapağı ve oynatma kontrolleri."] = "Track details, artwork and playback controls.",
        ["Medya kartını göster"] = "Show media card", ["Klavye kilitleri"] = "Lock keys", ["Caps Lock, Num Lock ve Scroll Lock."] = "Caps Lock, Num Lock and Scroll Lock.",
        ["Klavye kilitlerini göster"] = "Show lock key indicators", ["Diğer ses değişiklikleri"] = "Other volume changes",
        ["Ses başka bir yerden değiştirildiğinde de göster."] = "Show when volume changes elsewhere too.", ["Diğer ses değişikliklerini göster"] = "Show other volume changes",
        ["Parlaklık göstergesi"] = "Brightness indicator", ["Fn tuşlarıyla veya Windows'tan değiştirildiğinde göster."] = "Show changes made with Fn keys or Windows.",
        ["Parlaklık değişikliklerini göster"] = "Show brightness changes", ["Windows parlaklık panelini gizle"] = "Hide Windows brightness panel",
        ["Luma gösterirken alttaki ikinci paneli gizle."] = "Hide the second panel while Luma is showing.",
        ["Dahili ekran"] = "Built-in display", ["Laptop ekran parlaklığı"] = "Laptop screen brightness",
        ["Parlaklık desteği kontrol ediliyor…"] = "Checking brightness support…",
        ["Monitör parlaklığı (DDC/CI)"] = "Monitor brightness (DDC/CI)", ["Destekleyen bağlı ekranları ayrı ayarla."] = "Adjust supported connected displays separately.",
        ["DDC/CI monitör parlaklığı"] = "DDC/CI monitor brightness", ["Monitörleri tara"] = "Scan monitors",
        ["İsteğe bağlıdır. Açılana kadar monitör sorgusu yapılmaz."] = "Optional. Monitors are only queried when enabled.",
        ["Tema"] = "Theme", ["Koyu"] = "Dark", ["Açık"] = "Light", ["Windows ile aynı"] = "Match Windows",
        ["Vurgu rengi"] = "Accent color", ["Özel renk"] = "Custom color", ["Özel renk kodu"] = "Custom color code", ["Seç"] = "Apply",
        ["Örnek: #81B7DB"] = "Example: #81B7DB", ["Panel opaklığı"] = "Panel opacity", ["Ekrandaki konum"] = "Position",
        ["Alt orta"] = "Bottom center", ["Üst orta"] = "Top center", ["Sağ üst"] = "Top right", ["Sağ alt"] = "Bottom right", ["Sol üst"] = "Top left", ["Sol alt"] = "Bottom left",
        ["Gösterilecek ekran"] = "Target display", ["Göstergenin açılacağı monitör"] = "Display used for flyouts", ["İmlecin olduğu ekran"] = "Follow cursor", ["Ana ekran"] = "Primary display",
        ["Ekran bağlı değil"] = "Display disconnected", ["Seçtiğin ekran yeniden bağlanana kadar ana ekran kullanılır."] = "The primary display is used until your chosen display reconnects.",
        ["Gösterge boyutu"] = "Flyout size", ["Ekranda kalma süresi"] = "Display duration", ["Ses adımı"] = "Volume step", ["Her basışta ses değişimi"] = "Volume change per key press",
        ["Yumuşak geçişler"] = "Smooth transitions", ["Yumuşak animasyonlar"] = "Smooth animations", ["Albüm kapağını arka plana yansıt"] = "Use blurred artwork as background",
        ["Tam ekranda göstergeleri gizle"] = "Hide flyouts in fullscreen", ["Gösterilecek ekranda oyun veya video tam ekranken açılmasın."] = "Keep flyouts hidden during fullscreen games or video on the target display.",
        ["Windows ile başlat"] = "Start with Windows", ["Oturum açıldığında tepside hazır olsun."] = "Keep Luma ready in the tray when you sign in.",
        ["AYAR YEDEĞİ"] = "PREFERENCE BACKUP", ["Tercihlerini başka bir bilgisayara taşı veya varsayılana dön. Windows başlangıç ayarı, ses ve parlaklık düzeyleri değişmez."] = "Move preferences to another PC or restore defaults. Windows startup, volume and brightness levels stay as they are.",
        ["Dışa aktar"] = "Export", ["İçe aktar"] = "Import", ["Sıfırla"] = "Reset", ["Luma tercihleri varsayılana dönsün mü?"] = "Restore Luma's default preferences?",
        ["Sıfırlamayı onayla"] = "Confirm reset", ["Vazgeç"] = "Cancel", ["GÜNCELLEME"] = "UPDATES", ["Güncellemeleri denetle"] = "Check for updates",
        ["İndir ve güncelle"] = "Download and update", ["İptal"] = "Cancel", ["Sürüm sayfası"] = "Release page", ["Yalnızca sen istediğinde GitHub'a bağlanır."] = "Connects to GitHub only when you ask.",
        ["Bu bilgisayara kur"] = "Install on this PC", ["YARDIM"] = "HELP",
        ["Destek raporu yalnızca teknik özet içerir. Dosya yolları, kullanıcı adı ve çalan parça bilgileri eklenmez. Rapor bilgisayarına kaydedilir; gönderilmez."] = "Reports contain a technical summary only. No file paths, username or track details. Saved locally, never sent automatically.",
        ["Destek raporu kaydet"] = "Save support report", ["Başlangıç rehberi"] = "Getting started",
        ["Ses şeridindeki küçük oktan ses çıkışını seçebilir ve uygulamaların sesini ayrı ayrı ayarlayabilirsin."] = "Use the small arrow in the volume bar to choose an output and adjust each app's volume.",
        ["Medya kartının sağ altındaki ok, Windows'un sunduğu oynatıcılar arasında geçiş yapar. Seçtiğin kaynağın bilgileri ve kontrolleri birlikte değişir."] = "The arrow at the bottom right of the media card switches between players exposed by Windows. Track details and controls follow your selection.",
        ["Luma ayarlarını dışa aktar"] = "Export Luma preferences", ["Luma ayarlarını içe aktar"] = "Import Luma preferences", ["Ayar yedeğin kaydedildi."] = "Preference backup saved.",
        ["Dosya kaydedilemedi. Başka bir konum seç."] = "Couldn't save the file. Choose another location.", ["Ayarlar içe aktarıldı. Windows başlangıç tercihin korundu."] = "Preferences imported. Your Windows startup choice was kept.",
        ["Ayarlar alınamadı. Geçerli bir Luma yedeği seç; mevcut tercihler korundu."] = "Couldn't import preferences. Choose a valid Luma backup; existing preferences were kept.",
        ["Varsayılan tercihler uygulandı. Windows başlangıç tercihin korundu."] = "Defaults restored. Your Windows startup choice was kept.",
        ["Ayarlar kaydedilemedi; mevcut tercihler korundu."] = "Couldn't save. Existing preferences were kept.", ["Luma destek raporu"] = "Luma support report",
        ["Destek raporu kaydedildi. Paylaşmadan önce inceleyebilirsin."] = "Support report saved. You can review it before sharing.",
        ["Luma ayarları"] = "Luma settings", ["Çıkış"] = "Exit", ["Küçült"] = "Minimize", ["Sistem tepsisine gizle"] = "Hide to system tray",
        ["Luma kurulumu"] = "Luma setup", ["Luma'yı bilgisayarına kur"] = "Install Luma on your PC",
        ["Başlat menüsüne eklenir ve Windows ayarlarından kaldırılabilir. Mevcut Luma tercihleriniz korunur."] = "Added to Start and removable from Windows Settings. Existing preferences are kept.",
        ["Masaüstüne kısayol ekle"] = "Add a desktop shortcut", ["Kur"] = "Install", ["Luma'yı aç"] = "Open Luma", ["Kapat"] = "Close", ["Kuruluyor…"] = "Installing…",
        ["Kurulum tamamlandı. Luma'yı açarak yeni konumdan devam edebilirsin."] = "Installation complete. Open Luma to continue from its new location.",
        ["Luma'ya hoş geldin"] = "Welcome to Luma", ["Masaüstünde küçük bir yer, günlük kullanımda büyük kolaylık."] = "A little space on your desktop, a little ease in your day.",
        ["Konumunu ve görünümünü seç. Bunları daha sonra ayarlardan değiştirebilirsin."] = "Choose your position and appearance. You can change them later in Settings.",
        ["Hazırım"] = "I'm ready", ["Şimdilik bu ayarlarla devam et"] = "Continue with current preferences",
        ["Ses tuşlarına basınca Luma görünür. Ayarları açmak için tepsi simgesine bir kez tıkla."] = "Press a volume key to reveal Luma. Click its tray icon once to open Settings.",
        ["Başlangıç tercihin kaydedildi."] = "Startup preference saved.", ["Tuş erişimi yok"] = "Keyboard access unavailable", ["Ses aygıtı bekleniyor"] = "Waiting for an audio device",
        ["GitHub sürümü denetleniyor…"] = "Checking GitHub release…", ["En güncel kararlı sürümü kullanıyorsun."] = "You're using the latest stable release.",
        ["Sürüm bilgisi alınamadı. Daha sonra tekrar dene."] = "Couldn't get release information. Try again later.", ["Güncelleme denetlenemedi. İnternet bağlantısını kontrol et."] = "Couldn't check for updates. Check your connection.",
        ["Yeni sürüm hazır. Kurulum için sürüm sayfasını aç."] = "An update is ready. Open the release page to install.",
        ["Yeni sürüm hazır. İndirip doğrulayarak kurabilirsin; tercihler korunur."] = "An update is ready. Download, verify and install it while keeping your preferences.",
        ["İndiriliyor… Luma çalışmaya devam ediyor."] = "Downloading… Luma is still running.", ["İndirme iptal edildi. Mevcut sürüm çalışmaya devam ediyor."] = "Download cancelled. Your current version is still running.",
        ["Güncelleme indirilemedi veya doğrulanamadı. Mevcut sürüm korundu."] = "Couldn't download or verify the update. Your current version was kept.",
        ["Doğrulandı. Yeni sürüm kuruluyor…"] = "Verified. Installing the new version…",
        ["Buz mavisi"] = "Ice", ["Lila"] = "Lilac", ["Adaçayı"] = "Sage", ["Gül kurusu"] = "Rose", ["Yakut"] = "Ruby", ["Mercan"] = "Coral", ["Şeftali"] = "Peach", ["Kehribar"] = "Amber", ["Limon"] = "Lemon", ["Turkuaz"] = "Teal", ["Okyanus"] = "Ocean", ["Gümüş"] = "Silver",
        ["SES ÇIKIŞI"] = "AUDIO OUTPUT", ["UYGULAMA SESLERİ"] = "APP VOLUMES", ["Seçili çıkıştaki uygulamalar"] = "Apps on the selected output",
        ["İşlem uygulanamadı. Aygıt veya uygulama kapanmış olabilir; yeniden deneyebilirsin."] = "Couldn't apply the change. The device or app may have closed; try again.",
        ["Açık tut"] = "Keep open", ["Otomatik gizle"] = "Auto-hide", ["Göstergeyi gizle"] = "Hide flyout", ["Göstergeyi açık tut"] = "Keep flyout open", ["Luma göstergesi"] = "Luma flyout",
        ["Ayarlar için sağ tıkla"] = "Right-click for settings", ["Medya kaynağı"] = "Media source", ["Çalıyor"] = "Playing", ["Duraklatıldı"] = "Paused", ["Kurulumu onar"] = "Repair installation",
        ["Bu işlem şu anda kullanılamıyor."] = "This action is currently unavailable.", ["Bu kaynak artık kullanılamıyor."] = "This source is no longer available.",
        ["Dahili ekran parlaklığı"] = "Built-in screen brightness", ["Medya kaynağını seç"] = "Choose media source", ["Ses düzeyi"] = "Volume",
        ["Ses çıkışları yükleniyor…"] = "Loading audio outputs…", ["Ses çıkışı"] = "Audio output", ["Uygulama sesleri"] = "App volumes", ["Ses çıkışı ve uygulama sesleri"] = "Audio output and app volumes",
        ["Sessize al / sesi aç"] = "Mute / unmute", ["Sessize al veya sesi aç"] = "Mute or unmute", ["Önceki"] = "Previous", ["Önceki parça"] = "Previous track", ["Sonraki"] = "Next", ["Sonraki parça"] = "Next track",
        ["Oynat / duraklat"] = "Play / pause", ["Oynat veya duraklat"] = "Play or pause", ["Oynatma konumu"] = "Playback position", ["Otomatik"] = "Automatic", ["Çalan kaynağı takip et"] = "Follow the playing source",
        ["Desteklenen dahili ekran bulunamadı."] = "No supported built-in display found.", ["Dahili ekran · değişiklikler anında uygulanır."] = "Built-in display · changes apply immediately.",
        ["Parlaklık değiştirilemedi. Ekran bağlantısını kontrol edip tekrar dene."] = "Couldn't change brightness. Check the display connection and try again.",
        ["# ile başlayan altı haneli renk kodu gir."] = "Enter a six-digit color code starting with #.", ["Özel rengin uygulandı."] = "Custom color applied.",
        ["Bu ses çıkışında açık bir uygulama oturumu yok."] = "No app sessions on this audio output.", ["Kullanılabilir ses çıkışı bulunamadı."] = "No audio outputs available.", ["Ses çıkışları okunamadı. Tekrar açarak deneyebilirsin."] = "Couldn't read audio outputs. Try opening this panel again.",
        ["DDC/CI yanıtı alınamadı. Monitör menüsünü ve bağlantıyı kontrol et; dahili ekran için yukarıdaki kaydırıcıyı kullan."] = "No DDC/CI response. Check the monitor menu and connection; use the slider above for the built-in screen.",
        ["Parlaklık doğrulanamadı. Monitörü tekrar tara."] = "Couldn't verify brightness. Scan monitors again.",
        ["Monitörler sorgulanıyor…"] = "Scanning monitors…", ["Destekleyen ekranlarda monitör menüsünden DDC/CI açık olmalıdır."] = "Enable DDC/CI in the menu of supported monitors.",
        ["Bağlı fiziksel monitör bulunamadı."] = "No physical monitors found.", ["Monitör desteklemiyorsa veya DDC/CI kapalıysa kaydırıcı pasif kalır."] = "Sliders stay disabled if DDC/CI is unavailable or switched off.", ["Monitör kontrolü kapalı."] = "Monitor control is off."
    };
    private static readonly Dictionary<string, string> Turkish = English.GroupBy(p => p.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Key, StringComparer.Ordinal);
    public static string T(string text)
    {
        var original = Turkish.GetValueOrDefault(text, text);
        return Language == "en" ? English.GetValueOrDefault(original, original) : original;
    }
    public static void SetLanguage(string language)
    {
        var next = language == "en" ? "en" : "tr";
        if (next == Language) return;
        Language = next;
        foreach (var option in Luma.Themes.Palette.Options) option.RefreshLanguage();
        Changed?.Invoke();
    }
    public static void Apply(DependencyObject root)
    {
        var visited = new HashSet<DependencyObject>();
        void Visit(DependencyObject node)
        {
            if (!visited.Add(node)) return;
            if (node is Window window) window.Title = T(window.Title);
            if (node is TextBlock text && string.IsNullOrEmpty(text.Text) == false) text.Text = T(text.Text);
            if (node is ContentControl { Content: string content } control) control.Content = T(content);
            if (node is HeaderedItemsControl { Header: string header } headerControl) headerControl.Header = T(header);
            if (node is FrameworkElement { ToolTip: string tip } element) element.ToolTip = T(tip);
            var name = AutomationProperties.GetName(node);
            if (name.Length > 0) AutomationProperties.SetName(node, T(name));
            var help = AutomationProperties.GetHelpText(node);
            if (help.Length > 0) AutomationProperties.SetHelpText(node, T(help));
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) Visit(child);
            if (node is Visual) for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Visit(VisualTreeHelper.GetChild(node, i));
        }
        Visit(root);
    }
}
