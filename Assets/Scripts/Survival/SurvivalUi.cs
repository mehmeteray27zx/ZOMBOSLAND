using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Zombosland
{
    // Inventory / crafting screen and the quick-slot bar (IMGUI, drawn by the HUD while PlayerSurvival.UiOpen).
    // Input gating and the cursor are handled by PlayerSurvival; Tab and Esc close the screen there. This class only draws,
    // reads clicks and arrow / Enter / Q / E key events, and calls the public PlayerSurvival / Crafter / Inventory API.
    internal static class SurvivalUi
    {
        enum Zone { Tabs, Bag, Storage, Recipes, Actions }
        enum Act { Use, Equip, ToStorage, ToBag, Quick1, Quick2, Quick3 }

        const int Cols = 6;
        const float HeaderH = 56f, FooterH = 40f, Pad = 20f;

        static bool active;
        static Zone zone;
        static int focus;                   // index inside the focused zone (tab, cell, recipe row or action)
        static bool selStorage;
        static int selSlot = -1;
        static RecipeDef selRecipe;
        static int qty = 1;
        static bool onlyCraftable;
        static int scroll;
        static RecipeDef pendingRecipe;     // QA: applied when the screen opens
        static int pendingSlot = -1;
        static ItemDef hoverItem;
        static int hoverReserved;

        static readonly List<RecipeDef> recipes = new List<RecipeDef>();   // visible rows (available + filter)
        static readonly List<Act> actions = new List<Act>();
        static bool anyBenchRecipe;

        // ---------- lifecycle ----------

        // Called by the HUD every OnGUI while the screen is closed, so the next opening starts clean.
        internal static void Idle() { active = false; }

        internal static void QaSelectRecipe(RecipeDef r) { pendingRecipe = r; selRecipe = r; }
        internal static void QaSelectSlot(int slot) { pendingSlot = slot; selSlot = slot; selStorage = false; }

        static void Reset(PlayerSurvival ps)
        {
            active = true;
            zone = ps.Tab == UiTab.Inventory ? Zone.Bag : Zone.Recipes;
            focus = 0;
            selStorage = false;
            selSlot = pendingSlot;
            selRecipe = pendingRecipe;
            pendingSlot = -1;
            pendingRecipe = null;
            qty = 1;
            onlyCraftable = false;
            scroll = 0;
        }

        // ---------- quick bar ----------

        // Four quick slots bottom centre. Slot 0 is the pistol (fixed equipment).
        internal static void DrawQuickBar(PlayerSurvival ps, float w, float h, Texture2D pistolIcon)
        {
            if (ps == null || ps.Bag == null || ps.Quick == null) return;
            const float size = 64f, gap = 10f;
            float x0 = (w - (4 * size + 3 * gap)) / 2f, y = h - 24f - size;
            for (int i = 0; i < PlayerSurvival.QuickSlots; i++)
            {
                Rect r = new Rect(x0 + i * (size + gap), y, size, size);
                ItemDef item = i > 0 ? ps.Quick[i] : null;
                int avail = item != null ? ps.Bag.Available(item) : 0;
                bool dim = item != null && avail <= 0;
                UiTheme.Fill(r, UiTheme.WithAlpha(UiTheme.Panel, 0.9f));
                UiTheme.Border(r, UiTheme.Rule);
                Rect icon = new Rect(r.x + 12f, r.y + 12f, size - 24f, size - 24f);
                if (i == 0)
                {
                    if (pistolIcon != null) GUI.DrawTexture(icon, pistolIcon, ScaleMode.ScaleToFit, true);
                    else UiTheme.PistolSilhouette(icon, UiTheme.Text);
                }
                else if (item != null)
                {
                    UiTheme.ItemIcon(icon, item, dim ? 0.35f : 1f);
                    UiTheme.Label(new Rect(r.xMax - 36f, r.yMax - 20f, 30f, 16f), avail.ToString(), 13, dim ? UiTheme.Critical : UiTheme.Text, TextAnchor.MiddleRight, true);
                }
                UiTheme.Label(new Rect(r.x + 6f, r.y + 3f, 20f, 16f), InputLabels.Quick(i), 12, UiTheme.Secondary);
                if (ps.ActiveQuick == i) UiTheme.Border(r, UiTheme.Amber, 2f);
            }
        }

        // ---------- main entry ----------

        internal static void Draw(Sandbox player, PlayerSurvival ps, float w, float h, Texture2D pistolIcon)
        {
            if (ps == null || ps.Bag == null || ps.Crafter == null) return;
            if (!active) Reset(ps);
            Event e = Event.current;
            hoverItem = null;
            qty = Mathf.Clamp(qty, 1, Crafter.MaxQuantity);

            UiTheme.Fill(new Rect(0f, 0f, w, h), UiTheme.Scrim);

            BuildRecipes(ps);
            ValidateSelection(ps);
            BuildActions(ps);
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None) HandleKey(ps);

            float panelW = Mathf.Min(1640f, w - 96f);
            float panelH = Mathf.Clamp(h - 172f, 420f, 840f);
            Rect panel = new Rect((w - panelW) / 2f, 36f, panelW, panelH);
            UiTheme.Fill(panel, UiTheme.WithAlpha(UiTheme.Panel, 0.97f));
            UiTheme.Border(panel, UiTheme.Rule);

            if (DrawHeader(ps, panel)) return; // closed by mouse

            Rect content = new Rect(panel.x, panel.y + HeaderH + 1f, panel.width, panel.height - HeaderH - 1f - FooterH);
            if (ps.Tab == UiTab.Crafting) DrawCrafting(player, ps, content);
            else DrawInventory(player, ps, content);
            DrawFooter(ps, panel);

            DrawQuickBar(ps, w, h, pistolIcon);
            DrawTooltip(ps, w, h);
        }

        // ---------- header ----------

        // Returns true when the close control was clicked.
        static bool DrawHeader(PlayerSurvival ps, Rect panel)
        {
            Rect head = new Rect(panel.x, panel.y, panel.width, HeaderH);
            UiTheme.Label(new Rect(head.x + 24f, head.y, 200f, HeaderH), "ZOMBOSLAND", 20, UiTheme.Text, TextAnchor.MiddleLeft, true);
            UiTheme.HRule(head.x, head.yMax, head.width, UiTheme.Rule);

            string[] names = { "ENVANTER", "ÜRETİM", "GÖREVLER", "HARİTA" };
            float x = head.x + 24f + UiTheme.TextWidth("ZOMBOSLAND", 20, true) + 48f;
            for (int i = 0; i < names.Length; i++)
            {
                bool enabled = i < 2;
                float tw = UiTheme.TextWidth(names[i], 16, true);
                float width = Mathf.Max(tw, enabled ? 0f : UiTheme.TextWidth("yakında", 11)) + 36f;
                Rect r = new Rect(x, head.y, width, HeaderH);
                x += width + 6f;
                bool selected = enabled && ((i == 0 && ps.Tab == UiTab.Inventory) || (i == 1 && ps.Tab == UiTab.Crafting));
                if (enabled && !selected && UiTheme.Hover(r)) UiTheme.Fill(new Rect(r.x, r.y + 6f, r.width, r.height - 12f), UiTheme.PanelHover);
                if (enabled)
                {
                    UiTheme.Label(r, names[i], 16, selected ? UiTheme.Text : UiTheme.Secondary, TextAnchor.MiddleCenter, true);
                    if (selected) UiTheme.Fill(new Rect(r.x + 8f, r.yMax - 3f, r.width - 16f, 3f), UiTheme.Amber);
                    if (zone == Zone.Tabs && selected) UiTheme.Border(new Rect(r.x + 2f, r.y + 8f, r.width - 4f, r.height - 16f), new Color(1f, 1f, 1f, 0.9f));
                    if (UiTheme.Click(r) && !selected)
                    {
                        ps.Tab = i == 0 ? UiTab.Inventory : UiTab.Crafting;
                        AfterTabChange(ps, true);
                    }
                }
                else
                {
                    UiTheme.Label(new Rect(r.x, r.y + 9f, r.width, 24f), names[i], 16, UiTheme.Disabled, TextAnchor.MiddleCenter, true);
                    UiTheme.Label(new Rect(r.x, r.y + 32f, r.width, 16f), "yakında", 11, UiTheme.Disabled, TextAnchor.MiddleCenter);
                }
            }

            string close = InputLabels.Key(InputLabels.Menu) + " Kapat";
            float cw = UiTheme.TextWidth(close, 15) + 24f;
            Rect cr = new Rect(head.xMax - cw - 12f, head.y + 10f, cw, HeaderH - 20f);
            if (UiTheme.Hover(cr)) UiTheme.Fill(cr, UiTheme.PanelHover);
            UiTheme.Label(cr, close, 15, UiTheme.Secondary, TextAnchor.MiddleCenter);
            if (UiTheme.Click(cr)) { ps.CloseUi(); return true; }
            return false;
        }

        static void AfterTabChange(PlayerSurvival ps, bool fromMouse)
        {
            if (!fromMouse && zone == Zone.Tabs) { focus = 0; return; }
            zone = ps.Tab == UiTab.Inventory ? Zone.Bag : Zone.Recipes;
            focus = 0;
        }

        static void DrawFooter(PlayerSurvival ps, Rect panel)
        {
            Rect f = new Rect(panel.x, panel.yMax - FooterH, panel.width, FooterH);
            UiTheme.HRule(f.x, f.y, f.width, UiTheme.Rule);
            float x = f.x + 24f;
            if (ps.Tab == UiTab.Crafting && anyBenchRecipe)
            {
                bool near = ps.BenchInRange;
                Rect glyph = new Rect(x, f.y + 12f, 16f, 16f);
                if (near) UiTheme.Tick(glyph, UiTheme.Ok); else UiTheme.Cross(glyph, UiTheme.Secondary);
                UiTheme.Label(new Rect(x + 26f, f.y, 300f, f.height), near ? "Tezgâh erişimde" : "Tezgâh yok", 15, near ? UiTheme.Ok : UiTheme.Secondary);
            }
            string keys = "Ok tuşları: gezin   Enter: seç / uygula   Q / E: sekme";
            UiTheme.Label(new Rect(f.xMax - 700f - 24f, f.y, 700f, f.height), keys, 13, UiTheme.Disabled, TextAnchor.MiddleRight);
        }

        static void DrawTooltip(PlayerSurvival ps, float w, float h)
        {
            if (hoverItem == null) return;
            string text = UiTheme.Name(hoverItem) + (hoverReserved > 0 ? "  (ayrıldı: " + hoverReserved + ")" : "");
            float tw = UiTheme.TextWidth(text, 15) + 20f;
            Vector2 m = Event.current.mousePosition;
            Rect r = new Rect(Mathf.Clamp(m.x + 16f, 4f, w - tw - 4f), Mathf.Clamp(m.y + 20f, 4f, h - 34f), tw, 28f);
            UiTheme.Fill(r, UiTheme.Panel);
            UiTheme.Border(r, UiTheme.Rule);
            UiTheme.Label(r, text, 15, UiTheme.Text, TextAnchor.MiddleCenter);
        }

        // ---------- data prep ----------

        static void BuildRecipes(PlayerSurvival ps)
        {
            recipes.Clear();
            anyBenchRecipe = false;
            RecipeDef[] all = ps.database != null ? ps.database.recipes : null;
            if (all == null) return;
            for (int i = 0; i < all.Length; i++)
            {
                RecipeDef r = all[i];
                if (r == null || !r.available) continue;
                if (r.needsBench) anyBenchRecipe = true;
                if (onlyCraftable && !Craftable(ps, r)) continue;
                recipes.Add(r);
            }
        }

        static void ValidateSelection(PlayerSurvival ps)
        {
            if (selRecipe == null || !selRecipe.available)
            {
                selRecipe = recipes.Count > 0 ? recipes[0] : null;
                qty = 1;
            }
            if (selStorage && ps.OpenStorage == null) selStorage = false;
            if (zone == Zone.Storage && ps.OpenStorage == null) zone = Zone.Bag;
            if (ps.Tab == UiTab.Crafting && (zone == Zone.Bag || zone == Zone.Storage)) zone = Zone.Recipes;
            if (ps.Tab == UiTab.Inventory && zone == Zone.Recipes) zone = Zone.Bag;
        }

        // True when the inputs are in the bag and a needed bench is in range (bag space is checked by Crafter.Check).
        static bool Craftable(PlayerSurvival ps, RecipeDef r)
        {
            if (r.needsBench && !ps.BenchInRange) return false;
            if (r.inputs == null) return true;
            for (int i = 0; i < r.inputs.Length; i++)
            {
                ItemDef d = r.inputs[i].item;
                if (d == null || r.inputs[i].count <= 0) continue;
                if (ps.Bag.Available(d) < r.inputs[i].count) return false;
            }
            return true;
        }

        static ItemStack SelectedStack(PlayerSurvival ps)
        {
            Inventory inv = selStorage ? ps.Storage : ps.Bag;
            if (inv == null || selSlot < 0) return default(ItemStack);
            return inv[selSlot];
        }

        static void BuildActions(PlayerSurvival ps)
        {
            actions.Clear();
            if (ps.Tab != UiTab.Inventory) return;
            ItemStack s = SelectedStack(ps);
            if (s.IsEmpty) return;
            ItemDef d = s.item;
            if (selStorage) { actions.Add(Act.ToBag); return; }
            if (d.kind == ItemKind.Consumable && d.healAmount > 0f) actions.Add(Act.Use);
            if (d.kind == ItemKind.Melee) actions.Add(Act.Equip);
            if (ps.OpenStorage != null) actions.Add(Act.ToStorage);
            if (IsQuickable(d)) { actions.Add(Act.Quick1); actions.Add(Act.Quick2); actions.Add(Act.Quick3); }
        }

        static bool IsQuickable(ItemDef d)
        {
            return d != null && (d.kind == ItemKind.Consumable || d.kind == ItemKind.Throwable || d.kind == ItemKind.Melee);
        }

        static int QuickIndex(Act a) { return a == Act.Quick1 ? 1 : (a == Act.Quick2 ? 2 : 3); }

        static string ActionLabel(PlayerSurvival ps, Act a, ItemDef d)
        {
            switch (a)
            {
                case Act.Use: return "Kullan";
                case Act.Equip: return "Kuşan";
                case Act.ToStorage: return "Depoya taşı";
                case Act.ToBag: return "Çantaya taşı";
                default:
                    int slot = QuickIndex(a);
                    string name = "Hızlı yuva " + InputLabels.Quick(slot);
                    return ps.Quick != null && ps.Quick[slot] == d ? name + " — kaldır" : name;
            }
        }

        static bool ActionEnabled(PlayerSurvival ps, Act a, ItemDef d)
        {
            switch (a)
            {
                case Act.Use:
                case Act.Equip:
                case Act.ToStorage:
                    return ps.Bag.Available(d) > 0; // items reserved by a running craft cannot be used or moved
                case Act.ToBag:
                    return ps.Storage.Available(d) > 0;
                default:
                    return true;
            }
        }

        static void Perform(PlayerSurvival ps, Act a)
        {
            ItemStack s = SelectedStack(ps);
            if (s.IsEmpty) return;
            ItemDef d = s.item;
            switch (a)
            {
                case Act.Use:
                    ps.TryUseBandage(d);
                    break;
                case Act.Equip:
                    {
                        int slot = -1;
                        for (int i = 1; i < PlayerSurvival.QuickSlots; i++) if (ps.Quick[i] == d) { slot = i; break; }
                        if (slot < 0)
                        {
                            for (int i = 1; i < PlayerSurvival.QuickSlots; i++) if (ps.Quick[i] == null) { slot = i; break; }
                            if (slot < 0) slot = 1;
                            ps.AssignQuick(slot, d);
                        }
                        ps.UseQuick(slot);
                        break;
                    }
                case Act.ToStorage:
                    if (ps.OpenStorage != null && ps.Bag.MoveSlotTo(selSlot, ps.Storage) <= 0) Notifier.Show("Taşınamadı", 1.5f);
                    break;
                case Act.ToBag:
                    if (ps.Storage.MoveSlotTo(selSlot, ps.Bag) <= 0) Notifier.Show("Taşınamadı", 1.5f);
                    break;
                default:
                    {
                        int slot = QuickIndex(a);
                        ps.AssignQuick(slot, ps.Quick[slot] == d ? null : d);
                        break;
                    }
            }
        }

        static void SelectRecipe(RecipeDef r)
        {
            if (r != selRecipe) qty = 1;
            selRecipe = r;
        }

        static void CraftOrCancel(PlayerSurvival ps)
        {
            Crafter c = ps.Crafter;
            if (c.Busy) { c.Cancel("Üretim iptal edildi"); return; }
            if (selRecipe == null) return;
            if (c.Check(selRecipe, qty, ps.BenchInRange).Ok) c.Start(selRecipe, qty, Time.time, ps.BenchInRange);
        }

        // ---------- keyboard ----------

        static void HandleKey(PlayerSurvival ps)
        {
            Event e = Event.current;
            KeyCode k = e.keyCode;
            bool inv = ps.Tab == UiTab.Inventory;
            bool enter = k == KeyCode.Return || k == KeyCode.KeypadEnter;
            bool used = true;

            if (k == KeyCode.Q || k == KeyCode.E)
            {
                ps.Tab = inv ? UiTab.Crafting : UiTab.Inventory;
                AfterTabChange(ps, false);
                e.Use();
                return;
            }

            switch (zone)
            {
                case Zone.Tabs:
                    if (k == KeyCode.LeftArrow || k == KeyCode.RightArrow)
                    {
                        ps.Tab = inv ? UiTab.Crafting : UiTab.Inventory;
                        AfterTabChange(ps, false);
                    }
                    else if (k == KeyCode.DownArrow || enter) { zone = inv ? Zone.Bag : Zone.Recipes; focus = 0; }
                    else used = false;
                    break;

                case Zone.Bag:
                case Zone.Storage:
                    used = GridKey(ps, k, enter);
                    break;

                case Zone.Recipes:
                    if (k == KeyCode.UpArrow) { if (focus > 0) focus--; else zone = Zone.Tabs; }
                    else if (k == KeyCode.DownArrow) { if (focus < recipes.Count - 1) focus++; }
                    else if (k == KeyCode.RightArrow) { zone = Zone.Actions; focus = 1; }
                    else if (enter)
                    {
                        if (focus >= 0 && focus < recipes.Count) { SelectRecipe(recipes[focus]); zone = Zone.Actions; focus = 1; }
                    }
                    else used = false;
                    break;

                case Zone.Actions:
                    if (inv)
                    {
                        if (actions.Count == 0) { zone = selStorage ? Zone.Storage : Zone.Bag; focus = Mathf.Max(0, selSlot); break; }
                        focus = Mathf.Clamp(focus, 0, actions.Count - 1);
                        if (k == KeyCode.UpArrow) focus = Mathf.Max(0, focus - 1);
                        else if (k == KeyCode.DownArrow) focus = Mathf.Min(actions.Count - 1, focus + 1);
                        else if (k == KeyCode.LeftArrow) { zone = selStorage ? Zone.Storage : Zone.Bag; focus = Mathf.Max(0, selSlot); }
                        else if (enter)
                        {
                            ItemStack s = SelectedStack(ps);
                            if (!s.IsEmpty && ActionEnabled(ps, actions[focus], s.item)) Perform(ps, actions[focus]);
                        }
                        else used = false;
                    }
                    else
                    {
                        if (k == KeyCode.UpArrow) focus = 0;
                        else if (k == KeyCode.DownArrow) focus = 1;
                        else if (k == KeyCode.LeftArrow)
                        {
                            if (focus == 0 && !ps.Crafter.Busy) qty = Mathf.Max(1, qty - 1);
                            else if (focus != 0) { zone = Zone.Recipes; focus = Mathf.Max(0, recipes.IndexOf(selRecipe)); }
                        }
                        else if (k == KeyCode.RightArrow) { if (focus == 0 && !ps.Crafter.Busy) qty = Mathf.Min(Crafter.MaxQuantity, qty + 1); }
                        else if (enter) { if (focus == 1) CraftOrCancel(ps); }
                        else used = false;
                    }
                    break;
            }
            if (used) e.Use();
        }

        static bool GridKey(PlayerSurvival ps, KeyCode k, bool enter)
        {
            bool storage = zone == Zone.Storage;
            Inventory inv = storage ? ps.Storage : ps.Bag;
            int cap = inv.Capacity;
            if (cap <= 0) return false;
            focus = Mathf.Clamp(focus, 0, cap - 1);
            int row = focus / Cols, col = focus % Cols;
            if (k == KeyCode.LeftArrow)
            {
                if (col > 0) focus--;
                else if (storage) { zone = Zone.Bag; focus = Mathf.Min(ps.Bag.Capacity - 1, row * Cols + Cols - 1); }
            }
            else if (k == KeyCode.RightArrow)
            {
                if (col < Cols - 1 && focus + 1 < cap) focus++;
                else if (!storage && ps.OpenStorage != null) { zone = Zone.Storage; focus = Mathf.Min(ps.Storage.Capacity - 1, row * Cols); }
                else if (actions.Count > 0) { zone = Zone.Actions; focus = 0; }
            }
            else if (k == KeyCode.UpArrow)
            {
                if (row > 0) focus -= Cols; else zone = Zone.Tabs;
            }
            else if (k == KeyCode.DownArrow)
            {
                if (focus + Cols < cap) focus += Cols;
            }
            else if (enter)
            {
                if (selStorage == storage && selSlot == focus && actions.Count > 0) { zone = Zone.Actions; focus = 0; }
                else { selStorage = storage; selSlot = focus; }
            }
            else return false;
            return true;
        }

        // ---------- shared pieces ----------

        // 6-column slot grid inside `area`. Returns the y below the grid.
        static float DrawGrid(PlayerSurvival ps, Inventory inv, Rect area, bool storage, bool interactive, string title)
        {
            UiTheme.Label(new Rect(area.x, area.y, area.width, 22f), title, 13, UiTheme.Secondary, TextAnchor.MiddleLeft, true);
            const float gap = 6f;
            float top = area.y + 30f;
            float footer = 52f;
            float byWidth = Mathf.Floor((area.width - (Cols - 1) * gap) / Cols);
            int rows = Mathf.Max(1, (inv.Capacity + Cols - 1) / Cols);
            float byHeight = Mathf.Floor((area.height - 30f - footer - (rows - 1) * gap) / rows);
            float cell = Mathf.Max(24f, Mathf.Min(96f, Mathf.Min(byWidth, byHeight)));
            float gridW = Cols * cell + (Cols - 1) * gap;
            float gx = area.x + (area.width - gridW) / 2f;
            for (int i = 0; i < inv.Capacity; i++)
            {
                int row = i / Cols, col = i % Cols;
                Rect r = new Rect(gx + col * (cell + gap), top + row * (cell + gap), cell, cell);
                ItemStack s = inv[i];
                bool hover = interactive && UiTheme.Hover(r);
                bool selected = interactive && selStorage == storage && selSlot == i;
                UiTheme.Fill(r, hover ? UiTheme.PanelHover : UiTheme.PanelLight);
                UiTheme.Border(r, UiTheme.Rule);
                if (!s.IsEmpty)
                {
                    float inset = Mathf.Max(6f, cell * 0.14f);
                    UiTheme.ItemIcon(new Rect(r.x + inset, r.y + inset, cell - 2f * inset, cell - 2f * inset), s.item);
                    if (s.count > 1) UiTheme.Label(new Rect(r.xMax - 40f, r.yMax - 20f, 36f, 16f), s.count.ToString(), 13, UiTheme.Text, TextAnchor.MiddleRight, true);
                    if (UiTheme.Hover(r)) { hoverItem = s.item; hoverReserved = inv.Reserved(s.item); }
                }
                if (selected)
                {
                    UiTheme.Fill(r, UiTheme.WithAlpha(UiTheme.Amber, 0.16f));
                    UiTheme.Border(r, UiTheme.Amber, 2f);
                }
                bool focused = interactive && ((storage && zone == Zone.Storage) || (!storage && zone == Zone.Bag)) && focus == i;
                if (focused) UiTheme.Border(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(1f, 1f, 1f, 0.95f));
                if (interactive && UiTheme.Click(r))
                {
                    selStorage = storage;
                    selSlot = i;
                    zone = storage ? Zone.Storage : Zone.Bag;
                    focus = i;
                }
            }
            return top + rows * (cell + gap) + 6f;
        }

        static void VRule(float x, float y, float height) { UiTheme.Fill(new Rect(x, y, 1f, height), UiTheme.Rule); }

        // ---------- inventory tab ----------

        static void DrawInventory(Sandbox player, PlayerSurvival ps, Rect c)
        {
            bool hasStorage = ps.OpenStorage != null;
            float bagW = c.width * (hasStorage ? 0.34f : 0.36f);
            float storeW = hasStorage ? c.width * 0.34f : 0f;
            Rect bag = new Rect(c.x + Pad, c.y + Pad, bagW - 2f * Pad, c.height - 2f * Pad);
            float y = DrawGrid(ps, ps.Bag, bag, false, true, "SIRT ÇANTASI");
            DrawBagFooter(player, ps, bag.x, y, bag.width);
            VRule(c.x + bagW, c.y, c.height);
            if (hasStorage)
            {
                Rect st = new Rect(c.x + bagW + Pad, c.y + Pad, storeW - 2f * Pad, c.height - 2f * Pad);
                float sy = DrawGrid(ps, ps.Storage, st, true, true, "DEPO");
                UiTheme.Label(new Rect(st.x, sy, st.width, 22f), ps.Storage.UsedSlots + " / " + ps.Storage.Capacity + " yuva", 15, UiTheme.Text);
                VRule(c.x + bagW + storeW, c.y, c.height);
            }
            Rect detail = new Rect(c.x + bagW + storeW, c.y, c.width - bagW - storeW, c.height);
            DrawItemDetail(ps, detail);
        }

        static void DrawBagFooter(Sandbox player, PlayerSurvival ps, float x, float y, float width)
        {
            UiTheme.Label(new Rect(x, y, width, 22f), ps.Bag.UsedSlots + " / " + ps.Bag.Capacity + " yuva", 15, UiTheme.Text);
            int reserve = player != null ? player.Weapon.Reserve : 0;
            UiTheme.Label(new Rect(x, y + 22f, width, 20f), "9 mm: " + reserve, 14, UiTheme.Secondary);
        }

        static string KindName(ItemKind k)
        {
            switch (k)
            {
                case ItemKind.Consumable: return "Tüketilir";
                case ItemKind.Throwable: return "Fırlatılabilir";
                case ItemKind.Melee: return "Yakın dövüş";
                case ItemKind.Kit: return "Kit";
                default: return "Malzeme";
            }
        }

        static void DrawItemDetail(PlayerSurvival ps, Rect col)
        {
            float width = Mathf.Min(col.width - 2f * Pad, 560f);
            float x = col.x + Pad, y = col.y + Pad;
            ItemStack s = SelectedStack(ps);
            if (s.IsEmpty)
            {
                UiTheme.Label(new Rect(x, y, width, 30f), "Bir eşya seç", 20, UiTheme.Secondary);
                UiTheme.WrapLabel(new Rect(x, y + 36f, width, 60f), "Eşyanın adı, açıklaması ve işlemleri burada görünür.", 15, UiTheme.Disabled);
                return;
            }
            ItemDef d = s.item;
            Inventory inv = selStorage ? ps.Storage : ps.Bag;
            UiTheme.Label(new Rect(x, y, width, 34f), UiTheme.Name(d), 26, UiTheme.Text, TextAnchor.MiddleLeft, true);
            y += 42f;
            Rect frame = new Rect(x, y, 96f, 96f);
            UiTheme.Fill(frame, UiTheme.PanelLight);
            UiTheme.Border(frame, UiTheme.Rule);
            UiTheme.ItemIcon(new Rect(frame.x + 10f, frame.y + 10f, 76f, 76f), d);
            float tx = x + 112f, tw = width - 112f;
            UiTheme.Label(new Rect(tx, y, tw, 22f), KindName(d.kind), 14, UiTheme.Secondary);
            UiTheme.Label(new Rect(tx, y + 24f, tw, 24f), "Adet: " + s.count, 17, UiTheme.Text);
            int reserved = inv.Reserved(d);
            if (reserved > 0) UiTheme.Label(new Rect(tx, y + 48f, tw, 22f), "Ayrıldı: " + reserved, 14, UiTheme.Amber);
            else if (d.healAmount > 0f) UiTheme.Label(new Rect(tx, y + 48f, tw, 22f), "Can +" + Mathf.RoundToInt(d.healAmount), 14, UiTheme.Ok);
            y += 112f;
            UiTheme.WrapLabel(new Rect(x, y, width, 80f), d.description, 15, UiTheme.Secondary);
            y += 92f;
            UiTheme.HRule(x, y, width, UiTheme.Rule);
            y += 14f;
            float bw = Mathf.Min(width, 340f);
            for (int i = 0; i < actions.Count; i++)
            {
                Rect br = new Rect(x, y, bw, 44f);
                bool enabled = ActionEnabled(ps, actions[i], d);
                bool primary = i == 0 && actions[i] != Act.Quick1 && actions[i] != Act.Quick2 && actions[i] != Act.Quick3;
                bool clicked = UiTheme.Button(br, ActionLabel(ps, actions[i], d), primary ? UiTheme.ButtonKind.Primary : UiTheme.ButtonKind.Secondary, enabled, 16);
                if (zone == Zone.Actions && focus == i) UiTheme.Border(new Rect(br.x - 2f, br.y - 2f, br.width + 4f, br.height + 4f), new Color(1f, 1f, 1f, 0.95f));
                y += 52f;
                if (clicked)
                {
                    Perform(ps, actions[i]);
                    break; // the list may have changed (slot emptied)
                }
            }
        }

        // ---------- crafting tab ----------

        static void DrawCrafting(Sandbox player, PlayerSurvival ps, Rect c)
        {
            float w1 = c.width * 0.36f, w2 = c.width * 0.28f;
            Rect bag = new Rect(c.x + Pad, c.y + Pad, w1 - 2f * Pad, c.height - 2f * Pad);
            float y = DrawGrid(ps, ps.Bag, bag, false, false, "SIRT ÇANTASI");
            DrawBagFooter(player, ps, bag.x, y, bag.width);
            VRule(c.x + w1, c.y, c.height);
            DrawRecipeList(ps, new Rect(c.x + w1, c.y, w2, c.height));
            VRule(c.x + w1 + w2, c.y, c.height);
            DrawCraftDetail(ps, new Rect(c.x + w1 + w2, c.y, c.width - w1 - w2, c.height));
        }

        static void DrawRecipeList(PlayerSurvival ps, Rect col)
        {
            float x = col.x + 16f, width = col.width - 32f, y = col.y + Pad;
            UiTheme.Label(new Rect(x, y, width, 22f), "TARİFLER", 13, UiTheme.Secondary, TextAnchor.MiddleLeft, true);
            y += 28f;
            float half = (width - 6f) / 2f;
            DrawFilter(new Rect(x, y, half, 30f), "Tümü", !onlyCraftable, false);
            DrawFilter(new Rect(x + half + 6f, y, half, 30f), "Üretilebilir", onlyCraftable, true);
            y += 40f;

            const float rowH = 56f, gap = 6f;
            float listH = col.yMax - y - 12f;
            int visible = Mathf.Max(1, Mathf.FloorToInt((listH + gap) / (rowH + gap)));
            Event e = Event.current;
            Rect listRect = new Rect(x, y, width, listH);
            if (e.type == EventType.ScrollWheel && listRect.Contains(e.mousePosition))
            {
                scroll += e.delta.y > 0f ? 1 : -1;
                e.Use();
            }
            if (zone == Zone.Recipes)
            {
                focus = Mathf.Clamp(focus, 0, Mathf.Max(0, recipes.Count - 1));
                if (focus < scroll) scroll = focus;
                if (focus >= scroll + visible) scroll = focus - visible + 1;
            }
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, recipes.Count - visible));

            if (recipes.Count == 0)
            {
                UiTheme.Label(new Rect(x, y, width, 28f), onlyCraftable ? "Şimdilik üretilebilir tarif yok" : "Tarif yok", 15, UiTheme.Secondary);
                return;
            }
            for (int n = 0; n < visible && scroll + n < recipes.Count; n++)
            {
                int index = scroll + n;
                RecipeDef r = recipes[index];
                Rect row = new Rect(x, y + n * (rowH + gap), width, rowH);
                bool selected = r == selRecipe;
                bool hover = UiTheme.Hover(row);
                UiTheme.Fill(row, hover ? UiTheme.PanelHover : UiTheme.PanelLight);
                UiTheme.Border(row, UiTheme.Rule);
                if (selected)
                {
                    UiTheme.Fill(row, UiTheme.WithAlpha(UiTheme.Amber, 0.16f));
                    UiTheme.Border(row, UiTheme.Amber, 2f);
                }
                UiTheme.ItemIcon(new Rect(row.x + 8f, row.y + 8f, 40f, 40f), r.output);
                string name = string.IsNullOrEmpty(r.displayName) ? r.name : r.displayName;
                UiTheme.Label(new Rect(row.x + 58f, row.y + 6f, row.width - 58f - 90f, 24f), name, 17, UiTheme.Text, TextAnchor.MiddleLeft, true);
                UiTheme.Label(new Rect(row.x + 58f, row.y + 30f, row.width - 58f - 90f, 20f), r.needsBench ? "Tezgâh" : "Elde", 12, UiTheme.Secondary);
                bool ready = Craftable(ps, r);
                Rect glyph = new Rect(row.xMax - 84f, row.y + 20f, 16f, 16f);
                if (ready) UiTheme.Tick(glyph, UiTheme.Ok); else UiTheme.Cross(glyph, UiTheme.Secondary);
                UiTheme.Label(new Rect(row.xMax - 62f, row.y, 56f, row.height), ready ? "Hazır" : "Eksik", 13, ready ? UiTheme.Ok : UiTheme.Secondary, TextAnchor.MiddleLeft);
                if (zone == Zone.Recipes && focus == index) UiTheme.Border(new Rect(row.x - 2f, row.y - 2f, row.width + 4f, row.height + 4f), new Color(1f, 1f, 1f, 0.95f));
                if (UiTheme.Click(row))
                {
                    SelectRecipe(r);
                    zone = Zone.Recipes;
                    focus = index;
                }
            }
        }

        static void DrawFilter(Rect r, string label, bool on, bool craftableFilter)
        {
            UiTheme.Fill(r, UiTheme.Hover(r) ? UiTheme.PanelHover : UiTheme.PanelLight);
            UiTheme.Border(r, on ? UiTheme.Amber : UiTheme.Rule);
            UiTheme.Label(r, label, 14, on ? UiTheme.Text : UiTheme.Secondary, TextAnchor.MiddleCenter, on);
            if (UiTheme.Click(r)) { onlyCraftable = craftableFilter; scroll = 0; }
        }

        static string Secs(float s)
        {
            return (Mathf.Abs(s - Mathf.Round(s)) < 0.05f ? Mathf.RoundToInt(s).ToString() : s.ToString("0.#", CultureInfo.InvariantCulture)) + " sn";
        }

        static void DrawCraftDetail(PlayerSurvival ps, Rect col)
        {
            float x = col.x + Pad, width = col.width - 2f * Pad, y = col.y + Pad;
            RecipeDef r = selRecipe;
            Crafter c = ps.Crafter;
            if (r == null)
            {
                UiTheme.Label(new Rect(x, y, width, 30f), "Tarif seçilmedi", 20, UiTheme.Secondary);
                return;
            }
            string title = string.IsNullOrEmpty(r.displayName) ? r.name : r.displayName;
            UiTheme.Label(new Rect(x, y, width, 34f), title, 26, UiTheme.Text, TextAnchor.MiddleLeft, true);
            y += 42f;

            Rect frame = new Rect(x, y, 96f, 96f);
            UiTheme.Fill(frame, UiTheme.PanelLight);
            UiTheme.Border(frame, UiTheme.Rule);
            UiTheme.ItemIcon(new Rect(frame.x + 10f, frame.y + 10f, 76f, 76f), r.output);
            if (r.outputCount > 1) UiTheme.Label(new Rect(frame.xMax - 40f, frame.yMax - 22f, 36f, 18f), "x" + r.outputCount, 13, UiTheme.Text, TextAnchor.MiddleRight, true);
            UiTheme.WrapLabel(new Rect(x + 112f, y, width - 112f, 96f), r.description, 15, UiTheme.Secondary);
            y += 112f;

            UiTheme.Label(new Rect(x, y, width, 18f), "MALZEMELER", 12, UiTheme.Secondary, TextAnchor.MiddleLeft, true);
            y += 22f;
            UiTheme.HRule(x, y, width, UiTheme.Rule);
            y += 8f;
            bool busy = c.Busy;
            if (r.inputs != null)
            {
                for (int i = 0; i < r.inputs.Length; i++)
                {
                    ItemDef d = r.inputs[i].item;
                    if (d == null || r.inputs[i].count <= 0) continue;
                    int need = r.inputs[i].count * qty;
                    Rect glyph = new Rect(x, y + 5f, 16f, 16f);
                    string name = UiTheme.Name(d);
                    if (busy)
                    {
                        UiTheme.Tick(glyph, UiTheme.Ok);
                        UiTheme.Label(new Rect(x + 28f, y, width - 28f, 26f), name, 16, UiTheme.Text);
                        UiTheme.Label(new Rect(x, y, width, 26f), "Ayrıldı: " + ps.Bag.Reserved(d), 15, UiTheme.Secondary, TextAnchor.MiddleRight);
                    }
                    else
                    {
                        int have = ps.Bag.Available(d);
                        if (have >= need)
                        {
                            UiTheme.Tick(glyph, UiTheme.Ok);
                            UiTheme.Label(new Rect(x + 28f, y, width - 28f, 26f), name, 16, UiTheme.Text);
                            UiTheme.Label(new Rect(x, y, width, 26f), have + " / " + need, 16, UiTheme.Text, TextAnchor.MiddleRight, true);
                        }
                        else
                        {
                            UiTheme.Cross(glyph, UiTheme.Critical);
                            UiTheme.Label(new Rect(x + 28f, y, width - 28f, 26f), name + ": " + have + " / " + need + " — " + (need - have) + " eksik", 16, UiTheme.Critical);
                        }
                    }
                    y += 28f;
                }
            }
            y += 4f;
            UiTheme.HRule(x, y, width, UiTheme.Rule);
            y += 8f;

            // Where it is made, and how long it takes.
            Rect g = new Rect(x, y + 5f, 16f, 16f);
            if (r.needsBench)
            {
                if (ps.BenchInRange) UiTheme.Tick(g, UiTheme.Ok); else UiTheme.Cross(g, UiTheme.Secondary);
                UiTheme.Label(new Rect(x + 28f, y, width - 28f, 26f), "Tezgâh gerekli", 16, UiTheme.Text);
            }
            else UiTheme.Label(new Rect(x, y, width, 26f), "Elde üretim", 16, UiTheme.Text);
            y += 28f;
            UiTheme.Label(new Rect(x, y, 100f, 26f), "Süre", 16, UiTheme.Secondary);
            string time = Secs(r.seconds) + (qty > 1 ? "  (toplam " + Secs(r.seconds * qty) + ")" : "");
            UiTheme.Label(new Rect(x + 100f, y, width - 100f, 26f), time, 16, UiTheme.Text);
            y += 38f;

            // Quantity control.
            UiTheme.Label(new Rect(x, y, 100f, 36f), "Adet", 16, UiTheme.Secondary);
            Rect minus = new Rect(x + 100f, y, 36f, 36f), value = new Rect(minus.xMax + 4f, y, 64f, 36f), plus = new Rect(value.xMax + 4f, y, 36f, 36f);
            bool canAdjust = !busy;
            if (UiTheme.Button(minus, "−", UiTheme.ButtonKind.Secondary, canAdjust && qty > 1, 20)) qty = Mathf.Max(1, qty - 1);
            UiTheme.Fill(value, UiTheme.PanelLight);
            UiTheme.Label(value, qty.ToString(), 18, UiTheme.Text, TextAnchor.MiddleCenter, true);
            if (UiTheme.Button(plus, "+", UiTheme.ButtonKind.Secondary, canAdjust && qty < Crafter.MaxQuantity, 20)) qty = Mathf.Min(Crafter.MaxQuantity, qty + 1);
            if (zone == Zone.Actions && focus == 0) UiTheme.Border(new Rect(minus.x - 2f, minus.y - 2f, plus.xMax - minus.x + 4f, 40f), new Color(1f, 1f, 1f, 0.95f));
            y += 52f;

            // ÜRET (disabled while busy or blocked) with the reason underneath.
            CraftCheck check = c.Check(r, qty, ps.BenchInRange);
            bool canStart = !busy && check.Ok;
            Rect craft = new Rect(x, y, width, 52f);
            if (UiTheme.Button(craft, "ÜRET", UiTheme.ButtonKind.Primary, canStart, 20)) CraftOrCancel(ps);
            if (zone == Zone.Actions && focus == 1 && !busy) UiTheme.Border(new Rect(craft.x - 2f, craft.y - 2f, craft.width + 4f, craft.height + 4f), new Color(1f, 1f, 1f, 0.95f));
            y += 58f;
            if (busy)
            {
                float done = c.Total > 0 ? (c.Total - c.Remaining + c.UnitProgress(Time.time)) / c.Total : 0f;
                int current = Mathf.Clamp(c.Total - c.Remaining + 1, 1, Mathf.Max(1, c.Total));
                UiTheme.Label(new Rect(x, y, width, 24f), "Üretiliyor… " + current + " / " + c.Total, 16, UiTheme.Text, TextAnchor.MiddleLeft, true);
                y += 28f;
                UiTheme.Fill(new Rect(x, y, width, 8f), UiTheme.Rule);
                UiTheme.Fill(new Rect(x, y, width * Mathf.Clamp01(done), 8f), UiTheme.Amber);
                y += 18f;
                Rect cancel = new Rect(x, y, Mathf.Min(width, 200f), 44f);
                if (UiTheme.Button(cancel, "İPTAL", UiTheme.ButtonKind.Secondary, true, 16)) CraftOrCancel(ps);
                if (zone == Zone.Actions && focus == 1) UiTheme.Border(new Rect(cancel.x - 2f, cancel.y - 2f, cancel.width + 4f, cancel.height + 4f), new Color(1f, 1f, 1f, 0.95f));
                y += 54f;
            }
            else if (!check.Ok && !string.IsNullOrEmpty(check.message))
            {
                UiTheme.Label(new Rect(x, y, width, 24f), check.message, 15, UiTheme.Critical);
                y += 28f;
            }
            UiTheme.WrapLabel(new Rect(x, y, width, 40f), "Malzemeler üretim tamamlanınca harcanır.", 13, UiTheme.Secondary);
        }
    }
}
