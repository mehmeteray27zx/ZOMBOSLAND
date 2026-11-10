using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Deterministic procedural low-poly meshes (pine tree, cylinder, gable roof, rock, ribbon, quads). Every mesh is saved as an asset
// under Assets/Generated/Meshes and updated in place when it already exists.
public static class ProceduralMeshes
{
    public const string Folder = "Assets/Generated/Meshes";

    // Flat-shaded triangle soup with submeshes. Unity winding: normal = cross(b - a, c - a); `inside` flips a triangle that faces it.
    sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] triangles;
        readonly float uvScale;

        public MeshBuilder(int submeshes, float uvScale)
        {
            triangles = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) triangles[i] = new List<int>();
            this.uvScale = uvScale;
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, int sub, Vector3 inside)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) return;
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) { Vector3 t = b; b = c; c = t; n = -n; }
            n.Normalize();
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(Project(a, n)); uvs.Add(Project(b, n)); uvs.Add(Project(c, n));
            triangles[sub].Add(i); triangles[sub].Add(i + 1); triangles[sub].Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int sub, Vector3 inside)
        {
            Tri(a, b, c, sub, inside);
            Tri(a, c, d, sub, inside);
        }

        Vector2 Project(Vector3 p, Vector3 n)
        {
            Vector3 an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            if (an.y >= an.x && an.y >= an.z) return new Vector2(p.x, p.z) * uvScale;
            if (an.x >= an.z) return new Vector2(p.z, p.y) * uvScale;
            return new Vector2(p.x, p.y) * uvScale;
        }

        public Mesh ToMesh()
        {
            var mesh = new Mesh();
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    // ---------- saving ----------

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "Meshes");
    }

    public static Mesh Save(string name, Mesh mesh)
    {
        EnsureFolder();
        string path = Folder + "/" + name + ".asset";
        mesh.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ---------- pine tree ----------

    // Tapered 7-sided trunk (submesh 0) plus five irregular stacked cones (submesh 1), roughly 150 triangles. Origin at the trunk base.
    public static Mesh Pine(int variant)
    {
        var rnd = new System.Random(7000 + variant * 31);
        float H = 8.5f + variant * 0.9f;
        var mb = new MeshBuilder(2, 0.3f);

        const int trunkSides = 7;
        float trunkTop = H * 0.82f;
        float[] ty = { -0.2f, trunkTop * 0.5f, trunkTop };
        float[] tr = { 0.30f, 0.20f, 0.09f };
        for (int seg = 0; seg < 2; seg++)
            for (int i = 0; i < trunkSides; i++)
            {
                float a0 = i / (float)trunkSides * Mathf.PI * 2f, a1 = (i + 1) / (float)trunkSides * Mathf.PI * 2f;
                Vector3 p00 = new Vector3(Mathf.Cos(a0) * tr[seg], ty[seg], Mathf.Sin(a0) * tr[seg]);
                Vector3 p01 = new Vector3(Mathf.Cos(a1) * tr[seg], ty[seg], Mathf.Sin(a1) * tr[seg]);
                Vector3 p10 = new Vector3(Mathf.Cos(a0) * tr[seg + 1], ty[seg + 1], Mathf.Sin(a0) * tr[seg + 1]);
                Vector3 p11 = new Vector3(Mathf.Cos(a1) * tr[seg + 1], ty[seg + 1], Mathf.Sin(a1) * tr[seg + 1]);
                mb.Quad(p00, p10, p11, p01, 0, new Vector3(0f, (ty[seg] + ty[seg + 1]) * 0.5f, 0f));
            }

        const int sides = 13;
        const int layers = 5;
        for (int k = 0; k < layers; k++)
        {
            float baseY = 1.7f + k * ((H - 4.6f) / (layers - 1));
            float height = 3.1f - k * 0.28f;
            float radius = (2.35f - k * 0.42f) * (1f + variant * 0.07f);
            float cx = ((float)rnd.NextDouble() - 0.5f) * 0.3f, cz = ((float)rnd.NextDouble() - 0.5f) * 0.3f;
            Vector3 apex = new Vector3(cx + ((float)rnd.NextDouble() - 0.5f) * 0.2f, baseY + height, cz + ((float)rnd.NextDouble() - 0.5f) * 0.2f);
            Vector3 hub = new Vector3(cx, baseY - 0.3f, cz);
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f + k * 0.4f;
                float jitter = 0.8f + (float)rnd.NextDouble() * 0.38f;
                ring[i] = new Vector3(cx + Mathf.Cos(a) * radius * jitter, baseY + ((float)rnd.NextDouble() - 0.5f) * 0.2f, cz + Mathf.Sin(a) * radius * jitter);
            }
            Vector3 coneInside = new Vector3(cx, baseY + height * 0.3f, cz);
            Vector3 above = new Vector3(cx, baseY + 2f, cz);
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % sides];
                mb.Tri(a, apex, b, 1, coneInside);
                mb.Tri(hub, a, b, 1, above);
            }
        }
        return mb.ToMesh();
    }

    // ---------- cylinder ----------

    // Smooth-shaded tapered cylinder along +Y from 0 to height, with optional flat caps. UVs are world scaled (tile metres).
    public static Mesh Cylinder(float radiusBottom, float radiusTop, float height, int sides, float tile, bool caps = true)
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        float slope = (radiusBottom - radiusTop) / height;
        float circumference = Mathf.PI * 2f * Mathf.Max(radiusBottom, radiusTop);
        for (int i = 0; i <= sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 normal = (dir + Vector3.up * slope).normalized;
            v.Add(dir * radiusBottom); n.Add(normal); uv.Add(new Vector2(i / (float)sides * circumference / tile, 0f));
            v.Add(dir * radiusTop + Vector3.up * height); n.Add(normal); uv.Add(new Vector2(i / (float)sides * circumference / tile, height / tile));
        }
        for (int i = 0; i < sides; i++)
        {
            int b = i * 2;
            t.Add(b); t.Add(b + 1); t.Add(b + 3);
            t.Add(b); t.Add(b + 3); t.Add(b + 2);
        }
        if (caps)
        {
            for (int cap = 0; cap < 2; cap++)
            {
                float r = cap == 0 ? radiusBottom : radiusTop;
                if (r <= 0.0001f) continue;
                float y = cap == 0 ? 0f : height;
                Vector3 normal = cap == 0 ? Vector3.down : Vector3.up;
                int center = v.Count;
                v.Add(new Vector3(0f, y, 0f)); n.Add(normal); uv.Add(new Vector2(0.5f * r * 2f / tile, 0.5f * r * 2f / tile));
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); n.Add(normal);
                    uv.Add(new Vector2((0.5f + Mathf.Cos(a) * 0.5f) * r * 2f / tile, (0.5f + Mathf.Sin(a) * 0.5f) * r * 2f / tile));
                }
                for (int i = 0; i < sides; i++)
                {
                    if (cap == 1) { t.Add(center); t.Add(center + 2 + i); t.Add(center + 1 + i); }
                    else { t.Add(center); t.Add(center + 1 + i); t.Add(center + 2 + i); }
                }
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // ---------- gable roof ----------

    // Closed triangular prism: ridge along Z, eaves at y = 0, eaves overhang on all four sides.
    public static Mesh GableRoof(float width, float depth, float ridgeHeight, float overhang, float tile)
    {
        float hx = width * 0.5f + overhang, hz = depth * 0.5f + overhang;
        var mb = new MeshBuilder(1, 1f / tile);
        Vector3 a = new Vector3(-hx, 0f, -hz), b = new Vector3(-hx, 0f, hz), c = new Vector3(hx, 0f, hz), d = new Vector3(hx, 0f, -hz);
        Vector3 r0 = new Vector3(0f, ridgeHeight, -hz), r1 = new Vector3(0f, ridgeHeight, hz);
        Vector3 inside = new Vector3(0f, ridgeHeight * 0.3f, 0f);
        mb.Quad(a, b, r1, r0, 0, inside);   // left slope
        mb.Quad(d, c, r1, r0, 0, inside);   // right slope
        mb.Tri(a, d, r0, 0, inside);        // gables
        mb.Tri(b, c, r1, 0, inside);
        mb.Quad(a, b, c, d, 0, inside);     // underside
        return mb.ToMesh();
    }

    // ---------- rock ----------

    // Once-subdivided octahedron with seeded radial noise and a flattened bottom (32 triangles).
    public static Mesh Rock(float radius, int seed)
    {
        var rnd = new System.Random(seed);
        var cache = new Dictionary<Vector3Int, float>();
        System.Func<Vector3, Vector3> shape = p =>
        {
            Vector3 d = p.normalized;
            var key = new Vector3Int(Mathf.RoundToInt(d.x * 50f), Mathf.RoundToInt(d.y * 50f), Mathf.RoundToInt(d.z * 50f));
            float f;
            if (!cache.TryGetValue(key, out f)) { f = 0.72f + (float)rnd.NextDouble() * 0.5f; cache[key] = f; }
            Vector3 q = d * radius * f;
            q.y = Mathf.Max(q.y * 0.75f, -radius * 0.35f);
            return q;
        };
        Vector3[] ax = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        int[,] faces = { { 0, 2, 4 }, { 4, 2, 1 }, { 1, 2, 5 }, { 5, 2, 0 }, { 4, 3, 0 }, { 1, 3, 4 }, { 5, 3, 1 }, { 0, 3, 5 } };
        var mb = new MeshBuilder(1, 1f / Mathf.Max(0.5f, radius * 2f));
        for (int f = 0; f < 8; f++)
        {
            Vector3 p0 = ax[faces[f, 0]], p1 = ax[faces[f, 1]], p2 = ax[faces[f, 2]];
            Vector3 m01 = (p0 + p1).normalized, m12 = (p1 + p2).normalized, m20 = (p2 + p0).normalized;
            Vector3[][] tris = { new[] { p0, m01, m20 }, new[] { m01, p1, m12 }, new[] { m20, m12, p2 }, new[] { m01, m12, m20 } };
            foreach (var t in tris) mb.Tri(shape(t[0]), shape(t[1]), shape(t[2]), 0, Vector3.zero);
        }
        return mb.ToMesh();
    }

    // ---------- ribbon ----------

    // Surface strip from a grid of points: rows run along the path, columns left to right. UVs are world scaled.
    public static Mesh Ribbon(Vector3[,] grid, float tile)
    {
        int rows = grid.GetLength(0), cols = grid.GetLength(1);
        var v = new Vector3[rows * cols];
        var uv = new Vector2[rows * cols];
        float along = 0f;
        int mid = cols / 2;
        for (int r = 0; r < rows; r++)
        {
            if (r > 0) along += Vector3.Distance(grid[r, mid], grid[r - 1, mid]);
            float lateral = 0f;
            for (int c = 0; c < cols; c++)
            {
                if (c > 0) lateral += Vector3.Distance(grid[r, c], grid[r, c - 1]);
                v[r * cols + c] = grid[r, c];
                uv[r * cols + c] = new Vector2(lateral / tile, along / tile);
            }
        }
        var t = new List<int>((rows - 1) * (cols - 1) * 6);
        for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols - 1; c++)
            {
                int a = r * cols + c, b = (r + 1) * cols + c, cc = (r + 1) * cols + c + 1, d = r * cols + c + 1;
                t.Add(a); t.Add(b); t.Add(cc);
                t.Add(a); t.Add(cc); t.Add(d);
            }
        var mesh = new Mesh();
        if (v.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = v;
        mesh.uv = uv;
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // Upward-facing quads (corner order a, b, c, d around the quad), e.g. road dashes.
    public static Mesh UpQuads(List<Vector3[]> quads, float tile)
    {
        var mb = new MeshBuilder(1, 1f / tile);
        foreach (var q in quads)
        {
            Vector3 centre = (q[0] + q[1] + q[2] + q[3]) * 0.25f;
            mb.Quad(q[0], q[1], q[2], q[3], 0, centre - Vector3.up);
        }
        return mb.ToMesh();
    }
}
