using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Measures the imported empty-reload (slide rack) clips against the LPSP clips they replace.
// Frames are 0-based at 30 fps: the new clip must match the source outside frames 34-72 and bind to the same transforms.
public static class ReloadFlourishValidation
{
    const float Fps = 30f;

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("VALIDATION FAILED: " + message);
        Debug.Log("PASS: " + message);
    }

    static AnimationClip Clip(string fbx, string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
    }

    sealed class Track { public AnimationCurve[] pos, rot, scale; }

    static Dictionary<string, Track> Tracks(AnimationClip clip)
    {
        var map = new Dictionary<string, Track>();
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.type != typeof(Transform)) continue;
            Track t;
            if (!map.TryGetValue(b.path, out t)) map[b.path] = t = new Track { pos = new AnimationCurve[3], rot = new AnimationCurve[4], scale = new AnimationCurve[3] };
            AnimationCurve c = AnimationUtility.GetEditorCurve(clip, b);
            int i = "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1]);
            if (b.propertyName.StartsWith("m_LocalPosition")) t.pos[i] = c;
            else if (b.propertyName.StartsWith("m_LocalRotation")) t.rot[i] = c;
            else if (b.propertyName.StartsWith("m_LocalScale")) t.scale[i] = c;
        }
        return map;
    }

    static Vector3 V(AnimationCurve[] c, float t, Vector3 fallback)
    {
        return new Vector3(c[0] != null ? c[0].Evaluate(t) : fallback.x, c[1] != null ? c[1].Evaluate(t) : fallback.y, c[2] != null ? c[2].Evaluate(t) : fallback.z);
    }

    static Quaternion Q(AnimationCurve[] c, float t)
    {
        if (c[0] == null) return Quaternion.identity;
        return new Quaternion(c[0].Evaluate(t), c[1].Evaluate(t), c[2].Evaluate(t), c[3].Evaluate(t));
    }

    static float First(Dictionary<string, Track> m)
    {
        float first = float.MaxValue;
        foreach (Track t in m.Values)
            foreach (AnimationCurve c in t.pos.Concat(t.rot))
                if (c != null && c.length > 0) first = Mathf.Min(first, c[0].time);
        return first;
    }

    // Worst difference over the frames; returns (metres, degrees, scale, path of the worst).
    static void Compare(string label, AnimationClip oldClip, AnimationClip newClip, IEnumerable<int> frames, float offsetOld, float offsetNew, HashSet<string> skip, out float maxPos, out float maxRot, out float maxScale)
    {
        Dictionary<string, Track> a = Tracks(oldClip), b = Tracks(newClip);
        maxPos = maxRot = maxScale = 0f;
        string wp = "", wr = "", ws = "";
        foreach (KeyValuePair<string, Track> kv in a)
        {
            Track o = kv.Value, n;
            if (skip != null && skip.Contains(kv.Key)) continue;
            if (!b.TryGetValue(kv.Key, out n)) continue;
            foreach (int f in frames)
            {
                float ta = f / Fps + offsetOld, tb = f / Fps + offsetNew;
                float d = (V(o.pos, ta, Vector3.zero) - V(n.pos, tb, Vector3.zero)).magnitude;
                if (o.pos[0] != null && n.pos[0] != null && d > maxPos) { maxPos = d; wp = kv.Key + "@" + f; }
                float r = Quaternion.Angle(Q(o.rot, ta), Q(n.rot, tb));
                if (o.rot[0] != null && n.rot[0] != null && r > maxRot) { maxRot = r; wr = kv.Key + "@" + f; }
                float s = (V(o.scale, ta, Vector3.one) - V(n.scale, tb, Vector3.one)).magnitude;
                if (o.scale[0] != null && n.scale[0] != null && s > maxScale) { maxScale = s; ws = kv.Key + "@" + f; }
            }
        }
        Debug.Log("COMPARE " + label + ": pos " + maxPos.ToString("F5") + " (" + wp + ") rot " + maxRot.ToString("F3") + " (" + wr + ") scale " + maxScale.ToString("F5") + " (" + ws + ")");
    }

    static void Bindings(string label, AnimationClip oldClip, AnimationClip newClip)
    {
        Dictionary<string, Track> a = Tracks(oldClip), b = Tracks(newClip);
        List<string> missing = a.Keys.Where(k => !b.ContainsKey(k)).ToList(), extra = b.Keys.Where(k => !a.ContainsKey(k)).ToList();
        Debug.Log("BINDINGS " + label + ": old " + a.Count + " new " + b.Count + " missing " + missing.Count + " [" + string.Join(", ", missing.Take(6)) + "] extra " + extra.Count + " [" + string.Join(", ", extra.Take(6)) + "]");
        Debug.Log("TIMING " + label + ": old length " + oldClip.length.ToString("F4") + " first key " + First(a).ToString("F4") + " rate " + oldClip.frameRate + " | new length " + newClip.length.ToString("F4") + " first key " + First(b).ToString("F4") + " rate " + newClip.frameRate);
    }

    public static void Run()
    {
        try
        {
            ViewmodelFactory.PrepareReloadFlourish();
            const string Root = "Assets/ThirdParty/InfimaLPSP/Art/";
            AnimationClip oldArms = Clip(Root + "Animations/Character/Handguns/A_FP_PCH_Handgun_Reload_Empty.fbx", "A_FP_PCH_Handgun_Reload_Empty");
            AnimationClip oldGun = Clip(Root + "Meshes/Weapons/Handguns/SK_Handgun_03.fbx", "A_FP_WEP_Handgun_03_Reload_Empty");
            AnimationClip newArms = Clip(ViewmodelFactory.ReloadFlourishArmsFbx, ViewmodelFactory.ReloadFlourishArmsClip);
            AnimationClip newGun = Clip(ViewmodelFactory.ReloadFlourishGunFbx, ViewmodelFactory.ReloadFlourishGunClip);
            Check(oldArms != null && oldGun != null && newArms != null && newGun != null, "all four clips load");
            Bindings("arms", oldArms, newArms);
            Bindings("gun", oldGun, newGun);
            Check(Mathf.Abs(newArms.length - 82f / Fps) < 0.002f, "arms clip length is 82 frames at 30 fps (" + newArms.length.ToString("F4") + ")");
            Check(Mathf.Abs(newGun.length - 82f / Fps) < 0.002f, "gun clip length is 82 frames at 30 fps (" + newGun.length.ToString("F4") + ")");

            Dictionary<string, Track> na = Tracks(newArms), oa = Tracks(oldArms), ng = Tracks(newGun), og = Tracks(oldGun);
            Check(oa.Keys.All(na.ContainsKey), "new arms clip binds every LPSP arms transform");
            Check(og.Keys.All(ng.ContainsKey), "new gun clip binds every LPSP gun transform");
            float offA = First(na) - First(oa), offG = First(ng) - First(og);
            Debug.Log("OFFSETS arms " + offA.ToString("F4") + " gun " + offG.ToString("F4"));

            float p, r, s;
            Compare("arms frames 0-33", oldArms, newArms, Enumerable.Range(0, 34), 0f, offA, null, out p, out r, out s);
            Check(p < 0.001f && r < 0.5f && s < 0.001f, "arms match the LPSP clip before the new section");
            Compare("arms frames 72-82", oldArms, newArms, Enumerable.Range(72, 11), 0f, offA, null, out p, out r, out s);
            Check(p < 0.001f && r < 0.5f && s < 0.001f, "arms match the LPSP clip after the new section");

            string slidePath = og.Keys.First(k => k.EndsWith("/slide") || k == "slide");
            Debug.Log("slide path " + slidePath);
            Compare("gun except slide, all frames", oldGun, newGun, Enumerable.Range(0, 83), 0f, offG, new HashSet<string> { slidePath }, out p, out r, out s);
            Check(p < 0.001f && r < 0.5f && s < 0.001f, "gun matches the LPSP clip except the slide");

            Func<int, float, Vector3> oldSlide = (f, d) => V(og[slidePath].pos, f / Fps + d, Vector3.zero);
            Func<int, float, Vector3> newSlide = (f, d) => V(ng[slidePath].pos, f / Fps + d, Vector3.zero);
            foreach (int f in new[] { 0, 33, 40, 44, 45, 46, 47, 60, 82 })
                Debug.Log("SLIDE frame " + f + " old " + oldSlide(f, 0f).ToString("F5") + " new " + newSlide(f, offG).ToString("F5"));
            float travel = (newSlide(45, offG) - newSlide(46, offG)).magnitude;
            float oldTravel = (oldSlide(33, 0f) - oldSlide(82, 0f)).magnitude;
            Debug.Log("slide slam travel new " + travel.ToString("F5") + " old locked-to-closed " + oldTravel.ToString("F5"));
            Check(travel > oldTravel * 0.95f && travel < oldTravel * 1.3f, "slide closes across 0-based frames 45-46 with LPSP scale travel");
            Check((newSlide(33, offG) - oldSlide(33, 0f)).magnitude < 0.001f, "slide starts locked back like the LPSP clip");
            Check((newSlide(82, offG) - oldSlide(82, 0f)).magnitude < 0.001f, "slide ends closed like the LPSP clip");
            Check(Mathf.Approximately(Zombosland.ViewmodelPresenter.ReloadEmptySlamSeconds, 46f / Fps), "camera shake fires on the slide slam frame");
            Debug.Log("RELOAD_FLOURISH_VALIDATION_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("RELOAD_FLOURISH_VALIDATION_FAILED: " + e);
            EditorApplication.Exit(1);
        }
    }
}
