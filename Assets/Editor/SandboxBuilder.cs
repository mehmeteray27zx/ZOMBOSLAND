using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zombosland;

// Generates the whole combat sandbox scene. Never hand-edit CombatSandbox.unity; change this and re-run.
public static class SandboxBuilder
{
    const string ScenePath = "Assets/Scenes/CombatSandbox.unity";
    internal static readonly List<string> Report = new List<string>();

    // ---------- helpers ----------

    internal static Material Mat(string name, Color c, float smooth = 0.1f)
    {
        string path = "Assets/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.name = name; m.color = c; m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Box with world-scaled UVs and a matching BoxCollider. `size` is the real size; scale stays 1.
    internal static GameObject Box(string name, Vector3 position, Vector3 size, Material material, float tile, Transform parent = null, float yaw = 0f)
    {
        var o = new GameObject(name);
        o.layer = Layers.Environment;
        if (parent != null) o.transform.SetParent(parent, false);
        o.transform.localPosition = position;
        o.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        o.AddComponent<MeshFilter>().sharedMesh = BoxMeshFactory.Create(size, tile);
        o.AddComponent<MeshRenderer>().sharedMaterial = material;
        o.AddComponent<BoxCollider>().size = size;
        return o;
    }

    internal static void RegisterLayers()
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        layers.GetArrayElementAtIndex(Layers.ZombieMover).stringValue = "ZombieMover";
        layers.GetArrayElementAtIndex(Layers.Player).stringValue = "Player";
        layers.GetArrayElementAtIndex(Layers.Corpse).stringValue = "Corpse";
        layers.GetArrayElementAtIndex(Layers.ZombieHitbox).stringValue = "ZombieHitbox";
        layers.GetArrayElementAtIndex(Layers.Environment).stringValue = "Environment";
        layers.GetArrayElementAtIndex(Layers.Interactable).stringValue = "Interactable";
        tags.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- build ----------

    [MenuItem("Zombosland/Regenerate and Build (overwrites scene)")]
    public static void Build()
    {
        GenerateScene();
        WriteBuildSettings();
        BuildOnly();
    }

    // Project-wide setup shared by every scene builder (idempotent).
    internal static void PrepareProject()
    {
        var inputSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input = inputSettings.FindProperty("activeInputHandler");
        if (input != null) { input.intValue = 0; inputSettings.ApplyModifiedPropertiesWithoutUndo(); }
        PlayerSettings.companyName = "Zombosland";
        PlayerSettings.productName = "Zombosland Combat Sandbox";
        PlayerSettings.bundleVersion = "0.7";
        PlayerSettings.enableFrameTimingStats = true;
        RegisterLayers();
        GraphicsSettings.lightsUseLinearIntensity = true;
        GraphicsSettings.lightsUseColorTemperature = true;

        LookDevSetup.EnsureFolders();
        LookDevSetup.ImportPolyHavenTextures();
        LookDevSetup.ConfigureUrpAsset();
        LookDevSetup.ConfigureRenderer();
        AssetDatabase.SaveAssets();
    }

    // The shared material set (flat fallbacks, accents, PBR sets) both scenes use.
    internal sealed class MaterialSet
    {
        public Material concreteFlat, wallFlat, metalFlat, accent, coat, skin, denim, gunFlat, glass;
        public Material asphalt, floorWorn, wall, brick, paintedConcrete, rustyMetal, metalPlate;
    }

    internal static MaterialSet CreateMaterials()
    {
        var m = new MaterialSet();
        // Flat fallbacks (also used for coloured accents) + PBR sets.
        m.concreteFlat = Mat("Concrete", new Color(.18f, .22f, .24f));
        m.wallFlat = Mat("Wall", new Color(.28f, .33f, .34f));
        m.metalFlat = Mat("Metal", new Color(.09f, .12f, .13f), .5f);
        m.accent = Mat("Accent", new Color(.8f, .36f, .1f));
        m.coat = Mat("ZombieCoat", new Color(.34f, .38f, .45f));
        m.skin = Mat("ZombieSkin", new Color(.46f, .53f, .43f));
        m.denim = Mat("ZombiePants", new Color(.08f, .10f, .17f), .15f);
        m.gunFlat = Mat("Gun", new Color(.08f, .1f, .11f), .35f);
        m.glass = Mat("WindowGlass", new Color(.17f, .19f, .22f), .9f);
        m.glass.SetFloat("_Metallic", .3f);
        m.asphalt = LookDevSetup.MakePbr("asphalt_02", 3f, m.concreteFlat);
        m.floorWorn = LookDevSetup.MakePbr("concrete_floor_worn_001", 2f, m.concreteFlat, new Color(.72f, .68f, .6f));
        m.wall = LookDevSetup.MakePbr("concrete_wall_008", 3f, m.wallFlat);
        m.brick = LookDevSetup.MakePbr("red_brick_03", 2f, m.wallFlat);
        m.paintedConcrete = LookDevSetup.MakePbr("painted_concrete", 1.5f, m.wallFlat, new Color(1f, .72f, .86f));
        m.rustyMetal = LookDevSetup.MakePbr("rusty_metal_02", 2f, m.metalFlat);
        m.metalPlate = LookDevSetup.MakePbr("metal_plate", 0.4f, m.gunFlat);
        return m;
    }

    // Serialized per-scene weapon settings on the Sandbox component.
    internal static void ConfigureSandbox(Sandbox game, int startReserve, float noiseRadius)
    {
        var so = new SerializedObject(game);
        so.FindProperty("startReserve").intValue = startReserve;
        so.FindProperty("noiseRadius").floatValue = noiseRadius;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Build order: 0 = IlkIkmal (when it exists), 1 = CombatSandbox.
    internal static void WriteBuildSettings()
    {
        const string ilk = "Assets/Scenes/IlkIkmal.unity";
        if (File.Exists(ilk))
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ilk, true), new EditorBuildSettingsScene(ScenePath, true) };
        else
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    internal static void GenerateScene()
    {
        Report.Clear();
        LookDevSetup.Report.Clear();
        PrepareProject();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var mats = CreateMaterials();
        Material asphalt = mats.asphalt, floorWorn = mats.floorWorn, wallMat = mats.wall, brick = mats.brick, paintedConcrete = mats.paintedConcrete;
        Material rustyMetal = mats.rustyMetal, metalPlate = mats.metalPlate, glass = mats.glass, accent = mats.accent;

        LookDevSetup.SetupSky();
        BuildLighting();
        BuildWorld(asphalt, floorWorn, wallMat, brick, paintedConcrete, rustyMetal, glass, accent);

        var player = BuildPlayer(metalPlate, rustyMetal, accent);
        var game = player.GetComponent<Sandbox>();
        ConfigureSandbox(game, 72, 30f);

        var zombiePrefabs = ZombieFactory.CreatePrefabs(mats.coat, mats.skin, mats.denim, Report);
        BuildDirectorAndZombies(game, zombiePrefabs);
        BuildEffectsAndAudio();
        BuildPostProcessing(player.GetComponentInChildren<Camera>());

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        foreach (string line in LookDevSetup.Report) Report.Add(line);
        foreach (string line in Report) Debug.Log("ZOMBOSLAND_BUILD_NOTE: " + line);
    }

    internal static void BuildLighting(Vector3? probePosition = null, Vector3? probeSize = null)
    {
        var go = new GameObject("Overcast sun");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.useColorTemperature = true;
        light.colorTemperature = 6200f;
        light.color = Color.white;
        light.intensity = 1.7f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 1f;
        go.transform.rotation = Quaternion.Euler(48, -35, 0);
        RenderSettings.sun = light;

        var probe = new GameObject("Reflection Probe").AddComponent<ReflectionProbe>();
        probe.transform.position = probePosition ?? new Vector3(0, 3, 8);
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = probeSize ?? new Vector3(34, 12, 48);
        probe.boxProjection = true;
        probe.resolution = 128;
        probe.hdr = true;
    }

    static void BuildWorld(Material asphalt, Material floorWorn, Material wall, Material brick, Material painted, Material rusty,
                           Material glass, Material accent)
    {
        // Abandoned urban yard: asphalt floor, concrete perimeter (4.5 m), brick facades, barriers, containers, crates.
        Box("Ground", new Vector3(0, -.5f, 8), new Vector3(32, 1, 45), asphalt, 3f);
        Box("Far wall", new Vector3(0, 2.25f, 29), new Vector3(32, 4.5f, 1), wall, 3f);
        Box("Left wall", new Vector3(-16, 2.25f, 8), new Vector3(1, 4.5f, 45), wall, 3f);
        Box("Right wall", new Vector3(16, 2.25f, 8), new Vector3(1, 4.5f, 45), wall, 3f);
        Box("Back wall", new Vector3(0, 2.25f, -14), new Vector3(32, 4.5f, 1), wall, 3f);

        for (int s = -1; s <= 1; s += 2)
        {
            float x = 14.9f * s;
            Box(s < 0 ? "Left facade" : "Right facade", new Vector3(x, 4.5f, 12), new Vector3(1.2f, 9, 24), brick, 2f);
            float face = x - s * 0.6f; // inner face of the facade
            foreach (float z in new[] { 4f, 10f, 16f, 22f })
                foreach (float y in new[] { 2.1f, 5.3f })
                {
                    Box("Window", new Vector3(face - s * 0.006f, y, z), new Vector3(0.03f, 1.7f, 2.0f), glass, 1f);
                    Box("Window sill", new Vector3(face - s * 0.08f, y - 0.95f, z), new Vector3(0.2f, 0.12f, 2.4f), wall, 1.5f);
                    Box("Window lintel", new Vector3(face - s * 0.05f, y + 0.93f, z), new Vector3(0.14f, 0.12f, 2.3f), wall, 1.5f);
                }
        }

        // Concrete floor patches over the asphalt.
        foreach (float x in new[] { -6f, 0f, 6f })
            Object.DestroyImmediate(Box("Floor strip", new Vector3(x, 0.006f, -2), new Vector3(2.4f, 0.004f, 20), floorWorn, 2f).GetComponent<BoxCollider>());

        // Three barricades (kept from the original layout) with warning stripes.
        for (int i = -1; i <= 1; i++)
        {
            Box("Barricade", new Vector3(i * 7, 0.65f, 12), new Vector3(3, 1.3f, 1), painted, 1.5f);
            Box("Warning stripe", new Vector3(i * 7, 1.3f, 11.48f), new Vector3(3, .12f, .04f), accent, 1f);
        }
        // Jersey barriers: three stepped boxes each.
        Jersey(new Vector3(-8, 0, 5), 0f, painted);
        Jersey(new Vector3(9, 0, 7), 20f, painted);
        Jersey(new Vector3(-3, 0, 18), 90f, painted);
        Jersey(new Vector3(5, 0, 17), 0f, painted);

        // Containers and crate stacks.
        Box("Container", new Vector3(-8.5f, 1.2f, 22), new Vector3(6, 2.4f, 2.6f), rusty, 2f);
        Box("Container", new Vector3(10, 1.2f, 23), new Vector3(6, 2.4f, 2.6f), rusty, 2f, null, 12f);
        for (int k = -1; k <= 1; k++) Box("Storage crate", new Vector3(-12 + k * 2, .6f, 17), new Vector3(1.6f, 1.2f, 1.6f), rusty, 1f);
        Box("Storage crate", new Vector3(-13, 1.8f, 17), new Vector3(1.6f, 1.2f, 1.6f), rusty, 1f, null, 15f);
        Box("Storage crate", new Vector3(-11, 1.8f, 17), new Vector3(1.6f, 1.2f, 1.6f), rusty, 1f, null, -10f);
        Box("Storage crate", new Vector3(12, .6f, 14), new Vector3(1.6f, 1.2f, 1.6f), rusty, 1f);
        Box("Storage crate", new Vector3(12, .6f, 15.8f), new Vector3(1.6f, 1.2f, 1.6f), rusty, 1f);

        // Fixed test obstruction: name, position and size are used by validation and --qa-wall.
        Box("Near obstruction", new Vector3(4, 1, 3), new Vector3(2, 2, 1), wall, 3f);
        Box("Near stripe", new Vector3(4, 1.7f, 2.48f), new Vector3(2, .12f, .04f), accent, 1f);
    }

    static void Jersey(Vector3 position, float yaw, Material material)
    {
        var parent = new GameObject("Jersey barrier").transform;
        parent.position = position;
        parent.rotation = Quaternion.Euler(0, yaw, 0);
        Box("Base", new Vector3(0, 0.125f, 0), new Vector3(0.8f, 0.25f, 3f), material, 1.5f, parent);
        Box("Middle", new Vector3(0, 0.425f, 0), new Vector3(0.5f, 0.35f, 3f), material, 1.5f, parent);
        Box("Top", new Vector3(0, 0.8f, 0), new Vector3(0.3f, 0.4f, 3f), material, 1.5f, parent);
    }

    // ---------- player ----------

    internal static GameObject BuildPlayer(Material gunMat, Material metal, Material accent)
    {
        var player = new GameObject("Player");
        player.layer = Layers.Player;
        player.transform.position = new Vector3(0, .05f, -4);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.center = Vector3.up * .9f; cc.radius = .3f;

        var pivot = new GameObject("AimPivot");
        pivot.transform.SetParent(player.transform, false);
        pivot.transform.localPosition = Vector3.up * 1.65f;

        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(pivot.transform, false);
        cameraObject.tag = "MainCamera";
        var cam = cameraObject.AddComponent<Camera>();
        cam.nearClipPlane = 0.03f; cam.farClipPlane = 300f; cam.fieldOfView = 75f;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<CameraEffects>();

        var vm = new GameObject("Viewmodel");
        vm.transform.SetParent(cameraObject.transform, false);
        vm.transform.localPosition = new Vector3(.20f, -.20f, .55f);
        Transform muzzle = null, slide = null, magazine = null;
        ViewmodelRig rig;
        bool animated = ViewmodelFactory.BuildArmsViewmodel(vm.transform, out rig);
        foreach (string note in ViewmodelFactory.Notes) Report.Add(note);
        if (animated) vm.transform.localPosition = Vector3.zero; // arms FBX root is the camera point
        else BuildViewmodel(vm.transform, gunMat, metal, accent, out muzzle, out slide, out magazine);
        var presenter = vm.AddComponent<ViewmodelPresenter>();
        presenter.muzzle = muzzle; presenter.slide = slide; presenter.magazine = magazine; presenter.adsPosition = adsPosition;
        if (animated)
        {
            presenter.armsAnimator = rig.armsAnimator; presenter.pistolAnimator = rig.pistolAnimator;
            presenter.muzzle = rig.muzzle; presenter.eject = rig.eject; presenter.animated = true;
            presenter.defaultArmsRoot = rig.armsRoot; presenter.survivalist = rig.survivalist;
            presenter.axe = rig.axe;
            presenter.bat = rig.bat; presenter.bandageRoll = rig.bandageRoll; presenter.bandageRings = rig.bandageRings;
        }

        player.AddComponent<PlayerMotor>();
        player.AddComponent<Sandbox>();
        player.AddComponent<Hud>();
        return player;
    }

    static Vector3 adsPosition = new Vector3(0f, -0.04f, 0.34f);

    static Material GunMetal()
    {
        const string path = LookDevSetup.MaterialFolder + "/GunMetal.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(.06f, .065f, .07f));
        m.SetFloat("_Metallic", .8f);
        m.SetFloat("_Smoothness", .45f);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(LookDevSetup.PolyHaven + "/metal_plate/metal_plate_normal.jpg");
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", .4f); m.EnableKeyword("_NORMALMAP"); m.SetTextureScale("_BumpMap", new Vector2(1f, 1f)); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material GunPolymer()
    {
        const string path = LookDevSetup.MaterialFolder + "/GunPolymer.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(.045f, .047f, .05f));
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", .25f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildViewmodel(Transform vm, Material gunMat, Material metal, Material accent, out Transform muzzle, out Transform slide, out Transform magazine)
    {
        muzzle = slide = magazine = null;
        const string pistolPath = "Assets/Art/Weapons/Pistol/pistol.fbx";
        var pistol = AssetDatabase.LoadAssetAtPath<GameObject>(pistolPath);
        if (pistol != null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(pistol);
            model.name = "Pistol";
            model.transform.SetParent(vm, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            Bounds b = RenderBounds(model);
            if (b.size.z > 0.001f) model.transform.localScale *= 0.2f / b.size.z; // 0.2 m long
            slide = FindDeep(model.transform, "Slide");
            magazine = FindDeep(model.transform, "Magazine");
            var marker = FindDeep(model.transform, "Muzzle");
            Bounds nb = RenderBounds(model);
            Vector3 world = marker != null ? marker.position : new Vector3(nb.center.x, nb.center.y, nb.max.z);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(vm, false);
            muzzle.position = world;
            Report.Add("Viewmodel uses " + pistolPath);
        }
        else
        {
            // Primitive pistol, ~0.19 m long, ~0.13 m tall, 0.03 m wide; +Z is the muzzle direction.
            Material steel = GunMetal(), polymer = GunPolymer();
            Box2("Frame", new Vector3(0, -.012f, .075f), new Vector3(.026f, .03f, .15f), steel, vm, .1f);
            slide = Box2("Slide", new Vector3(0, .018f, .085f), new Vector3(.03f, .03f, .17f), steel, vm, .1f).transform;
            var grip = Box2("Grip", new Vector3(0, -.047f, .018f), new Vector3(.03f, .07f, .042f), polymer, vm, .1f);
            grip.transform.localRotation = Quaternion.Euler(15, 0, 0);
            magazine = Box2("Magazine", new Vector3(0, -.06f, .018f), new Vector3(.022f, .07f, .03f), steel, vm, .1f).transform;
            magazine.localRotation = Quaternion.Euler(15, 0, 0);
            Box2("TriggerGuardBottom", new Vector3(0, -.04f, .07f), new Vector3(.01f, .004f, .05f), polymer, vm, .1f);
            Box2("TriggerGuardFront", new Vector3(0, -.031f, .094f), new Vector3(.01f, .02f, .004f), polymer, vm, .1f);
            Box2("Trigger", new Vector3(0, -.03f, .058f), new Vector3(.006f, .016f, .005f), steel, vm, .1f);
            const float sightY = .0385f;
            Box2("RearSight", new Vector3(0, sightY, .012f), new Vector3(.012f, .01f, .006f), steel, vm, .1f);
            Box2("FrontSight", new Vector3(0, sightY, .163f), new Vector3(.005f, .01f, .006f), steel, vm, .1f);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(vm, false);
            muzzle.localPosition = new Vector3(0, .018f, .172f);
            adsPosition = new Vector3(0f, -sightY, .40f); // sight line on the camera axis
        }

        const string armsPath = "Assets/Art/Characters/Arms/arms.fbx";
        var arms = AssetDatabase.LoadAssetAtPath<GameObject>(armsPath);
        if (arms != null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(arms);
            model.name = "Arms";
            model.transform.SetParent(vm, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            Report.Add("Viewmodel uses " + armsPath);
        }
    }

    // Viewmodel parts: world-scaled box, no collider, no layer change.
    static GameObject Box2(string name, Vector3 position, Vector3 size, Material material, Transform parent, float tile = 0.4f)
    {
        var o = Box(name, position, size, material, tile, parent);
        Object.DestroyImmediate(o.GetComponent<BoxCollider>());
        o.layer = 0;
        return o;
    }

    static Bounds RenderBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // ---------- zombies, effects, audio, post ----------

    static void BuildDirectorAndZombies(Sandbox game, GameObject[] zombiePrefabs)
    {
        var director = new GameObject("ZombieDirector").AddComponent<ZombieDirector>();
        director.player = game;
        director.zombiePrefab = zombiePrefabs[0];
        director.zombiePrefabs = zombiePrefabs;
        for (int i = 0; i < 5; i++)
        {
            var z = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefabs[i % zombiePrefabs.Length]);
            z.name = "Zombie " + (i + 1);
            z.transform.position = new Vector3((i - 2) * 3, 0, 9 + i % 2 * 3);
            z.transform.rotation = Quaternion.Euler(0, 180, 0);
            var brain = z.GetComponent<ZombieBrain>();
            var so = new SerializedObject(brain);
            so.FindProperty("player").objectReferenceValue = game;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    internal static void BuildEffectsAndAudio()
    {
        var circle = LookDevSetup.SoftCircleTexture();
        var star = LookDevSetup.MuzzleStarTexture();
        var fx = new GameObject("Effects").AddComponent<EffectPool>();
        var lpspFlash = AssetDatabase.LoadAssetAtPath<Texture2D>(ViewmodelFactory.Root + "Art/Textures/Effects/T_MuzzleFlash_D.png");
        fx.flashMaterial = LookDevSetup.MakeParticleMaterial("Fx_MuzzleFlash", lpspFlash != null ? lpspFlash : star, LookDevSetup.Blend.Additive, new Color(1f, .78f, .45f, 1f));
        if (lpspFlash != null) Report.Add("Muzzle flash uses T_MuzzleFlash_D");
        Material casingMaterial;
        fx.casingMesh = ViewmodelFactory.CasingMesh(out casingMaterial);
        fx.casingMaterial = casingMaterial;
        if (fx.casingMesh != null) Report.Add("Shell casings use SM_Bullet_Small_Empty");
        fx.dustMaterial = LookDevSetup.MakeParticleMaterial("Fx_Dust", circle, LookDevSetup.Blend.Alpha, Color.white);
        fx.sparkMaterial = LookDevSetup.MakeParticleMaterial("Fx_Spark", circle, LookDevSetup.Blend.Additive, new Color(1f, .75f, .35f, 1f));
        fx.bloodMaterial = LookDevSetup.MakeParticleMaterial("Fx_Blood", circle, LookDevSetup.Blend.Alpha, Color.white);
        fx.decalMaterial = LookDevSetup.MakeDecalMaterial(circle);

        var audio = new GameObject("Audio").AddComponent<SfxBank>();
        var found = new List<SfxBank.Override>();
        var names = new Dictionary<string, Sfx>
        {
            {"gunshot", Sfx.Shot}, {"dry_fire", Sfx.DryFire}, {"mag_out", Sfx.MagOut}, {"mag_in", Sfx.MagIn}, {"slide", Sfx.Slide},
            {"hit_flesh", Sfx.HitFlesh}, {"hit_concrete", Sfx.HitConcrete}, {"zombie_growl", Sfx.ZombieGrowl},
            {"zombie_attack", Sfx.ZombieAttack}, {"zombie_death", Sfx.ZombieDeath}, {"player_hurt", Sfx.PlayerHurt}
        };
        foreach (var pair in names)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + pair.Key + ".wav");
            if (clip != null) { found.Add(new SfxBank.Override { id = pair.Value, clip = clip }); Report.Add("Audio override: " + pair.Key + ".wav"); }
        }
        // Infima LPSP recordings fill every id that has no Assets/Audio override; several files per id become variations.
        const string sfx = ViewmodelFactory.Root + "Audio/SFX/";
        var lpsp = new List<KeyValuePair<Sfx, string[]>>
        {
            new KeyValuePair<Sfx, string[]>(Sfx.Shot, new[] { "Weapons/Fire/S_WEP_Fire_002.wav", "Weapons/Fire/S_WEP_Fire_003.wav" }),
            new KeyValuePair<Sfx, string[]>(Sfx.DryFire, new[] { "Weapons/Fire/S_WEP_Fire_Empty.wav" }),
            new KeyValuePair<Sfx, string[]>(Sfx.HitConcrete, new[] { "Impacts/S_WEP_Impact_Bullet_01.wav", "Impacts/S_WEP_Impact_Bullet_02.wav", "Impacts/S_WEP_Impact_Bullet_03.wav", "Impacts/S_WEP_Impact_Bullet_04.wav" }),
            new KeyValuePair<Sfx, string[]>(Sfx.Casing, new[] { "Weapons/Casings/S_WEP_Casing_01.wav", "Weapons/Casings/S_WEP_Casing_02.wav", "Weapons/Casings/S_WEP_Casing_03.wav", "Weapons/Casings/S_WEP_Casing_04.wav", "Weapons/Casings/S_WEP_Casing_05.wav" }),
            new KeyValuePair<Sfx, string[]>(Sfx.ReloadTactical, new[] { "Weapons/Reloads/S_WEP_Handgun_Reload.wav" }),
            new KeyValuePair<Sfx, string[]>(Sfx.ReloadEmpty, new[] { "Weapons/Reloads/S_WEP_Handgun_Reload_Empty.wav" }),
            new KeyValuePair<Sfx, string[]>(Sfx.AimIn, new[] { "Weapons/Aiming/S_WEP_Aim_In.wav" })
        };
        foreach (var entry in lpsp)
        {
            if (found.Exists(o => o.id == entry.Key)) continue;
            int count = 0;
            foreach (string file in entry.Value)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + file);
                if (clip == null) continue;
                found.Add(new SfxBank.Override { id = entry.Key, clip = clip });
                count++;
            }
            if (count > 0) Report.Add("Audio LPSP: " + entry.Key + " x" + count);
        }
        audio.overrides = found.ToArray();
    }

    internal static void BuildPostProcessing(Camera cam)
    {
        var profile = LookDevSetup.CreatePostProfile();
        var volumeObject = new GameObject("Post Volume");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;
        volume.sharedProfile = profile;

        var data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.TemporalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
    }

    // ---------- player build ----------

    [MenuItem("Zombosland/Build Existing Scene")]
    public static void BuildOnly()
    {
        PlayerSettings.enableFrameTimingStats = true;
        var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Build/Zombosland.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("ZOMBOSLAND_BUILD_RESULT: " + report.summary.result + " errors=" + report.summary.totalErrors);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
