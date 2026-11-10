# Zombosland — Combat Sandbox tamamlama spesifikasyonu v0.3

**Sahip:** ana ajan (spec + QC). **Uygulayıcı:** ekonomik model alt ajanı. **Tarih:** 2026-11-06.
**Kaynaklar:** `AGENTS.md`, Master Plan v0.2 §3, `Docs/QA_ACCEPTANCE.md`, mevcut kod.

## Sabit kurallar

- Unity **6000.6.5f1**, URP **17.6.0**. `Packages/manifest.json` değiştirilmez; yeni paket yok. Kullanılabilir yerleşik modüller: physics, audio, imgui, ve `com.unity.modules.ai` / `particlesystem` / `animation` gerekiyorsa manifest'e **yalnızca bu yerleşik modüller** (`"1.0.0"`) eklenebilir; bunu raporla.
- Eski Input Manager kullanılmaya devam eder (`activeInputHandler=0`). Input System paketi yok.
- Sahne `SandboxBuilder.Build()` ile tamamen üretilir. Elle sahne düzenlemesi yok; her şey builder'dan gelir.
- Oynanış durumu sunumdan bağımsız: animasyon/ses/VFX hiçbir zaman mühimmat, reload, hasar veya ölüm durumunu belirlemez. Sunum yalnızca durumu okur.
- **Nişan/isabet kaynağı görsel kameradan ayrıdır.** Headbob, sway, kamera sarsıntısı ve viewmodel animasyonu ateş ışınını değiştirmez. Recoil (nişan tepmesi) oynanış durumudur ve ayarlardan bağımsızdır.
- Hiçbir ölçüm/sonuç uydurulmaz. Derlenmeyen iş teslim edilmez.
- Basit, okunabilir C#; `namespace Zombosland`. ECS/Jobs yok. Her yeni `.cs` için Unity `.meta` dosyasını Unity üretir (batch çalıştırma sonrası); elle GUID yazma.

## 1. Oyuncu ve kamera hiyerarşisi

```
Player (CharacterController, layer Player)
└─ AimPivot        ← yaw gövdede, pitch + recoil burada. Atış ışını BURADAN.
   └─ Camera       ← yalnızca görsel ofset: headbob konumu, sarsıntı rotasyonu
      └─ Viewmodel ← sway, ADS konumu, ateş tepmesi animasyonu, reload animasyonu
```

- `Sandbox.ResolveShot(...)` imzası ve davranışı korunur (WeaponValidation kullanıyor); canlı atış `AimPivot` konum/yönlerini verir.
- Kamera sarsıntısı: ateşte ve hasar alınca küçük rotasyon gürültüsü (Perlin), `cameraShake` ayarıyla ölçeklenir, 0'da tamamen kapalı.
- Headbob: yerde ve yatay hız > 0.5 iken sinüs; yürüyüş dikey ~0.025 m, yanal ~0.012 m, koşuda frekans ve genlik artar; `headbob` ayarıyla ölçeklenir, ADS'de %20'ye iner.
- Sway: viewmodel fare deltası ile gecikmeli döner (maks ~3°) ve yürürken küçük bob; `weaponSway` ile ölçeklenir, ADS'de %25.
- Duvar engeli: mevcut SphereCast mantığı korunur ama `AimPivot`'tan yapılır; engelde silah aşağı/geri çekilir (kaybolma yerine), `WeaponState` etkilenmez, engel varken namlu engeli `ResolveShot` tarafından zaten yakalanır.

## 2. Ayarlar (Esc menüsü, PlayerPrefs)

Ayrı ayrı: fare hassasiyeti (0.3–5), FOV (60–100), headbob (0–1), silah sway (0–1), kamera sarsıntısı (0–1), ana ses (0–1). Her biri anında uygulanır ve devam ederken kaydedilir. Eski `shake` anahtarı varsa `weaponSway`'e taşınır. Menüde "0 = kapalı" açıklaması. Türkçe karakterler kullanılabilir.

## 3. Silah (WeaponState genişletme)

- `WeaponState` saf kalır. Ekle: `float ReloadDuration` (1.5), `float ReloadProgress(float now)` (0..1, reload yoksa 0), `bool TryDryFire(float now)` (şarjör boş ve reload yokken tetik çekişi; cadence ile sınırlı, `true` → "klik" sesi). Mevcut davranış ve WeaponValidation testleri **aynen geçmeli**.
- Reload kuralı korunur: tamamlanana kadar ateş yok, iptal yok.
- Ateşte gürültü olayı: `NoiseEvents.Emit(position, radius=30)`; zombiler duyar (bkz. §5).

## 4. Sunum: viewmodel animasyonu, VFX, ses

**Viewmodel prosedürel animasyonu** (`ViewmodelPresenter`): ateşte geri tepme (z −0.04, pitch +4°, ~0.08 s'de yay ile toparlanma); reload sırasında `ReloadProgress` eğrisi: aşağı ve yana dön (0–0.2), şarjör çıkışı (şarjör alt objesi varsa aşağı in), takılması (~0.65), sürgü (~0.85), geri dönüş. Boş şarjörde sürgü geride görünümü (sürgü objesi varsa).

**VFX havuzları** (`EffectPool`, Destroy/Instantiate yok, sabit kapasite, en eskiyi geri dönüştür):
- Namlu alevi: `Muzzle` transformunda 0.05 s görünen additive quad + kısa Point Light (range 6, intensity yüksek, gölge yok). 
- İsabet: dünyada toz + kıvılcım ParticleSystem; zombide kan sıçraması ParticleSystem (koyu kırmızı, yer çekimli). Kapasite 16 + 16.
- Mermi izi decal: yüzey normaline hizalı küçük quad (0.06 m), normal boyunca 0.002 m ofset, URP/Lit veya Unlit koyu malzeme, maks 64, en eskisi yeniden kullanılır. Zombilere decal yapıştırma (parent'a bağlamadan) yok.
- Partikül malzemeleri `Universal Render Pipeline/Particles/Unlit`; dokular kodla üretilen yumuşak daire `Texture2D` (builder `Assets/Generated/Textures/` altına PNG olarak kaydeder).

**Ses** (`SfxBank` + 3D `AudioSource` havuzu, kapasite 24): Prosedürel ama gerçekçiye yakın:
- Silah sesi: beyaz gürültü patlaması (üstel sönüm ~0.25 s) + 60–90 Hz düşük "thump" + kısa yüksek frekans çatlaması; hafif rastgele perde (±4%).
- Kuru tetik klik, şarjör çıkış/giriş, sürgü (kısa filtrelenmiş gürültü klikleri) reload ilerlemesine göre tetiklenir (her reload'da bir kez, ilerleme eşiği geçişinde).
- İsabet: et (boğuk), beton (keskin). Zombi: hırıltı (düşük geçirgen filtreli gürültü + yavaş genlik modülasyonu, 3D, rastgele aralıklı), saldırı hırlaması, ölüm. Oyuncu hasar sesi.
- Gerçek kayıtlar daha sonra `Assets/Audio/` altına konursa (`gunshot.wav` vb.) `SfxBank` onları tercih eder; yoksa prosedürel olanı kullanır. Tüm sesler `AudioListener.volume = masterVolume`.

## 5. Zombi

**Tek hasar sözleşmesi:**
```csharp
public struct DamageInfo { public float Amount; public Vector3 Point, Direction; public HitZone Zone; public Object Source; }
public enum HitZone { Body, Head, Limb }
public interface IDamageable { bool ApplyDamage(in DamageInfo info); } // true = bu vuruş öldürdü (tam bir kez)
```
`ZombiePart` → `owner.ApplyDamage(...)`; çarpanlar: Head 2.0, Body 1.0, Limb 0.7; taban hasar 34. `HealthState` kullanılır. Ölü zombide hasar/olay yok (test edilebilir).

**Hareket — NavMesh, paket yok:** `ZombieDirector` sahne başlangıcında `UnityEngine.AI.NavMeshBuilder.CollectSources` + `BuildNavMeshData` + `NavMesh.AddNavMeshData` ile zemin ve engellerden runtime NavMesh üretir (agent radius 0.35, height 1.8, climb 0.35, slope 40). Zombi: `NavMeshAgent` (speed yürüme 1.1, kovalamada 1.6–2.2 rastgele, angularSpeed 240, avoidance açık). Zombinin oyuncuyu engellemesi için `ZombieMover` katmanında kinematik kapsül collider (CharacterController kaldırılır). NavMesh üretilemezse düz hat takip yedeği + uyarı log'u.

**Durum makinesi** (`ZombieBrain`, açık enum): `Idle/Wander` → `Chase` (görüş: 18 m + 110° koni + LOS ışını; veya `NoiseEvents` duyması) → `Attack` (≤1.6 m: 0.55 s hazırlık, sonra mesafe+LOS kontrolüyle 10 hasar, 1.2 s bekleme) → `Stagger` (isabette kısa durma: kafa 0.45 s, gövde 0.25 s, uzuv 0.15 s; Attack hazırlığını iptal eder) → `Dead`. `Chase` hedefi kaybedilirse son görülen konuma git, 6 s sonra Wander.

**Prosedürel animasyon** (`ZombieAnimator`): referanslar `hips, spine, head, leftUpperArm, rightUpperArm, leftUpperLeg, rightUpperLeg` (+ isteğe bağlı lowerLeg/lowerArm). Eğer `Animator` + humanoid avatar varsa bu kemikler `GetBoneTransform` ile doldurulur; yoksa builder primitive iskeleti atar. LateUpdate'te dinlenme pozuna **eklemeli** rotasyonlar: ajan hızına bağlı sendeleyerek yürüme (bacak salınımı, kollar öne uzanık, gövde öne eğik, kafa sallanması), saldırı hazırlığında kollar kalkar, stagger'da isabet yönünden uzağa gövde/kafa itmesi (yaylı sönüm). Kırmızı renk yanıp sönmesi kaldırılır.

**Ölüm — ragdoll** (`ZombieRagdoll`): builder her kemik parçası için `Rigidbody` (kinematic) + `CharacterJoint` (açı limitleri makul) + collider kurar; hitbox collider'ları bu parçalardadır. Ölünce: agent/brain/animator kapatılır, rigidbody'ler kinematic değil, isabet yönünde impuls (kafa vuruşu daha güçlü). Ölü parçalar `Corpse` katmanına (10) alınır; ateş ışını cesede decal/kan efekti verebilir ama hasar vermez. Ceset 20 s sonra veya 24'ten fazla ceset olunca en eski kaldırılır.

## 6. Oyuncu sağlığı ve HUD

- Oyuncu `HealthState(100)`; hasarda kırmızı kenar vinyeti + kamera sarsıntısı + ses; ölünce 2 s karartma, sonra sahne yeniden yüklenir. `--perf` modunda oyuncu ölümsüz.
- HUD (IMGUI, 1080p ölçekli): sol altta sağlık çubuğu + sayı; sağ altta büyük şarjör sayısı / rezerv, reload sırasında ilerleme çubuğu, boşta "R — doldur" uyarısı; dinamik nişangâh (4 çizgi, recoil ve hareketle açılır, ADS'de gizli); isabet işareti (gövde beyaz, kafa kırmızı, öldürme daha büyük; 0.15 s). Kontrol yardımı satırı F1 ile aç/kapa (varsayılan ilk 10 s görünür). Okunabilirlik için yarı saydam koyu arka planlar.

## 7. Performans senaryosu ve ölçüm

`PerfHarness` başlangıç argümanları: `--perf=10|30|100`, opsiyonel `--perf-warmup=10 --perf-seconds=60`.
- Sabit tohum (12345) ile sabit spawn noktaları; oyuncu sabit noktada, kamera sabit hızla 360° yatay tarar (süre boyunca 2 tur); zombiler normal AI ile kovalar; oyuncu ölümsüz, ateş yok.
- `QualitySettings.vSyncCount=0`, `Application.targetFrameRate=-1` (rapora yazılır).
- Isınma sonrası pencere boyunca her kare: `Time.unscaledDeltaTime` ms, ayrıca `FrameTimingManager.CaptureFrameTimings()` ile CPU/GPU kare süresi (destekleniyorsa; desteklenmiyorsa "N/A" yaz, uydurma). Builder `PlayerSettings.enableFrameTimingStats = true` yapar.
- Çıktı: exe yanında `PerfResults/perf_<N>_<yyyyMMdd_HHmmss>.csv` (kare başına) ve `.txt` özet: N, süre, kare sayısı, ort FPS, ort ms, %1 düşük FPS (en yavaş %1 karelerin ortalama ms'inden), p99 ms, maks ms, >33.3 ms ve >50 ms takılma sayısı, CPU/GPU ort ms, `Profiler.GetTotalAllocatedMemoryLong/ReservedMemoryLong` başlangıç/bitiş, GC sayısı (`System.GC.CollectionCount(0)`), çözünürlük, tam ekran modu, kalite seviyesi, grafik API, `SystemInfo` CPU/GPU/RAM/OS, build sürümü. Bitince `Application.Quit()`.
- `--qa-autoquit=SEC`: SEC saniye sonra temiz çıkış (kapanış donması teşhisi için; Player.log'a "QA_AUTOQUIT_REQUESTED" yaz).

## 8. Kapanış donması (açık QA)

Çıkış yolu: `Time.timeScale=1`, imleci serbest bırak, `SaveSettings()`, sonra `Application.Quit()`. `OnApplicationQuit` içinde havuzları/AudioSource'ları durdur. Kök neden bilinmiyor; teşhis için `--qa-autoquit` yeter, iddia yok.

## 9. Gerçekçi görsel temel (builder)

- **Dokular:** `Assets/ThirdParty/PolyHaven/<id>/<id>_albedo.jpg` (sRGB), `_normal.jpg` (NormalMap, OpenGL), `_mask.png` (sRGB **kapalı**; R=metalik, G=AO, A=pürüzsüzlük). Builder `TextureImporter` ayarlarını yapar (aniso 8, mipmap, maxSize 2048) ve yeniden içe aktarır.
- **Malzeme** `MakePbr(id, worldTileMeters)`: `Universal Render Pipeline/Lit`, `_BaseMap`, `_BumpMap` (+`_NORMALMAP`), `_MetallicGlossMap` (+`_METALLICSPECGLOSSMAP`), `_OcclusionMap` (+`_OCCLUSIONMAP`), `_Smoothness=1` (harita ölçekler), `_Metallic=1` yalnızca metal setlerinde (aksi halde harita R=0 zaten). `Assets/Generated/Materials/` altına kaydet. Doku yoksa mevcut düz renk malzemeye düş.
- **Dünya ölçekli UV:** Unity küp primitive'i dokuyu gerer. `BoxMeshFactory.Create(Vector3 size, float tileMeters)` her yüzde UV = yüz boyutu / tileMeters olan, normal + tangent'lı kutu mesh üretir; `Assets/Generated/Meshes/` altına asset olarak kaydedilir (aynı boyut+tile için yeniden kullan). Duvarlar/zemin/bariyerler bunu kullanır; `BoxCollider` boyutu eşleşir.
- Doku eşleşmesi: zemin `asphalt_02` (tile 3 m), iç zemin şeritleri `concrete_floor_worn_001`, çevre duvarları `concrete_wall_008`, bina cepheleri `red_brick_03`, bariyerler `painted_concrete`, konteyner/sandıklar `rusty_metal_02`, silah yer tutucu `metal_plate`.
- **Gökyüzü/ortam:** `Assets/ThirdParty/PolyHaven/HDRI/*.hdr` → TextureImporter shape Cube; `Skybox/Cubemap` malzemesi; `RenderSettings.ambientMode = Skybox`, yansıma kaynağı skybox; bir `ReflectionProbe` (realtime, OnAwake, merkezde, kutu sahne boyutu). Üstel sis, gökyüzü tonuyla uyumlu, yoğunluk ~0.008.
- **Işık:** bulutlu öğleden sonra; directional soft shadows, sıcaklık modu açık (~6200 K), intensity ~1.6; gölge çözünürlüğü yüksek.
- **URP asset (PC_RPAsset):** HDR açık, shadow distance 70, 4 cascade, soft shadows yüksek kalite, render scale 1. API ile ayarlanamayanlar `SerializedObject` ile; değişiklikleri raporla. Pipeline değişikliği yok.
- **Renderer (PC_Renderer):** `ScreenSpaceAmbientOcclusion` renderer feature yoksa ekle (radius ~0.4, intensity ~1.2). Ekleme başarısızsa log + devam.
- **Post-process:** global `Volume`, `Assets/Generated/PostProfile.asset`: Tonemapping ACES, Bloom (threshold 1.0, intensity 0.25, scatter 0.6), Color Adjustments (postExposure 0.1, contrast 12, saturation −18), White Balance (temperature −5), Vignette 0.22, Film Grain düşük (0.12), Motion Blur **kapalı**. Kamera: `renderPostProcessing = true`, antialiasing **TAA** (yoksa SMAA High), near clip 0.03.
- **Düzen:** Terk edilmiş kentsel otopark/avlu: asfalt zemin, beton çevre duvarları (4.5 m), iki yanda tuğla bina cepheleri (pencereli için iç girintili kutular), jersey beton bariyerleri (3 kademeli kesitli kutu yeterli), paslı konteyner (6×2.4×2.6 m) ve sandık yığınları, "Near obstruction" adlı obje ve konumu **korunur** (validation ve `--qa-wall` kullanıyor), oyuncu başlangıcı (0,0.05,-4) korunur. Okunabilir siluetler için arka planlar orta tonlu.

## 10. Harici model kancaları (Blender çıktıları)

Builder, şu dosyalar varsa kullanır, yoksa primitive yer tutucuya düşer (her ikisi de çalışmalı):
- `Assets/Art/Characters/Zombie/zombie.fbx` — Humanoid olarak içe aktar (ModelImporter `animationType = Human`, avatar oluştur). Kemik adları: `Hips, Spine, Chest, Neck, Head, LeftUpperArm, LeftLowerArm, LeftHand, RightUpperArm, RightLowerArm, RightHand, LeftUpperLeg, LeftLowerLeg, LeftFoot, RightUpperLeg, RightLowerLeg, RightFoot`. Builder modeli zombi köküne yerleştirir, `Animator`'ı (controller'sız) bırakır, `GetBoneTransform` ile hitbox kapsülleri/ragdoll eklemlerini kemiklere kurar (Head → HitZone.Head, Hips/Spine/Chest → Body, kollar/bacaklar → Limb). Avatar geçersizse uyar ve primitive'e düş.
- `Assets/Art/Weapons/Pistol/pistol.fbx` — statik mesh; orijin kabzada, +Z namlu yönü; alt objeler `Muzzle` (boş), isteğe bağlı `Slide`, `Magazine`. Viewmodel'de primitive silahın yerini alır; `Muzzle` yoksa mesh sınırlarının +Z ucu.
- `Assets/Art/Characters/Arms/arms.fbx` — varsa viewmodel'e eklenir (yalnız görsel).
- Ölçek: FBX içe aktarmada `globalScale` dokunulmaz; builder sınır kutusuna göre zombiyi 1.8 m boya, tabancayı 0.2 m uzunluğa ölçekler.

## 11. Doğrulama (uygulayıcı çalıştırır, sonuçları aynen raporlar)

Unity: `"C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe" -batchmode -quit -projectPath E:\ZOMBOSLAND -executeMethod <Method> -logFile E:\ZOMBOSLAND\Logs\<ad>.log`. Editör açıkken (kilit) çalışmaz; öyleyse dur ve raporla.

1. `WeaponValidation.Run` — mevcut testler + yeni: `ReloadProgress` 0→1, `TryDryFire` yalnızca boşken, ve `DamageInfo` ile ölü zombiye ikinci vuruşun `false` dönmesi (saf mantık testleri; MonoBehaviour gerekiyorsa EditMode'da `new GameObject` ile).
2. `SandboxBuilder.Build` — `ZOMBOSLAND_BUILD_RESULT: Succeeded errors=0`.
3. `SandboxValidation.Run` — mevcut 4 kontrol (yeni hiyerarşiye uyarlanmış ama aynı amaç) + yeni: NavMesh üretim kodu derleniyor ve sahnede `ZombieDirector` var; her zombide ragdoll parçaları kinematic başlıyor; `AimPivot` var ve `Camera` onun çocuğu; PostProfile atanmış global Volume var; PolyHaven malzemeleri doku içeriyor (doku dosyaları varsa).
4. Log'larda derleme hatası ve yeni uyarı listesi (`grep -E "error CS|warning CS"`).

Teslim raporu: değişen/eklenen dosyalar, her doğrulamanın log yolu ve gerçek sonucu, yapılmayanlar ve neden, bilinen riskler. Ölçüm yapılmadıysa "ölçülmedi".
