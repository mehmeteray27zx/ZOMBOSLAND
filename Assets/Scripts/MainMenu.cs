using System;
using UnityEngine;

namespace Zombosland
{
    // Main menu: shown once per app run when a person starts the game (Ilk Ikmal or CombatSandbox), on top of the live (frozen) scene.
    // Ilk Ikmal offers continue / new game; CombatSandbox has no save, so it only offers start.
    // While it is up the game is paused exactly like the Esc menu (Sandbox.SetPaused), so nothing starts running underneath;
    // closing it resumes the game and starts the playtest clock.
    public static class MainMenu
    {
        enum Page { Root, Settings }

        const float ButtonW = 400f, ButtonH = 56f, Gap = 8f;

        static bool shown;      // already shown during this app run
        static bool requested;  // "Ana menüye dön" asked for it again
        static int blocked;     // 0 = not checked, 1 = may show, 2 = never in this run
        static Page page;
        static bool askNewGame;
        static bool hasSave;
        static bool campaign;   // Ilk Ikmal (quest + save); false in CombatSandbox
        static string versionText;

        // True from the moment the menu opens until it closes.
        public static bool Active { get; private set; }

        // False in scripted / batch runs, where the menu never shows (the pause menu hides "Ana menüye dön" then).
        public static bool Available { get { return !Blocked; } }

        // Editor "enter play mode without domain reload" keeps statics: start every run clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            shown = false;
            requested = false;
            blocked = 0;
            Active = false;
            askNewGame = false;
            page = Page.Root;
        }

        // QA / perf runs, --skip-menu and batch mode never get the menu.
        static bool Blocked
        {
            get
            {
                if (blocked == 0)
                {
                    blocked = Application.isBatchMode ? 2 : 1;
                    foreach (string a in Environment.GetCommandLineArgs())
                        if (a.StartsWith("--qa", StringComparison.Ordinal) || a.StartsWith("--perf", StringComparison.Ordinal) || a == "--skip-menu") { blocked = 2; break; }
                }
                return blocked == 2;
            }
        }

        // Called by Sandbox.Start. True when the menu opens (the caller pauses the game).
        internal static bool TryBegin()
        {
            if (Active || Blocked) return false;
            if (shown && !requested) return false; // scene reloads do not bring it back
            shown = true;
            requested = false;
            Active = true;
            page = Page.Root;
            askNewGame = false;
            SettingsPanel.Reset();
            GUIUtility.hotControl = 0;
            if (QuestFuse.Instance != null) PlaytestLog.ResetSession(); // Ilk Ikmal: the playtest clock stays stopped until the menu closes
            return true;
        }

        // "Ana menüye dön": the next scene load shows the menu again.
        internal static void RequestReturn() { requested = true; }

        // Esc: back out of settings or a dialog; nothing at the root.
        internal static void OnEscape()
        {
            if (askNewGame) askNewGame = false;
            else if (page == Page.Settings)
            {
                page = Page.Root;
                GameSettings.Save();
            }
        }

        // Closes the menu and lets the game run: the playtest clock, hint cards and quest timers start from here.
        static void Close(Sandbox player)
        {
            Active = false;
            askNewGame = false;
            GUIUtility.hotControl = 0;
            QuestFuse q = QuestFuse.Instance; // playtest logging exists only in Ilk Ikmal
            if (q != null)
            {
                PlaytestLog.BeginLevel();
                if (PlaytestLog.QuestTimes.Count == 0) PlaytestLog.RecordQuest(q.State.ToString(), 0f);
            }
            player.ResumeFromMenu();
        }

        public static void Draw(Sandbox player, float w, float h)
        {
            // The button set must be the same in the Layout and Repaint passes, so the scene and save state are read once per layout.
            if (Event.current != null && Event.current.type == EventType.Layout)
            {
                campaign = QuestFuse.Instance != null;
                hasSave = campaign && SaveSystem.HasSave;
            }
            UiTheme.Fill(new Rect(0f, 0f, w, h), UiTheme.MenuScrim);
            if (page == Page.Settings)
            {
                if (SettingsPanel.Draw(w, h, true))
                {
                    page = Page.Root;
                    GameSettings.Save();
                }
                return;
            }

            bool open = !askNewGame;
            float left = Mathf.Max(96f, Mathf.Round(w * 0.1f));
            float titleY = Mathf.Round(h * 0.2f);
            UiTheme.Label(new Rect(left, titleY, 1100f, 128f), "ZOMBOSLAND", 112, UiTheme.Text, TextAnchor.MiddleLeft, true);
            UiTheme.Fill(new Rect(left + 4f, titleY + 136f, 160f, 4f), UiTheme.Amber);
            UiTheme.Label(new Rect(left + 4f, titleY + 148f, 600f, 28f), campaign ? "İLK İKMAL" : "ATIŞ ALANI", 20, UiTheme.Secondary, TextAnchor.MiddleLeft);

            float y = titleY + 192f;
            if (campaign)
            {
                if (hasSave)
                {
                    if (UiTheme.MenuButton(new Rect(left, y, ButtonW, ButtonH), "Devam et", UiTheme.MenuButtonKind.Primary, open, 22)) Close(player);
                    y += ButtonH + Gap;
                }
                UiTheme.MenuButtonKind newKind = hasSave ? UiTheme.MenuButtonKind.Normal : UiTheme.MenuButtonKind.Primary;
                if (UiTheme.MenuButton(new Rect(left, y, ButtonW, ButtonH), "Yeni oyun", newKind, open, 22))
                {
                    if (hasSave) askNewGame = true;
                    else Close(player);
                }
            }
            else if (UiTheme.MenuButton(new Rect(left, y, ButtonW, ButtonH), "Başla", UiTheme.MenuButtonKind.Primary, open, 22)) Close(player);
            y += ButtonH + Gap;
            if (UiTheme.MenuButton(new Rect(left, y, ButtonW, ButtonH), "Ayarlar", UiTheme.MenuButtonKind.Normal, open, 22))
            {
                SettingsPanel.Reset();
                page = Page.Settings;
            }
            y += ButtonH + Gap;
            if (UiTheme.MenuButton(new Rect(left, y, ButtonW, ButtonH), "Çıkış", UiTheme.MenuButtonKind.Normal, open, 22)) GameQuit.Request();

            if (versionText == null) versionText = "v" + Application.version;
            UiTheme.Label(new Rect(w - 24f - 240f, h - 24f - 24f, 240f, 24f), versionText, 16, UiTheme.Secondary, TextAnchor.MiddleRight);

            if (askNewGame)
            {
                int result = UiTheme.Confirm(w, h, "YENİ OYUN", "Kayıt silinecek. Emin misin?", "Evet", "Vazgeç");
                if (result == 1)
                {
                    askNewGame = false;
                    Active = false;
                    GUIUtility.hotControl = 0;
                    SaveSystem.Delete();
                    player.RestartLevel(); // same path as the pause menu; the menu does not come back after the reload
                }
                else if (result == 2) askNewGame = false;
            }
        }
    }
}
