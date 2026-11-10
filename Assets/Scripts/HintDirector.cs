using System.Collections.Generic;
using UnityEngine;

namespace Zombosland
{
    // Scene component: situational hint cards (spec 6). At most one card is up at any time.
    // The objective line and the [E] world label are not cards and do not count against that quota.
    // Priority: critical (no ammo) -> a valid action (reload) -> first-use hints (move, aim, noise).
    // Conditions are re-evaluated every frame, so a card that is no longer true is never played from a queue.
    public class HintDirector : MonoBehaviour
    {
        public const string Move = "move", Aim = "aim", Reload = "reload", NoAmmo = "noammo", Noise = "noise";
        public static readonly string[] AllIds = { Move, Aim, Reload, NoAmmo, Noise };
        // Highest priority first.
        static readonly string[] Priority = { NoAmmo, Reload, Move, Aim, Noise };

        public static HintDirector Instance { get; private set; }

        public Sandbox player;
        public QuestFuse quest;

        const float CardSeconds = 6f, NoiseCardSeconds = 5f, ReminderGap = 20f;
        const int MaxReminders = 2;
        const float IdleSeconds = 3f, WalkMeters = 2f;
        const float AimMin = 10f, AimMax = 18f, AimConeDegrees = 30f, AimCancelDistance = 4f;
        const float NoiseWindow = 2f;
        const float KeepClear = 120f; // no card within this many pixels of the crosshair centre

        // Learned flags live in PlayerPrefs ("hint_<id>"); this is only a read cache so the frame loop never hits the registry.
        static readonly Dictionary<string, bool> learned = new Dictionary<string, bool>();
        // Session-long counters (static, so a reload after death keeps them).
        static readonly Dictionary<string, int> shownCount = new Dictionary<string, int>();
        static readonly Dictionary<string, float> lastShown = new Dictionary<string, float>();

        // Current card (null id = none).
        string currentId;
        float shownAt, duration;

        ZombieBrain[] zombies = new ZombieBrain[0];
        float zombieRefreshAt, aimCheckAt;
        bool aimEligible, aimCancel;
        float aimEligibleAt = -100f;
        float walked, idleSince, noiseAt = -100f;
        Vector3 lastPosition;
        bool havePosition, subscribed;

        // Drawing / QA.
        GUIStyle textStyle, keyStyle;
        int drawnThisFrame;
        public Rect LastCardRect { get; private set; }
        public float LastCenterDistance { get; private set; } = -1f; // virtual 1080p pixels, centre of the screen to the nearest card edge
        public int QaFrames { get; private set; }
        public int QaViolations { get; private set; }  // frames in which more than one card was up / drawn
        public int QaMaxCards { get; private set; }
        public bool QaDrawn { get; private set; }      // OnGUI has drawn at least one card

        public string CurrentId { get { return currentId; } }
        public string CurrentText { get { return currentId == null ? null : TextOf(currentId); } }
        public bool Showing(string id) { return currentId == id; }
        public float Walked { get { return walked; } }

        // ---------- persistent state ----------

        static string Key(string id) { return "hint_" + id; }

        public static bool IsLearned(string id)
        {
            bool value;
            if (!learned.TryGetValue(id, out value))
            {
                value = PlayerPrefs.GetInt(Key(id), 0) == 1;
                learned[id] = value;
            }
            return value;
        }

        static void Learn(string id)
        {
            if (id == NoAmmo || IsLearned(id)) return; // "no ammo" is critical state, never learned
            learned[id] = true;
            PlayerPrefs.SetInt(Key(id), 1);
            PlayerPrefs.Save();
        }

        // Pause menu: forget everything the player has learned.
        public static void ResetProgress()
        {
            foreach (string id in AllIds) PlayerPrefs.DeleteKey(Key(id));
            PlayerPrefs.Save();
            learned.Clear();
            shownCount.Clear();
            lastShown.Clear();
            if (Instance != null) Instance.CloseCurrent(false);
        }

        // ---------- QA isolation (the scripted runs must not leave the user's flags changed) ----------

        internal struct QaPrefs { public int[] values; public bool tutorial; }

        // Saves the user's hint flags and tutorial toggle, then starts from a clean slate (nothing learned, tutorial on).
        internal static QaPrefs QaIsolate()
        {
            var saved = new QaPrefs { values = new int[AllIds.Length], tutorial = GameSettings.Tutorial };
            for (int i = 0; i < AllIds.Length; i++) saved.values[i] = PlayerPrefs.HasKey(Key(AllIds[i])) ? PlayerPrefs.GetInt(Key(AllIds[i]), 0) : -1;
            ResetProgress();
            GameSettings.Tutorial = true;
            return saved;
        }

        internal static void QaRestore(QaPrefs saved)
        {
            for (int i = 0; i < AllIds.Length; i++)
            {
                if (saved.values[i] < 0) PlayerPrefs.DeleteKey(Key(AllIds[i]));
                else PlayerPrefs.SetInt(Key(AllIds[i]), saved.values[i]);
            }
            PlayerPrefs.Save();
            learned.Clear();
            shownCount.Clear();
            lastShown.Clear();
            GameSettings.Tutorial = saved.tutorial;
        }

        // ---------- lifecycle ----------

        void Awake()
        {
            if (BootRouter.IsAbandoned(gameObject)) return;
            Instance = this;
        }

        void Start()
        {
            if (Instance != this) return;
            // Sandbox.Awake clears the noise subscribers, so subscribing happens here.
            NoiseEvents.ZombieHeard += OnZombieHeard;
            subscribed = true;
            idleSince = Time.time;
            RefreshZombies();
        }

        void OnDestroy()
        {
            if (subscribed) NoiseEvents.ZombieHeard -= OnZombieHeard;
            if (Instance == this) Instance = null;
        }

        void OnZombieHeard(ZombieBrain z) { noiseAt = Time.time; }

        void RefreshZombies()
        {
            zombies = FindObjectsByType<ZombieBrain>();
            zombieRefreshAt = Time.time + 2f;
        }

        // ---------- per-frame logic ----------

        void Update()
        {
            if (Instance != this) return;

            // QA: what the previous frame's OnGUI pass drew, and what the logic believes is up now.
            QaFrames++;
            int cards = currentId != null ? 1 : 0;
            if (cards > QaMaxCards) QaMaxCards = cards;
            if (drawnThisFrame > QaMaxCards) QaMaxCards = drawnThisFrame;
            if (cards > 1 || drawnThisFrame > 1) QaViolations++;
            drawnThisFrame = 0;

            if (player == null || player.PerfMode) { CloseCurrent(false); return; }
            float now = Time.time;
            TrackMovement(now);

            // Learning happens whether or not cards are visible: the player really did the thing.
            if (!player.IsDead)
            {
                WeaponState w = player.Weapon;
                if (walked >= WalkMeters) Done(Move);
                if (player.AdsHeld) Done(Aim);
                if (w.Reloading) Done(Reload);
            }

            if (!GameSettings.Tutorial || player.IsDead) { CloseCurrent(false); return; }

            if (now >= zombieRefreshAt) RefreshZombies();
            if (now >= aimCheckAt) { aimCheckAt = now + 0.2f; UpdateAim(); }

            if (currentId != null && (now - shownAt >= duration || !Keep(currentId))) CloseCurrent(false);

            for (int i = 0; i < Priority.Length; i++)
            {
                string id = Priority[i];
                if (currentId != null && PriorityIndex(currentId) <= i) break; // the card that is up outranks everything below it
                if (Eligible(id, now)) { Show(id, now); break; }
            }
        }

        static int PriorityIndex(string id) { return System.Array.IndexOf(Priority, id); }

        // A successful action: it is learned and closes its card at once.
        void Done(string id)
        {
            Learn(id);
            if (currentId == id) CloseCurrent(true);
        }

        void TrackMovement(float now)
        {
            Vector3 p = player.transform.position;
            if (havePosition)
            {
                Vector3 d = p - lastPosition;
                d.y = 0f;
                float m = d.magnitude;
                if (m > 0.02f) idleSince = now;
                if (m < 2f) walked += m; // larger jumps are teleports, not walking
            }
            lastPosition = p;
            havePosition = true;
            if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f) idleSince = now;
        }

        // aim: an unaware zombie 10-18 m away, in front of the camera, and the player has never aimed down the sights.
        void UpdateAim()
        {
            aimEligible = false;
            aimCancel = false;
            Transform pivot = player != null ? player.AimPivot : null;
            if (pivot == null || IsLearned(Aim)) return;
            Vector3 eye = pivot.position, forward = pivot.forward;
            for (int i = 0; i < zombies.Length; i++)
            {
                ZombieBrain z = zombies[i];
                if (z == null || z.IsDead) continue;
                Vector3 to = z.transform.position - player.transform.position;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance < AimCancelDistance) aimCancel = true;
                if (z.CanSeePlayer || distance < AimMin || distance > AimMax) continue;
                Vector3 head = z.transform.position + Vector3.up * 1.5f;
                if (Vector3.Angle(forward, head - eye) > AimConeDegrees) continue;
                if (Physics.Linecast(eye, head, Layers.SightMask, QueryTriggerInteraction.Ignore)) continue; // it must actually be visible
                aimEligible = true;
                aimEligibleAt = Time.time;
            }
        }

        bool InSafeRoom()
        {
            return quest != null && quest.safeRoomTrigger != null && quest.safeRoomTrigger.bounds.Contains(player.transform.position + Vector3.up * 0.9f);
        }

        // Show conditions, evaluated at the moment a card would be shown.
        bool ShowCondition(string id, float now)
        {
            WeaponState w = player.Weapon;
            switch (id)
            {
                case NoAmmo: return w.Ammo == 0 && w.Reserve == 0 && !w.Reloading;
                case Reload: return w.Ammo == 0 && w.Reserve > 0 && !w.Reloading;
                case Move: return InSafeRoom() && now - idleSince >= IdleSeconds;
                case Aim: return aimEligible && !aimCancel;
                case Noise: return now - noiseAt <= NoiseWindow;
            }
            return false;
        }

        // While a card is up it only closes on its own terms (success, time, or the state it describes ending).
        bool Keep(string id)
        {
            WeaponState w = player.Weapon;
            switch (id)
            {
                case NoAmmo: return w.Ammo == 0 && w.Reserve == 0;
                case Reload: return w.Ammo == 0 && w.Reserve > 0 && !w.Reloading;
                case Aim: return !aimCancel && Time.time - aimEligibleAt <= 1.5f; // closes once the zombie has been out of view for a moment
            }
            return true;
        }

        bool Eligible(string id, float now)
        {
            if (id != NoAmmo && IsLearned(id)) return false;
            int shown;
            shownCount.TryGetValue(id, out shown);
            if (shown > 0)
            {
                // Reminders: only while the failure goes on, at least 20 s after the last time, at most two.
                if (id == Noise || shown - 1 >= MaxReminders) return false;
                float last;
                if (!lastShown.TryGetValue(id, out last) || now - last < ReminderGap) return false;
            }
            return ShowCondition(id, now);
        }

        void Show(string id, float now)
        {
            if (currentId != null) CloseCurrent(false);
            currentId = id;
            shownAt = now;
            duration = id == Noise ? NoiseCardSeconds : CardSeconds;
            int shown;
            shownCount.TryGetValue(id, out shown);
            shownCount[id] = shown + 1;
            lastShown[id] = now;
            PlaytestLog.RecordHintShown(id);
            if (id == Noise) { Learn(Noise); PlaytestLog.RecordHintDone(id); } // informative: learned once it has been seen
        }

        void CloseCurrent(bool completed)
        {
            if (currentId == null) return;
            if (completed) PlaytestLog.RecordHintDone(currentId);
            currentId = null;
        }

        // ---------- text ----------

        static string TextOf(string id)
        {
            switch (id)
            {
                case Move: return InputLabels.Prompt(InputLabels.MoveKeys, "Hareket et");
                case Aim: return InputLabels.Prompt(InputLabels.Aim, "Nişan al");
                case Reload: return InputLabels.Prompt(InputLabels.Reload, "Yeniden doldur");
                case NoAmmo: return "Mühimmat yok — uzaklaş";
                case Noise: return "Silah sesi yakındakileri çekebilir";
            }
            return id;
        }

        // ---------- drawing (called from Hud.OnGUI with the 1080p scale matrix already set) ----------

        void EnsureStyles()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            textStyle.normal.textColor = Color.white;
            keyStyle.normal.textColor = new Color(0.08f, 0.06f, 0.02f);
        }

        // ammoTop: top edge of the ammo panel; cards sit above it (the reload card is attached to it).
        public void Draw(float scale, float w, float h, float ammoLeft, float ammoTop, float ammoWidth)
        {
            if (currentId == null || Event.current.type != EventType.Repaint) return;
            if (player != null && player.MenuOpen) return;
            EnsureStyles();
            string full = TextOf(currentId);
            string key = null, body = full;
            if (full.StartsWith("[") && full.IndexOf(']') > 0)
            {
                int end = full.IndexOf(']');
                key = full.Substring(0, end + 1);
                body = full.Substring(end + 2);
            }
            const float height = 48f, pad = 12f;
            float keyW = key != null ? keyStyle.CalcSize(new GUIContent(key)).x + 20f : 0f;
            float textW = textStyle.CalcSize(new GUIContent(body)).x;
            float width = pad + (key != null ? keyW + 12f : 0f) + textW + 18f;
            bool attached = currentId == Reload;
            if (attached) width = Mathf.Max(width, ammoWidth);
            Rect card = attached
                ? new Rect(ammoLeft, ammoTop - height, ammoWidth, height)                      // sits right on the ammo panel
                : new Rect(ammoLeft + ammoWidth - width, ammoTop - height - 12f, width, height); // right-aligned, just above it
            if (attached && width > ammoWidth) card = new Rect(ammoLeft + ammoWidth - width, ammoTop - height, width, height);

            // Nothing within 120 px (screen pixels) of the crosshair centre.
            float limit = KeepClear / Mathf.Min(1f, scale);
            float dx = Mathf.Max(card.xMin - w / 2f, 0f, w / 2f - card.xMax), dy = Mathf.Max(card.yMin - h / 2f, 0f, h / 2f - card.yMax);
            float centerDistance = Mathf.Sqrt(dx * dx + dy * dy);
            LastCenterDistance = centerDistance;
            if (centerDistance < limit) return;

            float age = Time.time - shownAt;
            float alpha = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((duration - age) / 0.3f);
            Hud.Fill(card, UiTheme.WithAlpha(UiTheme.Panel, 0.9f * alpha));
            Hud.Fill(new Rect(card.x, card.yMax - 2f, card.width, 2f), UiTheme.WithAlpha(UiTheme.Amber, (attached ? 0.95f : 0.45f) * alpha));
            float x = card.x + pad;
            if (key != null)
            {
                Rect chip = new Rect(x, card.y + 7f, keyW, height - 14f);
                Hud.Fill(chip, UiTheme.WithAlpha(UiTheme.Amber, 0.95f * alpha));
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(chip, key, keyStyle);
                x += keyW + 12f;
                GUI.Label(new Rect(x, card.y, textW + 12f, height), body, textStyle);
                GUI.color = old;
            }
            else
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(x, card.y, textW + 12f, height), body, textStyle);
                GUI.color = old;
            }
            LastCardRect = card;
            QaDrawn = true;
            drawnThisFrame++;
        }
    }
}
