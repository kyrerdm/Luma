# Luma

Windows 11 için sade ve akıcı ses, medya ve klavye göstergeleri. İnce bir ses şeridi, ayrı bir medya kartı ve ihtiyacın bittiğinde kaybolan küçük kontroller.

**[Windows için indir](https://github.com/kyrerdm/Luma/releases/latest)** · [Kurulum](#kurulum) · [Özellikler](#özellikler) · [Kaynak koddan derleme](#kaynak-koddan-derleme)

![Luma'nın ses şeridi ve medya kartı](docs/images/luma-motion.gif)

*Görsellerdeki parça, kapak ve kaynak bilgileri örnek veridir.*

## Kurulum

**Gereksinim:** Windows 11, 64 bit (x64). ARM64 için ayrı paket henüz yoktur.

1. **[Son sürüm sayfasını aç](https://github.com/kyrerdm/Luma/releases/latest).**
2. Sayfanın **Assets** bölümünden **`Luma-0.4.0-win-x64.zip`** dosyasını indir. GitHub'ın eklediği **Source code** dosyaları çalıştırılabilir uygulamayı içermez.
3. İndirilen ZIP'e sağ tıkla ve **Tümünü ayıkla** seçeneğiyle kalıcı bir klasöre çıkar. Örneğin `C:\Users\kullanıcı\Apps\Luma`.
4. Çıkardığın klasördeki **`Luma.exe`** dosyasını aç.
5. Klavyeden sesi açıp kısarak göstergeyi dene. Ayarlar ekranından **Göstergeyi dene** düğmesini de kullanabilirsin.

Paket, gereken .NET Desktop Runtime dosyalarını içerir. **Ayrıca .NET veya Visual Studio yüklemek gerekmez.** Bu sürüm taşınabilirdir; bir kurulum sihirbazı ya da MSI paketi içermez. Dosyaları ZIP'in içinden çalıştırmak yerine önce klasöre çıkar.

### Her oturumda otomatik başlatma

Ayarlar ekranında **Windows ile başlat** seçeneğini aç. Luma sonraki oturumlarda sistem tepsisinde hazır olur. Bu ayarı açtıktan sonra uygulama klasörünü taşımadan önce seçeneği kapat; yeni konumdan açtıktan sonra tekrar etkinleştir.

### Güncelleme

1. Eski Luma'nın **sistem tepsisi simgesine sağ tıkla → Çıkış** seç. Pencereyi kapatmak uygulamadan çıkmaz.
2. Yeni ZIP'i ayrı bir klasöre çıkar ve yeni **`Luma.exe`** dosyasını aç.
3. **Windows ile başlat** zaten açıksa yeni Luma.exe açıldığında başlangıç yolu güncellenir. Kapalıysa kapalı kalır.

Mevcut tercihler korunur; ayarlar `%LOCALAPPDATA%\LumaFlyouts\settings.json` dosyasında saklanır.

### Kaldırma

**Windows ile başlat** seçeneğini kapat, tepsiden **Çıkış** seç ve Luma'yı çıkardığın klasörü sil. Tercihlerini de kaldırmak istersen `%LOCALAPPDATA%\LumaFlyouts` klasörünü silebilirsin.

## Özellikler

| Özellik | Açıklama |
| --- | --- |
| İnce ses şeridi | Ses açma, kısma, sessize alma, kaydırıcı ve fare tekerleğiyle kontrol. |
| Ayrı medya kartı | Windows'a medya oturumu sunan oynatıcılardan parça, sanatçı ve kapak bilgisi. |
| Oynatma kontrolleri | Oynatıcının desteklediği ölçüde oynat/duraklat, önceki/sonraki ve zaman çizelgesi. |
| Medya kaynağı seçimi | Sağ alttaki okla Windows'un sunduğu kaynakları listele; seçilen oynatıcının bilgileri ve düğmeleri birlikte değişir. |
| Klavye göstergeleri | Caps Lock, Num Lock ve Scroll Lock durumu. |
| Akıcı geçişler | Kartların açılışı, kapanışı, boyut değişimi ve ses kaydırıcısı için hafif animasyonlar. |
| Görünüm tercihleri | Renk örnekleriyle 12 vurgu rengi; albüm kapağının bulanık arka plan yansıması. |
| Konum ve süre | Dört köşe ve üst/alt orta; %75–%140 boyut ayarı, görünme süresi ve ses adımı. |
| Çoklu ekran | İmlecin olduğu ekran, ana ekran veya bağlı monitörlerden biri seçilebilir. Seçilen ekran çıkarılırsa ana ekran kullanılır. |
| Sistem tepsisi | Tek sol tıkla ayarlar; koyu sağ tık menüsünde önizleme ve uygulamadan tamamen çıkma. |

## Kullanım ipuçları

- Ses şeridindeki hoparlör simgesi sesi kapatıp açar. Fare tekerleği, imleç şeridin üzerindeyken ses seviyesini değiştirir.
- İmleci göstergenin üzerine getirince otomatik gizleme durur. Medya kartının sol altındaki **pin** simgesi göstergeyi açık tutar; tekrar basınca otomatik gizlemeye döner. Kilit tuşu bildirimleri geçicidir; süreleri bitince sabitlenmiş ses/medya kartı geri gelir.
- Göstergeye **sağ tıklayarak** ayarlara ulaşabilir veya göstergeyi gizleyebilirsin.
- Medya oturumu yoksa boş medya kartı gösterilmez. Oynatıcının desteklemediği düğmeler pasif olur.
- Medya kartının **sağ altındaki ok**, kaynak listesini açar. Listede uygulama, parça adı ve çalma durumu görünür. Kaynak değiştirmek diğer oynatıcıları durdurmaz veya başlatmaz.
- Manuel seçimin kaynak duraklatılsa da korunur. Kaynak kapanırsa kalan oturumlara dönülür. Menüdeki **Otomatik** seçeneği çalan kaynağı takip eder; Luma yeniden açıldığında otomatik modla başlar.
  Bu seçim Luma kartındaki medya düğmeleri için geçerlidir.
- **Gösterilecek ekran** alanından monitör seçebilirsin. Seçtiğin ekran bağlı değilken ana ekran kullanılır; yeniden bağlandığında tercih ettiğin ekrana dönülür.
- **Göstergeyi dene** örnek veri kullanır. Bu moddaki ses, medya ve zaman çizelgesi kontrolleri gerçek sesi veya oynatıcıyı değiştirmez.
- Önizlemede kaynak oku da denenebilir; örnek kaynaklar arasında geçiş sistem oynatıcılarını etkilemez.
- Ayarlar penceresini üst çubuğundan tutarak taşıyabilirsin. Renk, konum ve monitör listeleri koyu tema ve arayüzle aynı yazı tipini kullanır.
- Vurgu renkleri: buz mavisi, lila, adaçayı, gül kurusu, yakut, mercan, şeftali, kehribar, limon, turkuaz, okyanus ve gümüş. Renkler listedeki küçük örneklerle gösterilir.
- Ayarlar penceresini kapatmak Luma'yı tepsiye gizler. Tamamen kapatmak için tepsi simgesine sağ tıkla ve **Çıkış** seç.

<details>
<summary>Ayarlar ekranı</summary>

![Luma ayarları](docs/images/luma-settings.png)

</details>

<details>
<summary>Tepsi menüsü ve renk seçenekleri</summary>

![Koyu tepsi menüsü](docs/images/luma-tray-menu.png)
![Renk örnekleriyle vurgu listesi](docs/images/luma-accent-menu.png)

</details>

<details>
<summary>Medya kaynağı seçimi</summary>

![Kaynak seçme oku](docs/images/luma-source-card.png)
![Medya kaynakları listesi](docs/images/luma-source-menu.png)

</details>

## Mevcut durum ve sınırlamalar

**0.4.0 erken bir sürümdür.** Ses ve klavye göstergeleri ile medya kartının uygulaması hazırdır. Parlaklık kontrolü, açık tema, ARM64 paketi, kurulum sihirbazı ve Microsoft Store yayını henüz yoktur. Paket henüz dijital olarak imzalı değildir.

Medya bilgisi ve düğmeler oynatıcının Windows'a sunduğu özelliklere bağlıdır. Canlı yayınlarda süre veya ileri sarma olmayabilir. Windows medya API'sine erişim ve komut yönlendirmesi kontrol edildi; canlı Spotify/diğer oynatıcılarda uçtan uca kullanım henüz doğrulanmadı.

Kaynak listesi [Windows'un sunduğu medya oturumlarından](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager.getsessions) oluşur. Normal Chrome sekmeleri [ortak bir sistem medya oturumu kullanır](https://chromium.googlesource.com/chromium/src/%2B/HEAD/content/browser/media/system_media_controls/); bu durumda iki sekme ayrı ayrı seçilemez. Chrome–Spotify gibi ayrı oturumlar listelenebilir. Her tarayıcı sekmesini bağımsız kontrol etmek ek tarayıcı entegrasyonu gerektirir.

Standart ses tuşları CoreAudio üzerinden işlenir. Bazı üreticilerin Fn yazılımları veya kumandaları farklı yollar kullanabilir; bunlarda Windows'un kendi paneli de görünebilir. Fiziksel Fn tuşları, yönetici olarak çalışan uygulamalar ve farklı DPI değerlerinde çoklu monitör kullanımı ayrıca denenmelidir.

Bir sorunla karşılaşırsan **[Issues](https://github.com/kyrerdm/Luma/issues)** bölümüne Windows sürümünü, Luma sürümünü, kullandığın oynatıcıyı/donanımı ve sorunu tekrar oluşturma adımlarını ekleyebilirsin.

## Gizlilik ve sistem davranışı

Luma telemetri göndermez, ağ isteği yapmaz ve klavye kaydı tutmaz. Klavye kancası ses ve kilit tuşları için kullanılır. Explorer veya Windows sistem dosyaları değiştirilmez. **Windows ile başlat** açıldığında yalnızca mevcut kullanıcının `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` alanına `LumaFlyouts` girdisi eklenir.

## Kaynak koddan derleme

Son kullanıcıların bu adımlara ihtiyacı yoktur; hazır uygulama **[Releases](https://github.com/kyrerdm/Luma/releases)** bölümündedir.

Geliştirme için **Windows 11**, **Git** ve **.NET 10 SDK x64 (10.0.402 veya daha yeni bir 10.0 SDK)** gerekir. Proje WPF, Win32/CoreAudio ve Microsoft Windows SDK .NET projeksiyonlarını kullanır; harici bir UI kütüphanesi içermez.

```powershell
git clone https://github.com/kyrerdm/Luma.git
cd Luma
.\scripts\build.ps1
.\scripts\verify.ps1
```

Çalışma zamanı dahil taşınabilir paket:

```powershell
.\scripts\build.ps1 -Publish -SelfContained
.\scripts\verify.ps1 -Published
.\scripts\package.ps1
```

Çıktı klasörü `artifacts\Luma-0.4.0`, paylaşılacak dosya `artifacts\Luma-0.4.0-win-x64.zip` konumundadır. SHA-256 özeti ZIP'in yanındaki `.sha256` dosyasına yazılır. Hata ayıklama sembolleri dağıtım ZIP'ine eklenmez.

`build.ps1 -Publish`, çalışma zamanını dahil etmeden paket oluşturur; bu çıktı için .NET 10 Desktop Runtime gerekir. Kişisel ayarlardan ayrı bir geliştirme oturumu için:

```powershell
.\artifacts\Luma-0.4.0\Luma.exe --data-dir .\.local
```

### Tepki süresi

Ses aygıtı, medya sorguları ve tuş dinleyicisi arayüzden ayrı yürütülür. Gösterge ilk tuştan önce hazırlanır; yavaş bir aygıt sorgusu panelin görünmesini bekletmez. Bekleyen eski ses yanıtları yeni kullanıcı girişini geri almaz.

### Doğrulama kapsamı

0.4.0 için **67 kontrol** başarılı oldu. Kontroller; ayarların kurtarılmasını ve kalıcılığını, CoreAudio okumasını, gerçek klavye kancasına yazılımsal ses tuşu basışları gönderilmesini, Windows medya API'sinin açılmasını, WPF pencerelerini ve kullanıcı tercihlerini kapsar. Üst çubuğun Windows sürükleme alanı olması, pencere düğmelerinin tıklanabilmesi, koyu açılır listeler ve sabitlenmiş medya üzerinde geçici kilit bildirimleri de doğrulanır. Ayrıca 12 rengin kalıcılığı, tepsi tıklama davranışı ve WinForms tepsi menüsünün koyu zemini/vurgu rengi kontrol edilir. Kaynak seçimi, ayrı oturumlara komut yönlendirme, kaynak kapanınca geri dönüş, yükleme sırasında seçim değişikliği ve kaynak menüsünün otomatik gizlemeyi durdurması izole oturumlarla doğrulanır. Önizlemenin sistem komutları göndermemesi, animasyonun fazladan ses isteği üretmemesi, zaman çizelgesi ve boş medya kartının kapanması da kontrol edilir.

Dört köşe yerleşimi, %75 / %100 / %140 ölçekleri, bağlı ekranların listelenmesi ve iki ekranlı bir benzetimde seçim/bağlantı kesilmesi davranışı kontrol edilir. Gerçek monitör takıp çıkarma ve farklı DPI kombinasyonları henüz doğrulanmadı. 1.200 ms bekleyen bir ses aygıtı benzetiminde ilk görüntü hazırlığı 250 ms sınırının altında kalır; bu değer fiziksel Fn tuşunu veya gerçek donanımın uçtan uca gecikmesini ölçmez. Testler normal Windows masaüstü erişimi gerektirir; başsız veya kısıtlı bir ortamda giriş testleri çalışmayabilir. Başlangıç kaydı değiştirilmez. Sonuçlar ve WPF'den alınan görseller `artifacts\verification` içine yazılır. Animasyon karelerini GIF'e çevirmek için Pillow bulunan bir Python ile `scripts\render-motion.py` çalıştırılabilir.

## İlham ve üçüncü taraf bileşenler

Luma, [ModernFlyouts](https://github.com/ModernFlyouts-Community/ModernFlyouts) fikrinden ilham alan bağımsız bir projedir. ModernFlyouts'un kodunu veya görsel varlıklarını içermez; onunla bağlantılı resmi bir devam projesi değildir. Örnek albüm kapağı WPF ile çizilmiştir.

Dağıtım paketindeki .NET, Windows Desktop, Windows SDK ve C#/WinRT bildirimleri uygulamanın yanındaki lisans/bilgi dosyalarında bulunur.
