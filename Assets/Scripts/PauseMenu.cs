using UnityEngine;

namespace Zombosland
{
    // Esc menu: a root page (resume, settings, save / load, new game, main menu, quit) and the shared settings page.
    // Destructive entries ask first. Draw is called by the HUD while Sandbox.MenuOpen and no main menu is up.
    public static class PauseMenu
    {
        enum Page { Root, Settings }
        enum Ask { None, NewGame, MainMenu, Quit }

        const float PanelW = 480f, ButtonH = 48f, Gap = 8f;
        const string EscHint = InputLabels.Menu + " ile devam et";

        static Page page;
        static Ask ask;
        static bool hasSave;

        // Called by Sandbox every time the menu opens: always starts at the root page.
        internal static void Reset()
        {
            page = Page.Root;
            ask = Ask.None;
            SettingsPanel.Reset();
            GUIUtility.hotControl = 0;
        }

        // Esc inside the menu. True when it was used here (closing a dialog or the settings page); false at the root,
        // where the caller resumes the game.
        internal static bool OnEscape()
        {
            if (ask != Ask.None) { ask = Ask.None; return true; }
            if (page == Page.Settings)
            {
                page = Page.Root;
                GameSettings.Save();
                return true;
            }
            return false;
        }

        public static void Draw(Sandbox player, float width, float height)
        {
            // The button set must be the same in the Layout and Repaint passes, so the save state is read once per layout.
            if (Event.current != null && Event.current.type == EventType.Layout) hasSave = SaveSystem.HasSave;
            UiTheme.Fill(new Rect(0f, 0f, width, height), UiTheme.Scrim);
            bool open = ask == Ask.None;
            if (page == Page.Settings)
            {
                if (SettingsPanel.Draw(width, height, open))
                {
                    page = Page.Root;
                    GameSettings.Save();
                }
            }
            else DrawRoot(player, width, height, open);
            if (ask != Ask.None) DrawAsk(player, width, height);
        }

        static void DrawRoot(Sandbox player, float width, float height, bool open)
        {
            bool saveEntries = QuestFuse.Instance != null; // Ilk Ikmal only: save / load / new game
            bool loadEntry = saveEntries && hasSave;
            bool menuEntry = MainMenu.Available;           // both scenes; hidden in scripted runs, where there is no main menu
            // resume, settings, then [save, [load], new game] or [restart], then [main menu], then quit
            int count = 2 + (saveEntries ? 2 + (loadEntry ? 1 : 0) : 1) + (menuEntry ? 1 : 0) + 1;
            float panelH = 168f + count * (ButtonH + Gap) + (saveEntries ? 16f : 0f);
            Rect panel = new Rect(Mathf.Round((width - PanelW) / 2f), Mathf.Round((height - panelH) / 2f), PanelW, panelH);
            UiTheme.Fill(panel, UiTheme.WithAlpha(UiTheme.Panel, 0.98f));
            UiTheme.Border(panel, UiTheme.Rule);
            UiTheme.Label(new Rect(panel.x + 32f, panel.y + 32f, PanelW - 64f, 40f), "DURAKLATILDI", 28, UiTheme.Text, TextAnchor.MiddleLeft, true);
            UiTheme.HRule(panel.x + 32f, panel.y + 88f, PanelW - 64f, UiTheme.Rule);

            float x = panel.x + 32f, w = PanelW - 64f, y = panel.y + 104f;
            if (UiTheme.MenuButton(new Rect(x, y, w, ButtonH), "Devam et", UiTheme.MenuButtonKind.Primary, open, 20)) player.ResumeFromMenu();
            y += ButtonH + Gap;
            if (UiTheme.MenuButton(new Rect(x, y, w, ButtonH), "Ayarlar", UiTheme.MenuButtonKind.Normal, open, 20))
            {
                SettingsPanel.Reset();
                page = Page.Settings;
            }
            y += ButtonH + Gap;
            if (saveEntries)
            {
                if (UiTheme.MenuButton(new Rect(x, y, w, ButtonH), "Kaydet", UiTheme.MenuButtonKind.Normal, open, 20))
                    Notifier.Show(SaveSystem.Save("manual") ? "Oyun kaydedildi" : "Kayıt alınamadı", 2.5f);
                y += ButtonH + Gap;
                if (loadEntry)
                {
                    if (UiTheme.MenuButton(new Rect(x, y, w, ButtonH), "Son kaydı yükle", UiTheme.MenuButtonKind.Normal, open, 20)) player.Restart();
                    y += ButtonH + Gap;
                }
                if (UiTheme.MenuButton(new Rect(x, y, w, ButtonH), "Yeni oyun", UiTheme.MenuButtonKind.Normal, open, 20)) ask = Ask.NewGame;
                y += ButtonH + Gap + 16f;
            }
            else
            {
                if (UiTheme.MenuButton(new Rect(x, y, w, ButtonH), "Yeniden başlat", UiTheme.MenuButtonKind.Normal, open, 20)) player.Restart();
                y += ButtonH + Gap;
            }
            if (menuEntry)
            {
                if (UiTheme.MenuButton(new Rect(x, y, w, ButtonH), "Ana menüye dön", UiTheme.MenuButtonKind.Normal, open, 20)) ask = Ask.MainMenu;
                y += ButtonH + Gap;
            }
            if (UiTheme.MenuButton(new Rect(x, y, w, ButtonH), "Masaüstüne çık", UiTheme.MenuButtonKind.Normal, open, 20)) ask = Ask.Quit;
            UiTheme.Label(new Rect(panel.x, panel.yMax - 48f, PanelW, 24f), EscHint, 16, UiTheme.Secondary, TextAnchor.MiddleCenter);
        }

        static void DrawAsk(Sandbox player, float width, float height)
        {
            string title, message;
            if (ask == Ask.NewGame) { title = "YENİ OYUN"; message = "Kayıt silinecek. Emin misin?"; }
            else if (ask == Ask.MainMenu) { title = "ANA MENÜYE DÖN"; message = "Kaydedilmemiş ilerleme kaybolur."; }
            else { title = "MASAÜSTÜNE ÇIK"; message = "Oyundan çıkılacak. Kaydedilmemiş ilerleme kaybolur."; }
            int result = UiTheme.Confirm(width, height, title, message, "Evet", "Vazgeç");
            if (result == 0) return;
            Ask chosen = ask;
            ask = Ask.None;
            if (result != 1) return;
            if (chosen == Ask.NewGame)
            {
                SaveSystem.Delete();
                player.RestartLevel();
            }
            else if (chosen == Ask.MainMenu)
            {
                MainMenu.RequestReturn();
                player.RestartLevel();
            }
            else GameQuit.Request();
        }
    }
}
