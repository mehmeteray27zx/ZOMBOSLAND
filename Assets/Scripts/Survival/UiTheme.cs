using UnityEngine;

namespace Zombosland
{
    // Flat IMGUI look shared by the HUD, the inventory / crafting screen and the pause menu.
    // Squared corners, 1 px rules, no gradients. Uses the default IMGUI font (it renders Turkish letters).
    internal static class UiTheme
    {
        public static readonly Color Panel = Hex(0x17, 0x1D, 0x1B);
        public static readonly Color PanelLight = Hex(0x1F, 0x26, 0x24);
        public static readonly Color PanelHover = Hex(0x2A, 0x33, 0x30);
        public static readonly Color Rule = Hex(0x33, 0x3D, 0x39);
        public static readonly Color Text = Hex(0xEC, 0xE9, 0xDF);
        public static readonly Color Secondary = Hex(0xAD, 0xB5, 0xAA);
        public static readonly Color Disabled = Hex(0x6C, 0x74, 0x6D);
        public static readonly Color Amber = Hex(0xD7, 0xA4, 0x4C);
        public static readonly Color AmberHover = Hex(0xE6, 0xB8, 0x66);
        public static readonly Color Ok = Hex(0x91, 0xA7, 0x7A);
        public static readonly Color Critical = Hex(0xCF, 0x70, 0x61);
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.45f);

        static GUIStyle line, wrap, none;
        static Texture2D flat, flatHover, flatActive;
        static GUIStyle flatButton;

        public static Color Hex(int r, int g, int b) { return new Color(r / 255f, g / 255f, b / 255f, 1f); }

        public static Color WithAlpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

        static void EnsureStyles()
        {
            if (line != null) return;
            line = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Overflow, richText = false, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            wrap = new GUIStyle(line) { wordWrap = true, clipping = TextClipping.Clip };
            none = GUIStyle.none;
        }

        // ---------- primitives ----------

        public static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // Rectangle outline drawn inside r.
        public static void Border(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y + t, t, r.height - 2f * t), c);
            Fill(new Rect(r.xMax - t, r.y + t, t, r.height - 2f * t), c);
        }

        public static void HRule(float x, float y, float width, Color c) { Fill(new Rect(x, y, width, 1f), c); }

        // Line between two points in the scaled HUD space (rotated 1-px-class rectangle).
        public static void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            Matrix4x4 saved = GUI.matrix;
            // RotateAroundPivot takes the pivot in screen pixels; convert from the scaled HUD space.
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, saved.MultiplyPoint3x4(new Vector3(a.x, a.y, 0f)));
            Fill(new Rect(a.x, a.y - width / 2f, len, width), c);
            GUI.matrix = saved;
        }

        public static void Diamond(Vector2 center, float size, Color c)
        {
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, saved.MultiplyPoint3x4(new Vector3(center.x, center.y, 0f)));
            Fill(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), c);
            GUI.matrix = saved;
        }

        // ---------- text ----------

        public static float TextWidth(string s, int size, bool bold = false)
        {
            EnsureStyles();
            line.fontSize = size;
            line.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            return line.CalcSize(new GUIContent(s ?? "")).x;
        }

        public static void Label(Rect r, string s, int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            if (string.IsNullOrEmpty(s)) return;
            EnsureStyles();
            line.fontSize = size;
            line.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            line.alignment = anchor;
            line.normal.textColor = c;
            GUI.Label(r, s, line);
        }

        public static void WrapLabel(Rect r, string s, int size, Color c, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            if (string.IsNullOrEmpty(s)) return;
            EnsureStyles();
            wrap.fontSize = size;
            wrap.fontStyle = FontStyle.Normal;
            wrap.alignment = anchor;
            wrap.normal.textColor = c;
            GUI.Label(r, s, wrap);
        }

        // ---------- widgets ----------

        public enum ButtonKind { Primary, Secondary }

        // Flat button with its own fills. Returns true on click; disabled buttons never fire and ignore hover.
        public static bool Button(Rect r, string label, ButtonKind kind, bool enabled, int size = 18)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            Color fill, text;
            if (!enabled) { fill = PanelLight; text = Disabled; }
            else if (kind == ButtonKind.Primary) { fill = hover ? AmberHover : Amber; text = Panel; }
            else { fill = hover ? PanelHover : PanelLight; text = Text; }
            Fill(r, fill);
            if (kind == ButtonKind.Secondary || !enabled) Border(r, enabled ? Rule : WithAlpha(Rule, 0.6f));
            Label(r, label, size, text, TextAnchor.MiddleCenter, true);
            if (!enabled) return false;
            EnsureStyles();
            return GUI.Button(r, GUIContent.none, none);
        }

        // Invisible click area (used for cells and rows that draw themselves).
        public static bool Click(Rect r)
        {
            EnsureStyles();
            return GUI.Button(r, GUIContent.none, none);
        }

        public static bool Hover(Rect r) { return r.Contains(Event.current.mousePosition); }

        // Hand-drawn glyphs ----------------------------------------------------------------

        public static void Tick(Rect box, Color c)
        {
            float u = Mathf.Min(box.width, box.height);
            Vector2 o = new Vector2(box.x + (box.width - u) / 2f, box.y + (box.height - u) / 2f);
            Line(o + new Vector2(u * 0.12f, u * 0.55f), o + new Vector2(u * 0.40f, u * 0.82f), c, Mathf.Max(2f, u * 0.14f));
            Line(o + new Vector2(u * 0.40f, u * 0.82f), o + new Vector2(u * 0.88f, u * 0.22f), c, Mathf.Max(2f, u * 0.14f));
        }

        public static void Cross(Rect box, Color c)
        {
            float u = Mathf.Min(box.width, box.height);
            Vector2 o = new Vector2(box.x + (box.width - u) / 2f, box.y + (box.height - u) / 2f);
            Line(o + new Vector2(u * 0.18f, u * 0.18f), o + new Vector2(u * 0.82f, u * 0.82f), c, Mathf.Max(2f, u * 0.14f));
            Line(o + new Vector2(u * 0.82f, u * 0.18f), o + new Vector2(u * 0.18f, u * 0.82f), c, Mathf.Max(2f, u * 0.14f));
        }

        // Medical plus built from two fills.
        public static void Plus(Rect box, Color c)
        {
            float t = box.width * 0.34f;
            Fill(new Rect(box.x + (box.width - t) / 2f, box.y, t, box.height), c);
            Fill(new Rect(box.x, box.y + (box.height - t) / 2f, box.width, t), c);
        }

        // Simple pistol silhouette (slide, barrel block, grip) for when no icon texture is assigned.
        public static void PistolSilhouette(Rect box, Color c)
        {
            float u = box.width;
            Fill(new Rect(box.x + u * 0.08f, box.y + box.height * 0.28f, u * 0.84f, box.height * 0.22f), c);   // slide
            Fill(new Rect(box.x + u * 0.08f, box.y + box.height * 0.50f, u * 0.52f, box.height * 0.10f), c);   // frame
            Fill(new Rect(box.x + u * 0.30f, box.y + box.height * 0.50f, u * 0.20f, box.height * 0.34f), c);   // grip
            Fill(new Rect(box.x + u * 0.54f, box.y + box.height * 0.50f, u * 0.04f, box.height * 0.14f), c);   // trigger guard
        }

        // Item icon, or a square with the first letter when the item has no texture.
        public static void ItemIcon(Rect box, ItemDef item, float alpha = 1f)
        {
            if (item == null) return;
            if (item.icon != null)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(box, item.icon, ScaleMode.ScaleToFit, true);
                GUI.color = old;
                return;
            }
            Fill(box, WithAlpha(Rule, alpha));
            Border(box, WithAlpha(Secondary, alpha * 0.6f));
            string name = Name(item);
            string letter = name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?";
            Label(box, letter, Mathf.Max(12, Mathf.RoundToInt(box.height * 0.5f)), WithAlpha(Text, alpha), TextAnchor.MiddleCenter, true);
        }

        public static string Name(ItemDef d) { return d == null ? "" : (string.IsNullOrEmpty(d.displayName) ? d.name : d.displayName); }

        // ---------- IMGUI skin pieces for the pause menu ----------

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        // Flat GUIStyle button (for GUILayout use).
        public static GUIStyle FlatButton(int size)
        {
            if (flatButton == null)
            {
                flat = Solid(PanelLight);
                flatHover = Solid(PanelHover);
                flatActive = Solid(Amber);
                flatButton = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, border = new RectOffset(0, 0, 0, 0), margin = new RectOffset(4, 4, 4, 4), padding = new RectOffset(8, 8, 4, 4) };
                flatButton.normal.background = flat;
                flatButton.hover.background = flatHover;
                flatButton.active.background = flatActive;
                flatButton.focused.background = flat;
                flatButton.normal.textColor = Text;
                flatButton.hover.textColor = Text;
                flatButton.focused.textColor = Text;
                flatButton.active.textColor = Panel;
            }
            flatButton.fontSize = size;
            return flatButton;
        }

        // ---------- menu widgets (pause menu, settings, main menu) ----------
        // Rect based, no GUILayout. Every widget claims its control id on every pass (Layout and Repaint), enabled or not,
        // so the control set never differs between passes. Pointer capture goes through GUIUtility.hotControl.

        public enum MenuButtonKind { Normal, Primary, Danger }

        public const float MenuRowHeight = 44f;

        public static readonly Color PanelPressed = Hex(0x12, 0x17, 0x15);
        public static readonly Color AmberPressed = Hex(0xB4, 0x86, 0x38);
        public static readonly Color CriticalDim = Hex(0x8F, 0x4C, 0x43);
        public static readonly Color CriticalPressed = Hex(0x74, 0x3C, 0x34);
        public static readonly Color MenuScrim = new Color(0.02f, 0.03f, 0.03f, 0.8f);

        // Shared pointer logic: true on a completed click (press and release inside the rect).
        static bool Press(Rect r, bool enabled, out bool hover, out bool down)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, r);
            Event e = Event.current;
            bool inside = r.Contains(e.mousePosition);
            bool clicked = false;
            if (enabled)
            {
                EventType t = e.GetTypeForControl(id);
                if (t == EventType.MouseDown && e.button == 0 && inside)
                {
                    GUIUtility.hotControl = id;
                    e.Use();
                }
                else if (t == EventType.MouseUp && GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    clicked = inside;
                    e.Use();
                }
            }
            int hot = GUIUtility.hotControl;
            hover = enabled && inside && (hot == 0 || hot == id);
            down = enabled && inside && hot == id;
            return clicked;
        }

        // Large menu button. Primary is amber; Danger is for destructive confirmations.
        public static bool MenuButton(Rect r, string label, MenuButtonKind kind, bool enabled, int size = 20)
        {
            bool hover, down;
            bool clicked = Press(r, enabled, out hover, out down);
            if (Event.current.type == EventType.Repaint)
            {
                Color fill, text;
                bool border = true;
                Color borderColor = hover || down ? Amber : Rule;
                if (!enabled) { fill = PanelLight; text = Disabled; borderColor = WithAlpha(Rule, 0.6f); }
                else if (kind == MenuButtonKind.Primary) { fill = down ? AmberPressed : (hover ? AmberHover : Amber); text = Panel; border = false; }
                else if (kind == MenuButtonKind.Danger) { fill = down ? CriticalPressed : (hover ? Critical : CriticalDim); text = Text; border = false; }
                else { fill = down ? PanelPressed : (hover ? PanelHover : PanelLight); text = Text; }
                Fill(r, fill);
                if (border) Border(r, borderColor);
                Label(r, label, size, text, TextAnchor.MiddleCenter, true);
            }
            return clicked;
        }

        // Thin-rail slider. The whole rect is the hit area: click anywhere on it to jump, then drag. Returns the new value
        // (also sets GUI.changed). step > 0 snaps to multiples of step from min.
        public static float Slider(Rect r, float value, float min, float max, float step, bool enabled)
        {
            const float handleW = 12f, handleH = 24f, trackH = 4f;
            int id = GUIUtility.GetControlID(FocusType.Passive, r);
            Event e = Event.current;
            float x0 = r.x + handleW / 2f, x1 = r.xMax - handleW / 2f;
            bool inside = r.Contains(e.mousePosition);
            if (enabled)
            {
                EventType t = e.GetTypeForControl(id);
                if (t == EventType.MouseDown && e.button == 0 && inside)
                {
                    GUIUtility.hotControl = id;
                    value = SliderFromMouse(e.mousePosition.x, x0, x1, min, max, step, value);
                    e.Use();
                }
                else if (t == EventType.MouseDrag && GUIUtility.hotControl == id)
                {
                    value = SliderFromMouse(e.mousePosition.x, x0, x1, min, max, step, value);
                    e.Use();
                }
                else if (t == EventType.MouseUp && GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
            }
            if (e.type == EventType.Repaint)
            {
                int hot = GUIUtility.hotControl;
                bool drag = enabled && hot == id;
                bool hover = enabled && inside && (hot == 0 || drag);
                float t01 = Mathf.InverseLerp(min, max, value);
                float cx = Mathf.Lerp(x0, x1, t01);
                float cy = r.y + r.height / 2f;
                Rect track = new Rect(x0, cy - trackH / 2f, x1 - x0, trackH);
                Fill(track, Rule);
                Fill(new Rect(track.x, track.y, cx - track.x, trackH), enabled ? Amber : Disabled);
                Color handle = !enabled ? Disabled : (drag || hover ? AmberHover : Text);
                Fill(new Rect(cx - handleW / 2f, cy - handleH / 2f, handleW, handleH), handle);
            }
            return value;
        }

        static float SliderFromMouse(float mouseX, float x0, float x1, float min, float max, float step, float current)
        {
            float v = Mathf.Lerp(min, max, Mathf.InverseLerp(x0, x1, mouseX));
            if (step > 0f) v = min + Mathf.Round((v - min) / step) * step;
            v = Mathf.Clamp(v, min, max);
            if (!Mathf.Approximately(v, current)) GUI.changed = true;
            else v = current; // keep the exact old value so nothing downstream sees float noise
            return v;
        }

        // On / off switch. The rect is the hit area; the switch sits at its right edge with "Açık" / "Kapalı" to its left.
        public static bool Toggle(Rect r, bool value, bool enabled)
        {
            bool hover, down;
            bool clicked = Press(r, enabled, out hover, out down);
            if (Event.current.type == EventType.Repaint)
            {
                const float tw = 48f, th = 24f, knob = 16f;
                Rect track = new Rect(r.xMax - tw, r.y + (r.height - th) / 2f, tw, th);
                Color fill = !enabled ? PanelLight : (value ? (hover || down ? AmberHover : Amber) : (hover || down ? PanelHover : Rule));
                Fill(track, fill);
                Border(track, !enabled ? WithAlpha(Rule, 0.6f) : (hover || down ? Amber : Rule));
                float kx = value ? track.xMax - 4f - knob : track.x + 4f;
                Fill(new Rect(kx, track.y + (th - knob) / 2f, knob, knob), !enabled ? Disabled : (value ? Panel : Text));
                Color text = !enabled ? Disabled : (value ? Text : Secondary);
                Label(new Rect(r.x, r.y, r.width - tw - 12f, r.height), value ? "Açık" : "Kapalı", 18, text, TextAnchor.MiddleRight, true);
            }
            if (clicked) { value = !value; GUI.changed = true; }
            return value;
        }

        // "‹ value ›" chooser for a short list of discrete options. Returns the new index (clamped, no wrap).
        public static int Selector(Rect r, string text, int index, int count, bool enabled)
        {
            const float arrow = 44f;
            Rect mid = new Rect(r.x + arrow, r.y, r.width - 2f * arrow, r.height);
            Fill(mid, PanelLight);
            Label(mid, text, 18, enabled ? Text : Disabled, TextAnchor.MiddleCenter, true);
            bool prev = MenuButton(new Rect(r.x, r.y, arrow, r.height), "‹", MenuButtonKind.Normal, enabled && index > 0, 26);
            bool next = MenuButton(new Rect(r.xMax - arrow, r.y, arrow, r.height), "›", MenuButtonKind.Normal, enabled && index < count - 1, 26);
            if (prev) { index--; GUI.changed = true; }
            else if (next) { index++; GUI.changed = true; }
            return index;
        }

        // Tab header: the selected one is bright with an amber underline. True when clicked.
        public static bool Tab(Rect r, string label, bool selected, bool enabled)
        {
            bool hover, down;
            bool clicked = Press(r, enabled, out hover, out down);
            if (Event.current.type == EventType.Repaint)
            {
                if (down) Fill(r, PanelPressed);
                else if (hover && !selected) Fill(r, PanelHover);
                Color text = !enabled ? Disabled : (selected || hover ? Text : Secondary);
                Label(r, label, 18, text, TextAnchor.MiddleCenter, true);
                if (selected) Fill(new Rect(r.x, r.yMax - 3f, r.width, 3f), Amber);
            }
            return clicked;
        }

        // Modal question. Returns 0 while open, 1 for yes, 2 for no. Draw it last, with the page behind it disabled.
        public static int Confirm(float w, float h, string title, string message, string yes, string no)
        {
            Fill(new Rect(0f, 0f, w, h), WithAlpha(Color.black, 0.5f));
            const float pw = 560f, ph = 256f;
            Rect p = new Rect(Mathf.Round((w - pw) / 2f), Mathf.Round((h - ph) / 2f), pw, ph);
            Fill(p, WithAlpha(Panel, 0.99f));
            Border(p, Rule);
            Fill(new Rect(p.x, p.y, p.width, 3f), Amber);
            Label(new Rect(p.x + 32f, p.y + 24f, p.width - 64f, 40f), title, 24, Text, TextAnchor.MiddleLeft, true);
            WrapLabel(new Rect(p.x + 32f, p.y + 80f, p.width - 64f, 56f), message, 18, Secondary);
            int result = 0;
            Rect yesRect = new Rect(p.x + 32f, p.yMax - 32f - 48f, 240f, 48f);
            Rect noRect = new Rect(p.xMax - 32f - 240f, p.yMax - 32f - 48f, 240f, 48f);
            if (MenuButton(yesRect, yes, MenuButtonKind.Danger, true, 20)) result = 1;
            if (MenuButton(noRect, no, MenuButtonKind.Primary, true, 20)) result = 2;
            return result;
        }
    }
}
