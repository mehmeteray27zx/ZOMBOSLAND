using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombosland;

// Placeholder buildings: fuel station, hut, water tower with three houses, rocks. All boxes/primitives (temporary art), colliders on Environment.
internal static partial class OpenWorldBuilder
{
    struct Gap
    {
        public int side;      // 0 = +x wall, 1 = -x wall, 2 = +z wall, 3 = -z wall
        public float center, width, sill, top;
        public Gap(int side, float center, float width, float sill, float top) { this.side = side; this.center = center; this.width = width; this.sill = sill; this.top = top; }
    }

    static Material M(string name, float r, float g, float b, float smooth = 0.1f) { return SandboxBuilder.Mat(name, new Color(r, g, b), smooth); }

    static Transform FrameRoot(string name, Frame f, float level)
    {
        var t = new GameObject(name).transform;
        t.SetParent(Root(), false);
        t.position = RW(f.o, level);
        t.rotation = Quaternion.Euler(0f, f.yaw, 0f);
        return t;
    }

    // Four walls with door / window openings. Local y = 0 is the floor top; wall bottoms are sunk 0.15 m into the slab.
    static void Room(Transform parent, string name, Vector2 centre, float sx, float sz, float height, float thick, Material mat, float tile, Gap[] gaps)
    {
        const float baseY = -0.15f;
        var walls = new GameObject(name).transform;
        walls.SetParent(parent, false);
        for (int side = 0; side < 4; side++)
        {
            bool alongX = side >= 2;
            float length = alongX ? sx : sz - 2f * thick;
            float fixedPos = side == 0 ? centre.x + sx * 0.5f - thick * 0.5f : side == 1 ? centre.x - sx * 0.5f + thick * 0.5f
                           : side == 2 ? centre.y + sz * 0.5f - thick * 0.5f : centre.y - sz * 0.5f + thick * 0.5f;
            float centreAlong = alongX ? centre.x : centre.y;
            var list = new List<Gap>();
            foreach (Gap g in gaps) if (g.side == side) list.Add(g);
            list.Sort((a, b) => a.center.CompareTo(b.center));
            float cursor = -length * 0.5f;
            int sideCopy = side;
            Action<float, float, float, float> piece = (a0, a1, y0, y1) =>
            {
                if (a1 - a0 < 0.02f || y1 - y0 < 0.02f) return;
                float mid = (a0 + a1) * 0.5f, ym = (y0 + y1) * 0.5f;
                Vector3 pos = alongX ? new Vector3(centreAlong + mid, ym, fixedPos) : new Vector3(fixedPos, ym, centreAlong + mid);
                Vector3 size = alongX ? new Vector3(a1 - a0, y1 - y0, thick) : new Vector3(thick, y1 - y0, a1 - a0);
                SandboxBuilder.Box(name + "_Wall" + sideCopy, pos, size, mat, tile, walls);
            };
            foreach (Gap g in list)
            {
                float g0 = g.center - g.width * 0.5f, g1 = g.center + g.width * 0.5f;
                piece(cursor, g0, baseY, height);
                if (g.sill > baseY) piece(g0, g1, baseY, g.sill);
                if (g.top < height) piece(g0, g1, g.top, height);
                cursor = g1;
            }
            piece(cursor, length * 0.5f, baseY, height);
        }
    }

    static void Slab(Transform parent, string name, Vector2 centre, Vector2 size, Material mat)
    {
        SandboxBuilder.Box(name, new Vector3(centre.x, -0.15f, centre.y), new Vector3(size.x, 0.3f, size.y), mat, 2f, parent);
    }

    static Light PlaceLight(string name, Transform frame, Vector3 local, Color color, float intensity, float range, bool enabled)
    {
        var l = IlkIkmalBuilder.MakeLight(name, frame.TransformPoint(local), color, intensity, range, enabled);
        l.transform.SetParent(Root(), true);
        return l;
    }

    static Vector3 stationCenterWorld;

    // ---------- fuel station ----------

    static void BuildStation(SandboxBuilder.MaterialSet mats)
    {
        var root = FrameRoot("Station", station, stationLevel);
        Material green = M("OW_FadedGreen", .24f, .33f, .26f, .2f), red = M("OW_StationRed", .50f, .11f, .09f, .25f);
        Material sign = M("OW_SignPlain", .66f, .50f, .17f, .2f), dark = M("OW_DarkPanel", .06f, .07f, .08f, .5f), timber = M("OW_Timber", .30f, .21f, .13f, .08f);
        Material amber = IlkIkmalBuilder.Emissive("OW_LampAmber", new Color(.9f, .6f, .25f), new Color(2.0f, 1.2f, .4f));

        // Forecourt slab (24+ m wide strip along the road), top at the road level.
        Slab(root, "Forecourt", new Vector2(17.8f, 0f), new Vector2(28.4f, 30f), mats.floorWorn);

        // Fuel canopy: slab with a painted red band, four pillars, 4.45 m clearance.
        var canopy = new GameObject("Canopy").transform;
        canopy.SetParent(root, false);
        SandboxBuilder.Box("Canopy_Slab", new Vector3(11f, 4.7f, 0f), new Vector3(8f, 0.5f, 12f), green, 2f, canopy);
        VisualBox("Canopy_Band_A", new Vector3(11f, 4.62f, 0f), new Vector3(8.12f, 0.24f, 12.12f), Quaternion.identity, red, canopy);
        foreach (float px in new[] { 7.9f, 14.1f })
            foreach (float pz in new[] { -5.2f, 5.2f })
                SandboxBuilder.Box("Canopy_Pillar", new Vector3(px, 2.2f, pz), new Vector3(0.4f, 4.7f, 0.4f), mats.rustyMetal, 1f, canopy);
        VisualBox("Canopy_Underside_Lamp_A", new Vector3(11f, 4.42f, -3f), new Vector3(0.9f, 0.08f, 0.4f), Quaternion.identity, amber, canopy);
        VisualBox("Canopy_Underside_Lamp_B", new Vector3(11f, 4.42f, 3f), new Vector3(0.9f, 0.08f, 0.4f), Quaternion.identity, amber, canopy);

        // Two pumps.
        foreach (float pz in new[] { -2.6f, 2.6f })
        {
            var pump = new GameObject("Pump").transform;
            pump.SetParent(root, false);
            SandboxBuilder.Box("Pump_Body", new Vector3(11f, 0.7f, pz), new Vector3(0.7f, 1.5f, 0.5f), red, 1f, pump);
            VisualBox("Pump_Display", new Vector3(10.64f, 1.15f, pz), new Vector3(0.05f, 0.3f, 0.34f), Quaternion.identity, dark, pump);
            VisualBox("Pump_Top", new Vector3(11f, 1.5f, pz), new Vector3(0.78f, 0.1f, 0.58f), Quaternion.identity, mats.metalFlat, pump);
            SandboxBuilder.Box("Pump_Island", new Vector3(11f, 0.075f, pz), new Vector3(1.4f, 0.15f, 1.1f), mats.paintedConcrete, 1.5f, pump);
        }

        // Shop 6 (x) by 8 (z): front door (towards the canopy) and back door, two front windows, side windows; flat roof; walkable interior.
        var shop = new GameObject("Shop").transform;
        shop.SetParent(root, false);
        Room(shop, "Shop", new Vector2(23f, 0f), 6f, 8f, 3.0f, 0.3f, M("OW_Plaster", .66f, .62f, .54f, .08f), 1.5f, new[]
        {
            new Gap(1, 0f, 1.6f, 0f, 2.2f),        // front door gap
            new Gap(1, -2.7f, 1.4f, 1.0f, 2.1f), new Gap(1, 2.7f, 1.4f, 1.0f, 2.1f),
            new Gap(0, 2f, 1.6f, 0f, 2.2f),        // back door gap
            new Gap(2, 0f, 1.2f, 1.0f, 2.1f), new Gap(3, 0f, 1.2f, 1.0f, 2.1f)
        });
        SandboxBuilder.Box("Shop_Roof", new Vector3(23f, 3.15f, 0f), new Vector3(6.6f, 0.3f, 8.6f), mats.wall, 3f, shop);
        VisualBox("Shop_Sign", new Vector3(19.7f, 2.65f, 0f), new Vector3(0.1f, 0.5f, 3.2f), Quaternion.identity, sign, shop);
        SandboxBuilder.Box("Shop_Counter", new Vector3(24.9f, 0.5f, -1.5f), new Vector3(0.6f, 1.0f, 4.2f), timber, 1f, shop);
        SandboxBuilder.Box("Shop_Shelf", new Vector3(23f, 0.9f, -3.35f), new Vector3(2.6f, 1.8f, 0.5f), mats.rustyMetal, 1f, shop);

        // Fuel tank at the rear and a support saddle.
        var tankMesh = ProceduralMeshes.Save("Proc_FuelTank", ProceduralMeshes.Cylinder(1.1f, 1.1f, 6f, 18, 1.5f));
        var tank = new GameObject("Fuel_Tank");
        tank.layer = Layers.Environment;
        tank.transform.SetParent(root, false);
        tank.transform.localPosition = new Vector3(30f, 1.2f, 2f);
        tank.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tank.AddComponent<MeshFilter>().sharedMesh = tankMesh;
        tank.AddComponent<MeshRenderer>().sharedMaterial = green;
        var tankBox = tank.AddComponent<BoxCollider>();
        tankBox.size = new Vector3(2.2f, 6f, 2.2f); tankBox.center = new Vector3(0f, 3f, 0f);
        SandboxBuilder.Box("Tank_Saddle_A", new Vector3(30f, 0.2f, 3.2f), new Vector3(1.6f, 0.4f, 0.5f), mats.paintedConcrete, 1f, root);
        SandboxBuilder.Box("Tank_Saddle_B", new Vector3(30f, 0.2f, 6.8f), new Vector3(1.6f, 0.4f, 0.5f), mats.paintedConcrete, 1f, root);

        // Wrecked van beside the road (left side), a plain unbranded sign on a pole.
        var van = new GameObject("Van_Wreck").transform;
        van.SetParent(root, false);
        van.localPosition = new Vector3(-6.2f, 0f, -9f);
        van.localRotation = Quaternion.Euler(0f, 14f, 0f);
        SandboxBuilder.Box("Van_Body", new Vector3(0f, 0.75f, 0f), new Vector3(2.0f, 1.3f, 4.6f), mats.rustyMetal, 2f, van);
        SandboxBuilder.Box("Van_Cabin", new Vector3(0f, 1.7f, 1.0f), new Vector3(1.9f, 0.8f, 1.9f), mats.rustyMetal, 2f, van);
        VisualBox("Van_Windshield", new Vector3(0f, 1.75f, 2.0f), new Vector3(1.7f, 0.55f, 0.06f), Quaternion.Euler(-12f, 0f, 0f), mats.glass, van);
        VisualBox("Van_Wheel_Spare", new Vector3(1.2f, 0.35f, -0.4f), new Vector3(0.3f, 0.7f, 0.7f), Quaternion.Euler(0f, 0f, 20f), dark, van);
        SandboxBuilder.Box("Pole_Sign", new Vector3(6.5f, 2.8f, 12f), new Vector3(0.18f, 5.6f, 0.18f), mats.rustyMetal, 1f, root);
        SandboxBuilder.Box("Sign_Panel", new Vector3(6.5f, 5.0f, 12f), new Vector3(0.2f, 1.3f, 2.4f), sign, 1f, root);
        VisualBox("Sign_Stripe", new Vector3(6.38f, 5.0f, 12f), new Vector3(0.05f, 0.2f, 2.2f), Quaternion.identity, red, root);

        PlaceLight("Station_Shop_Light", root, new Vector3(23f, 2.7f, 0f), new Color(1f, .78f, .5f), 3.5f, 7f, true);
        stationCenterWorld = root.TransformPoint(new Vector3(16f, 0f, 0f));
        MarkStatic(root);
        Note("station: forecourt 28.4 x 30 m slab, canopy (clearance 4.45 m), 2 pumps, shop 6 x 8 m with front/back door gaps (1.6 m) and windows, tank, van wreck, plain sign; 3 point lights");
    }

    // ---------- hut ----------

    static void BuildHut(SandboxBuilder.MaterialSet mats)
    {
        var root = FrameRoot("Hut", hut, HutLevel);
        Material timber = M("OW_Timber", .30f, .21f, .13f, .08f), roofMat = mats.rustyMetal;
        Slab(root, "Hut_Floor", Vector2.zero, new Vector2(4.8f, 5.8f), mats.floorWorn);
        Room(root, "Hut", Vector2.zero, 4f, 5f, 2.5f, 0.15f, timber, 1.2f, new[]
        {
            new Gap(2, 0f, 1.6f, 0f, 2.1f),        // door gap on the +z side (faces the footpath)
            new Gap(0, 0.4f, 0.8f, 1.1f, 1.8f), new Gap(1, -0.4f, 0.8f, 1.1f, 1.8f)
        });
        var roofMesh = ProceduralMeshes.Save("Proc_Hut_Roof", ProceduralMeshes.GableRoof(4f, 5f, 1.1f, 0.35f, 2f));
        var roof = new GameObject("Hut_Roof");
        roof.transform.SetParent(root, false);
        roof.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        roof.AddComponent<MeshFilter>().sharedMesh = roofMesh;
        roof.AddComponent<MeshRenderer>().sharedMaterial = roofMat;
        SandboxBuilder.Box("Hut_Table", new Vector3(-1.2f, 0.4f, 0.4f), new Vector3(0.8f, 0.8f, 1.4f), timber, 1f, root);
        SandboxBuilder.Box("Hut_Bunk", new Vector3(1.3f, 0.3f, -1.4f), new Vector3(0.9f, 0.6f, 2.0f), timber, 1f, root);
        SandboxBuilder.Box("Hut_Woodpile", new Vector3(2.9f, 0.4f, -1.2f), new Vector3(0.7f, 0.8f, 1.6f), timber, 1f, root, 0f);
        MarkStatic(root);
        Note("hut: 4 x 5 m shack, door gap 1.6 m, two window gaps, gable roof, walkable interior, floor top y " + F(HutLevel));
    }

    // ---------- water tower and houses ----------

    static void BuildTower(SandboxBuilder.MaterialSet mats)
    {
        var root = new GameObject("WaterTower").transform;
        root.SetParent(Root(), false);
        root.position = RW(TowerPoi, TowerLevel);
        Material green = M("OW_FadedGreen", .24f, .33f, .26f, .2f), steel = mats.rustyMetal;
        const float legX = 2.4f;
        SandboxBuilder.Box("Tower_Footing", new Vector3(0f, -0.2f, 0f), new Vector3(6.4f, 0.4f, 6.4f), mats.paintedConcrete, 2f, root);
        foreach (float lx in new[] { -legX, legX })
            foreach (float lz in new[] { -legX, legX })
            {
                SandboxBuilder.Box("Tower_Leg", new Vector3(lx, 11.05f, lz), new Vector3(0.4f, 22.1f, 0.4f), steel, 1f, root);
                SandboxBuilder.Box("Tower_Foot", new Vector3(lx, 0.15f, lz), new Vector3(0.9f, 0.5f, 0.9f), mats.paintedConcrete, 1f, root);
            }
        // Bracing: X diagonals on every face at two levels (visual beams, no colliders).
        float[] levels = { 3f, 10f, 17f };
        for (int k = 0; k < levels.Length - 0; k++)
        {
            float y0 = levels[k], y1 = k + 1 < levels.Length ? levels[k + 1] : 21.8f;
            for (int face = 0; face < 4; face++)
            {
                Vector3 a = face == 0 ? new Vector3(-legX, y0, -legX) : face == 1 ? new Vector3(legX, y0, -legX) : face == 2 ? new Vector3(legX, y0, legX) : new Vector3(-legX, y0, legX);
                Vector3 b = face == 0 ? new Vector3(legX, y0, -legX) : face == 1 ? new Vector3(legX, y0, legX) : face == 2 ? new Vector3(-legX, y0, legX) : new Vector3(-legX, y0, -legX);
                Brace("Tower_Brace", a, new Vector3(b.x, y1, b.z), 0.14f, steel, root, false);
                Brace("Tower_Brace", b, new Vector3(a.x, y1, a.z), 0.14f, steel, root, false);
                if (k == 0 || k == 1) Brace("Tower_Strut", new Vector3(a.x, y1, a.z), new Vector3(b.x, y1, b.z), 0.16f, steel, root, false);
            }
        }
        SandboxBuilder.Box("Tower_Platform", new Vector3(0f, 21.95f, 0f), new Vector3(6.8f, 0.3f, 6.8f), steel, 1.5f, root);
        var tankMesh = ProceduralMeshes.Save("Proc_Tower_Tank", ProceduralMeshes.Cylinder(3f, 3f, 4.5f, 20, 2f));
        var tank = new GameObject("Tower_Tank");
        tank.layer = Layers.Environment;
        tank.transform.SetParent(root, false);
        tank.transform.localPosition = new Vector3(0f, 22.1f, 0f);
        tank.AddComponent<MeshFilter>().sharedMesh = tankMesh;
        tank.AddComponent<MeshRenderer>().sharedMaterial = green;
        var tankCol = tank.AddComponent<BoxCollider>();
        tankCol.size = new Vector3(6f, 4.5f, 6f); tankCol.center = new Vector3(0f, 2.25f, 0f);
        var capMesh = ProceduralMeshes.Save("Proc_Tower_Cap", ProceduralMeshes.Cylinder(3.15f, 0.3f, 1.4f, 20, 2f));
        var cap = new GameObject("Tower_Cap");
        cap.transform.SetParent(root, false);
        cap.transform.localPosition = new Vector3(0f, 26.55f, 0f);
        cap.AddComponent<MeshFilter>().sharedMesh = capMesh;
        cap.AddComponent<MeshRenderer>().sharedMaterial = mats.rustyMetal;
        VisualBox("Tower_Ladder_L", new Vector3(-0.3f, 11f, -legX - 0.25f), new Vector3(0.06f, 22f, 0.06f), Quaternion.identity, steel, root);
        VisualBox("Tower_Ladder_R", new Vector3(0.3f, 11f, -legX - 0.25f), new Vector3(0.06f, 22f, 0.06f), Quaternion.identity, steel, root);
        for (float y = 0.6f; y < 21.5f; y += 0.9f) VisualBox("Tower_Rung", new Vector3(0f, y, -legX - 0.25f), new Vector3(0.6f, 0.04f, 0.04f), Quaternion.identity, steel, root);
        MarkStatic(root);

        // Three small gabled houses on the tower hill; the first one has an accessible interior.
        for (int i = 0; i < 3; i++) BuildHouse(i, mats);
        Note("water tower: 4 legs with X bracing, tank at y +22 .. +26.6 m above the hill (hill pad y " + F(TowerLevel) + "), 3 houses (house 0 enterable)");
    }

    static void BuildHouse(int index, SandboxBuilder.MaterialSet mats)
    {
        Frame f = houses[index];
        var root = FrameRoot("House_" + index, f, TowerLevel);
        Material wall = index == 0 ? mats.wall : mats.paintedConcrete;
        Slab(root, "House_Floor", Vector2.zero, new Vector2(6.6f, 5.6f), mats.floorWorn);
        var roofMesh = ProceduralMeshes.Save("Proc_House_Roof", ProceduralMeshes.GableRoof(5f, 6f, 1.5f, 0.3f, 2f));
        var roof = new GameObject("House_Roof");
        roof.transform.SetParent(root, false);
        roof.transform.localPosition = new Vector3(0f, 2.6f, 0f);
        roof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // ridge along local x (the long side)
        roof.AddComponent<MeshFilter>().sharedMesh = roofMesh;
        roof.AddComponent<MeshRenderer>().sharedMaterial = mats.rustyMetal;
        if (index == 0)
        {
            // Door on the wall facing the tower.
            Vector2 toTower = TowerPoi - f.o;
            float dx = Vector2.Dot(toTower, f.Right), dz = Vector2.Dot(toTower, f.Fwd);
            int doorSide = Mathf.Abs(dx) > Mathf.Abs(dz) ? (dx > 0 ? 0 : 1) : (dz > 0 ? 2 : 3);
            var gaps = new List<Gap> { new Gap(doorSide, 0f, 1.5f, 0f, 2.1f) };
            foreach (int side in new[] { 0, 1, 2, 3 })
                if (side != doorSide) gaps.Add(new Gap(side, 0f, 1.2f, 1.0f, 2.0f));
            Room(root, "House", Vector2.zero, 6f, 5f, 2.6f, 0.25f, wall, 3f, gaps.ToArray());
            VisualBox("House_Table", new Vector3(0.9f, 0.4f, -1.4f), new Vector3(1.2f, 0.8f, 0.7f), Quaternion.identity, mats.rustyMetal, root);
            PlaceLight("Tower_House_Light", root, new Vector3(0f, 2.3f, 0f), new Color(1f, .76f, .5f), 3.5f, 7f, true);
            houseDoorSide = doorSide;
        }
        else
        {
            SandboxBuilder.Box("House_Solid", new Vector3(0f, 1.25f, 0f), new Vector3(6f, 2.7f, 5f), wall, 3f, root);
            VisualBox("Boarded_Door", new Vector3(0f, 1.05f, -2.52f), new Vector3(1.2f, 2.1f, 0.06f), Quaternion.identity, M("OW_Timber", .30f, .21f, .13f, .08f), root);
            VisualBox("Boarded_Window", new Vector3(2f, 1.5f, -2.52f), new Vector3(1.0f, 0.9f, 0.06f), Quaternion.identity, M("OW_Timber", .30f, .21f, .13f, .08f), root);
        }
        MarkStatic(root);
    }

    static int houseDoorSide;

    // ---------- rocks ----------

    static void BuildRocksAndClutter(SandboxBuilder.MaterialSet mats)
    {
        var parent = new GameObject("Rocks").transform;
        parent.SetParent(Root(), false);
        var rockMat = UrpLit("Assets/Generated/Materials/OW_RockGrey.mat", new Color(.34f, .33f, .30f), .12f);
        string dir = "Assets/ThirdParty/DreamplexFarm/";
        string[] models = { dir + "rock_small.fbx", dir + "rock medium.fbx", dir + "rocks.fbx" };
        float[] sizes = { 0.55f, 1.3f, 2.3f };
        bool[] solid = { false, true, true };
        var rnd = new System.Random(5150);
        int placed = 0, collidable = 0;
        for (int attempt = 0; attempt < 600 && placed < 80; attempt++)
        {
            float x = 10f + (float)rnd.NextDouble() * (RegionSize - 20f), z = 10f + (float)rnd.NextDouble() * (RegionSize - 20f);
            float pick = (float)rnd.NextDouble(), size = 0.8f + (float)rnd.NextDouble() * 0.6f, yaw = (float)rnd.NextDouble() * 360f, want = (float)rnd.NextDouble();
            int idx = roadF.Index(x, z);
            if (roadF.d[idx] < RoadHalf + 4f || pathF.d[idx] < PathHalf + 2.5f || creekF.d[idx] < 4f || FootDist(x, z) < 6f || NearLoot(x, z, 3f)) continue;
            bool onPad = false;
            for (int p = 0; p < pads.Count && !onPad; p++) onPad = pads[p].Dist(x, z) < 3f;
            if (onPad) continue;
            // Prefer steep ground, creek banks and the perimeter; sparse elsewhere.
            float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(RegionSize - x, RegionSize - z));
            float bias = Mathf.Max(Ss(14f, 30f, SlopeDegAt(x, z)), Mathf.Max(1f - Ss(4f, 18f, creekF.d[idx]), 1f - Ss(10f, 70f, edge))) * 0.8f + 0.12f;
            if (want > bias) continue;
            int kind = pick < 0.55f ? 0 : pick < 0.85f ? 1 : 2;
            var go = PlaceFbx(models[kind], RW(x, z, GroundY(x, z) - 0.08f), yaw, sizes[kind] * size, false, rockMat, parent, solid[kind]);
            if (go == null) break;
            placed++;
            if (solid[kind]) collidable++;
        }

        // Smooth pebbles on the ford banks (procedural, no collider).
        var pebbleMesh = ProceduralMeshes.Save("Proc_Pebble", ProceduralMeshes.Rock(0.3f, 77));
        int pebbles = 0;
        for (int k = -9; k <= 9; k++)
        {
            if (k == 0) continue;
            int i = Mathf.Clamp(fordCreekIndex + k * 3, 0, creek.Count - 1);
            Vector2 right = RightOf(creek.Dir(i));
            Vector2 p = creek.p[i] + right * ((k % 2 == 0 ? 1f : -1f) * (2.4f + (float)rnd.NextDouble() * 0.8f));
            var o = new GameObject("Pebble");
            o.transform.SetParent(parent, false);
            o.transform.position = RW(p, GroundY(p.x, p.y) - 0.05f);
            o.transform.rotation = Quaternion.Euler(0f, k * 47f, 0f);
            o.transform.localScale = Vector3.one * (0.7f + (float)rnd.NextDouble() * 0.9f);
            o.AddComponent<MeshFilter>().sharedMesh = pebbleMesh;
            o.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
            pebbles++;
        }
        MarkStatic(parent);
        Note("rocks: " + placed + " Dreamplex rocks (" + collidable + " with BoxCollider, small ones none) + " + pebbles + " ford pebbles");
    }
}
