using System.Collections.Generic;
using UnityEngine;

namespace Zombosland
{
    // Tabbed settings page shared by the pause menu and the main menu. It draws its own panel; the caller draws the scrim.
    // Immediate-mode, rect based: the same controls are claimed in the Layout and Repaint passes. Value strings and the
    // resolution / quality lists are cached, so an open page allocates only when a value actually changes.
    internal static class SettingsPanel
    {
        const float PanelW = 880f, PanelH = 640f, Pad = 32f, RowH = UiTheme.MenuRowHeight, Stride = 48f, CtlW = 360f;
        const string EscHint = InputLabels.Menu + " ile geri dön";
        const string VideoNote = "Çözünürlük 'Uygula' ile değişir. VSync açıkken FPS sınırı kullanılmaz.";

        static readonly string[] TabNames = { "OYUN", "SES", "GRAFİK", "KONTROLLER" };
        static readonly string[] ScreenModes = { "Tam ekran", "Pencere" };
        static readonly string[] FpsLabels = { "30", "60", "120", "144", "Sınırsız" };

        // Read-only key list (keys are fixed in code, see InputLabels).
        static readonly string[] CtlNames =
        {
            "Hareket", "Koş", "Eğil", "Zıpla", "Ateş", "Nişan", "Doldur",
            "Etkileşim", "Çanta", "Silahı çek / kaldır", "Hızlı yuva", "Duraklat / Ayarlar", "Yardım şeridi"
        };
        static readonly string[] CtlKeys =
        {
            InputLabels.Move, InputLabels.Sprint, InputLabels.Crouch, InputLabels.Jump, InputLabels.Fire, InputLabels.Aim, InputLabels.Reload,
            InputLabels.Interact, InputLabels.Inventory, InputLabels.Holster, InputLabels.Quick(0) + "–" + InputLabels.Quick(3), InputLabels.Menu, InputLabels.Help
        };
        static float[] ctlKeyW;

        static int tab;
        static int pendingRes = -1; // index into the resolution list; -1 = take it from GameSettings on the next draw

        static string[] qualityLabels;
        static List<int> resW, resH;
        static List<string> resText;

        // Value text cache for the six sliders: rebuilt only when the value changes.
        static readonly float[] valueCache = new float[6];
        static readonly string[] valueText = new string[6];

        internal static void Reset()
        {
            tab = 0;
            pendingRes = -1;
        }

        // Draws the page. Returns true when "Geri" was pressed. enabled = false while a dialog is on top.
        internal static bool Draw(float w, float h, bool enabled)
        {
            Rect panel = new Rect(Mathf.Round((w - PanelW) / 2f), Mathf.Round((h - PanelH) / 2f), PanelW, PanelH);
            UiTheme.Fill(panel, UiTheme.WithAlpha(UiTheme.Panel, 0.99f));
            UiTheme.Border(panel, UiTheme.Rule);
            UiTheme.Label(new Rect(panel.x + Pad, panel.y + 24f, 400f, 40f), "AYARLAR", 28, UiTheme.Text, TextAnchor.MiddleLeft, true);

            float tabW = (PanelW - 2f * Pad) / TabNames.Length, tabY = panel.y + 80f;
            UiTheme.HRule(panel.x + Pad, tabY + 47f, PanelW - 2f * Pad, UiTheme.Rule);
            for (int i = 0; i < TabNames.Length; i++)
                if (UiTheme.Tab(new Rect(panel.x + Pad + i * tabW, tabY, tabW, 48f), TabNames[i], tab == i, enabled)) tab = i;

            Rect area = new Rect(panel.x + Pad, panel.y + 144f, PanelW - 2f * Pad, 384f);
            if (tab == 0) DrawGame(area, enabled);
            else if (tab == 1) DrawAudio(area, enabled);
            else if (tab == 2) DrawGraphics(area, enabled);
            else DrawControls(area);

            bool back = UiTheme.MenuButton(new Rect(panel.x + Pad, panel.yMax - 24f - 48f, 160f, 48f), "‹ Geri", UiTheme.MenuButtonKind.Normal, enabled, 20);
            UiTheme.Label(new Rect(panel.xMax - Pad - 240f, panel.yMax - 24f - 48f, 240f, 48f), EscHint, 16, UiTheme.Secondary, TextAnchor.MiddleRight);
            return back;
        }

        // ---------- row helpers ----------

        // Row background (hover), bottom rule and the name on the left. Returns the row rect.
        static Rect BeginRow(Rect area, int index, string name, bool enabled)
        {
            Rect row = new Rect(area.x, area.y + index * Stride, area.width, RowH);
            if (enabled && UiTheme.Hover(row)) UiTheme.Fill(row, UiTheme.WithAlpha(UiTheme.PanelLight, 0.7f));
            UiTheme.HRule(row.x, row.yMax + 2f, row.width, UiTheme.WithAlpha(UiTheme.Rule, 0.5f));
            UiTheme.Label(new Rect(row.x + 8f, row.y, row.width - CtlW - 24f, RowH), name, 18, enabled ? UiTheme.Text : UiTheme.Disabled);
            return row;
        }

        static Rect Control(Rect row) { return new Rect(row.xMax - CtlW, row.y, CtlW, RowH); }

        // mode: 0 = one decimal, 1 = integer, 2 = percent.
        static float SliderRow(Rect row, int slot, float value, float min, float max, float step, int mode, bool enabled)
        {
            Rect c = Control(row);
            float nv = UiTheme.Slider(new Rect(c.x, c.y, c.width - 88f, c.height), value, min, max, step, enabled);
            UiTheme.Label(new Rect(c.xMax - 80f, c.y, 80f, c.height), ValueText(slot, nv, mode), 18, enabled ? UiTheme.Text : UiTheme.Disabled, TextAnchor.MiddleRight, true);
            return nv;
        }

        static string ValueText(int slot, float v, int mode)
        {
            if (valueText[slot] != null && valueCache[slot] == v) return valueText[slot];
            valueCache[slot] = v;
            valueText[slot] = mode == 0 ? v.ToString("F1") : (mode == 1 ? v.ToString("F0") : Mathf.RoundToInt(v * 100f) + "%");
            return valueText[slot];
        }

        // ---------- tabs ----------

        static void DrawGame(Rect a, bool enabled)
        {
            Rect row = BeginRow(a, 0, "Fare hassasiyeti", enabled);
            GameSettings.Sensitivity = SliderRow(row, 0, GameSettings.Sensitivity, 0.3f, 5f, 0.1f, 0, enabled);
            row = BeginRow(a, 1, "Görüş alanı (FOV)", enabled);
            GameSettings.Fov = SliderRow(row, 1, GameSettings.Fov, 60f, 100f, 1f, 1, enabled);
            row = BeginRow(a, 2, "Y eksenini ters çevir", enabled);
            GameSettings.InvertY = UiTheme.Toggle(Control(row), GameSettings.InvertY, enabled);
            row = BeginRow(a, 3, "Headbob", enabled);
            GameSettings.Headbob = SliderRow(row, 2, GameSettings.Headbob, 0f, 1f, 0.05f, 2, enabled);
            row = BeginRow(a, 4, "Silah salınımı", enabled);
            GameSettings.WeaponSway = SliderRow(row, 3, GameSettings.WeaponSway, 0f, 1f, 0.05f, 2, enabled);
            row = BeginRow(a, 5, "Kamera sarsıntısı", enabled);
            GameSettings.CameraShake = SliderRow(row, 4, GameSettings.CameraShake, 0f, 1f, 0.05f, 2, enabled);
            if (HintDirector.Instance != null) // Ilk Ikmal only; CombatSandbox has no hint cards
            {
                row = BeginRow(a, 6, "Öğretici", enabled);
                GameSettings.Tutorial = UiTheme.Toggle(Control(row), GameSettings.Tutorial, enabled);
                row = BeginRow(a, 7, "Öğretici ilerlemesini sıfırla", enabled);
                Rect c = Control(row);
                if (UiTheme.MenuButton(new Rect(c.xMax - 160f, c.y, 160f, c.height), "Sıfırla", UiTheme.MenuButtonKind.Normal, enabled, 18)) HintDirector.ResetProgress();
            }
        }

        static void DrawAudio(Rect a, bool enabled)
        {
            Rect row = BeginRow(a, 0, "Ana ses", enabled);
            float v = SliderRow(row, 5, GameSettings.MasterVolume, 0f, 1f, 0.05f, 2, enabled);
            if (v != GameSettings.MasterVolume)
            {
                GameSettings.MasterVolume = v;
                GameSettings.ApplyAudio();
            }
        }

        static void DrawGraphics(Rect a, bool enabled)
        {
            EnsureVideoLists();

            // Quality level (applies at once).
            Rect row = BeginRow(a, 0, "Kalite", enabled);
            int q = Mathf.Clamp(GameSettings.VideoQuality, 0, qualityLabels.Length - 1);
            int nq = UiTheme.Selector(Control(row), qualityLabels[q], q, qualityLabels.Length, enabled);
            if (nq != q)
            {
                GameSettings.VideoQuality = nq;
                GameSettings.VideoTouched = true;
                GameSettings.ApplyFrame();
            }

            // Window mode (applies at once; also picks a matching resolution).
            row = BeginRow(a, 1, "Ekran kipi", enabled);
            int mode = GameSettings.Fullscreen ? 0 : 1;
            int nm = UiTheme.Selector(Control(row), ScreenModes[mode], mode, ScreenModes.Length, enabled);
            if (nm != mode)
            {
                GameSettings.SetFullscreen(nm == 0);
                GameSettings.VideoTouched = true;
                GameSettings.ApplyScreen();
                pendingRes = -1;
            }

            // Resolution: pick, then "Uygula".
            row = BeginRow(a, 2, "Çözünürlük", enabled);
            if (pendingRes < 0 || pendingRes >= resW.Count) pendingRes = IndexOfRes(GameSettings.ResW, GameSettings.ResH);
            Rect c = Control(row);
            pendingRes = UiTheme.Selector(new Rect(c.x, c.y, c.width - 120f, c.height), resText[pendingRes], pendingRes, resW.Count, enabled);
            bool dirty = resW[pendingRes] != GameSettings.ResW || resH[pendingRes] != GameSettings.ResH;
            if (UiTheme.MenuButton(new Rect(c.xMax - 112f, c.y, 112f, c.height), "Uygula", UiTheme.MenuButtonKind.Primary, enabled && dirty, 18))
            {
                GameSettings.ResW = resW[pendingRes];
                GameSettings.ResH = resH[pendingRes];
                GameSettings.VideoTouched = true;
                GameSettings.ApplyScreen();
            }

            // VSync.
            row = BeginRow(a, 3, "Dikey senkron (VSync)", enabled);
            bool vs = UiTheme.Toggle(Control(row), GameSettings.VSync, enabled);
            if (vs != GameSettings.VSync)
            {
                GameSettings.VSync = vs;
                GameSettings.VideoTouched = true;
                GameSettings.ApplyFrame();
            }

            // Frame cap: meaningless while VSync is on.
            bool capEnabled = enabled && !GameSettings.VSync;
            row = BeginRow(a, 4, "FPS sınırı", capEnabled);
            int fi = FpsIndex(GameSettings.FpsCap);
            int nf = UiTheme.Selector(Control(row), FpsLabels[fi], fi, FpsLabels.Length, capEnabled);
            if (nf != fi)
            {
                GameSettings.FpsCap = GameSettings.FpsOptions[nf];
                GameSettings.VideoTouched = true;
                GameSettings.ApplyFrame();
            }

            UiTheme.Label(new Rect(a.x + 8f, a.y + 5f * Stride + 12f, a.width - 16f, 24f), VideoNote, 16, UiTheme.Secondary);
        }

        static void DrawControls(Rect a)
        {
            if (ctlKeyW == null)
            {
                ctlKeyW = new float[CtlKeys.Length];
                for (int i = 0; i < CtlKeys.Length; i++) ctlKeyW[i] = UiTheme.TextWidth(CtlKeys[i], 18, true) + 24f;
            }
            const int perColumn = 7;
            float colW = (a.width - 32f) / 2f;
            for (int i = 0; i < CtlNames.Length; i++)
            {
                Rect row = new Rect(a.x + (i / perColumn) * (colW + 32f), a.y + (i % perColumn) * Stride, colW, RowH);
                UiTheme.HRule(row.x, row.yMax + 2f, row.width, UiTheme.WithAlpha(UiTheme.Rule, 0.5f));
                UiTheme.Label(new Rect(row.x + 8f, row.y, row.width - 150f, RowH), CtlNames[i], 18, UiTheme.Text);
                Rect chip = new Rect(row.xMax - 8f - ctlKeyW[i], row.y + 6f, ctlKeyW[i], RowH - 12f);
                UiTheme.Fill(chip, UiTheme.PanelLight);
                UiTheme.Border(chip, UiTheme.Rule);
                UiTheme.Label(chip, CtlKeys[i], 18, UiTheme.Amber, TextAnchor.MiddleCenter, true);
            }
        }

        // ---------- cached lists ----------

        static void EnsureVideoLists()
        {
            if (qualityLabels == null)
            {
                string[] names = QualitySettings.names;
                qualityLabels = new string[names.Length];
                for (int i = 0; i < names.Length; i++)
                    qualityLabels[i] = names[i] == "PC" ? "Yüksek" : (names[i] == "Mobile" ? "Düşük" : names[i]);
            }
            if (resW == null)
            {
                resW = new List<int>();
                resH = new List<int>();
                resText = new List<string>();
                foreach (Resolution r in Screen.resolutions) AddRes(r.width, r.height);
            }
        }

        static int FindRes(int w, int h)
        {
            for (int i = 0; i < resW.Count; i++)
                if (resW[i] == w && resH[i] == h) return i;
            return -1;
        }

        static int IndexOfRes(int w, int h)
        {
            int i = FindRes(w, h);
            return i >= 0 ? i : AddRes(w, h);
        }

        // Unique, ordered by pixel count. Returns the index of the entry.
        static int AddRes(int w, int h)
        {
            if (w < 640 || h < 360) return resW.Count > 0 ? 0 : AddRaw(0, w, h);
            int existing = FindRes(w, h);
            if (existing >= 0) return existing;
            int at = 0;
            while (at < resW.Count && (long)resW[at] * resH[at] <= (long)w * h) at++;
            return AddRaw(at, w, h);
        }

        static int AddRaw(int at, int w, int h)
        {
            resW.Insert(at, w);
            resH.Insert(at, h);
            resText.Insert(at, w + " x " + h);
            return at;
        }

        static int FpsIndex(int cap)
        {
            if (cap <= 0) return FpsLabels.Length - 1;
            int best = 0, bestDelta = int.MaxValue;
            for (int i = 0; i < GameSettings.FpsOptions.Length - 1; i++)
            {
                int d = Mathf.Abs(GameSettings.FpsOptions[i] - cap);
                if (d < bestDelta) { bestDelta = d; best = i; }
            }
            return best;
        }
    }
}
