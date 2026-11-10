namespace Zombosland
{
    // Pure state: animation, audio and input cannot complete or cancel a reload.
    public sealed class WeaponState
    {
        public const int Capacity = 12;
        // 2.233 s clip * event at 0.633 / 2.733 s clip * event at 0.706 (the LPSP "animation ended" moments).
        public const float TacticalReloadSeconds = 1.41f;
        public const float EmptyReloadSeconds = 1.93f;
        public int Ammo { get; private set; } = Capacity;
        public int Reserve { get; private set; }
        public const float ShotInterval = 0.25f;
        public bool Reloading { get; private set; }
        // True when the current (or last started) reload began with an empty magazine.
        public bool ReloadFromEmpty { get; private set; }
        public float ReloadDuration { get { return Reloading && ReloadFromEmpty ? EmptyReloadSeconds : TacticalReloadSeconds; } }
        float reloadEnd;
        float nextShot;

        public WeaponState(int startReserve) { Reserve = startReserve < 0 ? 0 : startReserve; }

        public void Tick(float now)
        {
            if (!Reloading || now < reloadEnd) return;
            int needed = Capacity - Ammo;
            int transferred = System.Math.Min(needed, Reserve);
            Ammo += transferred;
            Reserve -= transferred;
            Reloading = false;
        }

        public bool BeginReload(float now)
        {
            if (Reloading || Ammo == Capacity || Reserve == 0) return false;
            Reloading = true;
            ReloadFromEmpty = Ammo == 0;
            reloadEnd = now + (ReloadFromEmpty ? EmptyReloadSeconds : TacticalReloadSeconds);
            return true;
        }

        public bool TryFire(float now)
        {
            if (Reloading || Ammo == 0 || now < nextShot) return false;
            Ammo--;
            nextShot = now + ShotInterval;
            return true;
        }

        // Pickups add reserve rounds; the magazine is not touched.
        public void AddReserve(int rounds) { if (rounds > 0) Reserve += rounds; }

        // 0..1 while reloading, 0 otherwise. Presentation reads this; it never changes state.
        public float ReloadProgress(float now)
        {
            if (!Reloading) return 0f;
            float p = 1f - (reloadEnd - now) / ReloadDuration;
            return p < 0f ? 0f : (p > 1f ? 1f : p);
        }

        // Trigger pull on an empty magazine (not reloading). True means "play the click".
        public bool TryDryFire(float now)
        {
            if (Reloading || Ammo != 0 || now < nextShot) return false;
            nextShot = now + 0.25f;
            return true;
        }

        // Loading a save: magazine and reserve are set directly, any reload in progress is dropped.
        public void RestoreForSave(int ammo, int reserve)
        {
            Ammo = ammo < 0 ? 0 : (ammo > Capacity ? Capacity : ammo);
            Reserve = reserve < 0 ? 0 : reserve;
            Reloading = false;
        }

        // QA only (--qa-playthrough): sets the reserve directly.
        internal void QaSetReserve(int reserve) { Reserve = reserve < 0 ? 0 : reserve; }

        // QA only (--qa-screens): sets the magazine without a reload.
        internal void QaSetAmmo(int ammo)
        {
            Ammo = ammo < 0 ? 0 : (ammo > Capacity ? Capacity : ammo);
        }
    }
}
