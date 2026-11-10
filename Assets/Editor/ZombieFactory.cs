using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Zombosland;

// Builds the zombie prefab(s): bones, hitboxes, ragdoll joints and AI components.
// Model priority: Assets/Art/Characters/Zombie/zombie.fbx -> Pxltiger Zombie1/2/3 (animated, clips) -> Unity Rio (procedural pose) -> primitive rig.
public static class ZombieFactory
{
    public const string PrefabPath = "Assets/Generated/Prefabs/Zombie.prefab";
    const string FbxPath = "Assets/Art/Characters/Zombie/zombie.fbx";
    public const string RioPath = "Assets/ThirdParty/UnityRio/Models/Rio.fbx";
    const string RioTex = "Assets/ThirdParty/UnityRio/Textures/";
    const string PxlRoot = "Assets/ThirdParty/PxltigerZombie/";
    public static readonly string[] PxlModels = { PxlRoot + "FBXs/Zombie1.FBX", PxlRoot + "FBXs/Zombie2.FBX", PxlRoot + "FBXs/Zombie3.FBX" };
    static readonly string[] PxlPrefabs = { "Assets/Generated/Prefabs/Zombie_A.prefab", "Assets/Generated/Prefabs/Zombie_B.prefab", "Assets/Generated/Prefabs/Zombie_C.prefab" };
    const string DamageClipPath = "Assets/ThirdParty/KevinIglesiasMelee/Animations/HumanM@CombatDamage01.fbx";
    const string ControllerPath = ViewmodelFactory.AnimatorFolder + "/Zombie.controller";

    enum Kind { Generic, Pxl, Rio }

    // Measured clip data (filled by BuildZombieController, copied to ZombieAnimator).
    static float walkSpeed, walk1Speed, runSpeed, attackStrike01;
    static AnimatorController zombieController;

    class Rig
    {
        public Transform hips, spine, chest, neck, head;
        public Transform leftUpperArm, leftLowerArm, leftHand, rightUpperArm, rightLowerArm, rightHand;
        public Transform leftUpperLeg, leftLowerLeg, leftFoot, rightUpperLeg, rightLowerLeg, rightFoot;
    }

    public static bool PxlAvailable { get { return File.Exists(PxlModels[0]); } }

    public static GameObject[] CreatePrefabs(Material coat, Material skin, Material pants, List<string> report)
    {
        if (File.Exists(FbxPath)) return new[] { MakePrefab(PrefabPath, FbxPath, Kind.Generic, coat, skin, pants, report) };
        if (PxlAvailable)
        {
            BuildZombieController(report);
            if (zombieController != null)
            {
                var list = new List<GameObject>();
                for (int i = 0; i < PxlModels.Length; i++)
                    if (File.Exists(PxlModels[i])) list.Add(MakePrefab(PxlPrefabs[i], PxlModels[i], Kind.Pxl, coat, skin, pants, report));
                if (list.Count > 0) return list.ToArray();
            }
        }
        if (File.Exists(RioPath)) return new[] { MakePrefab(PrefabPath, RioPath, Kind.Rio, coat, skin, pants, report) };
        return new[] { MakePrefab(PrefabPath, null, Kind.Generic, coat, skin, pants, report) };
    }

    static GameObject MakePrefab(string prefabPath, string fbx, Kind kind, Material coat, Material skin, Material pants, List<string> report)
    {
        var root = new GameObject("Zombie");
        root.layer = Layers.ZombieMover;

        Rig rig = fbx != null ? TryBuildFromFbx(root, fbx, kind, report) : null;
        bool clips = rig != null && kind == Kind.Pxl;
        if (rig == null) rig = BuildPrimitive(root, coat, skin, pants);

        // Movement capsule (kinematic) so zombies block the player without any CharacterController.
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.radius = 0.32f; capsule.height = 1.8f; capsule.center = new Vector3(0, 0.9f, 0);
        var rootBody = root.AddComponent<Rigidbody>();
        rootBody.isKinematic = true; rootBody.useGravity = false;

        var brain = root.AddComponent<ZombieBrain>();
        AddPhysics(root.transform, rig, brain);

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.32f; agent.height = 1.8f; agent.speed = 1.1f; agent.angularSpeed = 240f; agent.acceleration = 8f;
        agent.stoppingDistance = 1.2f; agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        agent.avoidancePriority = 50;
        agent.enabled = false; // ZombieBrain.Activate enables it once the runtime NavMesh exists

        root.AddComponent<ZombieRagdoll>();
        var anim = root.AddComponent<ZombieAnimator>();
        anim.hips = rig.hips; anim.spine = rig.spine; anim.chest = rig.chest != rig.spine ? rig.chest : null; anim.head = rig.head;
        anim.leftUpperArm = rig.leftUpperArm; anim.rightUpperArm = rig.rightUpperArm;
        anim.leftLowerArm = rig.leftLowerArm; anim.rightLowerArm = rig.rightLowerArm;
        anim.leftHand = rig.leftHand; anim.rightHand = rig.rightHand;
        anim.leftUpperLeg = rig.leftUpperLeg; anim.rightUpperLeg = rig.rightUpperLeg;
        anim.leftLowerLeg = rig.leftLowerLeg; anim.rightLowerLeg = rig.rightLowerLeg;
        if (clips)
        {
            anim.clipWalkSpeed = walkSpeed; anim.clipWalk1Speed = walk1Speed; anim.clipRunSpeed = runSpeed; anim.attackStrike01 = attackStrike01;
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------- primitive rig ----------

    static Transform Bone(string name, Transform parent, Vector3 localPosition)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    static void Visual(Transform bone, Vector3 center, Vector3 size, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Mesh";
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.transform.SetParent(bone, false);
        cube.transform.localPosition = center;
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Rig BuildPrimitive(GameObject root, Material coat, Material skin, Material pants)
    {
        var r = new Rig();
        Transform t = root.transform;
        r.hips = Bone("Hips", t, new Vector3(0, 0.95f, 0));
        r.spine = Bone("Spine", r.hips, new Vector3(0, 0.13f, 0));
        r.chest = Bone("Chest", r.spine, new Vector3(0, 0.17f, 0));
        r.neck = Bone("Neck", r.chest, new Vector3(0, 0.30f, 0));
        r.head = Bone("Head", r.neck, new Vector3(0, 0.08f, 0));
        Visual(r.hips, Vector3.zero, new Vector3(0.34f, 0.2f, 0.2f), pants);
        Visual(r.spine, new Vector3(0, 0.07f, 0), new Vector3(0.32f, 0.2f, 0.2f), coat);
        Visual(r.chest, new Vector3(0, 0.15f, 0), new Vector3(0.40f, 0.34f, 0.22f), coat);
        Visual(r.neck, new Vector3(0, 0.04f, 0), new Vector3(0.10f, 0.12f, 0.10f), skin);
        Visual(r.head, new Vector3(0, 0.10f, 0), new Vector3(0.20f, 0.25f, 0.22f), skin);

        for (int side = -1; side <= 1; side += 2)
        {
            string s = side < 0 ? "Left" : "Right";
            var ua = Bone(s + "UpperArm", r.chest, new Vector3(0.22f * side, 0.24f, 0));
            var la = Bone(s + "LowerArm", ua, new Vector3(0, -0.30f, 0));
            var hand = Bone(s + "Hand", la, new Vector3(0, -0.27f, 0));
            Visual(ua, new Vector3(0, -0.15f, 0), new Vector3(0.11f, 0.32f, 0.11f), coat);
            Visual(la, new Vector3(0, -0.13f, 0), new Vector3(0.09f, 0.28f, 0.09f), coat);
            Visual(hand, new Vector3(0, -0.05f, 0), new Vector3(0.09f, 0.11f, 0.07f), skin);
            var ul = Bone(s + "UpperLeg", r.hips, new Vector3(0.10f * side, -0.05f, 0));
            var ll = Bone(s + "LowerLeg", ul, new Vector3(0, -0.43f, 0));
            var foot = Bone(s + "Foot", ll, new Vector3(0, -0.43f, 0));
            Visual(ul, new Vector3(0, -0.21f, 0), new Vector3(0.16f, 0.44f, 0.16f), pants);
            Visual(ll, new Vector3(0, -0.21f, 0), new Vector3(0.13f, 0.44f, 0.13f), pants);
            Visual(foot, new Vector3(0, -0.02f, 0.07f), new Vector3(0.12f, 0.07f, 0.27f), pants);
            if (side < 0) { r.leftUpperArm = ua; r.leftLowerArm = la; r.leftHand = hand; r.leftUpperLeg = ul; r.leftLowerLeg = ll; r.leftFoot = foot; }
            else { r.rightUpperArm = ua; r.rightLowerArm = la; r.rightHand = hand; r.rightUpperLeg = ul; r.rightLowerLeg = ll; r.rightFoot = foot; }
        }
        return r;
    }

    // ---------- FBX rig (humanoid) ----------

    static Rig TryBuildFromFbx(GameObject root, string path, Kind kind, List<string> report)
    {
        if (!File.Exists(path)) return null;
        string label = Path.GetFileName(path);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (kind == Kind.Generic && importer != null && importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Avatar avatar = FindAvatar(asset, path);
        if (kind == Kind.Pxl && (avatar == null || !avatar.isValid || !avatar.isHuman) && path != PxlModels[0] && importer != null)
        {
            // Zombie2/3 copy the avatar of Zombie1; repair the link if the import left it invalid.
            var source = FindAvatar(AssetDatabase.LoadAssetAtPath<GameObject>(PxlModels[0]), PxlModels[0]);
            report.Add("NOTE: " + label + " avatar invalid (setup " + importer.avatarSetup + ", source " + (importer.sourceAvatar != null ? importer.sourceAvatar.name : "null") + "); relinking to Zombie1 avatar");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.sourceAvatar = source;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.SaveAndReimport();
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            avatar = FindAvatar(asset, path);
        }
        if (asset == null || avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            report.Add("WARNING: " + label + " exists but its humanoid avatar is invalid (avatar " + (avatar != null ? avatar.name + " valid=" + avatar.isValid + " human=" + avatar.isHuman : "null")
                + ", importer type " + (importer != null ? importer.animationType.ToString() + "/" + importer.avatarSetup : "?") + "); using fallback");
            return null;
        }
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.name = "Model";
        if (PrefabUtility.IsPartOfPrefabInstance(model))
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.SetParent(root.transform, false);
        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = kind == Kind.Pxl ? zombieController : null;
        animator.applyRootMotion = false;
        if (kind == Kind.Pxl)
        {
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.updateMode = AnimatorUpdateMode.Normal;
        }

        var r = new Rig
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips), spine = animator.GetBoneTransform(HumanBodyBones.Spine),
            chest = animator.GetBoneTransform(HumanBodyBones.Chest), neck = animator.GetBoneTransform(HumanBodyBones.Neck),
            head = animator.GetBoneTransform(HumanBodyBones.Head),
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand),
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm), rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm), rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand),
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot),
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot)
        };
        if (r.chest == null) r.chest = r.spine;
        if (r.neck == null) r.neck = r.head;
        foreach (var bone in new[] { r.hips, r.spine, r.head, r.leftUpperArm, r.leftLowerArm, r.leftHand, r.rightUpperArm, r.rightLowerArm, r.rightHand, r.leftUpperLeg, r.leftLowerLeg, r.leftFoot, r.rightUpperLeg, r.rightLowerLeg, r.rightFoot })
            if (bone == null)
            {
                report.Add("WARNING: " + label + " is missing a required humanoid bone; using fallback");
                Object.DestroyImmediate(model);
                return null;
            }

        // Scale only when the humanoid height (head bone + 0.12) is outside 1.6-2.0 m, then stand the soles on y = 0.
        Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes), rt = animator.GetBoneTransform(HumanBodyBones.RightToes);
        float ground = GroundHeight(r, lt, rt);
        float height = r.head.position.y + 0.12f - ground;
        if (height < 1.6f || height > 2.0f)
        {
            model.transform.localScale *= 1.8f / height;
            ground = GroundHeight(r, lt, rt);
        }
        model.transform.position += Vector3.up * (root.transform.position.y - ground);
        report.Add(label + ": humanoid height " + height.ToString("F2") + " m, model scale " + model.transform.localScale.x.ToString("F2") + ", ground offset " + (-ground).ToString("F3"));

        Material material = null;
        if (kind == Kind.Pxl) material = ZombiePxlMaterial();
        else if (kind == Kind.Rio) material = ZombieRioMaterial();
        if (material != null)
        {
            RemapAll(path, material);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new Material[Mathf.Max(1, smr.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                smr.sharedMaterials = mats;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
        report.Add(kind == Kind.Rio ? "Zombie built from Rio.fbx (tinted)" : "Zombie built from " + path + (kind == Kind.Pxl ? " (animated, tinted)" : ""));
        return r;
    }

    static Avatar FindAvatar(GameObject asset, string path)
    {
        Avatar avatar = null;
        if (asset != null)
        {
            var assetAnimator = asset.GetComponent<Animator>();
            if (assetAnimator != null) avatar = assetAnimator.avatar;
        }
        if (avatar == null) avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (avatar == null && mi != null && mi.avatarSetup == ModelImporterAvatarSetup.CopyFromOther) avatar = mi.sourceAvatar; // Zombie2/3 use Zombie1's avatar
        return avatar;
    }

    static float GroundHeight(Rig r, Transform leftToes, Transform rightToes)
    {
        float g = Mathf.Min(r.leftFoot.position.y - 0.09f, r.rightFoot.position.y - 0.09f);
        if (leftToes != null) g = Mathf.Min(g, leftToes.position.y);
        if (rightToes != null) g = Mathf.Min(g, rightToes.position.y);
        return g;
    }

    // ---------- materials ----------

    static Material LitAsset(string name)
    {
        string path = LookDevSetup.MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        return m;
    }

    static Texture2D Tex(string path, bool normal, bool linear)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool change = false;
            if (normal && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; change = true; }
            if (linear && importer.sRGBTexture) { importer.sRGBTexture = false; change = true; }
            if (change) importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material ZombieRioMaterial()
    {
        var m = LitAsset("Zombie_Rio");
        m.SetTexture("_BaseMap", Tex(RioTex + "Rio_AlbedoTransparency.png", false, false));
        m.SetColor("_BaseColor", new Color(0.66f, 0.74f, 0.60f));
        m.SetTexture("_MetallicGlossMap", Tex(RioTex + "Rio_MetallicSmoothness.png", false, true));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_SmoothnessTextureChannel", 0f);
        m.SetFloat("_Smoothness", 0.35f);
        m.SetTexture("_BumpMap", Tex(RioTex + "Riol_Normal.png", true, false));
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material ZombiePxlMaterial()
    {
        string t = PxlRoot + "Textures/";
        var m = LitAsset("Zombie_Pxl");
        m.SetTexture("_BaseMap", Tex(t + "Zombie.tga", false, false));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_MetallicGlossMap", Tex(t + "Zombie_metallic.tga", false, true));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_SmoothnessTextureChannel", 0f);
        m.SetFloat("_Smoothness", 0.4f);
        m.SetTexture("_BumpMap", Tex(t + "Zombie_nm.png", true, false));
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_OcclusionMap", Tex(t + "Zombie_ao.png", false, true));
        m.SetFloat("_OcclusionStrength", 1f);
        m.SetTexture("_EmissionMap", Tex(t + "zombie_emission.tga", false, false));
        m.SetColor("_EmissionColor", Color.white * 0.6f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Binds every embedded material slot of the model to `material` through the importer (reimports only on change).
    static void RemapAll(string fbx, Material material)
    {
        var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (importer == null) return;
        var existing = importer.GetExternalObjectMap();
        bool changed = false;
        foreach (string name in AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().Select(x => x.name).Distinct().ToArray())
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
            if (existing.ContainsKey(id)) continue;
            importer.AddRemap(id, material);
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    // ---------- animator controller (Pxltiger clips + Kevin Iglesias hit reaction) ----------

    static AnimationClip FirstClip(string fbx)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
    }

    static string Z(string name) { return PxlRoot + "Animations/Zombie@" + name + ".FBX"; }

    // Makes sure the default clip of a model loops (import setting on the third-party .meta; reported).
    static AnimationClip LoopingClip(string fbx, List<string> report)
    {
        AnimationClip clip = FirstClip(fbx);
        if (clip == null || clip.isLooping) return clip;
        var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (importer == null) return clip;
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (var c in clips) c.loopTime = true;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        report.Add("Enabled loopTime on " + Path.GetFileName(fbx) + " import settings");
        return FirstClip(fbx);
    }

    static void BuildZombieController(List<string> report)
    {
        AnimationClip walk = FirstClip(Z("Z_Walk")), walk1 = FirstClip(Z("Z_Walk1")), run = FirstClip(Z("Z_Run"));
        AnimationClip idle = LoopingClip(Z("Z_Idle"), report);
        AnimationClip walkIp = LoopingClip(Z("Z_Walk_InPlace"), report), walk1Ip = LoopingClip(Z("Z_Walk1_InPlace"), report), runIp = LoopingClip(Z("Z_Run_InPlace"), report);
        AnimationClip attack = FirstClip(Z("Z_Attack")), hit = FirstClip(DamageClipPath);
        walkSpeed = walk != null ? walk.averageSpeed.magnitude : 0f;
        walk1Speed = walk1 != null ? walk1.averageSpeed.magnitude : 0f;
        runSpeed = run != null ? run.averageSpeed.magnitude : 0f;
        float rootWalk = walkSpeed, rootWalk1 = walk1Speed, rootRun = runSpeed;
        float footWalk = FootSpeed(walkIp), footWalk1 = FootSpeed(walk1Ip), footRun = FootSpeed(runIp);
        string measured = "root-motion averageSpeed: walk=" + rootWalk.ToString("F2") + ", walk1=" + rootWalk1.ToString("F2") + ", run=" + rootRun.ToString("F2")
            + " m/s; stance-foot speed of the in-place clips: walk=" + footWalk.ToString("F2") + ", walk1=" + footWalk1.ToString("F2") + ", run=" + footRun.ToString("F2") + " m/s";
        // Root-motion speed is the spec's measure; if it is implausibly low next to the stance-foot speed, trust the feet.
        if (footWalk > 0.1f && walkSpeed < 0.5f * footWalk) walkSpeed = footWalk;
        if (footWalk1 > 0.1f && walk1Speed < 0.5f * footWalk1) walk1Speed = footWalk1;
        if (footRun > 0.1f && runSpeed < 0.5f * footRun) runSpeed = footRun;
        if (walkSpeed < 0.1f) walkSpeed = 1.2f;
        if (walk1Speed < 0.1f) walk1Speed = walkSpeed;
        if (runSpeed < walkSpeed + 0.5f) runSpeed = walkSpeed * 2.5f;
        float strikeSeconds;
        attackStrike01 = MeasureStrike(attack, out strikeSeconds);
        float attackSpeed = attack != null && strikeSeconds > 0.05f ? strikeSeconds / ZombieBrain.AttackWindupSeconds : 1f;
        report.Add("ZOMBIE_CLIPS measured: " + measured + " (used: " + walkSpeed.ToString("F2") + "/" + walk1Speed.ToString("F2") + "/" + runSpeed.ToString("F2")
            + "), attack length=" + (attack != null ? attack.length.ToString("F2") : "?") + " s, strike at " + strikeSeconds.ToString("F2") + " s (normalized " + attackStrike01.ToString("F2")
            + "), attack state speed=" + attackSpeed.ToString("F2") + ", hit clip length=" + (hit != null ? hit.length.ToString("F2") : "?") + " s");
        if (idle == null || walkIp == null || walk1Ip == null || runIp == null || attack == null || hit == null)
        {
            report.Add("WARNING: zombie clips missing; Pxltiger zombie controller not built");
            zombieController = null;
            return;
        }

        if (!AssetDatabase.IsValidFolder(ViewmodelFactory.AnimatorFolder)) AssetDatabase.CreateFolder("Assets/Generated", "Animators");
        AssetDatabase.DeleteAsset(ControllerPath);
        var c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("AnimRate", AnimatorControllerParameterType.Float);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("HeavyHit", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Variant", AnimatorControllerParameterType.Float);
        var ps = c.parameters;
        for (int i = 0; i < ps.Length; i++) if (ps[i].name == "AnimRate") ps[i].defaultFloat = 1f;
        c.parameters = ps;

        var sm = c.layers[0].stateMachine;
        BlendTree loco;
        AnimatorState locomotion = c.CreateBlendTreeInController("Locomotion", out loco);
        loco.blendType = BlendTreeType.Simple1D;
        loco.blendParameter = "Speed";
        loco.useAutomaticThresholds = false;
        loco.AddChild(idle, 0f);
        BlendTree walkTree = loco.CreateBlendTreeChild(walkSpeed);
        walkTree.name = "Walk";
        walkTree.blendType = BlendTreeType.Simple1D;
        walkTree.blendParameter = "Variant";
        walkTree.useAutomaticThresholds = false;
        walkTree.AddChild(walkIp, 0f);
        walkTree.AddChild(walk1Ip, 1f);
        loco.AddChild(runIp, runSpeed);
        locomotion.speedParameterActive = true;
        locomotion.speedParameter = "AnimRate";
        sm.defaultState = locomotion;

        AnimatorState attackState = sm.AddState("Attack");
        attackState.motion = attack;
        attackState.speed = attackSpeed;
        AnimatorState heavy = sm.AddState("HeavyHit");
        heavy.motion = hit;

        AnimatorStateTransition ta = sm.AddAnyStateTransition(attackState);
        ta.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        ta.hasExitTime = false; ta.hasFixedDuration = true; ta.duration = 0.1f; ta.canTransitionToSelf = false;
        AnimatorStateTransition th = sm.AddAnyStateTransition(heavy);
        th.AddCondition(AnimatorConditionMode.If, 0f, "HeavyHit");
        th.hasExitTime = false; th.hasFixedDuration = true; th.duration = 0.08f; th.canTransitionToSelf = false;
        AnimatorStateTransition ea = attackState.AddTransition(locomotion);
        ea.hasExitTime = true; ea.exitTime = 0.95f; ea.hasFixedDuration = true; ea.duration = 0.15f;
        AnimatorStateTransition eh = heavy.AddTransition(locomotion);
        eh.hasExitTime = true; eh.exitTime = 0.85f; eh.hasFixedDuration = true; eh.duration = 0.15f;
        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        zombieController = c;
    }

    // Mean backward speed (m/s) of the grounded foot relative to the hips over one cycle of an in-place clip.
    static float FootSpeed(AnimationClip clip)
    {
        if (clip == null) return 0f;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PxlModels[0]);
        if (asset == null) return 0f;
        var go = (GameObject)Object.Instantiate(asset);
        var animator = go.GetComponent<Animator>();
        float total = 0f; int count = 0;
        if (animator != null && animator.isHuman)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot), rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            int steps = Mathf.Max(30, Mathf.CeilToInt(clip.length * 60f));
            float dt = clip.length / steps;
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                clip.SampleAnimation(go, dt * i);
                Quaternion inv = Quaternion.Inverse(go.transform.rotation);
                Vector3 l = inv * (lf.position - hips.position), r = inv * (rf.position - hips.position);
                if (i > 0)
                {
                    // The lower foot is the planted one; its backward motion relative to the hips is the ground speed.
                    bool left = lf.position.y < rf.position.y;
                    float dz = left ? l.z - prevL.z : r.z - prevR.z;
                    total += Mathf.Max(0f, -dz / dt);
                    count++;
                }
                prevL = l; prevR = r;
            }
        }
        Object.DestroyImmediate(go);
        return count > 0 ? total / count : 0f;
    }

    // Samples the attack clip on a temporary Zombie1 and returns the normalized time at which a hand is farthest in front of the hips.
    static float MeasureStrike(AnimationClip clip, out float seconds)
    {
        seconds = 0f;
        if (clip == null) return 0f;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PxlModels[0]);
        if (asset == null) return 0f;
        var go = (GameObject)Object.Instantiate(asset);
        var animator = go.GetComponent<Animator>();
        float best = float.MinValue, bestT = 0f;
        if (animator != null && animator.isHuman)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform lh = animator.GetBoneTransform(HumanBodyBones.LeftHand), rh = animator.GetBoneTransform(HumanBodyBones.RightHand);
            int steps = Mathf.Max(20, Mathf.CeilToInt(clip.length * 60f));
            for (int i = 0; i <= steps; i++)
            {
                float t = clip.length * i / steps;
                clip.SampleAnimation(go, t);
                Vector3 forward = go.transform.forward;
                float f = Mathf.Max(Vector3.Dot(lh.position - hips.position, forward), Vector3.Dot(rh.position - hips.position, forward));
                if (f > best) { best = f; bestT = t; }
            }
        }
        Object.DestroyImmediate(go);
        seconds = bestT;
        return clip.length > 0f ? bestT / clip.length : 0f;
    }

    // ---------- hitboxes + ragdoll ----------

    static void AddPhysics(Transform root, Rig r, ZombieBrain brain)
    {
        Rigidbody hips = Part(r.hips, r.spine, 0.15f, HitZone.Body, 12f, null, 15f, brain);
        Rigidbody spine = r.spine != r.chest ? Part(r.spine, r.chest, 0.14f, HitZone.Body, 8f, hips, 15f, brain) : hips;
        Rigidbody chest = Part(r.chest, r.neck, 0.17f, HitZone.Body, 14f, spine, 15f, brain);
        Part(r.head, null, 0.13f, HitZone.Head, 5f, chest, 40f, brain);
        AddLimb(r.leftUpperArm, r.leftLowerArm, r.leftHand, chest, hips, 0.06f, 0.05f, 2.5f, 1.5f, 70f, 40f, brain, HumanLimb.LeftArm);
        AddLimb(r.rightUpperArm, r.rightLowerArm, r.rightHand, chest, hips, 0.06f, 0.05f, 2.5f, 1.5f, 70f, 40f, brain, HumanLimb.RightArm);
        AddLimb(r.leftUpperLeg, r.leftLowerLeg, r.leftFoot, hips, hips, 0.09f, 0.07f, 6f, 3f, 55f, 12f, brain, HumanLimb.LeftLeg);
        AddLimb(r.rightUpperLeg, r.rightLowerLeg, r.rightFoot, hips, hips, 0.09f, 0.07f, 6f, 3f, 55f, 12f, brain, HumanLimb.RightLeg);
    }

    static void AddLimb(Transform upper, Transform lower, Transform end, Rigidbody parent, Rigidbody fallbackParent,
                        float upperRadius, float lowerRadius, float upperMass, float lowerMass, float upperSwing, float lowerSwing,
                        ZombieBrain brain, HumanLimb limb)
    {
        Rigidbody u = Part(upper, lower, upperRadius, HitZone.Limb, upperMass, parent != null ? parent : fallbackParent, upperSwing, brain);
        Rigidbody l = Part(lower, end, lowerRadius, HitZone.Limb, lowerMass, u, lowerSwing, brain);
        u.GetComponent<ZombiePart>().limb = limb;
        l.GetComponent<ZombiePart>().limb = limb;
    }

    // Capsule (or sphere for the head) + kinematic rigidbody + joint to the parent body + ZombiePart on a bone.
    static Rigidbody Part(Transform bone, Transform tip, float radiusMeters, HitZone zone, float mass, Rigidbody parentBody, float swing, ZombieBrain brain)
    {
        bone.gameObject.layer = Layers.ZombieHitbox;
        float scale = Mathf.Max(0.0001f, bone.lossyScale.x);
        float radius = radiusMeters / scale;
        Vector3 axis;
        if (tip != null)
        {
            Vector3 tipLocal = bone.InverseTransformPoint(tip.position);
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = DominantAxis(tipLocal);
            capsule.radius = radius;
            capsule.height = Mathf.Max(2f * radius, tipLocal.magnitude + radius);
            capsule.center = tipLocal * 0.5f;
            axis = tipLocal.normalized;
        }
        else
        {
            axis = bone.InverseTransformDirection(Vector3.up).normalized; // head: sphere above the bone origin
            var sphere = bone.gameObject.AddComponent<SphereCollider>();
            sphere.radius = radius;
            sphere.center = axis * (0.10f / scale);
        }

        var body = bone.gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.isKinematic = true;
        body.linearDamping = 0.3f;
        body.angularDamping = 1.5f;

        var part = bone.gameObject.AddComponent<ZombiePart>();
        part.owner = brain;
        part.zone = zone;

        if (parentBody != null)
        {
            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parentBody;
            joint.axis = axis;
            Vector3 swingAxis = Vector3.Cross(axis, Vector3.forward);
            if (swingAxis.sqrMagnitude < 0.01f) swingAxis = Vector3.Cross(axis, Vector3.right);
            joint.swingAxis = swingAxis.normalized;
            joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
            joint.highTwistLimit = new SoftJointLimit { limit = 20f };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = swing };
            joint.enableProjection = true;
            joint.enablePreprocessing = false;
        }
        return body;
    }

    static int DominantAxis(Vector3 v)
    {
        Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        return a.x > a.y && a.x > a.z ? 0 : (a.y >= a.z ? 1 : 2);
    }
}
