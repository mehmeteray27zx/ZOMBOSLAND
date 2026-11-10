using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombosland;

// Terrain asset, layers, splat map, trees, grass details and the invisible perimeter walls.
internal static partial class OpenWorldBuilder
{
    const int TreeTarget = 1500;
    const string BirchSmall = "Assets/ThirdParty/DreamplexFarm/birch_small_2_green_leaves_LP.fbx";
    const string BirchMedium = "Assets/ThirdParty/DreamplexFarm/birch_medium_orange_leaves_LP.fbx";
    const string AtlasPath = "Assets/ThirdParty/DreamplexFarm/Texture_atlas_full_color_FREE_VERSION.png";

    static readonly List<Vector3> treeWorld = new List<Vector3>();
    static int pineCount, birchCount;
    static List<Vector2> sightPoints = new List<Vector2>();

    static Material UrpLit(string path, Color color, float smoothness, Texture baseMap = null, bool twoSided = false)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
        if (twoSided) m.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static TerrainLayer MakeLayer(string id, string polyHavenId, float tile, float smoothness)
    {
        var tex = ProceduralTextures.TerrainLayer(id, polyHavenId);
        string path = TerrainFolder + "/Layer_" + id + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
        layer.diffuseTexture = tex.albedo;
        layer.normalMapTexture = tex.normal;
        layer.tileSize = new Vector2(tile, tile);
        layer.smoothness = smoothness;
        layer.metallic = 0f;
        layer.normalScale = 1f;
        EditorUtility.SetDirty(layer);
        Note("terrain layer " + id + ": " + tex.source + ", tile " + tile + " m");
        return layer;
    }

    static void BuildTerrain()
    {
        int N = HeightRes;
        var layers = new[]
        {
            MakeLayer("dirt", "forest_ground_04", 11f, 0.08f),
            MakeLayer("gravel", "gravel_road", 9f, 0.12f),
            MakeLayer("grass", "aerial_grass_rock", 13f, 0.05f),
            MakeLayer("rock", "rock_face_03", 14f, 0.15f)
        };

        string dataPath = TerrainFolder + "/OpenRegion.asset";
        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
        if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, dataPath); }
        td.treeInstances = new TreeInstance[0];
        td.heightmapResolution = HeightRes;
        td.size = new Vector3(RegionSize, TerrainHeightRange, RegionSize);
        var normalized = new float[N, N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++) normalized[j, i] = Mathf.Clamp01((heights[j, i] - TerrainOrigin.y) / TerrainHeightRange);
        td.SetHeights(0, 0, normalized);
        td.alphamapResolution = AlphaRes;
        td.baseMapResolution = 1024;
        td.terrainLayers = layers;
        td.SetAlphamaps(0, 0, BuildSplat());

        string matPath = TerrainFolder + "/OpenRegion_Terrain.mat";
        var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (terrainMat == null) { terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); AssetDatabase.CreateAsset(terrainMat, matPath); }
        terrainMat.shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        terrainMat.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(terrainMat);

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.position = TerrainOrigin;
        go.layer = Layers.Environment;
        terrain = go.GetComponent<Terrain>();
        terrain.materialTemplate = terrainMat;
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 4f;
        terrain.treeDistance = 420f;
        terrain.treeBillboardDistance = 2000f; // these pines are plain meshes: Terrain billboards of them render as broken strips
        terrain.treeMaximumFullLODCount = 4000;
        terrain.treeMaximumFullLODCount = 400;
        terrain.detailObjectDistance = 70f;
        terrain.detailObjectDensity = 0.7f;
        terrain.basemapDistance = 400f;
        terrain.allowAutoConnect = false;

        PlaceTrees(td);
        PaintDetails(td);
        BuildPerimeterWalls();
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        Note("terrain: " + HeightRes + " heightmap, " + AlphaRes + " alphamap, 4 layers, " + (pineCount + birchCount) + " trees (" + pineCount + " pine, " + birchCount
             + " birch), height range " + F(MinHeight()) + " .. " + F(MaxHeight()) + " m, asset " + dataPath);
    }

    static float MinHeight() { float m = float.MaxValue; foreach (float h in heights) m = Mathf.Min(m, h); return m; }
    static float MaxHeight() { float m = float.MinValue; foreach (float h in heights) m = Mathf.Max(m, h); return m; }

    // ---------- splat ----------

    static float[,,] BuildSplat()
    {
        int A = AlphaRes;
        float cs = RegionSize / A;
        var rf = new Field(A, cs, true, road, 12f);
        var pf = new Field(A, cs, true, path, 8f);
        var cf = new Field(A, cs, true, creek, 10f);
        var map = new float[A, A, 4];
        for (int j = 0; j < A; j++)
            for (int i = 0; i < A; i++)
            {
                float x = (i + 0.5f) * cs, z = (j + 0.5f) * cs;
                int idx = j * A + i;
                float slope = SlopeDegAt(x, z);
                float padD = 1e9f;
                for (int p = 0; p < pads.Count; p++) padD = Mathf.Min(padD, pads[p].Dist(x, z));
                float footD = FootDist(x, z);

                float gravel = 1f - Ss(RoadHalf + 0.6f, RoadHalf + 4.2f, rf.d[idx]);
                gravel = Mathf.Max(gravel, 1f - Ss(0.5f, 3.5f, padD));
                gravel = Mathf.Max(gravel, 0.9f * (1f - Ss(2f, 9f, footD)));
                gravel = Mathf.Max(gravel, 1f - Ss(1.8f, 4.2f, cf.d[idx]));

                float dirt = 1f - Ss(PathHalf + 0.2f, PathHalf + 1.6f, pf.d[idx]);
                dirt = Mathf.Max(dirt, 0.85f * (1f - Ss(3.5f, 8f, cf.d[idx])));
                dirt = Mathf.Max(dirt, 0.8f * Ss(0.62f, 0.78f, Perlin(x, z, 0.045f, 12f, 34f)));
                dirt *= 1f - gravel * 0.7f;
                float rear = RearStripWeight(x, z); // dark trodden soil along the rear route
                if (rear > 0f) { gravel *= 1f - rear; dirt = Mathf.Max(dirt * (1f - rear), rear * 0.95f); }

                float rock = Ss(35f, 44f, slope) * (1f - gravel * 0.8f);
                float grass = Mathf.Max(0f, 1f - Mathf.Max(gravel, Mathf.Max(dirt, rock)));
                float sum = dirt + gravel + grass + rock;
                if (sum < 1e-4f) { grass = 1f; sum = 1f; }
                map[j, i, 0] = dirt / sum; map[j, i, 1] = gravel / sum; map[j, i, 2] = grass / sum; map[j, i, 3] = rock / sum;
            }
        return map;
    }

    // ---------- trees ----------

    struct TreeCandidate
    {
        public float x, z, priority, scaleH, scaleW, rotation;
        public int proto;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    static bool NearLoot(float x, float z, float radius)
    {
        float r2 = radius * radius;
        foreach (LootDef l in lootDefs) { float dx = l.rx - x, dz = l.rz - z; if (dx * dx + dz * dz < r2) return true; }
        return false;
    }

    static void PlaceTrees(TerrainData td)
    {
        // Tree prototypes: 3 procedural pines, 2 birches.
        var bark = UrpLit("Assets/Generated/Materials/OW_PineBark.mat", new Color(.20f, .14f, .10f), 0.05f);
        var needleA = UrpLit("Assets/Generated/Materials/OW_PineNeedle_A.mat", new Color(.20f, .31f, .16f), 0.05f);
        var needleB = UrpLit("Assets/Generated/Materials/OW_PineNeedle_B.mat", new Color(.24f, .35f, .17f), 0.05f);
        var needleC = UrpLit("Assets/Generated/Materials/OW_PineNeedle_C.mat", new Color(.17f, .28f, .17f), 0.05f);
        Material[] needles = { needleA, needleB, needleC };
        var protos = new List<TreePrototype>();
        for (int v = 0; v < 3; v++) protos.Add(new TreePrototype { prefab = MakePinePrefab(v, bark, needles[v]), bendFactor = 0f });
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        var birchMat = UrpLit("Assets/Generated/Materials/OW_Birch.mat", Color.white, 0.05f, atlas, true);
        GameObject birchA = MakeBirchPrefab("Birch_Small", BirchSmall, birchMat, 6.0f), birchB = MakeBirchPrefab("Birch_Medium", BirchMedium, birchMat, 8.0f);
        bool haveBirch = birchA != null && birchB != null;
        if (haveBirch) { protos.Add(new TreePrototype { prefab = birchA }); protos.Add(new TreePrototype { prefab = birchB }); }
        else Note("WARNING birch FBX missing, only pines are planted");
        td.treePrototypes = protos.ToArray();

        // Sight lanes: keep the water tower visible from the road (plan-view segments road -> tower).
        sightPoints.Clear();
        for (float s = 110f; s < road.length - 30f; s += 28f) sightPoints.Add(road.PointAt(s));

        var rnd = new System.Random(424242);
        var candidates = new List<TreeCandidate>();
        const float cell = 4.5f;
        for (float cz = 0f; cz < RegionSize; cz += cell)
            for (float cx = 0f; cx < RegionSize; cx += cell)
            {
                // Fixed number of random draws per cell keeps the result identical between runs.
                float x = cx + (float)rnd.NextDouble() * cell, z = cz + (float)rnd.NextDouble() * cell;
                float u = (float)rnd.NextDouble(), kind = (float)rnd.NextDouble(), sh = (float)rnd.NextDouble(), sw = (float)rnd.NextDouble(), rot = (float)rnd.NextDouble();
                if (x < 3f || z < 3f || x > RegionSize - 3f || z > RegionSize - 3f) continue;
                int idx = roadF.Index(x, z);
                float dRoad = roadF.d[idx], dPath = pathF.d[idx], dCreek = creekF.d[idx];
                if (dRoad < RoadHalf + 5.5f || dPath < PathHalf + 3f || dCreek < 6.5f) continue;
                if (FootDist(x, z) < 5f) continue;
                if (roadF.s[idx] < 110f && dRoad < 26f) continue; // gate approach stays open
                bool onPad = false;
                for (int p = 0; p < pads.Count && !onPad; p++) onPad = pads[p].Dist(x, z) < 3.5f;
                if (onPad) continue;
                float slope = SlopeDegAt(x, z);
                if (slope > 36f) continue;
                if (NearLoot(x, z, 4.5f)) continue;
                Vector2 q = new Vector2(x, z);
                bool blocksView = false;
                foreach (Vector2 sp in sightPoints) if (DistToSegment(q, sp, TowerPoi) < 4.5f) { blocksView = true; break; }
                if (blocksView) continue;

                float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(RegionSize - x, RegionSize - z));
                float forest = Ss(0.40f, 0.62f, Perlin(x, z, 0.011f, 2.5f, 8.5f));
                float p0 = Mathf.Lerp(0.10f, 1f, forest);
                p0 = Mathf.Max(p0, (1f - Ss(0f, 55f, edge)) * 0.95f);
                p0 *= Mathf.Lerp(0.15f, 1f, Ss(RoadHalf + 5.5f, 45f, dRoad)) * Mathf.Lerp(0.3f, 1f, Ss(PathHalf + 3f, 18f, dPath));
                p0 *= 1f - Ss(26f, 36f, slope);
                if (p0 < 0.01f) continue;

                bool birch = haveBirch && (Perlin(x, z, 0.018f, 90f, 33f) > 0.62f || (dCreek < 28f && kind < 0.5f));
                var c = new TreeCandidate { x = x, z = z, priority = u / p0, rotation = rot * Mathf.PI * 2f };
                if (birch) { c.proto = 3 + (kind < 0.5f ? 0 : 1); c.scaleH = 0.8f + sh * 0.4f; c.scaleW = 0.85f + sw * 0.3f; }
                else { c.proto = Mathf.Min(2, (int)(kind * 3f)); c.scaleH = 0.85f + sh * 0.5f; c.scaleW = 0.85f + sw * 0.35f; }
                candidates.Add(c);
            }
        candidates.Sort((a, b) => a.priority.CompareTo(b.priority));
        int take = Mathf.Min(TreeTarget, candidates.Count);
        var instances = new TreeInstance[take];
        treeWorld.Clear();
        pineCount = birchCount = 0;
        for (int i = 0; i < take; i++)
        {
            TreeCandidate c = candidates[i];
            instances[i] = new TreeInstance
            {
                position = new Vector3(c.x / RegionSize, 0f, c.z / RegionSize), widthScale = c.scaleW, heightScale = c.scaleH, rotation = c.rotation,
                color = new Color32(255, 255, 255, 255), lightmapColor = new Color32(255, 255, 255, 255), prototypeIndex = c.proto
            };
            treeWorld.Add(RW(c.x, c.z, HeightAt(c.x, c.z)));
            if (c.proto < 3) pineCount++; else birchCount++;
        }
        td.SetTreeInstances(instances, true);
    }

    static GameObject MakePinePrefab(int variant, Material bark, Material needle)
    {
        var mesh = ProceduralMeshes.Save("Proc_Pine_" + variant, ProceduralMeshes.Pine(variant));
        var go = new GameObject("Pine_" + variant);
        go.layer = Layers.Environment;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, needle };
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.radius = 0.35f; capsule.height = 5f; capsule.center = new Vector3(0f, 2.5f, 0f);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Generated/Prefabs/Trees/Pine_" + variant + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject MakeBirchPrefab(string name, string fbxPath, Material material, float targetHeight)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null) return null;
        var root = new GameObject(name);
        root.layer = Layers.Environment;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
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
        float scale = any && b.size.y > 0.01f ? targetHeight / b.size.y : 1f;
        inst.transform.SetParent(root.transform, false);
        inst.transform.localScale = Vector3.one * scale;
        inst.transform.localPosition = new Vector3(-b.center.x * scale, -b.min.y * scale, -b.center.z * scale);
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.radius = 0.3f; capsule.height = Mathf.Min(4.5f, targetHeight * 0.7f); capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Generated/Prefabs/Trees/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------- grass details ----------

    static void PaintDetails(TerrainData td)
    {
        const int res = 512;
        var olive = ProceduralTextures.GrassTuft("grass_tuft_olive", new Color(.13f, .18f, .08f), new Color(.29f, .35f, .16f), 5);
        var dry = ProceduralTextures.GrassTuft("grass_tuft_dry", new Color(.22f, .20f, .10f), new Color(.45f, .41f, .23f), 9);
        td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode); // layer values are per-cell instance counts, not 0-255 coverage
        td.SetDetailResolution(res, 16);
        td.detailPrototypes = new[]
        {
            new DetailPrototype { prototypeTexture = olive, usePrototypeMesh = false, renderMode = DetailRenderMode.GrassBillboard, minWidth = 0.55f, maxWidth = 1.0f, minHeight = 0.35f, maxHeight = 0.7f, noiseSpread = 0.6f, healthyColor = new Color(.8f, .8f, .75f), dryColor = new Color(.72f, .68f, .58f) },
            new DetailPrototype { prototypeTexture = dry, usePrototypeMesh = false, renderMode = DetailRenderMode.GrassBillboard, minWidth = 0.5f, maxWidth = 0.9f, minHeight = 0.3f, maxHeight = 0.6f, noiseSpread = 0.5f, healthyColor = new Color(.8f, .78f, .72f), dryColor = new Color(.75f, .72f, .65f) }
        };
        var layerA = new int[res, res];
        var layerB = new int[res, res];
        float cs = RegionSize / res;
        int cells = 0;
        for (int j = 0; j < res; j++)
            for (int i = 0; i < res; i++)
            {
                float x = (i + 0.5f) * cs, z = (j + 0.5f) * cs;
                int idx = roadF.Index(x, z);
                if (roadF.d[idx] < RoadHalf + 1.6f || pathF.d[idx] < PathHalf + 0.8f || creekF.d[idx] < 3.4f) continue;
                if (FootDist(x, z) < 4f || FootDepth(x, z) > 0f) continue;
                bool blocked = false;
                for (int p = 0; p < pads.Count && !blocked; p++) blocked = pads[p].Dist(x, z) < 2.5f;
                if (blocked) continue;
                if (SlopeDegAt(x, z) > 30f) continue;
                if (NearLoot(x, z, 2.8f)) continue;
                float mask = Ss(0.30f, 0.62f, Perlin(x, z, 0.035f, 61f, 17f)) * Mathf.Lerp(0.35f, 1f, Perlin(x, z, 0.13f, 4f, 40f));
                mask *= Ss(RoadHalf + 1.6f, RoadHalf + 7f, roadF.d[idx]);
                float dryFrac = Ss(0.45f, 0.7f, Perlin(x, z, 0.02f, 77f, 12f));
                int total = Mathf.RoundToInt(mask * 5f);
                if (total <= 0) continue;
                int b = Mathf.RoundToInt(total * dryFrac);
                layerA[j, i] = total - b;
                layerB[j, i] = b;
                cells++;
            }
        td.SetDetailLayer(0, 0, 0, layerA);
        td.SetDetailLayer(0, 0, 1, layerB);
        Note("grass details: 2 billboard prototypes, " + cells + " painted cells of " + res * res + ", cleared around roads, pads, creek and loot");
    }

    // ---------- perimeter walls ----------

    static void BuildPerimeterWalls()
    {
        var root = new GameObject("PerimeterWalls");
        float mid = TerrainOrigin.y + 30f;
        for (int k = 0; k < 4; k++)
        {
            var wall = new GameObject("Perimeter_" + (k == 0 ? "West" : k == 1 ? "East" : k == 2 ? "South" : "North"));
            wall.layer = Layers.Environment;
            wall.transform.SetParent(root.transform, false);
            wall.AddComponent<NavIgnore>();
            var box = wall.AddComponent<BoxCollider>();
            const float inset = 6f, thick = 2f;
            if (k < 2)
            {
                float x = k == 0 ? inset : RegionSize - inset;
                wall.transform.position = RW(x, RegionSize * 0.5f, mid);
                box.size = new Vector3(thick, 100f, RegionSize);
            }
            else
            {
                float z = k == 2 ? inset : RegionSize - inset;
                wall.transform.position = RW(RegionSize * 0.5f, z, mid);
                box.size = new Vector3(RegionSize, 100f, thick);
            }
        }
        Note("perimeter: 4 invisible BoxCollider walls 6 m inside the border (Environment layer, NavIgnore)");
    }
}
