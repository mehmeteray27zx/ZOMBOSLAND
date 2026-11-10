using UnityEngine;

namespace Zombosland
{
    // Esc settings panel. Sliders write straight into GameSettings, so every change applies immediately.
    public static class PauseMenu
    {
        static GUIStyle label, button;
        static bool hasSave;

        public static void Draw(Sandbox player, float width, float height)
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                label.normal.textColor = UiTheme.Text;
            }
            button = UiTheme.FlatButton(18); // flat, squared; shared style instance
            bool tutorialEntries = HintDirector.Instance != null; // Ilk Ikmal only; CombatSandbox has no hint cards
            bool saveEntries = QuestFuse.Instance != null;        // Ilk Ikmal only: Kaydet / Son kaydı yükle / Yeni oyun
            // The button set must be the same in the Layout and Repaint passes, so the save state is read once per layout.
            if (Event.current != null && Event.current.type == EventType.Layout) hasSave = SaveSystem.HasSave;
            float panelW = 420, panelH = (tutorialEntries ? 590 : 520) + (saveEntries ? 80 : 0);
            Rect panel = new Rect(width / 2 - panelW / 2, height / 2 - panelH / 2, panelW, panelH);
            UiTheme.Fill(new Rect(0f, 0f, width, height), UiTheme.Scrim);
            UiTheme.Fill(panel, UiTheme.WithAlpha(UiTheme.Panel, 0.98f));
            UiTheme.Border(panel, UiTheme.Rule);
            GUI.Label(new Rect(panel.x + 20, panel.y + 10, panelW - 40, 30), "AYARLAR — " + InputLabels.Menu + " ile devam", label);
            UiTheme.HRule(panel.x + 20, panel.y + 42, panelW - 40, UiTheme.Rule);

            GUILayout.BeginArea(new Rect(panel.x + 25, panel.y + 50, panelW - 50, panelH - 60));
            GameSettings.Sensitivity = Slider("Fare hassasiyeti", GameSettings.Sensitivity, 0.3f, 5f, "F1");
            GameSettings.Fov = Slider("Görüş alanı (FOV)", GameSettings.Fov, 60f, 100f, "F0");
            GameSettings.Headbob = Slider("Headbob", GameSettings.Headbob, 0f, 1f, "F2");
            GameSettings.WeaponSway = Slider("Silah sway", GameSettings.WeaponSway, 0f, 1f, "F2");
            GameSettings.CameraShake = Slider("Kamera sarsıntısı", GameSettings.CameraShake, 0f, 1f, "F2");
            GameSettings.MasterVolume = Slider("Ana ses", GameSettings.MasterVolume, 0f, 1f, "F2");
            GameSettings.ApplyAudio();
            GUILayout.Space(4);
            GUILayout.Label("0 = kapalı (headbob, sway, sarsıntı). Nişan tepmesi bu ayarlardan etkilenmez.", label);
            GUILayout.Space(8);
            if (tutorialEntries)
            {
                if (GUILayout.Button("Öğretici: " + (GameSettings.Tutorial ? "Açık" : "Kapalı"), button)) GameSettings.Tutorial = !GameSettings.Tutorial;
                if (GUILayout.Button("Öğretici ilerlemesini sıfırla", button)) HintDirector.ResetProgress();
                GUILayout.Space(8);
            }
            if (saveEntries)
            {
                if (GUILayout.Button("Kaydet", button))
                    Notifier.Show(SaveSystem.Save("manual") ? "Oyun kaydedildi" : "Kayıt alınamadı", 2.5f);
                if (hasSave && GUILayout.Button("Son kaydı yükle", button)) player.Restart();
                if (GUILayout.Button("Yeni oyun", button))
                {
                    SaveSystem.Delete();
                    player.RestartLevel();
                }
            }
            else if (GUILayout.Button("Yeniden başlat", button)) player.Restart();
            if (GUILayout.Button("Çıkış", button)) GameQuit.Request();
            GUILayout.EndArea();
        }

        static float Slider(string name, float value, float min, float max, string format)
        {
            GUILayout.Label(name + ": " + value.ToString(format), label);
            return GUILayout.HorizontalSlider(value, min, max);
        }
    }
}
