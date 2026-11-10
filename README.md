# Zombosland — İlk İkmal, açık bölge + Combat Sandbox (v0.7)

Unity 6000.6.5f1, URP 17.6.0, Windows standalone. Build sahne sırası: **0 = `IlkIkmal`** (oynanabilir ilk bölüm, gri kutu; açılış sahnesi), **1 = `CombatSandbox`** (çatışma sandbox'ı). İki sahne de tamamen koddan üretilir; elle düzenlemeyin: `IlkIkmalBuilder` → `Assets/Scenes/IlkIkmal.unity`, `SandboxBuilder` → `Assets/Scenes/CombatSandbox.unity`. İkisini üretip `Build/Zombosland.exe` almak için: **Zombosland > Regenerate All and Build** ya da `Unity.exe -batchmode -quit -projectPath . -executeMethod IlkIkmalBuilder.BuildAll`. Yalnızca İlk İkmal için: **Zombosland > Regenerate IlkIkmal (overwrites scene)**. Sahneyi koruyup yalnızca build almak için **Build Existing Scene**.

**İlk İkmal ve açık bölge:** oyuncu güvenli odadan çıkar, depoda sigortayı bulur, servis avlusundaki panoya takar. Işıklar yanar, dış kapı açılır, hedef "Eski istasyonu araştır" olur. Sonrası 500 × 500 m terrain üzerinde keşif, toplama ve üretimdir (tasarım: `Docs/SPEC_v0.6_ilk_ikmal.md`, `Docs/SPEC_v0.7_acik_bolge.md`).

## Kontroller
WASD hareket, Shift koşma, Ctrl eğilme, Space zıplama, sol fare ateş, sağ fare nişan, R doldurma (şarjör boş değilken 1,41 s, boşken 1,93 s; ateş ve iptal yok), **E etkileşim** (kapı, sigorta, mühimmat kutusu, pano, toplanabilirler, tezgâh, depo, yapı yuvaları; yalnızca İlk İkmal), **Tab çanta/üretim**, **1–4 hızlı yuvalar** (1 tabanca), Enter sonuç kartını kapatır, Esc ayarlar, F1 kontrol şeridini aç/kapa (CombatSandbox'ta ilk 10 s görünür; İlk İkmal'de yalnız F1 ile açılır). Ateş **yarı otomatiktir**: her atış için yeni bir sol tık gerekir, atışlar arası en az 0,25 s (en fazla 4 atış/s); süre dolmadan tıklama atış yapmaz ve kuyruğa alınmaz. Kafa isabeti ×3 (102 hasar, standart zombiyi tek atışta öldürür), gövde ×1, uzuv ×0,7. Başlangıç rezervi CombatSandbox'ta 72, İlk İkmal'de 36 mermi. Şarjör boşken tetik "klik" sesi verir. Can sıfırlanınca 2 s kararma sonrası sahne yeniden yüklenir (İlk İkmal'de görev, mühimmat, kapılar ve ışıklar başlangıç durumuna döner).

## Ayarlar (Esc, PlayerPrefs, anında uygulanır)
Fare hassasiyeti (0,3–5), FOV (60–100), headbob (0–1), silah sway (0–1), kamera sarsıntısı (0–1), ana ses (0–1). 0 = kapalı. Nişan tepmesi (recoil) oynanış durumudur ve bu ayarlardan etkilenmez. Eski `shake` değeri `weaponSway` anahtarına taşınır. İlk İkmal'de ayrıca `Öğretici: Açık / Kapalı` (kapalıyken yalnız ipucu kartları gizlenir; görev satırı ve `[E]` etiketleri kalır) ve `Öğretici ilerlemesini sıfırla` (öğrenilmiş ipuçlarını unutturur) bulunur; "Bölümü yeniden başlat" yeni bir deneme başlatır.

## Başlangıç argümanları
- `--sandbox` CombatSandbox'ı açar (argümansız açılış İlk İkmal'dir). `--perf`, `--qa-selftest`, `--qa-screens` da CombatSandbox'a yönlenir; seçim `BOOT_SCENE=<ad>` satırıyla loglanır.
- `--qa-playthrough` (yalnız İlk İkmal) betikli oyun akışı: NavMesh yolları, kapılar, sigorta, mühimmat kutusu, pano ve ışık olayı, Complete + sonuç dosyası, ölümden sonra yeniden yükleme, ipucu kartları, gürültü ve duvar engeli. Her adımı `QA_PLAYTHROUGH` satırıyla loglar, sonda `QA_PLAYTHROUGH_DONE pass=<n> fail=<n>` yazar ve çıkar. Oyuncunun `hint_*` ve `tutorial` PlayerPrefs değerlerini kaydedip sonda geri yükler.
- `--qa-screens-ilkikmal` (yalnız İlk İkmal; `-batchmode`/`-nographics` olmadan, gerçek çizimle) 1600x900 pencerede betikli kameralarla `QaScreens_ilkikmal/01..10_*.png` (+ ek `11_pause_menu.png`) yazar, `QA_SCREENS_ILKIKMAL_DONE` loglar ve çıkar.
- `--qa-freeze` zombileri durdurur; `--qa-ads` nişanı, `--qa-wall` yakın duvar konumunu sabitler (performans ölçümünde kullanılmaz).
- `--perf=10|30|100` sabit tohumlu (12345) performans senaryosu; `--perf-warmup=10 --perf-seconds=60` ile süreler. Sonuçlar exe yanında `PerfResults/perf_<N>_<zaman>.csv/.txt`. Oyuncu ölümsüz, ateş yok, kamera pencere boyunca 2 tur döner. VSync kapalı, kare sınırı yok.
- `--qa-autoquit=SEC` SEC saniye sonra temiz çıkış ister (Player.log: `QA_AUTOQUIT_REQUESTED`).
- `--qa-selftest` gerçek atış/öldürme/ragdoll/saldırı/reload akışını betikle çalıştırır, `QA_SELFTEST` satırları yazar ve çıkar.
- `--arms=lpsp` eski Infima LPSP küp kollarını seçer (varsayılan v0.5b'den beri Slayver Survivalist kolları). `--qa-screens` LPSP ile `QaScreens_lpsp/` klasörüne yazar.
Zamanlı QA argümanları (`--perf`, `--qa-autoquit`, `--qa-selftest`) pencere odakta olmasa da çalışsın diye `runInBackground` açar.

## Kod yerleşimi
`Assets/Scripts`: Sandbox (oyuncu durumu/ateş), PlayerMotor, CameraEffects, ViewmodelPresenter, WeaponState, Damage, NoiseEvents, EffectPool, SfxBank, ZombieBrain/Animator/Ragdoll/Part/Director, Hud, PauseMenu, PerfHarness, QaSelfTest, GameSettings, Layers; İlk İkmal: BootRouter, Interactor/IInteractable/InputLabels, DoorInteractable, FuseItem, AmmoCrate, FusePanel, QuestFuse, HintDirector, PlaytestLog, NavIgnore, ClearanceMarker, QaPlaythrough, QaScreensIlkIkmal. `Assets/Editor`: SandboxBuilder, IlkIkmalBuilder, BoxMeshFactory, LookDevSetup, ZombieFactory, WeaponValidation, SandboxValidation, IlkIkmalValidation.

## Harici varlık kancaları
Dosya varsa kullanılır, yoksa primitive yer tutucu: `Assets/Art/Characters/Zombie/zombie.fbx` (Humanoid), `Assets/Art/Weapons/Pistol/pistol.fbx` (`Muzzle`, isteğe bağlı `Slide`, `Magazine`), `Assets/Art/Characters/Arms/arms.fbx`, `Assets/Audio/<gunshot|dry_fire|mag_out|mag_in|slide|hit_flesh|hit_concrete|zombie_growl|zombie_attack|zombie_death|player_hurt>.wav`. Dokular `Assets/ThirdParty/PolyHaven` (CC0, bkz. LICENSE.md).

## Sınırlar
Ses ve efektler prosedürel yer tutucudur. 10/30/100 zombi performansı bu sürümde **ölçülmedi** (yalnızca duman testi yapıldı). Kapanış donması (Çıkış / Alt+F4) kök nedeni doğrulanmadı; teşhis için `--qa-autoquit` var. Gerçek oyuncu testi bekliyor.

**İlk İkmal sınırları.** Oynanış süresi (yalnız yürüme için hesaplanan yaklaşık 65 s bir tahmindir, ölçüm değil), mühimmat/zombi dengesi (48 başlangıç + 12 kutu = 60 mermi), ipucu okunurluğu, gri kutu ölçülerinin oynanışa uygunluğu ve görsel kalite **ölçülmedi**; bunlar insan testi bekliyor. Betikli QA (`--qa-playthrough`) akışın kilitlenmeden ve doğru durumlarla çalıştığını gösterir; eğlence, zorluk veya anlaşılırlık hakkında bir şey söylemez. İlk İkmal'in gerçek zamanlı ışık sayısı ve diğer ölçümler `Docs/IMPLEMENTATION_REPORT.md` v0.6 bölümündedir.


## v0.4 (görünür eller, fiziksel reload, okunur isabet, animasyonlu zombi)
- Kontroller değişmedi. Reload artık şarjör doluyken-değilse **1,41 s**, şarjör boşken **1,93 s** (mühimmat aktarımı sürenin sonunda tek kez; animasyon/ses durumu değiştirmez).
- Kollar + tabanca: Infima LPSP "Low Poly Shooter Pack - Free Sample" (yalnızca sanat/ses), kod ürettiği Animator controller'larla (`Assets/Generated/Animators`). Dosyalar yoksa eski primitive tabanca kullanılır.
- Zombi: öncelik `Assets/Art/Characters/Zombie/zombie.fbx` > Pxltiger Zombie1/2/3 (klipli, `ZombieDirector.zombiePrefabs`) > Unity Rio (prosedürel poz) > primitive. Hafif isabet = flinch, baş isabeti veya 60 hasar/1,2 s (bekleme 1,5 s) = ağır sendeleme.
- `--qa-screens`: 1600x900 pencerede betikli çalıştırma; exe yanında `QaScreens/01..12_*.png` yazar, `QA_SCREENS_DONE` loglar, çıkar.
- Varlık listesi ve lisanslar: `Assets/ThirdParty/{InfimaLPSP,UnityRio,PxltigerZombie,KevinIglesiasMelee}/LICENSE.md`.
- Proje `ImageConversion`/`ScreenCapture` modülünü içermediği için ekran görüntüleri `Texture2D.ReadPixels` + kendi PNG yazıcımızla alınır (manifest değişmedi).

## v0.5b (gerçekçi kollar varsayılan)
- FPS kolları: Slayver "Survivalist character" çıplak ten kol + gömlek kolu, aynı Handgun_03 ve aynı LPSP animasyonları (UE4 iskeleti ortak). Mühimmat, reload süreleri ve olaylar değişmedi.
- `--arms=lpsp` eski kollara döner. Lisans: `Assets/ThirdParty/Survivalist/LICENSE.md`.

## v0.6 (İlk İkmal: oynanabilir ilk bölüm, gri kutu)
- Sahne sırası: `0 = IlkIkmal`, `1 = CombatSandbox`; `PlayerSettings.bundleVersion = 0.6`. `BootRouter` argümansız açılışta İlk İkmal'i, `--sandbox`/`--perf`/`--qa-selftest`/`--qa-screens` ile CombatSandbox'ı açar.
- Görev: sigortayı depoda bul (`E`), avluda panoya tak, güvenli odaya dön. Durumlar `SearchFuse → CarryingFuse → FuseInstalled → Complete`; her geçiş `QUEST state=<ad> t=<sn>` olarak loglanır. Sonuç kartı 8 s (Enter kapatır).
- İpuçları (`HintDirector`): `move`, `aim`, `reload`, `noammo`, `noise`; aynı anda en fazla bir kart, en fazla 6 s, hatırlatma en erken 20 s sonra ve en fazla 2 kez; öğrenilenler `PlayerPrefs` `hint_<id>`.
- Gürültü: İlk İkmal'de atış sesi 22 m (CombatSandbox 30 m); zombi ile atış noktası arasında duvar varsa etkili yarıçap yarıya iner. Zombiler başlangıçta uyarılmamıştır.
- Oyun testi kaydı: bölüm bitince (veya erken çıkışta kısmi olarak) exe yanına `PlaytestResults/playtest_<zaman>.txt` yazılır ve Player.log'a `PLAYTEST_RESULT ...` satırı eklenir. Betikli QA çalıştırmaları `qa=1` ile işaretlenir; insan testi sonuçlarından ayrılırken bu alana bakın.

## v0.7 (ilk açık bölge, toplama, üretim, kayıt, yeni HUD)
- `Unity.exe -batchmode -quit -projectPath . -executeMethod IlkIkmalBuilder.BuildAll` iki sahneyi ve `Build/Zombosland.exe`yi üretir. Açılacak sahne `Assets/Scenes/IlkIkmal.unity`. Sahneyi elle düzenlemeyin; `Assets/Editor/OpenWorldBuilder*.cs` üzerinden değiştirin.
- Paketler: yalnız yerleşik `com.unity.modules.terrain` ve `terrainphysics` eklendi (editörle gelir). uGUI, TMP, AI Navigation eklenmedi.
- Bölge koordinatı → dünya: `(Rx-46, y, Rz-110)`. Ana yol avlu–istasyon–köprü–su kulesi, orman patikası avlu–kulübe–sığ geçit–köprü.
- Veriler editörden düzenlenir: `Assets/Data/Items`, `Assets/Data/Recipes`, `Assets/Data/ItemDatabase.asset` (builder yalnız eksikleri oluşturur).
- Kayıt: `persistentDataPath/save_v1.json` (sürümlü, atomik). Otomatik: sigorta takılınca ve avluya dönüşte. Esc menüsünde Kaydet / Son kaydı yükle / Yeni oyun. Ölümde son kayıt yüklenir.
- Yeni argümanlar: `--qa-survival`, `--qa-screens-openworld`, `--perf-openworld=N`, `--save-slot=<ad>`.
- Geçici varlıklar: istasyon, kulübe, su kulesi, evler, köprü korkulukları, sopa görünümü kutu/primitive yer tutucu; zemin dokuları ve ikonlar koddan üretilir; huş ve kayalar Dreamplex (Asset Store, bkz. `Assets/ThirdParty/DreamplexFarm/LICENSE.md`).
- Ölçümler ve bilinen eksikler: `Docs/IMPLEMENTATION_REPORT.md` v0.7 bölümü.
