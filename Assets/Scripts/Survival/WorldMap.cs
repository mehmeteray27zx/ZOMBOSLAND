using UnityEngine;

namespace Zombosland
{
    // Region (south-west origin, 500 x 500 m) <-> world conversion and the playable bounds.
    public static class WorldMap
    {
        public const float MinX = -46f, MaxX = 454f, MinZ = -110f, MaxZ = 390f;
        const float OffsetX = 46f, OffsetZ = 110f;

        public static Vector3 RegionToWorld(float rx, float rz) { return new Vector3(rx - OffsetX, 0f, rz - OffsetZ); }

        public static Vector2 WorldToRegion(Vector3 world) { return new Vector2(world.x + OffsetX, world.z + OffsetZ); }

        public static bool InBounds(Vector3 p) { return p.x >= MinX && p.x <= MaxX && p.z >= MinZ && p.z <= MaxZ; }
    }
}
