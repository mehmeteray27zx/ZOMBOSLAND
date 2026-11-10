# SPEC v0.8 — Toplama hissi, el eylemleri, ilk sefer döngüsü

Unity 6000.6.5f1, URP, eski Input Manager, IMGUI. Kod kuralları `SPEC_v0.7_acik_bolge.md` ile aynı: `namespace Zombosland`, kısa İngilizce yorum, Türkçe oyuncu metni, yeni paket yok, `.meta`ya dokunma, dosya silme yok, Unity'yi ÇALIŞTIRMA (ana ajan derler). Sahneler koddan üretilir. Reload akışına ve süresine dokunma.

## Ana ajanın yazdığı parçalar (bunları YAZMA, API'lerini kullan)
`Assets/Scripts/IInteractable.cs` içine eklenecek:
```csharp
public enum HandActionKind { None, Pickup, OpenContainer, Bandage, Draw }
public enum HandCarrier { Pistol, Hands, Bat, Axe }
// A use that the hands perform: the world change happens once, at the commit moment of the clip.
public interface IHandInteractable : IInteractable { HandActionKind HandKind { get; } bool RequiresLineOfSight { get; } }
// Name line of the world label ("Kumaş ×2"). Null = only the verb line.
public interface IFocusTitle { string Title { get; } }
// Called when E is pressed on something that cannot be used right now.
public interface IBlockedPress { void OnBlockedPress(Sandbox p); }
```
`Assets/Scripts/Sandbox.Hands.cs` (partial Sandbox), `ViewmodelPresenter`, `ViewmodelFactory`, `Sandbox.Melee.cs`:
```csharp
public static bool InstantHands;                // QA: hand actions apply at once, without clips. Default false.
public bool HandsBusy { get; }                  // any hand action is running
public HandActionKind CurrentHandAction { get; }
public HandCarrier Carrier { get; }             // what the hands hold now
public bool WeaponReady { get; }                // pistol in hand and nothing blocks it
// null when a hand action may start now; otherwise a short Turkish reason ("Doldurma bitmeden olmaz").
public string HandActionBlockedReason();
internal bool TryBeginItemAction(IHandInteractable target, Interactor source); // false = refused (nothing changed)
internal bool TryBeginBandage(ItemDef d);       // UseQuick path; heal happens at the commit moment via PlayerSurvival.TryUseBandage
internal void CancelHandAction(string reason);  // safe at any time; before the commit nothing has changed in the world
internal void ToggleHolster();                  // H key: pistol <-> empty hands
public void NotifyCrafting(bool active);        // craft loop clip while Crafter.Busy
```
Süreler (clip): Pickup 0,40 s, commit 0,20 s (el kapanır). OpenContainer 0,80 s, commit 0,467 s. Bandage 1,70 s, commit 1,20 s. Ateş, nişan, reload, hızlı yuva, Tab sırasında: eylem bitene kadar ateş/ADS/reload/hızlı yuva girdisi düşer (kuyruğa alınmaz). Reload sürerken ya da reload klibinin kuyruğu bitmeden eylem başlamaz.

Commit, `Interactor.Commit(IHandInteractable t)` ile yapılır (aşağıda). Eylem commit'ten önce kesilirse dünya ve çanta değişmez.

## İş paketi A — Etkileşim ve toplama hissi
Dosyalar: `Interactor.cs`, `Survival/LootPickup.cs`, `AmmoCrate.cs`, `FuseItem.cs`, `Survival/StorageBox.cs`, `Survival/BuildSlot.cs` (yalnız `Sandığı aç` kolu), `DoorInteractable.cs`, `Hud.cs`, `InputLabels.cs`, `Survival/Notifier.cs`, yeni `Survival/PickupFeed.cs`, `SfxBank.cs`, `Survival/PlayerSurvival.cs`.

1. **Interactor.** `Refresh`te hedef `IHandInteractable` ise ek kontroller: `RequiresLineOfSight` true iken göz noktası (`pivot.position`) ile `HintAnchor.position` arasında `Physics.Linecast` (Layers.Environment, `QueryTriggerInteraction.Ignore`); ilk engel hedefin kendi kökünün altında değilse hedef yok sayılır. Hedefe uzaklık (`pivot` → `HintAnchor`) `Interactor.Reach = 2.5f` üstündeyse yok sayılır. `Title` özelliği: hedef `IFocusTitle` ise onun `Title`'ı, değilse null. `Press()`: hedef kullanılamıyorsa ve `IBlockedPress` ise `OnBlockedPress(player)` çağır, false dön. Kullanılabiliyorsa: `used is IHandInteractable h && h.HandKind != None && !Sandbox.InstantHands` ise `return player.TryBeginItemAction(h, this);` (reddedilirse `HandActionBlockedReason()` metnini 1,2 s `Notifier` ile göster). Aksi halde mevcut ani akış. Yeni: `internal bool Commit(IHandInteractable t)`: t null değil, `CanInteract` true, oyuncu `HintAnchor`a 3,4 m'den yakın ve (gerekiyorsa) görüş açık → `t.Interact(player)`, `Used` olayı, `Refresh`, true. Değilse false. `QaPress` davranışı aynı kalır (`InstantHands` true iken ani).
2. **LootPickup** `IHandInteractable, IFocusTitle, IBlockedPress`: `HandKind=Pickup`, `RequiresLineOfSight=true`, `Title = "<ad> ×<kalan>"`. `BlockedBriefly=false`. `Interact` tek ve atomik kalsın: `Bag.Add` sonucu kadar `remaining` düşer, aynı çağrıda `Apply` + `MarkLoot` + bildirim + ses. Kısmi: kalan yerde kalır, feed "+1 Kumaş" ve Notifier "Çanta doldu: 1 Kumaş yerde kaldı". `OnBlockedPress`: bag doluysa `Notifier.Show("Çantada yer yok — <ad> yerde kaldı", 2f)` + `Sfx.Deny` 2D. Ses: `Sfx.PickupSoft` (cloth, tape, bandage, supply_cache) veya `Sfx.PickupHard` (diğer), 2D, 0,75.
3. **FuseItem**, **AmmoCrate** `IHandInteractable, IFocusTitle`: Pickup, görüş gerekli; başlık "Sigorta" / "Mühimmat kutusu ×12". Ses davranışı aynı.
4. **StorageBox** ve **BuildSlot** (yalnız kurulu sandık kolu): `HandKind=OpenContainer`, `RequiresLineOfSight=false`, `IFocusTitle` "Depo" / "Sandık". BuildSlot kurulmamışken ve barikatta `HandKind=None`. StorageBox'a `public Transform lid; public float lidOpenAngle = 70f;` ve kapak animasyonu: `Interact` çağrıldığında 0,25 s'de açılır, `ps.OpenStorage != this` olunca 0,3 s'de kapanır; `Sfx.LidOpen` 3B açılışta, `Sfx.LidClose` kapanışta. Kapak eksiği (null) hata vermez. Builder'da `Crate_Lid` -x kenarına menteşe `Lid_Hinge` altına alınır, `storage.lid` atanır (`OpenWorldBuilder.Gameplay.cs BuildYard`).
5. **DoorInteractable** `IHandInteractable, IFocusTitle`: `public bool cabinet;` true ise `HandKind=OpenContainer`, `Verb="Dolabı aç"`, `Title` "Dolap", `RequiresLineOfSight=false`, ses `Sfx.LidOpen`; false ise `HandKind=None`, `Title=null`, davranış aynen.
6. **PickupFeed** (static, `Survival/PickupFeed.cs`): `Add(ItemDef item, int n)`; en fazla 4 satır, her satır 1,8 s; aynı eşya son 2 s içindeyse satırı büyütür. `PlayerSurvival.NotifyPickup` artık `Notifier` yerine `PickupFeed.Add` çağırır (quest çağrısı kalır). HUD sağ altta, ammo panelinin üstünde çizer: ikon (varsa) + "+2 Kumaş", 0,12 s giriş, 0,3 s çıkış. `HintDirector` kartıyla çakışmasın diye kart gösteriliyorsa satırları kartın üstüne al (`HintDirector.Instance` üzerinden kart yüksekliği yoksa sabit 110 px yukarı).
7. **Hud.DrawWorldLabel** iki satır: `Interactor.Title` varsa üstte 15 punto `UiTheme.Secondary`, altta `[E] verb`. Kutu genişliği uzun satıra göre. Başlık yoksa bugünkü görünüm.
8. **Hud.DrawAmmo**: `player.Carrier == Hands` iken "Boş el" başlığı, altta `[H] silahı çek`. Silah varken ek satır yok. Sopa ve balta panelini koru.
9. **InputLabels**: `Holster = "H"`, `HolsterKey = KeyCode.H`. F1 yardım şeridine ekle.
10. **SfxBank**: enum SONUNA `PickupSoft, PickupHard, Deny, LidOpen, LidClose, BandageTie, Equip, Holster`. `SfxSynth` ile prosedürel üret (mevcut `Rustle`, `Click` kalıpları), süreler 0,08–0,25 s. Mevcut sıralamayı bozma.
11. **PlayerSurvival**: `Update`te `InputLabels.HolsterKey` (UI kapalıyken) → `sandbox.ToggleHolster()`. `UseQuick`: `sandbox.HandsBusy` iken false. Consumable: `Sandbox.InstantHands ? TryUseBandage(d) : sandbox.TryBeginBandage(d)`; `TryUseBandage` ani ve atomik kalır, "Can dolu" kontrolü başta da yapılsın (animasyon boşa oynamasın). `TryThrow` `HandsBusy` iken false. Crafter başlayınca/bitince `sandbox.NotifyCrafting(Crafter.Busy)` (Update'te değişim algıla). `RestoreFromSave` başında `sandbox.CancelHandAction("Kayıt yüklendi")`.
12. **QA**: Yeni `Assets/Scripts/QaHands.cs` yazma, onu ana ajan yazar. `QaPlaythrough`, `QaSurvival`, `QaScreens`, `QaScreensIlkIkmal`, `QaScreensOpenWorld`, `QaSelfTest`, `QaAxe`, `QaReload` Start başında `Sandbox.InstantHands = true`. `QaSurvival`'e ek testler: LootPickup kısmi alımda kalan, tam alımda tek sefer (mevcut `loot_once` kapsar, ek olarak iki `Interact` arka arkaya toplam sabit), `Interactor` görüş testi (duvar arkası etiket yok), dolu çantada `OnBlockedPress` eşya değiştirmiyor.

## İş paketi B — Keşif rotası ve zombi okunurluğu
Dosyalar: `QuestFuse.cs`, `Survival/SaveSystem.cs` (gerekirse), `Editor/OpenWorldBuilder*.cs`, `Editor/SurvivalAssets.cs`, `HintDirector.cs` (gerekirse), `ZombieBrain.cs`, `ZombieAnimator.cs`, `Editor/ZombieFactory.cs`, `QaSurvival.cs`, `QaPlaythrough.cs`, `QaScreensOpenWorld.cs`, `Editor/IlkIkmalValidation.cs`.

Döngü: keşif → toplama → üretim → çatışma veya kaçınma → güvenli noktaya dönüş. Yerleşim önce, çevre ayrıntısı sonra.

1. **Eşya:** `supply_cache` "Erzak çantası", Resource, maxStack 1, ikon üretimi diğerleriyle aynı. Veritabanına eklenir.
2. **QuestState** SONUNA `CraftLure, RetrieveCache`. Akış: `ExploreStation` → (`Bag.Available(lure)>0` ise `RetrieveCache`; `stone>=1 && cloth>=1` ise `CraftLure`) → `CraftLure` → (`lure>=1`) `RetrieveCache` → (`NotifyLoot` item `supply_cache`) `ReturnToYard` → avluya girince `FreeRoam`. Kontroller `Update`'te (`PlayerSurvival.Instance.Bag`), yalnız ileri gider. Metinler: ExploreStation "İstasyona git: taş ve kumaş topla" ve yanına canlı sayaç YOK; CraftLure "Dikkat taşı üret — [Tab]"; RetrieveCache "Dükkândan erzak çantasını al"; ReturnToYard "Erzak çantasını avluya getir"; marker: ilk ikisi `stationMarker`, RetrieveCache `cacheMarker` (yeni alan), ReturnToYard `yardMarker`. Avluya varınca: çantada `supply_cache` varsa `TryRemove`, ödül +2 Bandaj (`Bag.Add`, sığmayan depoya) ve `player.Weapon.AddReserve(12)`, `Notifier` "İlk sefer tamamlandı: +2 Bandaj, +12 mermi". Yoksa eski metin. `bandageIntroShown` kumaş ilk alındığında bir kez gösterilir (istasyon şartı kalkar). İstasyona 45 m kala oturumda bir kez `Notifier` "Ön avlu dolu: taşı uzağa at ya da arkadan dolaş" 5 s (`RetrieveCache` ve öncesi durumlarda).
3. **Yerleşim (station frame, `station.ToR(x,z)`, +x yoldan içeri, -z avlu yönü):**
   - Dördüncü zombi `(27,10)` → `(9,-3.5)`. Arka tamamen boş kalsın.
   - Arka çit (`Environment`, 2,0 m, box): `x=32` boyunca z -15,3..-0,9 ve 0,9..15; güney kenarı `z=-15,3` boyunca x 7..32. Kuzey kenarında çit yok. Kapı boşluğu z -0,9..0,9.
   - Arka yaklaşım: yoldan `(4,-22)` ayrılıp `(20,-22)`, `(38,-20)`, `(38,0)`, kapı `(32,0)`, `(28,0)`, arka kapı `(26,2)`. Terrain pad gerekirse: `pads`e bu şeridi kapsayan bir pad ekle (level = stationLevel-0.1, falloff 6) ve ağaç/çim temizliği `pins`/mevcut mekanizma ile. NavMesh'e dahil olmalı.
   - Dolap: `(25.0, 3.45)` kuzey duvarına yaslı, 1,1 × 0,45 × 1,9 m, arka+yan+üst+alt `Environment` kutular, ön kapak `Cabinet_Door` hinge sol kenar, `leafCollider` Environment. `DoorInteractable` adı `Cache_Cabinet`, `cabinet=true`, `openAngle=-110`. Trigger Interactable katmanında ön yüzde. Kaydı mevcut `openDoors` taşır.
   - Çanta: `loot_supply_cache`, `supply_cache`, 1 adet, dolabın içinde raf yüksekliği (~1,05 m). Görsel: çanta gövdesi + iki kolon, `IlkIkmalBuilder.Visual`. `AddLoot` dışı özel kurulum gerekiyorsa yap; id sabit.
   - Yeni loot: `loot_van_cloth_1` cloth 2 `(-4.4,-6.5)`, `loot_rear_stone_1` stone 2 `(6,-19)`.
4. **Çevre ayrıntısı (yerleşim bittikten sonra):** arka şeride ayak izi hissi veren koyu toprak şerit (mevcut Road/terrain boyama ile), çitin önünde iki yığın palet/varil, arka kapı üstünde söner lamba yok, kapı önünde tek bir ok tabela ("ARKA GİRİŞ", düz kutu + `IlkIkmalBuilder.Visual`). Ölçü kısa, performansı bozma.
5. **Zombi okunurluğu:** `AttackWindupSeconds` `public const float` ve 0,75 (ZombieFactory saldırı state hızı bu sabitten). ClipMode'da windup'ın ilk %70'inde `ZombieAnimator` üst gövdeye ek geri çekilme: spine/chest toplam 12° arkaya, baş 8° yukarı (0→1 eğri). Hafif isabet: chest push 12→20°, body 6→10°, head 28→32°, limb kolu 20→34°; `flinchSlowUntil` 0,25→0,30 s. Saldırı sesi windup başında kalır, vuruş anında `Sfx.HitFlesh` yok. QA bu sabitlere bağlıysa uyarla, test sayısını düşürme.
6. **QA:** `QaSurvival` QuestFlow/StationAndYard yeni akışa uyarlanır, test silme. Ek: `route_rear` (`Walk` ile yolu CharacterController'la yürü; her örnek noktada istasyon zombilerinin `Physics.Linecast` görüşü kapalı oranını logla ve yolun çit dışı kısmında %100 olmalı), `route_front` NavMesh, `cabinet_los` dolap kapalıyken çanta etiketi yok, açıkken var, `cache_once`, `reward_once`.
