using UnityEngine;

namespace Zombosland
{
    // Viewmodel presentation. Two paths:
    //  - animated: LPSP arms + pistol driven by Animator controllers (ADS, fire, reload come from clips);
    //    this class only feeds parameters, adds small cosmetic offsets and plays cues.
    //  - procedural (fallback): primitive pistol moved by code (ADS pose, sway, fire kick, reload, wall pull-back).
    // It reads WeaponState; nothing here can change ammo, reload or hit results.
    // Survivalist arms set (default since v0.5b). Built inactive by ViewmodelFactory; Awake activates it unless --arms=lpsp.
    [System.Serializable]
    public class AlternateArms
    {
        public GameObject root;
        public Animator armsAnimator, pistolAnimator;
        public Transform muzzle, eject;
        public GameObject axe; // v0.7 axe under this set's hand_r (inactive); null when not built
        public GameObject bat; // v0.8 bat under this set's hand_r (inactive); null when not built
        public GameObject bandageRoll;     // under hand_r, shown while wrapping
        public GameObject[] bandageRings;  // under lowerarm_l, appear one by one
    }

    public class ViewmodelPresenter : MonoBehaviour
    {
        // LPSP arms root (fallback, --arms=lpsp) and the Survivalist set (default); Awake swaps them.
        public GameObject defaultArmsRoot;
        public AlternateArms survivalist;
        public string ArmsSet { get; private set; } = "lpsp";
        // v0.7 axe of the LPSP set (the Survivalist one is AlternateArms.axe); the active set's axe is picked in Awake.
        public GameObject axe;
        // v0.8 LPSP-set counterparts of AlternateArms.bat / bandageRoll / bandageRings.
        public GameObject bat, bandageRoll;
        public GameObject[] bandageRings;

        public Transform muzzle, slide, magazine;
        // Animated path.
        public Animator armsAnimator, pistolAnimator;
        public Transform eject;
        public bool animated;

        static readonly Vector3 HipPosition = new Vector3(0.17f, -0.17f, 0.38f);
        // Viewmodel position that puts the sight line (rear and front sight) on the camera axis. Set by the builder.
        public Vector3 adsPosition = new Vector3(0f, -0.04f, 0.34f);
        const float WallProbe = 0.9f;

        // LPSP clip lengths (s). The reload clips are driven by the ReloadTime parameter = elapsed / length.
        const float ReloadClipSeconds = 2.233f, ReloadEmptyClipSeconds = 2.733f;
        // Empty reload: the slide slams shut on clip frame 47 (0-based 46 at 30 fps), matching the slide hit in the reload sound.
        public const float ReloadEmptySlamSeconds = 46f / 30f;
        const float ReloadSlamShake = 0.8f;
        static readonly int AimParam = Animator.StringToHash("Aim");
        static readonly int ReloadTimeParam = Animator.StringToHash("ReloadTime");
        static readonly int PoseState = Animator.StringToHash("Pose");
        static readonly int IdleState = Animator.StringToHash("Idle");
        static readonly int FireState = Animator.StringToHash("Fire");
        static readonly int SlideBackState = Animator.StringToHash("SlideBack");
        static readonly int ReloadState = Animator.StringToHash("Reload");
        static readonly int ReloadEmptyState = Animator.StringToHash("ReloadEmpty");
        static readonly int AxeIdleState = Animator.StringToHash("AxeIdle");
        static readonly int AxeSwingState = Animator.StringToHash("AxeSwing");
        static readonly int HandsIdleState = Animator.StringToHash("Hands_Idle");
        static readonly int BatIdleState = Animator.StringToHash("Bat_Idle");
        static readonly int BatSwingState = Animator.StringToHash("Bat_Swing");
        static readonly int LowerState = Animator.StringToHash("Handgun_Lower");
        static readonly int RaiseState = Animator.StringToHash("Handgun_Raise");
        static readonly int CraftState = Animator.StringToHash("Handgun_Craft");
        static readonly int PistolDrawState = Animator.StringToHash("Pistol_Draw");
        static readonly int PistolHolsterState = Animator.StringToHash("Pistol_Holster");
        static readonly int BatDrawState = Animator.StringToHash("Bat_Draw");
        static readonly int AxeDrawState = Animator.StringToHash("Axe_Draw");
        static readonly int SwingRateParam = Animator.StringToHash("SwingRate");

        // Clip timings (s) shared with Sandbox.Hands. Blender frames at 30 fps, see Docs/SPEC_v0.8_hands_loop.md.
        public const float PickupSeconds = 0.40f, PickupCommit = 0.20f;
        public const float ContainerSeconds = 0.80f, ContainerCommit = 0.467f;
        public const float BandageSeconds = 1.70f, BandageCommit = 1.20f;
        public const float PistolHolsterSeconds = 0.38f, DipSeconds = 0.16f;
        public const float PistolDrawSeconds = 0.58f, HeldDrawSeconds = 0.567f, HandsDrawSeconds = 0.22f;
        const float BatSwingClipSeconds = 20f / 30f, LowerClipSeconds = 6f / 30f;
        const float BandageGunHiddenFrom = 8f / 30f, BandageGunHiddenUntil = 44f / 30f;
        static readonly float[] BandageRingAt = { 20f / 30f, 26f / 30f, 33f / 30f };

        Sandbox player;
        PlayerMotor motor;
        Transform aim;
        Vector3 magazineRest, slideRest;
        Vector2 sway;
        float kick, kickVelocity, wallAmount, bobPhase, bobAmount;
        bool playedMagOut, playedMagIn, playedSlide;

        // Animated-path bookkeeping.
        bool wasReloading, reloadActive, reloadClipSound, slideLocked, wasAds, slamPending;
        float reloadClock, reloadClipLength, resetReloadTimeAt = -1f, lastShotTime = -100f;

        public Vector3 MuzzlePosition { get { return muzzle != null ? muzzle.position : transform.position; } }

        // What the hands hold. The pistol renderers are switched off for every other carrier; the arms stay visible.
        Renderer[] pistolRenderers;
        bool[] pistolRendererStates, pistolRendererIsGun;
        public HandCarrier Carrier { get; private set; } = HandCarrier.Pistol;
        public bool PistolVisible { get { return Carrier == HandCarrier.Pistol; } }
        public bool AxeMode { get { return Carrier == HandCarrier.Axe; } }

        GameObject activeAxe, activeBat, activeRoll;
        GameObject[] activeRings;
        public bool AxeAvailable { get { return activeAxe != null && armsAnimator != null; } }
        public bool BatAvailable { get { return activeBat != null && armsAnimator != null; } }

        // Hand action presentation.
        HandActionKind actionKind = HandActionKind.None;
        float actionClock, dipClock = -1f, dipSeconds, reachWeight;
        Vector3 reachTarget;
        bool hasReachTarget, lowered, crafting;
        public bool Lowered { get { return lowered; } }
        // True until the reload clip and its tail have finished; hand actions wait for it so the reload is never cut.
        public bool ReloadAnimating { get { return reloadActive; } }

        void EnsureRendererCache()
        {
            if (pistolRenderers != null) return;
            // Gun renderers = everything under the active set's ik_hand_gun (pistol + magazine).
            Transform gunAnchor = null;
            GameObject activeRoot = ArmsSet == "survivalist" && survivalist != null ? survivalist.root : defaultArmsRoot;
            if (activeRoot != null)
                foreach (Transform t in activeRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == "ik_hand_gun") { gunAnchor = t; break; }
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<MeleeViewmodel>() != null) continue; // the placeholder melee view stays out of this list
                if (IsHeldRenderer(r)) continue; // axe, bat and bandage props are never part of the pistol list
                list.Add(r);
            }
            pistolRenderers = list.ToArray();
            pistolRendererStates = new bool[pistolRenderers.Length];
            pistolRendererIsGun = new bool[pistolRenderers.Length];
            for (int i = 0; i < pistolRenderers.Length; i++)
            {
                pistolRendererStates[i] = pistolRenderers[i].enabled;
                pistolRendererIsGun[i] = gunAnchor != null && pistolRenderers[i].transform.IsChildOf(gunAnchor);
            }
        }

        static bool Under(Renderer r, GameObject root) { return root != null && r.transform.IsChildOf(root.transform); }

        static bool UnderAny(Renderer r, GameObject[] roots)
        {
            if (roots == null) return false;
            foreach (GameObject g in roots) if (Under(r, g)) return true;
            return false;
        }

        bool IsHeldRenderer(Renderer r)
        {
            if (Under(r, axe) || Under(r, bat) || Under(r, bandageRoll) || UnderAny(r, bandageRings)) return true;
            if (survivalist == null) return false;
            return Under(r, survivalist.axe) || Under(r, survivalist.bat) || Under(r, survivalist.bandageRoll) || UnderAny(r, survivalist.bandageRings);
        }

        void ApplyRendererVisibility()
        {
            // No bat art (fallback): the old placeholder melee view replaces the arms completely.
            bool hideAll = Carrier == HandCarrier.Bat && activeBat == null;
            for (int i = 0; i < pistolRenderers.Length; i++)
                if (pistolRenderers[i] != null) pistolRenderers[i].enabled = !hideAll && pistolRendererStates[i] && !(Carrier != HandCarrier.Pistol && pistolRendererIsGun[i]);
        }

        // Swaps what the hands hold. With `instant` the arms go straight to the carrier idle (saves, QA, fallback).
        public void SetCarrier(HandCarrier c, bool instant)
        {
            EnsureRendererCache();
            Carrier = c;
            ApplyRendererVisibility();
            // The reload / slide bookkeeping of the pistol restarts clean after any other carrier.
            reloadActive = false; slideLocked = false; resetReloadTimeAt = -1f;
            if (activeAxe != null) activeAxe.SetActive(c == HandCarrier.Axe);
            if (activeBat != null) activeBat.SetActive(c == HandCarrier.Bat);
            lowered = false;
            dipClock = -1f;
            if (instant) ReturnToIdle(0.15f);
        }

        int IdleStateFor(HandCarrier c)
        {
            switch (c)
            {
                case HandCarrier.Hands: return HandsIdleState;
                case HandCarrier.Bat: return BatIdleState;
                case HandCarrier.Axe: return AxeIdleState;
                default: return PoseState;
            }
        }

        void Play(int state, float fade, float offset = 0f)
        {
            if (armsAnimator != null && armsAnimator.HasState(0, state)) armsAnimator.CrossFadeInFixedTime(state, fade, 0, offset);
        }

        public void ReturnToIdle(float fade)
        {
            if (!animated) return;
            if (Carrier == HandCarrier.Pistol) SetReloadTime(0f);
            Play(IdleStateFor(Carrier), fade);
            if (Carrier == HandCarrier.Pistol && pistolAnimator != null) pistolAnimator.CrossFadeInFixedTime(IdleState, fade, 0, 0f);
        }

        string CarrierPrefix()
        {
            switch (Carrier)
            {
                case HandCarrier.Hands: return "Hands";
                case HandCarrier.Bat: return "Bat";
                case HandCarrier.Axe: return "Axe";
                default: return "Handgun";
            }
        }

        // Starts a hand action clip. A reload tail is never running here (Sandbox refuses the action while ReloadAnimating).
        public void PlayHandAction(HandActionKind kind, bool hasTarget, Vector3 target)
        {
            actionKind = kind;
            actionClock = 0f;
            hasReachTarget = hasTarget;
            reachTarget = target;
            if (!animated) return;
            string state = kind == HandActionKind.Bandage ? "Handgun_Bandage" : CarrierPrefix() + (kind == HandActionKind.OpenContainer ? "_OpenContainer" : "_Pickup");
            Play(Animator.StringToHash(state), 0.08f);
        }

        // The action ended (or was cancelled): back to the carrier idle, or to the lowered pose while a menu is open.
        public void EndHandAction(bool stayLowered)
        {
            actionKind = HandActionKind.None;
            hasReachTarget = false;
            HideBandageProps();
            ShowHeld(true);
            if (!animated) return;
            if (stayLowered && Carrier == HandCarrier.Pistol)
            {
                lowered = true;
                Play(LowerState, 0.14f);
            }
            else ReturnToIdle(0.14f);
        }

        public void SetLowered(bool value)
        {
            if (!animated || Carrier != HandCarrier.Pistol || value == lowered) return;
            lowered = value;
            Play(value ? LowerState : RaiseState, 0.1f);
        }

        // Work motion while a craft runs behind the menu.
        public void SetCrafting(bool on)
        {
            if (!animated || on == crafting) return;
            crafting = on;
            ShowHeld(!on);
            if (on) Play(CraftState, 0.25f);
            else if (lowered && Carrier == HandCarrier.Pistol) Play(LowerState, 0.2f, LowerClipSeconds);
            else ReturnToIdle(0.2f);
        }

        // First half of a swap: the holster clip for the pistol, a short procedural dip for everything else.
        public void PlayHolster(float seconds)
        {
            if (animated && Carrier == HandCarrier.Pistol && armsAnimator != null && armsAnimator.HasState(0, PistolHolsterState)) Play(PistolHolsterState, 0.06f);
            else { dipClock = 0f; dipSeconds = Mathf.Max(0.05f, seconds); }
        }

        // Second half of a swap: the new carrier comes up.
        public void PlayDraw()
        {
            dipClock = -1f;
            if (!animated) return;
            switch (Carrier)
            {
                case HandCarrier.Pistol: if (pistolAnimator != null) pistolAnimator.Play(IdleState, 0, 0f); Play(PistolDrawState, 0.05f); break;
                case HandCarrier.Bat: Play(BatDrawState, 0.05f); break;
                case HandCarrier.Axe: Play(AxeDrawState, 0.05f); break;
                default: Play(HandsIdleState, 0.2f); break;
            }
        }

        public void PlayAxeSwing()
        {
            if (AxeMode && armsAnimator != null) armsAnimator.CrossFadeInFixedTime(AxeSwingState, 0.03f, 0, 0f);
        }

        public void PlayBatSwing(float seconds)
        {
            if (Carrier != HandCarrier.Bat || armsAnimator == null) return;
            armsAnimator.SetFloat(SwingRateParam, BatSwingClipSeconds / Mathf.Max(0.1f, seconds));
            armsAnimator.CrossFadeInFixedTime(BatSwingState, 0.03f, 0, 0f);
        }

        // QA stills: the object in hand matches the posed carrier.
        internal void HideHeldForQa(HandCarrier c)
        {
            if (activeAxe != null) activeAxe.SetActive(c == HandCarrier.Axe);
            if (activeBat != null) activeBat.SetActive(c == HandCarrier.Bat);
        }

        void ShowHeld(bool show)
        {
            if (activeAxe != null) activeAxe.SetActive(show && Carrier == HandCarrier.Axe);
            if (activeBat != null) activeBat.SetActive(show && Carrier == HandCarrier.Bat);
        }

        void HideBandageProps()
        {
            if (activeRoll != null) activeRoll.SetActive(false);
            if (activeRings != null) foreach (GameObject g in activeRings) if (g != null) g.SetActive(false);
        }

        void UpdateActionPresentation(float dt)
        {
            if (actionKind == HandActionKind.None) { reachWeight = Mathf.MoveTowards(reachWeight, 0f, 8f * dt); return; }
            actionClock += dt;
            reachWeight = Mathf.MoveTowards(reachWeight, hasReachTarget ? 1f : 0f, 8f * dt);
            if (actionKind != HandActionKind.Bandage) return;
            bool wrapping = actionClock >= BandageGunHiddenFrom && actionClock < BandageGunHiddenUntil;
            ShowHeld(!wrapping); // a bat or axe is put aside while both hands work
            if (activeRoll != null) activeRoll.SetActive(wrapping);
            if (activeRings != null)
                for (int i = 0; i < activeRings.Length && i < BandageRingAt.Length; i++)
                    if (activeRings[i] != null) activeRings[i].SetActive(wrapping && actionClock >= BandageRingAt[i]);
        }

        void Awake()
        {
            // Must run before anything reads the animator / muzzle references.
            if (QaArgs.GetString("--arms", "survivalist") != "lpsp" && survivalist != null && survivalist.root != null && survivalist.armsAnimator != null)
            {
                if (defaultArmsRoot != null) defaultArmsRoot.SetActive(false);
                survivalist.root.SetActive(true);
                armsAnimator = survivalist.armsAnimator; pistolAnimator = survivalist.pistolAnimator;
                muzzle = survivalist.muzzle; eject = survivalist.eject;
                ArmsSet = "survivalist";
            }
            bool surv = ArmsSet == "survivalist";
            activeAxe = surv ? survivalist.axe : axe;
            activeBat = surv ? survivalist.bat : bat;
            activeRoll = surv ? survivalist.bandageRoll : bandageRoll;
            activeRings = surv ? survivalist.bandageRings : bandageRings;
            player = GetComponentInParent<Sandbox>();
            motor = GetComponentInParent<PlayerMotor>();
            if (magazine != null) magazineRest = magazine.localPosition;
            if (slide != null) slideRest = slide.localPosition;
        }

        public void OnShot()
        {
            kick = 1f;
            kickVelocity = 0f;
            lastShotTime = Time.time;
            if (animated)
            {
                // A shot ends any reload tail; ReloadTime is cleared a little later so the fade-out has a pose to blend from.
                if (reloadActive) { reloadActive = false; resetReloadTimeAt = Time.time + 0.2f; }
                slideLocked = false;
                if (armsAnimator != null) armsAnimator.CrossFadeInFixedTime(FireState, 0.03f, 0, 0f);
                if (pistolAnimator != null) pistolAnimator.Play(FireState, 0, 0f);
            }
            if (EffectPool.Instance != null)
            {
                if (muzzle != null) EffectPool.Instance.MuzzleFlash(muzzle);
                if (animated && eject != null) EffectPool.Instance.EjectCasing(eject);
            }
        }

        void LateUpdate()
        {
            if (player == null || player.IsPaused || player.AimPivot == null) return;
            aim = player.AimPivot;
            float dt = Time.deltaTime;
            float ads = player.AdsAmount;
            float swayScale = GameSettings.WeaponSway * Mathf.Lerp(1f, 0.25f, ads);

            Vector3 position = animated ? Vector3.zero : Vector3.Lerp(HipPosition, adsPosition, ads);
            Vector3 euler = Vector3.zero;

            UpdateActionPresentation(dt);
            // Reach bends a little toward the object (the clip reaches at the crosshair).
            if (reachWeight > 0.001f && hasReachTarget)
            {
                Vector3 local = aim.InverseTransformPoint(reachTarget);
                if (local.z > 0.05f)
                {
                    float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -5f, 5f);
                    float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg, -5f, 5f);
                    euler += new Vector3(pitch, yaw, 0f) * reachWeight;
                }
            }
            // Procedural holster dip (non-pistol carriers and the first half of a swap).
            if (dipClock >= 0f)
            {
                dipClock += dt;
                float d = Smooth(dipClock / dipSeconds);
                position += new Vector3(0.03f, -0.26f, -0.08f) * d;
                euler.x += 28f * d;
            }

            // Sway: mouse delta rotates the weapon with lag (max ~3 degrees).
            Vector2 target = Vector2.zero;
            if (!player.PerfMode && motor.InputEnabled)
                target = new Vector2(-Input.GetAxisRaw("Mouse Y"), -Input.GetAxisRaw("Mouse X")) * 0.8f;
            target = Vector2.ClampMagnitude(target, 3f);
            sway = Vector2.Lerp(sway, target, 1f - Mathf.Exp(-10f * dt));
            euler += new Vector3(sway.x, sway.y, sway.y * 0.5f) * swayScale;

            // Small walking bob on the weapon.
            bool moving = motor.Grounded && motor.HorizontalSpeed > 0.5f;
            bobAmount = Mathf.MoveTowards(bobAmount, moving ? 1f : 0f, 6f * dt);
            if (moving) bobPhase += dt * Mathf.PI * 2f * (motor.Sprinting ? 2.6f : 1.8f);
            position += new Vector3(Mathf.Sin(bobPhase) * 0.004f, Mathf.Abs(Mathf.Sin(bobPhase)) * -0.005f, 0f) * (bobAmount * swayScale);

            // Fire kick: spring back to rest in roughly 0.08 s (half strength on the animated path: the Fire clip kicks too).
            for (float left = dt; left > 0f; left -= 0.008f)
            {
                float step = Mathf.Min(left, 0.008f);
                kickVelocity += (-900f * kick - 42f * kickVelocity) * step;
                kick += kickVelocity * step;
            }
            float kickScale = GameSettings.WeaponSway * (animated ? 0.5f : 1f);
            position += new Vector3(0, 0, -0.04f) * (kick * kickScale);
            euler.x -= 4f * kick * kickScale;

            float now = Time.time;
            WeaponState weapon = player.Weapon;
            float p = weapon.ReloadProgress(now);
            if (!animated)
            {
                // Reload pose and parts.
                float pose = ReloadPose(p);
                position += new Vector3(-0.05f, -0.13f, -0.03f) * pose;
                euler += new Vector3(18f, 12f, -28f) * pose;
                UpdateReloadParts(p, weapon.Reloading, weapon.Ammo == 0, true);
            }

            // Wall pull-back: weapon dips and retracts instead of disappearing.
            RaycastHit wall;
            float wantWall = 0f;
            if (Physics.SphereCast(aim.position, 0.08f, aim.forward, out wall, WallProbe, Layers.ShotMask, QueryTriggerInteraction.Ignore))
                wantWall = Mathf.Clamp01(1f - wall.distance / WallProbe);
            wallAmount = Mathf.MoveTowards(wallAmount, Mathf.Clamp01(wantWall * 1.6f), 6f * dt);
            if (animated)
            {
                position += new Vector3(0f, -0.05f, -0.10f) * wallAmount;
                euler.x += 40f * wallAmount;
            }
            else
            {
                position = Vector3.Lerp(position, new Vector3(position.x, -0.33f, 0.14f), wallAmount);
                euler.x += 35f * wallAmount;
            }

            transform.localPosition = position;
            transform.localRotation = Quaternion.Euler(euler);

            if (animated) UpdateAnimators(weapon, ads, p, dt, now);
        }

        // ---------- animated path ----------

        void UpdateAnimators(WeaponState weapon, float ads, float progress, float dt, float now)
        {
            SfxBank sfx = SfxBank.Instance;
            if (armsAnimator != null) armsAnimator.SetFloat(AimParam, ads);
            if (pistolAnimator != null) pistolAnimator.SetFloat(AimParam, ads);

            if (player.AdsHeld && !wasAds && sfx != null) sfx.Play2D(Sfx.AimIn, 0.35f);
            wasAds = player.AdsHeld;

            // Another carrier in hand: the pistol animation logic (reload start / tail, slide lock) stays off; the arms belong to its clips.
            if (Carrier != HandCarrier.Pistol) { wasReloading = weapon.Reloading; return; }

            // Reload start is detected from WeaponState (false -> true), never from clip events.
            bool reloading = weapon.Reloading;
            if (reloading && !wasReloading) BeginReloadAnimation(weapon.ReloadFromEmpty, sfx);
            wasReloading = reloading;

            if (reloadActive)
            {
                reloadClock += dt;
                SetReloadTime(reloadClock / reloadClipLength);
                if (slamPending && reloadClock >= ReloadEmptySlamSeconds)
                {
                    slamPending = false;
                    player.AddCameraShake(ReloadSlamShake);
                }
                if (!reloadClipSound) UpdateReloadParts(progress, reloading, weapon.ReloadFromEmpty, false);
                // After the game-side reload ends the clip tail keeps playing at real speed, then returns to the pose.
                if (!reloading && (reloadClock / reloadClipLength >= 0.98f || reloadClock > reloadClipLength + 0.5f)) EndReloadAnimation();
            }
            else if (!reloading) UpdateReloadParts(0f, false, false, false); // resets the fallback cue flags
            if (resetReloadTimeAt > 0f && now >= resetReloadTimeAt) { resetReloadTimeAt = -1f; SetReloadTime(0f); }

            // Last round fired: after the Fire clip the slide stays back until the next reload.
            if (weapon.Ammo > 0) slideLocked = false;
            else if (!reloading && !reloadActive && !slideLocked && pistolAnimator != null && now - lastShotTime > 0.1f)
            {
                AnimatorStateInfo st = pistolAnimator.GetCurrentAnimatorStateInfo(0);
                bool firing = st.shortNameHash == FireState && st.normalizedTime < 0.9f;
                if (!firing)
                {
                    pistolAnimator.CrossFadeInFixedTime(SlideBackState, 0.05f, 0);
                    slideLocked = true;
                }
            }
        }

        void BeginReloadAnimation(bool fromEmpty, SfxBank sfx)
        {
            reloadActive = true;
            slideLocked = false;
            reloadClock = 0f;
            reloadClipLength = fromEmpty ? ReloadEmptyClipSeconds : ReloadClipSeconds;
            slamPending = fromEmpty;
            resetReloadTimeAt = -1f;
            SetReloadTime(0f);
            int state = fromEmpty ? ReloadEmptyState : ReloadState;
            if (armsAnimator != null) armsAnimator.CrossFadeInFixedTime(state, 0.1f, 0);
            if (pistolAnimator != null) pistolAnimator.CrossFadeInFixedTime(state, 0.1f, 0);
            Sfx id = fromEmpty ? Sfx.ReloadEmpty : Sfx.ReloadTactical;
            reloadClipSound = sfx != null && sfx.Has(id);
            if (reloadClipSound) sfx.Play2D(id, 0.8f);
        }

        void EndReloadAnimation()
        {
            reloadActive = false;
            if (armsAnimator != null) armsAnimator.CrossFadeInFixedTime(PoseState, 0.15f, 0);
            if (pistolAnimator != null) pistolAnimator.CrossFadeInFixedTime(IdleState, 0.15f, 0);
            resetReloadTimeAt = Time.time + 0.2f;
        }

        void SetReloadTime(float value)
        {
            if (armsAnimator != null) armsAnimator.SetFloat(ReloadTimeParam, value);
            if (pistolAnimator != null) pistolAnimator.SetFloat(ReloadTimeParam, value);
        }

        // ---------- procedural path ----------

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // 0 = normal pose, 1 = reload pose. Goes down/aside in 0..0.2, holds, returns at the end.
        static float ReloadPose(float p)
        {
            if (p <= 0f) return 0f;
            if (p < 0.2f) return Smooth(p / 0.2f);
            if (p < 0.88f) return 1f;
            return 1f - Smooth((p - 0.88f) / 0.12f);
        }

        // Sound cues (always) and magazine/slide motion (only when moveParts: the procedural pistol).
        void UpdateReloadParts(float p, bool reloading, bool empty, bool moveParts)
        {
            SfxBank sfx = SfxBank.Instance;
            if (!reloading)
            {
                playedMagOut = playedMagIn = playedSlide = false;
            }
            else
            {
                if (!playedMagOut && p >= 0.25f) { playedMagOut = true; if (sfx != null) sfx.Play2D(Sfx.MagOut, 0.6f); }
                if (!playedMagIn && p >= 0.65f) { playedMagIn = true; if (sfx != null) sfx.Play2D(Sfx.MagIn, 0.7f); }
                if (!playedSlide && p >= 0.85f) { playedSlide = true; if (sfx != null) sfx.Play2D(Sfx.Slide, 0.7f); }
            }
            if (!moveParts) return;

            if (magazine != null)
            {
                float drop = 0f;
                if (reloading)
                {
                    if (p < 0.35f) drop = Smooth((p - 0.2f) / 0.15f);
                    else if (p < 0.55f) drop = 1f;
                    else if (p < 0.65f) drop = 1f - Smooth((p - 0.55f) / 0.10f);
                }
                magazine.localPosition = magazineRest + Vector3.down * (0.12f * drop);
            }
            if (slide != null)
            {
                float back = 0f;
                if (reloading && p >= 0.8f) back = p < 0.88f ? Smooth((p - 0.8f) / 0.08f) : 1f - Smooth((p - 0.88f) / 0.07f);
                else if (!reloading && empty) back = 1f; // slide locked back on an empty magazine
                slide.localPosition = slideRest + Vector3.back * (0.03f * back);
            }
        }
    }
}
