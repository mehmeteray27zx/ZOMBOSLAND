using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Zombosland;

// First open region of Zombosland (500 x 500 m Terrain east/north of the old IlkIkmal map). Region coordinates R: world = (Rx - 46, y, Rz - 110),
// so terrain-local (x, z) equals R. This file: layout data, splines, corridor fields and the height field.
// Other parts: OpenWorldBuilder.Terrain.cs (layers, splat, vegetation, collider), .Roads.cs, .Props.cs, .Gameplay.cs.
internal static partial class OpenWorldBuilder
{
    // ---------- constants ----------

    public const float RegionSize = 500f;
    public const float TerrainHeightRange = 56f;
    public static readonly Vector3 TerrainOrigin = new Vector3(-46f, -8f, -110f);
    public const int HeightRes = 513;
    public const int AlphaRes = 1024;
    public const string TerrainFolder = "Assets/Generated/Terrain";

    const float RoadHalf = 3.5f;       // 7 m road
    const float PathHalf = 1.1f;       // 2.2 m footpath
    const float CellSize = RegionSize / (HeightRes - 1);

    // Old map footprint (outer wall faces) in R coordinates.
    const float FootX0 = 0f, FootX1 = 97f, FootZ0 = 68f, FootZ1 = 156f;

    static readonly Vector2[] RoadCtrl =
    {
        new Vector2(97, 72), new Vector2(125, 70), new Vector2(160, 85), new Vector2(195, 115), new Vector2(232, 140), new Vector2(262, 175),
        new Vector2(285, 225), new Vector2(306, 268), new Vector2(322, 295), new Vector2(345, 330), new Vector2(372, 372), new Vector2(392, 382)
    };
    // World y of the road surface at each control point (gate apron 0, flat at the station, climbing to the tower hill pad).
    static readonly float[] RoadElev = { 0f, 0f, 0.6f, 1.5f, 2.6f, 2.6f, 7.0f, 10.4f, 12.5f, 17.2f, 23.8f, 26.0f };

    static readonly Vector2[] PathCtrl =
    {
        new Vector2(97, 88), new Vector2(110, 100), new Vector2(112, 140), new Vector2(118, 190), new Vector2(128, 245), new Vector2(143, 292),
        new Vector2(185, 310), new Vector2(230, 312), new Vector2(262, 306), new Vector2(272, 303), new Vector2(295, 298), new Vector2(322, 295)
    };

    static readonly Vector2[] CreekCtrl =
    {
        new Vector2(170, 500), new Vector2(205, 420), new Vector2(240, 345), new Vector2(268, 305), new Vector2(314, 281), new Vector2(370, 215),
        new Vector2(440, 135), new Vector2(500, 95)
    };

    static readonly Vector2 StationPoi = new Vector2(245, 155);
    static readonly Vector2 HutPoi = new Vector2(145, 300);
    static readonly Vector2 FordPoi = new Vector2(267, 305);
    static readonly Vector2 TowerPoi = new Vector2(390, 390);
    const float HutLevel = 11f;
    const float TowerLevel = 26f;

    public static Vector3 RW(float rx, float rz, float y = 0f) { return new Vector3(rx - 46f, y, rz - 110f); }
    public static Vector3 RW(Vector2 r, float y = 0f) { return new Vector3(r.x - 46f, y, r.y - 110f); }

    static void Note(string text) { SandboxBuilder.Report.Add(text); }
    static string F(float v) { return v.ToString("F2", CultureInfo.InvariantCulture); }

    static float Ss(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // ---------- polylines ----------

    sealed class Poly
    {
        public Vector2[] p;      // R coordinates, uniform arc spacing
        public float[] s;        // arc length
        public float[] e;        // elevation (world y), road / path / creek bed
        public float[] ctrlS;    // arc length of every control point
        public float length;
        public int Count { get { return p.Length; } }

        public Vector2 Dir(int i)
        {
            int a = Mathf.Max(0, i - 1), b = Mathf.Min(p.Length - 1, i + 1);
            return (p[b] - p[a]).normalized;
        }

        public int IndexAt(float arc)
        {
            int lo = 0, hi = p.Length - 1;
            while (lo < hi - 1) { int mid = (lo + hi) / 2; if (s[mid] <= arc) lo = mid; else hi = mid; }
            return lo;
        }

        public Vector2 PointAt(float arc)
        {
            arc = Mathf.Clamp(arc, 0f, length);
            int i = Mathf.Min(IndexAt(arc), p.Length - 2);
            float t = Mathf.InverseLerp(s[i], s[i + 1], arc);
            return Vector2.Lerp(p[i], p[i + 1], t);
        }

        public float ElevAt(float arc)
        {
            arc = Mathf.Clamp(arc, 0f, length);
            int i = Mathf.Min(IndexAt(arc), p.Length - 2);
            return Mathf.Lerp(e[i], e[i + 1], Mathf.InverseLerp(s[i], s[i + 1], arc));
        }

        public int Nearest(Vector2 q)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < p.Length; i++) { float d = (p[i] - q).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            return best;
        }
    }

    static Vector2 TangentAt(Vector2[] c, int i)
    {
        return (c[Mathf.Min(i + 1, c.Length - 1)] - c[Mathf.Max(i - 1, 0)]).normalized;
    }

    // Hermite spline through the control points (Catmull-Rom style tangents scaled by chord length), resampled at a uniform arc step.
    static Poly MakeSpline(Vector2[] ctrl, float step)
    {
        var dense = new List<Vector2>();
        var denseCtrl = new List<int>();
        for (int i = 0; i < ctrl.Length - 1; i++)
        {
            Vector2 p0 = ctrl[i], p1 = ctrl[i + 1];
            float L = (p1 - p0).magnitude;
            Vector2 m0 = TangentAt(ctrl, i) * L, m1 = TangentAt(ctrl, i + 1) * L;
            denseCtrl.Add(dense.Count);
            for (int k = 0; k < 24; k++)
            {
                float t = k / 24f, t2 = t * t, t3 = t2 * t;
                dense.Add((2f * t3 - 3f * t2 + 1f) * p0 + (t3 - 2f * t2 + t) * m0 + (-2f * t3 + 3f * t2) * p1 + (t3 - t2) * m1);
            }
        }
        dense.Add(ctrl[ctrl.Length - 1]);
        denseCtrl.Add(dense.Count - 1);
        var cum = new float[dense.Count];
        for (int i = 1; i < dense.Count; i++) cum[i] = cum[i - 1] + (dense[i] - dense[i - 1]).magnitude;
        float total = cum[cum.Length - 1];

        int count = Mathf.CeilToInt(total / step) + 1;
        var poly = new Poly { p = new Vector2[count], s = new float[count], e = new float[count], length = total };
        int seg = 0;
        for (int j = 0; j < count; j++)
        {
            float arc = Mathf.Min(j * step, total);
            while (seg < dense.Count - 2 && cum[seg + 1] < arc) seg++;
            float t = Mathf.InverseLerp(cum[seg], cum[seg + 1], arc);
            poly.p[j] = Vector2.Lerp(dense[seg], dense[seg + 1], t);
            poly.s[j] = arc;
        }
        poly.ctrlS = new float[ctrl.Length];
        for (int i = 0; i < ctrl.Length; i++) poly.ctrlS[i] = cum[denseCtrl[i]];
        return poly;
    }

    static float PchipSlope(float[] xs, float[] ys, int i)
    {
        int n = xs.Length;
        if (i == 0) return (ys[1] - ys[0]) / (xs[1] - xs[0]);
        if (i == n - 1) return (ys[n - 1] - ys[n - 2]) / (xs[n - 1] - xs[n - 2]);
        float d0 = (ys[i] - ys[i - 1]) / (xs[i] - xs[i - 1]), d1 = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
        if (d0 * d1 <= 0f) return 0f;
        float w0 = 2f * (xs[i + 1] - xs[i]) + (xs[i] - xs[i - 1]), w1 = (xs[i + 1] - xs[i]) + 2f * (xs[i] - xs[i - 1]);
        return (w0 + w1) / (w0 / d0 + w1 / d1);
    }

    // Monotone cubic interpolation (no overshoot between control elevations).
    static float Pchip(float[] xs, float[] ys, float x)
    {
        int n = xs.Length;
        if (x <= xs[0]) return ys[0];
        if (x >= xs[n - 1]) return ys[n - 1];
        int k = 0;
        while (k < n - 2 && x > xs[k + 1]) k++;
        float h = xs[k + 1] - xs[k], t = (x - xs[k]) / h, t2 = t * t, t3 = t2 * t;
        float m0 = PchipSlope(xs, ys, k), m1 = PchipSlope(xs, ys, k + 1);
        return (2f * t3 - 3f * t2 + 1f) * ys[k] + (t3 - 2f * t2 + t) * h * m0 + (-2f * t3 + 3f * t2) * ys[k + 1] + (t3 - t2) * h * m1;
    }

    // ---------- corridor fields ----------

    // Per grid cell: distance to the polyline, the elevation and the arc length at the closest point.
    sealed class Field
    {
        public readonly int n;
        public readonly float cs;
        public readonly float off;
        public readonly float[] d, e, s;

        public Field(int n, float cs, bool cellCentre, Poly poly, float radius)
        {
            this.n = n; this.cs = cs; off = cellCentre ? 0.5f : 0f;
            d = new float[n * n]; e = new float[n * n]; s = new float[n * n];
            for (int i = 0; i < d.Length; i++) d[i] = 1e9f;
            for (int k = 0; k < poly.Count - 1; k++)
            {
                Vector2 a = poly.p[k], b = poly.p[k + 1];
                int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius) / cs - off)), i1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + radius) / cs - off));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - radius) / cs - off)), j1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + radius) / cs - off));
                Vector2 ab = b - a;
                float len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        Vector2 q = new Vector2((i + off) * cs, (j + off) * cs);
                        float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
                        float dist = (q - (a + ab * t)).magnitude;
                        int idx = j * n + i;
                        if (dist < d[idx])
                        {
                            d[idx] = dist;
                            e[idx] = Mathf.Lerp(poly.e[k], poly.e[k + 1], t);
                            s[idx] = Mathf.Lerp(poly.s[k], poly.s[k + 1], t);
                        }
                    }
            }
        }

        public int Index(float x, float z)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(x / cs - off), 0, n - 1), j = Mathf.Clamp(Mathf.RoundToInt(z / cs - off), 0, n - 1);
            return j * n + i;
        }
    }

    // ---------- pads and frames ----------

    struct Frame
    {
        public Vector2 o;
        public float yaw; // degrees, Unity Y rotation: local +z = (sin, cos), local +x = (cos, -sin) in R (x, z)
        public Vector2 Right { get { float t = yaw * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(t), -Mathf.Sin(t)); } }
        public Vector2 Fwd { get { float t = yaw * Mathf.Deg2Rad; return new Vector2(Mathf.Sin(t), Mathf.Cos(t)); } }
        public Vector2 ToR(float lx, float lz) { return o + Right * lx + Fwd * lz; }
        public Frame(Vector2 o, float yaw) { this.o = o; this.yaw = yaw; }
    }

    struct Pad
    {
        public Vector2 c;
        public float yaw, hx, hz, level, falloff;
        public bool circle;

        public float Dist(float x, float z)
        {
            float dx = x - c.x, dz = z - c.y;
            if (circle) return Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - hx);
            float t = yaw * Mathf.Deg2Rad, cs = Mathf.Cos(t), sn = Mathf.Sin(t);
            float lx = dx * cs - dz * sn, lz = dx * sn + dz * cs;
            float ex = Mathf.Max(Mathf.Abs(lx) - hx, 0f), ez = Mathf.Max(Mathf.Abs(lz) - hz, 0f);
            return Mathf.Sqrt(ex * ex + ez * ez);
        }
    }

    // ---------- state of one generation run ----------

    static Poly road, path, creek;                 // creek is clipped to the interior
    static Field roadF, pathF, creekF;             // at heightmap resolution (vertex aligned)
    static float[] creekWater;                     // water surface per creek sample
    static float[] creekFade;                      // 0..1 carve strength per creek sample
    static float bridgeCross;                      // arc of the road where it crosses the creek
    static float fordLevel;                        // bed height at the ford
    static int fordCreekIndex, fordPathIndex, hutPathIndex;
    static float[,] heights;                       // world y, [z, x]
    static readonly List<Pad> pads = new List<Pad>();
    static Frame station, hut;
    static float stationLevel;
    static readonly Frame[] houses = new Frame[3];
    static readonly List<Vector3> pins = new List<Vector3>();
    static Terrain terrain;
    static float maxRoadSlopeDeg, maxPathSlopeDeg;

    static void BuildSplines()
    {
        road = MakeSpline(RoadCtrl, 2f);
        for (int i = 0; i < road.Count; i++) road.e[i] = Pchip(road.ctrlS, RoadElev, road.s[i]);
        path = MakeSpline(PathCtrl, 2f);
        var rawCreek = MakeSpline(CreekCtrl, 2f);

        // Clip the creek to the interior (it fades out before the perimeter ridge).
        int first = -1, last = -1;
        for (int i = 0; i < rawCreek.Count; i++)
        {
            Vector2 q = rawCreek.p[i];
            if (Mathf.Min(Mathf.Min(q.x, q.y), Mathf.Min(RegionSize - q.x, RegionSize - q.y)) >= 42f) { if (first < 0) first = i; last = i; }
        }
        creek = new Poly { p = new Vector2[last - first + 1], s = new float[last - first + 1], e = new float[last - first + 1] };
        for (int i = first; i <= last; i++) { creek.p[i - first] = rawCreek.p[i]; creek.s[i - first] = rawCreek.s[i] - rawCreek.s[first]; }
        creek.length = creek.s[creek.Count - 1];

        float rs = 0f;
        for (int i = 1; i < road.Count; i++) rs = Mathf.Max(rs, Mathf.Abs(road.e[i] - road.e[i - 1]) / (road.s[i] - road.s[i - 1]));
        maxRoadSlopeDeg = Mathf.Atan(rs) * Mathf.Rad2Deg;
    }

    // ---------- layout (frames, pads, bridge) ----------

    static void PlanLayout()
    {
        // Station: the main road passes through R(245,155); the complex stands on the right-hand side of the road (+x in the frame).
        int si = road.Nearest(StationPoi);
        Vector2 t = road.Dir(si);
        station = new Frame(road.p[si], Mathf.Atan2(t.x, t.y) * Mathf.Rad2Deg);
        stationLevel = Mathf.Round(road.e[si] * 100f) / 100f;

        pads.Clear();
        pads.Add(new Pad { c = station.ToR(17.8f, 0f), yaw = station.yaw, hx = 14.6f, hz = 15.2f, level = stationLevel - 0.1f, falloff = 7f });
        // Rear approach (v0.8): a flat strip south of the fence and one east of it, so the rear route is walkable and clear of trees.
        pads.Add(new Pad { c = station.ToR(21.5f, -21f), yaw = station.yaw, hx = 19.5f, hz = 3.2f, level = stationLevel - 0.1f, falloff = 6f });
        pads.Add(new Pad { c = station.ToR(37f, -10f), yaw = station.yaw, hx = 4f, hz = 12.5f, level = stationLevel - 0.1f, falloff = 6f });
        rearRouteR = new Vector2[RearRouteLocal.Length];
        for (int k = 0; k < rearRouteR.Length; k++) rearRouteR[k] = station.ToR(RearRouteLocal[k].x, RearRouteLocal[k].y);

        // Hut: door side faces the nearest footpath point.
        hutPathIndex = path.Nearest(HutPoi);
        Vector2 toPath = path.p[hutPathIndex] - HutPoi;
        hut = new Frame(HutPoi, Mathf.Atan2(toPath.x, toPath.y) * Mathf.Rad2Deg);
        pads.Add(new Pad { c = HutPoi, yaw = hut.yaw, hx = 6.5f, hz = 7.5f, level = HutLevel - 0.1f, falloff = 9f });

        // Tower hill: one flat disc, houses stand on it.
        pads.Add(new Pad { c = TowerPoi, hx = 24f, hz = 24f, level = TowerLevel - 0.1f, falloff = 26f, circle = true });
        houses[0] = new Frame(new Vector2(372f, 398f), 20f);
        houses[1] = new Frame(new Vector2(408f, 378f), -35f);
        houses[2] = new Frame(new Vector2(396f, 410f), 75f);

        // Bridge crossing: closest approach between the road (between control points 7 and 8) and the creek.
        float best = float.MaxValue;
        bridgeCross = (road.ctrlS[7] + road.ctrlS[8]) * 0.5f;
        for (int i = 0; i < road.Count; i++)
        {
            if (road.s[i] < road.ctrlS[7] - 4f || road.s[i] > road.ctrlS[8] + 4f) continue;
            for (int k = 0; k < creek.Count; k++)
            {
                float d = (road.p[i] - creek.p[k]).sqrMagnitude;
                if (d < best) { best = d; bridgeCross = road.s[i]; }
            }
        }
    }

    // ---------- macro height ----------

    static float Perlin(float x, float z, float scale, float ox, float oz) { return Mathf.PerlinNoise(x * scale + ox, z * scale + oz); }

    static float MacroNoise(float x, float z)
    {
        float n1 = Perlin(x, z, 0.0065f, 17.3f, 91.7f);
        float n2 = Perlin(x, z, 0.02f, 3.1f, 55.2f);
        float n3 = Perlin(x, z, 0.06f, 8.9f, 1.7f);
        float ridge = 1f - Mathf.Abs(2f * Perlin(x, z, 0.011f, 40f, 7f) - 1f);
        return (n1 - 0.5f) * 9f + (n2 - 0.5f) * 4.5f + (n3 - 0.5f) * 1.4f + (ridge - 0.5f) * 5f;
    }

    static void BuildPins()
    {
        pins.Clear();
        // x = R x, y = world height, z = R z
        Action<float, float, float> add = (x, z, h) => pins.Add(new Vector3(x, h, z));
        add(80, 80, 0); add(97, 72, 0); add(97, 88, 0); add(60, 120, 0); add(70, 30, 1); add(180, 40, 2); add(300, 30, 3.5f); add(430, 40, 4);
        add(250, 90, 1.5f); add(120, 160, 2.5f); add(210, 200, 4f); add(330, 150, 6f); add(470, 180, 11f); add(470, 330, 21f);
        add(320, 470, 29f); add(150, 470, 18f); add(60, 400, 12f); add(30, 250, 7f); add(200, 260, 8f); add(230, 380, 14f);
        add(HutPoi.x, HutPoi.y, HutLevel); add(130, 335, 11.5f); add(160, 265, 10f);
        add(TowerPoi.x, TowerPoi.y, TowerLevel); add(420, 405, 27f); add(400, 435, 27f); add(360, 415, 25f); add(430, 360, 25f);
        add(350, 280, 13f); add(300, 340, 15f); add(420, 300, 18f); add(280, 420, 20f);
        for (int i = 0; i < road.Count; i += 22) add(road.p[i].x, road.p[i].y, road.e[i]);
        add(road.p[road.Count - 1].x, road.p[road.Count - 1].y, road.e[road.Count - 1]);
    }

    static float Macro(float x, float z)
    {
        double sw = 0.0, sv = 0.0;
        for (int i = 0; i < pins.Count; i++)
        {
            float dx = x - pins[i].x, dz = z - pins[i].z;
            float d2 = dx * dx + dz * dz + 25f;
            double w = 1.0 / ((double)d2 * Math.Sqrt(d2));
            sw += w; sv += w * pins[i].y;
        }
        return (float)(sv / sw) + MacroNoise(x, z);
    }

    // ---------- footprint helpers ----------

    static float FootDist(float x, float z)
    {
        float dx = Mathf.Max(Mathf.Max(FootX0 - x, 0f), x - FootX1), dz = Mathf.Max(Mathf.Max(FootZ0 - z, 0f), z - FootZ1);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static float FootDepth(float x, float z) { return Mathf.Min(Mathf.Min(x - FootX0, FootX1 - x), Mathf.Min(z - FootZ0, FootZ1 - z)); }

    // ---------- footpath and creek profiles ----------

    static void BuildPathAndCreek()
    {
        // Creek bed: ground (macro) minus a gully depth, smoothed along the creek; raised sill at the ford.
        var bed = new float[creek.Count];
        for (int i = 0; i < creek.Count; i++) bed[i] = Macro(creek.p[i].x, creek.p[i].y) - 3.6f;
        Smooth(bed, 12, 3);
        // Ford: where the footpath meets the creek near the ford POI.
        float bestD = float.MaxValue;
        fordCreekIndex = 0; fordPathIndex = 0;
        for (int k = 0; k < creek.Count; k++)
        {
            if ((creek.p[k] - FordPoi).magnitude > 45f) continue;
            for (int j = 0; j < path.Count; j++)
            {
                float d = (creek.p[k] - path.p[j]).sqrMagnitude;
                if (d < bestD) { bestD = d; fordCreekIndex = k; fordPathIndex = j; }
            }
        }

        // Footpath elevation: ground following, pinned at the gate (0), hut pad, ford bed and the main road junction; slope limited.
        int n = path.Count;
        var pe = new float[n];
        for (int k = 0; k < n; k++) pe[k] = Macro(path.p[k].x, path.p[k].y);
        float roadEnd = RoadElev[8]; // the footpath ends on road control point 8
        float distFordEnd = path.s[n - 1] - path.s[fordPathIndex];
        float lowerBound = roadEnd - 0.17f * distFordEnd;
        fordLevel = Mathf.Max(bed[fordCreekIndex] + 0.45f, lowerBound);
        var pinned = new bool[n];
        pe[0] = 0f; pinned[0] = true;
        pe[n - 1] = roadEnd; pinned[n - 1] = true;
        pe[hutPathIndex] = HutLevel; pinned[hutPathIndex] = true;
        pe[fordPathIndex] = fordLevel; pinned[fordPathIndex] = true;
        float maxStep = 2f * 0.19f; // 2 m samples, 19 percent (about 10.8 degrees)
        for (int iter = 0; iter < 400; iter++)
        {
            for (int k = 1; k < n - 1; k++) if (!pinned[k]) pe[k] = 0.25f * pe[k - 1] + 0.5f * pe[k] + 0.25f * pe[k + 1];
            if (iter % 4 == 3)
            {
                for (int k = 1; k < n; k++) if (!pinned[k]) pe[k] = Mathf.Clamp(pe[k], pe[k - 1] - maxStep, pe[k - 1] + maxStep);
                for (int k = n - 2; k >= 0; k--) if (!pinned[k]) pe[k] = Mathf.Clamp(pe[k], pe[k + 1] - maxStep, pe[k + 1] + maxStep);
            }
        }
        Array.Copy(pe, path.e, n);
        float ps = 0f;
        for (int k = 1; k < n; k++) ps = Mathf.Max(ps, Mathf.Abs(pe[k] - pe[k - 1]) / (path.s[k] - path.s[k - 1]));
        maxPathSlopeDeg = Mathf.Atan(ps) * Mathf.Rad2Deg;

        // Ford sill: the bed rises to the path level around the crossing; shallow 0.25 m water there, 0.7 m elsewhere.
        creekWater = new float[creek.Count];
        creekFade = new float[creek.Count];
        for (int k = 0; k < creek.Count; k++)
        {
            float g = Mathf.Exp(-Mathf.Pow((creek.s[k] - creek.s[fordCreekIndex]) / 9f, 2f));
            bed[k] = Mathf.Lerp(bed[k], fordLevel, g);
            creekWater[k] = bed[k] + Mathf.Lerp(0.7f, 0.25f, g);
            creek.e[k] = bed[k];
            creekFade[k] = Ss(0f, 20f, Mathf.Min(creek.s[k], creek.length - creek.s[k]));
        }
    }

    static void Smooth(float[] a, int window, int passes)
    {
        var tmp = new float[a.Length];
        for (int p = 0; p < passes; p++)
        {
            for (int i = 0; i < a.Length; i++)
            {
                float sum = 0f; int c = 0;
                for (int k = -window; k <= window; k++) { int j = i + k; if (j < 0 || j >= a.Length) continue; sum += a[j]; c++; }
                tmp[i] = sum / c;
            }
            Array.Copy(tmp, a, a.Length);
        }
    }

    // ---------- height field ----------

    static void BuildHeights()
    {
        int N = HeightRes;
        roadF = new Field(N, CellSize, false, road, 15f);
        pathF = new Field(N, CellSize, false, path, 10f);
        creekF = new Field(N, CellSize, false, creek, 9f);
        heights = new float[N, N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float x = i * CellSize, z = j * CellSize;
                int idx = j * N + i;
                float h = Macro(x, z);

                // Perimeter ridge (not near the old map footprint, where the old walls stand).
                float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(RegionSize - x, RegionSize - z));
                float footDist = FootDist(x, z);
                float ridgeT = (1f - Ss(8f, 78f, edge)) * Ss(10f, 50f, footDist);
                float ridgeH = 24f + 9f * Perlin(x, z, 0.02f, 5f, 9f) + 4f * (Perlin(x, z, 0.07f, 21f, 4f) - 0.5f);
                h = Mathf.Lerp(h, Mathf.Max(h, ridgeH), ridgeT);

                // Apron around the old map: forced towards 0 so the walls meet the ground.
                h = Mathf.Lerp(h, 0f, 1f - Ss(3f, 14f, footDist));

                for (int p = 0; p < pads.Count; p++)
                {
                    float d = pads[p].Dist(x, z);
                    if (d < pads[p].falloff) h = Mathf.Lerp(h, pads[p].level, 1f - Ss(0f, pads[p].falloff, d));
                }

                // Creek channel: flat bed 3.2 m wide, soft banks to 7.5 m.
                if (creekF.d[idx] < 9f)
                {
                    float fade = creekFade[NearestIndex(creek, creekF.s[idx])];
                    float bank = Mathf.Lerp(creekF.e[idx], h, Ss(1.6f, 7.5f, creekF.d[idx]));
                    h = Mathf.Lerp(h, bank, fade);
                }

                // Footpath, then the main road (flat across the width plus shoulder, soft blend beyond).
                if (pathF.d[idx] < 8.1f) h = Mathf.Lerp(h, pathF.e[idx], 1f - Ss(2.1f, 8.1f, pathF.d[idx]));
                if (roadF.d[idx] < 13.5f)
                {
                    float w = 1f - Ss(5.5f, 13.5f, roadF.d[idx]);
                    w *= 1f - (1f - Ss(9f, 12.5f, Mathf.Abs(roadF.s[idx] - bridgeCross)));   // channel under the bridge stays open
                    h = Mathf.Lerp(h, roadF.e[idx] - 0.05f, w);
                }

                // Old map footprint: below the Ground box inside, back to 0 over the last metre before the wall face.
                float depth = FootDepth(x, z);
                if (depth > 0f) h = Mathf.Lerp(0f, -1.5f, Ss(0f, 1f, depth));

                heights[j, i] = Mathf.Clamp(h, TerrainOrigin.y + 0.5f, TerrainOrigin.y + TerrainHeightRange - 0.5f);
            }
    }

    static int NearestIndex(Poly poly, float arc) { return Mathf.Clamp(poly.IndexAt(arc), 0, poly.Count - 1); }

    // Bilinear sample of the generated height field (world y). Used before the Terrain exists.
    static float HeightAt(float rx, float rz)
    {
        float fx = Mathf.Clamp(rx / CellSize, 0f, HeightRes - 1.001f), fz = Mathf.Clamp(rz / CellSize, 0f, HeightRes - 1.001f);
        int i = (int)fx, j = (int)fz;
        float tx = fx - i, tz = fz - j;
        return Mathf.Lerp(Mathf.Lerp(heights[j, i], heights[j, i + 1], tx), Mathf.Lerp(heights[j + 1, i], heights[j + 1, i + 1], tx), tz);
    }

    // Ground height for placing objects: the Terrain collider data when it exists, else the generated field.
    public static float GroundY(float rx, float rz)
    {
        if (terrain != null) return terrain.SampleHeight(RW(rx, rz)) + TerrainOrigin.y;
        return HeightAt(rx, rz);
    }

    static float SlopeDegAt(float rx, float rz)
    {
        float e = 1.5f;
        float gx = (HeightAt(rx + e, rz) - HeightAt(rx - e, rz)) / (2f * e), gz = (HeightAt(rx, rz + e) - HeightAt(rx, rz - e)) / (2f * e);
        return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
    }

    // ---------- entry point ----------

    public static void ResetState()
    {
        pads.Clear(); pins.Clear();
        terrain = null;
        heights = null;
        treeWorld.Clear();
        lootDefs.Clear();
    }

    // Builds terrain, roads, bridge, water, vegetation and the prop buildings (everything except interactables, loot and zombies).
    public static void BuildWorld(SandboxBuilder.MaterialSet mats)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ResetState();
        LookDevSetup.EnsureFolders();
        EnsureFolders();
        BuildSplines();
        BuildPins();
        PlanLayout();
        BuildPathAndCreek();
        PlanLoot();
        BuildHeights();
        Note("open world: roads/creek splines built, road max slope " + F(maxRoadSlopeDeg) + " deg, footpath max slope " + F(maxPathSlopeDeg)
             + " deg (limits 8 / 12), ford bed y " + F(fordLevel) + ", station pad y " + F(stationLevel) + ", bridge crossing at road arc " + F(bridgeCross) + " m of " + F(road.length));
        BuildTerrain();
        Note("open world: terrain + splat + vegetation built in " + F((float)watch.Elapsed.TotalSeconds) + " s");
        BuildRoadMeshes(mats);
        BuildBridge(mats);
        BuildWater();
        BuildRoadProps(mats);
        BuildStation(mats);
        BuildHut(mats);
        BuildTower(mats);
        BuildRocksAndClutter(mats);
        Note("open world: roads, bridge, water and props done in " + F((float)watch.Elapsed.TotalSeconds) + " s (cumulative)");
        Note("TEMPORARY: station, hut, water tower, three houses, van wreck, bridge rails and the melee view model are box/primitive placeholders (no final art)");
    }

    static void EnsureFolders()
    {
        foreach (string f in new[] { "Terrain", "Prefabs", "Nav", "Icons", "Materials" })
            if (!AssetDatabase.IsValidFolder("Assets/Generated/" + f)) AssetDatabase.CreateFolder("Assets/Generated", f);
        if (!AssetDatabase.IsValidFolder("Assets/Generated/Prefabs/Trees")) AssetDatabase.CreateFolder("Assets/Generated/Prefabs", "Trees");
    }
}
