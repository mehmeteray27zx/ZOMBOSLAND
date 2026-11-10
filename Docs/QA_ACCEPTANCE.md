# Zombosland — İlk FPS Combat Sandbox Kabul Kontrol Listesi

Bu liste ilk oynanabilir FPS çatışma prototipini değerlendirmek içindir. Tam oyun, açık dünya veya yayın kalitesi şartı değildir. Kontroller çalıştırıldıkça **Durum** alanına `Geçti`, `Sorun`, `Beklemede` veya `Uygulanmıyor` yazın; build, cihaz, ayar ve bulguları not edin. Henüz yapılmamış kontroller varsayılan olarak **Beklemede** kabul edilir.

## Görsel inceleme

Her inceleme maddesi için ilgili ekran görüntüsünü kaydedin (tercihen 1080p; build ve ayarları dosya adına/notuna ekleyin). Bunlar gerçek görüntü üzerinden değerlendirilecek; bu belge herhangi bir görüntünün incelendiğini veya kalite seviyesinin elde edildiğini iddia etmez.

| Görünüm / kanıt | Kabul ölçütü | Durum / not |
|---|---|---|
| Sandbox genel görünümü; başlangıç noktası ve aydınlık/karanlık alan | Oyuncu, zemin ve hedefler okunuyor; ışık/karanlık geçişleri hedefi kaybettirmiyor, bariz aşırı parlaklık veya siyah ezilmesi yok. Gri kutu için tutarlı ve yeterli. | Beklemede |
| Silah ve eller, hip-fire; ekran merkezi ve kenarları | Silah nişan alanını gereksiz kapatmıyor; ekran kenarı veya kamera dönüşünde belirgin kesilme, titreme ya da geometriye gömülme yok. | Beklemede |
| Zombi orta/uzak mesafe ve farklı arka planlar önünde | Siluet ve animasyon arka plandan ayırt ediliyor; hedefin yönü ve yaklaşması anlaşılır. | Beklemede |
| Zombiye isabet ve ölüm anı | İsabet tepkisi, hasar/ölüm oyuncuya okunur; tepki gerçek isabetle tutarlı, tekrar vuruşta hayalet/çift tepki yok. | Beklemede |
| HUD: sağlık, mermi/rezerv, nişangâh; açık ve karmaşık arka plan | Yazı ve simgeler oyun görüntüsünden ayrılıyor; mermi ve reload durumu hızlıca okunuyor, nişangâh hedefi aşırı örtmüyor. | Beklemede |
| ADS: hedefteyken ve kamerayı çevirirken | Nişangâh/nişan hizası tutarlı; yakınlaştırma ve görüş alanı geçişi kararlı, hedef HUD veya silah animasyonuyla kaybolmuyor. | Beklemede |
| Duvara yakın duruşta hip-fire ve ADS | Silah/eller duvarın içinden görünmüyor veya bu durum açıkça ve tutarlı biçimde ele alınıyor; duvar engeli oyuncuya anlaşılır. | Beklemede |
| Kamera/silah konfor ayarları: efektler açık/kapalı, farklı hassasiyet ve FOV | Ayarlar uygulanıyor; headbob, sway ve kamera sarsıntısı azaltılabiliyor/kapatılabiliyor. Görsel hareket nişan okunabilirliğini bozmuyor. | Beklemede |

**Görsel çıta notu:** İyi aydınlatılmış, anlaşılır bir prototip görünümü kabul edilebilir. Black State/AAA düzeyinde sanat, animasyon, materyal, çevre ayrıntısı veya prodüksiyon kalitesi bu ilk sandbox için kabul kapısı değildir; bu hedefler henüz doğrulanmamış/aspirasyoneldir.

## Oynanış kontrolleri

| Kontrol | Kabul ölçütü | Durum / not |
|---|---|---|
| Hareket ve çarpışma | Yürüme/koşma/eğilme/zıplama mevcut özelliklere göre çalışır; zemin ve engeller oyuncuyu durdurur, takılma veya zeminden düşme tekrarlanabilir kritik sorun yaratmaz. | Beklemede |
| Kamera | Fare bakışı, hassasiyet ve FOV ayarları beklenen yönde çalışır; konfor efektleri ayrı ayrı azaltılabilir/kapatılabilir. | Beklemede |
| Ateş, nişan ve recoil | Tek atış/ateş hızı, hip-fire ve ADS davranışı tutarlı; recoil ve toparlanma anlaşılır, görsel sapma tek başına isabet/hasar hesabını değiştirmez. | Beklemede |
| Hitscan ve engel | Farklı mesafede hedefe isabet ve ıskalama doğrulanır; hedef/katman seçimi tutarlı. Hedef ile namlu arasında duvar varken duvar arkasına hasar gitmez. | Beklemede |
| Mühimmat ve reload | Şarjör ve rezerv doğru azalır; boş şarjör ateş etmez; reload tamamlandığında mühimmat güncellenir. Reload sırasında ateş/iptal davranışı belirlenmiş kuralla tutarlı, kesinti/animasyon eksikliği durumu kilitlemez. | Beklemede |
| Hasar bölgeleri ve zombi tepkisi | Vücut bölgeleri varsa beklenen farklı hasar uygulanır; isabet tepkisi uygun, saldırı/ölüm akışı çalışır. Ölü hedef tekrar hasar/ölüm olayı üretmez. | Beklemede |
| Ses, animasyon ve VFX | Ateş, isabet ve reload sunumu ilgili oyun olayıyla senkron; eksik geçici varlıklar not edilir, sunum durumu oynanış mantığını kilitlemez. | Beklemede |
| Ayarların kalıcılığı/uygulanması | Mevcut ayarlar menüde değiştirilebilir ve oyun sırasında doğru etkiyi gösterir; kaydetme davranışı varsa yeniden başlatma sonrası kontrol edilir. | Beklemede |

## Build ve çalışma

- [ ] Proje editör sürümü `ProjectSettings/ProjectVersion.txt` ile kaydedildi; sessiz sürüm yükseltme/düşürme yapılmadı.
- [ ] Windows standalone build üretildi; derleme hatası yok. Build temiz açılıp sandbox'a giriyor ve temel akışta tekrar edilebilir çökme/kilitlenme yok.
- [ ] Test edilen build kimliği/tarihi, çözünürlük, grafik ayarları, cihaz ve bilinen hatalar kaydedildi.
- [ ] Ayarlar ve giriş temel akışta denenip sonuçları yukarıdaki maddelere işlendi.

## Performans ölçüm protokolü

Ölçüm yapılana kadar sonuç **Beklemede** kalır; hedef değer sonuç gibi yazılmaz.

1. Referans PC ve grafik ayarlarını testten önce belirleyin: CPU, GPU, RAM, sürücü, Windows, build türü, çözünürlük (hedef 1080p), kalite seviyesi, VSync/kare sınırı ve profiler/ölçüm aracı.
2. Standalone build kullanın. Aynı sahne, başlangıç noktası/rota, süre, zombi davranışı ve ayarlarla 10, 30 ve 100 aktif zombi senaryosu çalıştırın. Her senaryoda ısınma sonrası sabit bir ölçüm penceresi kullanıp süreyi ve tekrar sayısını kaydedin.
3. Her senaryoda CPU ve GPU kare süresi, ortalama FPS, %1 düşük FPS, bellek kullanımı ve gözlenen takılmaları kaydedin. Ham ölçüm veya ekran görüntüsü/log konumunu not edin; farklı cihaz/ayar sonuçlarını doğrudan kıyaslamayın.
4. 30 aktif zombide 1080p/60 FPS (yaklaşık 16,7 ms kare bütçesi) bir **hedef hipotezidir**, garanti veya geçilmiş sonuç değildir. 10/30/100 sonuçlarını ayrı raporlayın; 100 zombide 60 FPS zorunlu eşik değildir.
5. Editor Play Mode kullanılırsa sonucu standalone ile karıştırmayın; yüksek polling rate fare Editor ölçümünü etkileyebilir. Şüphede aynı rota ve ayarlarla karşılaştırın.

**Ölçüm kaydı:** Referans donanım: ___ · Build/commit: ___ · Ölçüm aracı: ___ · Senaryo/süre/tekrar: ___ · Sonuç dosyası: ___

## Dış oyuncu testi

3–5 dış oyuncuyla test hedeflenir. Katılımcı, build, oturum tarihi ve kısa notlar kaydedilmeden bu kapı **Beklemede** kalır; geliştirici/AI yorumu dış oyuncu geri bildirimi sayılmaz. Katılımcılara hareket konforu, hedefi ve isabeti anlama, silah geri tepmesi, reload açıklığı ve çatışma hissi sorulur. Geri bildirim bulunamıyorsa engel ve sonraki adım yazılır; sonuç uydurulmaz.

| Katılımcı / tarih | Konfor ve nişan anlaşılabilirliği | İsabet/recoil/reload hissi | Sorun veya alıntı özeti |
|---|---|---|---|
|  |  |  |  |
|  |  |  |  |
|  |  |  |  |

## İlk İkmal (v0.6) oyun içi doğrulama senaryoları

Kaynak: tasarım raporu `Zombosland_Tasarim_Raporu_2026-11-08.md`, "Oyun içi doğrulama senaryoları" (10 madde). Bunlar **insan testi** maddeleridir ve hepsi `Beklemede` başlar. "Betikli kanıt" sütunu `--qa-playthrough` / `--qa-screens-ilkikmal` çıktılarının ilgili parçasını gösterir; bir insanın oynayarak yaptığı doğrulamanın yerini tutmaz ve oyuncu anlayışı, süre, denge veya okunabilirlik hakkında bir şey söylemez. Test edilen build, tarih, cihaz ve çözünürlüğü not alın; İlk İkmal oyun testi çıktıları `PlaytestResults/playtest_<zaman>.txt` dosyalarında toplanır (`qa=1` olanlar betikli çalıştırmadır, insan testine sayılmaz).

| # | Senaryo | Betikli kanıt (insan testi değil) | Durum / not |
|---|---|---|---|
| 1 | Oyuncu tüm ipuçlarını okumadan doğrudan hareket eder: eğitim kuyruğu onu durdurmaz. | Kart yalnızca bilgi çizer, girdiyi engellemez; aynı anda en fazla 1 kart (playthrough adım 9–10, `violations=0`). | Beklemede |
| 2 | E tuşu başka bir tuşa atanır: kapı, sigorta ve pano aynı güncel tuşu gösterir. | Etiketler `InputLabels.Interact` sabitinden üretilir. Tuş yeniden atama arayüzü v0.7+ kapsamındadır; bu madde sabiti değiştirip yeniden derleyerek elle denenir. | Beklemede |
| 3 | Şarjör boş ve rezerv sıfır: doldurma önerilmez. Rezerv varsa doldurma doğru anda önerilir. | `noammo` kartı "Mühimmat yok — uzaklaş", `reload` kartı yalnızca şarjör 0 ve rezerv > 0 iken (adım 9). | Beklemede |
| 4 | Öğretici kapalı: görev, nesnelerin görsel yerleşimi ve normal etkileşim etiketleriyle bitirilebilir. | Öğretici kapalıyken kart çıkmadığı sınandı (adım 9); görev satırı ve `[E]` etiketleri kartlardan bağımsızdır. | Beklemede |
| 5 | Sigorta alındıktan sonra aynı yere bakılır: eski alma oku kaybolur; görev "Sigortayı panoya tak" olur. | Adım 3: aynı yere tekrar bakınca etiket yok, durum `CarryingFuse`. | Beklemede |
| 6 | Oyuncu eczaneyi ve pompalıyı atlar: tabancayla/kaçışla görev hâlâ tamamlanabilir. | Playthrough eczaneye girmeden `Complete` olur (`pharmacy=no`). Pompalı v0.6 kapsamında yok. | Beklemede |
| 7 | Aynı panoya tekrar basılır: görev ödülü veya ışık olayı tekrar tekrar çalışmaz. | Adım 6: ikinci basışta ışık olayı sayacı 1 kalır. | Beklemede |
| 8 | Zombi raflara, minibüse veya kapıya takılır: rota genişliği ve geçiş bağlantıları düzeltilir. | Yalnızca geometri: açıklıklar ve NavMesh yolları (adım 1, `IlkIkmalValidation`). Canlı zombi takılması betikle denenmedi. | Beklemede |
| 9 | Çatışma sırasında öğretici açılır: hedefi kapatmaz ve gereksiz bilgi kartı ertelenir. | Kartlar sağ altta, nişangâhın 120 px dışında; öncelik sırası ve tek kart kuralı sınandı (adım 9). Gerçek çatışmada okunabilirlik denenmedi. | Beklemede |
| 10 | Ölüm ve yeniden başlatma: sigorta, mühimmat, kapı, pano ve öğretici durumları tutarlı döner. | Adım 8: görev, sigorta, rezerv 36, kapılar, ışıklar ve öğrenilmiş ipuçları (pano yuvası görseli ayrıca elle bakılmalı). | Beklemede |

**Ek insan testi ölçümleri (rapor "Oyuncu testi ve ölçüm").** İlk 30 saniyede amacı kendi cümlesiyle anlatabilme, yön sormadan rotayı tamamlama, ipucundan sonra doğru eylemi yapma, isabet ile saldırıyı ayırt etme: **Beklemede**. Bitirme süresi, 20 s'yi aşan yön kaybı, gösterilen/tamamlanan ipuçları, ölümler, harcanan mermi, alınan hasar ve eczane sapması `PlaytestResults` dosyalarından okunur; oynanış süresi ve denge **ölçülmedi**.

## Kabul özeti

| Kapı | Durum | Kanıt / açık sorun |
|---|---|---|
| Görsel inceleme ve ekran görüntüleri | Beklemede |  |
| Oynanış kontrolleri | Beklemede |  |
| Temiz Windows standalone build ve temel çalışma | Beklemede |  |
| Performans ölçüm raporu (hedef/sonuç ayrımıyla) | Beklemede |  |
| 3–5 dış oyuncu testi veya açıkça beklemede notu | Beklemede |  |
| İlk İkmal (v0.6) oyun içi doğrulama senaryoları (10) | Beklemede | Betikli QA geçti, insan testi yapılmadı. |

İlk prototip için kabul; temel sandbox döngüsünün oynanabilir olması, kritik tekrar edilebilir sorunların belgelenmesi ve henüz çalıştırılmamış kapıların dürüstçe beklemede gösterilmesidir. Açık dünya, co-op, içerik ölçeği veya tam yayın kalitesi bu kontrol listesinin kapsamı değildir.
