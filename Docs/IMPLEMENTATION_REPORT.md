# İlk sandbox doğrulaması

- Unity 6000.6.5f1 ve Windows standalone modülü yerel kurulumdan kullanıldı. URP 17.6.0 sürümü kurulu editörün URP boş proje şablonu manifestinden alındı; HDRP karşılaştırması yapılmadı.
- `SandboxBuilder.Build`: `Logs/unity-final-build.log`, `Succeeded errors=0`; çıktı `Build/Zombosland.exe`.
- `WeaponValidation.Run`: `Logs/weapon-validation-final.log`, `WEAPON_VALIDATION_PASSED`. Mühimmat, atış aralığı, dolum, boş şarjör, ölümün tek geçişi ve canlı silahın kullandığı sorguyla ince yakın duvar kontrol edildi.
- `SandboxValidation.Run`: `Logs/SandboxValidation.txt`, dört kontrol geçti: sahne referansları, baş/gövde çarpıştırıcıları ve engel. İlk denemede eski test katman maskesi başarısızdı; test maskesi isimli katmanlara güncellenerek yeniden çalıştırıldı.
- Bağımsız görsel/oynanış QA: ana ajan Windows build'inde açılışı, atışta mühimmat azalmasını, zombi vurulma tepkisini, baş vuruşlarıyla ölümü, oyuncu hasarını ve ölüm sonrası yeniden başlamayı gözledi. Son düzeltmeler için görsel inceleme ayrıca sürüyor.
- Tüm dünya ve karakter şekilleri Unity primitive'leriyle bu proje için üretildi; geçici kısa sesler kodla sentezlenir. Unity URP şablon ayarları editörle birlikte gelen kaynaklardır; proje dışından sanat/ses indirilmedi.

Henüz doğrulanmadı: kapsamlı oyun modu testleri, gerçek oyuncu geri bildirimi, performans ve 10/30/100 zombi ölçümleri, URP/HDRP karşılaştırması, platform/Steam yayın uygunluğu. Headbob/sway uygulanmadı; tam el/silah animasyonları, gelişmiş ses/VFX ve zombi engel dolaşımı eksik. Zombiler engellerle çarpışır ancak rota bulmaz. Ağ desteği yok. Hedef 1080p/60 FPS ve 30 zombi ölçülmüş sonuç değildir. QA dondurma modu performans ölçümünde kullanılmaz.

Açık QA: Bir Windows test sürecinde Çıkış ve Alt+F4 sonrası süreç yanıtsız kaldı; Player.log motorun kapanma aşamasına ulaşıyor. DX11 karşılaştırması ve kök neden henüz doğrulanmadı.

## v0.3 uygulama

**Değişenler.** Spec: `Docs/SPEC_v0.3_combat_sandbox_completion.md`. Oyuncu hiyerarşisi `Player > AimPivot > Camera > Viewmodel`; atış ışını AimPivot'tan çıkar, kamera yalnızca görsel ofset (headbob, Perlin sarsıntı, FOV) uygular. Yeni dosyalar: PlayerMotor, CameraEffects, ViewmodelPresenter (sway, ateş tepmesi yayı, reload eğrisi, şarjör/sürgü, duvarda aşağı/geri çekilme), SfxBank (prosedürel ses + 24'lük 3D AudioSource havuzu, `Assets/Audio/*.wav` varsa tercih), EffectPool (namlu alevi + ışık, 16 toz/kıvılcım, 16 kan, 64 decal; çalışma sırasında Instantiate/Destroy yok), Damage (`DamageInfo/HitZone/IDamageable`), NoiseEvents, ZombieBrain (açık durum makinesi), ZombieAnimator (eklemeli prosedürel poz), ZombieRagdoll, ZombiePart, ZombieDirector (çalışma zamanı NavMesh, ceset sınırı 24 / 20 s), Hud + PauseMenu, GameSettings, PerfHarness, QaSelfTest, Layers, QaArgs. `WeaponState` genişledi (`ReloadDuration`, `ReloadProgress`, `TryDryFire`); eski davranış aynı. Editör: SandboxBuilder yeniden yazıldı, BoxMeshFactory (dünya ölçekli UV + tangent), LookDevSetup (doku içe aktarma, PBR, gökyüzü, URP asset, SSAO, post profil), ZombieFactory (primitive kemik iskeleti + FBX kancası + hitbox/ragdoll + prefab). `Zombie.cs`, `Sandbox.Hud.cs` silindi. `Packages/manifest.json`: yalnızca yerleşik modüller eklendi (`ai`, `animation`, `particlesystem`, 1.0.0). Katmanlar: ZombieMover 8, Player 9, Corpse 10, ZombieHitbox 11, Environment 12. Zombi başına yaklaşık 12 kinematik rigidbody + hitbox kapsülü vardır.

**Doğrulama (gerçek sonuçlar).**
- `Logs/v03-weapon.log`: `WEAPON_VALIDATION_PASSED`, 45 `PASS:` satırı (eski testler + ReloadProgress, TryDryFire, çarpanlar 2.0/1.0/0.7, ölü zombiye ikinci vuruş `false`).
- `Logs/v03-build.log`: `ZOMBOSLAND_BUILD_RESULT: Succeeded errors=0`.
- `Logs/v03-scene.log` (+ `Logs/SandboxValidation.txt`): 10 kontrolün tümü PASS, `SANDBOX_VALIDATION_RESULT PASS` (referanslar, hitbox, "Near obstruction", AimPivot hiyerarşisi, ZombieDirector + editörde NavMesh üretimi ve örnekleme, ragdoll kinematik başlangıcı, global Volume + PostProfile, PolyHaven malzemeleri dokulu + dünya ölçekli UV, efekt/ses/gökyüzü).
- `error CS`: 0. İlk derlemede `FindFirstObjectByType` / `FindObjectsSortMode` kullanımları için `warning CS0618` çıktı; `FindAnyObjectByType` / sıralamasız `FindObjectsByType` ile giderildi, son derlemelerde `warning CS` yok.
- Build çalıştırılarak: `--qa-selftest` (Player.log): AimPivot'tan gerçek atış kafaya 100→32 can, ikinci atış öldürdü, ragdoll dinamik oldu, 2 s sonra Corpse katmanında yerde durdu (hipsY≈0,17, hız 0); zombi saldırısı oyuncuyu 100→90 yaptı (0,55 s hazırlıktan sonra); reload ara ilerlemesi 0,53, bitişte şarjör 12. `--perf=10` ve `--perf=100` (3 s ısınma, 10 s pencere) çıktı üretip kendiliğinden çıktı: duman testi, ölçüm değildir (100 zombi: 5193 kare, ort. ≈519 FPS, p99 8,8 ms, maks 17,2 ms; CPU/GPU kare süresi FrameTimingManager'dan raporlandı). `--qa-autoquit=5` Player.log'a `QA_AUTOQUIT_REQUESTED` yazdı ve süreç kendi kendine çıkış kodu 0 ile kapandı. Bir pencere odak dışındayken ilk deneme çıkmadı: `runInBackground` kapalıyken oyun döngüsü duruyordu; zamanlı QA argümanları artık `runInBackground` açar. Bu, uygulama içi "Çıkış / Alt+F4" donmasının kök nedenini açıklamaz.
- Görsel: iki pencere görüntüsü incelendi (açılış görünümü, `--qa-wall --qa-ads`); dokular, gökyüzü, HUD ve silah görünüyor. Kapsamlı görsel QA yapılmadı.

**Yapılmadı / ölçülmedi.** 10/30/100 zombi için 60 s'lik gerçek ölçüm koşusu yapılmadı; hedef 1080p/60 doğrulanmadı. Gerçek ses kaydı, FBX zombi/tabanca/kollar (dosyalar yok; yalnızca kanca yazıldı, FBX yolu test edilmedi), el animasyonu klibi yok. Kapanış donmasının kök nedeni bulunmadı. Ses kalitesi, muzzle flash ve kan/toz efektlerinin görsel kalitesi bir insan tarafından incelenmedi (kod yolları `--qa-selftest` ile hatasız çalıştı, ekran görüntüsü alınmadı). TAA'nın viewmodel üzerindeki hayalet etkisi incelenmedi. Elle oyun testi yapılmadı.

**Bilinen riskler.** (1) Build logunda DoF/Panini post-process pass shader'ları "stripped" uyarısı veriyor (profilde kullanılmıyor; diğer pass'ler çalışıyor). (2) ZombieAnimator ofsetleri zombi uzayında (X sağ, Y yukarı, Z ileri) ve dinlenme pozunda kolların aşağı sarktığını varsayar; Blender FBX T-pozunda ise kol açıları ayarlanmalıdır. (3) 100 zombide ~1200 kinematik rigidbody var; gerçek ölçüm yapılana kadar fizik maliyeti bilinmiyor. (4) NavMesh yalnızca `Environment` katmanındaki sahne çarpıştırıcılarından üretilir; yeni dünya nesneleri bu katmana konmalı. (5) Prosedürel sesler yer tutucudur. (6) `NavMeshBuilder` Unity 6.6'da hâlâ derleniyor ve çalışıyor, ileride kullanımdan kalkabilir.


## v0.4 uygulama
**Spec:** `Docs/SPEC_v0.4_hands_reload_zombie.md` (bölüm 8 eki dahil). Yeni: `ViewmodelFactory` (editör), `ViewmodelAnimationEvents`, `QaScreens`; yeniden yazılan: `ViewmodelPresenter` (animasyonlu + prosedürel yol), `ZombieAnimator` (klip kipi + prosedürel kip, yön hedefleme, flinch/ağır itme), `ZombieFactory` (Pxltiger/Rio/primitive), WeaponState (1,41/1,93 s), EffectPool (12'lik kovan havuzu), SfxBank (yeni Sfx kimlikleri, çoklu klip, `Has`), ZombieBrain (poise/flinch, animator köprüsü), ZombieDirector (`zombiePrefabs`). Geçici `LpspProbe`/`RioProbe` silindi.

**Doğrulama (gerçek sonuçlar, bu oturumda çalıştırıldı).**
- `Logs/v04-build.log`: `ZOMBOSLAND_BUILD_RESULT: Succeeded errors=0`; `error CS`/`warning CS` satırı: 0.
- `Logs/v04-WeaponValidation.log`: `WEAPON_VALIDATION_PASSED` (58 `PASS:` satırı; 1,41/1,93 s sınır testleri, poise testleri dahil).
- `Logs/v04-SandboxValidation.log`: `SANDBOX_VALIDATION_RESULT PASS` (12 kontrol; yeni: `viewmodel_arms_pistol_animators_and_materials`, `zombie_prefabs_model_animator_material_and_limbs`).
- `Logs/v04-player-selftest.log`: `QA_SELFTEST kill dead=True`, saldırı 100->90, `reload ... duration=1,41 ... ammo=12`, `QA_SELFTEST_DONE`; NullReference / "has no receiver" / missing yok. İlk denemede animasyonlu zombi başını kaçırdığı için ikinci/üçüncü atış ıskaladı; test artık her takip atışından önce güncel baş kemiğine nişan alır.
- `Logs/v04-player-screens.log`: `QA_SCREENS_DONE`, 12 PNG (`Build/QaScreens/`), hata satırı yok.
- Ölçülen klip verisi (build raporu): root-motion averageSpeed walk=0,27, walk1=0,27, run=3,70 m/s (ayak tabanı hızı: 0,16 / 0,15 / 1,99 m/s, yalnızca çapraz kontrol); Z_Attack uzunluğu 1,00 s, vuruş anı 0,63 s (normalize 0,63) -> durum hızı 1,15; HumanM@CombatDamage01 1,00 s.
- Görsel inceleme: 12 görüntü tek tek incelendi (bkz. ajan raporu); namlu ağzı efekti ve kovan yönü eksen düzeltmesinden sonra doğrulandı (`eject right(cam)=(1,0,0)`, `muzzle fwd(cam)=(0,0,1)`).

**Ölçülmedi / yapılmadı.** Performans (zombi sayısı, FPS) ölçülmedi; elle oyun testi yapılmadı; ses kalitesi insan tarafından dinlenmedi; kapanış donması kök nedeni hâlâ bilinmiyor; 100 zombide animasyonlu rig maliyeti ölçülmedi. Yürüyüş ayak kayması (AnimRate sınırları 0,6-1,6) gözle/ölçümle doğrulanmadı.


## v0.5a Survivalist kol denemesi
**Spec:** `Docs/SPEC_v0.5a_survivalist_arms_trial.md`. Varsayılan davranış değişmedi (argüman yoksa LPSP kolları); `--arms=survivalist` Survivalist FPS kollarını aynı tabanca, sahne, ışık ve kamera ile gösterir.

**Kullanılan yaklaşım: 1 (generic yol eşleşmesi), nedeni ölçümle.** Geçici editör tanısı (`Zombosland > Diagnostics > Dump Arms Hierarchies`, çıktı `Logs/arms-hierarchy.txt`, `Logs/v05a-diag.log`) şunu gösterdi: LPSP kol FBX'i (Generic, 68 kemik) ile Survivalist FBX'i (Humanoid, 61 kemik SMR) aynı UE4 iskeletini taşıyor; `root` altındaki tüm kemiklerin yerel konum/dönüşü ve dünya konumları birebir aynı (örn. `hand_r` dünya (0.5665, 1.1168, -0.0034), `pelvis` (0, 0.9675, -0.0106)), yani ölçek uyumlu (`fileScale 0,01` ile import sonrası lossyScale 1, `globalScale` 1; 100x sapma yok). Tek fark: LPSP'de `Armature` (yerel dönüş Euler(270,0,0)) sarmalayıcısı ve `root/ik_hand_root/{ik_hand_gun, ik_hand_l, ik_hand_r}` yardımcı kemikleri var; Survivalist'te `root` doğrudan prefab kökü altında, `ik_*` yok. LPSP klipleri Generic ve yol tabanlı olduğundan Survivalist örneğinde `Armature` sarmalayıcısı ile `ik_hand_*` kemikleri LPSP'nin yerel dönüşümleriyle yeniden oluşturuldu (root'un dünya dönüşü sarmalayıcıdan önce/sonra (270,0,0), konum farkı 0). Humanoid retarget (2) ve Kevin Iglesias yedeği (3) gerekmedi. `ModelImporter`/`.meta` değiştirilmedi. Survivalist `ik_hand_gun`, bind pozunda `hand_r` ile aynı yerde/yönde (bağıl konum 0, bağıl dönüş 0), bu yüzden tabanca ofseti (konum 0, Euler(90,0,0)) LPSP ile aynı kaldı; ek ofset gerekmedi.

**Değişen/yeni dosyalar.**
- `Assets/Editor/ViewmodelFactory.cs`: `BuildSurvivalist`, `AttachPistol` (tabanca+şarjör+Muzzle/Eject anchor ortak yardımcı), `SurvLit`; `ViewmodelRig` genişledi. Survivalist set `Arms_Survivalist` adıyla viewmodel altında, varsayılan pasif. Yalnızca `FPS_HANDS` altındaki mesh'ler tutulur (üçüncü şahıs gövde modülleri silinir); görünür: `SK_Miliary_FPS_Arms` (çıplak ten) + `SK_Miliary_Military_FPS_Shirt3`; `..._Gloves1` pasif (`SurvivalistUseGloves` sabiti). Animator: LPSP controller'ları (`VM_Arms`, `VM_Pistol`) ve LPSP generic avatarı; mühimmat/reload süresi/olaylar (`ViewmodelAnimationEvents`) aynı.
- Malzemeler (kodla, `Assets/Generated/Materials/`): `VM_Surv_Skin` (paketteki `URP_FPS_Hands1_Skin1.mat` aslında Hands3 dokularını kullanıyor: color_skin1 + nmap + ao), `VM_Surv_Gloves`, `VM_Surv_Shirt` (Shirt3 Skin1). Gölge kapalı, `updateWhenOffscreen` açık, collider yok, layer 0.
- `Assets/Scripts/ViewmodelPresenter.cs`: `AlternateArms` sınıfı, `defaultArmsRoot`/`survivalist` alanları, `ArmsSet`; seçim `Awake` içinde (referanslar kullanılmadan önce) yapılır. `QaArgs.GetString`. `QaScreens.cs`: `--arms=survivalist` ile `QaScreens_survivalist/`, `QA_SCREENS arms=` log satırı. `QaSelfTest.cs`: `QA_SELFTEST arms=` satırı. `SandboxBuilder.cs`: presenter'a iki set atanır. `SandboxValidation.cs`: yeni kontrol `viewmodel_survivalist_arms_set`.
- `Assets/Editor/ArmsDiagnostics.cs`: yalnızca tanı amaçlı menü öğesi (bilerek bırakıldı, işaretli).

**Çalıştırılan komutlar ve sonuçlar (hepsi bu oturumda gerçekten çalıştırıldı).**
- `Unity.exe -batchmode -quit -projectPath E:\ZOMBOSLAND -executeMethod SandboxBuilder.Build -logFile Logs\v05a-build.log` → `ZOMBOSLAND_BUILD_RESULT: Succeeded errors=0`; `error CS`/`warning CS` yok. (İlk build'de tabanca SMR'sine yanlışlıkla ten malzemesi atanmıştı; malzeme döngüsü yalnızca `FPS_HANDS` ile sınırlanıp yeniden build alındı.)
- `Build\Zombosland.exe --qa-screens -logFile Logs\v05a-screens-lpsp.log` → `QA_SCREENS_DONE`, `Build\QaScreens\` 12 PNG.
- `Build\Zombosland.exe --qa-screens --arms=survivalist -logFile Logs\v05a-screens-surv.log` → `QA_SCREENS arms=survivalist`, `QA_SCREENS_DONE`, `Build\QaScreens_survivalist\` 12 PNG.
- `Build\Zombosland.exe --qa-selftest [--arms=survivalist] -logFile Logs\v05a-selftest-lpsp.log / -surv.log` → ikisinde de `QA_SELFTEST_DONE`; atış 12->11, baş vuruşu 100->32, öldürme, ragdoll Corpse katmanı, saldırı 100->90, reload 1,41 s ve şarjör 12; NullReference yok. Survivalist: `QA_SELFTEST arms=survivalist armsAnimatorActive=True`.
- `-executeMethod WeaponValidation.Run` → `WEAPON_VALIDATION_PASSED` (58 `PASS:`), `Logs/v05a-WeaponValidation.log`. `-executeMethod SandboxValidation.Run` → `SANDBOX_VALIDATION_RESULT PASS` (yeni kontrol dahil), `Logs/v05a-SandboxValidation.log`.

**Görsel inceleme (PNG'lere tek tek bakıldı; Survivalist, LPSP ile karşılaştırıldı).** Karşılaştırma: `Build\QaScreens\` (LPSP) ve `Build\QaScreens_survivalist\`.
- 01_hip: sağ el kabzayı makul kavrıyor, el–silah boşluğu veya iç içe geçme yok, bilek kırılmıyor; destek eli ekran dışında (LPSP ile aynı kadraj). Kol kumaşı/kol kesiği görünmüyor. Sorun yok.
- 02_ads: namlu/arpacık kamera ekseninde, hedef (zombi gövdesi) namlu arkasında kısmen kapalı (LPSP ile aynı nişan hizası). Çıplak ön kol LPSP'nin küp kolundan daha kalın/uzun görünüp alt orta ekranın büyük kısmını kaplıyor; okunurluğu LPSP'den biraz düşük ama hedef çevresi açık.
- 03_fire: namlu alevi + ışığı ten üzerinde beyaz parlak lekeler yapıyor (LPSP'de de benzer); parmak/kabza teması korunuyor.
- 05_reload_mid / 06_reload_late / 08_empty_reload: şarjör hareketi ve iki el animasyonu çalışıyor; sağ el parmakları kabzayı sarıyor, sol el şarjörü tutuyor. 06 ve 08'de sol baş parmak/işaret parmağı şarjör ve kabza panelinin üzerinden geçerken hafif iç içe geçme/yüzey kesişmesi olasılığı var (el boyutu LPSP küp elinden farklı olduğu için); gözle belirgin bir bozulma görülmedi ama yakın plan ölçülmedi. Bilek kırılması yok.
- 07_empty_slideback: sürgü geride, el kabzada, sorun görülmedi.
- 10_wall: QA konumunda kamera duvara bakıyor ve ekran duvar dokusuyla doluyor (hem LPSP hem Survivalist'te aynı); bu görüntü kol/silah geri çekilmesini göstermiyor, dolayısıyla duvarda Survivalist kolların kesilip kesilmediği bu görüntüyle doğrulanamadı.
- Kamera içi mesh görünmesi: yakın kırpma düzleminde kol kesiği gözlenmedi (yalnızca verilen görüntülerde).

**Yapılmadı / ölçülmedi.** Duvar yakınında kolların geri çekilmesi (10_wall bu amaca uygun değil), parmak–kabza kesişiminin sayısal ölçümü, koşma/zıplama sırasındaki görünüm, performans (Survivalist skin mesh maliyeti), gerçek oyuncu testi yapılmadı. Eldivenli varyant (`SurvivalistUseGloves`) ve Skin2/Skin3 dokuları denenmedi. 04, 09, 11, 12 görüntüleri bu denemede ayrıntılı incelenmedi (zombi odaklı).

## v0.5b Survivalist kolları varsayılan

Kullanıcı `--arms=survivalist` ile oyunu denedi ve varsayılan yapılmasını onayladı (2026-11-08).

**Değişiklik.** Sahne yapısı aynı (iki kol düzeneği de üretilir, Survivalist sahnede pasif kurulur). `ViewmodelPresenter.Awake` artık `--arms` verilmezse Survivalist'i etkinleştirir; yalnızca `--arms=lpsp` LPSP kollarını bırakır. Survivalist kurulamazsa LPSP otomatik kalır (`ArmsSet = "lpsp"`). `QaScreens` varsayılanı `Build\QaScreens\`, LPSP `Build\QaScreens_lpsp\`. `SandboxValidation` mesajları yeni anlama göre düzenlendi (kontrol mantığı aynı). Dosyalar: `ViewmodelPresenter.cs`, `QaScreens.cs`, `SandboxValidation.cs`, `README.md`.

**Çalıştırılan komutlar ve sonuçlar.**
- `SandboxBuilder.Build` → `ZOMBOSLAND_BUILD_RESULT: Succeeded errors=0` (`Logs/v05b-build.log`).
- `SandboxValidation.Run` → `SANDBOX_VALIDATION_RESULT PASS`; `WeaponValidation.Run` → `WEAPON_VALIDATION_PASSED`.
- `--qa-screens` → `QA_SCREENS arms=survivalist`, 12 PNG, `QA_SCREENS_DONE`; `--qa-screens --arms=lpsp` → `QA_SCREENS arms=lpsp`, 12 PNG, `QA_SCREENS_DONE`.
- `--qa-selftest` ve `--qa-selftest --arms=lpsp` → ikisinde de atış 12->11, zombi 100->32, öldürme, Corpse katmanı, saldırı 100->90, reload 1,41 s / 12, `QA_SELFTEST_DONE`.
- 02_ads iki klasörde gözle kontrol edildi: varsayılan Survivalist, LPSP seçeneği küp kollar; nişan hizası aynı.

**Ölçülmedi.** v0.5a'daki liste geçerli: duvar yakını geri çekilme, koşma/zıplama görünümü, performans, gerçek oyuncu testi (kullanıcının kendi denemesi dışında). `Build\QaScreens_survivalist\` v0.5a'dan kalan eski çıktıdır.

## v0.6 İlk İkmal (oynanabilir ilk bölüm, gri kutu)

**Spec:** `Docs/SPEC_v0.6_ilk_ikmal.md` (1–6. adımlar: silah, sahneler + BootRouter, gri kutu harita, etkileşim, görev + ışık olayı, ipuçları + gürültü + yerleşim + oyun testi kaydı + QA + belgeler). Yedek: `Backups/pre-v0.6_2026-11-08` (dokunulmadı). Bu proje git deposu olmadığı için aşağıdaki liste yedekle karşılaştırılarak çıkarıldı (`.meta` dosyaları hariç).

**Değişen / yeni dosyalar.**
- Yeni (`Assets/Scripts`): `BootRouter`, `Interactor`, `IInteractable` (+ `IBlockedLabel`), `InputLabels`, `DoorInteractable`, `FuseItem`, `AmmoCrate`, `FusePanel`, `QuestFuse`, `HintDirector`, `PlaytestLog`, `NavIgnore`, `ClearanceMarker`, `QaPlaythrough`, `QaScreensIlkIkmal`.
- Değişen (`Assets/Scripts`): `WeaponState` (kurucu `startReserve`, yarı otomatik aralık 0,25 s, `QaSetReserve`), `Sandbox` + `Sandbox.Weapon` (yarı otomatik tık, `startReserve` / `noiseRadius` serileştirilmiş alanları, `RestartLevel`, QA kancaları), `Damage` (kafa ×3), `Layers` (`Interactable` = 13, `ShotMask` / `SightMask` bu katmanı dışlar), `NoiseEvents` (`ZombieHeard`), `ZombieBrain` (`wanderRadius`, `CanSeePlayer`, `OnNoise` engel testi), `ZombieDirector` (`NavIgnore` işaretli kapı yaprakları NavMesh üretiminden çıkar), `Hud` (görev satırı, `[E]` dünya etiketi, sonuç kartı, ipucu kartı, İlk İkmal'de F1 şeridi), `PauseMenu` ("Bölümü yeniden başlat", öğretici düğmeleri), `GameSettings` (`Tutorial`), `PerfHarness` (`--qa-playthrough` başlatma), `QaSelfTest` (tek atışla öldürme, `QA_SELFTEST kill shots=N`).
- Yeni (`Assets/Editor`): `IlkIkmalBuilder`, `IlkIkmalValidation`. Değişen: `SandboxBuilder` (ortak yardımcılar `internal static`, `Interactable` katmanı kaydı, iki sahneli build ayarı), `WeaponValidation`, `SandboxValidation` (beklenen değerler spec §1 ile güncellendi: 36 ve 72 rezerv, ×3 / ×1 / ×0,7).
- Proje: `Assets/Scenes/IlkIkmal.unity` (yeni, üretilmiş), `CombatSandbox.unity` (yeniden üretildi), `ProjectSettings/TagManager.asset` (katman 13 `Interactable`), `EditorBuildSettings.asset` (iki sahne), `ProjectSettings.asset` (`bundleVersion` 0.4 → 0.6). Yeni varlık eklenmedi; malzemeler ve sesler CombatSandbox'tan yeniden kullanıldı.
- Belgeler: `README.md`, bu bölüm, `Docs/QA_ACCEPTANCE.md` ("İlk İkmal" bölümü).

**Zombi yerleşimi (§7).** 9 zombi (8 rota + 1 isteğe bağlı), `ZombieFactory` prefab'larıyla (`SandboxBuilder.BuildDirectorAndZombies` ile aynı boru hattı), sabit tohumlu (60601) yön: S1 (−4, −14) ve (−8, −10), S2 (2, 29), (−6, 35), (24, 33), S3 (33, 1), (35, 2,5), (38, −26), C (−23, 1). `wanderRadius`: S1/S2 = 3, S3/C = 2; `ZombieBrain` varsayılanı 6 (CombatSandbox değişmedi). Hepsi uyarılmamış (Wander) başlar. Builder her konumu editörde üretilen NavMesh'e örnekler: dokuzunun da sapması 0,00 m, **hiçbiri taşınmadı** (`Logs/v06c-build.log`, `ILKIKMAL_BUILD_NOTE: zombie ...`). `IlkIkmalValidation` aynı örneklemeyi 0,5 m eşiğiyle yeniden yapar.

**Doğrulama (gerçek sonuçlar, bu oturumda çalıştırıldı; Windows, Unity 6000.6.5f1).**
- `Unity.exe -batchmode -quit -projectPath E:\ZOMBOSLAND -executeMethod IlkIkmalBuilder.BuildAll -logFile Logs\v06c-build.log` → `ZOMBOSLAND_BUILD_RESULT: Succeeded errors=0`; `error CS` ve `warning CS` satırı yok.
- `WeaponValidation.Run` (`Logs/v06c-WeaponValidation.log`) → `WEAPON_VALIDATION_PASSED`, 68 `PASS:` satırı. `SandboxValidation.Run` (`Logs/v06c-SandboxValidation.log`) → `SANDBOX_VALIDATION_RESULT PASS`, 14 kontrol. `IlkIkmalValidation.Run` (`Logs/v06c-IlkIkmalValidation.log`) → `ILKIKMAL_VALIDATION_RESULT PASS`, 11 kontrol, `SKIP` satırı yok (yeni: `hint_director_single_and_wired`, `nine_zombies_placed_not_alerted_wander_radius_and_noise_radius`).
- `Build\Zombosland.exe --qa-playthrough -logFile Build\v06c-playthrough.log` → `QA_PLAYTHROUGH_DONE pass=52 fail=0`; `SKIPPED` / `FAIL` satırı yok. Kapsam: NavMesh yolları (kapı kapalı: A dışı → içi `PathPartial`; açık: A kapısı → D girişi 69,0 m, D yan kapı → pano 83,1 m, F → A 60,3 m), kapılar, sigorta, mühimmat kutusu (36 → 48, ikinci basış etkisiz), D yan kapı iki yönden, pano (sigortasız "Sigorta gerekli"; sigortayla `FuseInstalled`, ışık olayı sayacı 1), Complete + sonuç dosyası (`result=complete qa=1`), ölüm ve yeniden yükleme (görev, sigorta, rezerv 36, kapılar, ışıklar, NavMesh), öğrenilmiş `move` ipucunun ölümden sonra geri gelmemesi, `reload` / `noammo` / `aim` / `noise` kartları, öğretici kapalı, ipucu sıfırlama, kart sayısı (`frames=9381 max=1 violations=0`), gürültü (duvar arkasında 15 m: `Idle`, açıkta 15 m: `Chase`, `ZombieHeard=1`; yarıçap yarıya inmesi: 9,5 m'de duvar arkası zombi `Chase`, `ZombieHeard=2`).
- `Build\Zombosland.exe --qa-screens-ilkikmal -logFile Build\v06c-screens-ilkikmal.log` (pencereli, gerçek çizim) → `QA_SCREENS_ILKIKMAL_DONE`; `Build\QaScreens_ilkikmal\` içinde 10 PNG + ek `11_pause_menu.png`, 1600x900, yaklaşık 1,5–3,4 MB; `hint cards: frames=6253 violations=0 maxCards=1`.
- Gerileme: `--qa-selftest` → `BOOT_SCENE=CombatSandbox`, `QA_SELFTEST kill shots=1`, `QA_SELFTEST attack playerHealth 100->90 after 0,55s`, `QA_SELFTEST reload began=True duration=1,41 ... ammo=12`, `QA_SELFTEST_DONE`. `--qa-screens` → `BOOT_SCENE=CombatSandbox`, `QA_SCREENS_DONE`, 12 PNG. `--perf=10 --perf-warmup=2 --perf-seconds=5` → `PERF_DONE`, CSV yazıldı (duman testi: 2780 kare, ortalama 555,8 FPS; performans iddiası değildir). `--qa-autoquit=10` (argümansız açılış) → `BOOT_SCENE=IlkIkmal`, `QA_AUTOQUIT_REQUESTED after 10.0 s`, `PLAYTEST_RESULT result=partial qa=1 ...`; exception yok.

**Gerçek zamanlı ışık sayısı (ölçüm: `ILKIKMAL_BUILD_NOTE` ve `IlkIkmalValidation`).** Sahnede 1 yönlü + 5 nokta ışık (`Depot_Lamp`, `Bench_Lamp`, `A_Emergency`, `A_Main`, `Panel_Lamp`); başlangıçta açık: güneş + 3 nokta, ışık olayından sonra güneş + 4 nokta. Ayrıca `EffectPool` çalışma anında 4 havuzlu namlu ışığı oluşturur (yalnızca etkin olanlar sayılır). Radyo lambası yalnızca emisiftir.

**§3 ve diğer ölçülerden sapmalar.** Harita ölçülerinde bu adımda değişiklik yapılmadı; builder'ın ölçtüğü açıklıklar: A kapısı 2,00 m, depo girişi 2,20 m, D yan kapı 2,00 m, eczane bağlantısı 4,00 m, E koridoru ve kolu 3,50 m, F girişi 3,50 m, alt yol 8,00 m, ana sokak 11,00 m, raf araları 6,60 / 9,20 / 6,60 m (en az 2,4), F şeridi 5,00 m, minibüs yan geçişleri 3,34 / 3,34 m (en az 3,0). Kapı açıklıkları 0,4 m'lik duvar kalınlığıyla modellendi (A kapısı x −26…−25,6; D yan kapı x 14…14,4; depo girişi z 20,6…21); ana sokak kuzeyde z 20,6'da biter (spec ≈21). Bunların oyunda uygun olup olmadığı insan testiyle bakılacak. Spec'te açık olmayıp bu adımda verilen kararlar:
- `aim` kartı yalnızca zombi gerçekten görünürken (SightMask çizgisi açık) gösterilir; görüş konisi yarım açı 30° alındı; zombi uygunluktan çıkınca kart 1,5 s sonra kapanır (en çok 6 s kuralı aynen geçerli). Spec yalnızca mesafe ve koni der.
- Öncelik sınıfı "geçerli etkileşim" olarak `reload` alındı (tabloda ayrı bir etkileşim kartı yok; `[E]` etiketi kart sayılmaz): `noammo` > `reload` > `move` > `aim` > `noise`.
- `ZombieHeard` yalnızca zombi `Idle`/`Wander`'dan `Chase`'e geçtiğinde tetiklenir; zaten kovalayan ama oyuncuyu görmeyen zombinin hedefi güncellenir, olay çıkmaz.
- Gürültü ışını atıcıdan 0,5 m ileriden başlar (oyuncunun kendi kapsülüne çarpmasın diye) ve yalnızca zombi yarıçapın yarısından uzaktaysa atılır (yakındakiler engelden bağımsız duyar).
- `move` kartı "girdisiz" için hem hareket tuşu girdisini hem konum değişimini sayar; toplam yürüme, ışınlanma gibi kare başına 2 m'den büyük sıçramalar hariç yatay yer değiştirmedir.
- Öğretici düğmeleri yalnızca İlk İkmal'in Esc menüsünde görünür (CombatSandbox'ta ipucu kartı yok); panel o durumda 520 → 590 px.
- Oyun testi kaydındaki "hareketsiz/ilerlemesiz aralık": oyuncunun oyun süresinde (duraklatma sayılmaz) aynı noktanın 1,5 m çevresinde 20 s veya daha uzun kaldığı aralık sayısı; her aralık bir kez sayılır, Complete'ten sonra sayım durur. Erken çıkış dosyası `playtest_<zaman>_partial.txt` adını taşır.
- `--qa-screens-ilkikmal` istenen 10 görüntüye ek olarak duraklat menüsünü gösteren `11_pause_menu.png` üretir.

**Ekran görüntüsü gözlemleri (PNG'lere bakıldı; bunlar bir insan görsel kabulü değildir).**
- Etiketler çalışıyor: 03'te `[E] Sigortayı al`, 04'te `[E] Kapıyı aç`, 06'da `[E] Sigortayı tak` ve sol üstte "Sigortayı panoya tak" görünür; 09'da `[R] Yeniden doldur` kartı mühimmat paneline yapışık, 10'da "Elektrik geri geldi" satırı ve ekran ortasında yaklaşık 15 m'de bir zombi var. Kart/etiket hiçbir karede nişangâhın 120 px yakınına çizilmedi (QA: kart merkezden yaklaşık 725–755 px uzakta).
- Siyah kare veya duvarın içine giren kamera yok. 01 (A kapısından B'ye) kompozisyonu zayıf: açılmış yeşil kapı yaprağı sağ üçte birlik kısmı kaplıyor, B sokağı uzakta karanlık bir açıklık olarak kalıyor. 03'te tezgâh kameraya çok yakın ve üstten görünüyor.
- 04 (D yan kapı, içeriden) ve özellikle 06 (pano) çok karanlık: pano ve sigorta yuvası ancak etiket ve sarı yüzey sayesinde seçiliyor; `Panel_Lamp` olaydan önce kapalı olduğu için tasarım gereği ama okunurluk insan testinde sorun çıkarabilir.
- 07 / 08 (A iç mekân, olaydan önce / sonra): önce soğuk mavi acil ışığı, sonra sıcak tavan ışığı ve yanan radyo lambası görünüyor; fark okunur ama ılımlı, oda yine karanlık.
- 11: duraklat menüsünde iki öğretici düğmesi ve yeniden başlat/çıkış düzgün sığıyor, taşma yok.

**Bilinen sorunlar / açık riskler.**
- Gri kutu aydınlatması: avlu (pano), D yan kapı çevresi ve depo derinliği karanlık; denge ve okunabilirlik için insan testi gerekli.
- QA çalıştırmaları `Build/PlaytestResults/` altına `qa=1` işaretli dosyalar yazar ve `tutorial` PlayerPrefs anahtarını kaydeder; `hint_*` değerleri `--qa-playthrough` ve `--qa-screens-ilkikmal` sonunda eski haline getirilir, `--qa-autoquit` gibi diğer çalıştırmalarda ipucu bayrakları gerçek oyun gibi güncellenebilir.
- OnGUI tabanlı kart konum denetimi yalnızca `-batchmode` olmadan çalışır (`-batchmode` ile OnGUI çizilmez; playthrough orada o denetimi atlar).
- Zombilerin kapı/raf/minibüse takılması, gerçek rotada kovalama ve kapı açıldıktan sonra yol bulma betikle yalnızca NavMesh yolu olarak sınandı; canlı zombi davranışı insan testinde izlenmeli.
- Kapanış donması (Çıkış / Alt+F4) kök nedeni hâlâ doğrulanmadı; bu sürümde incelenmedi.

**Ölçülmedi / yapılmadı (açık liste).** Oynanış süresi (≈65 s yalnız yürüme hesabı ölçüm değildir) ve gerçek oyuncu süresi; mühimmat ve zombi dengesi (48 + 12 = 60 mermi ekonomisi); ipucu kartlarının anlaşılırlığı, zamanlaması ve okunabilirliği (yalnızca konum/sayı kuralları betikle sınandı); sonuç kartının ve görev satırının okunabilirliği; İlk İkmal sahnesinde 1080p/60 FPS veya herhangi bir performans ölçümü (yalnızca CombatSandbox'ta `--perf=10` 5 s duman testi); dokuz zombiyle CPU/GPU kare süresi; 1600x900 dışındaki çözünürlüklerde HUD yerleşimi; ses/VFX kalitesinin insan incelemesi; gerçek oyuncu testi (3–5 kişi); ışık olayının oyuncuya ödül olarak hissettirdiği etki. `Docs/QA_ACCEPTANCE.md` "İlk İkmal" bölümündeki insan testi maddelerinin hepsi `Beklemede`.

**Ana ajan kalite kontrolü (8 Kasım).** Ekran görüntüleri incelendi. `Hud.cs` içinde `GUIUtility.RotateAroundPivot` döndürme merkezini ekran pikseli olarak beklerken ölçekli HUD koordinatı alıyordu; 1080p dışındaki çözünürlükte `[E]` bağlantı çizgisi namlu yanında kopuk bir çapraz çizgi olarak görünüyordu ve aynı hata v0.5b öncesinden kalan isabet işaretini de kaydırıyordu. Döndürme merkezi `GUI.matrix` ile ekran uzayına çevrildi (iki satır). Sonra: `SandboxBuilder.BuildOnly` → `ZOMBOSLAND_BUILD_RESULT: Succeeded errors=0`; `--qa-screens-ilkikmal` → `QA_SCREENS_ILKIKMAL_DONE`, 03 ve 06'da çizgi etikete doğru bağlanıyor; `--qa-playthrough` → `QA_PLAYTHROUGH_DONE pass=52 fail=0`; `--qa-selftest` → `QA_SELFTEST_DONE`. İsabet işaretinin 1080p dışı görünümü gözle doğrulanmadı.
- Görsel notlar (gözle, ölçüm değil): F avlusu ve pano çok karanlık; görev nesnesi sarı rengiyle seçiliyor ama okunurluk insan testinde izlenmeli. 03 karesindeki `[Sağ fare] Nişan al` kartı, betiğin 02'den 1,5 s içinde ışınlanmasından kalan bir QA artefaktıdır.


## v0.7 — İlk açık bölge (2026-11-08)

**Durum doğrulama.** Unity 6000.6.5f1, URP 17.6.0, eski Input Manager, IMGUI, runtime NavMesh. Kayıt, envanter ve stamina sistemi yoktu; stamina eklenmedi ve HUD'da gösterilmiyor. Değişiklik öncesi `--qa-playthrough` 52/0.

**Betikli testler (son derleme).** `--qa-survival` 98/0 (son derlemede 6 çalıştırmanın 6'sı; önceki derlemelerde bir kez `melee_near` kaçırdı, kök nedeni bulunamadı ve sonraki 10 çalıştırmada tekrarlamadı), `--qa-playthrough` 52/0, `--qa-selftest` tamamlandı, `IlkIkmalValidation` 12/12, `SandboxValidation` PASS, `WeaponValidation` PASS. Bunlar akışın ve durumların doğruluğunu gösterir; eğlence, zorluk, anlaşılırlık hakkında bir şey söylemez.

**Performans (`--perf-openworld=N`, Windows standalone, 1920×1080 pencere, VSync kapalı, ana yol 452 m, 60 s; Ryzen 5 7600X, RTX 4080 SUPER, 31,8 GB RAM, Direct3D 12).** Ölçüm, yalnız ekran görüntüsü betiği ve editör doğrulayıcısı farklı bir önceki derlemede alındı.

| Kovalayan zombi (sahnedeki 24 zombi ayrıca var) | Ortalama FPS | %1 düşük FPS | CPU ms | GPU ms | p99 ms | >33 ms kare |
|---|---:|---:|---:|---:|---:|---:|
| 10 | 214,3 | 105,8 | 4,67 | 1,49 | 9,11 | 0 |
| 30 | 204,4 | 103,4 | 4,90 | 1,57 | 9,11 | 0 |

GPU zamanı karelerin yaklaşık %70'inde raporlandı. Sonuçlar yalnızca bu güçlü donanım için geçerlidir; orta seviye donanım ölçülmedi.

**Çözünürlük.** 1280×720, 1920×1080, 2560×1440 ve 21:9 olarak 2560×1080 gerçek ekran görüntüsüyle kontrol edildi (`Build/QaScreens_openworld/`). Monitör 3440×1440 desteklemediği için o çözünürlük test edilmedi.

**Ölçülmeyenler.** İnsan oyun testi, gerçek fare/klavye ile UI kullanımı (girdi kapıları durum üzerinden test edildi, IMGUI tıklamaları betiklenmedi), 15–20 dakikalık ilk döngü süresi, ses, oyun dengesi.

**Bilinen görsel eksikler.** Sığ geçitte su kenarı basamaklı; binalar ve sopa yer tutucu; zemin dokuları prosedürel; gökyüzü üst bölgede parlak; kulübe duvarları koyu.

**Kararlar.** Sigorta takıldığında görev biter; v0.6'daki "güvenli odaya dön" adımı ve sonuç kartı kalktı. Tabanca sabit ekipmandır, çantada yer kaplamaz; mühimmat `WeaponState.Reserve` üzerinde kalır ve çantada satır olarak görünür. Avlu, kapılar için 2 barikat ve 1 sandık yuvası içerir. NavMesh editörde 0,125 m voksel ile bake edilir.


## v0.7 ek — Gerçek ellerle balta denemesi (2026-11-08)

**Spec:** `Docs/SPEC_v0.7_axe_viewmodel.md`. Varlık: Poly Haven `wooden_axe_02` (CC0, 2.218 üçgen, 2K PBR). Klipler LPSP iskeletinde Blender'da üretildi; kaynak `SourceArt/Axe/`. Sopa ve tabanca akışı değişmedi. Tarif eklenmedi; `--give-axe` yalnızca elle deneme içindir. Hasar 60, menzil 2,0 m, süre 1,0 s geçici değerdir, denge kararı verilmedi.

**Düzeltme (QA'da yakalandı).** İlk derlemede kollar T-pozunda kaldı: Unity tek üst düğümlü FBX'te `Armature` düğümünü düşürdüğü için klip yolları bağlanmıyordu. `ModelImporter.preserveHierarchy = true` ile çözüldü.

**Komutlar ve sonuçlar (son derleme).** `IlkIkmalBuilder.BuildAll` ile sahneler yeniden üretildi, sonra `SandboxBuilder.BuildOnly`; ikisi de `Succeeded errors=0`.
- `--qa-axe` PASS: bilek, dirsek ve balta konumları Blender değerleriyle 0,1 mm içinde aynı (bekleme ve vuruş karesi); gerçek vuruş yolu ve tabancaya dönüş doğru. Görüntüler: `Build/QaAxe/` (8 PNG, gözle incelendi).
- `--qa-playthrough` 52/0, `--qa-selftest` tamamlandı, `IlkIkmalValidation`, `SandboxValidation`, `WeaponValidation` PASS, `--give-axe` istisnasız açıldı.
- `--qa-survival` 97/1 (6 çalıştırmanın 6'sında aynı): `zombie_breaks_barricade` KALDI. Ölçüm: kısmi yol araması yaklaşık 1,3 s sürerken `ZombieBrain.MoveTo` aramayı 0,2 s'de bir yeniden başlatıyor, zombi 5,5 s'de yaklaşık 5 cm ilerliyor. Balta kodu bu yola dokunmuyor. Önceki 98/0 sonucu `BuildOnly` ile mevcut sahnede alınmıştı; o sahne yeniden üretildiği ve depoda geçmiş olmadığı için karşılaştırma yapılamadı. Düzeltilmedi, kullanıcı kararı bekliyor.

**Ölçülmedi / yapılmadı.** İnsan oyun testi ve vuruş hissi, vuruş sesi, balta çekme animasyonu, gerçek zombiye baltayla vuruş, performans etkisi.

**Barikat düzeltmesi (2026-11-08, kullanıcı onayıyla).** `ZombieBrain.MoveTo`: süren yol araması yeniden başlatılmıyor, kısmi yol bulunduğunda bir sonraki aramadan önce 1 s bu yolda yürünüyor. `SandboxBuilder.BuildOnly` `Succeeded errors=0`. `--qa-survival` 98/0 (5 çalıştırmanın 5'i; `zombie_breaks_barricade` barikat canı 100 -> 70), `--qa-axe` PASS, `--qa-playthrough` 52/0, `--qa-selftest` tamamlandı. Editör doğrulayıcıları bu tek betik değişikliğinden sonra yeniden çalıştırılmadı. Performans etkisi ve kapalı yol karşısındaki zombi davranışının insan testi ölçülmedi.
