using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Texture import, PBR materials, sky, URP asset / renderer tweaks and the post-processing profile.
public static class LookDevSetup
{
    public const string PolyHaven = "Assets/ThirdParty/PolyHaven";
    public const string MaterialFolder = "Assets/Generated/Materials";
    public const string TextureFolder = "Assets/Generated/Textures";
    public static readonly List<string> Report = new List<string>();

    static readonly HashSet<string> MetalSets = new HashSet<string> { "rusty_metal_02", "metal_plate" };

    public static void EnsureFolders()
    {
        foreach (string f in new[] { "Meshes", "Materials", "Textures", "Prefabs" })
            if (!AssetDatabase.IsValidFolder("Assets/Generated/" + f))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
                AssetDatabase.CreateFolder("Assets/Generated", f);
            }
    }

    // ---------- textures ----------

    public static void ImportPolyHavenTextures()
    {
        foreach (string dir in Directory.GetDirectories(PolyHaven))
        {
            string id = Path.GetFileName(dir);
            if (id == "HDRI") continue;
            SetTexture(PolyHaven + "/" + id + "/" + id + "_albedo.jpg", TextureImporterType.Default, true);
            SetTexture(PolyHaven + "/" + id + "/" + id + "_normal.jpg", TextureImporterType.NormalMap, false);
            SetTexture(PolyHaven + "/" + id + "/" + id + "_mask.png", TextureImporterType.Default, false);
        }
    }

    static void SetTexture(string path, TextureImporterType type, bool srgb)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool changed = importer.textureType != type || importer.sRGBTexture != srgb || importer.anisoLevel != 8 ||
                       !importer.mipmapEnabled || importer.maxTextureSize != 2048;
        importer.textureType = type;
        importer.sRGBTexture = srgb;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        if (changed) importer.SaveAndReimport();
    }

    // ---------- materials ----------

    // URP/Lit with the Poly Haven set `id`. Falls back to a flat colour material when textures are missing.
    public static Material MakePbr(string id, float worldTileMeters, Material fallback, Color? tint = null)
    {
        string albedoPath = PolyHaven + "/" + id + "/" + id + "_albedo.jpg";
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        if (albedo == null) { Report.Add("PBR set missing, using flat material: " + id); return fallback; }
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(PolyHaven + "/" + id + "/" + id + "_normal.jpg");
        var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(PolyHaven + "/" + id + "/" + id + "_mask.png");

        string path = MaterialFolder + "/PBR_" + id + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", tint ?? Color.white);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        if (mask != null)
        {
            // Mask layout: R = metallic, G = AO, A = smoothness (URP reads smoothness from the metallic map alpha).
            m.SetTexture("_MetallicGlossMap", mask);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", mask);
            m.EnableKeyword("_OCCLUSIONMAP");
            m.SetFloat("_OcclusionStrength", 1f);
        }
        m.SetFloat("_Smoothness", 1f);
        m.SetFloat("_Metallic", MetalSets.Contains(id) ? 1f : 0f);
        m.SetFloat("_SmoothnessTextureChannel", 0f); // 0 = metallic map alpha
        m.SetTextureScale("_BaseMap", Vector2.one); // tiling comes from world-scaled mesh UVs
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- generated particle / decal textures ----------

    public static Texture2D SoftCircleTexture() { return GenerateTexture("soft_circle", 64, (u, v) => { float d = Mathf.Sqrt(u * u + v * v); return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d); }); }

    public static Texture2D MuzzleStarTexture()
    {
        return GenerateTexture("muzzle_star", 128, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            float angle = Mathf.Atan2(v, u);
            float spikes = 0.45f + 0.55f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2.5f)), 3f);
            float falloff = Mathf.Clamp01(1f - d / spikes);
            float core = Mathf.Clamp01(1f - d * 2.2f);
            return Mathf.Clamp01(falloff * falloff + core);
        });
    }

    static Texture2D GenerateTexture(string name, int size, Func<float, float, float> alpha)
    {
        string path = TextureFolder + "/" + name + ".png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(alpha(u, v));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public enum Blend { Alpha, Additive }

    public static Material MakeParticleMaterial(string name, Texture2D texture, Blend blend, Color tint)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        m.SetTexture("_BaseMap", texture);
        m.SetColor("_BaseColor", tint);
        MakeTransparent(m, blend);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Decal material: unlit transparent, dark.
    public static Material MakeDecalMaterial(Texture2D texture)
    {
        string path = MaterialFolder + "/BulletDecal.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Unlit");
        m.SetTexture("_BaseMap", texture);
        m.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f, 0.85f));
        MakeTransparent(m, Blend.Alpha);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Same property/keyword set the URP material inspector writes for Surface = Transparent.
    static void MakeTransparent(Material m, Blend blend)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", blend == Blend.Additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", 5f);                                   // SrcAlpha
        m.SetFloat("_DstBlend", blend == Blend.Additive ? 1f : 10f);   // One / OneMinusSrcAlpha
        m.SetFloat("_SrcBlendAlpha", 1f);
        m.SetFloat("_DstBlendAlpha", blend == Blend.Additive ? 1f : 10f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.SetFloat("_Cull", 2f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetShaderPassEnabled("ShadowCaster", false);
    }

    // ---------- sky / lighting ----------

    public static void SetupSky()
    {
        string path = PolyHaven + "/HDRI/kloofendal_overcast_puresky_2k.hdr";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Material sky = null;
        if (importer != null)
        {
            importer.textureShape = TextureImporterShape.TextureCube;
            importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            string matPath = MaterialFolder + "/Sky.mat";
            if (cube != null)
            {
                sky = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (sky == null) { sky = new Material(Shader.Find("Skybox/Cubemap")); AssetDatabase.CreateAsset(sky, matPath); }
                sky.shader = Shader.Find("Skybox/Cubemap");
                sky.SetTexture("_Tex", cube);
                sky.SetFloat("_Exposure", 0.75f);
                EditorUtility.SetDirty(sky);
            }
            else Report.Add("HDRI did not import as Cubemap; sky not set");
        }
        else Report.Add("HDRI missing: " + path);

        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.ambientIntensity = 0.8f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.46f, 0.50f, 0.54f);
        RenderSettings.fogDensity = 0.008f;
    }

    // ---------- URP asset / renderer ----------

    static UniversalRenderPipelineAsset ActiveAsset()
    {
        return (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
    }

    public static void ConfigureUrpAsset()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if (asset == null) { Report.Add("PC_RPAsset not found"); return; }
        var so = new SerializedObject(asset);
        SetInt(so, "m_SupportsHDR", 1);
        SetFloat(so, "m_ShadowDistance", 60f);
        SetInt(so, "m_ShadowCascadeCount", 3);
        SetInt(so, "m_SoftShadowsSupported", 1);
        SetInt(so, "m_SoftShadowQuality", 2);       // Medium
        SetFloat(so, "m_RenderScale", 1f);
        SetInt(so, "m_MainLightShadowmapResolution", 2048);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        var active = ActiveAsset();
        Report.Add("URP asset configured: PC_RPAsset (active quality asset: " + (active != null ? active.name : "none") + ")");
    }

    static void SetInt(SerializedObject so, string name, int v)
    {
        var p = so.FindProperty(name);
        if (p == null) { Report.Add("URP property not found: " + name); return; }
        p.intValue = v;
    }

    static void SetFloat(SerializedObject so, string name, float v)
    {
        var p = so.FindProperty(name);
        if (p == null) { Report.Add("URP property not found: " + name); return; }
        p.floatValue = v;
    }

    public static void ConfigureRenderer()
    {
        try
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (data == null) { Report.Add("PC_Renderer not found; SSAO not configured"); return; }
            ScreenSpaceAmbientOcclusion ssao = null;
            foreach (var f in data.rendererFeatures) if (f is ScreenSpaceAmbientOcclusion s) { ssao = s; break; }
            if (ssao == null)
            {
                ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "ScreenSpaceAmbientOcclusion";
                AssetDatabase.AddObjectToAsset(ssao, data);
                AssetDatabase.SaveAssets();
                var rso = new SerializedObject(data);
                var features = rso.FindProperty("m_RendererFeatures");
                var map = rso.FindProperty("m_RendererFeatureMap");
                string guid; long fileId;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out guid, out fileId);
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = fileId;
                rso.ApplyModifiedPropertiesWithoutUndo();
                Report.Add("SSAO renderer feature added");
            }
            else Report.Add("SSAO renderer feature already present");
            ssao.SetActive(true);
            var sso = new SerializedObject(ssao);
            TrySet(sso, "m_Settings.Radius", 0.4f);
            TrySet(sso, "m_Settings.Intensity", 1.2f);
            sso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ssao);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
        catch (Exception e) { Report.Add("SSAO setup failed (continuing): " + e.Message); Debug.LogWarning("SSAO setup failed: " + e); }
    }

    static void TrySet(SerializedObject so, string name, float v)
    {
        var p = so.FindProperty(name);
        if (p == null) { Report.Add("SSAO property not found: " + name); return; }
        p.floatValue = v;
    }

    // ---------- post processing ----------

    // VolumeProfile.Add does not store the component in the asset file; add it as a sub-asset so it persists.
    static T AddComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    public static VolumeProfile CreatePostProfile()
    {
        const string path = "Assets/Generated/PostProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null) AssetDatabase.DeleteAsset(path);
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var tone = AddComponent<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);

        var bloom = AddComponent<Bloom>(profile);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.25f);
        bloom.scatter.Override(0.6f);

        var color = AddComponent<ColorAdjustments>(profile);
        color.postExposure.Override(-0.35f);
        color.contrast.Override(18f);
        color.saturation.Override(-25f);

        var split = AddComponent<SplitToning>(profile);
        split.shadows.Override(new Color(0.55f, 0.64f, 0.8f));
        split.highlights.Override(new Color(1f, 0.97f, 0.92f));
        split.balance.Override(-20f);

        var wb = AddComponent<WhiteBalance>(profile);
        wb.temperature.Override(-5f);

        var vignette = AddComponent<Vignette>(profile);
        vignette.intensity.Override(0.22f);

        var grain = AddComponent<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.12f);

        var blur = AddComponent<MotionBlur>(profile);
        blur.intensity.Override(0f);
        blur.active = false; // motion blur stays off

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }
}
