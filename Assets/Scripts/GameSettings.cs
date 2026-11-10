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
            Clamp();
            ApplyAudio();
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
            PlayerPrefs.Save();
        }

        public static void ApplyAudio() { AudioListener.volume = MasterVolume; }

        static void Clamp()
        {
            Sensitivity = Mathf.Clamp(Sensitivity, 0.3f, 5f);
            Fov = Mathf.Clamp(Fov, 60f, 100f);
            Headbob = Mathf.Clamp01(Headbob);
            WeaponSway = Mathf.Clamp01(WeaponSway);
            CameraShake = Mathf.Clamp01(CameraShake);
            MasterVolume = Mathf.Clamp01(MasterVolume);
        }
    }
}
