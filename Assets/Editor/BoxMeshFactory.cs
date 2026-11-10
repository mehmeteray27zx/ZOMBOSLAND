using System.Globalization;
using UnityEditor;
using UnityEngine;

// Box meshes with world-scaled UVs (one UV unit = tileMeters) plus normals and tangents.
// Unity's cube primitive stretches a texture over each face; this keeps texel density constant.
public static class BoxMeshFactory
{
    const string Folder = "Assets/Generated/Meshes";

    // Per face: normal, U axis, V axis (U x V = normal). Unity is left-handed, so triangles are wound (BL, TR, TL).
    static readonly Vector3[] Normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
    static readonly Vector3[] UAxes = { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
    static readonly Vector3[] VAxes = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };

    public static Mesh Create(Vector3 size, float tileMeters)
    {
        string name = "Box_" + F(size.x) + "x" + F(size.y) + "x" + F(size.z) + "_t" + F(tileMeters);
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        EnsureFolder();
        var mesh = Build(size, tileMeters);
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    public static Mesh Build(Vector3 size, float tileMeters)
    {
        var vertices = new Vector3[24];
        var normals = new Vector3[24];
        var uvs = new Vector2[24];
        var tangents = new Vector4[24];
        var triangles = new int[36];
        Vector3 half = size * 0.5f;
        for (int f = 0; f < 6; f++)
        {
            Vector3 n = Normals[f], u = UAxes[f], v = VAxes[f];
            float halfN = Mathf.Abs(Vector3.Dot(n, half)) ;
            float halfU = Mathf.Abs(Vector3.Dot(u, half)), halfV = Mathf.Abs(Vector3.Dot(v, half));
            Vector3 c = n * halfN;
            int i = f * 4;
            vertices[i + 0] = c - u * halfU - v * halfV; // bottom-left
            vertices[i + 1] = c - u * halfU + v * halfV; // top-left
            vertices[i + 2] = c + u * halfU + v * halfV; // top-right
            vertices[i + 3] = c + u * halfU - v * halfV; // bottom-right
            float maxU = 2f * halfU / tileMeters, maxV = 2f * halfV / tileMeters;
            uvs[i + 0] = new Vector2(0, 0);
            uvs[i + 1] = new Vector2(0, maxV);
            uvs[i + 2] = new Vector2(maxU, maxV);
            uvs[i + 3] = new Vector2(maxU, 0);
            for (int k = 0; k < 4; k++) { normals[i + k] = n; tangents[i + k] = new Vector4(u.x, u.y, u.z, 1f); }
            int t = f * 6;
            triangles[t + 0] = i; triangles[t + 1] = i + 2; triangles[t + 2] = i + 1;
            triangles[t + 3] = i; triangles[t + 4] = i + 3; triangles[t + 5] = i + 2;
        }
        var mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.tangents = tangents;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    static string F(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "Meshes");
    }
}
