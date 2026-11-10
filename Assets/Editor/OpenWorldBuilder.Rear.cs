using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombosland;

// v0.8 station rear: supply cabinet inside the shop, rear fence with a gate gap, the rear approach route and its small details.
// All coordinates are in the station frame (station.ToR): +x from the road into the complex, -z towards the yard side.
internal static partial class OpenWorldBuilder
{
    // Rear route (road -> south of the fence -> east side -> gate gap -> shop back door) and the front route (road -> forecourt -> shop front door -> cabinet).
    static readonly Vector2[] RearRouteLocal =
    {
        new Vector2(4f, -22f), new Vector2(20f, -22f), new Vector2(38f, -20f), new Vector2(38f, 0f), new Vector2(32f, 0f), new Vector2(28f, 0f), new Vector2(26f, 2f)
    };
    static readonly Vector2[] FrontRouteLocal = { new Vector2(0f, -26f), new Vector2(10f, 0f), new Vector2(21.5f, 0f), new Vector2(25f, 2.3f) };
    // The four station zombies; all in the front half (spec: the rear stays empty).
    static readonly Vector2[] StationZombieLocal = { new Vector2(15.5f, -9f), new Vector2(17.5f, 9f), new Vector2(12f, 6.5f), new Vector2(9f, -3.5f) };

    static readonly Vector2 CabinetLocal = new Vector2(25.0f, 3.45f);
    const float CabinetWidth = 1.1f, CabinetDepth = 0.45f, CabinetHeight = 1.9f, CabinetShelfTop = 1.05f;

    static Vector2[] rearRouteR; // R coordinates, filled in PlanLayout (terrain splat uses it)

    // 0..1: how much of the rear route's trodden-soil strip covers this point (R coordinates).
    static float RearStripWeight(float x, float z)
    {
        if (rearRouteR == null) return 0f;
        Vector2 q = new Vector2(x, z);
        float best = float.MaxValue;
        for (int i = 0; i + 1 < rearRouteR.Length; i++)
        {
            Vector2 a = rearRouteR[i], b = rearRouteR[i + 1];
            if (q.x < Mathf.Min(a.x, b.x) - 3f || q.x > Mathf.Max(a.x, b.x) + 3f || q.y < Mathf.Min(a.y, b.y) - 3f || q.y > Mathf.Max(a.y, b.y) + 3f) continue;
            best = Mathf.Min(best, DistToSegment(q, a, b));
        }
        return best >= 2.2f ? 0f : 1f - Ss(0.9f, 2.2f, best);
    }

    static Vector3[] StationWorldPoints(Vector2[] local, float lift = 0f)
    {
        var pts = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
        {
            Vector2 r = station.ToR(local[i].x, local[i].y);
            pts[i] = RW(r, SurfaceY(r.x, r.y) + lift);
        }
        return pts;
    }

    static void BuildRear(SandboxBuilder.MaterialSet mats, Transform interactables, QuestFuse quest)
    {
        Physics.SyncTransforms();
        // Layout first: cabinet, fence, routes.
        Transform cabinet = BuildCabinet(mats, interactables);
        quest.cacheMarker = cabinet.position + Vector3.up * 1.0f;
        quest.rearRoute = StationWorldPoints(RearRouteLocal);
        quest.frontRoute = StationWorldPoints(FrontRouteLocal);
        quest.stationZombieSpots = StationWorldPoints(StationZombieLocal);
        Transform rear = FrameRoot("StationRear", station, stationLevel);
        BuildRearFence(rear);
        // Environment detail last.
        BuildRearDetails(mats, rear);
        Note("station rear: cabinet Cache_Cabinet at local (" + F(CabinetLocal.x) + ", " + F(CabinetLocal.y) + "), 2 m fence at x=32 (gate gap -0.9..0.9) and z=-15.3, rear route of "
             + RearRouteLocal.Length + " points, 2 extra pads, 1 front zombie moved");
    }

    // ---------- supply cabinet ----------

    static Transform BuildCabinet(SandboxBuilder.MaterialSet mats, Transform interactables)
    {
        Material steel = M("OW_CabinetGreen", .20f, .27f, .22f, .25f), dark = M("OW_CabinetInside", .09f, .10f, .09f, .1f);
        const float W = CabinetWidth, D = CabinetDepth, H = CabinetHeight;
        var cab = new GameObject("Cache_Cabinet").transform;
        cab.SetParent(interactables, false);
        Vector2 r = station.ToR(CabinetLocal.x, CabinetLocal.y);
        cab.position = RW(r, stationLevel);
        cab.rotation = Quaternion.Euler(0f, station.yaw, 0f);

        // Shell: back, both sides, top and bottom (Environment colliders). The front is the door leaf.
        SandboxBuilder.Box("Cabinet_Back", new Vector3(0f, H * 0.5f, D * 0.5f - 0.02f), new Vector3(W, H, 0.04f), steel, 1f, cab);
        SandboxBuilder.Box("Cabinet_Side_L", new Vector3(-W * 0.5f + 0.02f, H * 0.5f, 0f), new Vector3(0.04f, H, D), steel, 1f, cab);
        SandboxBuilder.Box("Cabinet_Side_R", new Vector3(W * 0.5f - 0.02f, H * 0.5f, 0f), new Vector3(0.04f, H, D), steel, 1f, cab);
        SandboxBuilder.Box("Cabinet_Top", new Vector3(0f, H - 0.02f, 0f), new Vector3(W, 0.04f, D), steel, 1f, cab);
        SandboxBuilder.Box("Cabinet_Bottom", new Vector3(0f, 0.02f, 0f), new Vector3(W, 0.04f, D), steel, 1f, cab);
        // Shelves are visual only, so nothing inside the cabinet blocks the sight line to the bag.
        IlkIkmalBuilder.Visual("Cabinet_Shelf_Upper", new Vector3(0f, CabinetShelfTop - 0.02f, 0f), new Vector3(W - 0.08f, 0.04f, D - 0.06f), dark, cab);
        IlkIkmalBuilder.Visual("Cabinet_Shelf_Lower", new Vector3(0f, 0.5f, 0f), new Vector3(W - 0.08f, 0.04f, D - 0.06f), dark, cab);

        // Door: hinge on the left edge (seen from the front), the leaf swings out into the room.
        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(cab, false);
        hinge.localPosition = new Vector3(-W * 0.5f + 0.02f, 0f, -D * 0.5f - 0.02f);
        hinge.gameObject.AddComponent<NavIgnore>();
        var leaf = SandboxBuilder.Box("Cabinet_Door", new Vector3(W * 0.5f - 0.02f, 0.04f + (H - 0.08f) * 0.5f, 0f), new Vector3(W - 0.04f, H - 0.08f, 0.03f), steel, 1f, hinge);
        IlkIkmalBuilder.Visual("Cabinet_Handle", new Vector3(0.4f, 0f, -0.035f), new Vector3(0.04f, 0.22f, 0.04f), mats.metalFlat, leaf.transform);
        for (int i = 0; i < 4; i++)
            IlkIkmalBuilder.Visual("Cabinet_Vent_" + i, new Vector3(-0.1f, 0.55f - i * 0.07f, -0.017f), new Vector3(0.5f, 0.025f, 0.01f), dark, leaf.transform);

        var door = cab.gameObject.AddComponent<DoorInteractable>();
        door.hinge = hinge;
        door.leafCollider = leaf.GetComponent<Collider>();
        door.obstacle = null; // the shell is part of the bake; nothing walkable lives inside
        door.hintAnchor = IlkIkmalBuilder.AddAnchor(cab, new Vector3(0f, 1.35f, -D * 0.5f - 0.1f));
        door.openAngle = 110f;
        door.cabinet = true;
        // No use trigger: the closed leaf (Environment, under this root) is what the interact ray hits; an open cabinet must not
        // shadow the supply bag behind it with a trigger volume.
        return cab;
    }

    // ---------- rear fence ----------

    static void BuildRearFence(Transform rear)
    {
        var fence = new GameObject("Fence").transform;
        fence.SetParent(rear, false);
        Material wood = M("OW_FenceTimber", .26f, .20f, .14f, .06f);
        const float h = 2.1f, y = 0.95f; // top at 2.0 m above the station floor, bottom sunk 0.1 m into the pad
        SandboxBuilder.Box("Fence_East_South", new Vector3(32f, y, -8.1f), new Vector3(0.2f, h, 14.4f), wood, 1.5f, fence);   // z -15.3 .. -0.9
        SandboxBuilder.Box("Fence_East_North", new Vector3(32f, y, 7.95f), new Vector3(0.2f, h, 14.1f), wood, 1.5f, fence);   // z 0.9 .. 15
        SandboxBuilder.Box("Fence_South", new Vector3(19.5f, y, -15.3f), new Vector3(25f, h, 0.2f), wood, 1.5f, fence);       // x 7 .. 32, z -15.3
        // Short funnel outside the gate gap (2 m clear): sight lines through the gap are limited to the corridor axis, so the zombies in the front half cannot see the rear approach.
        SandboxBuilder.Box("Fence_Gate_Wing_S", new Vector3(33.6f, y, -1.1f), new Vector3(3.0f, h, 0.2f), wood, 1.5f, fence);
        SandboxBuilder.Box("Fence_Gate_Wing_N", new Vector3(33.6f, y, 1.1f), new Vector3(3.0f, h, 0.2f), wood, 1.5f, fence);
        MarkStatic(fence);
    }

    // ---------- rear details (last) ----------

    static void BuildRearDetails(SandboxBuilder.MaterialSet mats, Transform rear)
    {
        var detail = new GameObject("RearDetails").transform;
        detail.SetParent(rear, false);
        Material post = M("OW_FencePost", .16f, .12f, .09f, .05f), pallet = M("OW_Pallet", .42f, .31f, .18f, .05f), barrel = M("OW_Barrel", .33f, .13f, .10f, .25f);

        // Posts every 3.6 m on the fence line (visual only).
        for (float z = -14.4f; z <= 14.4f; z += 3.6f)
        {
            if (Mathf.Abs(z) < 1.4f) continue;
            IlkIkmalBuilder.Visual("Fence_Post_E", new Vector3(32f, 1.1f, z), new Vector3(0.3f, 2.4f, 0.3f), post, detail);
        }
        for (float x = 9f; x <= 31f; x += 3.6f) IlkIkmalBuilder.Visual("Fence_Post_S", new Vector3(x, 1.1f, -15.3f), new Vector3(0.3f, 2.4f, 0.3f), post, detail);
        IlkIkmalBuilder.Visual("Gate_Post_S", new Vector3(32f, 1.2f, -1.1f), new Vector3(0.34f, 2.5f, 0.34f), post, detail);
        IlkIkmalBuilder.Visual("Gate_Post_N", new Vector3(32f, 1.2f, 1.1f), new Vector3(0.34f, 2.5f, 0.34f), post, detail);

        // Two stacks of pallets and barrels in front of the fence (outside). Small footprints, clear of the route.
        BuildPalletStack(detail, "Stack_East", new Vector2(34f, -9f), 8f, pallet, barrel);
        BuildPalletStack(detail, "Stack_South", new Vector2(14f, -17.4f), -12f, pallet, barrel);

        // Single arrow sign in front of the gate gap: pole, board, arrow tip, text.
        Material board = M("OW_SignBoard", .62f, .48f, .15f, .2f);
        SandboxBuilder.Box("Sign_Pole", new Vector3(35f, 1.1f, -2.2f), new Vector3(0.1f, 2.3f, 0.1f), mats.rustyMetal, 1f, detail);
        IlkIkmalBuilder.Visual("Sign_Board", new Vector3(35f, 1.9f, -2.27f), new Vector3(1.4f, 0.4f, 0.05f), board, detail);
        IlkIkmalBuilder.Visual("Sign_Arrow", new Vector3(34.2f, 1.9f, -2.27f), new Vector3(0.3f, 0.3f, 0.05f), board, detail).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        MarkStatic(detail);
        AddSignText(detail, new Vector3(35.1f, 1.9f, -2.31f), "ARKA GİRİŞ"); // after MarkStatic: a TextMesh must not be batched
    }

    static void BuildPalletStack(Transform parent, string name, Vector2 at, float yaw, Material pallet, Material barrel)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(at.x, -0.1f, at.y);
        root.localRotation = Quaternion.Euler(0f, yaw, 0f);
        for (int i = 0; i < 3; i++)
            SandboxBuilder.Box("Pallet_" + i, new Vector3(0f, 0.07f + i * 0.14f, 0f), new Vector3(1.2f - i * 0.04f, 0.14f, 0.8f), pallet, 1f, root, i * 7f);
        var mesh = ProceduralMeshes.Save("Proc_Rear_Barrel", ProceduralMeshes.Cylinder(0.3f, 0.3f, 0.9f, 12, 0.6f));
        for (int i = 0; i < 2; i++)
        {
            var b = new GameObject("Barrel_" + i);
            b.layer = Layers.Environment;
            b.transform.SetParent(root, false);
            b.transform.localPosition = new Vector3(1.0f + i * 0.7f, 0f, i == 0 ? 0.1f : -0.15f);
            b.AddComponent<MeshFilter>().sharedMesh = mesh;
            b.AddComponent<MeshRenderer>().sharedMaterial = barrel;
            var box = b.AddComponent<BoxCollider>();
            box.size = new Vector3(0.6f, 0.9f, 0.6f);
            box.center = new Vector3(0f, 0.45f, 0f);
        }
    }

    static void AddSignText(Transform parent, Vector3 localPosition, string text)
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (System.Exception) { }
        if (font == null) { Note("NOTE rear sign: built-in font not found, the sign board has no text"); return; }
        var go = new GameObject("Sign_Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = font;
        tm.fontSize = 64;
        tm.characterSize = 0.045f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(.08f, .07f, .05f);
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }
}
