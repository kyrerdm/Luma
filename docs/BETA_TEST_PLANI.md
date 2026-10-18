# Luma 0.7 günlük kullanım testi

Amaç, birkaç farklı bilgisayarda normal kullanım sırasında kalan sorunları bulmak. Bu liste otomatik test sonuçlarının yerine geçmez; gerçek donanım kontrolleri içindir.

## Her testte kaydet

- Luma sürümü ve Windows derlemesi.
- Laptop/masaüstü, kullanılan ses bağlantısı ve ekran sayısı.
- Sorunu tekrarlama adımları, beklenen ve görülen davranış.
- Gerekirse **Genel → Destek raporu kaydet**. Raporu paylaşmadan önce incele; otomatik gönderilmez.

## Senaryolar

| Kontrol | Beklenen davranış | Sonuç |
| --- | --- | --- |
| Bilgisayarı yeniden başlat; Windows ile başlat açıksa oturum aç | Luma tepside tek örnek olarak hazırdır; ayarlar penceresi kendiliğinden açılmaz | Bekliyor |
| Başlat menüsünden Luma'yı art arda aç | Var olan ayarlar penceresi açılır; ikinci bir tepsi simgesi oluşmaz | Bekliyor |
| Sesi hızlıca artır/azalt; sessize al | Gösterge gecikmeden tepki verir, Windows ses düzeyiyle aynı kalır | Bekliyor |
| Bluetooth/USB kulaklığı bağla, seç, çıkar | Aygıt listesi yenilenir; kapanan aygıta komut uygulanmaz | Bekliyor |
| Spotify ve bir tarayıcıda medya aç | Kaynak seçimi ve oynatma düğmeleri aynı oturumu kontrol eder | Bekliyor |
| Uykuya al ve uyandır; ardından ses/medya/parlaklığı kullan | Uygulamayı yeniden açmak gerekmez | Bekliyor |
| İkinci monitörü çıkar ve yeniden bağla | Gösterge erişilebilir ekranda kalır, seçili monitör bağlanınca ona döner | Bekliyor |
| Ekranlarda %100, %125, %150 veya %200 ölçek kullan | Kartlar kesilmez, konum ve açılır menüler doğru kalır | Bekliyor |
| Tam ekran oyun/video aç; gizleme tercihini aç/kapat | Seçili monitörde tercih uygulanır, tuşlar çalışmayı sürdürür | Bekliyor |
| Dahili ekranı Fn tuşlarıyla ayarla | Luma doğru değeri gösterir; mevcut yöntemle Windows panelinin kısa ilk belirmesi kalabilir | Bekliyor |
| Destekleyen harici monitörde DDC/CI aç ve düzeyi ayarla | Yalnızca seçilen ekran değişir; desteklenmeyen ekran pasif kalır | Bekliyor |
| Ayarları dışa aktar, bir tercihi değiştir, yedeği içe aktar | Tercihler geri gelir; canlı ses/parlaklık ve Windows başlangıç seçimi değişmez | Bekliyor |
| Bozuk dosyayı içe aktar veya sıfırlamadan vazgeç | Mevcut tercihler korunur | Bekliyor |
| Güncelleme denetle; indirmeyi iptal et veya bağlantıyı kes | Mevcut sürüm çalışmayı sürdürür, yarım paket çalıştırılmaz | Bekliyor |
| Koyu/açık/Windows teması ve Türkçe/English arasında geç | Geçerli tercihler kaydedilir, açık menüler kullanılabilir kalır | Bekliyor |
| 30 dakika boşta ve 30 dakika medya oynarken kullan | Sürekli CPU artışı, bellek büyümesi veya gecikme görülmez | Bekliyor |

Donanım gerektiren satırlar, gerçekten denenmeden başarılı işaretlenmez. İlk tur için farklı cihaz kullanan 3–5 kişi yeterli bir başlangıçtır.

Sorunlar: https://github.com/kyrerdm/Luma/issues
