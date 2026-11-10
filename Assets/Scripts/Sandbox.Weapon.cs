using System;
using UnityEngine;

namespace Zombosland
{
    public partial class Sandbox
    {
        const float Range = 100f;
        // Named layers ZombieMover (8) and Player (9) are excluded from shots.
        public const int ShotMask = Layers.ShotMask;

        // Serialized per scene: CombatSandbox 72, IlkIkmal 36.
        [SerializeField] int startReserve = 72;
        // Shot noise radius in metres (CombatSandbox 30).
        [SerializeField] float noiseRadius = 30f;

        WeaponState weapon; // created lazily so nothing reads it before startReserve is deserialized
        ViewmodelPresenter presenter;
        bool adsHeld;
        float adsAmount;

        public WeaponState Weapon { get { return weapon ?? (weapon = new WeaponState(startReserve)); } }
        public bool AdsHeld { get { return adsHeld; } }
        public float AdsAmount { get { return adsAmount; } }
        public float HitMarkerTime { get; private set; } = -100f;
        public event Action<HitZone, bool> HitConfirmed;

        void InitializeWeapon()
        {
            presenter = GetComponentInChildren<ViewmodelPresenter>();
        }

        void UpdateWeaponInput()
        {
            bool ui = UiOpen;
            bool input = !PerfMode && !ui;
            bool melee = MeleeEquipped;
            bool holdsPistol = Carrier == HandCarrier.Pistol;
            // No aiming while a menu is open, the hands are busy, or the pistol is not in hand.
            adsHeld = !ui && !melee && holdsPistol && !HandsBusy && (qaAds || (input && Input.GetMouseButton(1)));
            adsAmount = Mathf.MoveTowards(adsAmount, adsHeld ? 1f : 0f, 8f * Time.deltaTime);
            if (!input || HandsBusy) return; // hand actions own the arms; firing, reload and swings are dropped, never queued
            if (!melee && !holdsPistol) return; // empty hands

            if (melee)
            {
                // Reload and firing are off; WeaponState keeps ticking (Update) so a running reload still finishes.
                if (Input.GetMouseButtonDown(0)) TryMeleeSwing();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R)) Weapon.BeginReload(Time.time);
            // Semi-automatic: one shot per click; a click before the interval has elapsed is dropped, never queued.
            if (Input.GetMouseButtonDown(0))
            {
                if (Weapon.TryFire(Time.time)) Fire();
                else if (Weapon.TryDryFire(Time.time) && SfxBank.Instance != null) SfxBank.Instance.Play2D(Sfx.DryFire, 0.7f);
            }
        }

        // QA hooks for --qa-screens: forced ADS and a real shot through the normal Fire() path.
        internal void QaSetAds(bool value) { qaAds = value; }
        internal bool QaFire()
        {
            if (!Weapon.TryFire(Time.time)) return false;
            Fire();
            return true;
        }

        // QA: the gate the left mouse button goes through.
        internal void QaFireIfAllowed()
        {
            if (!HandsBusy && Carrier == HandCarrier.Pistol) QaFire();
        }

        void Fire()
        {
            Physics.SyncTransforms(); // hitboxes follow animated zombies; make sure the physics scene is current
            RaycastHit hit;
            bool hitSomething = ResolveShot(AimPivot.position, AimPivot.forward, AimPivot.right, AimPivot.up, out hit);

            motor.Recoil = Mathf.Min(4, motor.Recoil + 1.2f); // gameplay recoil, applied after the ray was resolved
            if (cameraEffects != null) cameraEffects.AddShake(0.12f);
            if (presenter != null) presenter.OnShot();
            Vector3 audioPos = presenter != null ? presenter.MuzzlePosition : AimPivot.position;
            if (SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.Shot, audioPos, 1f, UnityEngine.Random.Range(0.96f, 1.04f), false);
            NoiseEvents.Emit(AimPivot.position, noiseRadius);
            PlaytestLog.RecordShot();
            if (hitSomething) Impact(hit);
        }

        // Shared by live firing and validation, including the camera-to-muzzle sweep.
        public static bool ResolveShot(Vector3 camera, Vector3 forward, Vector3 right, Vector3 up, out RaycastHit hit)
        {
            Ray ray = new Ray(camera, forward);
            RaycastHit aim;
            Vector3 target = Physics.Raycast(ray, out aim, Range, ShotMask, QueryTriggerInteraction.Ignore) ? aim.point : ray.GetPoint(Range);
            Vector3 muzzle = camera + forward * 0.4f + right * 0.12f - up * 0.12f;
            Vector3 toMuzzle = muzzle - camera;
            if (Physics.Raycast(camera, toMuzzle.normalized, out hit, toMuzzle.magnitude, ShotMask, QueryTriggerInteraction.Ignore)) return true;
            Vector3 direction = target - muzzle;
            return Physics.Raycast(muzzle, direction.normalized, out hit, direction.magnitude + 0.02f, ShotMask, QueryTriggerInteraction.Ignore);
        }

        void Impact(RaycastHit hit)
        {
            EffectPool fx = EffectPool.Instance;
            SfxBank sfx = SfxBank.Instance;
            ZombiePart part = hit.collider.GetComponent<ZombiePart>();
            if (part == null)
            {
                if (fx != null) { fx.WorldImpact(hit.point, hit.normal); fx.BulletDecal(hit.point, hit.normal); }
                if (sfx != null) sfx.Play(Sfx.HitConcrete, hit.point, 0.8f, UnityEngine.Random.Range(0.92f, 1.08f), true);
                return;
            }

            // Zombie hit (corpses included): blood only, never a decal on a body.
            if (fx != null) fx.BloodImpact(hit.point, hit.normal, part.zone == HitZone.Head);
            if (sfx != null) sfx.Play(Sfx.HitFlesh, hit.point, 0.9f, UnityEngine.Random.Range(0.92f, 1.08f), true);
            if (part.owner == null || part.owner.IsDead) return;
            var info = new DamageInfo
            {
                Amount = DamageRules.BaseDamage,
                Point = hit.point,
                Direction = AimPivot.forward,
                Zone = part.zone,
                Source = this
            };
            PlaytestLog.RecordHit(part.zone);
            bool killed = part.Hit(info);
            bool head = part.zone == HitZone.Head;
            if (sfx != null)
            {
                if (head) PlayHeadCrack(sfx); // replaces the tick on head hits
                else sfx.Play(Sfx.HitTick, Vector3.zero, killed ? 0.4f : 0.25f, killed ? 0.85f : 1f, false); // hit confirmation, 2D
            }
            if (head && killed) StartHitStop(HitStopHeadKillSeconds);
            HitMarkerTime = Time.time;
            if (HitConfirmed != null) HitConfirmed(part.zone, killed);
        }
    }
}
