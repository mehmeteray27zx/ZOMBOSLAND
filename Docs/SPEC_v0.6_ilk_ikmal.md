# SPEC v0.6 — İlk İkmal: oynanabilir ilk bölüm (gri kutu)

Kaynak tasarım: `C:\Users\MSIGAMİNG\Documents\ChatGPT\Zombosland\design\ilk-ikmal-2026-11-08\Zombosland_Tasarim_Raporu_2026-11-08.md` (Öneri v1.0), harita planı `02-harita-plani.png`, öğretici taslağı `03-durumsal-ogretici.png` (son sürüm: alt panel görev satırı "Sigortayı panoya tak").

**Amaç:** Mevcut çatışma prototipini başı ve sonu olan küçük bir bölüme dönüştürmek. Oyuncu güvenli odadan çıkar, depoda sigortayı bulur, farklı yoldan döner, servis avlusundaki panoya takar, güvenli odada ışıkların geldiğini görür.

**Bu sürümün başarı ölçütü:** Bölüm baştan sona kilitlenmeden oynanır; görev, etkileşim ve ipuçları doğru durumda açılıp kapanır; betikli QA geçer. Oynanış süresi, denge ve okunabilirlik **insan testiyle** ölçülecektir. Bu SPEC bunları ölçülmüş saymaz, rapor da saymamalıdır.

## 0. Kapsam

**Kapsamda:** silah dengesi düzeltmeleri; yeni `IlkIkmal` sahnesi (kodla üretilen gri kutu); E etkileşimi; kapılar; sigorta görevi ve ışık ödülü; depo mühimmat kutusu; görev satırı; durumsal ipucu sistemi; gürültü düzeltmesi; zombi yerleşimi; oyun testi kayıtları; QA betikleri.

**Kapsam dışı (v0.7+):** bandaj ve H tuşu (eczane binası gri kutu olarak yapılır, içi boş kalır), pompalı ve silah değiştirme, tuş yeniden atama arayüzü, yön kaybı ipucu, ara kayıt, koşucu/ağır zombi, çalışma anında zombi üretimi, son sanat geçişi, yeni ses varlıkları.

**Değişmeyecekler:** Unity 6000.6.5f1, URP 17.6.0, paket listesi; `CombatSandbox` sahnesi ve davranışı (aşağıda açıkça belirtilen ortak değişiklikler dışında); `--perf`, `--qa-selftest`, `--qa-screens` çalışmaya devam eder; doldurma kuralı (ateş yok, iptal yok, 1,41 / 1,93 s).

## 1. Silah ve hasar kararları

| Konu | Eski | Yeni | Kapsam |
|---|---|---|---|
| Atış modu | Basılı tutunca otomatik, 0,18 s aralık | **Yarı otomatik**: her atış için yeni sol tık (`GetMouseButtonDown(0)`); en kısa aralık **0,25 s** (en fazla 4 atış/s) | Global |
| Kafa çarpanı | ×2 (68) | **×3 (102)**: kafaya tek isabet standart zombiyi öldürür | Global |
| Gövde / uzuv | ×1 / ×0,7 | Değişmez | — |
| Başlangıç rezervi | 72 sabit | `Sandbox` üzerinde serileştirilmiş `startReserve`; CombatSandbox **72**, IlkIkmal **36** | Sahneye göre |

- `WeaponState` başlangıç rezervini kurucudan alır: `new WeaponState(int startReserve)`. `Capacity = 12` değişmez.
- Ateş zamanlayıcısı (0,25 s) tıklama ile birlikte uygulanır. 0,25 s dolmadan tıklama atış yapmaz ve **kuyruğa alınmaz**.
- Boş tetik "klik" sesi mevcut kuralla devam eder.
- `QaFire` yolu (selftest) yarı otomatik kuraldan etkilenmeden çalışmaya devam etmelidir.
- `WeaponValidation` ve `SandboxValidation` beklenen değerleri güncellenir: 36 rezervle doldurma aktarımları ve 3,0 / 1,0 / 0,7 çarpanları. Eski 72 senaryosu da bir kez test edilir; iki başlangıç değeri de doğrulanır.
- `QaSelfTest` öldürme adımı artık tek kafa isabetiyle tamamlanabilir. Selftest bu sonucu loglar (`QA_SELFTEST kill shots=N`), sabit atış sayısı varsaymaz.

## 2. Sahne ve açılış düzeni

- Yeni editör sınıfı `Assets/Editor/IlkIkmalBuilder.cs` → `Assets/Scenes/IlkIkmal.unity`. CombatSandbox'taki gibi sahne tamamen koddan üretilir; elle düzenlenmez.
- `SandboxBuilder` içindeki ortak yardımcılar (malzemeler, `Box`, oyuncu, viewmodel, efekt/ses, post-process, ışık, sis/gök) `internal static` yapılarak yeniden kullanılır. **Kopyalanmaz.** Oyuncu ve kol düzeneği iki sahnede birebir aynı kalmalıdır.
- Menü:
  - `Zombosland/Regenerate IlkIkmal (overwrites scene)`
  - `Zombosland/Regenerate All and Build`: iki sahneyi üretir ve build alır.
  - Mevcut `Regenerate and Build` artık build ayarlarını **iki sahneyle** yazar; tek sahneyle ezmez.
- Build sahne sırası: `0 = IlkIkmal`, `1 = CombatSandbox`. `PlayerSettings.bundleVersion = "0.6"`.
- `BootRouter` (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`): `--perf`, `--qa-selftest`, `--qa-screens` veya yeni `--sandbox` argümanlarından biri varsa CombatSandbox açılır. Seçim `BOOT_SCENE=<ad>` satırıyla loglanır. Bu yaklaşım çalışmazsa (ilk kare IlkIkmal'de çalışırsa) alternatifi uygula ve raporla; mevcut QA argümanlarının davranışı değişmemeli.
- Batch giriş noktası: `-executeMethod IlkIkmalBuilder.BuildAll`.

## 3. Gri kutu harita

Eksenler: +Z kuzey, +X doğu, birim metre, zemin y=0. Ölçüler başlangıç değeridir. Gri kutu oyun testinde ayarlanacak; değişirse rapora yazılır. Dış sınır yaklaşık x −45…50, z −45…45; çevresi kapalı duvar. Yürünemeyen bloklar dolu kutudur ve içine girilmez.

| Bölge | Sınır / konum | İçerik |
|---|---|---|
| A — Güvenli oda | iç x −34…−26, z −40…−30 (8 × 10) | **Doğu duvarında yeşil kapı** (açıklık 2,0 m, merkez x=−26, z=−34). Masa ve tabanca (görsel, oyuncu silahlı başlar), pano notu, radyo (küçük kutu + lamba). Işıklar: `A_Emergency` açık (zayıf, soğuk), `A_Main` kapalı, `Radio_Lamp` sönük. Oyuncu doğuş noktası (−30, −36), doğuya bakar. |
| Alt yol | z −40…−32 (8 m geniş), x −26…24 | A, B'nin güney ucu ve F'nin batı çıkışını bağlar. **Elektrikten bağımsız olarak her zaman açık.** |
| B — Ana sokak | x −11.5…−0.5 (11 m), z −40…21 | Minibüs: merkez (−6, −5), 2,2 × 5,5 × 2,4 m, 25° çapraz; iki yanında en az 3 m geçiş. Bir alçak bariyer (−2, −20). Depo girişinin üstünde sıcak lamba (görsel yön işareti). |
| C — Eczane (boş) | iç x −26…−18, z −8…4 (8 × 12) | B'ye bağlantı geçidi: z −4…0 (4 m), x −18…−11.5. Yeşil tabela kutusu. v0.6'da ödül yok; 1 isteğe bağlı zombi. |
| D — Hırdavat deposu | iç x −10…14, z 21…39 (24 × 18) | **Güney giriş**: açık boşluk, 2,2 m, merkez x=−6. Kuzey–güney iki raf sırası x=−3 ve x=7, z 25…35, 0,8 m derin, 2,4 m yüksek; iki ucu da bağlı dolaşım (raf arası ≥2,4 m). **Arka tezgâh** (2, 37.5) ve üstünde **sigorta**; tezgâhın üstünde sıcak görev lambası. **Mühimmat kutusu** (12, 23). **Doğu yan kapı** (merkez x=14, z=33, 2,0 m): yalnız içeriden açılır, sonra açık kalır. |
| E — Yan geçit | doğuya x 14…34 / z 31.25…34.75 (3,5 m), sonra güneye x 32.25…35.75 / z −19…34.75 | Güneye inen kolda iki genişleme: 7 × 6 m, merkezleri (34, 15) ve (34, 0). Duvar boyunca boru kutuları (görsel). |
| Kapalı atölye | x 4…22, z −28…17 (dolu) | Görüş kesici. B ile E arasını ayırır. |
| Kapalı bina | x −40…−14, z 12…40 (dolu) | — |
| F — Servis avlusu | iç x 24…44, z −41…−19 (20 × 22) | Kuzeyden E girişi (x 32.25…35.75). **Batıdan alt yola açık bağlantı** (z −40…−32). **Sarı pano** doğu duvarında, merkez x=43.4, y=1.0, z=−30; 1,2 × 1,8 × 0,4 m. Pano üstünde lamba (`Panel_Lamp`, kapalı). İki engel kutusu iki dolaşım şeridi oluşturur. |

- Kritik geçişlerde net açıklık en az 2,0 m, raf aralarında en az 2,4 m olmalı. `IlkIkmalBuilder` bu ölçüleri kontrol eder ve `ILKIKMAL_BUILD_NOTE` satırlarıyla raporlar.
- Ana rota yaklaşık 260 m (A → B → D → E → F → A). 4 m/s yürüyüşle yalnız yürüme yaklaşık 65 s sürer. Bu hesap ölçüm değildir.
- Malzemeler: CombatSandbox'taki PolyHaven setleri tekrar kullanılır. Yeni doku yok. Renk vurguları: güvenli kapı yeşil, pano sarı, sigorta kırmızı, eczane tabelası yeşil.
- Işık: mevcut gök ve güneş ayarı. Ek nokta ışıklar yalnız depo lambası, tezgâh lambası, A ışıkları ve pano lambası. Gerçek zamanlı ışık sayısını düşük tut ve rapora yaz.
- `ZombieDirector.navCenter` / `navSize` tüm haritayı kapsamalı: yaklaşık (2.5, 0, 0) / (100, 10, 95). Kapı boşlukları NavMesh'te geçilebilir olmalıdır (bkz. §4).

## 4. Etkileşim sistemi (E)

- Yeni katman `Interactable` (13). `Layers.cs` ve `SandboxBuilder.RegisterLayers` güncellenir. `ShotMask` ve `SightMask` bu katmanı **dışlar**; atışlar ve görüş etkileşim tetiklerinden etkilenmez.
- `IInteractable`:
  - `string Verb` (ör. "Sigortayı al")
  - `bool CanInteract(Sandbox p)`
  - `void Interact(Sandbox p)`
  - `Transform HintAnchor`
- `Interactor` (oyuncu bileşeni), her karede:
  - `AimPivot`'tan 0,12 m yarıçaplı küre ışını gönderir; menzil **2,2 m**; maske `Environment | Interactable`; `QueryTriggerInteraction.Collide`.
  - İlk isabet `Environment` ise odak yok (duvar arkası seçilmez).
  - Odakta `CanInteract` true olan nesne varsa HUD dünya etiketi `[E] <Verb>` gösterilir.
  - `Input.GetKeyDown(KeyCode.E)` → `Interact`.
  - Ölüyken, menüdeyken veya doldururken de etkileşim mümkündür; **ateşi etkilemez**.
- Nesnelerin görsel gövdesi `Environment` katmanında; ayrı bir alt nesnede `Interactable` katmanında tetik collider bulunur.
- Tuş adı tek yerde tanımlanır: `InputLabels.Interact = "E"`, `Reload = "R"` vb. HUD metinleri bu sabitlerden üretilir; metinlere tuş gömülmez.

Nesneler:

| Nesne | Verb | Koşul | Sonuç |
|---|---|---|---|
| A kapısı | "Kapıyı aç" | Kapalıyken | Menteşe etrafında 90° açılır (0,6 s). Açık kalır, kapanmaz. |
| D yan kapı | "Kapıyı aç" | Kapalı ve oyuncu depo **içinde** (x < 14) | Aynı. Dışarıdan bakıldığında etiket yok; dünya metni "Bu taraftan açılmıyor" kısa süre görünür. |
| Sigorta | "Sigortayı al" | Görev `SearchFuse` | Sigorta görünümü gizlenir, görev `CarryingFuse`. |
| Mühimmat kutusu | "Mühimmatı al" | Alınmamışsa | Rezerve +12, bir kez. Kutu boş görünüme geçer. |
| Sarı pano | "Sigortayı tak" | Görev `CarryingFuse` | Görev `FuseInstalled` + ışık olayı. Sigortasız bakınca etiket yerine sönük "Sigorta gerekli" görünür. |

- Kapılar: kapalıyken bir `NavMeshObstacle` (carve) yolu keser; açılınca obstacle kapatılır. Zombiler kapalı kapıdan geçmez; açıldıktan sonra yol bulur.
- Her nesne sonucunu **en fazla bir kez** uygular. Tekrar basmak ödülü, ışığı veya mühimmatı tekrarlamaz.

## 5. Görev ve ödül

- `QuestFuse` (sahne bileşeni), durum makinesi: `SearchFuse → CarryingFuse → FuseInstalled → Complete`. Geçişler yalnız başarılı eylemle olur ve geri gitmez. Her geçiş `QUEST state=<ad> t=<sn>` satırıyla loglanır.
- Görev satırı metinleri:
  - `SearchFuse`: "Depodaki sigortayı bul"
  - `CarryingFuse`: "Sigortayı panoya tak"
  - `FuseInstalled`: "Güvenli odaya dön"
  - `Complete`: "Elektrik geri geldi"
- `FuseInstalled` ışık olayı tek seferde çalışır: `Panel_Lamp` açılır, `A_Main` açılır (sıcak), `A_Emergency` kapanır, `Radio_Lamp` emisif açılır. Ses varsa mevcut `SfxBank`'tan kısa bir mekanik ses (yeni varlık yok).
- `Complete`: oyuncu `FuseInstalled` durumundayken A iç hacmine (tetik kutusu) girdiğinde. Ekranın ortasında 8 s **sonuç kartı**: "Bölüm tamamlandı", süre, ölüm sayısı, atılan / isabet eden mermi. Kartı `Enter` kapatır. Oyun serbest dolaşımda devam eder; `Esc` menüsüne "Bölümü yeniden başlat" eklenir.
- Ölüm: mevcut davranış (2 s kararma, sahneyi yeniden yükleme). Görev, mühimmat, kapılar ve ışıklar başlangıç durumuna döner. **Sigorta, ölümden sonra hiçbir koşulda yanlış durumda kalmaz.**

## 6. Durumsal ipucu sistemi

- `HintDirector` (sahne bileşeni). Aynı anda **en fazla bir** ipucu kartı. Görev satırı ve `[E]` dünya etiketi bu kotaya sayılmaz.
- Öncelik: kritik (mühimmat yok) → geçerli etkileşim → ilk kullanım.
- Ekrandaki kart en fazla 6 s kalır. Başarılı eylem kartı hemen kapatır.
- Hatırlatma: başarısızlık sürüyorsa en erken 20 s sonra, ipucu başına en fazla 2 hatırlatma.
- Koşullar gösterim anında yeniden değerlendirilir; kuyruktan eski kart oynatılmaz.
- **Öğrenildi** bilgisi `PlayerPrefs`'te tutulur (`hint_<id>=1`); hatırlatma sayaçları oturum boyunca statik tutulur. Bu sayede ölüm sonrası yeniden yüklemede ipuçları tekrar çıkmaz.
- Duraklat menüsüne iki ayar eklenir:
  - `Öğretici: Açık / Kapalı` (`GameSettings.Tutorial`, PlayerPrefs). Kapalıyken yalnız kartlar gizlenir; görev satırı ve `[E]` etiketleri kalır.
  - `Öğretici ilerlemesini sıfırla`.

| id | Metin | Göster | Öğrenildi / kapan |
|---|---|---|---|
| move | `[W A S D] Hareket et` | A'da 3 s girdisiz | Toplam 2 m yürüyüş |
| aim | `[Sağ fare] Nişan al` | Bir zombi `canSee` ile oyuncuyu görmeden önce 10–18 m'de ve kameranın önünde (görüş konisi 30°); henüz hiç ADS yapılmamış | İlk ADS; bir zombi 4 m'ye girerse iptal |
| reload | `[R] Yeniden doldur` (mühimmat göstergesine bağlı) | Şarjör 0, rezerv > 0, doldurmuyor | Doldurma başladığında |
| noammo | `Mühimmat yok — uzaklaş` (öğrenilmez, kritik) | Şarjör 0 ve rezerv 0 | Durum değişince |
| noise | `Silah sesi yakındakileri çekebilir` | Bir zombi **gerçekten** bir atış sesine yöneldiğinde (bkz. §7 olayı); ilk kez | 5 s sonra; bir kez gösterilince öğrenilir |

- Sunum: IMGUI, mevcut `Hud` stiliyle. Yaklaşık %55 opak koyu zemin, kehribar tuş kutusu ve 1080p'de 22–26 px metin; mevcut `scale` matrisi kullanılır.
- Konumlar:
  - Görev satırı sol üstte; değiştiğinde 1,5 s vurgulanır.
  - Kartlar sağ altta, mühimmat göstergesinin üstünde.
  - Dünya etiketi: `HintAnchor` ekrana yansıtılır; etiket ile bağlantı noktası arasında kısa bir çizgi. Yansıma ekran dışına ya da kameranın arkasına düşerse çizilmez.
- Nişangâhın çevresinde (merkezden 120 px yarıçap) hiçbir kart çizilmez.
- Mevcut F1 kontrol şeridi kalır ama IlkIkmal'de otomatik 10 s gösterilmez; yalnız F1 ile açılır. CombatSandbox'ta davranış aynı kalır.

## 7. Gürültü ve zombiler

- `ZombieBrain.OnNoise`: zombi ile ses noktası arasında `SightMask` ile engel varsa etkili yarıçap **yarıya** iner. Atış başına zombi başına en fazla bir ışın. Perf modunda atış yok, değişiklikten etkilenmez.
- Gürültü yarıçapı sahneye göre `Sandbox` alanından gelir: CombatSandbox **30 m** (değişmez), IlkIkmal **22 m**.
- Yeni statik olay: `NoiseEvents.ZombieHeard(ZombieBrain z)`. `OnNoise` bir zombiyi gerçekten `Chase` durumuna geçirdiğinde tetiklenir. `HintDirector` ve oyun testi kaydı bunu kullanır.
- `ZombieBrain.canSee` salt okunur `CanSeePlayer` özelliğiyle dışa açılır; `aim` ipucu bunu kullanır.
- `ZombieBrain` dolaşma yarıçapı serileştirilmiş alan olur: `wanderRadius`, varsayılan 6. Bugünkü sabit 6 m davranışı korunur.
- Not: builder'la yerleştirilen zombiler `Director.Start` → `Activate` ile başlar ve uyarılmamış (Wander/Idle) gelir. `Alert()` yalnız `SpawnFixed` (perf) yolunda çağrılır; bu yol değişmez.

Yerleşim (hepsi builder'da, rastgele yok; tohum sabit):

| Grup | Konumlar (x, z) | wanderRadius | Not |
|---|---|---|---|
| S1 — sokak | (−4, −14), (−8, −10) | 3 | Oyuncu B'ye girdiğinde yaklaşık 20 m uzakta (görüş 18 m) |
| S2 — depo | (2, 29) raf arası, (−6, 35) batı koridor, (24, 33) E başlangıcı | 3 | Sigorta tezgâhından görünebilir |
| S3 — dönüş | (33, 1), (35, 2.5) ikinci genişleme; (38, −26) avlu | 2 | Bilinçli olarak dar kolda **değil**, genişlemede |
| C — isteğe bağlı | (−23, 1) | 2 | Eczane içi |

Toplam zorunlu rota düşmanı 8, isteğe bağlı 1. Kaynak bütçesi: 48 başlangıç + 12 kutu = 60 mermi. Bu ekonomi insan testinde ölçülecektir.

## 8. Oyun testi kaydı (ölçüm altyapısı)

- `PlaytestLog` (statik, oturum boyu; sahne yeniden yüklense de sürer).
- Kaydedilenler: bölüm başlangıç zamanı, ölüm sayısı, atılan / isabet eden / kafaya isabet eden mermi, alınan hasar, görev geçiş zamanları, eczane iç hacmine giriş (evet/hayır), gösterilen ve tamamlanan ipuçları (id başına), 20 s'den uzun hareketsiz/ilerlemesiz aralık sayısı.
- `Complete` olduğunda exe yanına `PlaytestResults/playtest_<zaman>.txt` yazılır ve Player.log'a tek satır `PLAYTEST_RESULT ...` eklenir. Erken çıkışta da (`GameQuit`) kısmi sonuç yazılır.
- Bu dosyalar insan testinin verisidir; betikli QA çalıştırmaları ayrıca `qa=1` ile işaretlenir.

## 9. QA ve doğrulama

**Editör (batch):** `IlkIkmalValidation` (`SandboxValidation` örüntüsüyle) şunları kontrol eder:
- Sahnede tek `QuestFuse`, tek `HintDirector`, 5 etkileşim nesnesi, 9 zombi var.
- Kritik açıklıklar ve raf araları §3'teki alt sınırlara uyuyor.
- Etkileşim tetikleri `Interactable` katmanında; `ShotMask` / `SightMask` bu katmanı dışlıyor.
- Build ayarları iki sahneyi doğru sırayla içeriyor.

**Çalışma anı:** `--qa-playthrough` (yalnız IlkIkmal) aşağıdaki adımları betikle yürütür ve her adımı `QA_PLAYTHROUGH` satırıyla loglar. Zombiler donuk (`ZombiesFrozen`); en sonda ayrı bir gürültü adımı çalışır.
1. NavMesh yolları: `NavMesh.CalculatePath` ile A kapısı → D girişi, D yan kapı → F panosu, F → A. Kapılar açıldıktan sonra hepsi `PathComplete` olmalı. Kapı kapalıyken A dışı → A içi yolu `PathComplete` **olmamalı**.
2. A kapısına bak → etiket "Kapıyı aç" → etkileşim → kapı açık.
3. Teleport + bakış ile sigortayı al → görev `CarryingFuse`. Aynı yere tekrar bak → etiket yok.
4. Mühimmat kutusu: rezerv +12. İkinci basış rezervi değiştirmiyor.
5. D yan kapıya dışarıdan bak → açılmıyor; içeriden → açılıyor.
6. Panoya sigortasız bakılan durum (ayrı kurulumla) → "Sigorta gerekli"; sigortayla → `FuseInstalled`, ışıklar açık. İkinci basış olayı tekrarlamıyor (ışık olay sayacı = 1).
7. A tetik hacmi → `Complete`; sonuç dosyası yazıldı.
8. Ölüm ve yeniden yükleme: sigorta alınmışken ölüm zorlanır → yeniden yükleme sonrası görev `SearchFuse`, sigorta yerinde, rezerv 36, kapılar kapalı, ışıklar başlangıç durumunda. Öğrenilmiş ipuçları öğrenilmiş kalır.
9. İpuçları: şarjör 0 ve rezerv > 0 → `reload` kartı görünür; doldurma başlayınca kapanır. Şarjör 0 ve rezerv 0 → `noammo`. Aynı anda iki kart yok (her karede kart sayısı ≤ 1, ihlal sayacı = 0).
10. Gürültü: zombileri çöz; duvar arkasında 15 m'deki bir zombi 22 m'lik atışla `Chase`'e **geçmemeli** (etkili yarıçap 11 m). Açık alanda 15 m'deki zombi geçmeli ve `ZombieHeard` bir kez tetiklenmeli.
11. Sonda `QA_PLAYTHROUGH_DONE pass=<n> fail=<n>` yazılır ve oyun çıkar.

**Gerileme kontrolü:**
- `--qa-selftest`: CombatSandbox açılıyor ve `QA_SELFTEST_DONE` yazılıyor.
- `--perf=10 --perf-seconds=10` duman testi: CSV yazılıyor (performans iddiası yok).
- `--qa-screens`: mevcut 12 görüntü üretiliyor.

**İsteğe bağlı (P1):** `--qa-screens-ilkikmal` ile 1600×900 pencerede 6 referans kamera: A'dan B'ye, minibüs, depo tezgâhı, D yan kapı, E genişleme, F panosu. Ayrıca ışık olayından önce ve sonra A içi. `QaScreens_ilkikmal/` klasörüne yazılır.

## 10. Teslim

- Build: `Build/Zombosland.exe`, açılış sahnesi IlkIkmal.
- `README.md`:
  - Kontrollere E (etkileşim) ve yarı otomatik atış eklenir.
  - Yeni argümanlar `--sandbox` ve `--qa-playthrough`.
  - Sahne sırası.
  - "Sınırlar" bölümü: süre ve denge ölçülmedi, insan testi bekliyor.
- `Docs/IMPLEMENTATION_REPORT.md` v0.6 bölümü:
  - Değişen dosyalar.
  - Validation, selftest ve playthrough log çıktıları (pass/fail sayıları birebir).
  - §3 ölçülerinden sapmalar.
  - Gerçek zamanlı ışık sayısı.
  - Bilinen sorunlar.
  - **Ölçülmemiş** şeylerin açık listesi.
- `Docs/QA_ACCEPTANCE.md`: "İlk İkmal" bölümü eklenir. Insan testi maddeleri tasarım raporu §8'deki 10 senaryodan alınır ve hepsi `Beklemede` durumunda başlar.
- Lisans: yeni varlık eklenmez. Eklenirse lisansı doğrulanır ve ilgili `LICENSE.md` yazılır.

## 11. Önerilen uygulama sırası

1. §1 silah değişiklikleri ve validation güncellemeleri → build + selftest.
2. §2 sahne iskeleti ve BootRouter → mevcut QA argümanları hâlâ CombatSandbox'a gidiyor.
3. §3 gri kutu ve NavMesh → playthrough adım 1.
4. §4 etkileşim ve kapılar, §5 görev ve ışık → playthrough adımları 2–8.
5. §6 ipuçları, §7 gürültü ve yerleşim → playthrough adımları 9–10.
6. §8 kayıt, §9 isteğe bağlı ekran görüntüleri, §10 belgeler.

Her adımdan sonra batch build alınır. Başarısız bir adım sonrakine geçmeden düzeltilir ve rapora yazılır.
