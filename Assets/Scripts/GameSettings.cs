using System;
using UnityEngine;

namespace Zombosland
{
    // Player-facing options, stored in PlayerPrefs. Values apply immediately because systems read them every frame.
    public static class GameSettings
    {
        public static float Sensitivity = 2f;     // 0.3 - 5
        public static float Fov = 75f;            // 60 - 100
        public static float Headbob = 1f;         // 0 - 1
        public static float WeaponSway = 1f;      // 0 - 1
        public static float CameraShake = 1f;     // 0 - 1
        public static float MasterVolume = 1f;    // 0 - 1
        public static bool Tutorial = true;       // false hides only the hint cards (objective line and [E] labels stay)
        public static bool InvertY = false;       // true flips vertical mouse look

        // Video options. Until the player changes one of them (VideoTouched) the fields only mirror the engine's own
        // values and nothing is ever applied, so a player who never opens Graphics sees no change at all.
        public static bool VideoTouched;
        public static int VideoQuality;           // QualitySettings level index
        public static bool Fullscreen;
        public static int ResW, ResH;
        public static bool VSync;
        public static int FpsCap = -1;            // -1 = unlimited

        public static readonly int[] FpsOptions = { 30, 60, 120, 144, -1 };

        static int automated; // 0 = not checked, 1 = no, 2 = yes

        // True for scripted runs (any argument starting with --qa or --perf): video options are never applied there.
        public static bool Automated
        {
            get
            {
                if (automated == 0)
                {
                    automated = 1;
                    foreach (string a in Environment.GetCommandLineArgs())
                        if (a.StartsWith("--qa", StringComparison.Ordinal) || a.StartsWith("--perf", StringComparison.Ordinal)) { automated = 2; break; }
                }
                return automated == 2;
            }
        }

        public static void Load()
        {
            Sensitivity = PlayerPrefs.GetFloat("sensitivity", 2f);
            Fov = PlayerPrefs.GetFloat("fov", 75f);
            Headbob = PlayerPrefs.GetFloat("headbob", 1f);
            CameraShake = PlayerPrefs.GetFloat("cameraShake", 1f);
            MasterVolume = PlayerPrefs.GetFloat("masterVolume", 1f);
            // Old builds stored the weapon motion slider as "shake"; migrate it once.
            if (!PlayerPrefs.HasKey("weaponSway") && PlayerPrefs.HasKey("shake"))
            {
                PlayerPrefs.SetFloat("weaponSway", PlayerPrefs.GetFloat("shake", 1f));
                PlayerPrefs.DeleteKey("shake");
            }
            WeaponSway = PlayerPrefs.GetFloat("weaponSway", 1f);
            Tutorial = PlayerPrefs.GetInt("tutorial", 1) != 0;
            InvertY = PlayerPrefs.GetInt("invertY", 0) != 0;
            LoadVideo();
            Clamp();
            ApplyAudio();
            if (VideoTouched && !Automated) ApplyVideo();
        }

        public static void Save()
        {
            Clamp();
            PlayerPrefs.SetFloat("sensitivity", Sensitivity);
            PlayerPrefs.SetFloat("fov", Fov);
            PlayerPrefs.SetFloat("headbob", Headbob);
            PlayerPrefs.SetFloat("weaponSway", WeaponSway);
            PlayerPrefs.SetFloat("cameraShake", CameraShake);
            PlayerPrefs.SetFloat("masterVolume", MasterVolume);
            PlayerPrefs.SetInt("tutorial", Tutorial ? 1 : 0);
            PlayerPrefs.SetInt("invertY", InvertY ? 1 : 0);
            if (VideoTouched)
            {
                PlayerPrefs.SetInt("videoSet", 1);
                PlayerPrefs.SetInt("videoQuality", VideoQuality);
                PlayerPrefs.SetInt("videoFullscreen", Fullscreen ? 1 : 0);
                PlayerPrefs.SetInt("videoResW", ResW);
                PlayerPrefs.SetInt("videoResH", ResH);
                PlayerPrefs.SetInt("videoVSync", VSync ? 1 : 0);
                PlayerPrefs.SetInt("videoFpsCap", FpsCap);
            }
            PlayerPrefs.Save();
        }

        public static void ApplyAudio() { AudioListener.volume = MasterVolume; }

        // Saved values when the player has changed a video option before; otherwise whatever the engine runs with right now.
        static void LoadVideo()
        {
            VideoTouched = PlayerPrefs.GetInt("videoSet", 0) != 0;
            int quality = QualitySettings.GetQualityLevel();
            bool full = Screen.fullScreen;
            int rw = Screen.width, rh = Screen.height;
            bool vsync = QualitySettings.vSyncCount > 0;
            int fps = Application.targetFrameRate <= 0 ? -1 : Application.targetFrameRate;
            if (VideoTouched)
            {
                VideoQuality = PlayerPrefs.GetInt("videoQuality", quality);
                Fullscreen = PlayerPrefs.GetInt("videoFullscreen", full ? 1 : 0) != 0;
                ResW = PlayerPrefs.GetInt("videoResW", rw);
                ResH = PlayerPrefs.GetInt("videoResH", rh);
                VSync = PlayerPrefs.GetInt("videoVSync", vsync ? 1 : 0) != 0;
                FpsCap = PlayerPrefs.GetInt("videoFpsCap", fps);
            }
            else
            {
                VideoQuality = quality;
                Fullscreen = full;
                ResW = rw;
                ResH = rh;
                VSync = vsync;
                FpsCap = fps;
            }
        }

        // Everything at once (used when loading saved options).
        public static void ApplyVideo()
        {
            ApplyFrame();
            ApplyScreen();
        }

        // Quality level, VSync and frame cap. The quality level goes first: switching levels resets VSync to the level's own value.
        public static void ApplyFrame()
        {
            int q = Mathf.Clamp(VideoQuality, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != q) QualitySettings.SetQualityLevel(q, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = FpsCap; // ignored by the engine while VSync is on
        }

        // Window mode and resolution. Full screen at the desktop size is a borderless window; a smaller size needs exclusive mode.
        public static void ApplyScreen()
        {
            if (ResW <= 0 || ResH <= 0) return;
            if (Screen.fullScreen == Fullscreen && Screen.width == ResW && Screen.height == ResH) return;
            FullScreenMode mode = FullScreenMode.Windowed;
            if (Fullscreen)
                mode = ResW >= Display.main.systemWidth && ResH >= Display.main.systemHeight ? FullScreenMode.FullScreenWindow : FullScreenMode.ExclusiveFullScreen;
            Screen.SetResolution(ResW, ResH, mode);
        }

        // Switching the window mode also picks a sensible size: the desktop size for full screen, a smaller listed size for a window.
        public static void SetFullscreen(bool on)
        {
            Fullscreen = on;
            int nw = Display.main.systemWidth, nh = Display.main.systemHeight;
            if (on)
            {
                ResW = nw;
                ResH = nh;
            }
            else if (ResW >= nw && ResH >= nh)
            {
                int bestW = 0, bestH = 0;
                foreach (Resolution r in Screen.resolutions)
                    if (r.height <= nh * 0.85f && r.width <= nw * 0.85f && (long)r.width * r.height > (long)bestW * bestH) { bestW = r.width; bestH = r.height; }
                if (bestW > 0) { ResW = bestW; ResH = bestH; }
                else { ResW = Mathf.Max(640, nw * 3 / 4); ResH = Mathf.Max(360, nh * 3 / 4); }
            }
        }

        static void Clamp()
        {
            Sensitivity = Mathf.Clamp(Sensitivity, 0.3f, 5f);
            Fov = Mathf.Clamp(Fov, 60f, 100f);
            Headbob = Mathf.Clamp01(Headbob);
            WeaponSway = Mathf.Clamp01(WeaponSway);
            CameraShake = Mathf.Clamp01(CameraShake);
            MasterVolume = Mathf.Clamp01(MasterVolume);
            VideoQuality = Mathf.Clamp(VideoQuality, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            if (ResW < 640) ResW = 640;
            if (ResH < 360) ResH = 360;
            if (FpsCap <= 0) FpsCap = -1;
        }
    }
}
