# Zombosland — Kod Haritası (CODEMAP)

Üretildi: 2026-11-10. Durum: README v0.7 esas alındı; kodda v0.8 parçaları da var (`QuestState` sonundaki iki durum, `OpenWorldBuilder.Rear.cs`, `Docs/SPEC_v0.8_hands_loop.md`).
Satır numarası bilerek yok: eskir. Sabit ve metot adlarıyla ara (`Grep`).
Yenileme: yeni `.cs` eklenince ilgili bölüme bir satır ekle; yeni public üye eklenince yalnız "Hızlı bul" tablosunu güncelle. Dosya taşınınca veya silinince bu haritayı da düzelt.

Sayılar: `Assets\Scripts` altında 69 `.cs` (50 kök + 19 `Survival`; 10 Kasım'da eklenenler: `Sandbox.HitFeel.cs`, `MainMenu.cs`, `SettingsPanel.cs`). `Assets\Editor` altında ayrıca 20 `.cs` (Scripts altında değil; bkz. bölüm 6). Tüm `Scripts` dosyaları bu haritada tam bir kez geçer.
Yöntem: Dosyalar tamamen okunmadı. Sınıf bildirimleri, üye imzaları, sabitler, sınıf üstü yorumlar ve kritik sayılar `Grep`/okuma ile doğrulandı. Emin olunmayan yerler "(doğrulanmadı)" ile işaretli.

Yol kuralı: Tablolardaki dosya yolu `Assets\Scripts`'e göreli. `Editor` ve `Data` yolları açıkça yazılı.

---

## 1. Hızlı bul

| Şunu değiştirmek istiyorsan | Dosya | Sabit / metot |
|---|---|---|
| Zombi kovalama hızı | `ZombieBrain.cs` | `chaseSpeed` = `Random.Range(1.6, 2.2)` (zombi başına bir kez atanır; `const` değil) |
| Zombi yürüme hızı | `ZombieBrain.cs` | `WalkSpeed` 1.1 (`InvestigateSpeedFactor` 1.5) |
| Zombi koşu/yürüyüş animasyon hızı | `ZombieAnimator.cs` | `clipRunSpeed` 3, `clipWalkSpeed` 1.2 |
| Zombi canı | `ZombieBrain.cs` | `MaxHealth` 100 |
| Zombi görüş menzili / açısı | `ZombieBrain.cs` | `SightRange` 18, `SightHalfAngle` 55, `CloseSense` 2 |
| Zombi saldırı hasarı / menzil / bekleme | `ZombieBrain.cs` | `AttackDamage` 10, `AttackRange` 1.6, `AttackCooldown` 1.2, `AttackWindupSeconds` 0.75 |
| Zombi durum makinesi | `ZombieBrain.cs` | `enum ZombieState` (Idle, Wander, Chase, Attack, Stagger, Dead, Investigate, BreakBarricade), `OnNoise`, `OnDistraction` |
| Zombi spawn / yerleşim noktaları | `Editor/IlkIkmalBuilder.cs` (`ZombieSpot`, `BuildZombies`), `Editor/SandboxBuilder.cs` (`BuildDirectorAndZombies`), `Editor/OpenWorldBuilder.Gameplay.cs` (açık bölge zombi noktaları) | runtime: `ZombieDirector.SpawnFixed(count, seed)`, `Spawn`, `navCenter`, `navSize` |
| Ceset limiti / ceset süresi | `ZombieDirector.cs` | `MaxCorpses` 24, `CorpseSeconds` 20 |
| Tabanca hasarı (taban) | `Damage.cs` | `DamageRules.BaseDamage` 34 (`Sandbox.Weapon.cs` bunu kullanır) |
| Bölgesel hasar çarpanları (kafa ×3, uzuv ×0.7, gövde ×1) | `Damage.cs` | `DamageRules.Multiplier(HitZone)` |
| Sendeleme (hafif / ağır) | `Damage.cs`, `ZombieBrain.cs` | `PoiseThreshold` 60, `PoiseWindow` 1.2, `HeavyStaggerCooldown` 1.5, `HeavyStaggerSeconds(zone)` |
| Tabanca şarjörü / atış aralığı | `WeaponState.cs` | `Capacity` 12, `ShotInterval` 0.25, `TryFire`, `TryDryFire` |
| Tabanca reload süresi | `WeaponState.cs` | `TacticalReloadSeconds` 1.41, `EmptyReloadSeconds` 1.93, `BeginReload` |
| Tabanca yedek mermi / menzil / gürültü | `Sandbox.Weapon.cs` | `startReserve` 72 (CombatSandbox; İlk İkmal sahnesinde 36), `Range` 100, `noiseRadius` 30 |
| Geri tepme (nişan) | `Sandbox.Weapon.cs`, `PlayerMotor.cs` | `Fire()` içinde `Recoil + 1.2`, en çok 4; `PlayerMotor` içinde 12/s sönüm |
| Ateş sonrası kamera sarsıntısı | `Sandbox.Weapon.cs`, `CameraEffects.cs` | `AddShake(0.12)`; `CameraEffects.LateUpdate` |
| Yakın dövüş (sopa / balta) değerleri | `Item` asset'leri `Assets/Data/Items/bat.asset`, `axe.asset` | `meleeDamage` (sopa 40, balta 60), `meleeSeconds` (sopa 0.7, balta 1), `meleeRange` (sopa 1.9) |
| Yakın dövüş vuruş anı / küre | `Sandbox.Melee.cs` | `MeleeHitAt` 0.35, `MeleeSphere` 0.18, `MeleeNoiseRadius` 5, `TryMeleeSwing`, `MeleeHit` |
| Oyuncu yürüme / koşma / eğilme hızı | `PlayerMotor.cs` | `Tick()` içinde sabit değerler: yürüme 4, koşma 6, eğilme 2.2 m/s (sabit tanımlı değil, satır içi) |
| Kamera sarsıntısı (ayar) | `GameSettings.cs`, `CameraEffects.cs` | `GameSettings.CameraShake` (0–1), `AddShake` |
| Headbob | `CameraEffects.cs`, `GameSettings.cs` | `LateUpdate` içinde genlik 0.012 / 0.025; `GameSettings.Headbob` |
| FOV / fare hassasiyeti / ana ses | `GameSettings.cs` | `Fov` 75, `Sensitivity` 2, `MasterVolume` 1 |
| Oyuncu sağlığı | `HealthState.cs`, `Sandbox.cs` | `new HealthState(100)`, `ApplyDamage(in DamageInfo)`, `Heal`, `RestoreHealth` |
| Bandaj (iyileşme / süre) | `Assets/Data/Items/bandage.asset`, `Assets/Data/Recipes/bandage.asset` | `healAmount` 25; tarif süresi 3 s. Kullanım: `PlayerSurvival.TryUseBandage`, `Sandbox.Hands.cs` `TryBeginBandage`, animasyon `ViewmodelPresenter.BandageSeconds` 1.70 / `BandageCommit` 1.20 |
| Envanter / çanta kapasitesi | `Survival/PlayerSurvival.cs`, `Survival/Inventory.cs` | `BagSlots` 24, `StorageSlots` 24, `QuickSlots` 4, `maxStack` (her `ItemDef`) |
| Toplama (loot) | `Survival/LootPickup.cs`, `Survival/WorldState.cs`, `Editor/OpenWorldBuilder.Gameplay.cs` | `LootPickup.Interact`, `WorldState.MarkLoot`, `LootDef` (loot planı) |
| Üretim tarifleri | `Assets/Data/Recipes/*.asset` (6 adet), `Survival/RecipeDef.cs` | `seconds`, `needsBench`, `inputs`, `outputCount`; üretim `Survival/Crafter.cs` `Start`, `Tick`, `Check` |
| Üretim adet üst sınırı | `Survival/Crafter.cs` | `MaxQuantity` 99 |
| Tezgâh menzili / tezgâh tarifleri | `Survival/Workbench.cs` | `BenchRange` 3.5, `AnyInRange` |
| Depo (yard sandığı) | `Survival/StorageBox.cs` | `lidOpenAngle` 70, `OpenSeconds` 0.25, `CloseSeconds` 0.3 |
| Barikat / yapı yuvaları | `Survival/BuildSlot.cs`, `Assets/Data/Recipes/barricade_kit.asset`, `chest_kit.asset`, `repair_kit.asset` | `MaxHealth` 100, `RepairAmount` 60, `ChestExtraSlots` 12, `ForceState`, `NearestBuiltBarricade` |
| Görev akışı (sigorta ve v0.8 adımları) | `QuestFuse.cs` | `enum QuestState`, `ObjectiveText`, `ObjectiveMarker`, `StallSeconds` 20; sigorta: `FuseItem.cs`, `FusePanel.cs` |
| Mühimmat kutusu | `AmmoCrate.cs` | `Rounds` 12 |
| Kapı açılış | `DoorInteractable.cs` | `openAngle` 90, `OpenSeconds` 0.6 |
| İpuçları (kart süresi, hatırlatma, tetikleyici) | `HintDirector.cs` | `CardSeconds` 6, `ReminderGap` 20, `MaxReminders` 2, `AimMin`/`AimMax`, `IdleSeconds` 3; PlayerPrefs `hint_<id>` |
| HUD (can, mermi, pusula, görev, toast) | `Hud.cs` | `Fill`; düzen sanal 1080p boyutuna göre |
| Envanter / üretim ekranı | `Survival/SurvivalUi.cs` | `Draw`, `DrawQuickBar`, `Cols` 6 |
| Esc (duraklatma) menüsü | `PauseMenu.cs` | `Draw(Sandbox, w, h)`, `Reset`, `OnEscape`; Devam et / Ayarlar / Kaydet / Son kaydı yükle / Yeni oyun / Ana menüye dön / Çıkış |
| Ayarlar ekranı (Oyun / Ses / Grafik / Kontroller) | `SettingsPanel.cs`, `GameSettings.cs` | `SettingsPanel.Draw(w, h, bool)`, `Reset`; `GameSettings.ApplyVideo`, `InvertY`, `VideoTouched`, `FpsOptions` |
| Ana menü (iki sahnede de) | `MainMenu.cs` | `Active`, `Available`, `TryBegin` (`Sandbox.Start` çağırır), `Draw`, `RequestReturn`; İlk İkmal'de Devam et / Yeni oyun, CombatSandbox'ta Başla; `--qa*`, `--perf*`, `--skip-menu` ve batch kipinde çıkmaz |
| Menü bileşenleri (düğme, onay penceresi, sürgü vb.) | `Survival/UiTheme.cs` | `MenuButton`, `MenuButtonKind`, `Confirm`, `MenuScrim` |
| Hit-stop ve yakın dövüş sarsıntısı (ayar sabitleri) | `Sandbox.HitFeel.cs` | `HitStopMeleeSeconds` 0.05, `HitStopHeadKillSeconds` 0.03, `HitStopTimeScale` 0.05, `MeleeShakeBat` 0.22, `MeleeShakeAxe` 0.30, `MeleeKillShakeBonus` 0.12 |
| Yerde kan lekesi / kafa vuruşu kanı | `EffectPool.cs` | `BloodStain`, `StainCount` 12, `BloodHeadBurst` 24 |
| Ortak UI stili (renk, buton, ikon) | `Survival/UiTheme.cs` | `Hex`, `Button`, `ItemIcon`, `Name` |
| Ekrandaki tuş adları | `InputLabels.cs` | `Move`, `Interact`, `Inventory`, `Reload`, `Key(...)`, `Prompt(...)` |
| Girdi (tuş bağlama) | `Sandbox.Weapon.cs`, `Interactor.cs`, `PlayerMotor.cs`, `Sandbox.Hands.cs` | Kodda sabit: R (yeniden doldur), sol fare (ateş), sağ fare (nişan), E (`Interactor.Press`), H (`InputLabels.HolsterKey`), Horizontal/Vertical eksen. Girdi Manager dosyası yok |
| Ses efektleri | `SfxBank.cs` | `enum Sfx`, `Play`, `Play2D`, `Has`; prosedürel sentez `SfxSynth` |
| VFX (kovan, kan, mermi izi, flaş) | `EffectPool.cs` | `MuzzleFlash`, `EjectCasing`, `WorldImpact`, `BloodImpact`, `BulletDecal`; `CasingCount` 12, `DecalCount` 64 |
| Toast / alım satırı | `Survival/Notifier.cs`, `Survival/PickupFeed.cs` | `Show` (`MaxQueued` 3); `Add` (`MaxLines` 4, `LineSeconds` 1.8) |
| Kayıt / yükleme (JSON, slot) | `Survival/SaveSystem.cs`, `Survival/WorldState.cs` | `Version` 1, `Save`, `TryLoad`, `UseSlot`, `FilePath`, `HasSave`; `WorldState.Capture`, `LoadedFromSave` |
| El / viewmodel animasyonu | `ViewmodelPresenter.cs`, `ViewmodelAnimationEvents.cs`, `Editor/ViewmodelFactory.cs` | `SetCarrier`, `PlayDraw`, `PlayHolster`, `PlayAxeSwing`, `PlayBatSwing`, `OnShot`; süreler `ReloadClipSeconds` 2.233 |
| El eylemleri (alma, kutu, bandaj, çekme) | `Sandbox.Hands.cs`, `IInteractable.cs` | `TryBeginItemAction`, `TryBeginBandage`, `ToggleHolster`, `CancelHandAction`; `HandActionKind`, `HandCarrier` |
| Sopa / balta görünümü (yer tutucu) | `Survival/MeleeViewmodel.cs` | `Play(float)`, `defaultSeconds` 0.7 |
| Zombi kol/bacak tepkisi | `ZombieAnimator.cs` | `TriggerAttack`, `TriggerHeavyHit`, `FlinchStiffness` 900, `FlinchKick` 65 |
| Ölüm ragdoll'u | `ZombieRagdoll.cs` | `Activate` |
| Sahne üretimi: CombatSandbox | `Editor/SandboxBuilder.cs` | `Regenerate and Build` (MenuItem), `Build Existing Scene`, `BuildLighting`, `Box`, `Mat` |
| Sahne üretimi: İlk İkmal (harita, kapılar, zombiler) | `Editor/IlkIkmalBuilder.cs` | `BuildAll` (`-executeMethod`), `BuildDirector`, `BuildZombies`, `Free`/`Solid` listeleri |
| Sahne üretimi: açık bölge (arazi, yol, bina, loot) | `Editor/OpenWorldBuilder*.cs` (6 dosya) | `OpenWorldBuilder.cs` yerleşim verisi; `.Terrain`, `.Roads`, `.Props`, `.Gameplay`, `.Rear` |
| Zombi prefab'ları | `Editor/ZombieFactory.cs` | `CreatePrefabs`, `PxlModels`, `RioPath`, `FbxPath` |
| Doğrulama (CombatSandbox / İlk İkmal / silah) | `Editor/SandboxValidation.cs`, `Editor/IlkIkmalValidation.cs`, `Editor/WeaponValidation.cs` | `Require`, `Run` (`-executeMethod IlkIkmalValidation.Run`) |
| Betikli QA girişleri | `QaSelfTest.cs`, `QaPlaythrough.cs`, `QaSurvival.cs`, `QaHands.cs`, `QaReload.cs`, `QaAxe.cs`, `QaScreens*.cs` | Bkz. bölüm 2.10 |
| Komut satırı argümanları | `BootRouter.cs`, `QaArgs.cs`, `PerfHarness.cs` (`RuntimeBootstrap`), `Sandbox.cs` (`Awake`), `Survival/SaveSystem.cs` | Bkz. bölüm 4 |
| Performans ölçüm aracı | `PerfHarness.cs` | `PerfHarness` (`Seed` 12345, `zombieCount`, `warmupSeconds`, `measureSeconds`), `Sandbox.EnterPerfMode` |
| İnsan oyun testi kaydı | `PlaytestLog.cs` | `BeginLevel`, `RecordShot`, `RecordDamage`, `RecordQuest`, `WriteComplete`, `WritePartial` |

---

## 2. Sistem bölümleri

Sütunlar: Dosya (`Assets\Scripts`'e göre) | Ana tip(ler) | Sorumluluk | Önemli üyeler.

### 2.1 Oyuncu

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `Sandbox.cs` | `Sandbox` (partial, `MonoBehaviour`, `IDamageable`), `GameQuit` | Oyuncu kökü: can, duraklatma, ölüm ve yeniden başlatma, güvenli nokta, QA kancaları. Sunum bileşenleri yalnız okur | `Health`, `ApplyDamage`, `Restart`, `IsDead`, `Survival`, `Motor`; `GameQuit.Request()` |
| `PlayerMotor.cs` | `PlayerMotor` | Bakış (gövde yaw, `AimPivot` pitch), yürüme/koşma/eğilme/zıplama, geri tepme değeri | `Teleport`, `Tick`, `InputEnabled`, `Recoil`, `Sprinting`, `Crouching` |
| `CameraEffects.cs` | `CameraEffects` | Yalnız görsel kamera ofseti: headbob, sarsıntı (trauma), FOV geçişi. Nişan ışını bu transform'u okumaz | `AddShake`, `LateUpdate` |
| `HealthState.cs` | `HealthState` (sealed) | Can değeri ve sınır kuralları | ctor(max), `Damage`, `Heal`, `Restore` |
| `GameSettings.cs` | `GameSettings` (static) | PlayerPrefs ayarları; değerler her karede okunur | `Sensitivity` 2, `Fov` 75, `Headbob`, `WeaponSway`, `CameraShake`, `MasterVolume`; `Load`, `Save`, `ApplyAudio` |

### 2.2 Silah & Savaş

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `Sandbox.Weapon.cs` | `Sandbox` (partial) | Ateş girdisi (yarı otomatik), ışın, hasar uygulama, geri tepme, ateş sesi ve gürültü | `Range` 100, `startReserve` 72, `noiseRadius` 30, `ResolveShot`, `ShotMask`, `Weapon`, `AdsHeld` |
| `WeaponState.cs` | `WeaponState` (sealed) | Saf durum: şarjör, yedek, atış aralığı, reload. Ses ve animasyon bunu değiştiremez | `Capacity` 12, `ShotInterval` 0.25, `TacticalReloadSeconds` 1.41, `TryFire`, `BeginReload`, `AddReserve` |
| `Damage.cs` | `DamageInfo` (struct), `HitZone`, `HumanLimb`, `IDamageable`, `DamageRules` | Hasar veri tipi ve bölge çarpanları, sendeleme sabitleri | `DamageRules.BaseDamage` 34, `Multiplier`, `HeavyStaggerSeconds`, `PoiseThreshold` |
| `Sandbox.Melee.cs` | `Sandbox` (partial) | Sopa/balta vuruş penceresi ve küre sorgusu; eşya değerlerini okur | `MeleeHitAt` 0.35, `MeleeSphere` 0.18, `SetMelee`, `TryMeleeSwing`, `MeleeLanded` |
| `Sandbox.Hands.cs` | `Sandbox` (partial) | El eylemleri: aynı anda bir tane; dünya değişikliği commit anında bir kez. Kavrama ve kaldırma | `TryBeginItemAction`, `TryBeginBandage`, `ToggleHolster`, `CancelHandAction`, `NotifyCrafting` |
| `Sandbox.HitFeel.cs` | `Sandbox` (partial) | Hit-stop (kısa zaman yavaşlaması) ve yakın dövüş kamera sarsıntısı; QA/perf çalıştırmalarında hit-stop kapalı | `StartHitStop`, `TickHitStop`, `MeleeHitFeedback`, `PlayHeadCrack` |
| `ViewmodelPresenter.cs` | `ViewmodelPresenter`, `AlternateArms` | Görünür kol ve silah animasyonları; yalnız `WeaponState` okur | `SetCarrier`, `PlayDraw`, `PlayHolster`, `OnShot`, `PlayAxeSwing`, `PlayBatSwing`, `ArmsSet` |
| `ViewmodelAnimationEvents.cs` | `ViewmodelAnimationEvents` | Klip olaylarının alıcısı; oyun durumu buna bağlı değil | `OnAnimationEndedReload`, `OnEjectCasing`, `OnSlideBack`, `OnAmmunitionFill` |
| `Survival/MeleeViewmodel.cs` | `MeleeViewmodel` | Sopa/balta yer tutucu görünümü; savurma eğrisi koddan | `Play(float)`, `defaultSeconds` 0.7 |

### 2.3 Zombi

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `ZombieBrain.cs` | `ZombieBrain`, `ZombieState` | Algı, açık durum makinesi, saldırı, hasar alma ve sendeleme kararı | `MaxHealth` 100, `SightRange` 18, `AttackDamage` 10, `Activate`, `Alert`, `ApplyDamage`, `Player` |
| `ZombieDirector.cs` | `ZombieDirector` (`Instance`) | Runtime NavMesh inşası, zombi aktivasyonu, spawn, ceset yönetimi | `Instance`, `player`, `zombiePrefabs`, `BuildNavMesh`, `SpawnFixed`, `RegisterCorpse` |
| `ZombieAnimator.cs` | `ZombieAnimator` | Klipleri sürer, prosedürel flinch ve saldırı pozu | `SetVariant`, `DriveClip`, `TriggerAttack`, `TriggerHeavyHit`, `StopClips` |
| `ZombiePart.cs` | `ZombiePart` | Kemik hitbox'ı; hasarı sahibine iletir, bölgeyi belirler | `Hit`, `owner` |
| `ZombieRagdoll.cs` | `ZombieRagdoll` | Ölümde kemik rigidbody'lerini kinematikten dinamiğe alır | `Activate` |

### 2.4 Survival / Envanter / Dünya

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `Survival/PlayerSurvival.cs` | `PlayerSurvival`, `UiTab` | Oyuncu çantası, depo, hızlı yuva, bandaj, atma, envanter ekranı durumu | `Instance` (public static alan), `Bag`, `Storage`, `Quick`, `BagSlots` 24, `UseQuick`, `TryUseBandage` |
| `Survival/Inventory.cs` | `Inventory`, `ItemStack` | Saf slot deposu ve üretim rezervasyonu; Unity sahnesi gerekmez | `Add`, `TryRemove`, `Reserve`, `CommitReserved`, `MoveSlotTo`, `Snapshot` |
| `Survival/ItemDef.cs` | `ItemDef` (SO), `ItemKind`, `ItemAmount` | Eşya tanımı | `id`, `kind`, `maxStack`, `healAmount`, `meleeDamage`, `meleeSeconds`, `noiseRadius` |
| `Survival/ItemDatabase.cs` | `ItemDatabase` (SO) | Id ile eşya ve tarif bulma | `FindItem`, `FindRecipe` |
| `Survival/RecipeDef.cs` | `RecipeDef` (SO) | Tarif: çıktı, girdiler, süre, tezgâh şartı | `output`, `outputCount`, `inputs`, `seconds`, `needsBench` |
| `Survival/Crafter.cs` | `Crafter`, `CraftCheck`, `CraftBlock` | Zamanlı üretim; zamanı dışarıdan (`now`) alır | `MaxQuantity` 99, `Check`, `Start`, `Tick`, `Cancel` |
| `Survival/LootPickup.cs` | `LootPickup` | Yerdeki eşya yığını; alınca çantaya geçer | `Interact`, `OnBlockedPress`, `SetRemaining`, `amount` |
| `Survival/StorageBox.cs` | `StorageBox` | Yard depo sandığı ve kapak animasyonu | `Interact`, `BeginLidOpen`, `lidOpenAngle` 70 |
| `Survival/Workbench.cs` | `Workbench` | Tezgâh: yakında olan oyuncu için tezgâh tarifleri açılır | `BenchRange` 3.5, `AnyInRange`, `Interact` |
| `Survival/BuildSlot.cs` | `BuildSlot`, `BuildKind` | Sabit yapı yuvası (barikat / sandık); durumu kaydedilir | `MaxHealth` 100, `RepairAmount` 60, `ChestExtraSlots` 12, `ForceState`, `Find` |
| `Survival/ThrownStone.cs` | `ThrownStone` | Dikkat taşı: ilk çarpmada veya 6 s sonra gürültü üretir | `Spawn`, `Launch`, `noiseRadius` 16 |
| `Survival/WorldMap.cs` | `WorldMap` (static) | Bölge ↔ dünya koordinat dönüşümü ve oyun sınırları | `RegionToWorld`, `WorldToRegion`, `InBounds`, `MinX` -46, `MaxX` 454, `MinZ` -110, `MaxZ` 390 |
| `Survival/WorldState.cs` | `WorldState` (`Instance`) | Loot kaydı; dünya durumunu kaydeder ve yükler. Zombiler kaydedilmez | `Instance`, `Register`, `MarkLoot`, `FindLoot`, `Capture`, `LoadedFromSave` |

### 2.5 Görev, İpucu ve Dünya Etkileşimi

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `QuestFuse.cs` | `QuestFuse` (`Instance`), `QuestState` | Görev durum makinesi: sigorta → güç → istasyon → lure → erzak → yard → serbest dolaşım. Enum sırası kayıt formatıdır | `State`, `ObjectiveText`, `ObjectiveMarker`, `TakeFuse`, `InstallFuse`, `NotifyLoot`, `RestoreState`, `StallSeconds` 20 |
| `HintDirector.cs` | `HintDirector` (`Instance`) | İpucu kartları: öncelik, kota, süre, hatırlatma; öğrenilenler PlayerPrefs | `Instance`, `Showing`, `IsLearned`, `ResetProgress`, `Draw`, `CardSeconds` 6 |
| `FusePanel.cs` | `FusePanel` | Servis avlusundaki pano; sigortayı bir kez kabul eder | `Interact`, `ForceInstalled`, `BlockedLabel` |
| `FuseItem.cs` | `FuseItem` | Depodaki sigorta; alınınca görev `CarryingFuse` olur | `Interact`, `ForceTaken` |
| `DoorInteractable.cs` | `DoorInteractable` | Menteşeli kapı, 90° açılır; kapalıyken NavMesh oyar | `openAngle` 90, `OpenSeconds` 0.6, `Unlock`, `ForceOpen` |
| `AmmoCrate.cs` | `AmmoCrate` | Tek seferlik mühimmat kutusu | `Rounds` 12, `Interact`, `ForceUsed` |
| `Interactor.cs` | `Interactor` | Oyuncu bileşeni: her karede küre ışınıyla kullanılabilir nesneyi bulur; E tuşu `Press` üzerinden | `Range` 2.2, `Reach` 2.5, `CommitReach` 3.4, `Refresh`, `Press`, `Commit` |
| `IInteractable.cs` | `IInteractable`, `IHandInteractable`, `IFocusTitle`, `IBlockedPress`, `IBlockedLabel`, `HandActionKind`, `HandCarrier` | Kullanılabilir nesne sözleşmeleri | `CanInteract`, `Interact`, `HintAnchor`, `Verb`, `HandKind` |
| `NoiseEvents.cs` | `NoiseEvents` (static) | Gürültü olayları; zombiler `Heard` ve `Distraction` olaylarına abone olur | `Emit`, `EmitDistraction`, `RaiseZombieHeard`, `ClearSubscribers` |
| `ClearanceMarker.cs` | `ClearanceMarker` | Builder'ın kritik boşluk işaretçisi; doğrulayıcı fizikle yeniden ölçer | `label`, `acrossX`, `minimum` |
| `NavIgnore.cs` | `NavIgnore` | Hareketli gövdeyi (kapı kanadı) runtime NavMesh'ten hariç tutar | (işaretleyici) |

### 2.6 UI

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `Hud.cs` | `Hud` | IMGUI HUD: can, mermi, hızlı yuva, pusula, görev, toast, nişan, hasar vinyeti. Envanter açıkken `SurvivalUi`'ya devreder | `Fill` (internal); sanal 1080p düzen |
| `PauseMenu.cs` | `PauseMenu` (static) | Duraklatma menüsü kök sayfası; kayıt silen ve çıkan düğmeler onay sorar | `Draw(Sandbox, width, height)`, `Reset`, `OnEscape` |
| `SettingsPanel.cs` | `SettingsPanel` (static) | Sekmeli ayarlar ekranı (Oyun / Ses / Grafik / Kontroller); duraklatma ve ana menüden ortak açılır | `Draw(w, h, bool)`, `Reset` |
| `MainMenu.cs` | `MainMenu` (static) | Ana menü: uygulama başına bir kez, donmuş sahnenin üstünde; açıkken oyun duraklatılmış sayılır | `Active`, `TryBegin`, `Draw`, `OnEscape`, `RequestReturn` |
| `InputLabels.cs` | `InputLabels` (static) | Ekranda gösterilen tuş adları; metinler buradan kurulur | `Move`, `Interact`, `Inventory`, `Reload`, `Key`, `Prompt` |
| `Survival/SurvivalUi.cs` | `SurvivalUi` (internal static) | Envanter/üretim ekranı ve hızlı yuva çubuğu; yalnız çizer, durumu `PlayerSurvival` tutar | `Draw`, `DrawQuickBar`, `Idle`, `QaSelectRecipe`, `QaSelectSlot` |
| `Survival/UiTheme.cs` | `UiTheme` (internal static) | Düz IMGUI stili; ortak çizim yardımcıları | `Hex`, `Fill`, `Label`, `Button`, `ItemIcon`, `Name` |
| `Survival/Notifier.cs` | `Notifier` (static) | Kısa ekran mesajları (toast) | `Show`, `Clear`, `MaxQueued` 3 |
| `Survival/PickupFeed.cs` | `PickupFeed` (static), `Line` | Sağ alttaki "+2 Kumaş" satırları | `Add`, `At`, `Alpha`, `MaxLines` 4, `LineSeconds` 1.8 |

### 2.7 Ses & VFX

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `SfxBank.cs` | `SfxBank` (`Instance`), `Sfx`, `SfxBank.Override`, `SfxSynth` | Ses kaynağı havuzu (3D + 2D). Kayıtlı klip yoksa prosedürel sentez kullanılır | `Instance`, `Play`, `Play2D`, `Has`, `PoolSize` 24 |
| `EffectPool.cs` | `EffectPool` (`Instance`) | Sabit kapasiteli VFX havuzu; çalışma sırasında yeni nesne üretmez | `Instance`, `MuzzleFlash`, `EjectCasing`, `WorldImpact`, `BloodImpact`, `BulletDecal` |

### 2.8 Kayıt

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `Survival/SaveSystem.cs` | `SaveSystem` (static), `SaveData`, `BuildSlotSave` | Tek dosyalı JSON kayıt, atomik yazma (tmp + replace), bozuk dosyayı `.bad` yapar | `Version` 1, `Save`, `TryLoad`, `UseSlot`, `Delete`, `FilePath`, `HasSave` |

### 2.9 Sahne Üretimi (Editor)

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `Editor/SandboxBuilder.cs` | `SandboxBuilder` | CombatSandbox'ı kodla üretir; ortak yardımcılar (kutu, malzeme, oyuncu, ışık, ses) | `Regenerate and Build`, `Build Existing Scene`, `BuildDirectorAndZombies`, `BuildLighting`, `Box`, `Mat` |
| `Editor/IlkIkmalBuilder.cs` | `IlkIkmalBuilder` | İlk İkmal sahnesi: harita, kapılar, sigorta, panel, mühimmat kutusu, zombiler | `BuildAll`, `BuildDirector`, `BuildZombies`, `ZombieSpot` |
| `Editor/OpenWorldBuilder.cs` | `OpenWorldBuilder` (partial) | Açık bölge yerleşim verisi, yollar, arazi yüksekliği | (partial, bkz. alt satırlar) |
| `Editor/OpenWorldBuilder.Terrain.cs` | `OpenWorldBuilder` (partial) | Arazi, katmanlar, splat, ağaç ve çim, görünmez çevre duvarı | — |
| `Editor/OpenWorldBuilder.Roads.cs` | `OpenWorldBuilder` (partial) | Yol şeritleri, köprü, dere, elektrik direği, korkuluk | — |
| `Editor/OpenWorldBuilder.Props.cs` | `OpenWorldBuilder` (partial) | Geçici bina ve kaya yer tutucuları | — |
| `Editor/OpenWorldBuilder.Gameplay.cs` | `OpenWorldBuilder` (partial) | Loot planı, yard tezgâhı/depo/lamba, görev bağlantıları, NavMesh bake (`BakeNavMesh`), açık bölge zombi noktaları | `LootDef`, `BakeNavMesh` |
| `Editor/OpenWorldBuilder.Rear.cs` | `OpenWorldBuilder` (partial) | v0.8 istasyon arkası: dolap, arka çit, arka yaklaşım rotası | — |
| `Editor/ZombieFactory.cs` | `ZombieFactory` | Zombi prefab'ları: model öncelik sırası (zombie.fbx → Pxltiger → Rio → primitive), animator | `CreatePrefabs`, `PxlModels`, `RioPath`, `FbxPath` |
| `Editor/ViewmodelFactory.cs` | `ViewmodelFactory`, `ViewmodelRig` | Birinci şahıs kol + tabanca düzeneği (Infima LPSP) ve Animator controller'ları | `BuildArmsViewmodel`, `ViewmodelRig` |
| `Editor/LookDevSetup.cs` | `LookDevSetup` | Doku import, PBR malzemeler, gökyüzü, URP ayarları, post işleme | `SetupSky` |
| `Editor/BoxMeshFactory.cs` | `BoxMeshFactory` | Dünya ölçekli UV'li kutu mesh'leri | — |
| `Editor/ProceduralMeshes.cs` | `ProceduralMeshes` | Çam, kaya, çatı gibi deterministik mesh asset'leri | — |
| `Editor/ProceduralTextures.cs` | `ProceduralTextures` | Zemin ve çim dokuları (Poly Haven yoksa yedek) | — |
| `Editor/SurvivalAssets.cs` | `SurvivalAssets`, `IconPainter` | Eşya/tarif SO'ları (yoksa oluşturur) ve piktogram ikonları | — |
| `Editor/ArmsDiagnostics.cs` | `ArmsDiagnostics` | Kol hiyerarşisi dökümü (tanılama) | Menü: `Zombosland/Diagnostics/Dump Arms Hierarchies` |

### 2.10 QA & Validasyon

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `QaSelfTest.cs` | `QaSelfTest` | `--qa-selftest`: atış, öldürme, ragdoll, saldırı, reload akışı | `QA_SELFTEST` satırları |
| `QaPlaythrough.cs` | `QaPlaythrough` | `--qa-playthrough`: İlk İkmal betikli oyun, sonuç dosyası | `WatchdogSeconds` 240, `QA_PLAYTHROUGH` |
| `QaSurvival.cs` | `QaSurvival` | `--qa-survival`: envanter, üretim, kayıt, ölüm sonrası yükleme kontrolleri | `WatchdogSeconds` 480, `QA_SURVIVAL` |
| `QaHands.cs` | `QaHands` | `--qa-hands`: el klipleri PNG ve commit kuralları | `QA_HANDS` |
| `QaReload.cs` | `QaReload` | `--qa-reload`: boş reload zamanlaması (slayt, sarsıntı, mermi) | `QA_RELOAD` |
| `QaAxe.cs` | `QaAxe` | `--qa-axe`: balta pozlarını Blender değerleriyle karşılaştırır | `QA_AXE` |
| `QaScreens.cs` (+ `QaPng`) | `QaScreens`, `QaPng` | `--qa-screens`: CombatSandbox ekran görüntüleri; el yazımı PNG yazıcı | `QaPng.Write` |
| `QaScreensIlkIkmal.cs` | `QaScreensIlkIkmal` | `--qa-screens-ilkikmal`: İlk İkmal kamera senaryoları | `QA_SCREENS_ILKIKMAL_DONE` |
| `QaScreensOpenWorld.cs` | `QaScreensOpenWorld` | `--qa-screens-openworld`: açık bölge HUD/arazi/üretim ekranları | `QaScreens_openworld/` |
| `PerfHarness.cs` | `RuntimeBootstrap` (static), `QaAutoQuit`, `PerfHarness` | QA yardımcılarını açılışta kurar; `--perf`, `--perf-openworld`, `--qa-autoquit`; sabit tohumlu senaryo | `Seed` 12345, `zombieCount`, `warmupSeconds`, `measureSeconds` |
| `PlaytestLog.cs` | `PlaytestLog` (static) | İnsan oyun testi ölçümü; bölüm bitince `PlaytestResults/` yazar. Betikli koşular `qa=1` ile işaretlenir | `BeginLevel`, `RecordShot`, `RecordDamage`, `RecordQuest`, `WriteComplete`, `WritePartial` |
| `QaArgs.cs` | `QaArgs` (static) | Komut satırı argümanı okuyucu | `Has`, `GetString`, `GetInt`, `GetFloat`, `TryGetFloat` |
| `Editor/SandboxValidation.cs` | `SandboxValidation` | CombatSandbox entegrasyon kontrolleri | `Require` |
| `Editor/IlkIkmalValidation.cs` | `IlkIkmalValidation` | İlk İkmal kontrolleri; kapı ve yol boşluğu ölçümleri | `Run` (`-executeMethod`) |
| `Editor/WeaponValidation.cs` | `WeaponValidation` | Silah doğrulamaları (içeriği ayrıntılı taranmadı; doğrulanmadı) | (doğrulanmadı) |
| `Editor/ReloadFlourishValidation.cs` | `ReloadFlourishValidation` | İçe aktarılan boş reload kliplerini LPSP kliplerine göre ölçer | 30 fps çerçeve 34–72 dışı eşleşme |

### 2.11 Yardımcılar

| Dosya | Ana tip(ler) | Sorumluluk | Önemli üyeler |
|---|---|---|---|
| `Layers.cs` | `Layers` (static) | Fizik katman indeksleri ve ışın/görüş maskeleri. Adlar `SandboxBuilder` tarafından kaydedilir | `Player` 9, `ZombieMover` 8, `Corpse` 10, `ZombieHitbox` 11, `Environment` 12, `Interactable` 13, `ShotMask`, `SightMask` |
| `BootRouter.cs` | `BootRouter` (static) | Argümana göre açılış sahnesini seçer: argümansız İlk İkmal, QA/perf/sandbox argümanlarıyla CombatSandbox | `SandboxScene` = "CombatSandbox", `IsAbandoned`, `Rerouted` |

Not: `Assets\Scripts` içinde ayrıca bir `Editor` alt klasörü yoktur; editör kodu `Assets\Editor` altındadır (bkz. 2.9 ve 2.10).

---

## 3. Bağlantılar

### 3.1 Singleton'lar (`static Instance`)

Grep ile doğrulanan 7 adet:

| Sınıf | Tanım | Kullanan (örnek) |
|---|---|---|
| `EffectPool` | `static Instance { get; private set; }` | `Sandbox.Weapon.cs`, `ViewmodelPresenter.cs`, `Sandbox.Melee.cs` |
| `SfxBank` | `static Instance { get; private set; }` | Neredeyse tüm ses çağrıları |
| `ZombieDirector` | `static Instance { get; private set; }` | `PerfHarness.cs`, `QaPlaythrough.cs`, `QaSurvival.cs`, `QaScreensIlkIkmal.cs` |
| `QuestFuse` | `static Instance { get; private set; }` | `Hud.cs`, `FuseItem.cs`, `FusePanel.cs`, `PauseMenu.cs` (Esc menüsü: "Kaydet" varlığı) |
| `HintDirector` | `static Instance { get; private set; }` | `Hud.cs`, `PauseMenu.cs`, `QaScreensIlkIkmal.cs` |
| `WorldState` | `static Instance { get; private set; }` | `QaSurvival.cs` (kayıt kontrolleri) |
| `PlayerSurvival` | `public static PlayerSurvival Instance;` (alan) | `QuestFuse.cs`, `BuildSlot.cs`, `QaHands.cs` |

`Sandbox` singleton değildir. Diğer bileşenler ona `GetComponent` / `GetComponentInParent` / alan ataması ile bağlanır.

### 3.2 Doğrudan bağımlılıklar (Grep ile doğrulandı)

- `Sandbox` (oyuncu) ← `Interactor`, `Hud`, `CameraEffects`, `ViewmodelPresenter`, `QaHands`, `QaReload`, `QaAxe`: `GetComponent<Sandbox>()` veya `GetComponentInParent<Sandbox>()`.
- `Sandbox.survival` = `GetComponent<PlayerSurvival>()` (`Sandbox.cs`). `CombatSandbox`'ta bu bileşen yok; kullanım null-güvenli.
- `Hud`, `Sandbox.HitConfirmed` olayına abone olur (olay tanımı `Sandbox.Weapon.cs`).
- `ZombieBrain.player` ve `ZombieDirector.player` `Sandbox` alanlarıdır; builder'lar atar (`SandboxBuilder`, `IlkIkmalBuilder`).
- `HintDirector.player` ve `QuestFuse.player` aynı şekilde `Sandbox` alanlarıdır.
- `QuestFuse` → `PlayerSurvival.Instance` (çanta işlemleri), `SfxBank.Instance`.
- `Sandbox.Weapon.cs` → `NoiseEvents.Emit`, `PlaytestLog.RecordShot`, `EffectPool.Instance`, `SfxBank.Instance`.
- `ZombieBrain` → `NoiseEvents.Heard` ve `NoiseEvents.Distraction` abonelikleri; `DamageRules`.
- `ZombieBrain` → `SfxBank.Instance` (ölüm, saldırı sesi).
- `BuildSlot` → `PlayerSurvival.Instance` (bağlantı; ayrıntı için `BuildSlot.cs`).
- `CameraEffects` ← `PlayerMotor` (headbob, koşma durumu) ve `Sandbox.AdsAmount` (nişan).
- `Hud` ← `Sandbox`, `PlayerSurvival`, `QuestFuse`, `HintDirector`; `PauseMenu.Draw(Sandbox, ...)` çağrılır.

---

## 4. Komut satırı argümanları

Kaynak: `BootRouter.cs`, `PerfHarness.cs`, `Sandbox.cs`, `ViewmodelPresenter.cs`, `Survival/SaveSystem.cs`, `Survival/PlayerSurvival.cs` içindeki `QaArgs.Has(...)` ve `StartsWith("--")` çağrıları.

| Argüman | Nerede | Ne yapar |
|---|---|---|
| (yok) | `BootRouter` | İlk İkmal (build index 0) açılır |
| `--sandbox` | `BootRouter` | CombatSandbox açılır |
| `--perf=N`, `--perf-warmup=`, `--perf-seconds=` | `PerfHarness` | Sabit tohumlu performans senaryosu; PerfResults'a yazar |
| `--perf-openworld=N` | `PerfHarness` | Açık bölge performansı |
| `--qa-selftest` | `PerfHarness` → `QaSelfTest` | Betikli atış/öldürme/saldırı/reload |
| `--qa-playthrough` | `PerfHarness` → `QaPlaythrough` | İlk İkmal betikli oyun |
| `--qa-survival` | `PerfHarness` → `QaSurvival` | Envanter/üretim/kayıt kontrolleri |
| `--qa-autoquit=SEC` | `PerfHarness` → `QaAutoQuit` | SEC saniye sonra temiz çıkış |
| `--qa-screens` | `Sandbox.cs` → `QaScreens` | CombatSandbox ekran görüntüleri |
| `--qa-screens-ilkikmal` | `Sandbox.cs` → `QaScreensIlkIkmal` | İlk İkmal ekran görüntüleri |
| `--qa-screens-openworld` | `Sandbox.cs` → `QaScreensOpenWorld` | Açık bölge ekran görüntüleri (`SaveSystem` kapalı) |
| `--qa-hands` | `Sandbox.cs` → `QaHands` | El klipleri ve commit kuralları |
| `--qa-reload` | `Sandbox.cs` → `QaReload` | Boş reload ölçümü |
| `--qa-axe` | `Sandbox.cs` → `QaAxe` | Balta pozları |
| `--qa-freeze` | `Sandbox.cs` | Zombileri dondurur |
| `--qa-ads` | `Sandbox.cs` | Nişanı sabitler |
| `--qa-wall` | `Sandbox.cs` | Yakın duvar konumunu sabitler |
| `--give-axe` | `Survival/PlayerSurvival.cs` | Balta verir (README'de yok) |
| `--save-slot=<ad>` | `Survival/SaveSystem.cs` | Kayıt slotu seçer (`save_<ad>.json`) |
| `--arms=lpsp` | `ViewmodelPresenter.cs` | Eski LPSP kollarına döner |

---

## 5. Veri ve içerik

### 5.1 ScriptableObject tipleri

| Tip | Dosya | Varlık klasörü | Adet |
|---|---|---|---|
| `ItemDef` | `Survival/ItemDef.cs` | `Assets/Data/Items/*.asset` | 13 |
| `RecipeDef` | `Survival/RecipeDef.cs` | `Assets/Data/Recipes/*.asset` | 6 |
| `ItemDatabase` | `Survival/ItemDatabase.cs` | `Assets/Data/ItemDatabase.asset` | 1 |

Eşyalar: `axe` (meleeDamage 60, süre 1), `bandage` (heal 25, stack 5), `barricade_kit` (stack 2), `bat` (meleeDamage 40, süre 0.7, menzil 1.9), `chest_kit`, `cloth` (10), `lure` (5), `repair_kit` (3), `scrap` (10), `stone` (10), `supply_cache` (1), `tape` (5), `wood` (10).
Tarifler: `bandage` (3 s), `barricade_kit` (8 s), `chest_kit` (8 s), `lure` (2 s), `bat` (4 s), `repair_kit` (6 s). `axe` ve `supply_cache` için tarif dosyası yok.

Editör tarafında: `SurvivalAssets.cs` SO'ları yalnız eksikse oluşturur, böylece elle yapılan düzenlemeler korunur.

### 5.2 Koda `const` olarak gömülü içerik

- Hasar ve sendeleme: `DamageRules` (34, 60, 1.2, 1.5, çarpanlar).
- Zombi: `ZombieBrain` (100 can, 18 m görüş, 10 hasar, 1.1 m/s yürüyüş), `ZombieDirector` (24 ceset, 20 s).
- Silah: `WeaponState` (12 şarjör, 0.25 s, 1.41/1.93 s reload), `Sandbox.Weapon` (100 m menzil).
- Yakın dövüş yedek değerleri: `Sandbox.Melee` (`DefaultMeleeDamage` 40, `DefaultMeleeRange` 1.9, `DefaultMeleeSeconds` 0.7). Süre için kodda doğrulandı: asset değeri 0 veya eksikse yedek devreye girer.
- Animasyon süreleri: `ViewmodelPresenter` (reload, çekme, bandaj).
- Görev: `QuestFuse` (20 s durma, ID'ler), `HintDirector` (6 s, 20 s, 2 hatırlatma).
- Çanta: `PlayerSurvival` (24/24/4 slot, 0.6 s atma aralığı), `Crafter` (99), `BuildSlot` (100 can, 60 onarım, 12 ekstra slot).
- Dünya: `WorldMap` (-46..454, -110..390 m).
- Ayar varsayılanları: `GameSettings`. Ses: `SfxBank` (24 kaynak). VFX: `EffectPool` (kapasiteler).
- Kayıt sürümü: `SaveSystem.Version` = 1.
- Tuş etiketleri: `InputLabels`. Tuşların kendisi `Input.Get*` çağrılarında sabit.

Editör içinde elle düzenlenebilen değerler (`[SerializeField]`/public alan): `Sandbox.startReserve`, `Sandbox.noiseRadius`, `ZombieBrain.wanderRadius`, `ZombieAnimator.clipRunSpeed`, `DoorInteractable.openAngle`, `StorageBox.lidOpenAngle`, `ThrownStone.noiseRadius`, `PerfHarness.zombieCount`.

---

## 6. Sahneler, prefab'lar, dokümanlar

### 6.1 Sahneler

| Yol | Build index | Üreten |
|---|---|---|
| `Assets\Scenes\IlkIkmal.unity` | 0 (açılış) | `Editor/IlkIkmalBuilder.cs` (elle düzenleme yok) |
| `Assets\Scenes\CombatSandbox.unity` | 1 | `Editor/SandboxBuilder.cs` (elle düzenleme yok) |

### 6.2 Editör kodu (`Assets\Editor`, Scripts altında değil)

| Dosya | Kısa rol |
|---|---|
| `ArmsDiagnostics.cs` | Kol hiyerarşisi tanılama menüsü |
| `BoxMeshFactory.cs` | Kutu mesh'leri (dünya ölçekli UV) |
| `IlkIkmalBuilder.cs` | İlk İkmal sahnesi üretici |
| `IlkIkmalValidation.cs` | İlk İkmal doğrulayıcı |
| `LookDevSetup.cs` | Doku, malzeme, gökyüzü, post |
| `OpenWorldBuilder.cs` | Açık bölge yerleşim verisi (ana dosya) |
| `OpenWorldBuilder.Gameplay.cs` | Loot, yard, görev bağlantıları, NavMesh |
| `OpenWorldBuilder.Props.cs` | Geçici binalar ve kayalar |
| `OpenWorldBuilder.Rear.cs` | v0.8 istasyon arkası |
| `OpenWorldBuilder.Roads.cs` | Yollar, köprü, dere |
| `OpenWorldBuilder.Terrain.cs` | Arazi, katmanlar, ağaç, çevre duvarı |
| `ProceduralMeshes.cs` | Çam, kaya, çatı mesh'leri |
| `ProceduralTextures.cs` | Zemin ve çim dokuları |
| `ReloadFlourishValidation.cs` | Boş reload klip doğrulayıcı |
| `SandboxBuilder.cs` | CombatSandbox üretici ve ortak yardımcılar |
| `SandboxValidation.cs` | CombatSandbox doğrulayıcı |
| `SurvivalAssets.cs` | Eşya/tarif SO ve ikon üretici |
| `ViewmodelFactory.cs` | Birinci şahıs kol rig'i |
| `WeaponValidation.cs` | Silah doğrulayıcı (doğrulanmadı: içerik ayrıntılı taranmadı) |
| `ZombieFactory.cs` | Zombi prefab'ları |

### 6.3 Prefab ve üretilen varlık klasörleri

| Yol | İçerik |
|---|---|
| `Assets\Generated\Prefabs\` | `Zombie.prefab`, `Zombie_A.prefab`, `Zombie_B.prefab`, `Zombie_C.prefab` (zombi), `ThrownStone.prefab` (dikkat taşı) |
| `Assets\Generated\Prefabs\Trees\` | `Birch_Medium`, `Birch_Small`, `Pine_0`, `Pine_1`, `Pine_2` |
| `Assets\Generated\Animators\` | `VM_Arms.controller`, `VM_Pistol.controller`, `Zombie.controller` |
| `Assets\Generated\Icons\` | Eşya ikonları (png) |
| `Assets\Art\Viewmodels\` | El/silah viewmodel varlıkları (içeriği taranmadı). `Assets\Art` içinde başka klasör yok |

Zombi prefab'ları `ZombieFactory` tarafından üretilir. Oyuncu bir prefab değildir: `SandboxBuilder.cs` içinde kodla `Player` adlı nesne kurulur (CharacterController, Sandbox, PlayerMotor, Hud; kamera alt nesnesinde CameraEffects). `Interactor` ve `PlayerSurvival` sonradan `IlkIkmalBuilder` ve `OpenWorldBuilder.Gameplay` tarafından eklenir.

### 6.4 Dokümanlar

Klasör: `Docs\`

| Dosya | Ne anlatır |
|---|---|
| `IMPLEMENTATION_REPORT.md` | Sürüm bazlı uygulama raporu ve ölçümler (v0.3 – v0.7, balta ek bölümü dahil) |
| `QA_ACCEPTANCE.md` | CombatSandbox kabul listesi, performans ölçüm protokolü, İlk İkmal doğrulama senaryoları |
| `SPEC_v0.3_combat_sandbox_completion.md` | Combat sandbox tamamlama spesifikasyonu |
| `SPEC_v0.4_hands_reload_zombie.md` | Görünür eller, fiziksel reload, zombi modeli ve tepkisi |
| `SPEC_v0.5a_survivalist_arms_trial.md` | Survivalist kol denemesi (karşılaştırma) |
| `SPEC_v0.6_ilk_ikmal.md` | İlk İkmal bölümü spesifikasyonu |
| `SPEC_v0.7_acik_bolge.md` | Açık bölge, toplama/üretim, HUD, kayıt spesifikasyonu |
| `SPEC_v0.7_axe_viewmodel.md` | Balta, gerçek ellerle (dikey dilim) |
| `SPEC_v0.8_hands_loop.md` | Toplama hissi, el eylemleri, keşif rotası ve zombi okunurluğu |

Kök dizinde ilgili belgeler: `Zombosland_GDD_v1.0.md` (oyun tasarım belgesi; içeriği okunmadı, yalnız ad), `ILKIKMAL_VALIDATION_RESULT` (doğrulama sonucu; okunmadı), `AGENTS.md` (ajan kuralları), `README.md` (sürüm ve kontroller).

### 6.5 Üçüncü parti ve dışarıda bırakılanlar

- `Assets\ThirdParty`: üçüncü parti varlıklar burada.
- Girilmedi: `Library`, `Temp`, `Logs`, `UserSettings`, `.codegraph`, `SourceArt`.

---

## 7. Bilinen tutarsızlıklar ve açık noktalar

1. İlk İkmal mermi sayısı README içinde çelişiyor: üst bölümde "İlk İkmal'de 36 mermi", İlk İkmal bölümünde "48 başlangıç + 12 kutu = 60". Kod tarafında `Sandbox.Weapon.cs` yorumu 36 diyor.
2. README `--qa-screens` çıktısını `01..12` olarak yazar; `QaScreens.cs` sınıf yorumu "10 screenshots" diyor (doğrulanmadı: hangisinin güncel olduğu).
3. README `--qa-hands`, `--qa-reload`, `--qa-axe`, `--give-axe` argümanlarını anmaz; kodda var.
4. `Assets\Audio` klasörü yok. `SfxBank.cs` ve `SandboxBuilder.cs` (satır 463) `Assets/Audio/<ad>.wav` bekler; bu durumda prosedürel yedek devrede olmalıdır (çalışma zamanı doğrulanmadı).
5. `Assets\Art\Characters\Zombie\zombie.fbx` yok (`Assets\Art` yalnız `Viewmodels` içerir). Zombi modeli Pxltiger / Rio / primitive yolundan gelir (doğrulandı: `ZombieFactory` öncelik yorumu ve `Assets\ThirdParty\PxltigerZombie` klasörü).
6. README "v0.7" başlıklıdır; v0.8 kodu (`QuestState` sonundaki iki durum, `OpenWorldBuilder.Rear.cs`, `SPEC_v0.8`) README'de yok.
7. İlk İkmal gürültü yarıçapı README'de 22 m. Sahne değeri `IlkIkmal.unity` içinde kontrol edilmedi (doğrulanmadı).
8. `ZombieDirector.navCenter` ve `navSize` kodda varsayılan değerlerle tanımlı; sahnedeki gerçek değer kontrol edilmedi (doğrulanmadı).
9. Kapsam notu: `Assets\Editor` `Assets\Scripts` altında değil. Editör dosyaları bu haritada ayrı bölümde (bölüm 2.9, 2.10, 6.2) yer alır.
10. Kod yorumları ve metinlerinde ajan veya yapay zekâ için talimat gibi görünen bir içerik bulunmadı (Grep ile tarandı).

---

## 8. Yenileme notu

- Yeni `.cs` dosyası: bölüm 2 altındaki uygun tabloya bir satır, gerekiyorsa bölüm 1'e bir satır.
- Yeni sabit veya public metot: bölüm 1 tablosunda ilgili satırı güncelle.
- Yeni sahne, eşya veya tarif: bölüm 5 ve 6.
- Yeni komut satırı argümanı: bölüm 4.
- Sürüm değişince bölüm 0 üst satırındaki sürüm notunu düzelt.
