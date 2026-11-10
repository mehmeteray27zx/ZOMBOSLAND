using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Zombosland
{
    // Session-wide measurement log for human playtests. Static, so it survives scene reloads (death / restart).
    // Complete writes PlaytestResults/playtest_<time>.txt next to the exe and one PLAYTEST_RESULT line to Player.log;
    // quitting earlier writes the same with result=partial. Scripted QA runs are marked qa=1.
    public static class PlaytestLog
    {
        public struct QuestMark { public string state; public float seconds; }

        static bool started, resultWritten, pharmacyEntered;
        static float levelStart;
        static DateTime startedLocal;
        static int stalls;
        static readonly Dictionary<string, int> hintsShown = new Dictionary<string, int>();
        static readonly Dictionary<string, int> hintsDone = new Dictionary<string, int>();

        public static int Deaths { get; private set; }
        public static int ShotsFired { get; private set; }
        public static int Hits { get; private set; }
        public static int HeadHits { get; private set; }
        public static float DamageTaken { get; private set; }
        public static readonly List<QuestMark> QuestTimes = new List<QuestMark>();

        public static bool Started { get { return started; } }
        public static float LevelSeconds { get { return started ? Time.realtimeSinceStartup - levelStart : 0f; } }
        public static bool PharmacyEntered { get { return pharmacyEntered; } }
        public static int Stalls { get { return stalls; } }
        public static string LastResultPath { get; private set; }
        public static string LastResultLine { get; private set; }
        public static int HintShown(string id) { int n; hintsShown.TryGetValue(id, out n); return n; }
        public static int HintDone(string id) { int n; hintsDone.TryGetValue(id, out n); return n; }

        // True for any scripted run (--qa-*, --perf*): their results are marked qa=1.
        public static bool QaRun
        {
            get
            {
                foreach (string a in Environment.GetCommandLineArgs())
                    if (a.StartsWith("--qa-", StringComparison.Ordinal) || a.StartsWith("--perf", StringComparison.Ordinal)) return true;
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookQuit()
        {
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting; // Alt+F4 and window close, as well as GameQuit
        }

        static void OnQuitting() { WritePartial(); }

        // The first call of the session starts the clock; reloads after a death do not restart it.
        public static void BeginLevel()
        {
            if (started) return;
            started = true;
            resultWritten = false;
            levelStart = Time.realtimeSinceStartup;
            startedLocal = DateTime.Now;
        }

        // New attempt from the pause menu.
        public static void ResetSession()
        {
            started = false;
            resultWritten = false;
            pharmacyEntered = false;
            stalls = 0;
            Deaths = ShotsFired = Hits = HeadHits = 0;
            DamageTaken = 0f;
            QuestTimes.Clear();
            hintsShown.Clear();
            hintsDone.Clear();
        }

        public static void RecordShot() { ShotsFired++; }
        public static void RecordHit(HitZone zone) { Hits++; if (zone == HitZone.Head) HeadHits++; }
        public static void RecordDamage(float amount) { if (amount > 0f) DamageTaken += amount; }
        public static void RecordDeath() { Deaths++; }
        public static void RecordQuest(string state, float seconds) { QuestTimes.Add(new QuestMark { state = state, seconds = seconds }); }
        public static void RecordPharmacyEntry() { pharmacyEntered = true; }
        public static void RecordStall() { stalls++; }

        public static void RecordHintShown(string id)
        {
            int n; hintsShown.TryGetValue(id, out n); hintsShown[id] = n + 1;
        }

        public static void RecordHintDone(string id)
        {
            int n; hintsDone.TryGetValue(id, out n); hintsDone[id] = n + 1;
        }

        // ---------- output ----------

        // Level finished: final result.
        public static string WriteComplete() { return Write("complete"); }

        // Quit before the level was finished (no-op once a result exists or nothing was played).
        public static void WritePartial()
        {
            if (!started || resultWritten) return;
            Write("partial");
        }

        static string HintList(Dictionary<string, int> source)
        {
            var sb = new StringBuilder();
            foreach (string id in HintDirector.AllIds)
            {
                int n; source.TryGetValue(id, out n);
                if (sb.Length > 0) sb.Append(',');
                sb.Append(id).Append(':').Append(n);
            }
            return sb.ToString();
        }

        static string QuestList()
        {
            var sb = new StringBuilder();
            foreach (QuestMark m in QuestTimes)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(m.state).Append('@').Append(m.seconds.ToString("F1", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        static string Write(string result)
        {
            resultWritten = true;
            CultureInfo ci = CultureInfo.InvariantCulture;
            string seconds = LevelSeconds.ToString("F1", ci);
            string qa = QaRun ? "1" : "0";
            string pharmacy = pharmacyEntered ? "yes" : "no";
            string damage = DamageTaken.ToString("F0", ci);
            var file = new StringBuilder();
            file.AppendLine("result=" + result);
            file.AppendLine("qa=" + qa);
            file.AppendLine("version=" + Application.version);
            file.AppendLine("started=" + startedLocal.ToString("yyyy-MM-dd HH:mm:ss", ci));
            file.AppendLine("level_seconds=" + seconds);
            file.AppendLine("deaths=" + Deaths);
            file.AppendLine("shots_fired=" + ShotsFired);
            file.AppendLine("shots_hit=" + Hits);
            file.AppendLine("shots_head=" + HeadHits);
            file.AppendLine("damage_taken=" + damage);
            file.AppendLine("pharmacy_entered=" + pharmacy);
            file.AppendLine("stalls_over_20s=" + stalls);
            file.AppendLine("quest_times=" + QuestList());
            file.AppendLine("hints_shown=" + HintList(hintsShown));
            file.AppendLine("hints_completed=" + HintList(hintsDone));

            string path = null;
            try
            {
                string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "PlaytestResults");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, "playtest_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", ci) + (result == "partial" ? "_partial" : "") + ".txt");
                File.WriteAllText(path, file.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                path = null;
                Debug.LogWarning("PLAYTEST_RESULT file could not be written: " + e.Message);
            }
            LastResultPath = path;
            LastResultLine = "PLAYTEST_RESULT result=" + result + " qa=" + qa + " seconds=" + seconds + " deaths=" + Deaths + " shots=" + ShotsFired
                + " hits=" + Hits + " head=" + HeadHits + " damage=" + damage + " pharmacy=" + pharmacy + " stalls=" + stalls
                + " quest=" + QuestList() + " hints_shown=" + HintList(hintsShown) + " hints_completed=" + HintList(hintsDone)
                + " file=" + (path ?? "none");
            Debug.Log(LastResultLine);
            return path;
        }
    }
}
