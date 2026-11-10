using System;
using System.Globalization;

namespace Zombosland
{
    // Command line helpers for QA / perf modes.
    public static class QaArgs
    {
        static string[] args;
        static string[] Args { get { if (args == null) args = Environment.GetCommandLineArgs(); return args; } }

        public static bool Has(string name) { return Array.IndexOf(Args, name) >= 0; }

        public static bool TryGetFloat(string key, out float value)
        {
            string prefix = key + "=";
            foreach (string a in Args)
                if (a.StartsWith(prefix, StringComparison.Ordinal) &&
                    float.TryParse(a.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            value = 0;
            return false;
        }

        // Value of "key=value" (e.g. --arms=survivalist), or the fallback.
        public static string GetString(string key, string fallback)
        {
            string prefix = key + "=";
            foreach (string a in Args)
                if (a.StartsWith(prefix, StringComparison.Ordinal)) return a.Substring(prefix.Length);
            return fallback;
        }

        public static int GetInt(string key, int fallback)
        {
            float v;
            return TryGetFloat(key, out v) ? (int)v : fallback;
        }

        public static float GetFloat(string key, float fallback)
        {
            float v;
            return TryGetFloat(key, out v) ? v : fallback;
        }
    }
}
