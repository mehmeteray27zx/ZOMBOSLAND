using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombosland;

// Road ribbons, dashes, the bridge, the creek water, utility poles, guardrails and gate clutter.
internal static partial class OpenWorldBuilder
{
    static Transform worldRoot;

    static Transform Root()
    {
        if (worldRoot == null) worldRoot = new GameObject("OpenWorld").transform;
        return worldRoot;
    }

    static void MarkStatic(Transform t)
    {
        foreach (var tr in t.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic);
    }

    // Box with an arbitrary rotation (SandboxBuilder.Box only supports yaw).
    static GameObject BoxR(string name, Vector3 localPosition, Vector3 size, Quaternion rotation, Material mat, float tile, Transform parent)
    {
        var o = SandboxBuilder.Box(name, localPosition, size, mat, tile, parent);
        o.transform.localRotation = rotation;
        return o;
    }

    static GameObject VisualBox(string name, Vector3 localPosition, Vector3 size, Quaternion rotation, Material mat, Transform parent)
    {
        var o = BoxR(name, localPosition, size, rotation, mat, 1f, parent);
        UnityEngine.Object.DestroyImmediate(o.GetComponent<BoxCollider>());
        o.layer = 0;
        return o;
    }

    // Beam between two local points (length along the box z axis).
    static GameObject Brace(string name, Vector3 a, Vector3 b, float thickness, Material mat, Transform parent, bool collider)
    {
        Vector3 d = b - a;
        var o = BoxR(name, (a + b) * 0.5f, new Vector3(thickness, thickness, d.magnitude), Quaternion.LookRotation(d.normalized), mat, 1f, parent);
        if (!collider) { UnityEngine.Object.DestroyImmediate(o.GetComponent<BoxCollider>()); o.layer = 0; }
        return o;
    }

    // ---------- road surface ----------

    static bool OnBridge(float s) { return s >= road.ctrlS[7] && s <= road.ctrlS[8]; }

    static float BridgeTop(float s)
    {
        return Mathf.Lerp(RoadElev[7], RoadElev[8], Mathf.InverseLerp(road.ctrlS[7], road.ctrlS[8], s)) - 0.06f;
    }

    static Vector2 RightOf(Vector2 dir) { return new Vector2(dir.y, -dir.x); }

    static Vector3 RoadPoint(float s, float lateral, float yOffset = 0f)
    {
        int i = road.IndexAt(s);
        Vector2 p = road.PointAt(s) + RightOf(road.Dir(Mathf.Min(i, road.Count - 1))) * lateral;
        float y = OnBridge(s) ? BridgeTop(s) + 0.03f : GroundY(p.x, p.y) + (Mathf.Abs(lateral) >= RoadHalf - 0.01f ? 0.02f : 0.04f);
        return RW(p, y + yOffset);
    }

    static void BuildRoadMeshes(SandboxBuilder.MaterialSet mats)
    {
        var parent = Root();
        float[] offsets = { -RoadHalf, -RoadHalf * 0.5f, 0f, RoadHalf * 0.5f, RoadHalf };
        var grid = new Vector3[road.Count, 5];
        for (int r = 0; r < road.Count; r++)
            for (int c = 0; c < 5; c++) grid[r, c] = RoadPoint(road.s[r], offsets[c]);
        var mesh = ProceduralMeshes.Save("Proc_Road_Main", ProceduralMeshes.Ribbon(grid, 3f));
        var go = new GameObject("Road_Main");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mats.asphalt;

        // Faded, cracked centre dashes as a separate mesh.
        var rnd = new System.Random(8080);
        var quads = new List<Vector3[]>();
        for (float s = 10f; s < road.length - 12f; s += 7.5f)
        {
            if (rnd.NextDouble() < 0.22) continue; // missing dash
            float len = 2.6f - (float)rnd.NextDouble() * 0.9f;
            float s0 = s, s1 = s + len;
            if (OnBridge(s0) != OnBridge(s1)) continue;
            Vector3 a = RoadPoint(s0, -0.08f, 0.02f), b = RoadPoint(s0, 0.08f, 0.02f), c = RoadPoint(s1, 0.08f, 0.02f), d = RoadPoint(s1, -0.08f, 0.02f);
            quads.Add(new[] { a, b, c, d });
        }
        var dashMesh = ProceduralMeshes.Save("Proc_Road_Dashes", ProceduralMeshes.UpQuads(quads, 1f));
        var dashMat = SandboxBuilder.Mat("OW_RoadDash", new Color(.60f, .58f, .50f), .05f);
        var dash = new GameObject("Road_Dashes");
        dash.transform.SetParent(parent, false);
        dash.AddComponent<MeshFilter>().sharedMesh = dashMesh;
        dash.AddComponent<MeshRenderer>().sharedMaterial = dashMat;
        MarkStatic(go.transform); MarkStatic(dash.transform);
        Note("road: ribbon " + road.Count + " rows x 5 columns (2 m steps, +0.04 m over the lowered terrain), " + quads.Count + " centre dashes, no collider (the Terrain is the walking surface), length " + F(road.length) + " m");
    }

    // ---------- bridge ----------

    static void BuildBridge(SandboxBuilder.MaterialSet mats)
    {
        Vector3 a = RW(RoadCtrl[7], RoadElev[7]), b = RW(RoadCtrl[8], RoadElev[8]);
        Vector3 d = b - a;
        var bridge = new GameObject("Bridge").transform;
        bridge.SetParent(Root(), false);
        bridge.position = (a + b) * 0.5f;
        bridge.rotation = Quaternion.LookRotation(d.normalized);
        float length = d.magnitude;
        SandboxBuilder.Box("Bridge_Deck", new Vector3(0f, -0.06f - 0.35f, 0f), new Vector3(7.6f, 0.7f, length + 0.5f), mats.wall, 3f, bridge);
        Material rail = mats.rustyMetal;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 3.65f;
            VisualBox("Rail_Top_" + side, new Vector3(x, 0.82f, 0f), new Vector3(0.1f, 0.08f, length), Quaternion.identity, rail, bridge);
            VisualBox("Rail_Mid_" + side, new Vector3(x, 0.45f, 0f), new Vector3(0.08f, 0.08f, length), Quaternion.identity, rail, bridge);
            for (float z = -length * 0.5f + 0.2f; z <= length * 0.5f; z += 3.9f)
                VisualBox("Rail_Post", new Vector3(x, 0.4f, z), new Vector3(0.14f, 0.95f, 0.14f), Quaternion.identity, rail, bridge);
            var col = new GameObject("Rail_Collider_" + side);
            col.layer = Layers.Environment;
            col.transform.SetParent(bridge, false);
            col.transform.localPosition = new Vector3(x, 0.45f, 0f);
            col.AddComponent<BoxCollider>().size = new Vector3(0.2f, 0.9f, length);
        }
        MarkStatic(bridge);
        Note("bridge: deck 7.6 x " + F(length) + " m from road ctrl 7 to 8, top y " + F(RoadElev[7] - 0.06f) + " -> " + F(RoadElev[8] - 0.06f) + ", rails both sides; terrain carved below, end step about 0.01-0.05 m");
    }

    // ---------- water ----------

    static void BuildWater()
    {
        string path = "Assets/Generated/Materials/OW_Water.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", new Color(.08f, .14f, .17f, .78f));
        m.SetFloat("_Smoothness", .95f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", 5f);
        m.SetFloat("_DstBlend", 10f);
        m.SetFloat("_SrcBlendAlpha", 1f);
        m.SetFloat("_DstBlendAlpha", 10f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 2f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(m);

        float[] offsets = { -3f, -1.5f, 0f, 1.5f, 3f };
        var grid = new Vector3[creek.Count, 5];
        for (int r = 0; r < creek.Count; r++)
        {
            Vector2 right = RightOf(creek.Dir(r));
            float g = Mathf.Exp(-Mathf.Pow((creek.s[r] - creek.s[fordCreekIndex]) / 9f, 2f));
            float widthScale = 1f - 0.22f * g;
            for (int c = 0; c < 5; c++)
            {
                Vector2 p = creek.p[r] + right * offsets[c] * widthScale;
                grid[r, c] = RW(p, creekWater[r]);
            }
        }
        var mesh = ProceduralMeshes.Save("Proc_Creek_Water", ProceduralMeshes.Ribbon(grid, 4f));
        var go = new GameObject("Creek_Water");
        go.transform.SetParent(Root(), false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Note("water: creek ribbon " + creek.Count + " rows, no collider, ford at creek arc " + F(creek.s[fordCreekIndex]) + " m, bed y " + F(fordLevel) + ", depth 0.25 m, gravel banks in the splat map");
    }

    // ---------- poles, guardrails, gate clutter ----------

    static void BuildRoadProps(SandboxBuilder.MaterialSet mats)
    {
        var parent = new GameObject("RoadProps").transform;
        parent.SetParent(Root(), false);

        // Utility poles on the left of the road between the gate and the station.
        var poleMat = SandboxBuilder.Mat("OW_Pole", new Color(.24f, .17f, .11f), .05f);
        var poleMesh = ProceduralMeshes.Save("Proc_Pole", ProceduralMeshes.Cylinder(0.15f, 0.1f, 8f, 7, 1.5f));
        int poles = 0;
        for (float s = 36f; s < road.ctrlS[5] - 10f; s += 38f)
        {
            Vector3 pos = RoadPoint(s, -6.4f);
            pos.y = GroundY(pos.x + 46f, pos.z + 110f) - 0.1f;
            var pole = new GameObject("Pole_" + poles);
            pole.layer = Layers.Environment;
            pole.transform.SetParent(parent, false);
            pole.transform.position = pos;
            pole.AddComponent<MeshFilter>().sharedMesh = poleMesh;
            pole.AddComponent<MeshRenderer>().sharedMaterial = poleMat;
            var capsule = pole.AddComponent<CapsuleCollider>();
            capsule.radius = 0.15f; capsule.height = 8f; capsule.center = new Vector3(0f, 4f, 0f);
            Vector2 dir = road.Dir(road.IndexAt(s));
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            pole.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            VisualBox("Crossarm", new Vector3(0f, 7.4f, 0f), new Vector3(1.9f, 0.12f, 0.12f), Quaternion.identity, poleMat, pole.transform);
            VisualBox("Crossarm2", new Vector3(0f, 6.8f, 0f), new Vector3(1.5f, 0.1f, 0.1f), Quaternion.identity, poleMat, pole.transform);
            poles++;
        }

        // Guardrails on the outside of bends (not near the gate, the station or the bridge).
        var rail = mats.rustyMetal;
        int rails = 0;
        float railLength = 0f;
        int window = 7;
        for (int i = window; i < road.Count - window; i += 2)
        {
            float s = road.s[i];
            if (s < 90f || OnBridge(s) || s > road.ctrlS[7] - 30f && s < road.ctrlS[8] + 30f) continue;
            if (Mathf.Abs(s - road.s[road.Nearest(station.o)]) < 45f) continue;
            Vector2 d0 = road.Dir(i - window), d1 = road.Dir(i + window);
            float turn = Vector2.SignedAngle(d0, d1);
            if (Mathf.Abs(turn) < 17f) continue;
            float lateral = (turn > 0f ? 1f : -1f) * (RoadHalf + 1.5f);   // SignedAngle > 0 is a left turn (counter clockwise seen from above), so the outside is the right side (+lateral)
            Vector3 a = RoadPoint(road.s[i], lateral, 0.55f), b = RoadPoint(road.s[Mathf.Min(i + 2, road.Count - 1)], lateral, 0.55f);
            Brace("Guardrail", a, b, 0.14f, rail, parent, true);
            VisualBox("Guardrail_Post", RoadPoint(road.s[i], lateral, 0.4f), new Vector3(0.12f, 0.9f, 0.12f), Quaternion.identity, rail, parent);
            rails++; railLength += Vector3.Distance(a, b);
        }

        // Barrels and pallets just outside the gate, off the road.
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        var atlasMat = UrpLit("Assets/Generated/Materials/OW_Atlas.mat", Color.white, 0.12f, atlas, true);
        var timber = SandboxBuilder.Mat("OW_Timber", new Color(.30f, .21f, .13f), .08f);
        float gy = GroundY(104f, 64.6f);
        PlaceFbx("Assets/ThirdParty/DreamplexFarm/barrel_close_red.fbx", RW(104.2f, 64.8f, gy), 20f, 0.95f, true, atlasMat, parent);
        PlaceFbx("Assets/ThirdParty/DreamplexFarm/barrel_damaged_blue.fbx", RW(105.5f, 65.6f, GroundY(105.5f, 65.6f)), 70f, 0.95f, true, atlasMat, parent);
        var pallet = new GameObject("Pallets").transform;
        pallet.SetParent(parent, false);
        pallet.position = RW(108.4f, 64.4f, GroundY(108.4f, 64.4f));
        pallet.rotation = Quaternion.Euler(0f, 12f, 0f);
        SandboxBuilder.Box("Pallet_1", new Vector3(0f, 0.07f, 0f), new Vector3(1.2f, 0.14f, 1.0f), timber, 1f, pallet);
        SandboxBuilder.Box("Pallet_2", new Vector3(0.1f, 0.21f, 0.05f), new Vector3(1.2f, 0.14f, 1.0f), timber, 1f, pallet, 8f);
        MarkStatic(parent);
        Note("road props: " + poles + " utility poles, " + rails + " guardrail pieces (" + F(railLength) + " m) on bend exteriors, 2 barrels + 2 pallets outside the gate");
    }

    // Instantiates a model prefab scaled to `target` (height or widest horizontal extent), base centre at `position`, plus a box collider.
    static GameObject PlaceFbx(string fbxPath, Vector3 position, float yaw, float target, bool byHeight, Material material, Transform parent, bool collider = true)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null) { Note("WARNING missing model " + fbxPath); return null; }
        var holder = new GameObject(System.IO.Path.GetFileNameWithoutExtension(fbxPath));
        holder.transform.SetParent(parent, false);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        inst.transform.SetParent(holder.transform, false);
        Bounds b = default(Bounds);
        bool any = false;
        foreach (var r in inst.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int k = 0; k < mats.Length; k++) mats[k] = material;
            r.sharedMaterials = mats;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        foreach (var c in inst.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        if (!any) { UnityEngine.Object.DestroyImmediate(holder); return null; }
        float dim = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        float k2 = dim > 0.001f ? target / dim : 1f;
        inst.transform.localScale = Vector3.one * k2;
        inst.transform.localPosition = new Vector3(-b.center.x * k2, -b.min.y * k2, -b.center.z * k2);
        holder.transform.position = position;
        holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (collider)
        {
            holder.layer = Layers.Environment;
            var box = holder.AddComponent<BoxCollider>();
            box.size = b.size * k2;
            box.center = new Vector3(0f, b.size.y * k2 * 0.5f, 0f);
        }
        return holder;
    }
}
