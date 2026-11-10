using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zombosland
{
    // Player root: owns gameplay state (health, weapon, pause). Presentation components only read it.
    public partial class Sandbox : MonoBehaviour, IDamageable
    {
        PlayerMotor motor;
        CameraEffects cameraEffects;
        Vector3 spawn;
        PlayerSurvival survival; // null in CombatSandbox: every use below is null-safe
        Vector3 safePosition;
        float nextSafeSample;
        bool paused, qaFreeze, qaAds, dead;
        float diedAtUnscaled;
        readonly HealthState health = new HealthState(100);

        const float SafeSampleSeconds = 1f, FallLimitY = -30f;

        public bool IsPaused { get { return paused; } }
        // Inventory / crafting screen is open: movement, firing and interaction are off, the cursor is free.
        public bool UiOpen { get { return survival != null && survival.UiOpen; } }
        public PlayerSurvival Survival { get { return survival; } }
        public Vector3 SafePosition { get { return safePosition; } }
        public bool ZombiesFrozen { get { return paused || qaFreeze; } }
        public bool MenuOpen { get { return paused; } }
        public bool IsDead { get { return dead; } }
        public bool PerfMode { get; private set; }
        public float Health { get { return health.Current; } }
        public float LastHurtTime { get; private set; } = -100f;
        public float SecondsSinceDeath { get { return dead ? Time.unscaledTime - diedAtUnscaled : 0f; } }
        public PlayerMotor Motor { get { return motor; } }
        public Transform AimPivot { get; private set; }

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return; // first scene was replaced by QA routing
            GameSettings.Load();
            NoiseEvents.ClearSubscribers();
        }

        void Start()
        {
            qaFreeze = QaArgs.Has("--qa-freeze");
            qaAds = QaArgs.Has("--qa-ads");
            motor = GetComponent<PlayerMotor>();
            AimPivot = transform.Find("AimPivot");
            cameraEffects = GetComponentInChildren<CameraEffects>();
            if (QaArgs.Has("--qa-wall")) motor.Teleport(new Vector3(4, .05f, 2.1f));
            spawn = transform.position;
            safePosition = spawn;
            survival = GetComponent<PlayerSurvival>();
            InitializeWeapon();
            DynamicGI.UpdateEnvironment();
            SetCursor(false);
            if (QaArgs.Has("--qa-screens") && GetComponent<QaScreens>() == null) gameObject.AddComponent<QaScreens>();
            if (QaArgs.Has("--qa-screens-ilkikmal") && GetComponent<QaScreensIlkIkmal>() == null) gameObject.AddComponent<QaScreensIlkIkmal>();
            if (QaArgs.Has("--qa-screens-openworld") && GetComponent<QaScreensOpenWorld>() == null) gameObject.AddComponent<QaScreensOpenWorld>();
            if (QaArgs.Has("--qa-axe") && GetComponent<QaAxe>() == null) gameObject.AddComponent<QaAxe>();
            if (QaArgs.Has("--qa-reload") && GetComponent<QaReload>() == null) gameObject.AddComponent<QaReload>();
            if (QaArgs.Has("--qa-hands") && GetComponent<QaHands>() == null) gameObject.AddComponent<QaHands>();
        }

        // QA hooks for --qa-screens (never used by normal play).
        internal void QaSetFreeze(bool value) { qaFreeze = value; }
        internal void QaSetPaused(bool value) { SetPaused(value); }

        void Update()
        {
            if (dead)
            {
                if (SecondsSinceDeath >= 2f) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && !PerfMode) HandleEscape();
            if (paused) return;
            Weapon.Tick(Time.time);
            motor.Tick(Time.deltaTime);
            UpdateHands();
            UpdateWeaponInput();
            UpdateMelee();
            CheckFall();
        }

        // Esc closes the inventory first (the pause menu does not open); otherwise it toggles the pause menu.
        void HandleEscape()
        {
            if (UiOpen) survival.CloseUi();
            else SetPaused(!paused);
        }

        // QA hook: the same path as the Esc key.
        internal void QaEscape() { if (!PerfMode) HandleEscape(); }

        // Safe-position bookkeeping and the fall net. CombatSandbox (no PlayerSurvival) keeps the old rule: y < -10 -> spawn.
        void CheckFall()
        {
            if (survival == null)
            {
                if (transform.position.y < -10) motor.Teleport(spawn);
                return;
            }
            Vector3 p = transform.position;
            if (p.y < FallLimitY || !WorldMap.InBounds(p))
            {
                motor.Teleport(safePosition);
                return;
            }
            if (motor.Grounded && Time.time >= nextSafeSample)
            {
                nextSafeSample = Time.time + SafeSampleSeconds;
                safePosition = p;
            }
        }

        // Loading a save moved the player: that spot is the new safe point.
        internal void ResetSafePoint(Vector3 position)
        {
            safePosition = position;
            nextSafeSample = Time.time + SafeSampleSeconds;
        }

        // Called by the perf harness: no input, no damage, no firing.
        public void EnterPerfMode()
        {
            PerfMode = true;
            motor.InputEnabled = false;
        }

        // Called by PlayerSurvival when the inventory / crafting screen opens or closes.
        internal void ApplyUiState(bool open)
        {
            if (!PerfMode && motor != null) motor.InputEnabled = !open;
            if (open) adsHeld = false;
            SetCursor(open || paused || dead);
            OnUiStateForHands(open);
        }

        void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0 : 1;
            SetCursor(value || UiOpen);
            if (!value) GameSettings.Save();
        }

        // Cursor control for the UI (IMGUI inventory etc.).
        internal void SetCursorForUi(bool visible) { SetCursor(visible || paused || dead); }

        void SetCursor(bool visible)
        {
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }

        public void Heal(float amount) { health.Heal(amount); }

        // Loading a save sets health directly.
        public void RestoreHealth(float value) { health.Restore(value); }

        public float MaxHealth { get { return health.Max; } }

        // New attempt: the playtest counters start over too.
        public void RestartLevel()
        {
            PlaytestLog.ResetSession();
            Restart();
        }

        public void Restart()
        {
            GameSettings.Save();
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void AddCameraShake(float amount) { if (cameraEffects != null) cameraEffects.AddShake(amount); }

        public bool ApplyDamage(in DamageInfo info)
        {
            if (dead || PerfMode || health.IsDead) return false;
            bool killed = health.Damage(info.Amount);
            PlaytestLog.RecordDamage(info.Amount);
            LastHurtTime = Time.time;
            if (cameraEffects != null) cameraEffects.AddShake(killed ? 1f : 0.55f);
            if (SfxBank.Instance != null) SfxBank.Instance.Play2D(Sfx.PlayerHurt, 0.9f);
            if (killed)
            {
                dead = true;
                CancelHandAction("Öldün");
                PlaytestLog.RecordDeath();
                diedAtUnscaled = Time.unscaledTime;
                SetCursor(true);
            }
            return killed;
        }
    }

    // Single clean exit path (also used by --qa-autoquit and the perf harness).
    public static class GameQuit
    {
        public static void Request()
        {
            Time.timeScale = 1;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            GameSettings.Save();
            PlaytestLog.WritePartial(); // no-op unless a level was started and not finished
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }
    }
}
