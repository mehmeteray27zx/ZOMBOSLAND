# SPEC v0.4 — Görünür eller, fiziksel reload, okunur isabet tepkisi, gerçek zombi modeli

Kaynak: `Zombosland_Oynanis_Inceleme_ve_Yol_Haritasi_2026-11-07.md` (öncelik: tek tabanca + görünür eller + okunur isabet tepkisi + fiziksel reload). Unity 6000.6.5f1, URP 17.6.0. **Paket/manifest değişikliği yok.** Sahne yine yalnızca `SandboxBuilder` ile üretilir.

## 0. Varlıklar (zaten projeye kopyalandı, .meta GUID'leri korunarak)

`Assets/ThirdParty/InfimaLPSP/` — Infima Games "Low Poly Shooter Pack – Free Sample" (Standard Unity Asset Store EULA). Yalnızca sanat/ses; paket scriptleri, prefab'ları, controller'ları, proje ayarları **alınmadı ve alınmayacak**.
- `Art/Meshes/Character/SK_FP_CH_Default_Cubic.fbx` — FPS kolları (Generic, UE4 mannequin iskeleti, 68 kemik). Malzeme slotu adı: `Arms`.
- `Art/Meshes/Weapons/Handguns/SK_Handgun_03.fbx` — tabanca (Generic, kendi iskeleti `SKEL_Handgun_03/root/{hammer, magazine/SOCKET_Magazine, slide/{SOCKET_Default,SOCKET_Scope}, SOCKET_Eject, SOCKET_Grip, SOCKET_Laser, SOCKET_Muzzle}`), malzeme slotları: `Body, Details, Grip, Hammer, Slide`. Gömülü klipler: `A_WEP_Handgun_03_Reference`(0,033 s), `A_FP_WEP_Handgun_03_Reload`(2,233), `A_FP_WEP_Handgun_03_Reload_Empty`(2,733), `A_WEP_Handgun_03_Fire`(0,833), `A_WEP_Handgun_03_Slide_Back_Pose`(0,033). `A_TP_*` kliplerini kullanma.
- `SM_Handgun_03_Magazine_Default.fbx` — şarjör (slotlar `Body, Bullets`), `SOCKET_Magazine` altına sıfır yerel dönüşümle takılır (görsel kontrolde yön yanlışsa düzelt ve raporla).
- `Art/Meshes/Weapons/_Common/Casings/SM_Bullet_Small_Empty.fbx` — boş kovan (slot `Bullet`).
- `Art/Animations/Character/Handguns/*.fbx` — kol klipleri (30 fps): `Idle_Pose` 0,033 s; `Aim_Pose` 0,733 (durağan); `Fire` 0,733; `Aim_Fire` 0,733; `Reload` 2,233 (olaylar: OnAmmunitionFill@0,519, OnAnimationEndedReload@0,633); `Reload_Empty` 2,733 (OnAmmunitionFill@0,430, OnSlideBack@0,587, OnAnimationEndedReload@0,706); ayrıca Holster/Unholster/Inspect/Run (bu sürümde kullanılmıyor). Klip adları `A_FP_PCH_Handgun_<Ad>`.
- Dokular: `Art/Textures/Character/T_FP_CH_Camo_001_001_D.png`; `Art/Textures/Weapons/T_WEP_Basic_005_D, _006_D, _043_D, T_WEP_Basic_R, T_WEP_Steel_Brushed_001_D, T_WEP_Steel_Brushed_N`; `Art/Textures/Casings/T_WEP_Casings_D`; efekt: `Art/Textures/Effects/T_MuzzleFlash_D, T_Smoke_01_D, T_Impact_Concrete_D`.
- Sesler `Audio/SFX/...`: `Weapons/Fire/S_WEP_Fire_002.wav, S_WEP_Fire_003.wav, S_WEP_Fire_Empty.wav`; `Weapons/Reloads/S_WEP_Handgun_Reload.wav, S_WEP_Handgun_Reload_Empty.wav`; `Weapons/Casings/S_WEP_Casing_01..05.wav`; `Weapons/Aiming/S_WEP_Aim_In.wav`; `Impacts/S_WEP_Impact_Bullet_01..04.wav`.

`Assets/ThirdParty/UnityRio/` — Unity Technologies "LowPoly Survival Character Rio" (Standard Unity Asset Store EULA): `Models/Rio.fbx` (Humanoid, geçerli avatar `RioAvatar`, A-poz, boy ≈1,78 m, ayaklar y≈0,02; tek SkinnedMeshRenderer `Rio`, slot `PlayerMaterial`), `Textures/Rio_AlbedoTransparency.png`, `Rio_MetallicSmoothness.png`, `Riol_Normal.png` (normal map olarak içe aktarılmış).

**Ölçülmüş yerleşim (prob, `Logs/lpsp-probe.txt`):** Kol FBX'inin kökü kamera noktasıdır: kol kökü kameranın yerel (0,0,0) konumuna, sıfır dönüşle konduğunda Idle_Pose'ta namlu kamera uzayında (0,035; -0,062; 0,411); Aim_Pose'ta namlu (0; -0,017; 0,550), gez soketi (0; -0,008; 0,336) — gez-arpacık kamera ekseninde. Tabanca kökü `ik_hand_gun` kemiğine **yerel konum 0, yerel dönüş Euler(90,0,0)** ile bağlanır (LPSP envanteri böyle). Bu değerlerle tabanca dik ve ileri bakıyor.

## 1. Editör: `Assets/Editor/ViewmodelFactory.cs` (yeni)

1. URP/Lit malzemeler `Assets/Generated/Materials/` altında oluştur/güncelle (var olanı yeniden kullan):
   - `VM_Arms`: BaseMap=Camo_001_001_D, BaseColor (0,82; 0,82; 0,82), Metallic 0, Smoothness 0,2.
   - `VM_Steel`: BaseMap=Steel_Brushed_001_D, BumpMap=Steel_Brushed_N (`_NORMALMAP`), Metallic 0,66, Smoothness 0,4.
   - `VM_Basic005` (Smoothness 0,3, Metallic 0,15), `VM_Basic006` (0,45 / 0,15), `VM_Basic043` (0,45 / 0,15) — ilgili `_D` dokuları.
   - `VM_Casings`: BaseMap=Casings_D, Metallic 0,6, Smoothness 0,5.
2. `ModelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slotAdı), malzeme)` + `SaveAndReimport()` (yalnızca remap değiştiyse): kollar `Arms→VM_Arms`; tabanca `Body→VM_Steel, Details→VM_Basic006, Grip→VM_Basic043, Hammer→VM_Basic005, Slide→VM_Basic005`; şarjör `Body→VM_Steel, Bullets→VM_Casings`; kovan `Bullet→VM_Casings`. Remap sonrası renderer'da `null` malzeme kalmamalı (doğrulamada kontrol).
3. `AnimatorController` varlıkları kodla üret: `Assets/Generated/Animators/VM_Arms.controller`, `VM_Pistol.controller` (her çalıştırmada silip yeniden oluştur). Parametreler: `Aim` (float), `ReloadTime` (float).
   - Kollar, katman 0, durumlar: `Pose` = 1B BlendTree(`Aim`): Idle_Pose(0) ↔ Aim_Pose(1); `Fire` = BlendTree(`Aim`): Fire(0) ↔ Aim_Fire(1), çıkış zamanıyla `Pose`'a geçiş (exitTime 0,9, süre 0,08 s); `Reload` = Reload klibi, `timeParameter=ReloadTime` aktif; `ReloadEmpty` = Reload_Empty, aynı şekilde. Varsayılan `Pose`. Write Defaults açık.
   - Tabanca: `Idle` = Reference; `Fire` = A_WEP_Handgun_03_Fire, çıkışta `Idle`'a (exitTime 0,95, 0,02 s); `SlideBack` = Slide_Back_Pose (döngüsüz bekler); `Reload`/`ReloadEmpty` = FP reload klipleri, `timeParameter=ReloadTime`.
4. Klip olay alıcısı: kol ve tabanca Animator nesnelerine `ViewmodelAnimationEvents` ekle (aşağıda), böylece LPSP'nin klip olayları "no receiver" hatası üretmez.
5. `BuildArmsViewmodel(Transform viewmodel, out ViewmodelRig rig)`: kolları `Viewmodel` altına (yerel 0 / identity), tabancayı `ik_hand_gun` altına (0, Euler(90,0,0)), şarjörü `SOCKET_Magazine` altına koy. Tüm Collider'ları kaldır. Bütün viewmodel renderer'larında `shadowCastingMode=Off`, `receiveShadows=true`; SkinnedMeshRenderer'larda `updateWhenOffscreen=true`. Katman 0. Animator: `cullingMode=AlwaysAnimate`, `applyRootMotion=false`, controller'lar atanmış. Döndürdüğü referanslar: armsAnimator, pistolAnimator, muzzle(`SOCKET_Muzzle`), eject(`SOCKET_Eject`). Varlıklardan biri eksikse `false` döner ve `SandboxBuilder` mevcut primitive tabancaya düşer (geri dönüş yolu korunur).
6. `SandboxBuilder.BuildPlayer`: LPSP yolu başarılıysa `Viewmodel` yerel konumu **(0,0,0)** olur; presenter alanları: `armsAnimator, pistolAnimator, muzzle, eject, animated=true`. Rapor satırı: `Viewmodel uses Infima LPSP arms + Handgun_03`.

## 2. Oyun durumu: `WeaponState`

- `TacticalReloadSeconds = 1.41f` (=2,233×0,633), `EmptyReloadSeconds = 1.93f` (=2,733×0,706). `BeginReload` boş şarjörde (Ammo==0) boş süreyi seçer. `public bool ReloadFromEmpty { get; }` (reload başlangıcındaki durum). `ReloadDuration` mevcut reload'un süresini döndürür (reload yokken taktik süre). `ReloadSeconds` sabitini kaldır; kullanan yerleri güncelle.
- Mühimmat aktarımı yine `Tick` içinde, süre sonunda **tek kez**. Animasyon/ses/olay durumu değiştiremez.
- `WeaponValidation`: 1,5 s varsayan testleri yeni sürelere çevir; ek testler: dolu olmayan şarjörde süre 1,41, boş şarjörde 1,93 ve `ReloadFromEmpty`; aktarım sınırdan hemen önce yok, sınırda var.

## 3. Sunum: `ViewmodelPresenter` (animasyonlu yol)

`animated==true` iken:
- Taban konum/dönüş sıfır (ADS'yi Aim_Pose klibi yapar). `armsAnimator`/`pistolAnimator` `Aim` = `player.AdsAmount`.
- Sway, yürüme bob'u, ateş tepmesi yayı ve duvar geri çekilmesi `Viewmodel` dönüşümüne **küçük ofset** olarak kalır. Tepme ölçeği animasyonlu yolda 0,5 (Fire klibi de tepiyor); duvar: `position += (0,-0.05,-0.10)*wall`, `euler.x += 40*wall`. Kozmetik ayarlar (`WeaponSway`) 0 iken bunlar kapanır; nişan/hasar etkilenmez.
- Atış (`OnShot`): kol `CrossFadeInFixedTime("Fire", 0.03f, 0, 0f)`; tabanca `Play("Fire",0,0)`; namlu efekti `muzzle`'da; kovan çıkar (`EffectPool.EjectCasing(eject)`). Son mermi atıldıysa (Ammo==0) tabanca Fire bitince `SlideBack`'e geçer ve reload'a kadar orada kalır.
- Reload başlayınca (Reloading false→true geçişi): `ReloadFromEmpty`'e göre `Reload`/`ReloadEmpty` durumuna 0,1 s crossfade (her iki Animator). Her karede `ReloadTime = (geçen süre)/klipUzunluğu`; oyun reload'u bittikten sonra kuyruk (klibin kalan kısmı, el nişana dönüşü) gerçek hızda sürer, normalize süre ≥ 0,98 olunca 0,15 s ile `Pose`/`Idle`'a dön. Kuyruk sırasında ateş edilirse kuyruk kesilir ve Fire oynar. Reload bitince `ReloadTime` sıfırlanır. **Hiçbir yol presenter'ı reload'da takılı bırakmaz** (durum `WeaponState`'ten okunur, olaylara bağlı değildir).
- Ses: reload başında tek dosya (`Sfx.ReloadTactical` / `Sfx.ReloadEmpty`), 2D, ses 0,8. Bu kayıtlar yoksa eski mag-out/mag-in/slide işaretlerine düş. Sağ tık basılınca bir kez `Sfx.AimIn` (0,35).
- `animated==false` ise mevcut prosedürel davranış değişmeden çalışır.

`ViewmodelAnimationEvents` (yeni, `Assets/Scripts`): `OnAmmunitionFill, OnAnimationEndedHolster, OnAnimationEndedInspect, OnAnimationEndedReload, OnEjectCasing, OnSlideBack` boş metodları. Yorum: "LPSP klip olayları; oyun durumu bunlara bağlı değildir".

## 4. Efekt ve ses

- `EffectPool`: `public Mesh casingMesh; public Material casingMaterial;` 12'lik kovan havuzu (çalışmada Instantiate/Destroy yok). Kovan: MeshRenderer + küçük BoxCollider + Rigidbody (kütle 0,01, sürekli çarpışma tespiti: `ContinuousDynamic`), katman `Ignore Raycast` (2) — atış maskesi dışında. Çıkış hızı `eject.right*1.6 + eject.up*1.2 + rastgele(±0,3)` + oyuncu hızı yok; dönüş hızı rastgele 20 rad/s. 0,45–0,7 s sonra konumunda `Sfx.Casing` 3D (ses 0,35). 3,5 s sonra gizle. Oyuncu ile çarpışmasın: başlangıçta `Physics.IgnoreLayerCollision(2, Layers.Player)`. Builder kovan mesh'ini `SM_Bullet_Small_Empty.fbx`'ten alır (ölçek FBX'teki gibi), malzeme `VM_Casings`. Mesh yoksa kovan özelliği kapalı.
- Namlu alevi malzemesi: `T_MuzzleFlash_D` varsa onu kullan (aditif), yoksa mevcut yıldız dokusu.
- `SfxBank`: `Sfx` enum'una **sona** ekle: `ReloadTactical, ReloadEmpty, Casing, AimIn, HitTick`. Override aynı id için birden çok klip toplayabilsin (liste→dizi; varyasyon). `public bool Has(Sfx id)`. Prosedürel yedek: `Casing` (kısa metalik tık), `HitTick` (çok kısa yüksek tık), `AimIn` (yumuşak hışırtı); `ReloadTactical/ReloadEmpty` yedeği yok (`Has` false).
- Builder eşlemesi (önce `Assets/Audio/<ad>.wav` geçerli kalır, sonra LPSP): Shot←Fire_002+Fire_003; DryFire←Fire_Empty; HitConcrete←Impact_Bullet_01..04; Casing←Casing_01..05; ReloadTactical←Handgun_Reload; ReloadEmpty←Handgun_Reload_Empty; AimIn←Aim_In.
- İsabet onayı: `Sandbox.Impact` zombi isabetinde 2D `HitTick` (ses 0,25; öldürmede 0,4, pitch 0,85). Iskalamada yok.

## 5. Zombi: model ve tepki

**Model (`ZombieFactory`):** FBX adayları sırayla: `Assets/Art/Characters/Zombie/zombie.fbx`, `Assets/ThirdParty/UnityRio/Models/Rio.fbx`. Rio için ölçekleme yapma (boy 1,6–2,0 m aralığındaysa ölçek 1); yere oturtmayı renderer bounds ile değil `min(LeftToes.y, RightToes.y, LeftFoot.y-0.09, RightFoot.y-0.09)` ile yap (ayak tabanı y=0). Malzeme `Assets/Generated/Materials/Zombie_Rio.mat` (URP/Lit): BaseMap=Rio_Albedo, BaseColor (0,66; 0,74; 0,60) (hasta-yeşil, soluk), `_MetallicGlossMap`=Rio_MetallicSmoothness (`_METALLICSPECGLOSSMAP`, smoothness kaynağı alfa, Smoothness 0,35), BumpMap=Riol_Normal (`_NORMALMAP`, 1). `ZombieBrain.Activate` içinde örnek başına MaterialPropertyBlock ile `_BaseColor` çarpanı ±%10 parlaklık ve hafif ton farkı (sabit tohum değil, `Random`). Gölge açık. Rapor: `Zombie built from Rio.fbx (tinted)`.

**Kol pozu (`ZombieAnimator`):** Üst/alt kollar artık "yön hedefleme" ile pozlanır, böylece A-poz (Rio) ve aşağı sarkan (primitive) rig aynı görünür: `Aim(bone, child, hedefYönZombieUzayı, ağırlık)` = dinlenme dünya yönünü (bone→child) hedef yöne çeviren `FromToRotation` dinlenme rotasyonuna uygulanır. Üst kol hedefi: `normalize(±0.18, -0.35, 1)` (sol −x), yürürken ±6° salınım; saldırı hazırlığında `normalize(±0.15, 0.75, 0.6)`'ya lerp (windup). Alt kol hedefi: üst kol yönünden 15° yukarı bükülü (`normalize(±0.1, -0.1, 1)`), windup'ta `(±0.1, 0.9, 0.4)`. El için `child` = el kemiği; alt kol için `child` = el, üst kol için `child` = alt kol. Diğer kemikler mevcut eklemeli yöntemle.

**Hafif / ağır tepki ayrımı (`ZombieBrain` + `DamageRules`):** Her mermi artık zombiyi kilitlemez.
- `DamageRules.HeavyStaggerSeconds(zone)`: Head 0,5, Body 0,45, Limb 0,35. `PoiseThreshold = 60f`, `PoiseWindow = 1.2f`, `HeavyStaggerCooldown = 1.5f`.
- İsabet → `poise += hasar (çarpanlı)`; pencere dışında kalan birikim sıfırlanır. Ağır sendeleme (mevcut Stagger durumu: hareket durur, saldırı iptal) yalnızca: (a) baş isabeti veya (b) poise ≥ eşik — ve ikisinde de son ağır sendelemeden beri cooldown dolmuşsa. Ağır sendelemede poise sıfırlanır.
- Aksi hâlde **hafif tepki (flinch)**: durum değişmez, saldırı hazırlığı iptal edilmez (yalnızca baş/ağır iptal eder); hareket hızı 0,25 s için ×0,55; `LastFlinchTime`, `FlinchDirection` (atış yönü, yatay), `FlinchZone`, `FlinchSide` (isabet noktası zombinin sağında mı solunda mı: −1/+1) yayımlanır.
- Ölüm tek kez, mevcut ragdoll yolu.
- `ZombieAnimator`: flinch, ayrı bir yay ile 0,25 s'de sönen eklemeli darbe: Body → gövde/göğüs atış yönünde 12°, yanlamasına `FlinchSide*6°` burulma; Head → baş 28°, gövde 6°; Limb → o taraftaki üst kol 20° geri savrulur ya da (bacak hitbox'ı ise) kalça 5° çöker. Ağır sendeleme mevcut itme yayı + daha büyük açı (gövde 30°, baş 45°) ve bacaklar 0,2 s'lik geri adım (kalça −0,06 m). Hangi uzvun vurulduğunu bilmek için `ZombiePart`'a `public HumanLimb limb` (None, LeftArm, RightArm, LeftLeg, RightLeg) ekle; factory doldurur; `DamageInfo`'ya dokunma, brain `Hit` çağrısında parçayı alır (`ZombiePart.Hit` → `owner.ApplyDamage(info, this)` aşırı yüklemesi; `IDamageable` arayüzü aynı kalır).

## 6. QA görüntü modu: `--qa-screens`

Yeni `QaScreens` (Assets/Scripts), `Sandbox.Start` sonunda argüman varsa etkinleşir (runInBackground açar, zombileri dondurur gibi `qaFreeze`, oyuncuyu spawn'da tutar). Sıra (gerçek zamanlı, `ScreenCapture.CaptureScreenshot`, exe yanında `QaScreens/` klasörü; editörde proje kökü):
1. t=1,5 s `01_hip.png` (bir zombi kameranın 4 m önüne, yüzü oyuncuya dönük ışınlanmış).
2. t=2,5 s ADS zorla → t=3,2 s `02_ads.png`.
3. t=3,6 s ADS bırak, bir atış (gerçek `Fire` yolu, zombinin gövdesine nişan alınmış) → 1 kare sonra `03_fire.png`; 0,06 s sonra `04_flinch.png`.
4. t=4,5 s reload başlat (önce şarjörü 5'e indir — yalnızca QA için `WeaponState.QaSetAmmo(int)` internal/QA metodu) → reload %35'inde `05_reload_mid.png`, %80'inde `06_reload_late.png`.
5. Şarjörü boşalt (QaSetAmmo(0)), slide geri pozu → `07_empty_slideback.png`; boş reload başlat, %50'de `08_empty_reload.png`.
6. Zombiyi 1,2 m'ye getir, saldırı hazırlığı pozunda (`windup` zorla) `09_zombie_close.png`; duvara yaslan (`--qa-wall` konumu) `10_wall.png`.
7. `QA_SCREENS_DONE` logla ve `GameQuit.Request()`.
Pencere boyutu 1600×900 (`Screen.SetResolution`, pencereli).

## 7. Doğrulama ve teslim

- `SandboxValidation`'a ekle: viewmodel kol Animator'ü + tabanca Animator'ü + `SOCKET_Muzzle` + eject soketi var; viewmodel renderer'larında null malzeme yok ve gölge atmıyor; zombi prefab'ında Rio SkinnedMeshRenderer var ve malzemesi `Zombie_Rio`; `ZombiePart.limb` kol/bacak parçalarında dolu.
- `WeaponValidation` güncel ve geçiyor. `QaSelfTest` yeni sürelerle geçiyor (gerekirse beklemeleri `ReloadDuration`'dan türet).
- Komutlar (Unity kapanışta takılabilir — her çağrıyı `timeout` ile sar ve sonucu log satırından oku, süreç çıkış koduna güvenme):
  - `Unity.exe -batchmode -quit -projectPath E:\ZOMBOSLAND -executeMethod SandboxBuilder.Build -logFile Logs\v04-build.log` → `ZOMBOSLAND_BUILD_RESULT: Succeeded errors=0`, `error CS` 0, `warning CS` 0 (yeni kod için).
  - `-executeMethod WeaponValidation.Run` → `WEAPON_VALIDATION_PASSED`; `-executeMethod SandboxValidation.Run` → `SANDBOX_VALIDATION_RESULT PASS`.
  - `Build\Zombosland.exe --qa-selftest` ve `--qa-screens` (Player.log `%USERPROFILE%\AppData\LocalLow\Zombosland\Zombosland Combat Sandbox\Player.log`). Player.log'da `NullReference`, `has no receiver`, `missing` yok.
- `Assets/ThirdParty/InfimaLPSP/LICENSE.md` ve `Assets/ThirdParty/UnityRio/LICENSE.md`: yayıncı, paket adı, kaynak (kullanıcının Asset Store hesabıyla indirilmiş yerel önbellek `%APPDATA%\Unity\Asset Store-5.x`), alınma tarihi 2026-11-07, lisans "Standard Unity Asset Store EULA — oyunda kullanım serbest, ham varlıklar ayrı olarak/açık depoda yeniden dağıtılamaz", alınan dosya listesi, yapılan değişiklikler (URP malzemeleri yeniden kuruldu, kendi controller'larımız, renk tonu).
- `Assets/Editor/LpspProbe.cs` ve `RioProbe.cs` geçici; teslimde sil.
- README (v0.4: kontroller değişmedi, reload 1,41/1,93 s, `--qa-screens`, varlık listesi) ve `Docs/IMPLEMENTATION_REPORT.md` "v0.4" bölümü: yalnızca gerçekten çalıştırılan komutların sonuçları; ölçülmeyen şeyleri "ölçülmedi" diye yaz.

## Kapsam dışı
Yeni silahlar, holster/inspect, koşu animasyonu, Asset Store'dan yeni indirme, Mixamo, paket ekleme, ağ, performans ölçümü (yalnızca duman testi).

## 8. EK (kullanıcı paketleri indirdi): Animasyonlu zombi — Pxltiger "Zombie" + Kevin Iglesias tepki/ölüm klipleri

Yeni varlıklar (seçici kopya, .meta korunarak):
- `Assets/ThirdParty/PxltigerZombie/` (Pxltiger "Zombie", Draft 1.2, Standard Unity Asset Store EULA): `FBXs/Zombie1.FBX, Zombie2.FBX, Zombie3.FBX` (Humanoid; Zombie2/3 ve tüm klipler avatarı Zombie1'den kopyalar), `Animations/Zombie@Z_{Idle,Walk,Walk1,Run,Attack,FallingBack,FallingForward}.FBX` ve `_InPlace` sürümleri (Walk, Walk1, Run), `Textures/Zombie.tga` (albedo), `Zombie_nm.png` (normal), `Zombie_metallic.tga`, `Zombie_ao.png`, `zombie_emission.tga`. Paketin .mat/.controller/prefab/sahnesi alınmadı.
- `Assets/ThirdParty/KevinIglesiasMelee/Animations/HumanM@CombatDamage01.fbx` (vurulma tepkisi), `HumanM@Death01.fbx`, `HumanM@Idle01.fbx` (Kevin Iglesias "Human Melee Animations FREE" 2.0.2, Standard Unity Asset Store EULA). .meta'larda `avatarSetup` 2→1 (CreateFromThisModel) yapıldı çünkü kaynak avatar pakette yok.

Uygulama:
1. **Model önceliği** (`ZombieFactory`): `Assets/Art/Characters/Zombie/zombie.fbx` → Pxltiger `Zombie1/2/3.FBX` → Rio → primitive. Pxltiger varsa **üç prefab** üret (`Zombie_A/B/C.prefab`, her biri bir FBX); `ZombieDirector`'a `public GameObject[] zombiePrefabs` ekle (tekil alan geriye uyum için kalsın), sahnedeki 5 zombi ve perf senaryosu sırayla/karışık kullansın. Yere oturtma ayak/parmak kemiğinden (bölüm 5'teki kural); ölçek: humanoid boy (Head bone y + 0,12) 1,6–2,0 m dışındaysa 1,8 m'ye ölçekle.
2. **Malzeme** `Zombie_Pxl.mat` (URP/Lit): BaseMap=Zombie.tga, BumpMap=Zombie_nm.png (importer textureType NormalMap değilse kodla ayarla), `_MetallicGlossMap`=Zombie_metallic.tga (smoothness alfa; Smoothness 0,4), OcclusionMap=Zombie_ao.png, Emission=zombie_emission.tga, `_EmissionColor` beyaz×0,6, `_EMISSION` açık. Örnek başına ±%10 ton (MaterialPropertyBlock). FBX malzeme slotları remap ile `Zombie_Pxl`'e bağlanır; null/pembe kalmasın.
3. **Animator controller** (kodla, `Assets/Generated/Animators/Zombie.controller`, her build'de yeniden): parametreler `Speed` (float, m/s), `AnimRate` (float), `Attack` (trigger), `HeavyHit` (trigger), `Variant` (float 0/1). Base layer:
   - `Locomotion`: 2B değil, iç içe 1B: hız ekseni `Speed` — 0: Z_Idle; yürüme eşiği = Z_Walk (root-motion'lı sürüm) klibinin `averageSpeed.magnitude` ölçümü; koşma eşiği = Z_Run'ın ölçümü. Hareket kliplerinde **InPlace** sürümleri kullan (hareketin tek sahibi NavMeshAgent). Yürüme düğümü kendisi `Variant` ile Z_Walk_InPlace ↔ Z_Walk1_InPlace karışımı (her zombi Activate'te 0 veya 1 seçer). Durum hızı `AnimRate` parametresiyle çarpılır: `AnimRate = clamp(gerçekPlanarHız / karışımYerelHızı, 0.6, 1.6)` (ayak kaymasını azaltmak için; durgunken 1).
   - `Attack`: Z_Attack, tek oynatma (döngü kapalı geçersiz kılınamıyorsa çıkış zamanıyla dön: exitTime 0,95 → Locomotion). Editörde klibi örnekleyerek **vuruş anı**nı ölç (sağ veya sol elin kalçaya göre en ileri olduğu normalize zaman); durum hızını `vuruşAnı(s) / AttackWindupSeconds(0,55)` yap ki vuruş hazırlık bitişine denk gelsin. Ölçülen değerleri build raporuna yaz.
   - `HeavyHit`: HumanM@CombatDamage01, AnyState'ten `HeavyHit` tetikleyicisiyle (kendine geçiş kapalı), çıkış zamanı 0,85'te 0,15 s ile Locomotion'a.
   - Ölüm: **mevcut ragdoll korunur** (Animator ölümde kapanır; ölüm klipleri bu sürümde kullanılmaz ama içe aktarılmış kalır). Animator `applyRootMotion=false`, `cullingMode=CullUpdateTransforms`, `updateMode=Normal`.
4. **`ZombieBrain` → animator köprüsü**: her karede `Speed`=PlanarSpeed; saldırı hazırlığı başlarken `Attack` tetikle; ağır sendelemede `HeavyHit` tetikle; donmuşken (`ZombiesFrozen`) `animator.speed=0`.
5. **`ZombieAnimator` iki kip**: (a) *klipli kip* (Animator'de controller var): yürüme/kol/bacak prosedürü **kapalı**; yalnızca hafif flinch ve ağır itme ofsetleri LateUpdate'te **o anki animasyonlu pozun üzerine** uygulanır (`bone.rotation = offsetWorld * bone.rotation`; rest yakalama yok). (b) *prosedürel kip* (Rio/primitive): bölüm 5'teki gibi. Flinch kuralları bölüm 5 ile aynı.
6. Doğrulama: zombi prefab'larında Animator controller atanmış, avatar geçerli humanoid, malzeme `Zombie_Pxl`; SandboxValidation'a ekle. `--qa-screens`: 09'da zombi saldırı pozunda (Attack tetiklenmiş, uygun normalize zamanda dondur); yeni `11_zombie_walk.png` (zombi kameraya doğru 2 m yürürken) ve `12_zombie_heavyhit.png` (HeavyHit tetiklendikten 0,2 s sonra).
7. LICENSE.md: `PxltigerZombie/LICENSE.md`, `KevinIglesiasMelee/LICENSE.md` (yayıncı, paket, sürüm, kullanıcının hesabından 2026-11-07'de Package Manager ile indirildi, Standard Unity Asset Store EULA, alınan dosyalar, değişiklikler: URP malzeme, kendi controller, avatarSetup düzeltmesi).
