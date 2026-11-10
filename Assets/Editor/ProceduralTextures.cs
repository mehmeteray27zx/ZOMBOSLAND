using System.IO;
using UnityEditor;
using UnityEngine;

// Seamless terrain albedo/normal maps and grass tuft textures, generated deterministically (PNG assets under Assets/Generated/Terrain).
// Poly Haven sets are used when present (Assets/ThirdParty/PolyHaven/<id>/<id>_albedo.jpg), otherwise these procedural ones.
public static class ProceduralTextures
{
    public const string Folder = "Assets/Generated/Terrain";
    const int Size = 1024;

    public sealed class LayerTextures
    {
        public Texture2D albedo, normal;
        public bool procedural;
        public string source;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "Terrain");
    }

    // ---------- noise ----------

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 362437;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }

    static int Wrap(int v, int period) { v %= period; return v < 0 ? v + period : v; }

    // Periodic value noise: x and y are in [0, 1), the pattern repeats every px / py lattice cells.
    static float ValueNoise(float x, float y, int px, int py, int seed)
    {
        float fx = x * px, fy = y * py;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * tx * (tx * (tx * 6f - 15f) + 10f);
        ty = ty * ty * ty * (ty * (ty * 6f - 15f) + 10f);
        int xa = Wrap(x0, px), xb = Wrap(x0 + 1, px), ya = Wrap(y0, py), yb = Wrap(y0 + 1, py);
        float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), tx);
        float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), tx);
        return Mathf.Lerp(a, b, ty);
    }

    static float Fbm(float x, float y, int px, int py, int octaves, int seed)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += ValueNoise(x, y, px << o, py << o, seed + o * 17) * amp;
            norm += amp;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    static Color Rgb(float r, float g, float b) { return new Color(r, g, b, 1f); }

    static Color Scale(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

    // ---------- terrain layers ----------

    public static LayerTextures TerrainLayer(string layerId, string polyHavenId)
    {
        var result = new LayerTextures();
        string ph = LookDevSetup.PolyHaven + "/" + polyHavenId + "/" + polyHavenId;
        var phAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(ph + "_albedo.jpg");
        if (phAlbedo != null)
        {
            result.albedo = phAlbedo;
            result.normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ph + "_normal.jpg");
            result.source = "Poly Haven " + polyHavenId;
            return result;
        }

        EnsureFolder();
        string albedoPath = Folder + "/" + layerId + "_albedo.png";
        string normalPath = Folder + "/" + layerId + "_normal.png";
        if (!File.Exists(albedoPath) || !File.Exists(normalPath)) Generate(layerId, albedoPath, normalPath);
        ImportTerrainTexture(albedoPath, false);
        ImportTerrainTexture(normalPath, true);
        result.albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        result.normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        result.procedural = true;
        result.source = "procedural " + layerId;
        return result;
    }

    static void ImportTerrainTexture(string path, bool normal)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (importer == null) return;
        bool changed = importer.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) || importer.sRGBTexture == normal
                       || !importer.mipmapEnabled || importer.anisoLevel != 8 || importer.wrapMode != TextureWrapMode.Repeat;
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = 1024;
        if (changed) importer.SaveAndReimport();
    }

    static void Generate(string layerId, string albedoPath, string normalPath)
    {
        var height = new float[Size * Size];
        var colors = new Color32[Size * Size];
        float normalStrength = 3f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                float h;
                Color c;
                switch (layerId)
                {
                    case "dirt":
                    {
                        float f = Fbm(u, v, 4, 4, 6, 11), g = Fbm(u, v, 32, 32, 3, 12), speck = ValueNoise(u, v, 256, 256, 13);
                        c = Color.Lerp(Rgb(.22f, .17f, .11f), Rgb(.40f, .32f, .22f), Mathf.SmoothStep(0.25f, 0.75f, f));
                        c = Scale(c, 0.86f + 0.28f * g);
                        if (speck > 0.9f) c = Color.Lerp(c, Rgb(.5f, .46f, .4f), 0.5f);
                        h = f * 0.6f + g * 0.3f + speck * 0.1f;
                        normalStrength = 3f;
                        break;
                    }
                    case "gravel":
                    {
                        float pebbles = Fbm(u, v, 48, 48, 2, 21), fine = ValueNoise(u, v, 256, 256, 22), broad = Fbm(u, v, 5, 5, 4, 23);
                        float stone = Mathf.SmoothStep(0.45f, 0.7f, pebbles);
                        Color tint = Color.Lerp(Rgb(.40f, .37f, .33f), Rgb(.54f, .50f, .44f), ValueNoise(u, v, 96, 96, 24));
                        c = Color.Lerp(Rgb(.30f, .28f, .25f), tint, stone);
                        c = Scale(c, (0.85f + 0.3f * broad) * (0.92f + 0.16f * fine));
                        h = stone * 0.7f + fine * 0.15f + broad * 0.15f;
                        normalStrength = 2.5f;
                        break;
                    }
                    case "grass":
                    {
                        float patches = Fbm(u, v, 3, 3, 5, 31), blades = Fbm(u, v, 96, 96, 3, 32), dirtPatch = Fbm(u, v, 6, 6, 4, 33);
                        Color green = Color.Lerp(Rgb(.19f, .29f, .10f), Rgb(.30f, .40f, .15f), blades);
                        Color dry = Color.Lerp(Rgb(.38f, .38f, .17f), Rgb(.48f, .45f, .22f), blades);
                        c = Color.Lerp(green, dry, Mathf.SmoothStep(0.45f, 0.75f, patches));
                        c = Color.Lerp(c, Rgb(.33f, .27f, .17f), Mathf.SmoothStep(0.68f, 0.85f, dirtPatch) * 0.55f);
                        c = Scale(c, 0.9f + 0.2f * ValueNoise(u, v, 192, 192, 34));
                        h = blades * 0.6f + patches * 0.4f;
                        normalStrength = 2f;
                        break;
                    }
                    default: // rock
                    {
                        float strata = Fbm(u, v, 12, 12, 4, 41), mass = Fbm(u, v, 6, 6, 5, 42), crack = Mathf.Abs(ValueNoise(u, v, 12, 12, 43) - 0.5f) * 2f;
                        c = Color.Lerp(Rgb(.36f, .34f, .30f), Rgb(.60f, .57f, .51f), Mathf.SmoothStep(0.2f, 0.8f, strata * 0.6f + mass * 0.4f));
                        float line = Mathf.SmoothStep(0.06f, 0f, crack);
                        c = Scale(c, 1f - line * 0.5f);
                        h = strata * 0.5f + mass * 0.4f - line * 0.3f;
                        normalStrength = 4f;
                        break;
                    }
                }
                height[y * Size + x] = h;
                colors[y * Size + x] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0, 255), (byte)Mathf.Clamp(c.g * 255f, 0, 255), (byte)Mathf.Clamp(c.b * 255f, 0, 255), 255);
            }

        var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        albedo.SetPixels32(colors);
        File.WriteAllBytes(albedoPath, albedo.EncodeToPNG());
        Object.DestroyImmediate(albedo);

        var normals = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = height[y * Size + (x + 1) % Size] - height[y * Size + (x + Size - 1) % Size];
                float dy = height[((y + 1) % Size) * Size + x] - height[((y + Size - 1) % Size) * Size + x];
                Vector3 n = new Vector3(-dx * normalStrength * 8f, -dy * normalStrength * 8f, 1f).normalized;
                normals[y * Size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        var normal = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        normal.SetPixels32(normals);
        File.WriteAllBytes(normalPath, normal.EncodeToPNG());
        Object.DestroyImmediate(normal);
        AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
    }

    // ---------- grass tufts ----------

    // 128 x 128 RGBA tuft: curved blades fanning from the bottom centre, alpha cut-out, colour darker at the base.
    public static Texture2D GrassTuft(string name, Color baseColor, Color tipColor, int seed)
    {
        EnsureFolder();
        string path = Folder + "/" + name + ".png";
        const int S = 128;
        var rnd = new System.Random(seed);
        int blades = 16;
        float[] x0 = new float[blades], x1 = new float[blades], top = new float[blades], width = new float[blades], shade = new float[blades];
        for (int b = 0; b < blades; b++)
        {
            float spread = ((float)rnd.NextDouble() - 0.5f) * 30f;
            x0[b] = 64f + spread * 0.6f;
            x1[b] = 64f + spread * 2.1f + ((float)rnd.NextDouble() - 0.5f) * 14f;
            top[b] = 70f + (float)rnd.NextDouble() * 55f;
            width[b] = 6f + (float)rnd.NextDouble() * 5f;
            shade[b] = 0.8f + (float)rnd.NextDouble() * 0.4f;
        }
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float best = 0f;
                float bestShade = 1f;
                float f = 0f;
                for (int b = 0; b < blades; b++)
                {
                    if (y >= top[b]) continue;
                    float t = y / top[b];
                    float cx = Mathf.Lerp(x0[b], x1[b], Mathf.Pow(t, 1.4f));
                    float half = width[b] * 0.5f * (1f - t);
                    if (Mathf.Abs(x + 0.5f - cx) <= half && 1f - t > best) { best = 1f - t; bestShade = shade[b]; f = t; }
                }
                Color c = Color.Lerp(baseColor, tipColor, f) ;
                c = Scale(c, bestShade);
                if (best <= 0f) px[y * S + x] = new Color32((byte)(baseColor.r * 255f), (byte)(baseColor.g * 255f), (byte)(baseColor.b * 255f), 0);
                else px[y * S + x] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0, 255), (byte)Mathf.Clamp(c.g * 255f, 0, 255), (byte)Mathf.Clamp(c.b * 255f, 0, 255), 255);
            }
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.5f;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
