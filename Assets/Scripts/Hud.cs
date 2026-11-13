using UnityEngine;

namespace Zombosland
{
    // IMGUI heads-up display scaled to 1080p: health, ammo, quick slots, compass, objective, toast, crosshair, hit marker,
    // damage vignette. While the inventory / crafting screen is open it hands over to SurvivalUi.
    // Every layout value derives from the virtual size (w, h), so nothing clips from 16:9 up to ultrawide.
    public class Hud : MonoBehaviour
    {
        // Optional pistol icon for quick slot 1 (drawn as a simple silhouette when null).
        public Texture2D pistolIcon;

        Sandbox player;
        PlayerMotor motor;
        Texture2D vignette;
        bool showHelp = true;
        float helpUntil, spread, markerTime = -10f;
        bool markerHead, markerKill;
        Interactor interactor;
        Camera viewCamera;
        Rect ammoBox;

        const float Margin = 28f;

        void Awake()
        {
            player = GetComponent<Sandbox>();
            motor = GetComponent<PlayerMotor>();
            interactor = GetComponent<Interactor>();
        }

        void OnEnable() { player = GetComponent<Sandbox>(); if (player != null) player.HitConfirmed += OnHit; }
        void OnDisable() { if (player != null) player.HitConfirmed -= OnHit; }

        void Start()
        {
            helpUntil = Time.unscaledTime + 10f;
            if (QuestFuse.Instance != null) showHelp = false; // Ilk Ikmal: the control strip only opens with F1
            viewCamera = GetComponentInChildren<Camera>();
            vignette = MakeVignette();
        }

        void OnHit(HitZone zone, bool kill)
        {
            markerTime = Time.unscaledTime;
            markerHead = zone == HitZone.Head;
            markerKill = kill;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) { showHelp = !showHelp; helpUntil = float.MaxValue; }
            float target = motor.Recoil * 3f + motor.HorizontalSpeed * 1.5f + (motor.Grounded ? 0f : 6f);
            spread = Mathf.Lerp(spread, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }

        static Texture2D MakeVignette()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    float a = Mathf.Clamp01((d - 0.35f) / 0.65f);
                    tex.SetPixel(x, y, new Color(0.65f, 0f, 0f, a * a));
                }
            tex.Apply();
            return tex;
        }

        internal static void Fill(Rect r, Color c) { UiTheme.Fill(r, c); }

        void OnGUI()
        {
            if (player == null) return;
            float scale = Mathf.Clamp(Screen.height / 1080f, .75f, 2f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float w = Screen.width / scale, h = Screen.height / scale;
            if (MainMenu.Active) { MainMenu.Draw(player, w, h); return; } // the main menu hides the whole HUD

            PlayerSurvival survival = player.Survival;
            bool screen = survival != null && survival.UiOpen && !player.IsDead;
            if (!screen) SurvivalUi.Idle();

            DrawVignette(w, h);
            if (!player.IsDead)
            {
                DrawHealth(h);
                DrawAmmo(w, h);
                if (screen)
                {
                    SurvivalUi.Draw(player, survival, w, h, pistolIcon);
                }
                else
                {
                    DrawCrosshair(w, h);
                    DrawHitMarker(w, h);
                    DrawHelp();
                    DrawCompass(w);
                    DrawObjective();
                    if (survival != null) SurvivalUi.DrawQuickBar(survival, w, h, pistolIcon);
                    DrawWorldLabel(scale, w, h);
                    DrawHints(scale, w, h);
                    DrawPickupFeed(w);
                }
                DrawToast(w, h);
            }
            if (player.IsDead)
            {
                Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, Mathf.Clamp01(player.SecondsSinceDeath / 1.5f)));
                UiTheme.Label(new Rect(0, h / 2 - 30, w, 60), "Öldün", 30, UiTheme.Text, TextAnchor.MiddleCenter, true);
            }
            else if (player.MenuOpen) PauseMenu.Draw(player, w, h);
        }

        void DrawVignette(float w, float h)
        {
            float since = Time.time - player.LastHurtTime;
            float alpha = Mathf.Clamp01(1f - since / 0.7f) * 0.9f;
            if (player.Health < 30f && !player.IsDead) alpha = Mathf.Max(alpha, 0.25f + 0.1f * Mathf.Sin(Time.unscaledTime * 5f));
            if (alpha <= 0.01f) return;
            Color old = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(0, 0, w, h), vignette);
            GUI.color = old;
        }

        // Bottom left: plus icon, "CAN", number and a thin bar. Critical colour below 30.
        void DrawHealth(float h)
        {
            Rect box = new Rect(Margin, h - Margin - 56f, 320f, 56f);
            UiTheme.Fill(box, UiTheme.WithAlpha(UiTheme.Panel, 0.86f));
            UiTheme.Border(box, UiTheme.Rule);
            float max = Mathf.Max(1f, player.MaxHealth);
            float fraction = Mathf.Clamp01(player.Health / max);
            bool low = player.Health < 30f;
            Color c = low ? UiTheme.Critical : UiTheme.Ok;
            UiTheme.Plus(new Rect(box.x + 14f, box.y + 16f, 24f, 24f), c);
            UiTheme.Label(new Rect(box.x + 52f, box.y + 6f, 60f, 16f), "CAN", 13, UiTheme.Secondary, TextAnchor.MiddleLeft, true);
            UiTheme.Label(new Rect(box.x + 52f, box.y + 20f, 70f, 32f), Mathf.CeilToInt(player.Health).ToString(), 26, low ? UiTheme.Critical : UiTheme.Text, TextAnchor.MiddleLeft, true);
            Rect bar = new Rect(box.x + 126f, box.y + 24f, box.width - 126f - 16f, 8f);
            UiTheme.Fill(bar, UiTheme.Rule);
            UiTheme.Fill(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), c);

            // Stamina: a thin line on top of the health box, hidden while full. Critical colour while sprint is locked.
            float stamina = motor.Stamina01;
            if (stamina < 0.999f)
            {
                Rect line = new Rect(box.x, box.y - 7f, box.width, 4f);
                UiTheme.Fill(line, UiTheme.WithAlpha(UiTheme.Panel, 0.86f));
                UiTheme.Fill(new Rect(line.x, line.y, line.width * stamina, line.height), motor.Exhausted ? UiTheme.Critical : UiTheme.Text);
            }
        }

        // Bottom right: "8 / 24", "9 mm" under it, reload bar and short states. The bat name replaces it while a melee weapon is in hand.
        void DrawAmmo(float w, float h)
        {
            const float bw = 232f, bh = 84f;
            ammoBox = new Rect(w - Margin - bw, h - Margin - bh, bw, bh);
            Rect b = ammoBox;
            UiTheme.Fill(b, UiTheme.WithAlpha(UiTheme.Panel, 0.86f));
            UiTheme.Border(b, UiTheme.Rule);

            if (player.Carrier == HandCarrier.Hands)
            {
                UiTheme.Label(new Rect(b.x + 16f, b.y + 6f, bw - 32f, 48f), "Boş el", 28, UiTheme.Text, TextAnchor.MiddleLeft, true);
                UiTheme.Label(new Rect(b.x + 16f, b.y + 56f, bw - 32f, 20f), InputLabels.Key(InputLabels.Holster) + " silahı çek", 13, UiTheme.Secondary, TextAnchor.MiddleLeft, true);
                return;
            }

            if (player.MeleeEquipped)
            {
                UiTheme.Label(new Rect(b.x + 16f, b.y + 6f, bw - 32f, 48f), UiTheme.Name(player.MeleeItem), 28, UiTheme.Text, TextAnchor.MiddleLeft, true);
                UiTheme.Label(new Rect(b.x + 16f, b.y + 56f, 100f, 20f), "MELEE", 13, UiTheme.Secondary, TextAnchor.MiddleLeft, true);
                return;
            }

            WeaponState wp = player.Weapon;
            string ammo = wp.Ammo.ToString();
            float aw = UiTheme.TextWidth(ammo, 40, true);
            UiTheme.Label(new Rect(b.x + 16f, b.y + 4f, aw + 4f, 52f), ammo, 40, wp.Ammo == 0 ? UiTheme.Critical : UiTheme.Text, TextAnchor.MiddleLeft, true);
            UiTheme.Label(new Rect(b.x + 20f + aw, b.y + 8f, bw - 36f - aw, 48f), " / " + wp.Reserve, 24, UiTheme.Secondary, TextAnchor.MiddleLeft);
            UiTheme.Label(new Rect(b.x + 16f, b.y + 56f, 60f, 20f), "9 mm", 13, UiTheme.Secondary);

            Rect state = new Rect(b.x + 76f, b.y + 56f, bw - 92f, 20f);
            if (wp.Reloading)
            {
                float p = wp.ReloadProgress(Time.time);
                UiTheme.Label(state, "Dolduruluyor", 13, UiTheme.Amber, TextAnchor.MiddleRight, true);
                UiTheme.Fill(new Rect(b.x + 16f, b.yMax - 7f, bw - 32f, 3f), UiTheme.Rule);
                UiTheme.Fill(new Rect(b.x + 16f, b.yMax - 7f, (bw - 32f) * Mathf.Clamp01(p), 3f), UiTheme.Amber);
            }
            else if (wp.Ammo == 0 && wp.Reserve > 0)
            {
                // The reload hint card sits on this panel and says the same thing; show only one of the two.
                HintDirector hints = HintDirector.Instance;
                if (hints == null || !hints.Showing(HintDirector.Reload)) UiTheme.Label(state, InputLabels.Reload + " — doldur", 13, UiTheme.Amber, TextAnchor.MiddleRight, true);
            }
            else if (wp.Ammo == 0) UiTheme.Label(state, "Mühimmat yok", 13, UiTheme.Critical, TextAnchor.MiddleRight, true);
        }

        // Tiny white dot with a thin shadow; it grows slightly with `spread` and fades on ADS.
        void DrawCrosshair(float w, float h)
        {
            float fade = 1f - Mathf.Clamp01(player.AdsAmount * 2f);
            if (fade <= 0.01f) return;
            float d = 3f + Mathf.Clamp(spread * 0.12f, 0f, 2.5f);
            float cx = w / 2f, cy = h / 2f;
            UiTheme.Fill(new Rect(cx - d / 2f - 1f, cy - d / 2f - 1f, d + 2f, d + 2f), new Color(0, 0, 0, 0.55f * fade));
            UiTheme.Fill(new Rect(cx - d / 2f, cy - d / 2f, d, d), new Color(1, 1, 1, 0.95f * fade));
        }

        void DrawHitMarker(float w, float h)
        {
            float age = Time.unscaledTime - markerTime;
            if (age > 0.15f) return;
            float alpha = 1f - age / 0.15f;
            Color c = markerHead ? new Color(1f, 0.15f, 0.12f, alpha) : new Color(1f, 1f, 1f, alpha);
            float len = markerKill ? 16f : 10f, inner = markerKill ? 10f : 7f;
            Matrix4x4 saved = GUI.matrix;
            for (int i = 0; i < 4; i++)
            {
                GUI.matrix = saved;
                // RotateAroundPivot takes the pivot in screen pixels; convert from the scaled HUD space.
                GUIUtility.RotateAroundPivot(45f + 90f * i, saved.MultiplyPoint3x4(new Vector3(w / 2f, h / 2f, 0f)));
                Fill(new Rect(w / 2f + inner, h / 2f - 1f, len, 2f), c);
            }
            GUI.matrix = saved;
        }

        // Y of the F1 strip: below the compass in Ilk Ikmal, in the corner elsewhere.
        static float StripY { get { return QuestFuse.Instance != null ? 64f : Margin; } }

        void DrawHelp()
        {
            if (!showHelp || Time.unscaledTime > helpUntil) return;
            string text = InputLabels.Move + " hareket | " + InputLabels.Sprint + " koş | " + InputLabels.Crouch + " eğil | " + InputLabels.Jump + " zıpla | "
                + InputLabels.Fire + " ateş | " + InputLabels.Aim + " nişan | " + InputLabels.Reload + " doldur | "
                + (QuestFuse.Instance != null ? InputLabels.Interact + " etkileşim | " + InputLabels.Inventory + " çanta | 1-4 hızlı yuva | " + InputLabels.Holster + " silah çek/kaldır | " : "")
                + InputLabels.Menu + " ayarlar | " + InputLabels.Help + " yardım";
            float width = UiTheme.TextWidth(text, 15) + 32f;
            Rect r = new Rect(Margin, StripY, width, 36f);
            UiTheme.Fill(r, UiTheme.WithAlpha(UiTheme.Panel, 0.86f));
            UiTheme.Border(r, UiTheme.Rule);
            UiTheme.Label(new Rect(r.x + 16f, r.y, width - 24f, r.height), text, 15, UiTheme.Text);
        }

        // Hint card (at most one), bottom right above the ammo panel.
        void DrawHints(float scale, float w, float h)
        {
            HintDirector hints = HintDirector.Instance;
            if (hints != null) hints.Draw(scale, w, h, ammoBox.x, ammoBox.y, ammoBox.width);
        }

        // Pickup lines ("+2 Kumaş") bottom right above the ammo panel; they move above the hint card while one is up.
        void DrawPickupFeed(float w)
        {
            int n = PickupFeed.Count;
            if (n == 0) return;
            float bottom = ammoBox.y;
            HintDirector hints = HintDirector.Instance;
            if (hints != null && hints.CurrentId != null)
            {
                Rect card = hints.LastCardRect;
                bottom = Mathf.Min(bottom, card.height > 1f ? card.y : ammoBox.y - 110f);
            }
            const float rowH = 30f, gap = 4f, icon = 22f;
            float y = bottom - 10f - rowH, right = ammoBox.xMax;
            for (int i = n - 1; i >= 0; i--)
            {
                PickupFeed.Line l = PickupFeed.At(i);
                float a = PickupFeed.Alpha(l);
                if (a > 0.01f)
                {
                    string text = "+" + l.count + " " + UiTheme.Name(l.item);
                    float tw = UiTheme.TextWidth(text, 16, true);
                    float rw = 10f + icon + 8f + tw + 14f;
                    Rect r = new Rect(right - rw, y, rw, rowH);
                    UiTheme.Fill(r, UiTheme.WithAlpha(UiTheme.Panel, 0.92f * a));
                    UiTheme.Border(r, UiTheme.WithAlpha(UiTheme.Rule, a));
                    UiTheme.Fill(new Rect(r.x, r.y, 2f, r.height), UiTheme.WithAlpha(UiTheme.Amber, a));
                    UiTheme.ItemIcon(new Rect(r.x + 10f, r.y + (rowH - icon) / 2f, icon, icon), l.item, a);
                    UiTheme.Label(new Rect(r.x + 10f + icon + 8f, r.y, tw + 8f, rowH), text, 16, UiTheme.WithAlpha(UiTheme.Text, a), TextAnchor.MiddleLeft, true);
                }
                y -= rowH + gap;
            }
        }

        // Quest objective, top left (below the control strip while that is visible). Amber for 1.5 s after a change.
        void DrawObjective()
        {
            QuestFuse q = QuestFuse.Instance;
            if (q == null) return;
            bool stripVisible = showHelp && Time.unscaledTime <= helpUntil;
            float y = stripVisible ? StripY + 44f : 24f;
            bool flash = Time.unscaledTime - q.StateChangedAt < 1.5f;
            string heading = q.ObjectiveHeading, text = q.ObjectiveText;
            float inner = Mathf.Max(UiTheme.TextWidth(heading, 13, true), UiTheme.TextWidth(text, 20, true));
            Rect r = new Rect(Margin, y, Mathf.Max(220f, inner + 32f), 66f);
            UiTheme.Fill(r, UiTheme.WithAlpha(UiTheme.Panel, flash ? 0.94f : 0.86f));
            UiTheme.Border(r, flash ? UiTheme.Amber : UiTheme.Rule);
            UiTheme.Label(new Rect(r.x + 16f, r.y + 6f, r.width - 32f, 18f), heading, 13, UiTheme.Secondary, TextAnchor.MiddleLeft, true);
            UiTheme.Fill(new Rect(r.x + 16f, r.y + 27f, r.width - 32f, 1f), UiTheme.Amber);
            UiTheme.Label(new Rect(r.x + 16f, r.y + 32f, r.width - 32f, 28f), text, 20, flash ? UiTheme.Amber : UiTheme.Text, TextAnchor.MiddleLeft, true);
        }

        // Top centre compass strip: 180 degrees across, K (north, +Z) D (east, +X) G B, amber diamond for the objective marker.
        void DrawCompass(float w)
        {
            QuestFuse q = QuestFuse.Instance;
            if (q == null) return;
            Transform cam = viewCamera != null ? viewCamera.transform : transform;
            float cw = Mathf.Min(620f, w - 80f), ch = 32f;
            Rect strip = new Rect((w - cw) / 2f, 20f, cw, ch);
            UiTheme.Fill(strip, UiTheme.WithAlpha(UiTheme.Panel, 0.86f));
            UiTheme.Border(strip, UiTheme.Rule);

            float yaw = Mathf.Repeat(cam.eulerAngles.y, 360f);
            float ppd = cw / 180f, cx = strip.center.x;
            int first = Mathf.FloorToInt((yaw - 90f) / 15f), last = Mathf.CeilToInt((yaw + 90f) / 15f);
            for (int i = first; i <= last; i++)
            {
                float px = cx + (i * 15f - yaw) * ppd;
                if (px < strip.x + 8f || px > strip.xMax - 8f) continue;
                int deg = ((i * 15) % 360 + 360) % 360;
                if (deg % 90 == 0)
                {
                    string letter = deg == 0 ? "K" : (deg == 90 ? "D" : (deg == 180 ? "G" : "B"));
                    UiTheme.Label(new Rect(px - 10f, strip.y + 1f, 20f, 20f), letter, 15, deg == 0 ? UiTheme.Text : UiTheme.Secondary, TextAnchor.MiddleCenter, true);
                    UiTheme.Fill(new Rect(px - 0.5f, strip.yMax - 8f, 1f, 6f), UiTheme.Secondary);
                }
                else if (deg % 45 == 0) UiTheme.Fill(new Rect(px - 0.5f, strip.yMax - 12f, 1f, 10f), UiTheme.Secondary);
                else UiTheme.Fill(new Rect(px - 0.5f, strip.yMax - 7f, 1f, 5f), UiTheme.Disabled);
            }
            UiTheme.Fill(new Rect(cx - 1f, strip.y + 1f, 2f, 5f), UiTheme.Text); // view direction

            if (q.HasObjectiveMarker)
            {
                Vector3 to = q.ObjectiveMarker - cam.position;
                float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float limit = cw / 2f - 12f;
                float dx = Mathf.Clamp(Mathf.DeltaAngle(yaw, bearing) * ppd, -limit, limit);
                UiTheme.Diamond(new Vector2(cx + dx, strip.center.y), 9f, UiTheme.Amber);
            }
        }

        // Toast above the quick slots (centre of the screen stays clear).
        void DrawToast(float w, float h)
        {
            string text = Notifier.Current;
            if (text == null) return;
            float now = Time.unscaledTime;
            float a = Mathf.Clamp01((now - Notifier.ShownAt) / 0.12f) * Mathf.Clamp01((Notifier.Until - now) / 0.25f);
            if (a <= 0.01f) return;
            float tw = UiTheme.TextWidth(text, 18) + 40f;
            Rect r = new Rect((w - tw) / 2f, h - 24f - 64f - 8f - 34f, tw, 34f);
            UiTheme.Fill(r, UiTheme.WithAlpha(UiTheme.Panel, 0.94f * a));
            UiTheme.Border(r, UiTheme.WithAlpha(UiTheme.Rule, a));
            UiTheme.Fill(new Rect(r.x, r.y, 2f, r.height), UiTheme.WithAlpha(UiTheme.Amber, a));
            UiTheme.Label(r, text, 18, UiTheme.WithAlpha(UiTheme.Text, a), TextAnchor.MiddleCenter);
        }

        // "[E] verb" in a compact opaque box near the focused object. Nothing else (no enemy health, no distances).
        void DrawWorldLabel(float scale, float w, float h)
        {
            if (interactor == null || viewCamera == null || interactor.Label == null || interactor.Anchor == null) return;
            Vector3 sp = viewCamera.WorldToScreenPoint(interactor.Anchor.position);
            if (sp.z <= 0f) return; // behind the camera
            Vector2 anchor = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            if (anchor.x < 0f || anchor.x > w || anchor.y < 0f || anchor.y > h) return; // off screen
            string text = interactor.Label;
            bool dim = interactor.LabelDim;
            string title = interactor.Title;
            bool titled = !string.IsNullOrEmpty(title);
            float textW = UiTheme.TextWidth(text, 18, true);
            if (titled) textW = Mathf.Max(textW, UiTheme.TextWidth(title, 15, true));
            float boxH = titled ? 56f : 34f;
            Rect box = new Rect(Mathf.Clamp(anchor.x + 24f, 10f, w - textW - 44f), Mathf.Clamp(anchor.y - 46f, 10f, h - boxH - 16f), textW + 32f, boxH);
            UiTheme.Fill(box, UiTheme.Panel);
            UiTheme.Border(box, UiTheme.Rule);
            UiTheme.Fill(new Rect(box.x, box.y, 3f, box.height), dim ? UiTheme.Disabled : UiTheme.Amber);
            if (titled)
            {
                UiTheme.Label(new Rect(box.x + 16f, box.y + 4f, textW + 12f, 20f), title, 15, UiTheme.Secondary, TextAnchor.MiddleLeft, true);
                UiTheme.Label(new Rect(box.x + 16f, box.y + 24f, textW + 12f, 28f), text, 18, dim ? UiTheme.Secondary : UiTheme.Text, TextAnchor.MiddleLeft, true);
            }
            else UiTheme.Label(new Rect(box.x + 16f, box.y, textW + 12f, box.height), text, 18, dim ? UiTheme.Secondary : UiTheme.Text, TextAnchor.MiddleLeft, true);
        }
    }
}
