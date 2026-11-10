# SPEC v0.7 — Balta, gerçek ellerle (dikey dilim)

Amaç: Survivalist elleriyle iki elle tutulan balta. Tek deneme silahı; beğenilirse aynı yol çekiç ve ilaçlara uygulanacak. Mevcut sopa (bat) ve tabanca davranışı değişmez.

## Hazır varlıklar (değiştirme, Blender'dan geldi)
`Assets/Art/Viewmodels/Axe/`
- `A_FP_Axe_Idle.fbx` — Generic klip (2 kare) + `Armature/root/.../hand_r` altında `wooden_axe_02` mesh düğümü.
- `A_FP_Axe_Swing.fbx` — Generic klip, 60 fps, 61 kare (1,000 s). Vuruş anı normalize 0,35 (`Sandbox.MeleeHitAt` ile aynı).
- `T_Axe_albedo.png` (sRGB), `T_Axe_normal.png` (OpenGL normal), `T_Axe_mask.png` (R metalik, G AO, A pürüzsüzlük; `LookDevSetup` ile aynı kural).
Kaynak: Poly Haven `wooden_axe_02`, CC0, yazar Ulan Cabanilla, https://polyhaven.com/a/wooden_axe_02. Üretim kaynağı: `SourceArt/Axe/`.

## Yapılacaklar

### 1. İçe aktarma (`Assets/Editor/ViewmodelFactory.cs`)
İki FBX için `ModelImporter`, LPSP `A_FP_PCH_Handgun_Idle_Pose.fbx.meta` ile aynı: `globalScale 0.01`, `useFileScale true`, `animationType Generic`, `importAnimation true`, `resampleCurves false`, `animationCompression Off`, malzeme içe aktarma kapalı. `clipAnimations` tek klip: take adı FBX'ten okunur, klip adı `A_FP_Axe_Idle` (loopTime true) / `A_FP_Axe_Swing` (loopTime false). Yalnızca ayar değiştiyse `SaveAndReimport`. Dokular: normal için `TextureImporterType.NormalMap`, maske `sRGBTexture=false`.

`VM_Axe` malzemesi (`LookDevSetup.MaterialFolder`, URP/Lit): `_BaseMap`, `_BumpMap` (`_NORMALMAP`), `_MetallicGlossMap` + `_METALLICSPECGLOSSMAP`, `_Smoothness 1`, `_SmoothnessTextureChannel 0`, `_BaseColor` beyaz.

### 2. Animator (`BuildArmsController`)
`VM_Arms` kontrolcüsüne iki durum: `AxeIdle` (A_FP_Axe_Idle), `AxeSwing` (A_FP_Axe_Swing), `Exit(AxeSwing, AxeIdle, 0.97f, 0.05f)`. Varsayılan durum `Pose` kalır. `reloadTime` yok.

### 3. Baltanın ele bağlanması
`AttachAxe(Transform handR)`: FBX içindeki `hand_r` altındaki `wooden_axe_02` düğümünün yerel konum/dönüş/ölçeğini oku; mesh'i yeni GameObject `Axe` olarak hedef `hand_r` altına aynı yerel dönüşümle koy. FBX `hand_r.lossyScale` ile hedefin ölçeği farklıysa farkı `Notes` raporuna yaz ve ölçeği dünya ölçeği tutacak şekilde düzelt. Gölge verme kapalı, gölge alma açık, `VM_Axe`, başlangıçta pasif. Hem LPSP (`ViewmodelRig`) hem Survivalist (`AlternateArms`) setinde yapılır; alan adı `axe` (GameObject). `SandboxBuilder` presenter'a aktarır.

### 4. Sunum (`ViewmodelPresenter`)
- `AlternateArms`'a ve presenter'a `axe` alanı. `Awake` seçimi sırasında etkin set belirlenir.
- `SetPistolVisible` listesine balta renderer'ları girmemeli.
- Yeni `SetAxeMode(bool on)`: açıkken kol renderer'ları görünür, tabanca renderer'ları (`ik_hand_gun` altındakiler) gizli, `axe` aktif; `armsAnimator.CrossFadeInFixedTime("AxeIdle", 0.15f, 0, 0f)`. Kapanışta `axe` pasif, `CrossFadeInFixedTime(Pose, 0.15f, 0, 0f)`, görünürlük tabanca durumuna döner.
- `PlayAxeSwing()`: `armsAnimator.CrossFadeInFixedTime("AxeSwing", 0.03f, 0, 0f)`.
- Tabanca durumları (`Fire`, reload, ADS sesi) balta modunda tetiklenmemeli; `AdsHeld` zaten sıfırlanıyor, doğrula. Reload başlıyorsa baltada engellendiğini kontrol et, engellenmiyorsa bildir.

### 5. Oyun mantığı
- `SurvivalAssets.Items`: `new ItemSpec("axe", "Balta", "Yıpranmış demir başlı, ağır bir balta. Yakın dövüşte güçlüdür.", ItemKind.Melee, 1, damage: 60f, range: 2.0f, seconds: 1.0f)`. Tarif EKLEME (oyun ekonomisi kararı ürün sahibinin). `make("bat", ...)` simge üreticisini örnek alıp balta simgesi ekle.
- `Sandbox.Melee`: `meleeItem` kimliği `axe` ise `presenter.SetAxeMode(true)` ve eski `meleeView` pasif; `TryMeleeSwing` içinde `meleeAnim.Play` yerine `presenter.PlayAxeSwing()`. Başka melee veya `null` olduğunda balta modu kapanır, mevcut akış aynen işler. Hasar zamanlaması (`MeleeHitAt`) değişmez.
- `--give-axe` başlangıç argümanı: oyuncuya çantada yoksa 1 balta verir ve hızlı yuva 2'ye atar. Yalnızca kullanıcının elle denemesi için.

### 6. QA: `--qa-axe` (`Assets/Scripts/QaAxe.cs`, `QaScreensOpenWorld` düzeninde; `Sandbox`'ta aynı biçimde eklenir, `SaveSystem:52` satırına da eklenir ki kayıt yazmasın)
Baltayı kuşan, `armsAnimator.speed = 0`, sırayla `Play` + `Update(0)`, `Build/QaAxe/` altına PNG:
`idle`, `swing_12` (12/60), `swing_16` (16/60), `swing_19` (19/60), `swing_21` (21/60), `swing_24` (24/60), `swing_36` (36/60), `swing_50` (50/60).

İdle ve `swing_21` için kol kök nesnesine (`Arms_Survivalist`, viewmodel yerel uzayı) göre konum ölç, `QA_AXE <etiket> <ad> ölçülen beklenen fark` yaz. Tolerans 0,5 cm:

| Etiket | hand_r | hand_l | lowerarm_r | Balta mesh sınır merkezi (`renderer.bounds.center`) |
|---|---|---|---|---|
| idle | 0.14608, -0.04998, 0.25698 | 0.13697, -0.25150, 0.21557 | 0.12821, -0.10377, -0.00675 | 0.15870, -0.04035, 0.43307 |
| swing_21 | 0.08593, -0.18229, 0.33377 | 0.26879, -0.17690, 0.23922 | 0.14717, -0.27813, 0.08917 | 0.09340, -0.32416, 0.43902 |

Ardından animator hızını 1 yap, oyunun gerçek yolu ile: `QaMeleeSwing()`, 1,2 s bekle, `MeleeSwings` 1 arttı mı ve durum `AxeIdle` mı; `UseQuick(0)` ile tabancaya dön, 0,4 s sonra durum `Pose`, `axe` pasif, tabanca renderer'ı açık. Sonuç satırları: `QA_AXE_RESULT PASS|FAIL <neden>`, en son `QA_AXE_DONE`, `GameQuit.Request()`.

### 7. Lisans
`Assets/Art/Viewmodels/Axe/LICENSE.md`: kaynak, CC0, yazar, indirme tarihi 2026-11-08, klipler projede üretildi.

## Sınırlar
- Paket veya Unity sürümü değiştirme. Var olan klip, tabanca, sopa kodlarının davranışını değiştirme.
- Sistemde derleme ve build komutlarını sen çalıştırma; yalnızca kaynak değişikliği yap. Kontrolü ana ajan yapar.
- Çalıştırmadığın testi yapılmış gibi yazma.
