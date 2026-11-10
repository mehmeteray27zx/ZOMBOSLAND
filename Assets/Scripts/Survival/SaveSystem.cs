using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Zombosland
{
    // JsonUtility cannot store dictionaries, so inventories and loot are parallel arrays.
    [Serializable]
    public class BuildSlotSave
    {
        public string slotId;
        public bool built;
        public float health;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public float px, py, pz, yaw;
        public float health;
        public int ammo, reserve;
        public int quest;
        public string[] openDoors = new string[0];
        public bool fuseTaken, panelInstalled;
        public string[] usedAmmoCrates = new string[0];
        public string[] bagIds = new string[0];
        public int[] bagCounts = new int[0];
        public string[] storageIds = new string[0];
        public int[] storageCounts = new int[0];
        public string[] quickIds = new string[0];
        public string[] lootIds = new string[0];
        public int[] lootRemaining = new int[0];
        public bool bandageIntro;
        public BuildSlotSave[] buildSlots = new BuildSlotSave[0]; // missing in older saves: JsonUtility leaves null, which means "no change"
    }

    // Single-slot file save in persistentDataPath. Written atomically (tmp + replace). A broken or foreign-version file is
    // kept as .bad and the game starts without a save.
    public static class SaveSystem
    {
        public const int Version = 1;

        static string forcedSlot;

        // QA runs that pass --save-slot keep saving; the other scripted runs do not touch the player's save.
        public static bool Enabled
        {
            get
            {
                if (QaArgs.Has("--qa-screens-openworld")) return false; // screenshots never touch or write a save, even with --save-slot
                if (QaArgs.Has("--qa-axe")) return false; // the axe QA never touches or writes a save either
                if (QaArgs.Has("--qa-reload")) return false; // neither does the reload QA
                if (forcedSlot != null) return true;
                if (QaArgs.GetString("--save-slot", null) != null) return true;
                foreach (string a in Environment.GetCommandLineArgs())
                    if (a == "--qa-playthrough" || a == "--qa-selftest" || a.StartsWith("--qa-screens", StringComparison.Ordinal)
                        || a == "--perf" || a.StartsWith("--perf", StringComparison.Ordinal)) return false;
                return true;
            }
        }

        public static string SlotName { get { return forcedSlot ?? QaArgs.GetString("--save-slot", null); } }

        public static string FilePath
        {
            get
            {
                string slot = SlotName;
                string file = string.IsNullOrEmpty(slot) ? "save_v1.json" : "save_" + slot + ".json";
                return Path.Combine(Application.persistentDataPath, file);
            }
        }

        public static bool HasSave { get { return Enabled && File.Exists(FilePath); } }

        // Used by --qa-survival: its own slot, wiped first.
        public static void UseSlot(string slot, bool deleteExisting)
        {
            forcedSlot = slot;
            if (deleteExisting) Delete();
        }

        public static void Delete()
        {
            try
            {
                string path = FilePath;
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e) { Debug.LogWarning("SAVE delete failed: " + e.Message); }
        }

        // Snapshot of the world. The craft is cancelled and reservations are released first, so the file never holds half a craft.
        public static bool Save(string reason)
        {
            if (!Enabled) return false;
            WorldState world = WorldState.Instance;
            if (world == null || !world.CanSave) return false;
            PlayerSurvival ps = PlayerSurvival.Instance;
            if (ps != null && ps.Bag != null)
            {
                ps.Crafter.Cancel("Kayıt alındı");
                ps.Bag.ReleaseAll();
            }
            try
            {
                SaveData data = world.Capture();
                string json = JsonUtility.ToJson(data, true);
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
                Debug.Log("SAVE ok reason=" + reason + " quest=" + (QuestState)data.quest + " file=" + path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("SAVE failed reason=" + reason + " error=" + e.Message);
                return false;
            }
        }

        // Null when there is no usable save. A broken file is moved aside as .bad.
        public static SaveData TryLoad()
        {
            if (!Enabled) return null;
            string path = FilePath;
            if (!File.Exists(path)) return null;
            SaveData data = null;
            string problem = null;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null) problem = "empty or unreadable";
                else if (data.version != Version) problem = "version " + data.version + " (expected " + Version + ")";
                else if (!IsConsistent(data)) problem = "inconsistent arrays";
            }
            catch (Exception e) { problem = e.Message; }
            if (problem == null) return data;
            Debug.LogWarning("SAVE broken (" + problem + "), kept as .bad, starting without a save: " + path);
            try
            {
                string bad = path + ".bad";
                File.Copy(path, bad, true);
                File.Delete(path);
            }
            catch (Exception e) { Debug.LogWarning("SAVE could not move the broken file: " + e.Message); }
            return null;
        }

        static bool IsConsistent(SaveData d)
        {
            return d.bagIds != null && d.bagCounts != null && d.bagIds.Length == d.bagCounts.Length
                && d.storageIds != null && d.storageCounts != null && d.storageIds.Length == d.storageCounts.Length
                && d.lootIds != null && d.lootRemaining != null && d.lootIds.Length == d.lootRemaining.Length
                && d.openDoors != null && d.usedAmmoCrates != null && d.quickIds != null;
        }
    }
}
