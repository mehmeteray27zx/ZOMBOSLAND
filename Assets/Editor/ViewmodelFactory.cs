using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Zombosland;

// References produced by BuildArmsViewmodel (assigned to ViewmodelPresenter by SandboxBuilder).
public class ViewmodelRig
{
    public Animator armsAnimator, pistolAnimator;
    public Transform muzzle, eject;
    public GameObject armsRoot;               // default (LPSP) arms root
    public AlternateArms survivalist;         // null when the Survivalist set could not be built
    public GameObject axe;                    // v0.7 axe under the LPSP hand_r (inactive); null when the axe art is missing
    public GameObject bat, bandageRoll;       // v0.8 props under the LPSP hands (inactive)
    public GameObject[] bandageRings;
}

// Builds the first-person viewmodel from the Infima LPSP free-sample art: arms + Handgun_03 + magazine,
// URP materials (assigned through model import remaps), and our own Animator controllers. Art/audio only: no LPSP scripts or prefabs.
public static class ViewmodelFactory
{
    public const string Root = "Assets/ThirdParty/InfimaLPSP/";
    const string ArmsFbx = Root + "Art/Meshes/Character/SK_FP_CH_Default_Cubic.fbx";
    const string GunFbx = Root + "Art/Meshes/Weapons/Handguns/SK_Handgun_03.fbx";
    const string MagFbx = Root + "Art/Meshes/Weapons/Handguns/SM_Handgun_03_Magazine_Default.fbx";
    const string CasingFbx = Root + "Art/Meshes/Weapons/_Common/Casings/SM_Bullet_Small_Empty.fbx";
    const string AnimFolder = Root + "Art/Animations/Character/Handguns/";
    const string TextureFolder = Root + "Art/Textures/";
    // v0.5a Survivalist trial (approach 1: LPSP generic clips on the identical UE4 skeleton, see Docs/IMPLEMENTATION_REPORT.md).
    const string SurvFbx = "Assets/ThirdParty/Survivalist/Meshes/SK_Military_Survivalist.fbx";
    const string SurvTex = "Assets/ThirdParty/Survivalist/Textures/";
    // Which Survivalist FPS meshes are visible: bare skin arms + shirt sleeves (gloves variant stays hidden).
    const bool SurvivalistUseGloves = false;
    // v0.7 axe (Blender-made clips + Poly Haven wooden_axe_02, CC0; see Assets/Art/Viewmodels/Axe/LICENSE.md).
    const string AxeFolder = "Assets/Art/Viewmodels/Axe/";
    const string AxeIdleFbx = AxeFolder + "A_FP_Axe_Idle.fbx";
    const string AxeSwingFbx = AxeFolder + "A_FP_Axe_Swing.fbx";
    const string ReloadFlourishFolder = "Assets/Art/Viewmodels/HandgunReload/";
    public const string ReloadFlourishArmsFbx = ReloadFlourishFolder + "A_FP_Handgun_ReloadEmpty_Arms.fbx";
    public const string ReloadFlourishGunFbx = ReloadFlourishFolder + "A_FP_WEP_Handgun_ReloadEmpty_Gun.fbx";
    public const string ReloadFlourishArmsClip = "A_FP_Handgun_ReloadEmpty_Arms";
    public const string ReloadFlourishGunClip = "A_FP_WEP_Handgun_ReloadEmpty_Gun";
    const string AxeMeshNode = "wooden_axe_02";
    // v0.8 hand actions (Blender clips made in this project, SourceArt/HandActions) and the bat (Poly Haven baseball_bat, CC0).
    const string HandActionFolder = "Assets/Art/Viewmodels/HandActions/";
    const string BatFolder = "Assets/Art/Viewmodels/Bat/";
    const string BatIdleFbx = BatFolder + "A_FP_Bat_Idle.fbx";
    const string BatMeshNode = "baseball_bat";
    static readonly string[] HandActionClips =
    {
        "A_FP_Handgun_Pickup", "A_FP_Handgun_OpenContainer", "A_FP_Handgun_Lower", "A_FP_Handgun_Raise", "A_FP_Handgun_Bandage", "A_FP_Handgun_Craft",
        "A_FP_Hands_Idle", "A_FP_Hands_Pickup", "A_FP_Hands_OpenContainer",
        "A_FP_Bat_Draw", "A_FP_Bat_Swing", "A_FP_Bat_Pickup", "A_FP_Bat_OpenContainer",
        "A_FP_Axe_Draw", "A_FP_Axe_Pickup", "A_FP_Axe_OpenContainer"
    };
    public const string AnimatorFolder = "Assets/Generated/Animators";
    const string ArmsControllerPath = AnimatorFolder + "/VM_Arms.controller";
    const string PistolControllerPath = AnimatorFolder + "/VM_Pistol.controller";

    // Notes for the build report (SandboxBuilder prints them).
    public static readonly List<string> Notes = new List<string>();

    public static bool AssetsPresent()
    {
        foreach (string p in new[] { ArmsFbx, GunFbx, MagFbx })
            if (!File.Exists(p)) return false;
        foreach (string clip in new[] { "Idle_Pose", "Aim_Pose", "Fire", "Aim_Fire", "Reload", "Reload_Empty" })
            if (!File.Exists(AnimFolder + "A_FP_PCH_Handgun_" + clip + ".fbx")) return false;
        return true;
    }

    // ---------- materials ----------

    static Material Lit(string name, string baseMap, Color color, float metallic, float smoothness, string normalMap = null)
    {
        string path = LookDevSetup.MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + baseMap));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        if (normalMap != null)
        {
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + normalMap));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Arms() { return Lit("VM_Arms", "Character/T_FP_CH_Camo_001_001_D.png", new Color(0.82f, 0.82f, 0.82f), 0f, 0.2f); }
    static Material Steel() { return Lit("VM_Steel", "Weapons/T_WEP_Steel_Brushed_001_D.png", new Color(0.42f, 0.43f, 0.45f), 0.7f, 0.45f, "Weapons/T_WEP_Steel_Brushed_N.png"); }
    static Material Basic005() { return Lit("VM_Basic005", "Weapons/T_WEP_Basic_005_D.png", new Color(0.22f, 0.23f, 0.25f), 0.35f, 0.4f); }
    static Material Basic006() { return Lit("VM_Basic006", "Weapons/T_WEP_Basic_006_D.png", Color.white, 0.15f, 0.45f); }
    static Material Basic043() { return Lit("VM_Basic043", "Weapons/T_WEP_Basic_043_D.png", Color.white, 0.15f, 0.45f); }
    static Material Casings() { return Lit("VM_Casings", "Casings/T_WEP_Casings_D.png", Color.white, 0.6f, 0.5f); }

    // Assigns materials to model material slots through the importer remap (only reimports when something changed).
    static void Remap(string fbx, params object[] slotAndMaterial)
    {
        var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (importer == null) return;
        var existing = importer.GetExternalObjectMap();
        bool changed = false;
        for (int i = 0; i < slotAndMaterial.Length; i += 2)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), (string)slotAndMaterial[i]);
            var material = (Material)slotAndMaterial[i + 1];
            Object current;
            if (existing.TryGetValue(id, out current) && current == material) continue;
            importer.AddRemap(id, material);
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
        var embedded = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().Select(x => x.name).ToArray();
        Notes.Add("Remap " + Path.GetFileName(fbx) + (changed ? " (reimported)" : " (unchanged)") + "; embedded materials after import: [" + string.Join(",", embedded) + "]");
    }

    static void ApplyMaterials()
    {
        Material arms = Arms(), steel = Steel(), b5 = Basic005(), b6 = Basic006(), b43 = Basic043(), casings = Casings();
        AssetDatabase.SaveAssets();
        Remap(ArmsFbx, "Arms", arms);
        Remap(GunFbx, "Body", steel, "Details", b6, "Grip", b43, "Hammer", b5, "Slide", b5);
        Remap(MagFbx, "Body", steel, "Bullets", casings);
        if (File.Exists(CasingFbx)) Remap(CasingFbx, "Bullet", casings);
    }

    // Casing mesh + material for EffectPool (null when the file is missing).
    public static Mesh CasingMesh(out Material material)
    {
        material = null;
        if (!File.Exists(CasingFbx)) return null;
        material = Casings();
        Remap(CasingFbx, "Bullet", material);
        return AssetDatabase.LoadAllAssetsAtPath(CasingFbx).OfType<Mesh>().FirstOrDefault();
    }

    // ---------- axe import (v0.7) ----------

    public static bool AxeAssetsPresent()
    {
        return File.Exists(AxeIdleFbx) && File.Exists(AxeSwingFbx)
            && File.Exists(AxeFolder + "T_Axe_albedo.png") && File.Exists(AxeFolder + "T_Axe_normal.png") && File.Exists(AxeFolder + "T_Axe_mask.png");
    }

    // Same settings as the LPSP A_FP_PCH_Handgun_Idle_Pose.fbx importer: Generic, 0.01 scale, no resampling or compression, no materials.
    // One clip named `clipName`; the take name comes from the FBX. Reimports only when a setting differs.
    static void ImportAxeClip(string fbx, string clipName, bool loop)
    {
        var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (importer == null) { Notes.Add("WARNING: axe FBX not importable: " + fbx); return; }
        string take = null;
        foreach (var c in importer.clipAnimations) { take = c.takeName; break; }
        if (string.IsNullOrEmpty(take)) foreach (var c in importer.defaultClipAnimations) { take = c.takeName; break; }
        if (string.IsNullOrEmpty(take)) foreach (var t in importer.importedTakeInfos) { take = t.name; break; }
        if (string.IsNullOrEmpty(take)) { Notes.Add("WARNING: no take found in " + fbx); return; }

        var existing = importer.clipAnimations;
        bool changed = !importer.preserveHierarchy || importer.globalScale != 0.01f || !importer.useFileScale || importer.animationType != ModelImporterAnimationType.Generic
            || !importer.importAnimation || importer.resampleCurves || importer.animationCompression != ModelImporterAnimationCompression.Off
            || importer.materialImportMode != ModelImporterMaterialImportMode.None
            || existing.Length != 1 || existing[0].name != clipName || existing[0].takeName != take || existing[0].loopTime != loop;
        if (changed)
        {
            importer.preserveHierarchy = true; // without it Unity drops the single top node and the clip paths lose "Armature/"
            importer.globalScale = 0.01f;
            importer.useFileScale = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.resampleCurves = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            ModelImporterClipAnimation clip = null;
            foreach (var c in importer.defaultClipAnimations) { if (c.takeName == take) { clip = c; break; } }
            if (clip == null) clip = new ModelImporterClipAnimation { takeName = take, firstFrame = 0f, lastFrame = 0f };
            clip.name = clipName;
            clip.takeName = take;
            clip.loopTime = loop;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }
        Notes.Add("Axe clip " + clipName + " (take '" + take + "')" + (changed ? " (reimported)" : " (unchanged)"));
    }

    static void SetAxeTexture(string path, bool normalMap, bool srgb)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType == type && importer.sRGBTexture == srgb) return;
        importer.textureType = type;
        importer.sRGBTexture = srgb;
        importer.SaveAndReimport();
    }

    // URP/Lit, mask layout as LookDevSetup.MakePbr: R metallic, G AO, A smoothness.
    static Material AxeMaterial()
    {
        SetAxeTexture(AxeFolder + "T_Axe_albedo.png", false, true);
        SetAxeTexture(AxeFolder + "T_Axe_normal.png", true, false);
        SetAxeTexture(AxeFolder + "T_Axe_mask.png", false, false);
        string path = LookDevSetup.MaterialFolder + "/VM_Axe.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AxeFolder + "T_Axe_albedo.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AxeFolder + "T_Axe_normal.png"));
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AxeFolder + "T_Axe_mask.png"));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Smoothness", 1f);
        m.SetFloat("_SmoothnessTextureChannel", 0f); // 0 = metallic map alpha
        EditorUtility.SetDirty(m);
        return m;
    }

    // Imports the axe clips and textures; returns false (axe skipped, Notes explain) when the art is missing.
    static bool PrepareAxe(out Material material)
    {
        material = null;
        if (!AxeAssetsPresent()) { Notes.Add("Axe art incomplete; axe skipped"); return false; }
        ImportAxeClip(AxeIdleFbx, "A_FP_Axe_Idle", true);
        ImportAxeClip(AxeSwingFbx, "A_FP_Axe_Swing", false);
        material = AxeMaterial();
        AssetDatabase.SaveAssets();
        return true;
    }

    // Imports every v0.8 hand action clip and the bat; a missing file only drops that state.
    static void PrepareHandActions()
    {
        foreach (string n in HandActionClips)
        {
            string path = HandActionFolder + n + ".fbx";
            if (!File.Exists(path)) { Notes.Add("WARNING: hand action missing: " + path); continue; }
            ImportAxeClip(path, n, n == "A_FP_Hands_Idle" || n == "A_FP_Handgun_Craft");
        }
        if (File.Exists(BatIdleFbx)) ImportAxeClip(BatIdleFbx, "A_FP_Bat_Idle", true);
        else Notes.Add("WARNING: bat art missing; placeholder melee view stays");
        AssetDatabase.SaveAssets();
    }

    static Material BatMaterial()
    {
        if (!File.Exists(BatFolder + "T_Bat_albedo.png")) return null;
        SetAxeTexture(BatFolder + "T_Bat_albedo.png", false, true);
        SetAxeTexture(BatFolder + "T_Bat_normal.png", true, false);
        SetAxeTexture(BatFolder + "T_Bat_mask.png", false, false);
        string path = LookDevSetup.MaterialFolder + "/VM_Bat.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BatFolder + "T_Bat_albedo.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BatFolder + "T_Bat_normal.png"));
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BatFolder + "T_Bat_mask.png"));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Smoothness", 1f);
        m.SetFloat("_SmoothnessTextureChannel", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material BandageMaterial()
    {
        string path = LookDevSetup.MaterialFolder + "/VM_BandageCloth.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", new Color(0.80f, 0.77f, 0.68f));
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.06f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Bandage roll in the right hand and three wraps on the left forearm; placed from the bind pose of this arm set, all inactive.
    static void AttachBandageProps(Transform arms, Material cloth, out GameObject roll, out GameObject[] rings)
    {
        roll = null; rings = null;
        Transform handR = FindDeep(arms, "hand_r"), mid = FindDeep(arms, "middle_01_r"), index = FindDeep(arms, "index_01_r"), pinky = FindDeep(arms, "pinky_01_r");
        Transform handL = FindDeep(arms, "hand_l"), lower = FindDeep(arms, "lowerarm_l");
        if (handR == null || mid == null || index == null || pinky == null || handL == null || lower == null || cloth == null) { Notes.Add("WARNING: bandage bones missing; props skipped"); return; }
        Mesh rollMesh = ProceduralMeshes.Save("Proc_BandageRoll", ProceduralMeshes.Cylinder(0.027f, 0.027f, 0.052f, 28, 1f));
        var r = new GameObject("BandageRoll");
        r.transform.SetParent(handR, true);
        r.transform.position = Vector3.Lerp(handR.position, mid.position, 0.85f) + (handR.position - lower.position).normalized * 0.01f;
        r.transform.rotation = Quaternion.FromToRotation(Vector3.up, (index.position - pinky.position).normalized);
        r.AddComponent<MeshFilter>().sharedMesh = rollMesh;
        r.AddComponent<MeshRenderer>().sharedMaterial = cloth;
        r.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.SetActive(false);
        roll = r;

        Vector3 axis = (handL.position - lower.position).normalized;
        float length = Vector3.Distance(handL.position, lower.position);
        rings = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            float t = 0.10f + 0.085f * i;
            float radius = 0.036f + 0.0022f * i;
            Mesh mesh = ProceduralMeshes.Save("Proc_BandageRing_" + i, ProceduralMeshes.Cylinder(radius, radius + 0.001f, 0.026f, 32, 1f, false));
            var g = new GameObject("BandageRing_" + i);
            g.transform.SetParent(lower, true);
            g.transform.position = handL.position - axis * (t * length);
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = cloth;
            g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.SetActive(false);
            rings[i] = g;
        }
    }

    // Imports the hand-authored empty reload (slide rack) clips; when the files are missing the LPSP empty reload stays in use.
    public static void PrepareReloadFlourish()
    {
        if (!File.Exists(ReloadFlourishArmsFbx) || !File.Exists(ReloadFlourishGunFbx)) { Notes.Add("Reload flourish art missing; LPSP empty reload used"); return; }
        ImportAxeClip(ReloadFlourishArmsFbx, ReloadFlourishArmsClip, false);
        ImportAxeClip(ReloadFlourishGunFbx, ReloadFlourishGunClip, false);
        AssetDatabase.SaveAssets();
    }

    // Copies the axe mesh from the FBX hand_r/wooden_axe_02 node to a new "Axe" object under `handR` with the same local transform.
    // If the FBX hand_r has a different world scale than the target hand_r, the local position and scale are corrected so the axe keeps its world size.
    static GameObject AttachAxe(Transform handR, Material material, string setName) { return AttachHeld(handR, material, setName, AxeIdleFbx, AxeMeshNode, "Axe"); }

    static GameObject AttachHeld(Transform handR, Material material, string setName, string fbx, string meshNode, string objectName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (prefab == null || handR == null || material == null) return null;
        var source = (GameObject)Object.Instantiate(prefab);
        try
        {
            Transform srcHand = FindDeep(source.transform, "hand_r");
            Transform node = srcHand != null ? FindDeep(srcHand, meshNode) : null;
            var filter = node != null ? node.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) { Notes.Add("WARNING: " + setName + ": " + meshNode + " mesh not found under hand_r in " + fbx + "; skipped"); return null; }

            Vector3 localPos = node.localPosition, localScale = node.localScale;
            Quaternion localRot = node.localRotation;
            Vector3 a = srcHand.lossyScale, b = handR.lossyScale;
            Vector3 ratio = new Vector3(b.x != 0f ? a.x / b.x : 1f, b.y != 0f ? a.y / b.y : 1f, b.z != 0f ? a.z / b.z : 1f);
            bool differs = Mathf.Abs(ratio.x - 1f) > 1e-3f || Mathf.Abs(ratio.y - 1f) > 1e-3f || Mathf.Abs(ratio.z - 1f) > 1e-3f;
            Notes.Add(objectName + " " + setName + ": FBX hand_r lossyScale " + a.ToString("F4") + ", target " + b.ToString("F4") + (differs ? " -> corrected by " + ratio.ToString("F4") : " (same)"));
            if (differs)
            {
                localPos = Vector3.Scale(localPos, ratio);
                localScale = Vector3.Scale(localScale, ratio);
            }

            var axe = new GameObject(objectName);
            axe.transform.SetParent(handR, false);
            axe.transform.localPosition = localPos;
            axe.transform.localRotation = localRot;
            axe.transform.localScale = localScale;
            axe.layer = 0;
            axe.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = axe.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            axe.SetActive(false);
            return axe;
        }
        finally { Object.DestroyImmediate(source); }
    }

    // ---------- clips and controllers ----------

    static AnimationClip FindClip(string fbx, string clipName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
    }

    static AnimationClip ArmClip(string shortName) { return FindClip(AnimFolder + "A_FP_PCH_Handgun_" + shortName + ".fbx", "A_FP_PCH_Handgun_" + shortName); }
    static AnimationClip GunClip(string clipName) { return FindClip(GunFbx, clipName); }

    static AnimatorController NewController(string path)
    {
        if (!AssetDatabase.IsValidFolder(AnimatorFolder)) AssetDatabase.CreateFolder("Assets/Generated", "Animators");
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Aim", AnimatorControllerParameterType.Float);
        controller.AddParameter("ReloadTime", AnimatorControllerParameterType.Float);
        controller.AddParameter("SwingRate", AnimatorControllerParameterType.Float);
        var ps = controller.parameters;
        for (int i = 0; i < ps.Length; i++) if (ps[i].name == "SwingRate") ps[i].defaultFloat = 1f;
        controller.parameters = ps;
        return controller;
    }

    static AnimatorState BlendState(AnimatorController c, string name, AnimationClip a, AnimationClip b)
    {
        BlendTree tree;
        AnimatorState state = c.CreateBlendTreeInController(name, out tree);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Aim";
        tree.useAutomaticThresholds = false;
        tree.AddChild(a, 0f);
        tree.AddChild(b, 1f);
        state.writeDefaultValues = true;
        return state;
    }

    static AnimatorState ClipState(AnimatorStateMachine sm, string name, AnimationClip clip, bool reloadTime)
    {
        AnimatorState state = sm.AddState(name);
        state.motion = clip;
        state.writeDefaultValues = true;
        if (reloadTime) { state.timeParameterActive = true; state.timeParameter = "ReloadTime"; }
        return state;
    }

    static void Exit(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = duration;
    }

    static AnimatorController BuildArmsController()
    {
        AnimatorController c = NewController(ArmsControllerPath);
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        AnimatorState pose = BlendState(c, "Pose", ArmClip("Idle_Pose"), ArmClip("Aim_Pose"));
        AnimatorState fire = BlendState(c, "Fire", ArmClip("Fire"), ArmClip("Aim_Fire"));
        AnimatorState reload = ClipState(sm, "Reload", ArmClip("Reload"), true);
        AnimationClip emptyReloadArms = FindClip(ReloadFlourishArmsFbx, ReloadFlourishArmsClip);
        if (emptyReloadArms == null) Notes.Add("WARNING: reload flourish arms clip not found; LPSP empty reload used");
        AnimatorState reloadEmpty = ClipState(sm, "ReloadEmpty", emptyReloadArms != null ? emptyReloadArms : ArmClip("Reload_Empty"), true);
        sm.defaultState = pose;
        Exit(fire, pose, 0.9f, 0.08f);
        // v0.7 axe: held in AxeIdle, the swing returns to it; entered only by the presenter (no transitions into these).
        AnimationClip axeIdle = FindClip(AxeIdleFbx, "A_FP_Axe_Idle"), axeSwing = FindClip(AxeSwingFbx, "A_FP_Axe_Swing");
        if (axeIdle != null && axeSwing != null)
        {
            AnimatorState axeIdleState = ClipState(sm, "AxeIdle", axeIdle, false);
            AnimatorState axeSwingState = ClipState(sm, "AxeSwing", axeSwing, false);
            Exit(axeSwingState, axeIdleState, 0.97f, 0.05f);
        }
        else Notes.Add("WARNING: axe clips not found; AxeIdle/AxeSwing states not added");
        AddHandActionStates(sm, pose);
        // Safety net: if the presenter never ends a reload, the clip returns to the pose by itself.
        Exit(reload, pose, 0.99f, 0.15f);
        Exit(reloadEmpty, pose, 0.99f, 0.15f);
        EditorUtility.SetDirty(c);
        return c;
    }

    // v0.8: one state per clip, named after the clip without the A_FP_ prefix. Actions end through the presenter; only the swing and draw clips return by themselves.
    static void AddHandActionStates(AnimatorStateMachine sm, AnimatorState pose)
    {
        var states = new Dictionary<string, AnimatorState>();
        foreach (string n in HandActionClips)
        {
            AnimationClip clip = FindClip(HandActionFolder + n + ".fbx", n);
            if (clip == null) { Notes.Add("WARNING: clip not found: " + n); continue; }
            states[n.Substring(5)] = ClipState(sm, n.Substring(5), clip, false);
        }
        AnimationClip batIdle = FindClip(BatIdleFbx, "A_FP_Bat_Idle");
        AnimatorState batIdleState = batIdle != null ? ClipState(sm, "Bat_Idle", batIdle, false) : null;
        if (batIdleState != null)
        {
            if (states.ContainsKey("Bat_Draw")) Exit(states["Bat_Draw"], batIdleState, 0.96f, 0.06f);
            if (states.ContainsKey("Bat_Swing"))
            {
                states["Bat_Swing"].speedParameterActive = true;
                states["Bat_Swing"].speedParameter = "SwingRate";
                Exit(states["Bat_Swing"], batIdleState, 0.97f, 0.05f);
            }
        }
        AnimatorState axeIdle = sm.states.Select(c => c.state).FirstOrDefault(x => x.name == "AxeIdle");
        if (axeIdle != null && states.ContainsKey("Axe_Draw")) Exit(states["Axe_Draw"], axeIdle, 0.96f, 0.06f);
        if (states.ContainsKey("Handgun_Raise")) Exit(states["Handgun_Raise"], pose, 0.95f, 0.06f);
        // LPSP holster / unholster, played faster than authored so a swap stays short.
        AnimationClip draw = ArmClip("Unholster"), holster = ArmClip("Holster");
        if (draw != null)
        {
            AnimatorState s = ClipState(sm, "Pistol_Draw", draw, false);
            s.speed = 1.67f;
            Exit(s, pose, 0.95f, 0.08f);
        }
        if (holster != null) ClipState(sm, "Pistol_Holster", holster, false).speed = 2.0f;
    }

    static AnimatorController BuildPistolController()
    {
        AnimatorController c = NewController(PistolControllerPath);
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        AnimatorState idle = ClipState(sm, "Idle", GunClip("A_WEP_Handgun_03_Reference"), false);
        AnimatorState fire = ClipState(sm, "Fire", GunClip("A_WEP_Handgun_03_Fire"), false);
        ClipState(sm, "SlideBack", GunClip("A_WEP_Handgun_03_Slide_Back_Pose"), false); // no exit: holds the pose
        AnimatorState reload = ClipState(sm, "Reload", GunClip("A_FP_WEP_Handgun_03_Reload"), true);
        AnimationClip emptyReloadGun = FindClip(ReloadFlourishGunFbx, ReloadFlourishGunClip);
        if (emptyReloadGun == null) Notes.Add("WARNING: reload flourish gun clip not found; LPSP empty reload used");
        AnimatorState reloadEmpty = ClipState(sm, "ReloadEmpty", emptyReloadGun != null ? emptyReloadGun : GunClip("A_FP_WEP_Handgun_03_Reload_Empty"), true);
        sm.defaultState = idle;
        Exit(fire, idle, 0.95f, 0.02f);
        Exit(reload, idle, 0.99f, 0.15f);
        Exit(reloadEmpty, idle, 0.99f, 0.15f);
        EditorUtility.SetDirty(c);
        return c;
    }

    // ---------- viewmodel ----------

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static GameObject Place(string path, string name, Transform parent)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        instance.name = name;
        if (PrefabUtility.IsPartOfPrefabInstance(instance))
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.transform.SetParent(parent, false);
        return instance;
    }

    static Animator EnsureAnimator(GameObject root, string fbx, RuntimeAnimatorController controller)
    {
        var animator = root.GetComponent<Animator>();
        if (animator == null)
        {
            animator = root.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return animator;
    }

    // Arms go under `viewmodel` at the origin (the arms FBX root is the camera point), the pistol under ik_hand_gun
    // (local position 0, rotation Euler(90,0,0) as in the LPSP inventory), the magazine under SOCKET_Magazine.
    // Returns false (and builds nothing) when an art file is missing so the builder can fall back to the primitive pistol.
    public static bool BuildArmsViewmodel(Transform viewmodel, out ViewmodelRig rig)
    {
        rig = null;
        Notes.Clear();
        if (!AssetsPresent()) { Notes.Add("LPSP art incomplete; primitive pistol fallback"); return false; }
        LookDevSetup.EnsureFolders();
        ApplyMaterials();
        Material axeMaterial;
        PrepareAxe(out axeMaterial);
        PrepareReloadFlourish();
        PrepareHandActions();
        Material batMaterial = BatMaterial(), bandageMaterial = BandageMaterial();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AnimatorController armsController = BuildArmsController();
        AnimatorController pistolController = BuildPistolController();
        AssetDatabase.SaveAssets();

        GameObject arms = Place(ArmsFbx, "Arms", viewmodel);
        arms.transform.localPosition = Vector3.zero;
        arms.transform.localRotation = Quaternion.identity;
        Transform hand = FindDeep(arms.transform, "ik_hand_gun");
        if (hand == null) { Notes.Add("ik_hand_gun missing; primitive pistol fallback"); Object.DestroyImmediate(arms); return false; }

        GameObject gun;
        Transform muzzleFx, ejectFx;
        if (!AttachPistol(hand, out gun, out muzzleFx, out ejectFx)) { Notes.Add("pistol sockets missing; primitive pistol fallback"); Object.DestroyImmediate(arms); return false; }

        rig = new ViewmodelRig
        {
            armsAnimator = EnsureAnimator(arms, ArmsFbx, armsController),
            pistolAnimator = EnsureAnimator(gun, GunFbx, pistolController),
            muzzle = muzzleFx,
            eject = ejectFx,
            armsRoot = arms
        };
        if (arms.GetComponent<ViewmodelAnimationEvents>() == null) arms.AddComponent<ViewmodelAnimationEvents>();
        if (gun.GetComponent<ViewmodelAnimationEvents>() == null) gun.AddComponent<ViewmodelAnimationEvents>();

        foreach (var c in arms.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var t in arms.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        foreach (var r in arms.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true;
            if (r.sharedMaterials.Any(m => m == null)) Notes.Add("WARNING: null material on " + r.name);
        }
        Notes.Add("Viewmodel uses Infima LPSP arms + Handgun_03");
        Transform lpspHandR = FindDeep(arms.transform, "hand_r");
        if (axeMaterial != null && lpspHandR != null) rig.axe = AttachAxe(lpspHandR, axeMaterial, "LPSP");
        if (batMaterial != null && lpspHandR != null) rig.bat = AttachHeld(lpspHandR, batMaterial, "LPSP", BatIdleFbx, BatMeshNode, "Bat");
        AttachBandageProps(arms.transform, bandageMaterial, out rig.bandageRoll, out rig.bandageRings);
        try { rig.survivalist = BuildSurvivalist(viewmodel, armsController, pistolController, hand, axeMaterial, batMaterial, bandageMaterial); }
        catch (System.Exception e) { Notes.Add("WARNING: Survivalist arms build failed: " + e.Message); rig.survivalist = null; }
        return true;
    }

    // Pistol + magazine + effect anchors under a hand-gun anchor (shared by both arm sets).
    static bool AttachPistol(Transform hand, out GameObject gun, out Transform muzzleFx, out Transform ejectFx)
    {
        muzzleFx = ejectFx = null;
        gun = Place(GunFbx, "Handgun_03", hand);
        gun.transform.localPosition = Vector3.zero;
        gun.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Transform magSocket = FindDeep(gun.transform, "SOCKET_Magazine");
        Transform muzzle = FindDeep(gun.transform, "SOCKET_Muzzle");
        Transform eject = FindDeep(gun.transform, "SOCKET_Eject");
        if (magSocket == null || muzzle == null || eject == null) return false;

        GameObject mag = Place(MagFbx, "Magazine", magSocket);
        mag.transform.localPosition = Vector3.zero;
        mag.transform.localRotation = Quaternion.identity; // node rotation of the static FBX is the Blender->Unity fix-up; the socket frame already carries it
        mag.transform.localScale = Vector3.one;

        // The LPSP sockets are Blender-oriented (barrel = socket -Y). Effect anchors under them give the code a Unity-style frame:
        // MuzzleFx +Z along the barrel; EjectFx +X to the player's right and +Y up (rotation measured in the Idle pose, log lines QA_SCREENS eject/muzzle).
        muzzleFx = new GameObject("MuzzleFx").transform;
        muzzleFx.SetParent(muzzle, false);
        muzzleFx.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ejectFx = new GameObject("EjectFx").transform;
        ejectFx.SetParent(eject, false);
        ejectFx.localRotation = Quaternion.LookRotation(new Vector3(-0.01f, -1f, 0.008f), new Vector3(-0.8f, 0f, -0.6f));
        return true;
    }

    // ---------- Survivalist arms (v0.5a) ----------

    static Material SurvLit(string name, string baseMap, string normalMap, string aoMap, float smoothness)
    {
        string path = LookDevSetup.MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(SurvTex + baseMap);
        if (albedo == null) Notes.Add("WARNING: Survivalist texture missing: " + baseMap);
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", smoothness);
        if (normalMap != null)
        {
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SurvTex + normalMap));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        if (aoMap != null)
        {
            m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SurvTex + aoMap));
            m.SetFloat("_OcclusionStrength", 1f);
            m.EnableKeyword("_OCCLUSIONMAP");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SurvNote(string text) { Notes.Add("Survivalist: " + text); }

    static Transform NewBone(string name, Transform parent, Transform template)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = template.localPosition;
        t.localRotation = template.localRotation;
        t.localScale = template.localScale;
        return t;
    }

    // Approach 1: the Survivalist skeleton has the same bone names, local transforms and world scale as the LPSP arms
    // (measured, Logs/arms-hierarchy.txt); only the "Armature" wrapper and the ik_hand_* helper bones are missing. They are recreated with
    // LPSP's exact local transforms so the LPSP generic clips (VM_Arms.controller) bind by path and the pistol follows ik_hand_gun.
    static AlternateArms BuildSurvivalist(Transform viewmodel, AnimatorController armsController, AnimatorController pistolController, Transform lpspHandGun, Material axeMaterial, Material batMaterial, Material bandageMaterial)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SurvFbx) == null) { SurvNote("FBX missing; set not built"); return null; }
        Transform lpspIkRoot = lpspHandGun.parent;
        Transform lpspIkL = FindDeep(lpspHandGun, "ik_hand_l"), lpspIkR = FindDeep(lpspHandGun, "ik_hand_r");

        GameObject arms = Place(SurvFbx, "Arms_Survivalist", viewmodel);
        arms.transform.localPosition = Vector3.zero;
        arms.transform.localRotation = Quaternion.identity;
        arms.transform.localScale = Vector3.one;

        // Keep only the FPS meshes; the third-person body modules are not needed (and cost skinning time).
        Transform fpsHands = FindDeep(arms.transform, "FPS_HANDS");
        foreach (var smr in arms.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (fpsHands == null || !smr.transform.IsChildOf(fpsHands)) Object.DestroyImmediate(smr.gameObject);
        var oldAnimator = arms.GetComponent<Animator>();
        if (oldAnimator != null) Object.DestroyImmediate(oldAnimator);

        Transform root = FindDeep(arms.transform, "root");
        Transform hand = FindDeep(arms.transform, "hand_r");
        if (root == null || hand == null) { SurvNote("root/hand_r missing; set not built"); Object.DestroyImmediate(arms); return null; }

        // Armature wrapper (LPSP: Armature localRotation = Euler(270,0,0), root local identity under it).
        var armature = new GameObject("Armature").transform;
        armature.SetParent(arms.transform, false);
        armature.localPosition = Vector3.zero;
        armature.localRotation = Quaternion.Euler(270f, 0f, 0f);
        Quaternion rootBefore = root.rotation; Vector3 rootPosBefore = root.position;
        root.SetParent(armature, false);
        root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;
        SurvNote("root world rotation before/after wrapper: " + rootBefore.eulerAngles.ToString("F1") + " / " + root.rotation.eulerAngles.ToString("F1") + ", pos delta " + (root.position - rootPosBefore).magnitude.ToString("F5"));

        // ik_hand_root / ik_hand_gun / ik_hand_l / ik_hand_r with LPSP's local transforms.
        Transform ikRoot = NewBone("ik_hand_root", root, lpspIkRoot);
        Transform ikGun = NewBone("ik_hand_gun", ikRoot, lpspHandGun);
        if (lpspIkL != null) NewBone("ik_hand_l", ikGun, lpspIkL);
        if (lpspIkR != null) NewBone("ik_hand_r", ikGun, lpspIkR);
        // Idle-pose relation between the gun anchor and the Survivalist hand_r (measured, reported).
        Vector3 relPos = hand.InverseTransformPoint(ikGun.position);
        Quaternion relRot = Quaternion.Inverse(hand.rotation) * ikGun.rotation;
        SurvNote("ik_hand_gun in hand_r space (bind pose): pos " + relPos.ToString("F4") + " rot " + relRot.eulerAngles.ToString("F1"));

        GameObject gun;
        Transform muzzleFx, ejectFx;
        if (!AttachPistol(ikGun, out gun, out muzzleFx, out ejectFx)) { SurvNote("pistol sockets missing; set not built"); Object.DestroyImmediate(arms); return null; }

        // Materials. FPS_Hands1_Skin1.mat (package) uses the Hands3 skin textures; Gloves1 / Shirt3 skin1 for the others.
        Material skin = SurvLit("VM_Surv_Skin", "T_Military_Hands3_color_skin1.png", "T_Military_Hands3_nmap.png", "T_Military_Hands3_ao.png", 0.25f);
        Material glove = SurvLit("VM_Surv_Gloves", "T_Military_Gloves1_color_skin1.png", "T_Military_Gloves1_nmap.png", "T_Military_Gloves1_ao.png", 0.2f);
        Material shirt = SurvLit("VM_Surv_Shirt", "T_Military_Shirt3_Skin1_color.png", "T_Military_Shirt3_nmap.png", "T_Military_Shirt3_ao.png", 0.15f);
        AssetDatabase.SaveAssets();
        foreach (var smr in fpsHands.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] slots = new Material[smr.sharedMaterials.Length];
            bool isGlove = smr.name.Contains("Gloves"), isShirt = smr.name.Contains("Shirt");
            for (int i = 0; i < slots.Length; i++) slots[i] = isShirt ? shirt : isGlove ? glove : skin;
            smr.sharedMaterials = slots;
            smr.gameObject.SetActive(isGlove ? SurvivalistUseGloves : (isShirt || !SurvivalistUseGloves));
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = true;
            smr.updateWhenOffscreen = true;
            SurvNote("mesh " + smr.name + " slots=" + slots.Length + " bones=" + smr.bones.Length + " active=" + smr.gameObject.activeSelf);
            if (smr.rootBone == null) SurvNote("WARNING: " + smr.name + " has no root bone");
        }
        foreach (var c in arms.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var t in arms.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;

        // Generic animator on the root with the LPSP controllers; the LPSP generic avatar matches bones by path.
        var armsAnimator = arms.AddComponent<Animator>();
        armsAnimator.avatar = AssetDatabase.LoadAllAssetsAtPath(ArmsFbx).OfType<Avatar>().FirstOrDefault();
        armsAnimator.runtimeAnimatorController = armsController;
        armsAnimator.applyRootMotion = false;
        armsAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var pistolAnimator = EnsureAnimator(gun, GunFbx, pistolController);
        arms.AddComponent<ViewmodelAnimationEvents>();
        if (gun.GetComponent<ViewmodelAnimationEvents>() == null) gun.AddComponent<ViewmodelAnimationEvents>();
        foreach (var r in arms.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            if (r.sharedMaterials.Any(m => m == null)) SurvNote("WARNING: null material on " + r.name);
        }
        GameObject axe = axeMaterial != null ? AttachAxe(hand, axeMaterial, "Survivalist") : null;
        GameObject bat = batMaterial != null ? AttachHeld(hand, batMaterial, "Survivalist", BatIdleFbx, BatMeshNode, "Bat") : null;
        GameObject bandageRoll;
        GameObject[] bandageRings;
        AttachBandageProps(arms.transform, bandageMaterial, out bandageRoll, out bandageRings);
        arms.SetActive(false);
        SurvNote("set built (inactive by default)");
        return new AlternateArms { root = arms, armsAnimator = armsAnimator, pistolAnimator = pistolAnimator, muzzle = muzzleFx, eject = ejectFx, axe = axe, bat = bat, bandageRoll = bandageRoll, bandageRings = bandageRings };
    }
}
