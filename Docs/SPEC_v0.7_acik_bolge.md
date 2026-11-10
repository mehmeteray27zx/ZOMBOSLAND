# SPEC v0.7 — İlk açık bölge, toplama/üretim, HUD

Kaynak istek: `Documents\ChatGPT\Zombosland\design\acik-dunya-2026-11-08\` (Claude_Uygulama_Promptu.md, Zombosland_Acik_Dunya_Raporu.md). Aşağıdaki sözleşme uygulama içindir; değerler başlangıç tasarım değeridir.

## Doğrulanmış mevcut durum (2026-11-08)
Unity 6000.6.5f1, URP 17.6.0, eski Input Manager (`Input.*`), UI = IMGUI (`Hud.OnGUI`; uGUI/TMP yok, EKLENMEYECEK). NavMesh runtime `NavMeshBuilder` ile (AI Navigation paketi yok, EKLENMEYECEK). Kayıt sistemi yok (yalnız PlayerPrefs ayarları). Envanter yok. Stamina yok (HUD'da DAYANIKLILIK GÖSTERİLMEZ). Sahneler koddan üretilir (`IlkIkmalBuilder`). `manifest.json`'a yalnız yerleşik `com.unity.modules.terrain` ve `terrainphysics` eklendi. Baseline: `--qa-playthrough` 52/0.

## Kod kuralları
`namespace Zombosland`, kısa İngilizce yorumlar, oyuncuya görünen metin Türkçe. Sahne bileşenleri Awake'te `if (BootRouter.IsAbandoned(gameObject)) return;`. Editör kodu `Assets/Editor`, runtime `Assets/Scripts` (yeni dosyalar `Assets/Scripts/Survival/`). asmdef yok. C# 9 uyumlu. `Input.*` kullan. Yeni paket yok. `.meta` dosyalarına dokunma, var olan dosyayı silme. Unity'yi ÇALIŞTIRMA (ana ajan derler). Her `FindObjectsByType` çağrısı projedeki tek argümanlı kullanımla aynı olsun.

## Dünya koordinatı
Bölge koordinatı (R: güneybatı başlangıç, 500×500 m) → dünya: `world = (Rx - 46, y, Rz - 110)`. `WorldMap.RegionToWorld(float rx, float rz)` (runtime static, `Assets/Scripts/Survival/WorldMap.cs`). Avlu (80,80) = mevcut F servis avlusu (dünya 34,-30). Eski harita dünya x -46..51, z -42..46 alanındadır.

## Görev akışı (QuestFuse)
`enum QuestState { SearchFuse, CarryingFuse, FuseInstalled, Complete, ExploreStation, ReturnToYard, FreeRoam }` (sıra korunur, sonuna eklenir).
- `InstallFuse()` başarılı: `FuseInstalled` → ışık olayı (bir kez) → `outerGate.ForceOpen(false)`, `serviceDoor.Unlock()` → `Complete` (CompletedSeconds/Card* doldur, `PlaytestLog.WriteComplete()`) → `ExploreStation`. `Notifier.Show("Elektrik geri geldi", 4f)`. Autosave `"fuse"`. Güvenli odaya dönme adımı ve sonuç kartı KALKAR (`CardVisible` hep false, API kalır; `Hud` kart çizimi ayrı iş paketinde silinir).
- `ExploreStation`: "Eski istasyonu araştır", pusula işareti `stationMarker`. `stationBounds` içinde kumaş alınca (`NotifyLoot`) → `ReturnToYard`: "Bandaj üret, avluya dön", işaret `yardMarker`; `Notifier` ile bir kez "Yeni tarif: Bandaj — [Tab] çantayı açar" (tuş etiketi InputLabels'tan).
- `yardBounds` içine girince → `FreeRoam`: "Bölgeyi keşfet", işaret yok, Toast "İlk sefer tamamlandı", autosave `"yard"`. Sonra her avluya dönüşte (dışarıdan içeri geçişte) autosave.
- Ek özellikler: `string ObjectiveHeading` (ilk 4 durum "İLK İKMAL", sonrası "DIŞ BÖLGE"), `bool HasObjectiveMarker`, `Vector3 ObjectiveMarker`.
- Yeni alanlar: `public Light[] yardLamps; public Light benchLamp; public Renderer[] lampMeshes; public Color lampGlow; public DoorInteractable outerGate, serviceDoor; public Bounds stationBounds, yardBounds; public Vector3 stationMarker, yardMarker;`. Işık olayında lambalar `enabled=true`, `lampMeshes` Glow.
- `RestoreState(QuestState s)` : olay ÇALIŞTIRMADAN durum ve (s >= FuseInstalled ise) ışıkları uygular, `LightEventCount = 1`, kapı durumları WorldState'ten gelir.

## Veri (ScriptableObject, editörden düzenlenir)
```csharp
public enum ItemKind { Resource, Consumable, Throwable, Melee, Kit }
[CreateAssetMenu(menuName="Zombosland/Item")] public class ItemDef : ScriptableObject {
  public string id, displayName; [TextArea] public string description; public ItemKind kind; public int maxStack = 10; public Texture2D icon;
  public float healAmount, meleeDamage, meleeRange, meleeSeconds, noiseRadius; }
[System.Serializable] public struct ItemAmount { public ItemDef item; public int count; }
[CreateAssetMenu(menuName="Zombosland/Recipe")] public class RecipeDef : ScriptableObject {
  public string id, displayName; [TextArea] public string description; public ItemDef output; public int outputCount = 1;
  public ItemAmount[] inputs; public float seconds; public bool needsBench; public bool available = true; }
[CreateAssetMenu(menuName="Zombosland/Database")] public class ItemDatabase : ScriptableObject {
  public ItemDef[] items; public RecipeDef[] recipes; public ItemDef FindItem(string id); public RecipeDef FindRecipe(string id); }
```
Kimlikler / yığın: cloth "Kumaş" 10, scrap "Hurda" 10, wood "Odun" 10, tape "Bant" 5, stone "Taş" 10, bandage "Bandaj" 5 (Consumable, healAmount 25), lure "Dikkat taşı" 5 (Throwable, noiseRadius 16), bat "Ahşap sopa" 1 (Melee, meleeDamage 40, meleeRange 1.9, meleeSeconds 0.7), repair_kit "Tamir kiti" 3, barricade_kit "Barikat kiti" 2, chest_kit "Sandık kiti" 1 (Kit).
Tarifler: bandage 2 cloth→1, 3 s; lure 1 stone+1 cloth→1, 2 s; bat 3 wood+1 tape→1, 4 s (hepsi elde, available=true). repair_kit 3 scrap+1 tape, 6 s; barricade_kit 5 wood+2 scrap, 8 s; chest_kit 6 wood+2 scrap, 8 s (needsBench=true, available=false — ikinci aşama).
Varlık yolu: `Assets/Data/Items/*.asset`, `Assets/Data/Recipes/*.asset`, `Assets/Data/ItemDatabase.asset`. Builder yalnız EKSİKSE oluşturur (editör düzenlemesi korunur). İkonlar `Assets/Generated/Icons/<id>.png`.

## Çekirdek (saf C#, MonoBehaviour değil; Unity sahnesi olmadan çalışır)
```csharp
public struct ItemStack { public ItemDef item; public int count; public bool IsEmpty; }
public sealed class Inventory {
  public Inventory(int capacity); public int Capacity {get;} public int UsedSlots {get;}
  public ItemStack this[int slot] {get;} public event Action Changed;
  public int Count(ItemDef d); public int Reserved(ItemDef d); public int Available(ItemDef d); // Available = Count - Reserved
  public int FreeSpaceFor(ItemDef d);   // toplam eklenebilir adet
  public int Add(ItemDef d, int n);     // eklenen adedi döner (kısmi olabilir), önce mevcut yığınlar
  public bool TryRemove(ItemDef d, int n); // yalnız Available üzerinden, ya hep ya hiç
  public bool Reserve(ItemDef d, int n); public void Release(ItemDef d, int n); public void ReleaseAll();
  public bool CommitReserved(ItemAmount[] inputs, ItemDef output, int outputCount); // atomik: kopya üzerinde dener; çıktı girdiler düştükten sonra sığmıyorsa HİÇBİR şeyi değiştirmeden false
  public int MoveSlotTo(int slot, Inventory other); // yalnız Available kadar taşır; taşınan adedi döner
  public void SwapSlots(int a, int b); public void SetCapacity(int c); // SetCapacity küçültmez
  public void Load(ItemStack[] slots); public ItemStack[] Snapshot(); public void Clear(); }
public enum CraftBlock { None, MissingInput, NeedsBench, BagFull, Busy, Unavailable }
public struct CraftCheck { public CraftBlock block; public string message; public bool Ok; }
public sealed class Crafter {
  public Crafter(Inventory bag); public RecipeDef Active; public bool Busy; public int Remaining, Total; public float UnitProgress(float now);
  public string LastMessage;
  public event Action<RecipeDef> UnitCompleted; public event Action<string> Cancelled;
  public CraftCheck Check(RecipeDef r, int qty, bool benchInRange);
  public bool Start(RecipeDef r, int qty, float now, bool benchInRange); // Busy iken false; qty×girdi ayırır
  public void Tick(float now, bool benchInRange); // gerekli tezgâhtan uzaksa Cancel("Tezgâhtan uzaklaştın"); süre dolunca 1 birim CommitReserved; sığmazsa Cancel("Çantada yer yok")
  public void Cancel(string reason); } // kalan ayrımları serbest bırakır, çıktı vermez, Busy=false, iki kez çağrılsa da güvenli
```
`Check` qty birim için kopya envanterde sıralı simülasyon yapar (dolu çantada girdinin boşalttığı yuva kullanılabilir). Metinler: eksik "Kumaş: 1 / 2 — 1 eksik" (birden çok eksikte ilk eksik satır + gerisi UI'da), `NeedsBench` "Tezgâh gerekli", `BagFull` "Çantada yer yok", `Busy` "Üretim sürüyor". Negatif miktar/çift çıktı imkânsız olmalı.

## PlayerSurvival (oyuncu bileşeni, `Assets/Scripts/Survival/PlayerSurvival.cs`)
```csharp
public enum UiTab { Inventory, Crafting }
public class PlayerSurvival : MonoBehaviour {
  public static PlayerSurvival Instance; public ItemDatabase database; public GameObject meleeView; public ThrownStone stonePrefab;
  public const int BagSlots = 24, StorageSlots = 24, QuickSlots = 4;
  public Inventory Bag, Storage; public Crafter Crafter; public ItemDef[] Quick; // Quick[0] daima null = tabanca (sabit ekipman, çantada yer kaplamaz); 1..3 ItemDef referansı (kopya değil)
  public bool UiOpen; public UiTab Tab; public StorageBox OpenStorage; public bool BenchInRange; public int ActiveQuick;
  public void OpenUi(UiTab tab, StorageBox storage=null); public void CloseUi(); // CloseUi üretimi Cancel("Menü kapandı") ile iptal eder
  public void AssignQuick(int slot, ItemDef d); public bool UseQuick(int slot); public void NotifyPickup(ItemDef d, int n);
  public void RestoreFromSave(...); }
```
- Tuşlar `InputLabels` sabitlerinden: Tab envanter (`InventoryKey`, UiOpen değilken açılır, açıkken kapanır), 1–4 hızlı yuva. `InputLabels`e `KeyCode InventoryKey = KeyCode.Tab`, `string Inventory`, `KeyCode QuickKey(int i)`, `string Quick(int i)` eklenir; `Interact/Reload` etiketleri mevcut.
- Hızlı yuva: 0 = tabanca seç. Melee → kuşan (Quick highlight). Consumable → `TryUseBandage` (Available>0, can<100 ise can +healAmount, max 100, 1 adet düş; tam canda tüketmez, Notifier "Can dolu"). Throwable → aim yönünde at (1 düş), `ThrownStone`. Üretimi ayrılmış adet kullanılamaz. Sopa çantadan çıkarsa tabancaya dön.
- Üretim çıktısı boş hızlı yuvaya otomatik atanır (1..3).
- `Crafter.Tick(Time.time, BenchInRange)` Update'te. `BenchInRange`: sahnedeki `Workbench`lerden oyuncuya ≤3.5 m. Ölüm: `CloseUi` + iptal. `OnDestroy`: `Crafter.Cancel`.
- Girdi kapısı: UiOpen iken `motor.InputEnabled=false` (PerfMode'u bozmadan), ateş/ADS/reload/E/yürüme kapalı, fare görünür ve kilitsiz; yerçekimi ve dünya sürer. Kapanınca imleç kilit + kontrol geri. `Sandbox.Update`: Esc → UiOpen ise `CloseUi` (Pause açılmaz) değilse mevcut pause. Pause açıkken Tab/1-4 çalışmaz.
- Melee (`Sandbox.Melee.cs` partial): `MeleeEquipped`, LMB; süre `meleeSeconds`, isabet penceresi %35'te bir kez: `Physics.SphereCast(AimPivot.position, 0.18, forward, meleeRange, ShotMask, Ignore)` → `ZombiePart` ise mevcut `part.Hit(DamageInfo{Amount=meleeDamage,...})`, kan efekti, `HitConfirmed`; `NoiseEvents.Emit(pos, 5f)`. Menzil dışında hasar yok. `ViewmodelPresenter.SetPistolVisible(bool)` tabanca/kol renderer'larını açıp kapatır; `MeleeViewmodel` (`Play()`, kod eğrisiyle savurma, GameSettings.WeaponSway ile ölçeklenmez) `meleeView` üzerinde. Reload/ADS melee'de yok. Melee kuşanılırken reload iptal edilmez (WeaponState tıklamaya devam).
- `Notifier` (static): `Show(string, float seconds)`, `Current`, HUD okur. Çok kısa kuyruk (en fazla 3).

## Dikkat taşı / AI
`ThrownStone : MonoBehaviour` (Rigidbody + SphereCollider + `OnCollisionEnter`): ilk çarpmada veya 6 s'de bir kez `NoiseEvents.EmitDistraction(point, radius)`, sonra 4 s'de yok olur. Oyuncu ile çarpışmaz. `NoiseEvents.Distraction` olayı `ClearSubscribers`'a da eklenir.
`ZombieState` sonuna `Investigate`. `ZombieBrain.OnDistraction(pos, radius)`: ölü/aktif-değil/görüşte kovalayan/Attack/Stagger ise yok say. Mesafe > radius yok say; `NoiseBlocked` varsa etkili yarıçap yarı. `NavMesh.SamplePosition(pos,1.5)` ile hedef, durum `Investigate`, `MoveTo(hedef, WalkSpeed*1.5, true)`. canSee → Alert. Varınca 3 s bekler (`StopMoving`), sonra `home = position`, Wander. `RaiseZombieHeard` çağrılmaz. Zaman aşımı 20 s.

## Dünya etkileşimleri
- `LootPickup : MonoBehaviour, IInteractable, IBlockedLabel` — `public string lootId, verb; public ItemDef item; public int amount=1; public GameObject visual; public Collider trigger; public Transform hintAnchor;` Etkileşim `Bag.Add`; tamamı alındıysa görsel/trigger kapanır, `WorldState.MarkLoot(id,0)`; kısmi ise kalan yazılır; sığmazsa `BlockedLabel` "Çantada yer yok". `NotifyPickup` çağırır.
- `Workbench : IInteractable` Verb "Tezgâhı kullan" → `OpenUi(Crafting)`. `StorageBox : IInteractable` Verb "Depoyu aç" → `OpenUi(Inventory, this)`; depo içeriği `PlayerSurvival.Storage`.
- `DoorInteractable` ekle: `public bool locked; public string lockedLabel="Elektrik gerekli"; public void Unlock(); public void ForceOpen(bool instant);` locked iken CanInteract false, BlockedLabel lockedLabel. `ForceOpen(false)` normal animasyonla açar (locked=false). `ForceOpen(true)` animasyonsuz.
- `FuseItem.ForceTaken()`, `FusePanel.ForceInstalled()`, `AmmoCrate.ForceUsed()` (olaysız, görsel doğru). `AmmoCrate` değişmez davranış, kimlik = gameObject.name.
- `WeaponState.RestoreForSave(int ammo, int reserve)`, `HealthState.Heal(float)` / `Restore(float)`, `Sandbox.Heal(float)`, `Sandbox.RestoreHealth(float)`.
- Düşme güvenliği: `Sandbox` her 1 s yere basarken son güvenli konumu tutar; y < -30 veya `WorldMap` dışına çıkarsa oraya ışınlar (eski spawn yerine; CombatSandbox davranışı `-10` → spawn aynı kalsın).

## Kayıt (`SaveSystem`, `WorldState`)
`SaveData` (JsonUtility, `version=1`): konum/yaw, can, ammo/reserve, QuestState int, açık kapı adları, fuseTaken, panelInstalled, usedAmmoCrates, bag/storage (id+count dizileri; boş yuva id=""), quick id dizisi, loot id→kalan listesi, bandageIntro. Yol `persistentDataPath/save_v1.json`; `--save-slot=<ad>` ile `save_<ad>.json`. Atomik yazım (tmp + replace). Bozuk/yanlış sürüm: logla, `.bad` olarak sakla, kayıtsız başla. `--qa-playthrough`, `--qa-selftest`, `--qa-screens*`, `--perf*` varken `--save-slot` yoksa kayıt devre dışı.
`SaveSystem.Save(reason)`: önce `Crafter.Cancel("Kayıt alındı")`, `Bag.ReleaseAll`, sonra yazar; `SAVE ok reason=...` loglar. `WorldState` sahne bileşeni (`[DefaultExecutionOrder(200)]`, IlkIkmal): Start'ta kayıt varsa uygular, `SAVE loaded`. Ölüm: sahne yeniden yüklenir → son kayıt uygulanır (kayıt yoksa baştan). PauseMenu: "Kaydet", "Son kaydı yükle" (yalnız kayıt varsa; sahne yeniden yükleme ile), "Yeni oyun" (eski "Bölümü yeniden başlat", kaydı siler). IlkIkmal dışı sahnede bu düğmeler yok.
Zombiler kaydedilmez. Alınmış loot yeniden doğmaz.

## QA
`--qa-survival` (IlkIkmal, `QaSurvival.cs`, `RuntimeBootstrap`e eklenir, `--save-slot` zorunlu değil, kendi `qa_survival` slotunu kullanır ve başta siler): `QA_SURVIVAL <id> PASS|FAIL <detay>` satırları, sonda `QA_SURVIVAL_DONE pass= fail=` ve çıkış. Zorunlu kontroller: yığın/kapasite; Reserve sonrası Available; eksik girdi mesajı; normal üretim; dolu çantada girdinin yuvasıyla üretim; çift Start; iptal/iade; süre dolumu + Cancel yarışı (çift çıktı yok); menü kapanışı; ölüm; tezgâhtan uzaklaşma; kayıtta iptal; bandaj tam canda tüketilmez, max 100; sopa 1.5 m vurur 3 m vurmaz; taş zombiyi Investigate yapar ve noktaya yaklaştırır; görev tek seferlik (ikinci InstallFuse olay çoğaltmaz); kaydet→yeniden yükle: envanter, loot çoğalmaz, kapı/ışık/görev; UI açıkken `InputEnabled=false`, kapanınca true, Esc pause açmaz; NavMesh yolları (avlu→istasyon, istasyon→köprü, köprü→su kulesi, avlu→kulübe, kulübe→sığ geçit→köprü, kapalı kapı engeli). CharacterController ile yol boyunca gerçek yürüme (`controller.Move` adımları, takılma yok) istasyon ve kulübe rotalarında.
`--qa-playthrough` yeni akışa uyarlanır (adım 7 ve QuestState beklentileri); test adedini düşürme.
