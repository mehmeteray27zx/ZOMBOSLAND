# ZOMBOSLAND
## Oyun Tasarım Belgesi — GDD v1.0

**Tarih:** 10 Kasım 2026  
**Belge türü:** Yaşayan oyun tasarımı, sistem şartnamesi ve yapılacak işler  
**Ürün:** Windows PC / Steam için birinci şahıs açık dünya zombi hayatta kalma oyunu  
**Geliştirme yönü:** Önce tek oyunculu oynanabilir çekirdek; ardından dünya, hayatta kalma ve co-op  
**İş modeli hedefi:** Ücretsiz oynanabilir; kozmetik gelir modeli henüz doğrulanacak  
**Durum:** Mevcut proje belgelerinden birleştirilmiş tasarım taslağı; yeni öneriler ayrıca belirtilmiştir.

> Bu belge nelerin yapılacağını ve nasıl çalışması gerektiğini tanımlar. Bir özelliğin burada yer alması, oyunda uygulanmış veya test edilmiş olduğu anlamına gelmez. Çalışma klasöründe Unity kaynak projesi bulunmadığından mevcut uygulama durumu kod veya canlı oyun üzerinden doğrulanmamıştır.

---

## İçindekiler

1. [Vizyon ve oyuncu deneyimi](#1-vizyon-ve-oyuncu-deneyimi)
2. [Kararlar, varsayımlar ve mevcut durum](#2-kararlar-varsayımlar-ve-mevcut-durum)
3. [Kapsam ve geliştirme aşamaları](#3-kapsam-ve-geliştirme-aşamaları)
4. [Temel oynanış döngüleri](#4-temel-oynanış-döngüleri)
5. [Oyuncu hareketi, kamera ve kontroller](#5-oyuncu-hareketi-kamera-ve-kontroller)
6. [Çatışma, silahlar ve hasar](#6-çatışma-silahlar-ve-hasar)
7. [Zombiler, algı ve gürültü](#7-zombiler-algı-ve-gürültü)
8. [Karşılaşmalar ve dünya nüfusu](#8-karşılaşmalar-ve-dünya-nüfusu)
9. [Sağlık ve hayatta kalma](#9-sağlık-ve-hayatta-kalma)
10. [Etkileşim ve eşya toplama](#10-etkileşim-ve-eşya-toplama)
11. [Envanter, ekipman ve depolama](#11-envanter-ekipman-ve-depolama)
12. [Üretim, tamir ve kaynak ekonomisi](#12-üretim-tamir-ve-kaynak-ekonomisi)
13. [Güvenli merkez ve üs kurma](#13-güvenli-merkez-ve-üs-kurma)
14. [Dünya, harita ve keşif](#14-dünya-harita-ve-keşif)
15. [Görevler, hikâye ve öğretici](#15-görevler-hikâye-ve-öğretici)
16. [İlerleme ve tekrar oynanabilirlik](#16-ilerleme-ve-tekrar-oynanabilirlik)
17. [Ölüm, yeniden deneme ve zorluk](#17-ölüm-yeniden-deneme-ve-zorluk)
18. [Kayıt ve dünya kalıcılığı](#18-kayıt-ve-dünya-kalıcılığı)
19. [Arayüz, menüler ve erişilebilirlik](#19-arayüz-menüler-ve-erişilebilirlik)
20. [Sanat, animasyon, ses ve efektler](#20-sanat-animasyon-ses-ve-efektler)
21. [Co-op ve ağlı oyun](#21-co-op-ve-ağlı-oyun)
22. [Ekonomi, Steam ve canlı işletim](#22-ekonomi-steam-ve-canlı-işletim)
23. [Teknik sistemler ve performans](#23-teknik-sistemler-ve-performans)
24. [İçerik üretim listesi](#24-içerik-üretim-listesi)
25. [Aşamalı yapılacak işler](#25-aşamalı-yapılacak-işler)
26. [Test ve kabul ölçütleri](#26-test-ve-kabul-ölçütleri)
27. [Açık kararlar ve riskler](#27-açık-kararlar-ve-riskler)
28. [İlk uygulanacak iş paketi](#28-ilk-uygulanacak-iş-paketi)
29. [Kaynaklar ve değişiklik kaydı](#29-kaynaklar-ve-değişiklik-kaydı)

---

## 1. Vizyon ve oyuncu deneyimi

### 1.1. Kısa tanım

Zombosland, oyuncunun terk edilmiş bir bölgede kaynak aradığı, ateş etmenin sonuçlarını yönettiği ve bulduklarıyla güvenli yaşam alanını geliştirdiği bir FPS hayatta kalma oyunudur. Oyuncu her seferinde ne kadar ilerleyeceğine, neyi taşıyacağına, ne zaman çatışacağına ve ne zaman geri döneceğine karar verir.

**Ana vaat:** Bir kutu mühimmat veya birkaç parça kumaş için çıktığın yolculuk, kendi kararların yüzünden tehlikeli hâle gelebilir; sağ salim geri dönmek üssünde görünür bir ilerleme yaratır.

### 1.2. Tasarım sütunları

| Sütun | Oyunda karşılığı | Başarı sorusu |
|---|---|---|
| Güçlü FPS hissi | Duyarlı hareket, okunabilir nişan, tutarlı isabet, iyi ses ve animasyon | Oyuncu ateş ettiğinde ne olduğunu anlayabiliyor mu? |
| Gürültünün bedeli | Atış ve dikkat dağıtıcılar çevredeki zombilerin kararlarını etkiler | Oyuncu çatışmak ile sessizce geçmek arasında seçim yapıyor mu? |
| Anlamlı keşif | Farklı kaynak sunan mekânlar, alternatif rotalar, uzaktan seçilen işaretler | Bir sonraki yere gitmek için anlaşılır bir neden var mı? |
| Görünür kalıcı gelişim | Elektrik, depo, tezgâh, açılan geçitler ve üs parçaları | Seferden dönünce dünya veya hazırlık imkânları değişiyor mu? |
| Gerilim ve rahatlama | Tehditli dış dünya ile güvenli hazırlık alanının dönüşümlü kullanımı | Oyun sürekli aynı yoğunlukta mı, yoksa nefes aldırıyor mu? |
| Okunabilir gerçekçilik | Tutarlı sanat yönü, görünür düşmanlar, anlaşılır sistemler | Atmosfer korunurken bilgiye ulaşmak mümkün mü? |

### 1.3. Oyuncu ve oturum hedefi

**Önerilen hedef kitle:** Keşif, kaynak yönetimi ve FPS çatışmasını birlikte isteyen; her karşılaşmayı zorunlu savaş olarak görmek istemeyen PC oyuncuları. Rekabetçi PvP, ilk ürünün merkezinde değildir.

İlk İkmal görevi için 8–12 dakika; ilk açık bölgedeki hazırlık–keşif–üretim–dönüş döngüsü için 15–20 dakika hedeflenir. Açık bölge süresi, ilk görevin süresine eklenmiş zorunlu toplam değildir; ayrı bir deneyim ölçümüdür. Sonraki tam oyun seferleri için 20–40 dakika yeni bir tasarım önerisidir. Hiçbiri ölçülmüş sonuç değildir.

### 1.4. Başarı tanımı

Oyuncu hedefini anlayabilmeli, kaynak uğruna bilinçli risk alabilmeli, çatışmadan kaçabilmeli ve döndüğünde ne kazandığını söyleyebilmelidir. İlk doğrulama sorusu: “Oyuncu daha büyük harita olmadan bu döngüyü ikinci kez oynamak istiyor mu?”

## 2. Kararlar, varsayımlar ve mevcut durum

### 2.1. Belgenin nasıl okunacağı

- **Önceki karar:** Master Plan'da ürün yönü olarak belirtilen tercih.
- **Önceki öneri:** Tasarım raporlarında yer alan, uygulaması doğrulanmamış davranış.
- **Yeni öneri:** Bu GDD'nin boşlukları doldurmak için önerdiği davranış; kullanıcı onayı alınmış karar sayılmaz.
- **Açık karar:** Üretime geçmeden netleştirilmesi gereken seçenek.
- Sayısal değerler aksi belirtilmedikçe başlangıç dengeleme değerleridir. Gerçek proje verileri okunmadan mevcut değerlerin üzerine körlemesine yazılmaz.

### 2.2. Ürün kararları

| Alan | Tasarım durumu |
|---|---|
| Kamera | Önceki karar: birinci şahıs; ilk sürümde üçüncü şahıs modu yok |
| Platform | Önceki karar: Windows PC / Steam |
| Motor ve dil | Önceki karar: Unity / C#; belgelerde 6000.6.5f1 yazıyor, gerçek proje sürümü ayrıca doğrulanacak |
| Görsel altyapı | URP ilk aday; nihai seçim gerçek proje ve ölçümlerle yapılacak |
| Oyun yönü | Açık dünya zombi survival; önce küçük ve elle tasarlanmış bölge |
| Çok oyunculu | Tek oyunculu çekirdekten sonra co-op; oyuncu sayısı ve servisler açık |
| Gelir modeli | F2P hedefi; kozmetik odaklı model hipotez |
| Dünya kimliği | Sanayi mahallesi ile ormanlık kırsal; Türkiye esintisi önceki öneri, kesin hikâye coğrafyası değil |
| Performans | Belirlenecek referans PC'de 1080p / 60 FPS, 30 aktif zombi hedefi |

### 2.3. Eldeki belgelerin gösterdiği durum

| Sistem | Önceki raporlarda görülen/bildirilen | Bu GDD için uygulama durumu |
|---|---|---|
| FPS çatışma | Eller, tabanca, nişan, atış ve doldurma görüntüleri | Görsel gözlem aktarımı; mantık ve hissiyat testi bekliyor |
| Sağlık | Can göstergesinin azalması | Hasar kuralları ve ölüm akışı doğrulanacak |
| Zombi | Yaklaşan düşmanlar ve yere düşen bedenler | Algı, saldırı, gürültü ve ölüm kuralları doğrulanacak |
| Sigorta görevi | Bulma/takma hedefleri görülmüş; tamamlanma kullanıcı tarafından bildirilmiş | Kapı, ışık, tekrar etkileşim ve kayıt birlikte test edilecek |
| Ayarlar | Hassasiyet, FOV, kamera hareketi, ses, öğretici seçenekleri görülmüş | İşlevler ve kalıcılık kontrol edilecek |
| Açık bölge, crafting, üs, co-op | Tasarım belgelerinde tarif edilmiş | Uygulanmış kabul edilmiyor |

### 2.4. Belgeler arasındaki farkların birleştirilmesi

Bu GDD için aşağıdaki taslak standartlar kullanılır:

| Konu | Önceki fark | GDD v1.0 tercihi |
|---|---|---|
| Bandaj | Eski görev belgesinde +30; daha yeni açık dünya belgesinde +25 | +25 can; 2 sn kullanım, 3 sn üretim. Kullanım ve üretim ayrı işlemler |
| Görev bitişi | Odaya dönüşte bitiş veya panoya takınca bitiş | Başarılı sigorta takma ana görevi tamamlar; odaya dönüş sonraki yönlendirme olur |
| Kapılar | İlk görev dönüş bağlantısı ile dış dünya kapısı karışabilir | İç dönüş yolu baştan erişilir; dış bölge kapısı elektrik gelince açılır |
| Harita boyutu | 120 × 100 m görev alanı, 150 × 150 m ilk açık parça, 500 × 500 m bölge | Farklı aşama ölçekleridir; üç bağımsız dev harita talebi değildir. Mevcut alan mümkün olduğunca yeniden kullanılır |
| Taşıma | İlk görevde sayaçlar; açık bölgede 24 yuva | Geçici sayaçlar kalıcı envantere taşınır; iki ayrı eşya kaynağı tutulmaz |
| Sonraki silah | Önceki raporda pompalı öncelikli | Güncel açık bölge döngüsündeki sopa/dikkat taşı önce; pompalı çekirdek doğrulandıktan sonra |

Bu tercihler belgedeki tutarlılığı sağlar; gerçek uygulama farklıysa davranış karşılaştırılıp karar kaydı güncellenir.

## 3. Kapsam ve geliştirme aşamaları

### 3.1. Aşama tanımları

| Aşama | Hedef teslim | İçeriği | Sonraki aşamaya geçiş |
|---|---|---|---|
| M0 — Mevcut temeli doğrula | Çalışan çatışma ve görev temeli | Hareket, tabanca, zombi, etkileşim, sigorta görevi | Kritik çatışma ve görev hataları kapanır |
| M1 — İlk açık parça | Baştan sona oynanan küçük survival döngüsü | 150 × 150 m parça, iki rota, loot, 24 yuva, 3 tarif, sabit depo, temel kayıt | Topla–üret–kullan–dön–kaydet/yükle çalışır |
| M2 — İlk tam bölge | İlk temsilî oynanabilir bölüm | Yaklaşık 500 × 500 m, 5 odak, ilk yapı yuvaları, görsel/ses standardı | Dış oyuncu testi ve performans kapısı |
| M3 — Survival derinliği | Dengelenmiş tek oyunculu temel | Beslenme/su, geliştirilmiş ilerleme, dünya yenilenmesi, koşullu yeni tehditler | Kaynak ekonomisi ve uzun oturumlar doğrulanır |
| M4 — Co-op prototipi | Küçük ağlı oynanış testi | Yetkili hasar/envanter, bağlantı, ortak dünya, kayıt sahipliği | Gecikme, kopma ve eşya tutarlılığı testleri |
| M5 — Yayına hazırlık | Tanımlanmış kapsamlı test sürümü | Steam, ayarlar, yerelleştirme, dağıtım, destek, ürün/gelir kararı | Yayın kontrol listesi ve operasyon hazırlığı |

M4 prototipi, pahalı M3 içerik çoğaltmasından önce teknik riski ölçmek için öne alınabilir. Bu, ilk tek oyunculu çekirdeği bekletmez. Erken erişimde co-op bulunup bulunmayacağı ayrı ürün kararıdır.

### 3.2. İlk açık parça için zorunlu sistemler

FPS hareket/kamera; tabanca ve sopa; sağlık/bandaj; zombi görme/duyma/araştırma/saldırma; tek etkileşim sistemi; loot; envanter/hızlı yuva; üç çalışan tarif; görev geçişi; güvenli depo; iki rota; temel kayıt; HUD/öğretici/ayarlar; ölümden tutarlı yeniden deneme.

### 3.3. İlk teslimin dışında kalanlar

Araç sürüşü, büyük şehir, prosedürel sonsuz dünya, serbest çok katlı inşa, PvP, oyuncu baskını, ticaret pazarı, NPC fraksiyonları, tam yetenek ağacı, tarım, mevsimler, evcil hayvan, boss, rastgele silah istatistiği ve gerçek para mağazası M1 gereksinimi değildir.

Bunların “bütün sistemler” başlığı altında değerlendirilmesi, tamamının yapılacağı sözü değildir. Gelecek adayları için 16. ve 27. bölümlerde karar kapıları bulunur.

## 4. Temel oynanış döngüleri

### 4.1. Anlık döngü

Çevreyi oku → tehdidi fark et → rota/silah seç → yaklaş, atlat veya çatış → isabet/hasar/gürültü sonucunu değerlendir → yeni konuma geç.

### 4.2. Sefer döngüsü

Üste hazırlan → bir ihtiyaç veya hedef seç → bölgeye git → kaynak ve bilgi topla → taşıma kapasitesi/sağlık/mühimmatı değerlendir → gerekirse üret veya iyileş → dönüş yolunu seç → kaynakları depola → üssü ya da ekipmanı geliştir.

### 4.3. Uzun vadeli döngü

Güvenli merkez kur → çevreyi öğren → yeni tarif/tezgâh/rota aç → daha riskli bölgeye eriş → nadir bileşen getir → merkezin işlevlerini artır → yeni hedefe hazırlan.

### 4.4. Başarı, başarısızlık ve çıkış

İlk görevde başarı elektriğin geri gelmesidir. Serbest seferde başarı oyuncunun hedeflediği kaynağı veya keşif bilgisini güvene almasıdır. Bir alanın bütün zombilerini öldürmek genel başarı şartı değildir. Oyuncu herhangi bir seferi erken kesip geri dönebilir; yalnızca geri dönüşü engellemek için kapılar kapanmaz.

## 5. Oyuncu hareketi, kamera ve kontroller

### 5.1. Hareket

Yürüme, koşma, eğilme ve zıplama mevcut kontrolcü üzerinde doğrulanır. Eğim, basamak, kapı, alçak tavan ve havada hareket davranışları belirgin olmalıdır. Eğilmeden kalkarken tavan kontrol edilir. Oyuncu dünya sınırından düşerse son güvenli konuma alınır; bu kurtarma eşya veya görev çoğaltmaz.

**Yeni başlangıç önerileri:** Yürüme 4 m/sn, koşma 6 m/sn, eğilme 2 m/sn. Gerçek harita ve animasyon ölçeğine göre değiştirilecek değerlerdir. İlk görev zorunlu zıplama/eğilme engeline bağlanmaz. Yüzme, sürünme, kayma ve parkur M1 dışında kalır.

### 5.2. Kamera ve konfor

Hassasiyet, ADS hassasiyeti, ters Y, FOV; headbob, silah salınımı ve kamera sarsıntısının ayrı kontrolü gerekir. Başlangıç FOV önerisi 90, ayar aralığı 70–110 yatay eşdeğeridir; motorun FOV ekseniyle dönüşümü doğrulanır. Değerler ölçülmüş rahatlık standardı değildir.

Kamera efektlerini kapatmak gerçek isabet kuralını değiştirmez. Görsel silah hareketi ile hasar yönü ayrı tutulur; nişangâh gerçek atış davranışını doğru anlatmalıdır. ADS geçişi ve koşudan çıkış hedefi gereksiz sallamamalıdır.

### 5.3. Girdi sözleşmesi

| Eylem | Önerilen varsayılan | Temel kural |
|---|---|---|
| Hareket / bakış | WASD / fare | Menü odağı varken oyuna sızmaz |
| Koş / eğil / zıpla | Shift / Ctrl / Space | Basılı tut/aç-kapa tercihi gereken eylemlerde sunulur |
| Ateş / ADS | Sol / sağ fare | Duruma uygun değilse işlem uygulanmaz |
| Doldur / etkileşim | R / E | Güncel tuş etiketi arayüze yansır |
| Envanter / duraklat | Tab / Esc | Envanter dünyayı sürdürür; tek oyunculu duraklatma oyunu durdurur |
| Hızlı yuvalar | 1–4 | Envanterdeki eşyaya referans verir |
| Fener / harita | F / M | Yalnızca ilgili özellik etkinse gösterilir |

Eylemler yeniden atanabilir olmalı; çakışma ve varsayılana dönme işlemi açık sunulmalıdır. Kontrolcü desteği M5 ürün kararıdır; desteklenirse tüm menü ve nişan akışı birlikte tamamlanır.

**Kabul:** Kapı/eğim/tavan testleri geçer; menü açılırken ateş edilmez; odak kaybından dönüşte takılı tuş kalmaz; azaltılmış kamera hareketiyle oyun tamamlanabilir.

## 6. Çatışma, silahlar ve hasar

### 6.1. Ateşli silah kuralları

İlk ateşli silahlar hitscan kullanır. Kamera hedefi belirler, namlu–hedef arasındaki engel kontrolü gerçek atışa izin verilip verilmediğini belirler. Namlunun önünde duvar varsa kamera hedefi görse bile duvarın arkasındaki zombi vurulmaz.

Silah durumları: hazır, ateşleme, toparlanma, doldurma, silah değiştirme ve devre dışı. Ölen oyuncu ateş edemez. Ateş hızı, mühimmat tüketimi ve hasar tek bir oynanış akışından çıkar; ses/animasyon olayı hasarı ikinci kez çalıştırmaz.

İlk doldurma davranışı: tamamlanana kadar ateş yok, oyuncu isteğiyle iptal yok. Mühimmat tamamlanmada tek kez aktarılır. Ölüm/sahne değişimi gibi kesintiler kilitlenme veya ücretsiz mermi üretmez. Doldurma sırasında silah değiştirme ilk sürümde engellenir. Envanter açılması/aktif eşya kullanımı için aynı çakışma kuralı uygulanır ve kısa durum bilgisi verilir.

### 6.2. Silah rolleri

| Silah | Rol | Bedel | Kapsam |
|---|---|---|---|
| 9 mm tabanca | Kontrollü başlangıç silahı | Kalabalık baskısında zayıf, isabet ister | M0–M1 |
| Ahşap sopa | Mühimmat harcamayan yakın seçenek | Yakına girme ve toparlanma riski | M1 |
| Sarılı dikkat taşı | Çatışmayı başka yöne taşıma | Tek kullanımlık, yanlış konum tehlikeyi artırabilir | M1 |
| Pompalı | Yakında güçlü durdurma | Fazla gürültü, düşük kapasite, yavaş doldurma | M2 adayı |
| Hafif makineli | Yakın mesafede kısa seri | Hızlı mühimmat tüketimi | M3 adayı |
| Yarı otomatik karabina | Orta mesafede hedef seçimi | Seyrek mühimmat, kuşatmada zorluk | M3 adayı |
| Levye/balta | Sopa sonrası rol farklılığı | Dayanıklılık ve araç işlevi ayrıca tasarlanmalı | İhtiyaca bağlı |

Silah sayısı yerine rol farkı aranır. Eklenti, susturucu, dürbün ve silah dayanıklılığı M1'de yoktur. İleride susturucu eklenirse tamamen sessiz ateş garantisi vermez; gerçek algı yarıçapına bağlanır.

### 6.3. Başlangıç denge verileri

| Değer | Başlangıç önerisi |
|---|---|
| Oyuncu / standart zombi canı | 100 / 100 |
| Tabanca şarjör / başlangıç rezervi | 12 / 36 |
| Tabanca gövde / kafa hasarı | 34 / ×3 |
| Tabanca ateş sınırı / doldurma | 4 atış/sn / 1,8 sn |
| Pompalı kapasite / saçma | 6 / 8 × 15 hasar, tamamı isabet ederse 120 |
| Pompalı ateş / ilk doldurma | 1 atış/sn / 3,2 sn tam doldurma |
| Sopa — yeni öneri | 25 hasar, 1,3 m erişim, en fazla 1 vuruş/sn |

Mesafe, saçma dağılımı ve zırh uygulanıyorsa gerçek hasar değişir. Pompalının her mesafede tek atışta öldürdüğü varsayılmaz. Silah verileri düzenlenebilir tanımlardan gelir; denge değişikliği kod değişimi gerektirmemelidir.

### 6.4. Yakın dövüş ve fırlatma

Sopa; hazırlık → etkin vuruş penceresi → toparlanma akışını kullanır. Tek savuruş aynı hedefe en fazla bir kez hasar verir. Duvar arkasına, erişim dışına veya ölüm sonrası hedefe yeni saldırı uygulanmaz. İlk sürümde her savuruş en yakın geçerli hedefi vurur; geniş alan temizleme rolü verilmez.

Dikkat taşı oyuncunun bakışına göre fırlatılır. İlk geçerli düşüşte bir ses olayı üretir; her fizik sekmesinde yeni kalıcı tehdit oluşturmaz. Duvar içinden doğma ve oyuncu kapsülüyle çarpışma kontrol edilir. Fırlatma başarıyla başladığında bir adet tüketilir; fizik nesnesi oluşturulamazsa tüketim geri alınır. İlk sürümde geri toplanmaz; görsel nesne süre sonunda temizlenir.

### 6.5. Hasar ve geri bildirim

Ortak hasar kaydı; kaynak, hedef, miktar, bölge, isabet konumu ve saldırı kimliği taşır. Hafif tepki, ağır sendeleme ve ölüm ayrı sonuçlardır. Her tabanca mermisi zombiyi tamamen durdurmaz. Ölüm tek kez işlenir; ölü zombi yeni saldırı üretemez.

Kafa/gövde ayrımı okunabilir olmalı. Oyuncuya yönlü hasar bilgisi, kısa ses ve ayarlanabilir ekran etkisi verilir. Kan efekti nişanı uzun süre kapatmaz. Organ koparma ve ayrıntılı parçalanma M1 gereksinimi değildir.

**Kabul:** Yakın duvar; boş/kısmi şarjör; yetersiz rezerv; 30 ardışık doldurma; silah değişimi; birden fazla collider; ölüm sonrası vuruş; sopa menzili ve taşın tek ses olayı kontrol edilir.

## 7. Zombiler, algı ve gürültü

### 7.1. Temel davranış

Durumlar: bekleme/dolaşma → şüphe/araştırma → görsel takip → saldırı hazırlığı → saldırı → toparlanma. Hedef kaybolursa son bilinen konuma gider, sınırlı süre arar, sonra dolaşmaya döner. Sendeleme ve ölüm bu akışa kontrollü müdahale eder.

Zombi oyuncunun konumunu her durumda bilmez. Görüşte mesafe, açı ve engel; seste olayın konumu ve işitilebilirlik kullanılır. Oyuncu doğrudan görülüyorsa dikkat taşı anında ve koşulsuz hedef değiştirtmez. Ses araştırması ile kesin görsel hedef birbirinden ayrılır.

### 7.2. Başlangıç algı önerileri

| Olay | İlk test değeri | Davranış |
|---|---:|---|
| Görsel algı | 25 m, 100° toplam görüş açısı | Görüş hattı ve kısa doğrulama sonrasında takip |
| Tabanca sesi | 45 m yarıçap | İşitebilen mevcut zombiler kaynağı araştırır |
| Pompalı sesi | 65 m | Daha geniş çevreyi etkiler |
| Dikkat taşı | 18 m | Taşın düştüğü konuma araştırma |
| Koşu sesi | 10 m | Yakındaki ve hedefi olmayan zombiyi uyarır |
| Hedef kaybı araması | Yaklaşık 6 sn | Son bilinen konum çevresinde arar |

Bunlar yeni dengeleme önerileridir; ilk 150 m parçada gereğinden büyük baskı oluşturursa küçültülür. Duvarların ses azaltması daha sonraki iyileştirmedir. M1'de uygulanmıyorsa oyuncuya “duvar sesi tamamen keser” öğretisi verilmez.

### 7.3. Saldırı

**Yeni başlangıç önerisi:** Standart saldırı 10 hasar; yaklaşık 0,6 sn görünür hazırlık ve 1,2 sn toparlanma. Kesin erişim gerçek kapsüllere göre ayarlanır. Hasar anında hedefin menzil ve engel koşulları tekrar kontrol edilir; saldırı başladı diye uzaklaşmış oyuncuya vurulmaz.

Kalabalıkta zombilerin aynı noktaya yığılması azaltılır. Saldırıya izin verilen yakın düşman sayısı ile toplam aktif düşman sayısı farklı ayarlardır. İlk görevde aynı anda 4–6 aktif tehdit, yük testinde 30 aktif zombi hedeflenebilir.

### 7.4. Türler

| Tür | Oynanış farkı | Ekleme koşulu |
|---|---|---|
| Standart | Gürültü, yaklaşma ve kaçışın temelini öğretir | M0 |
| Koşucu | Oyuncuyu konum değiştirmeye zorlar; önceden okunabilir ses/hareket | M2 sonrası, kaçış rotaları doğrulanınca |
| Ağır | Bir geçidi tutar; yavaş dönüş ve belirgin saldırı açıklığı | M3 adayı; yalnız fazla can yeterli değil |
| Ses çağıran | Durdurulabilir alarm davranışı | Ancak normal gürültü ekonomisi dengelendikten sonra |

İlk aşamada tek türün kıyafet, yürüyüş ritmi ve animasyon başlangıcı çeşitlendirilir. Özel düşman, yeni modelden önce yeni ve anlaşılır karar üretmelidir.

**Kabul:** Görüş engeli arkasındaki oyuncuya sürekli kilitlenmez; duyduğu noktayı araştırır; kayıp hedefi sonsuza kadar takip etmez; saldırı ve ölüm tekrar hasar üretmez; kapı, raf ve eğimde takılı kalma kaydedilir.

## 8. Karşılaşmalar ve dünya nüfusu

### 8.1. İlk karşılaşma modeli

Önceden yerleştirilmiş sınırlı düşman grupları kullanılır. Atış yeni zombi yaratmaz; var olanları hareketlendirir. Temizlenmiş bir koridor oyuncunun arkasında açıklamasız dolmaz. İlk görevde ana sokakta 2, depoda 2–3, dönüş çevresinde 3–5 düşman başlangıç planıdır; hepsinin öldürülmesi zorunlu değildir.

### 8.2. Sonraki nüfus sistemi — yeni öneri

Her bölge için nüfus bütçesi, etkin düşman sınırı, yenilenme bekleme süresi, izinli doğma noktaları ve kalıcı temizlenme kuralları tanımlanır. Yeni doğma; kamera görüşü dışında, oyuncudan yeterince uzakta ve ulaşılabilir rotada gerçekleşir. Kesin uzaklıklar harita ve hareket hızıyla test edilir.

Yenilenme yalnızca bölgeden çıkıldı ve belirlenen süre geçtiyse değerlendirilebilir. Kaydet/yükle nüfusu rastgele yeniden doldurmaz. İlk açık parçada zombi/loot yenilenmesi kapalıdır; uzun vadeli yenilenme M3 kapısıdır.

### 8.3. Tempo ve olaylar

Güvenli hazırlık, küçük çatışma, keşif, kısa baskı, toparlanma ve dönüş rahatlaması dönüşümlü kullanılır. M1'de kapsamlı dinamik yönetici gerekmez. M3 adayları: arızalı alarm, önceden duyulan sürü geçişi, riskli ikmal noktası ve sınırlı merkez savunması.

Olayın tetikleyicisi, süresi, düşman sınırı, ödülü, başarısızlığı ve tekrar koşulu tanımlanmadan üretime alınmaz. Güvenli merkezin işlevini ortadan kaldıran sürekli saldırı başlangıç tasarımı değildir.

## 9. Sağlık ve hayatta kalma

### 9.1. Sağlık ve iyileşme — M1

Can 0–100 aralığındadır. Sıfır can ölüm akışını başlatır. İlk sürümde otomatik tam iyileşme yoktur. Bandaj +25 can verir, üst sınırı aşmaz; tam canda tüketilmez. Üretim bandaj oluşturur, kendiliğinden iyileştirmez.

Bandaj kullanımı 2 sn sürer. Bu sırada ateş ve diğer eşya kullanımı engellenir; koşu iptal edilir, yürüyüşe izin verilir. Hasar, ölüm veya oyuncunun iptali işlemi keser; tamamlanmadan bandaj tüketilmez. Kullanım sonunda iyileşme ve tüketim tek işlem olarak uygulanır. Kullanılan eşya işlem boyunca ayrılamaz veya depoya taşınamaz.

### 9.2. Stamina — yeni öneri, M2 adayı

Koşu ve yakın dövüş arasında kaynak kararı oluşturuyorsa eklenir. İlk deneme: 100 stamina, koşuda saniyede 12, sopa vuruşunda 15 tüketim; tüketim durduktan 1,5 sn sonra saniyede 18 yenilenme. Stamina biterse koşu yürüyüşe döner. Sopa için yeterli stamina yoksa saldırı başlamaz; oyuncunun normal yürümesi engellenmez.

Stamina eklenmeden rota ve düşman hızları yeniden test edilir. İlk kaçış deneyimini bozuyorsa M3'e ertelenir. HUD yalnız çalışan stamina sistemini gösterir.

### 9.3. Açlık ve susuzluk — yeni öneri, M3

Amaç sürekli sayaç doldurtmak değil, sonraki seferin hazırlığını anlamlı kılmaktır. İkisi ayrı 0–100 değerdir. Yalnız oynanan dünya zamanında azalır; oyun kapalıyken veya tek oyunculu duraklatmada kayıp olmaz. İlk 15–20 dakikada tam dolu ihtiyaçların ölüm eşiğine düşmesi hedeflenmez.

Düşük değer önce okunabilir uyarı, sonra stamina toparlanma cezası verir. Sıfır ihtiyaçta sağlık kaybı eklenip eklenmeyeceği zorluk testine bağlı açık karardır. Aynı duruma birden çok ceza üst üste verilmez. Yiyecek/içecek tamamlanan kullanımda tüketilir; tam ihtiyaçta gereksiz tüketim engellenir veya açıkça onaylanır.

### 9.4. İleri hayatta kalma adayları

| Sistem | Tasarım amacı | İlk davranış önerisi | Kapsam kararı |
|---|---|---|---|
| Kanama | Çatışmadan sonra acil tedavi kararı | Sınırlı süreli can kaybı, net ikon/metin; özel tedavi ayrımı gerekebilir | M3 deneyi; bandajın rolü yeniden dengelenir |
| Enfeksiyon | Riskli temas sonrası yeni sefer hedefi | Her vuruşta değil, tanımlı saldırıda; evre ve tedavi açık anlatılır | Tedavi erişimi garantilenmeden eklenmez |
| Sıcaklık/ıslaklık | Hava ve kıyafet arasında hazırlık | Okunabilir ortam etkisi ve sığınma çözümü | Dinamik hava sonrası aday |
| Uyku/yorgunluk | Sefer süresi ve üs dinlenmesi | Güvenli yerde zaman atlama, ihtiyaç/olay uzlaşması | Co-op zaman kuralları çözülmeden eklenmez |
| Zırh | Savunma ile taşıma/konfor tercihi | Hasar azaltma, açık dayanıklılık ve onarım | M3 adayı |

Bu sistemler yalnız HUD göstergesi olarak eklenmez; sebep, sonuç, çözüm ve kaynak erişimi birlikte tamamlanır.

## 10. Etkileşim ve eşya toplama

Tek etkileşim eylemi; kapı, eşya, kap, pano, tezgâh ve depo için kullanılır. Oyuncu geçerli hedefe baktığında, erişim ve görüş hattı uygunsa kısa etiket görünür. Başlangıç erişim önerisi 2 m'dir. Üst üste nesnelerde öncelik en yakın geçerli odak hedefindedir.

Eşya alma bir işlem olarak yürütülür: hedef mevcut mu → alınabilir miktar ne → kapasite yeterli mi → envantere ekle → dünya miktarını azalt. Başarısız alımda eşya dünyadan silinmez. Kısmi alım destekleniyorsa alınan ve kalan miktar açık gösterilir; desteklenmiyorsa tamamı için yer gerekir.

Görev sigortası ayrı görev alanında taşınır; çanta doluluğuna takılmaz, satılmaz ve düşürülemez. Görev tamamlanınca tüketimi ve görev geçişi birlikte gerçekleşir.

Loot aileleri: mühimmat, sağlık, hammaddeler, araç/ekipman, görev eşyası ve ileride nadir bileşen. İlk yerleşimler sabittir. Sonradan isteğe bağlı loot aday noktalarda değişebilir; ana görev ve ilk bandaj için gerekli kaynak şansa bağlı kalmaz.

**Kabul:** Aynı nesneye hızlı tekrar basmak çoğaltma yaratmaz; dolu çanta eşyayı yok etmez; duvar arkasındaki nesne alınmaz; yükleme sonrası alınmış eşya geri gelmez.

## 11. Envanter, ekipman ve depolama

### 11.1. M1 taşıma modeli

- Çanta: 24 yuva, 6 × 4 görünüm. İlk sürümde ağırlık sistemi yok.
- Hızlı kullanım: 4 yuva; çantadaki eşya/ekipmana referans, ayrı kopya değil.
- Ekipman: tabanca, ana silah ve yakın dövüş için ayrı tanımlı yerler önerilir. Bir eşya aynı anda hem ekipman hem çanta mülkiyetinde görünmez.
- Görev alanı: kritik eşyalar için ayrı bölüm; normal kapasite tüketmez.
- Güvenli merkez deposu: yeni öneri olarak 24 yuva; oyuncu yapımı ek sandık başına 12 yuva.
- Mühimmat rezervi envanterde tutulur; HUD aynı verinin toplamını okur. Dolu şarjördeki mermi ayrıca silaha aittir; iki kez sayılmaz.

### 11.2. Eşya verileri

Her eşyanın kalıcı tür kimliği, adı, açıklaması, ikon referansı, kategori, yığın sınırı, kullanım/ekipman davranışı, dünya temsili ve gerekiyorsa örnek kimliği bulunur. İlk yığın önerileri: ham kaynak 20, bandaj 5, dikkat taşı 5, tabanca mermisi 60; silah ve yapı kitleri 1. Bunlar denge değeridir.

Eşsiz silah/ekipmanın örnek kimliği korunur. Yığın bölme/birleştirme, tekli alma, hepsini taşıma ve hızlı yuvaya atama davranışları tanımlanır. M1'de sürükleme zorunlu değildir.

### 11.3. İşlem kuralları

Depoya aktarımda ya iki taraf birlikte güncellenir ya hiçbiri değişmez. Üretime ayrılmış veya kullanımda olan miktar aktarılamaz. Eşya biterse hızlı yuva boş durumuna geçer; yanlış bir başka eşyaya sessizce bağlanmaz. Çıkarılan silahın şarjörü kendi verisiyle korunur.

Normal eşyayı dünyaya bırakma eklenirse güvenli düşürme noktası, benzersiz kimlik, kayıt ve geri alma birlikte gerekir. M1'e yetişmiyorsa bırakma düğmesi sunulmaz; depolama ile kapasite yönetilir. Görev eşyaları hiçbir durumda bu akışa girmez.

**Kabul:** Envanter–depo–ekipman–şarjör toplamları korunur; negatif miktar oluşmaz; hızlı yuvada hayalet eşya kalmaz; kayıt/yükleme yığın ve ekipmanı değiştirmez.

## 12. Üretim, tamir ve kaynak ekonomisi

### 12.1. Tarifler

| Tarif | Girdi | Çıktı | Yer / süre | Aşama |
|---|---|---|---|---|
| Bandaj | 2 kumaş | 1 bandaj | Elde / 3 sn | M1 |
| Dikkat taşı | 1 taş + 1 kumaş | 1 sarılı taş | Elde / 2 sn | M1; işiten AI gerekir |
| Ahşap sopa | 3 odun + 1 bant | 1 sopa | Elde / 4 sn | M1; yakın dövüş gerekir |
| Tamir kiti | 3 hurda + 1 bant | 1 kit | Tezgâh / 6 sn | M2; barikat onarımı gerekir |
| Barikat | 5 odun + 2 hurda | 1 barikat kiti | Tezgâh / 8 sn | M2 |
| Sandık | 6 odun + 2 hurda | 1 sandık kiti | Tezgâh / 8 sn | M2; 12 ek yuva |

İlk sürümde mühimmat üretimi, silah yapımı ve karmaşık parça ağaçları yoktur. Odun ve taş toplanabilir nesnelerden gelir; ağaç kesme/maden sistemi gerekmez.

### 12.2. Üretim işlemi

Tek aktif üretim vardır. Başlangıçta girdiler ayrılır; tamamlanana kadar gerçekten tüketilmez. Çıktı kapasitesi girdiler tüketildikten sonraki durum üzerinden hesaplanır. Girdinin boşalttığı yuva çıktı için kullanılabilir.

Tamamlanma; girdileri tüketme ve çıktıyı ekleme açısından bölünmez tek işlemdir. Çift tıklama ikinci işlem başlatmaz. İptal, ölüm, menü kapanışı, sahne değişimi veya gereken tezgâhtan uzaklaşma girdileri serbest bırakır ve çıktı vermez. Çanta kapasitesi tamamlanmada yeniden doğrulanır; başarısızsa girdiler korunur.

Kayıt istendiğinde devam eden üretim önce güvenle iptal edilir, sonra kararlı durum yazılır. İlk sürüm üretim kuyruğunu kaydetmez. Envanter/üretim açıkken dünya devam eder; oyuncu tehlikeli yerde üretimin riskini görür.

Arayüz eksik kaynağı miktarıyla gösterir: “Kumaş 1/2 — 1 eksik”. Tezgâh ve kapasite engelleri ayrı açıklanır. İşlevi henüz uygulanmamış tarif görünürde çalışıyor gibi sunulmaz.

### 12.3. Onarım

M2'de kit yalnız barikata uygulanır. Yeni öneri: 300 azami canı olan barikata 100 yapı canı ekler; üst sınırı aşmaz, tam canda tüketilmez. Hedef hasarlı ve geçerli kurulum noktasında olmalıdır. Yok edilmiş yapıyı onarmak yeniden inşa yerine geçmez. Silah dayanıklılığı eklenmeden kitin silah tamir ettiği söylenmez.

### 12.4. Kaynak kaynakları ve tüketimler

| Kaynak | Başlıca buluntu alanı | Başlıca tüketim |
|---|---|---|
| Kumaş | Ev, istasyon, sağlık dolabı | Bandaj ve dikkat taşı |
| Hurda | Atölye, araç çevresi, servis alanı | Yapı, sandık, tamir |
| Odun | Kulübe, palet, belirli toplanabilir nesne | Sopa ve yapılar |
| Bant | İstasyon ve hırdavat | Sopa ve tamir |
| Taş | Yol kenarı/dere üzerindeki işaretlenebilir nesneler | Dikkat taşı |
| Mühimmat | Kontrollü ikmal kapları | Çatışma |

Kumaşın hem sağlık hem dikkat dağıtma için kullanılması anlamlı tercihtir; ilk tarif öğretimini kilitleyecek kadar kıt olmamalıdır. İlk istasyonda en az bir bandaj tarifini karşılayan erişilebilir kumaş garantilenir.

İlk görev kaynak önerisi: tabancada 12 + 36, depoda 12 ek mermi. 10 standart zombi için 30 gövde isabeti gerekir; %60 isabet varsayımı yaklaşık 50 atıştır. Bu yalnız tasarım hesabıdır. Kaçılabilir karşılaşma ve ek ikmal, düşük isabetli oyuncunun tamamen kilitlenmesini önlemelidir.

## 13. Güvenli merkez ve üs kurma

### 13.1. Güvenli merkezin işlevi

Hazırlık, depo, tezgâh, görev sonucu ve kayıt için sabit merkezdir. Elektriğin gelmesi ilk görünür yükseltmedir. M1'de iç güvenli odanın oyuncu üzerindeki baskıyı kesmesi gerekir; dış avlu aynı mutlak güvenlik statüsünde olmak zorunda değildir. Düşmanlar kapalı iç odaya doğmaz. Açık kapı ve takip davranışı ayrıca test edilir.

### 13.2. Gelişim basamakları

| Basamak | Açılan imkân | Gerekli içerik |
|---|---|---|
| Başlangıç | Acil ışık, temel depo, çıkış | Mevcut güvenli oda |
| Elektrik | Ana ışık, dış kapı, tezgâh erişimi | Sigorta görevi |
| Depolama | Ek sandık | Malzeme toplama ve sandık kiti |
| Savunma | Belirli yuvada barikat | Yapı canı, zombi engel davranışı, tamir |
| İleri hazırlık | Yeni tezgâh/istasyon | M3 görev ve kaynak ilerlemesi |

### 13.3. M2 yapı sistemi

Yalnız önceden tanımlanmış kurulum yuvaları kullanılır. Oyuncu hayalet önizleme ve gerekli kit bilgisini görür. Geçerli yerleştirmede kit tüketilir ve tek yapı oluşur; geçersiz durumda tüketilmez. Zorunlu görev yolu, kayıt noktası ve son kaçış rotası kapatılamaz.

Barikatın düşman rotasını değiştirmesi veya saldırılabilir engel olması açık biçimde uygulanır. Yol bulma güncellenmiyorsa barikat geçiş kapatan bir özellik olarak yayımlanmaz. Hasar, tamir, yıkım ve yükleme aynı kimliği kullanır.

İlk sürümde sökme/taşıma yoktur; kullanıcıya yerleştirmeden önce belirtilir. Daha sonra eklenirse dolu sandığın içeriği kaybolmaz ve tekrar söküp kurma kaynak üretmez.

### 13.4. Serbest inşa — gelecek aday

Temel, duvar, kapı, çatı, destek/çökme, sahiplik, arazi eğimi, çakışma, yapı sınırı ve ağ eşitleme ayrı tasarım gerektirir. M2 yuvalı sistem kanıtlanmadan bu kapsama geçilmez. Otomatik çevrimdışı üs çürümesi, zorunlu günlük bakım ve PvP baskını mevcut ürün kararı değildir.

## 14. Dünya, harita ve keşif

### 14.1. Dünya kimliği

Terk edilmiş küçük sanayi ile kırsalın birleşimi: atölyeler, istasyon, servis yolları, çamlık tepeler, dere, kulübe ve uzak su kulesi. Türkiye'den esinlenme öneridir; şehir, tarih ve salgın nedeni henüz kanonlaştırılmamıştır.

Mekânlar kaynak ve rota açısından farklılaşır. Her binanın içi açılmaz. Kullanılabilir girişler ışık, açıklık, tabela ve çevresel ipuçlarıyla ayırt edilir.

### 14.2. İlk bölge

İlk üretim 150 × 150 m'lik avlu–yol–loot parçasıdır. İlk tam bölge yaklaşık 500 × 500 m'dir. Aşağıdaki koordinatlar tam bölge taslağıdır; hepsi 150 m prototipe sığdırılmaz. Küçük parçada istasyon işlevi geçici yakın loot noktasıyla temsil edilebilir.

| Odak | Taslak merkez X/Z, metre | İşlev |
|---|---:|---|
| Sanayi avlusu | 80 / 80 | Görev, tezgâh, depo, çıkış |
| Akaryakıt istasyonu | 245 / 155 | İlk kaynak kararı; ön/arka bağlantı |
| Orman bakım kulübesi | 145 / 300 | Odun, kumaş ve alternatif rota |
| Köprü/dere | 310 / 280 | Rota birleşimi; alternatif sığ geçit |
| Su kulesi ve evler | 390 / 390 | Yön bulma işareti ve ikinci sefer hedefi |

Ana yol avlu → istasyon → köprü → su kulesi; yan rota avlu → kulübe → sığ geçit → köprü. En az iki dönüş seçeneği bulunur. Tek zorunlu dar boğaz oyuncuyu bütün zombileri öldürmeye zorlamaz.

### 14.3. Terrain ve okunabilirlik

Yükselti önerisi 25–45 m; sık kullanılan patikalar için ilk eğim denemesi yaklaşık 5–12°'dir. Gerçek yürünebilirlik kontrolcü ve AI ile doğrulanır. Toprak, çakıl, seyrek çimen ve kaya ana yüzey aileleridir. Asfalt geçişleri ve bina tabanları elle düzeltilir.

Doğal sırtlar görüşü kırar; landmark'lar tamamen kaybolmaz. Çimen loot'u, kaya kapıyı, dekor kaçış yolunu örtmez. Harita sınırı çevreyle anlatılır; düşme/sıkışma için güvenli kurtarma bulunur. Sığ geçit yüzme gerektirmez.

### 14.4. Harita ve yön bulma

M1'de pusula ve dünyadaki işaretler yeterlidir; sürekli minimap gerekmez. M2 için basit bölge haritası önerilir: keşfedilen mekân adı, bilinen görev hedefi ve oyuncu işareti. Keşfedilmemiş loot/düşman konumu otomatik gösterilmez. Hedefin konumu bilinmiyorsa tam koordinat yerine araştırma alanı verilir.

### 14.5. Gün/gece, hava ve ışık

M1–M2: sabit, aydınlık bulutlu öğleden sonra. M3 deneyi: gündüz/gece geçişi ve sınırlı yağmur. Gece yalnız ekranı karartmak değildir; fener, düşman algısı, sığınak ve yön bulma birlikte tasarlanır. Hava değişimi performans ve okunabilirlik sınırları içinde kalır.

Fener eklenirse aç/kapa, dünya ışığı, gölge maliyeti ve kayıt gerekir. Pil tüketimi ayrı kaynak sistemi olarak ancak anlamlı karar üretiyorsa eklenir. Işıkların açık olması sigortadan sonra kalıcı dünya durumu olarak kaydedilir.

## 15. Görevler, hikâye ve öğretici

### 15.1. Hikâye yaklaşımı

Önce çevrede görülebilen küçük insan amaçları: elektriği geri getirmek, malzeme bulmak, güvenli geçit açmak. Notlar, yarım kalmış işler ve radyo bu amaçları bağlar. İlk teslimde uzun sinematik, dallanan diyalog ve zorunlu seslendirme yoktur.

Ana karakter adı, salgın kaynağı ve büyük hikâye sonu açık karardır. Tasarımın işlemesi için bunların şimdi uydurularak kesinleştirilmesi gerekmez.

### 15.2. Görev 01 — İlk İkmal

1. Güvenli odada sigorta ihtiyacı anlaşılır; başlangıç tabancası ve kaynaklar alınır.
2. Ana sokaktan depoya gidilir; eczaneye sapmak isteğe bağlıdır.
3. Sigorta alınır; ayrı görev alanına eklenir.
4. Ana sokak veya yan geçitten servis avlusuna dönülür.
5. Sigorta panoya takılır; görev tek kez tamamlanır.
6. Pano/oda/tezgâh ışıkları açılır ve dış bölge kapısının kilidi çözülür.
7. Yeni hedef “Eski istasyonu araştır” olarak sunulur; oyuncu önce depoyu kullanabilir.

Durumlar: başlamadı → sigorta aranıyor → sigorta taşınıyor → elektrik açıldı/tamamlandı. Işık ve kapı durumları bu sonuçla tutarlı olmalıdır. “Odaya dön” gerekiyorsa ayrı yönlendirmedir; ana görevin yeniden tamamlanmasını sağlamaz.

### 15.3. Görev 02 — İlk Üretim

İstasyonu keşfet → yeterli kumaş bul → bandaj tarifini öğren → bandaj üret → güvenli merkeze dön. Oyuncunun üretimden önce hasar alması veya bandajı boşa tüketmesi şart değildir. Üretimi daha önce yaptıysa başarı geçmişi üzerinden koşul tamamlanır; yeniden üretmeye zorlanmaz.

### 15.4. Görev 03 — Yeni Geçit

M2 önerisi: kulübedeki anahtarı veya araç parçasını bul → kısa bağlantıyı aç → dönüş avantajını kullan. Ödül yalnız sayı artışı değil, haritada kalıcı bir kolaylıktır. Açılan geçit görev iptalinde veya yüklemede kendiliğinden kapanmaz.

### 15.5. Görev sistemi kuralları

Görev tanımı, aşama, koşul, ödül ve dünya sonucu ayrı izlenir. Yükleme veya olay tekrarı ödülü çoğaltmaz. Bir hedefin ön koşulu önceden gerçekleştiyse görev bunu tanır. Kritik eşya kaybolmaz; bozulmuş/eski kayıt için görev durumunu onaran doğrulama gerekir.

### 15.6. Durumsal öğretici

Etkileşim ipucu, durum yardımı ve görev satırı farklı sunumlardır. Aynı anda en fazla bir öğretici kartı ve bir görev satırı görünür. Öncelik: kritik durum → geçerli etkileşim → ilk kullanım → yön yardımı.

| Koşul | Yardım | Başarı/kapanış |
|---|---|---|
| Güvenli odada hareketsizlik | Hareket tuşları | Oyuncu yürüdüğünde |
| Erişilebilir kapı/eşya | Güncel etkileşim tuşu + fiil | Başarı veya hedef kaybı |
| Boş şarjör, rezerv var | Doldur | Doldurma başlayınca |
| Şarjör ve rezerv boş | Uzaklaş / kullanılabilir silaha geç | Durum değişince |
| Düşük can, bandaj, uygun an | Bandaj kullan | Kullanım başlayınca |
| Zombi gerçekten silah sesini araştırdı | Gürültünün etkisi | Kısa gösterimden sonra |
| Çatışma dışında uzun yön kaybı | Erişilebilir geçide kısa yön yardımı | Rota ilerleyince |

Kartın gösterilmesi öğrenme sayılmaz; başarılı eylem öğrenme durumunu tamamlar. İlk öneri 4–6 sn gösterim, en erken 20 sn sonra en fazla iki hatırlatmadır. Normal etkileşim etiketi bu kotaya tabi değildir. Öğretici kapalıyken görev bitirilebilir olmalıdır.

## 16. İlerleme ve tekrar oynanabilirlik

### 16.1. İlk ilerleme modeli

M1–M2'de deneyim seviyesi yerine işlev açılır: elektrik, dış kapı, depo kapasitesi, tezgâh, tarif, kısa yol. Oyuncunun kişisel gelişimi; rota bilgisi, daha iyi nişan, gürültü yönetimi ve kaynak tercihidir.

Tarifler ilgili eşya, görev veya tezgâh üzerinden açılır. İlk bandaj, sopa ve dikkat taşı öğretiminde gerekli keşif koşulları garantilenir. Rastgele tarif düşmesi ilerlemeyi kilitlemez.

### 16.2. Sonraki ilerleme — yeni öneri

M3'te küçük uzmanlıklar denenebilir: daha verimli üretim, taşıma kolaylığı, daha hızlı bakım. Büyük düz hasar bonuslarıyla başlangıç silahını anlamsızlaştıran bir ağaç önerilmez. Yetenek puanı, sınır, yeniden dağıtma ve co-op dengesi birlikte belirlenmeden XP sistemi eklenmez.

### 16.3. Tekrar oynanabilirlik ve ileri hedefler

Önce isteğe bağlı loot ve düşman yerleşiminde sınırlı varyasyon; sonra kaynak kıtlığı, yeni rota, yeni görev ve bölgesel olaylar denenir. Uzun vadeli hedef önerileri: merkezi tam çalışır hâle getirmek, uzak bir haberleşme noktasını onarmak ve yüksek riskli bir bölgeden özel bileşen çıkarmak.

İlk bölüm bir oturum sonunda tamamlanabilir; sınırlı içeriği sonsuz döngü gibi sunmak gerekmez. İlk parçada kaynaklar yenilenmiyorsa kalan serbest keşfin sonlu olduğu kabul edilir. Uzun vadeli kaynak yenilenmesi çözülmeden sınırsız survival iddiası yapılmaz.

### 16.4. İsteğe bağlı büyük sistem adayları

| Aday | Gerekli ön koşul | Karar |
|---|---|---|
| NPC ve tüccar | Ticaret ihtiyacı, fiyat dengesi, davranış/arayüz | M3 sonrası deney; ilk bölgeye şart değil |
| Fraksiyon/itibar | Birden çok ilişki ve anlamlı sonuç | Ana hikâye kapsamı netleşince |
| Araç | Yaya seyahatin ölçülmüş sorunu, yeterli dünya ölçeği | 500 m bölge için zorunlu değil |
| Tarım | Uzun oturum kaynak döngüsü, zaman ve kayıt | Beslenme dengesi sonrası |
| Ağaç kesme/maden | Kaynak toplama ritmi ve dünya kalıcılığı | Hazır toplama düğümleri yetersizse |
| Boss ve özel bölge | Temel çatışma, uygun animasyon ve arena | Yeni rol ihtiyacı varsa |
| PvP ve baskın | Ayrı denge, güvenlik, dünya sahipliği ve işletim | Mevcut hedef dışında; ayrı ürün kararı |

## 17. Ölüm, yeniden deneme ve zorluk

### 17.1. İlk sürüm ölüm akışı

M0 testinde ölüm görevi tutarlı başlangıç durumuna döndürebilir. M1'den itibaren öneri: son geçerli kontrol noktasını yüklemek. Oyuncu, çanta, depo, loot, görev, kapı ve yapı durumu aynı anlık görüntüden geri gelir. Yalnız oyuncuyu geri sarıp dünyadaki tüketimi korumak kabul edilmez.

Ölümde üretim/kullanım iptal edilir, girdi kapatılır, ölüm ekranı açılır. “Kontrol noktasından devam” ve “Ana menü” bulunur. Ölüyken yeni otomatik kayıt son sağlıklı kaydı ezmez. İlk sürümde cesetten eşya kurtarma veya kalıcı karakter kaybı yoktur.

### 17.2. Kontrol noktaları ve kayıp

Yeni oyun başlangıcı, elektrik açılması ve güvenli merkeze sağlıklı dönüş önerilen otomatik kayıt noktalarıdır. Dönüş kaydı, merkezdeki transfer ve üretim işlemleri bittikten sonra kararlı durumda alınır. Manuel kayıt güvenli merkezde sunulur. Son kayıt zamanı görünür; oyuncu ölürse ne kadar ilerlemenin geri alınacağını anlayabilir.

### 17.3. Gelecek ölüm modeli

M3'te dünyada eşya çantası bırakma denenirse: tek ölüm çantası mı çoklu mu, süre, görev eşyası muafiyeti, ulaşılamayan çanta kurtarma, co-op erişimi ve kayıt kuralları birlikte tanımlanır. Bu model M1 kontrol noktası modeliyle aynı anda uygulanmaz.

### 17.4. Zorluk

İlk test tek Standart profil kullanır. Sonraki Kolay profil; daha düşük gelen hasar, daha cömert ikmal ve daha yavaş ihtiyaç tüketimi sağlayabilir. Zor profil eklenirse yapay zekâyı her şeyi bilen hâle getirmez; kaynak ve tolerans üzerinden değişir. Kesin çarpanlar oyuncu testinden sonra belirlenir.

## 18. Kayıt ve dünya kalıcılığı

### 18.1. Kaydedilecek veriler

| Alan | Veri |
|---|---|
| Kimlik | Kayıt sürümü, dünya kimliği, kayıt zamanı, build bilgisi |
| Oyuncu | Güvenli konum, bakış, can, etkin ihtiyaçlar |
| Eşyalar | Çanta, ekipman, şarjörler, hızlı yuvalar, görev alanı |
| Merkez | Depolar, yapılar, yapı canı ve içerikleri |
| Görev | Aktif/tamamlanan aşamalar, tek seferlik ödül kayıtları |
| Dünya | Alınmış loot kimlikleri, kapılar, pano/ışık, açılmış rotalar |
| Düşman | Kalıcı yerleşim kimliği, canlı/ölü durumu; gerekiyorsa can ve güvenli konum |
| İlerleme | Tarifler, keşfedilen yerler, öğretici başarıları |
| Sonraki sistemler | Dünya zamanı ve yeniden doğma zamanları yalnız etkinse |

İlk sürümde canlı zombilerin hassas animasyon anı kaydedilmez; yüklemede güvenli bir davranış durumuna dönerler. Öldürülmüş sabit zombi kendiliğinden canlanmaz. Dinamik düşmanların politikası M3 nüfus sisteminde belirlenir.

### 18.2. Güvenilirlik kuralları

Yazım sırasında eski geçerli kayıt korunur; geçici dosya doğrulanıp başarıyla tamamlandığında yeni kayıt devreye girer. En az bir önceki sağlam yedek tutulur. Kayıt yok, bozuk, eksik veya eski sürümse anlaşılır hata/geri dönüş sunulur; bozuk veriyi sessizce yeni oyunla ezme yapılmaz.

Nesne kimlikleri sahne adından veya anlık sıra numarasından türetilmez. Sahne değişince aynı loot/yapı farklı eşya sanılmaz. Görev, envanter ve dünya durumu tek tutarlı kayıt işlemiyle yazılır.

Ayarlar oyun kaydından ayrıdır; yeni oyunda hassasiyet/erişilebilirlik sıfırlanmaz. İlk kayıt modeli yereldir. Bulut kayıt ve co-op dünya sahipliği ayrı doğrulama gerektirir.

**Kabul:** Görevin her aşamasında; üretim sırasında; depo transferinden sonra; öldürülmüş zombi ve alınmış loot yanında; eski/bozuk kayıtla yükleme denenir. Çift eşya, kayıp sigorta ve silinmiş ilerleme oluşmamalıdır.

## 19. Arayüz, menüler ve erişilebilirlik

### 19.1. HUD

Sol üstte tek aktif hedef, üst ortada ince pusula, sol altta can ve varsa stamina, sağ altta mühimmat, alt ortada dört hızlı yuva. Merkez ve düşman siluetleri açık kalır. Etkileşim yalnız geçerli hedefte görünür. Açlık/enfeksiyon gibi uygulanmamış sistem göstergeleri eklenmez.

### 19.2. Gerekli ekranlar

| Ekran | Asgari içerik |
|---|---|
| Ana menü | Devam, yeni oyun, ayarlar, çıkış; kayıt yoksa Devam kapalı |
| Yeni oyun | Profil/dünya bilgisi; mevcut kaydı ezme konusunda açık seçim |
| Duraklatma | Devam, ayarlar, uygun yerde kaydet, ana menü |
| Envanter/üretim | 6 × 4 çanta, ekipman, tarifler, malzeme, çıktı, süre, engel nedeni |
| Depo | İki tarafın kapasitesi ve aktarım miktarı |
| Görev günlüğü | Aktif hedef, önceki sonuç, bilinen konum |
| Ölüm | Son kayıt bilgisi, yeniden deneme, ana menü |
| Yükleme/hata | İşlem durumu, tekrar deneme ve güvenli geri dönüş |
| Sonraki co-op | Lobi, davet, oyuncular, bağlantı durumu, ayrılma |

### 19.3. Görsel dil

| Rol | Renk önerisi |
|---|---|
| Koyu mat panel | `#171D1B` |
| Ana yazı | `#ECE9DF` |
| İkincil yazı | `#ADB5AA` |
| Aktif eylem / vurgu | `#D7A44C` |
| Sağlık / uygun | `#91A77A` |
| Kritik / eksik | `#CF7061` |

Amber zeminde koyu yazı kullanılır. Eskime efekti metnin arkasına taşınmaz. Renk tek başına durum anlatmaz; ikon/metin eşlik eder. Kumaş katlanmış parça, bandaj bağlı rulo olarak ayrıştırılır.

### 19.4. Davranış ve erişilebilirlik

Menüde imleç serbest, kamera/ateş/hareket girdileri engellidir. Envanter açıkken dünya çalışır; tek oyunculu Esc menüsü duraklatır. Co-op'ta kişisel menü dünyayı durdurmaz ve bu açık anlatılır. Odak dönüşü güvenli olmalıdır.

UI/metin ölçeği, Türkçe karakter desteği, görünür klavye odağı, sürükleme dışında taşıma, yüksek kontrast, altyazı ve önemli seslerin görsel karşılığı gerekir. FOV, sarsıntı, headbob, sway ve hareket bulanıklığı tercihleri korunur. Öğretici açık/azaltılmış/kapalı ve sıfırlama seçenekleri sunulur.

1280 × 720, 1920 × 1080, 2560 × 1440 ve 21:9 görünüm; %125/%150 metin ölçeği kontrol edilir. Metin taşması, Türkçe karakter ve yeniden atanmış tuşların görünümü test edilir. Türkçe geliştirme dili; İngilizce yayın adayıdır. Metinler kod içine dağılmaz, yerelleştirme anahtarlarından gelir.

## 20. Sanat, animasyon, ses ve efektler

### 20.1. Görsel hedef

Gerçekçiliğe yakın, tutarlı ve okunabilir dünya. Solmuş yeşil kapı, yıpranmış beton, mat metal, yerel asfalt yamaları, çamlık ve sıcak çalışma ışıkları ortak dil oluşturur. Her yüzey aşırı paslı, parlak veya ıslak yapılmaz. Gölge detayları korunur.

Önce tek yol kesiti, tek iç mekân ve tek düşman hedef kaliteye getirilir. Sonra aynı standart çoğaltılır. Önceki videodaki eski eller hakkındaki eleştiri, değişmiş yeni el modeline doğrulamadan uygulanmaz.

### 20.2. Animasyon kapsamı

| Varlık | Gerekli hareketler |
|---|---|
| FPS eller/tabanca | Bekleme, yürüme, koşu, ADS giriş/çıkış, ateş, doldurma, çıkarma/indirme |
| Sopa | Hazırlık, vuruş, toparlanma, taşıma |
| Kullanım | Bandaj ve taş fırlatma için anlaşılır sunum |
| Standart zombi | Bekleme, dolaşma, takip, saldırı hazırlığı, saldırı, toparlanma, hafif/ağır tepki, ölüm |
| Dünya | Kapı, pano, ışık ve yapı durum geçişleri |

El–silah tutuşu, şarjör teması, bilek deformasyonu, kamera kesişimi ve ADS hizası gerçek oyun kamerasında kontrol edilir. Animasyon mantığın sonucunu gösterir; kritik oyun durumu yalnız animasyon olayına bağımlı kalmaz.

### 20.3. Ses

Atış, boş tetik, doldurma aşamaları, isabet yüzeyleri, zombi hazırlık/saldırı/ölüm, oyuncu hasarı, yüzeye göre adım, eşya alma, üretim, kapı, pano, elektrik ve bölgesel ambiyans gerekir. Yakın tehdidin yönü okunur. Sesler aynı anda çalınca kritik tehdit kaybolmaz.

Oyuncunun duyduğu ses ile AI'ın gürültü olayı ortak oynanış eyleminden çıkar ama ayrı işlevlerdir. Sesi kullanıcı ayarından kısmak zombinin işitmesini değiştirmez. Müzik gerilimi destekler, düşman sesini sürekli örtmez. Ana, efekt, müzik ve arayüz ses düzeyi ayrı ayarlanır.

### 20.4. Efekt ve varlık kabulü

Namlu parlaması, kovan, yüzeye göre isabet, sınırlı kan ve çevresel toz başlangıç kapsamıdır. Tekrarlanan efektler havuzlanabilir; süre sonunda temizlenir. Cesetlerin görsel ömrü ile zombi ölümünün kalıcı kaydı ayrıdır.

Her varlık için kaynak/lisans, ölçek, malzeme, çarpışma, LOD ihtiyacı, animasyon uyumu ve performans kontrolü kaydedilir. Konsept görsel hazır oyun modeli veya işleyen UI değildir. Ticari hakları belirsiz varlık yayın paketine girmez.

## 21. Co-op ve ağlı oyun

### 21.1. Ürün önerisi

Önce iki oyunculu teknik prototip; başarılı olursa 2–4 kişilik co-op aday kapsamı. Bu sayı yeni öneridir. M1 için sunucu, hesap sistemi ve eşleştirme zorunlu değildir.

### 21.2. Yetki ve eşitleme

Hasar, envanter, loot, üretim, görev, yapı ve dünya kaydı yetkili oturum tarafında doğrulanır. İstemci “bu eşyayı aldım” sonucu yerine alma isteği gönderir. Aynı eşyayı iki kişi aynı anda alamaz. Hareket, animasyon ve efekt sunumu gecikme altında ayrıca değerlendirilir.

Yetkili tarafın oyuncu host'u mu ayrı sunucu mu olacağı; maliyet, güvenlik ve devamlılık ihtiyacıyla seçilir. Paket/servis seçimi bu GDD'de sabitlenmez.

### 21.3. Gerekli co-op sistemleri

- Lobi, davet, katılma, bağlantı hatası ve oturumdan ayrılma.
- Sonradan katılana görev/dünya/loot durumunun eksiksiz aktarılması.
- Kopan oyuncunun envanter sahipliği ve yeniden bağlanma politikası.
- Ortak görev, kişisel çanta, ortak depo ve eşya alma yarışı.
- Yere düşme/canlandırma; tam ölüm ve yeniden doğma kararı.
- Dost ateşi tercihi; başlangıç önerisi kapalı.
- Oyuncu işareti ve konum bildirimi; sesli sohbet ancak gerekli kapsamla.
- Oturum sahibi ayrıldığında kayıt ve kapanış. İlk prototipte host devri zorunlu değil; yoksa oyuncuya açık bildirilir.
- Uygunsuz davranış için çıkarma/engelleme; herkese açık eşleştirme varsa moderasyon planı.

### 21.4. Kalıcılık önerisi

İlk prototipte dünya ve o dünyadaki oyuncu envanterleri oturum sahibinin kaydına bağlıdır. Dünyalar arası eşya aktarımı kapalıdır. Bu, ekonomik çoğaltma ve sahiplik belirsizliğini azaltan yeni öneridir; nihai servis kararı değildir.

**Kabul:** İki oyuncunun aynı loot'u alması, eşzamanlı üretim/transfer, gecikmeli hasar, katılma/kopma, host kapanışı ve kayıt sonrası dönüş denenir. 50/100/200 ms gecikme ve kontrollü paket kaybı senaryoları ölçüm önerisidir, yapılmış test değildir.

## 22. Ekonomi, Steam ve canlı işletim

### 22.1. Oyun içi ekonomi

İlk ekonomi eşya ve malzeme takası olmadan kaynak giriş–tüketim dengesi üzerinden kurulur. Para birimi, tüccar ve oyuncu pazarı gerekmez. Sefer başına bulunan/harcanan mühimmat, bandaj, yapı malzemesi ve boş kalan kapasite ölçülür.

Tüccar eklenirse sınırlı stok, alış/satış farkı, ilerleme kilidi, tekrar satış ve sonsuz para döngüsü ayrı test edilir. Üretip satmak, satın alınan girdiden sınırsız kâr doğurmamalıdır; özellikle istenen bir sistemse sınırı açıklanır.

### 22.2. F2P ürün önerisi

Gelir adayları görsel silah kaplaması, kıyafet ve merkez dekorudur. Yeni tasarım ilkesi olarak hasar, kapasite, loot şansı, üretim süresi ve ihtiyaç tüketimi satın almayla iyileştirilmez. Görsel ürünler düşman okunabilirliğini veya oyuncunun saklanmasını avantajlı hâle getirmemelidir.

M1–M3'te ödeme mağazası yapılmaz. Önce oyun döngüsü ve işletim maliyeti doğrulanır. F2P'nin sürdürülebilirliği, mağaza kapsamı, fiyatlar ve co-op hizmet maliyeti M5 ürün kararıdır. Ücretli rastgele ödül kutusu veya sezon bileti mevcut kapsam değildir.

### 22.3. Yayın için gerekli işler

Steam build/dağıtım akışı, mağaza içeriği, desteklenen diller, başarımlar, varsa bulut kayıt, çevrimdışı davranış, hata raporu, güncelleme/geri dönüş planı ve destek kanalı tanımlanır. Başarımlar gerçek ilerlemeyi izlemeli; tekrar yükleme ödülleri çoğaltmamalıdır.

Ödeme eklenirse hak sahipliği, satın alma sonucu, iptal/iade sonrası durum ve servis kesintisi ele alınır. Güncel platform gereklilikleri, yaş derecelendirmesi, veri/mahremiyet ve yayın yükümlülükleri uygulama aşamasında resmî kaynaklarla ayrıca doğrulanır; bu belge güncel mevzuat veya platform kuralı beyanı değildir.

### 22.4. İşletim ve güncelleme

Test dağıtımı ile genel sürüm ayrılır. Her sürümün değişiklik notu, kayıt uyumluluğu ve bilinen sorunları tutulur. Kritik kayıt/çoğaltma hatasında dağıtımı durdurma ve önceki sürüme dönme prosedürü gerekir. Oyuncu verisi toplanıyorsa amacı, kapsamı ve tercihler açık sunulur; gereksiz kişisel veri alınmaz.

## 23. Teknik sistemler ve performans

### 23.1. Sorumluluk sınırları

| Sistem | Sorumluluk |
|---|---|
| Girdi | Oyuncu eylemleri ve menü odağı |
| Oyuncu | Hareket, sağlık, kullanım ve aktif durum |
| Silah/hasar | Atış, engel kontrolü, mühimmat ve tekil hasar |
| AI | Algı, hedef, yol, saldırı ve tepki |
| Etkileşim | Hedef seçme, erişim ve işlem isteği |
| Eşya/envanter | Tanım, sahiplik, miktar, kapasite ve transfer |
| Üretim/yapı | Girdi ayırma, tamamlanma, yerleşim ve onarım |
| Görev/dünya | İlerleme koşulları ve kalıcı çevre sonuçları |
| Kayıt | Sürümlü tutarlı durum, yedek ve yükleme |
| Sunum | UI, ses, efekt, animasyon ve yerelleştirme |

Bu sınırlar yeni bir genel framework kurma talebi değildir. Mevcut yapıya uyan küçük bileşenlerle uygulanır. Aynı envanter veya hasar bilgisinin birden fazla bağımsız sahibi olmaz. Simülasyonun girdi ve sunumdan ayrılması gelecekte co-op'a yardım eder; hazır ağ desteği anlamına gelmez.

### 23.2. Veri ve araçlar

Silah, düşman, eşya, tarif, loot tablosu ve görev verileri düzenlenebilir tanımlarda tutulur. Kimlik tekrarı, eksik ikon/model, geçersiz tarif ve bulunamayan görev referansları için basit doğrulama aracı gerekir. Geliştirici paneli can, kaynak, görev aşaması ve AI durumunu görmeye yardım edebilir; yayın sürümünde yetkisiz avantaj yaratmaz.

### 23.3. Dünya yükleme

İlk 500 m bölge için streaming zorunlu kabul edilmez; bellek ve yükleme ölçülür. Daha büyük dünyada bölgesel yükleme, kalıcı nesne kimliği, komşu bölge navigasyonu ve kayıt sınırları ayrı prototipte doğrulanır. Görsel nesnenin yükten çıkarılması loot veya düşmanın kalıcı durumunu sıfırlamaz.

### 23.4. Performans hedefi

Belirlenecek referans PC'de 1080p/60 FPS ve 30 aktif zombi hedeflenir. 60 FPS yaklaşık 16,7 ms kare bütçesidir. Referans işlemci, ekran kartı, RAM, depolama ve grafik ayarı henüz belirlenmediği için hedef gerçekleşmiş veya sistem gereksinimi ilan edilmiş değildir.

Windows standalone build'de sabit rota ve koşullar kullanılır. 10 ve 30 aktif zombi olağan ölçümler; 100 stres senaryosudur, 60 FPS geçiş zorunluluğu değildir. CPU/GPU kare zamanı, ortalama/%1 düşük FPS, bellek, yükleme süresi ve takılmalar raporlanır.

Optimizasyon sırası ölçülen darboğaza göre belirlenir: görünürlük, LOD, gölge, bitki yoğunluğu, AI güncellemesi, fizik, havuzlama ve kaynak ömrü. Ölçüm olmadan ECS/Jobs/Burst veya pipeline değişimi zorunlu sayılmaz.

### 23.5. Proje sağlığı

Gerçek editör/paket sürümleri proje dosyalarından kaydedilir. Mevcut `.meta`, sahne ve prefab referansları korunur. Derleme, Windows build, temel açılış ve kayıt uyumluluğu her önemli teslimin kapısıdır. Paket lisansı ve varlık kaynağı üretim kaydına eklenir.

## 24. İçerik üretim listesi

### 24.1. M1 asgari içerik

| Alan | Teslim |
|---|---|
| Oyuncu | Bir uyumlu FPS el/kol seti, tabanca, sopa, bandaj ve taş sunumu |
| Düşman | Bir davranış ailesi; sınırlı görünüş/hareket varyasyonu |
| Eşya | 5 ham kaynak, bandaj, dikkat taşı, sopa, tabanca mermisi, görev sigortası |
| Mekân | Güvenli oda/avlu, görev deposu, kısa dış rota, ilk loot noktası |
| Etkileşim | Kapı, pano, loot nesnesi, kap, tezgâh ve sabit depo |
| Görev | İlk İkmal ve ilk üretim yönlendirmesi |
| UI | HUD, durumsal ipucu, envanter/üretim, depo, ayarlar, ölüm/yükleme |
| Ses/VFX | Tek tabancanın ve standart zombinin tam geri bildirim seti |
| Teknik | Kayıt, ayarlar, test sahnesi, Windows test build'i |

### 24.2. M2 ilaveleri

Beş odaklı ilk bölge; kulübe, köprü ve su kulesi; üç tezgâh tarifi; barikat ve sandık; kısa yol görevi; gerekçelendirilirse pompalı; bölge haritası; hedef kalitede çevre, ses ve ışık. Koşucu ve stamina otomatik zorunlu değil, ayrı deneme kalemleridir.

### 24.3. İçerik teslim şablonu

Her varlık veya sistem teslimi: ad/kimlik, amaç, aşama, bağımlılık, kaynak/lisans, geçici/nihai durumu, oynanış davranışı, görsel/ses ihtiyacı, kabul kontrolü ve bilinen sorun içerir. “Model var” veya “ekran çizildi” işlevli sistemin tamamlanması sayılmaz.

## 25. Aşamalı yapılacak işler

**Öncelikler:** P0 ilgili aşamanın çıkışını engeller; P1 o aşamanın önemli iyileştirmesidir; P2 sonraki adaydır. P0 tüm oyunun hemen yapılacağı anlamına gelmez. Aşağıdaki kutular uygulama durumu teyit edilene kadar açıktır.

### M0 — Mevcut uygulamayı doğrula

| Durum / ID | İş | Öncelik | Bağımlılık | Tamamlanma ölçütü |
|---|---|---|---|---|
| [ ] ZL-001 | Gerçek Unity projesi, sahne, sürüm, paket ve mevcut sistem envanteri | P0 | — | Doğrulanmış mevcut durum listesi |
| [ ] ZL-002 | FPS hareket, kamera ve girdi düzeltmeleri | P0 | 001 | Eğim/kapı/tavan/ayar testleri geçer |
| [ ] ZL-003 | Tabanca, ADS, isabet, engel ve doldurma | P0 | 001–002 | Mühimmat ve yakın duvar testleri geçer |
| [ ] ZL-004 | Zombi saldırı, hasar, tepki ve ölüm | P0 | 003 | Tek saldırı/ölüm sonucu ve okunabilir tepki |
| [ ] ZL-005 | Tek etkileşim sistemi ve sigorta alma/takma | P0 | 001 | Erişim ve tekrar etkileşim tutarlı |
| [ ] ZL-006 | Elektrik sonucu, iç dönüş yolu ve dış kapı | P0 | 005 | Görev tek kez biter; ışık ve kapı doğru |
| [ ] ZL-007 | Durumsal öğretici, gerçek tuş etiketleri | P1 | 002–006 | Öğretici açık/kapalı tamamlanabilir |
| [ ] ZL-008 | Çatışma/görev Windows doğrulama build'i | P0 | 002–006 | Baştan sona görev ve yeniden deneme |

### M1 — İlk açık parça

| Durum / ID | İş | Öncelik | Bağımlılık | Tamamlanma ölçütü |
|---|---|---|---|---|
| [ ] ZL-101 | 150 × 150 m gri kutu ve iki rota | P0 | 008 | Gidiş/dönüş takılmadan yürünür |
| [ ] ZL-102 | Arazi, kapı ve engellerde AI navigasyonu | P0 | 101,004 | Zombi geçişleri ve sıkışma testi |
| [ ] ZL-103 | Eşya kimliği ve veri tanımları | P0 | 001 | Geçerli, benzersiz eşya/veri seti |
| [ ] ZL-104 | 24 yuva, ekipman, 4 hızlı yuva | P0 | 103 | Kapasite ve sahiplik korunur |
| [ ] ZL-105 | Loot, kaynak yerleşimi, sabit depo | P0 | 101,104 | Alma/transfer çoğaltma yapmaz |
| [ ] ZL-106 | AI işitme, araştırma, hedef kaybı | P0 | 102 | Ateş mevcut düşmanları kaynağa çeker |
| [ ] ZL-107 | Yakın dövüş ve sopa | P0 | 003–004,104 | Tek vuruş, menzil ve engel doğru |
| [ ] ZL-108 | Dikkat taşı fırlatma ve ses | P0 | 104,106 | Bir tüketim, bir düşüş olayı |
| [ ] ZL-109 | Bandaj kullanım/iptal | P0 | 104 | +25, üst sınır, kesilme ve tüketim doğru |
| [ ] ZL-110 | Üretim işlemi ve ilk üç tarif | P0 | 104,107–109 | Ayrılan girdi ve çıktı tek işlem |
| [ ] ZL-111 | HUD, envanter, üretim ve depo ekranları | P0 | 104–110 | İşlevler, engeller ve girdi odağı doğru |
| [ ] ZL-112 | İlk üretim görevi ve garantili kaynak | P0 | 105,110 | Hasar/boşa kullanım gerekmeden tamamlanır |
| [ ] ZL-113 | Sürümlü kayıt, yedek ve yükleme | P0 | 006,104–110 | Eşya, görev ve dünya birlikte korunur |
| [ ] ZL-114 | Kontrol noktası, ölüm ve devam | P0 | 113 | Aynı dünya anına tutarlı dönüş |
| [ ] ZL-115 | İlk parçada ışık, malzeme, ses standardı | P1 | 101,111 | Rota/loot/düşman okunur |
| [ ] ZL-116 | Uçtan uca oynanış ve performans kontrolü | P0 | 101–115 | İlk döngü ve kayıt testi raporlanır |

### M2 — İlk tam bölge

| Durum / ID | İş | Öncelik | Bağımlılık | Tamamlanma ölçütü |
|---|---|---|---|---|
| [ ] ZL-201 | Bölgeyi beş odaklı 500 × 500 m'ye genişlet | P0 | 116 | İki rota döngüsü, işlevli odaklar |
| [ ] ZL-202 | Bölge haritası ve keşif bilgisi | P1 | 201 | Bilinmeyen loot'u ifşa etmez |
| [ ] ZL-203 | Tezgâh, yapı yuvası ve kit yerleştirme | P0 | 110,113 | Geçerli yere tek tüketim/tek yapı |
| [ ] ZL-204 | Barikat canı, AI engeli ve tamir | P0 | 203,102 | Hasar/rota/onarım tutarlı |
| [ ] ZL-205 | Ek sandık ve içeriğin kalıcılığı | P0 | 203,104,113 | 12 yuva ve yükleme testi |
| [ ] ZL-206 | Kısa yol görevi | P1 | 201,113 | Açılan yol kalıcı ve yararlı |
| [ ] ZL-207 | Pompalı silah paketi | P1 | 003,116 | Ayrı rol, saçma ve mühimmat testleri |
| [ ] ZL-208 | Stamina ve koşucu deneyi | P2 | 116,201 | Kaçış seçenekleri korunursa kabul |
| [ ] ZL-209 | Bölge sanat/ses/animasyon geçişi | P0 | 201 | Tek kalite standardı, lisans kaydı |
| [ ] ZL-210 | Dış oyuncu, erişilebilirlik ve profil | P0 | 201–209 etkin kapsam | Test bulguları ve kapı kararı |

### M3 — Survival derinliği

| Durum / ID | İş | Öncelik | Bağımlılık | Tamamlanma ölçütü |
|---|---|---|---|---|
| [ ] ZL-301 | Açlık/susuzluk ve yiyecek/içecek | P1 | 210 | Hazırlık kararı yaratır; erken kilitlemez |
| [ ] ZL-302 | Dünya nüfusu ve loot yenilenmesi | P0 | 113,201 | Yüklemeyle istismar ve görünür doğma yok |
| [ ] ZL-303 | Kaynak giriş/tüketim dengesi | P0 | 302 ve etkin ihtiyaç sistemleri | Uzun seferde kıtlık/bolluk ölçülür |
| [ ] ZL-304 | İleri görevler, tarif ve merkez gelişimi | P0 | 210,303 | Her açılım somut yeni imkân verir |
| [ ] ZL-305 | Gün/gece, fener ve sınırlı hava deneyi | P2 | 209,210 | Rota, algı ve performans korunur |
| [ ] ZL-306 | Yeni zombi/silah rolleri | P1 | 210,303 | Her ekleme farklı karar üretir |
| [ ] ZL-307 | Kanama/enfeksiyon/zırh karar prototipleri | P2 | 301,303 | Sebep, tedavi, kaynak ve UI birlikte |
| [ ] ZL-308 | Uzun oturum ve kayıt uyumluluğu testi | P0 | Etkin M3 sistemleri | Ekonomi, bellek ve kayıt bozulmaz |

### M4 — Co-op

| Durum / ID | İş | Öncelik | Bağımlılık | Tamamlanma ölçütü |
|---|---|---|---|---|
| [ ] ZL-401 | Oyuncu sayısı, host/sunucu, servis, dünya sahipliği kararı | P0 | 116 ve maliyet incelemesi | Gerekçeli karar kaydı |
| [ ] ZL-402 | İki oyunculu hareket/hasar prototipi | P0 | 401 | Gecikmede tutarlı temel çatışma |
| [ ] ZL-403 | Yetkili eşya, loot, üretim ve yapı | P0 | 402,113 | Eşzamanlı isteklerde çoğaltma yok |
| [ ] ZL-404 | Lobi, davet, katılma/kopma | P0 | 402 | Hatalar ve yeniden bağlanma anlaşılır |
| [ ] ZL-405 | Ortak görev, ölüm/canlandırma ve kayıt | P0 | 403–404 | Tüm oyuncularda aynı dünya sonucu |
| [ ] ZL-406 | Ağ koşulları, host kapanışı, kapasite testi | P0 | 405 | Kapsamı destekleyen ölçüm ve hata listesi |

### M5 — Yayın hazırlığı

| Durum / ID | İş | Öncelik | Bağımlılık | Tamamlanma ölçütü |
|---|---|---|---|---|
| [ ] ZL-501 | Yayın kapsamı ve co-op dahil/hariç kararı | P0 | 308, gerekiyorsa 406 | Desteklenecek özellikler açık |
| [ ] ZL-502 | Steam dağıtım, mağaza ve hesap/hak akışları | P0 | 501 | Gerçek test ortamında doğrulama |
| [ ] ZL-503 | Türkçe/İngilizce ve tüm menü hata durumları | P0 | 501 | Eksik/taşan metin ve kilitli akış yok |
| [ ] ZL-504 | Sistem gereksinimleri ve son performans | P0 | 501 | Gerçek donanım/build ölçümleri |
| [ ] ZL-505 | Lisans, platform, veri ve destek hazırlığı | P0 | 502 | Güncel kontrol ve sorumlu listesi |
| [ ] ZL-506 | Kozmetik model ve gerekirse mağaza | P1 | Ürün kararı,502 | Oynanış avantajı yok, işlem hataları yönetilir |
| [ ] ZL-507 | Güncelleme, kayıt geçişi ve geri dönüş provası | P0 | 502–505 | Önceki kayıt korunur, geri dönüş denenir |
| [ ] ZL-508 | Kapalı test ve yayın kararı | P0 | Etkin M5 kapsamı | Kritik hata yok, bilinen sınırlar açık |

### 25.1. Kritik bağımlılık zinciri

Mevcut proje doğrulama → çatışma/görev → küçük arazi ve navigasyon → eşya/envanter → ses/sopa/bandaj → üretim → kayıt/ölüm → tam döngü testi → bölge/yapı genişletme.

Co-op için teknik risk prototipi ayrı planlanabilir; kapsamlı içerik üretimini yanlış ağ varsayımları üzerine kurmamak için karar geciktirilmez. Takvim, her kapının gerçek teslim verisinden sonra tahmin edilir; bu GDD gün veya yayın tarihi taahhüdü içermez.

## 26. Test ve kabul ölçütleri

### 26.1. İşlevsel kontrol matrisi

| Kod | Senaryo | Beklenen sonuç |
|---|---|---|
| T01 | Namlunun önünde duvar, kamera hedefi görüyor | Duvarın arkasına hasar geçmez |
| T02 | Boş/kısmi şarjör, yetersiz rezerv, hızlı ateş/doldur | Negatif/çift mühimmat veya kilitlenme yok |
| T03 | Zombi saldırı hazırlığından sonra oyuncu uzaklaşır | Erişim dışı hasar uygulanmaz |
| T04 | Ölen zombiye tekrar isabet | Ölüm/ödül/saldırı tekrar oluşmaz |
| T05 | Ses var, görüş yok; sonra hedef kaybı | Son duyulan/bilinen noktayı araştırır |
| T06 | Dolu çantayla loot, hızlı tekrar etkileşim | Eşya kaybolmaz veya çoğalmaz |
| T07 | Depoya aktar, hızlı yuvayı kullan, kaydet/yükle | Sahiplik ve toplam miktar korunur |
| T08 | Girdilerin boşalttığı tek yuvaya üretim | Çıktı doğru biçimde yerleşir |
| T09 | Üretimde iptal/ölüm/menü kapama/tezgâhtan uzaklaşma | Girdiler serbest; çıktı yok |
| T10 | Üretim tamamlanmasıyla aynı anda kayıt/iptal | Yalnız bir tutarlı sonuç oluşur |
| T11 | Tam can, eksik can ve kullanım sırasında hasar | Bandaj kuralları ve tüketim doğru |
| T12 | Sopa savuruşunda birden çok collider ve duvar | Hedef başına tek geçerli hasar |
| T13 | Taş sekmesi ve fizik oluşturma hatası | Tek tüketim/ses veya güvenli geri alma |
| T14 | Sigortasız pano, doğru teslim, tekrar teslim | Yalnız doğru ilk teslim sonuç verir |
| T15 | Görevin her adımında yükleme | Sigorta, görev, kapı ve ışık uyumlu |
| T16 | Yapı yerleştir/tamir et/yükle | Kit, can, rota ve içerik tutarlı |
| T17 | Öğretici kapalı, değiştirilmiş tuş, büyük metin | Görev tamamlanır; etiketler okunur |
| T18 | Envanter/oyun/masaüstü odağı arasında geçiş | İstenmeyen ateş, hareket veya takılı imleç yok |
| T19 | Ölüm, eski kayıt, bozuk dosya, yazma kesintisi | Tutarlı yeniden deneme ve sağlam yedeğe dönüş |
| T20 | Co-op'ta aynı loot/üretim/transfer isteği | Yetkili tek sonuç, çoğaltma yok |

### 26.2. Oyuncu testi

İlk hedef 3–5 yeni oyuncudur. Beş kişiyle test yapılırsa en az 4 kişinin dış yardım olmadan ilk döngüyü tamamlaması başlangıç kabul hedefidir; küçük örneklem genel pazar kanıtı sayılmaz.

Kaydedilecekler: amacı açıklama, ilk anlamlı karar süresi, yön kaybı, görev/üretim süresi, ölüm, mermi tüketimi, isabet, alınan hasar, eczane/yan rota tercihi, geri dönüş ve tekrar oynama isteği. Sorular: “Nereye gideceğini nasıl anladın?”, “Ateşin sonucu ne oldu?”, “Neden geri döndün?”, “Üretimde ne eksikti?”

İlk açık bölgedeki anlamlı loot/rota kararı için 30–60 saniye hedefi kullanılır. Gerçek mesafe, hız ve oyuncu davranışı ölçülür; hedefi tutturmak için zorunlu bekleme eklenmez.

### 26.3. Performans testi

Aynı build, cihaz, rota, süre, grafik ayarı, çözünürlük ve düşman davranışı kullanılır. Öneri: kısa ısınma ardından 5 dakikalık sabit rota; ayrıca 30 dakikalık kullanımda bellek ve birikme kontrolü. Metrikler ham kayıtla saklanır. Donanım veya test süresi değişirse karşılaştırmada belirtilir.

### 26.4. Bir iş ne zaman biter?

İlgili sahnede çalışan davranış + veri/UI/ses ihtiyaçları + hata/iptal akışları + gerekiyorsa kayıt desteği + ilgili kabul kontrolü + build doğrulaması + bilinen sorun kaydı. Geçici varlık kullanılmışsa açık işaretlenir. Test yapılmadıysa “test bekliyor” yazılır; görev kutusu doğrulanmış tamamlanma olarak kapatılmaz.

## 27. Açık kararlar ve riskler

### 27.1. Karar listesi

| ID | Açık karar | Bu GDD'deki geçici yaklaşım | Ne zaman gerekli? |
|---|---|---|---|
| K01 | Gerçek proje yolu ve uygulama durumu | Tasarım belgeleri esas; uygulama bilinmiyor | İlk uygulama işi |
| K02 | Referans PC ve grafik seviyesi | 1080p/60 hedef, cihaz belirtilmedi | İlk performans kapısı |
| K03 | Kesin coğrafya, dönem, karakter ve salgın hikâyesi | İsimsiz sanayi/kırsal bölge | Kalıcı hikâye ve marka üretimi |
| K04 | Bandaj/görev birleşim tercihleri | Bölüm 2.4 taslak standart | İlgili mevcut kodu değiştirirken |
| K05 | Stamina ve ihtiyaçların kapsamı | Stamina M2 deneyi; ihtiyaçlar M3 | İlgili aşama |
| K06 | Uzun vadeli ölüm cezası | M1 tutarlı kontrol noktası | M3 ekonomi testi |
| K07 | Loot/nüfus yenilenmesi | M1 kapalı, M3 tasarlanacak | Tekrar oynanabilir uzun oturum |
| K08 | Serbest inşa, yapı hasarı ve üs baskını | Önce sınırlı yuvalar | M2 sonuçları sonrası |
| K09 | Co-op sayı, ağ, servis, dünya sahipliği | İki kişi prototip, 2–4 aday | M4 öncesi |
| K10 | Erken erişimde co-op | Açık | Yayın kapsamı |
| K11 | F2P maliyeti ve kozmetik kapsam | Adil kozmetik hipotezi | Mağaza/operasyon yatırımı öncesi |
| K12 | Kontrolcü ve ek platform | Önce Windows klavye/fare | Yayın özellik listesi |
| K13 | Ekip, bütçe ve üretim kapasitesi | Tarih taahhüdü yok | Kapılardan sonra takvimleme |

### 27.2. Risk ve küçültme yolu

| Risk | Erken belirti | Müdahale |
|---|---|---|
| Kapsam büyümesi | Her temel işe araç/PvP/şehir eklenmesi | M1/M2 teslim sınırını koru, adayları ayrı tut |
| Çatışma yeterince iyi değil | İsabet/saldırı anlaşılmıyor | Yeni silah yerine mevcut tabancayı düzelt |
| Gürültü cezalandırıcı | Her atış tüm bölgeyi topluyor | Menzil, grup ve tempo bütçesini düşür |
| Envanter/kayıt tutarsız | Eşya çoğalıyor, görev kayboluyor | İçerik büyümesini durdur; işlem/kimlik düzelt |
| Harita boş veya yorucu | Uzun yürüyüş, karar yok | Bölgeyi küçült, odakları yakınlaştır |
| Kaynak tıkanması | Bandaj/ana görev üretilemiyor | Öğretim kaynaklarını garanti et, kaçışı koru |
| Görsel kalite dağınık | El, zombi ve çevre farklı dillerde | Bir referans mekânda ortak standart |
| Performans düşüşü | Bitki, AI veya gölgede takılma | Ölçülen maliyeti düşür, aktif bütçeyi sınırla |
| Co-op çok maliyetli | Geç yeniden yazım ve servis yükü | Erken küçük ağ prototipi, içerik kapsamını ayarla |
| F2P sürdürülemiyor | Hizmet maliyeti karşılanmıyor | Yayın öncesi ürün modelini yeniden değerlendir |

## 28. İlk uygulanacak iş paketi

**Amaç:** Mevcut sigorta görevinden çıkıp kısa bir dış rotada malzeme toplamak, bandaj üretmek ve merkeze dönmek; kayıt sonrası bu ilerlemeyi korumak.

1. Gerçek Unity projesinde çalışan/eksik sistemleri ZL-001 ile çıkar; mevcut özellikleri yeniden yapma.
2. Sigorta tesliminin tek seferlik görev, ışık ve dış kapı sonucunu doğrula.
3. İki bağlantılı rotası olan küçük avlu–yol–loot parçasını kur.
4. Tek eşya/envanter temeline çanta, hızlı yuvalar ve depoyu bağla.
5. Kumaş alma, bandaj üretme ve bandaj kullanmayı uçtan uca tamamla.
6. Gürültü araştırması, dikkat taşı ve sopayı aynı döngüye ekle.
7. Görev, eşya, dünya ve ölüm için tutarlı kayıt/yeniden deneme ekle.
8. Windows test build'inde önce işlev, sonra yön bulma/hissiyat ve performansı doğrula.

Bu paket M1'in uygulanabilir başlangıcıdır. İlk tamamlanma ölçütü haritanın büyüklüğü veya eşya sayısı değil, oyuncunun bir seferi anlamlı seçimlerle tamamlayıp ilerlemesini koruyabilmesidir.

## 29. Kaynaklar ve değişiklik kaydı

### 29.1. Yerel dayanaklar

- [Master Plan v0.2](Zombosland_Master_Plan_v0.2.md): ürün yönü, FPS önceliği, aşamalar ve performans hedefi.
- [Proje çalışma yönergeleri v0.2](Zombosland_AGENTS_v0.2.md): uygulama, doğrulama ve kapsam ilkeleri.
- [7 Kasım oynanış incelemesi](Zombosland_Oynanis_Inceleme_ve_Yol_Haritasi_2026-11-07.md): önceki prototip değerlendirmesi.
- [7 Kasım güncelleme değerlendirmesi](Zombosland_Guncelleme_Degerlendirme_Raporu_2026-11-07.md): çatışma sunumu, eller, ilk görev ve ilerleme önerileri.
- [8 Kasım İlk İkmal tasarımı](design/ilk-ikmal-2026-11-08/Zombosland_Tasarim_Raporu_2026-11-08.md): görev alanı, durumsal öğretici, silah rolleri ve başlangıç denge değerleri.
- [8 Kasım açık dünya tasarımı](design/acik-dunya-2026-11-08/Zombosland_Acik_Dunya_Raporu.md): ilk bölge, beş odak, envanter, crafting ve HUD.
- [Açık dünya uygulama kapsamı](design/acik-dunya-2026-11-08/Claude_Uygulama_Promptu.md): önceki uygulama sırası ve işlem kuralları; bu GDD hazırlanırken uygulanmış kabul edilmedi.

### 29.2. Mevcut görsel referanslar

- [Açık bölge atmosferi](design/acik-dunya-2026-11-08/01-acik-dunya-terrain.png)
- [HUD yönü](design/acik-dunya-2026-11-08/02-oynanis-hud.png)
- [Envanter/üretim yönü](design/acik-dunya-2026-11-08/03-envanter-crafting.png)
- [İlk İkmal mekânları](design/ilk-ikmal-2026-11-08/01-mekan-referanslari.png)
- [İlk İkmal harita planı](design/ilk-ikmal-2026-11-08/02-harita-plani.png)
- [Öğretici yönü](design/ilk-ikmal-2026-11-08/03-durumsal-ogretici.png)

Görseller atmosfer ve düzen referansıdır; içlerindeki küçük yazı, hücre veya sayı farklılıklarında bu belgenin davranış tanımları esas alınır. Görseller uygulanmış oyun ekranı sayılmaz.

### 29.3. Sürüm kaydı

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 10 Kasım 2026 | Önceki plan ve tasarımlar birleştirildi; tüm ana sistemler, gelecek adayları, 56 yapılacak iş, kabul kontrolleri ve açık kararlar tanımlandı. Bandaj, görev bitişi ve harita aşamaları için tutarlı taslak tercihler eklendi. |

**Güncelleme kuralı:** Her önemli tasarım değişikliği hangi sistemi ve aşamayı etkilediğiyle kaydedilir. Sayısal denge değişirse ilgili tablo ve kabul senaryosu birlikte güncellenir. Tamamlanma kutuları yalnız gerçek uygulama ve ilgili doğrulama sonrasında kapatılır.
