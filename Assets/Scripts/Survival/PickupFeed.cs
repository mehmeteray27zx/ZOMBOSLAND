using System.Collections.Generic;
using UnityEngine;

namespace Zombosland
{
    // Short "+2 Kumaş" lines at the bottom right. At most 4 lines; the same item within 2 s grows its line.
    public static class PickupFeed
    {
        public struct Line { public ItemDef item; public int count; public float born, lastAdded; }

        public const int MaxLines = 4;
        public const float LineSeconds = 1.8f, MergeSeconds = 2f, FadeIn = 0.12f, FadeOut = 0.3f;

        static readonly List<Line> lines = new List<Line>();

        public static int Count { get { Prune(); return lines.Count; } }

        public static Line At(int i) { return lines[i]; }

        // Opacity of a line right now (0..1).
        public static float Alpha(Line l)
        {
            float now = Time.unscaledTime, age = now - l.lastAdded;
            float inAge = now - l.born;
            return Mathf.Clamp01(inAge / FadeIn) * Mathf.Clamp01((LineSeconds - age) / FadeOut);
        }

        public static void Add(ItemDef item, int n)
        {
            if (item == null || n <= 0) return;
            Prune();
            float now = Time.unscaledTime;
            for (int i = 0; i < lines.Count; i++)
            {
                Line l = lines[i];
                if (l.item != item || now - l.lastAdded > MergeSeconds) continue;
                l.count += n;
                l.lastAdded = now;
                lines.RemoveAt(i);
                lines.Add(l); // newest at the end
                return;
            }
            if (lines.Count >= MaxLines) lines.RemoveAt(0);
            lines.Add(new Line { item = item, count = n, born = now, lastAdded = now });
        }

        public static void Clear() { lines.Clear(); }

        static void Prune()
        {
            float now = Time.unscaledTime;
            for (int i = lines.Count - 1; i >= 0; i--)
                if (now - lines[i].lastAdded >= LineSeconds) lines.RemoveAt(i);
        }
    }
}
