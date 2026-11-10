using System;
using UnityEditor;
using UnityEngine;
using Zombosland;

public static class WeaponValidation
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("VALIDATION FAILED: " + message);
        Debug.Log("PASS: " + message);
    }

    public static void Run()
    {
        try
        {
            var state = new WeaponState(72);
            Check(state.Ammo == 12 && state.Reserve == 72, "initial ammunition");
            Check(!state.BeginReload(0), "full magazine cannot reload");
            Check(state.TryFire(0), "first shot fires");
            Check(!state.TryFire(.1f), "cadence blocks early shot");
            Check(!state.TryFire(.2f), "0.25 s interval blocks a shot at 0.2 s");
            Check(state.TryFire(.25f), "shot fires after the 0.25 s interval");
            Check(state.BeginReload(.2f), "reload begins");
            Check(!state.TryFire(2), "reload blocks fire until tick");
            state.Tick(1.60f);
            Check(state.Reloading && state.Ammo == 10, "reload remains pending before deadline");
            state.Tick(1.62f);
            Check(!state.Reloading && state.Ammo == 12 && state.Reserve == 70, "reload transfers ammo once");
            state.Tick(5);
            Check(state.Reserve == 70, "reload does not transfer twice");
            for (int i = 0; i < 12; i++) Check(state.TryFire(10 + i), "shot " + i);
            Check(!state.TryFire(30) && state.Ammo == 0, "empty magazine blocks firing");

            var health = new HealthState(100);
            Check(!health.Damage(60) && health.Current == 40 && !health.IsDead, "health remains alive after partial damage");
            Check(health.Damage(40) && health.IsDead, "death transition fires once");
            Check(!health.Damage(40) && health.Current == 0, "repeated damage after death ignored");
            // --- v0.3 additions ---
            var w = new WeaponState(72);
            Check(w.ReloadProgress(0) == 0f, "reload progress is 0 when not reloading");
            Check(!w.TryDryFire(0), "dry fire is refused while the magazine has ammo");
            for (int i = 0; i < 12; i++) w.TryFire(i);
            Check(w.Ammo == 0 && !w.Reloading, "magazine emptied for dry-fire tests");
            Check(w.TryDryFire(20f), "dry fire clicks on an empty magazine");
            Check(!w.TryDryFire(20.1f), "dry fire is limited by cadence");
            Check(w.TryDryFire(20.4f), "dry fire clicks again after cadence");
            Check(w.BeginReload(21f), "reload begins from empty");
            Check(!w.TryDryFire(21.5f), "dry fire is refused while reloading");
            Check(w.ReloadProgress(21f) == 0f, "reload progress starts at 0");
            Check(Mathf.Abs(w.ReloadProgress(21f + WeaponState.EmptyReloadSeconds * 0.5f) - 0.5f) < 0.001f, "reload progress is 0.5 halfway");
            Check(w.ReloadProgress(22.4f) < 1f, "reload progress below 1 before the deadline");
            Check(w.ReloadProgress(30f) == 1f, "reload progress clamps to 1");
            w.Tick(23f);
            Check(!w.Reloading && w.Ammo == 12 && w.ReloadProgress(23f) == 0f, "reload progress returns to 0 after completion");
            Check(w.ReloadDuration == WeaponState.TacticalReloadSeconds, "idle reload duration is the tactical duration");

            // --- v0.4 additions: tactical / empty reload durations ---
            Check(Mathf.Approximately(WeaponState.TacticalReloadSeconds, 1.41f) && Mathf.Approximately(WeaponState.EmptyReloadSeconds, 1.93f), "reload constants are 1.41 s and 1.93 s");
            var t = new WeaponState(72);
            t.TryFire(0f);
            Check(t.BeginReload(5f) && !t.ReloadFromEmpty && Mathf.Approximately(t.ReloadDuration, 1.41f), "partial magazine reload takes 1.41 s and is not from empty");
            t.Tick(5f + t.ReloadDuration - 0.001f);
            Check(t.Reloading && t.Ammo == 11, "no ammo transfer just before the 1.41 s boundary");
            t.Tick(5f + t.ReloadDuration);
            Check(!t.Reloading && t.Ammo == 12 && t.Reserve == 71, "ammo transferred exactly at the 1.41 s boundary");
            var e = new WeaponState(72);
            for (int i = 0; i < 12; i++) e.TryFire(i);
            Check(e.Ammo == 0 && e.BeginReload(20f) && e.ReloadFromEmpty && Mathf.Approximately(e.ReloadDuration, 1.93f), "empty magazine reload takes 1.93 s and reports ReloadFromEmpty");
            e.Tick(20f + e.ReloadDuration - 0.001f);
            Check(e.Reloading && e.Ammo == 0, "no ammo transfer just before the 1.93 s boundary");
            e.Tick(20f + e.ReloadDuration);
            Check(!e.Reloading && e.Ammo == 12 && e.Reserve == 60, "ammo transferred exactly at the 1.93 s boundary");
            e.Tick(40f);
            Check(e.Reserve == 60, "boundary transfer happens once");
            Check(e.ReloadFromEmpty, "ReloadFromEmpty keeps the state of the last reload start");

            // --- v0.6 additions: startReserve 36 / 72, 0.25 s interval, head x3 ---
            var s36 = new WeaponState(36);
            Check(s36.Ammo == 12 && s36.Reserve == 36, "initial ammunition with startReserve 36");
            Check(Mathf.Approximately(WeaponState.ShotInterval, 0.25f), "shot interval constant is 0.25 s");
            Check(s36.TryFire(0f) && !s36.TryFire(0.24f) && s36.TryFire(0.25f) && s36.Ammo == 10, "36 reserve: two shots 0.25 s apart, 0.24 s is blocked");
            Check(s36.BeginReload(1f) && s36.ReloadDuration == WeaponState.TacticalReloadSeconds, "36 reserve: tactical reload begins");
            s36.Tick(1f + WeaponState.TacticalReloadSeconds);
            Check(s36.Ammo == 12 && s36.Reserve == 34, "36 reserve: tactical reload moves 2 rounds (reserve 34)");
            for (int i = 0; i < 12; i++) s36.TryFire(10 + i);
            Check(s36.Ammo == 0 && s36.BeginReload(30f) && s36.ReloadFromEmpty, "36 reserve: empty reload begins");
            s36.Tick(30f + WeaponState.EmptyReloadSeconds);
            Check(s36.Ammo == 12 && s36.Reserve == 22, "36 reserve: empty reload moves 12 rounds (reserve 22)");
            var low = new WeaponState(5);
            for (int i = 0; i < 12; i++) low.TryFire(i);
            low.BeginReload(20f);
            low.Tick(30f);
            Check(low.Ammo == 5 && low.Reserve == 0, "reload transfers only what the reserve holds (5)");
            Check(!low.BeginReload(40f), "reload refused when the reserve is 0 even with a partial magazine");
            var none = new WeaponState(0);
            for (int i = 0; i < 12; i++) none.TryFire(i);
            Check(none.Ammo == 0 && !none.BeginReload(40f), "empty reserve: reload refused");

            Check(Mathf.Approximately(DamageRules.Multiplier(HitZone.Head), 3f) && Mathf.Approximately(DamageRules.Multiplier(HitZone.Body), 1f) && Mathf.Approximately(DamageRules.Multiplier(HitZone.Limb), 0.7f), "damage zone multipliers 3.0 / 1.0 / 0.7");
            var zombieObject = new GameObject("Test zombie");
            var brain = zombieObject.AddComponent<ZombieBrain>();
            var hitInfo = new DamageInfo { Amount = DamageRules.BaseDamage, Zone = HitZone.Head, Direction = Vector3.forward, Point = Vector3.zero };
            Check(brain.ApplyDamage(hitInfo) && brain.IsDead && brain.Health == 0f, "single head hit (102 damage) kills a 100 HP zombie and reports the kill once");
            Check(!brain.ApplyDamage(hitInfo) && brain.IsDead && brain.Health == 0f, "hit on a dead zombie returns false and changes nothing");
            UnityEngine.Object.DestroyImmediate(zombieObject);

            // v0.4: light hits flinch, enough damage in the poise window (or a head hit) staggers.
            var poiseObject = new GameObject("Test zombie poise");
            var poiseBrain = poiseObject.AddComponent<ZombieBrain>();
            var bodyHit = new DamageInfo { Amount = DamageRules.BaseDamage, Zone = HitZone.Body, Direction = Vector3.forward, Point = Vector3.up };
            Check(!poiseBrain.ApplyDamage(bodyHit) && poiseBrain.State != ZombieState.Stagger && poiseBrain.LastFlinchTime > -50f, "first body hit only flinches (no stagger)");
            Check(!poiseBrain.ApplyDamage(bodyHit) && poiseBrain.State == ZombieState.Stagger, "second body hit inside the poise window (68 >= 60) staggers");
            Check(Mathf.Approximately(DamageRules.PoiseThreshold, 60f) && Mathf.Approximately(DamageRules.PoiseWindow, 1.2f) && Mathf.Approximately(DamageRules.HeavyStaggerCooldown, 1.5f), "poise constants 60 / 1.2 s / 1.5 s");
            Check(Mathf.Approximately(DamageRules.HeavyStaggerSeconds(HitZone.Head), 0.5f) && Mathf.Approximately(DamageRules.HeavyStaggerSeconds(HitZone.Body), 0.45f) && Mathf.Approximately(DamageRules.HeavyStaggerSeconds(HitZone.Limb), 0.35f), "heavy stagger durations 0.5 / 0.45 / 0.35");
            UnityEngine.Object.DestroyImmediate(poiseObject);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Thin camera to muzzle wall";
            wall.transform.position = new Vector3(.06f, -.06f, .2f);
            wall.transform.localScale = new Vector3(.08f, .08f, .02f);
            Physics.SyncTransforms();
            RaycastHit hit;
            Check(Sandbox.ResolveShot(Vector3.zero, Vector3.forward, Vector3.right, Vector3.up, out hit) && hit.collider.gameObject == wall, "camera to muzzle catches thin off-axis wall");
            UnityEngine.Object.DestroyImmediate(wall);
            Debug.Log("WEAPON_VALIDATION_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }
}

