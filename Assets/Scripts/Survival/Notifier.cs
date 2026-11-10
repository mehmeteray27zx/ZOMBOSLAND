using System.Collections.Generic;
using UnityEngine;

namespace Zombosland
{
    // Short on-screen messages (toast). The HUD reads Current; at most 3 wait in the queue.
    public static class Notifier
    {
        struct Entry { public string text; public float seconds; }

        const int MaxQueued = 3;
        static readonly List<Entry> queue = new List<Entry>();
        static string current;
        static float shownAt, until;

        // Text that should be on screen right now (null = nothing).
        public static string Current
        {
            get
            {
                Advance();
                return current;
            }
        }

        // Unscaled time the current message appeared (for fades).
        public static float ShownAt { get { return shownAt; } }
        public static float Until { get { return until; } }

        public static void Show(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (seconds < 0.5f) seconds = 0.5f;
            Advance();
            float now = Time.unscaledTime;
            if (current == null)
            {
                Begin(text, seconds, now);
                return;
            }
            if (current == text) { until = Mathf.Max(until, now + seconds); return; }
            if (queue.Count > 0 && queue[queue.Count - 1].text == text) return;
            if (queue.Count >= MaxQueued) queue.RemoveAt(0);
            queue.Add(new Entry { text = text, seconds = seconds });
        }

        public static void Clear()
        {
            queue.Clear();
            current = null;
        }

        static void Begin(string text, float seconds, float now)
        {
            current = text;
            shownAt = now;
            until = now + seconds;
        }

        static void Advance()
        {
            if (current == null) return;
            float now = Time.unscaledTime;
            if (now < until) return;
            if (queue.Count > 0)
            {
                Entry e = queue[0];
                queue.RemoveAt(0);
                Begin(e.text, e.seconds, now);
            }
            else current = null;
        }
    }
}
