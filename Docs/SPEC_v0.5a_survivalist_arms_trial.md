# SPEC v0.5a — Survivalist el–kol denemesi (karşılaştırma sürümü)

Amaç: Mevcut Infima LPSP "Cubic" kollarına alternatif olarak Slayver "Survivalist character" FPS kollarını **aynı tabanca (Handgun_03), aynı sahne, aynı ışık ve aynı oyun kamerası** ile denemek ve iki seti yan yana ekran görüntüsüyle karşılaştırmak. Bu bir deneme; varsayılan oyun davranışı değişmez.

## Girdi varlıkları (zaten projede)
- `Assets/ThirdParty/Survivalist/Meshes/SK_Military_Survivalist.fbx` — Humanoid, UE4 Mannequin iskeleti. FPS kolları alt nesne `SK_Miliary_FPS_Arms` (yazım paketteki gibi). Diğer modüller (Shirt3, Gloves1, Vest, Cap, Backpack, Head...) bu denemede gizli.
- `Assets/ThirdParty/Survivalist/Materials/URP_*.mat`, `Textures/T_Military_{Hands1,Hands3,Gloves1,Shirt3}_*.png`.
- `Assets/ThirdParty/KevinIglesiasSoldier/Animations/HumanM@Gun_*.fbx` — Humanoid üçüncü şahıs tabanca klipleri (yalnızca yedek yol).
- Mevcut: `Assets/ThirdParty/InfimaLPSP/...` kollar, Handgun_03, şarjör, `A_FP_PCH_Handgun_*` klipleri. `ViewmodelFactory` bunları `Assets/Generated/Animators/VM_Arms.controller` ve `VM_Pistol.controller` ile bağlar.

## Önemli gözlem
LPSP kol FBX'i ve Survivalist aynı UE4 kemik adlarını taşır (root, pelvis, spine_0x, clavicle_l/r, upperarm, lowerarm, hand, parmaklar, twist kemikleri). LPSP ayrıca `ik_hand_root/ik_hand_gun` taşır; tabanca bu kemiğe bağlıdır.

## Yaklaşım (sırayla dene, ilk çalışanı kullan, hangisini kullandığını raporla)
1. **Generic yol eşleşmesi:** LPSP kol kliplerini (aynı `VM_Arms.controller`) Survivalist iskeletinde oynat. Survivalist örneğinde, LPSP'deki göreli yol ile aynı yere `ik_hand_root/ik_hand_gun` boş nesneleri oluştur ki tabanca LPSP'deki gibi takip etsin. Ön koşul: hiyerarşi yolları ve birim ölçeği uyuşmalı (FBX import scale / bind pose). Survivalist FBX'inin rig tipini değiştirmen gerekiyorsa `.meta`'yı değil, editör kodunda `ModelImporter` ile ayarla ve raporla. Uyuşmazlık varsa (ör. 100x ölçek, farklı kök yolu) bunu ölç ve yaz.
2. **Humanoid retarget:** LPSP kol FBX'i tam UE4 iskeleti (bacaklar dahil) taşıdığı için Humanoid avatar üretilebilir. LPSP kliplerini Humanoid olarak Survivalist'e retarget et; tabancayı Survivalist `hand_r` kemiğine, LPSP'deki `ik_hand_gun`→`hand_r` göreli dönüşümünü (Idle_Pose'da ölçülmüş) kullanarak bağla.
3. **Yedek:** Kevin Iglesias `HumanM@Gun_*` klipleri. Yalnızca 1 ve 2 başarısızsa.

Yaklaşım 1 veya 2 kullanılırsa mühimmat, reload süresi, animasyon olayları (`ViewmodelAnimationEvents`) ve tabanca/şarjör animasyonu mevcut haliyle korunmalı.

## Uygulama kısıtları
- Varsayılan davranış **aynı kalır**: argüman yoksa LPSP kolları kullanılır; mevcut QA çıktıları değişmemeli.
- Yeni başlangıç argümanı: `--arms=survivalist` (varsayılan `lpsp`). `QaArgs` üzerinden okunur.
- `SandboxBuilder` iki kol düzeneğini de viewmodel altında üretir (`Arms` mevcut, `Arms_Survivalist` yeni, varsayılan pasif). Çalışma anında seçilen düzenek etkinleşir ve `ViewmodelPresenter`'ın `armsAnimator`/`pistolAnimator`/`muzzle`/`eject` referansları seçilene göre ayarlanır (Awake sırası sorununa dikkat; presenter'ı seçimden önce kullanma). Tabanca iki düzenekte ayrı örnek olabilir.
- Survivalist malzemeleri: projedeki LPSP gibi kodla URP/Lit malzeme kur (`Assets/Generated/Materials/VM_Surv_*.mat`) — ten (Hands1 veya Hands3; FPS_Hands1 .mat dosyası hangi dokuları kullanıyorsa onu esas al), eldiven (Gloves1), kol kumaşı (Shirt3) varsa. Paket .mat dosyalarını oyunda kullanmak zorunda değilsin; doku eşlemesini bunlardan oku.
- Kollarda gölge kapalı, `updateWhenOffscreen` açık, collider yok, layer 0 — LPSP ile aynı kurallar.
- `--qa-screens` çıktısı `--arms=survivalist` ile `QaScreens_survivalist/` klasörüne yazılsın (varsayılan klasör `QaScreens/` aynı).
- Paket scriptleri, StarterAssets, ProjectSettings, Packages manifest **değiştirilmez**. Yeni üçüncü taraf varlık eklenmez.
- `SandboxValidation` / `WeaponValidation` mevcut kontrolleri geçmeye devam etmeli; Survivalist düzeneği için null malzeme/eksik kemik uyarısı `ViewmodelFactory.Notes`'a yazılsın.

## Doğrulama (gerçekten çalıştır, sonuçları olduğu gibi raporla)
Unity: `C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe`. Proje: `E:\ZOMBOSLAND`. Başka bir editör bu projeyi açıkken batch çalıştırma.
1. `Unity.exe -batchmode -quit -projectPath E:\ZOMBOSLAND -executeMethod SandboxBuilder.Build -logFile Logs\v05a-build.log` → hata yok, `Build/Zombosland.exe` güncel.
2. `Build\Zombosland.exe --qa-screens -logFile Logs\v05a-screens-lpsp.log` ve `Build\Zombosland.exe --qa-screens --arms=survivalist -logFile Logs\v05a-screens-surv.log` → iki klasörde 12'şer PNG, `QA_SCREENS_DONE`.
3. `Build\Zombosland.exe --qa-selftest` her iki kol seçeneğiyle → `QA_SELFTEST` satırları geçiyor.
4. PNG'lere kendin bak (01_hip, 02_ads, 03_fire, 05_reload_mid, 06_reload_late, 08_empty_reload, 10_wall). Kontrol: parmak–kabza teması, el–silah boşluğu/iç içe geçme, bilek kırılması, kol kesiği/kamera içi görünen mesh, nişan hizası, hedef çevresinin okunurluğu.

## Rapor
`Docs/IMPLEMENTATION_REPORT.md` sonuna "v0.5a Survivalist kol denemesi" bölümü: kullanılan yaklaşım ve nedeni, değişen dosyalar, ölçülen değerler (ölçek, ofsetler), çalıştırılan komutlar ve sonuçları, görsel sorun listesi (görüntü adlarıyla), ölçülmeyen/yapılmayanlar. Yapılmamış testi yapılmış gibi yazma.
