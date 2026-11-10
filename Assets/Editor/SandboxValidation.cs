using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Zombosland;

/// <summary>Integration checks for the generated combat sandbox scene.</summary>
public static class SandboxValidation
{
    private const string ScenePath = "Assets/Scenes/CombatSandbox.unity";

    public static void Run()
    {
        var results = new List<string>();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();

        Check(results, "scene_no_missing_scripts", () =>
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var go in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject) == 0,
                        "Missing script on " + HierarchyPath(go));
        });

        Sandbox player = null;
        Camera camera = null;
        ZombieBrain[] zombies = Array.Empty<ZombieBrain>();
        Check(results, "player_camera_and_zombie_references", () =>
        {
            player = UnityEngine.Object.FindAnyObjectByType<Sandbox>();
            Require(player != null, "Sandbox player component not found");
            camera = player.GetComponentInChildren<Camera>(true);
            Require(camera != null && camera.CompareTag("MainCamera"), "Player camera missing or not tagged MainCamera");
            Require(player.GetComponent<CharacterController>() != null, "Player CharacterController missing");
            zombies = UnityEngine.Object.FindObjectsByType<ZombieBrain>();
            Require(zombies.Length > 0, "No zombies found in generated scene");
            foreach (var zombie in zombies)
            {
                Require(zombie.player == player, HierarchyPath(zombie.transform) + " does not reference the scene player");
                Require(zombie.GetComponent<CharacterController>() == null, HierarchyPath(zombie.transform) + " still has a CharacterController");
                Require(zombie.GetComponent<NavMeshAgent>() != null, HierarchyPath(zombie.transform) + " lacks NavMeshAgent");
                var mover = zombie.GetComponent<CapsuleCollider>();
                Require(mover != null && zombie.gameObject.layer == Layers.ZombieMover, HierarchyPath(zombie.transform) + " lacks ZombieMover capsule");
                var body = zombie.GetComponent<Rigidbody>();
                Require(body != null && body.isKinematic, HierarchyPath(zombie.transform) + " mover body must be kinematic");
            }
        });

        Check(results, "start_reserve_serialized_per_scene", () =>
        {
            Require(player != null, "Cannot run: no player");
            var so = new SerializedObject(player);
            Require(so.FindProperty("startReserve").intValue == 72, "CombatSandbox startReserve must be 72");
            Require(Mathf.Approximately(so.FindProperty("noiseRadius").floatValue, 30f), "CombatSandbox noiseRadius must be 30");
            Require(player.Weapon.Reserve == 72, "CombatSandbox weapon reserve must start at 72");
            foreach (int start in new[] { 36, 72 })
            {
                var temp = new GameObject("Reserve test").AddComponent<Sandbox>();
                var tempSo = new SerializedObject(temp);
                tempSo.FindProperty("startReserve").intValue = start;
                tempSo.ApplyModifiedPropertiesWithoutUndo();
                Require(temp.Weapon.Reserve == start && temp.Weapon.Ammo == 12, "Sandbox startReserve " + start + " not applied");
                temp.Weapon.TryFire(0f);
                Require(temp.Weapon.BeginReload(1f), "reload must begin with one round missing");
                temp.Weapon.Tick(1f + WeaponState.TacticalReloadSeconds);
                Require(temp.Weapon.Ammo == 12 && temp.Weapon.Reserve == start - 1, "reserve after tactical reload with start " + start + " expected " + (start - 1) + " got " + temp.Weapon.Reserve);
                UnityEngine.Object.DestroyImmediate(temp.gameObject);
            }
        });

        Check(results, "physics_head_and_body_hitboxes_ignore_controller", () =>
        {
            Require(zombies.Length > 0, "Cannot run: no validated zombies");
            var zombie = zombies[0];
            ZombiePart head = null, body = null;
            foreach (var part in zombie.GetComponentsInChildren<ZombiePart>(true))
            {
                if (part.zone == HitZone.Head) head = part;
                if (part.zone == HitZone.Body && part.transform == (zombie.GetComponent<ZombieAnimator>().chest ?? zombie.GetComponent<ZombieAnimator>().spine)) body = part;
            }
            Require(head != null && body != null, "Zombie Head/Chest ZombiePart missing");
            Require(head.owner == zombie && body.owner == zombie, "Hitboxes do not reference owning ZombieBrain");
            Require(Mathf.Approximately(DamageRules.Multiplier(head.zone), 3f) && Mathf.Approximately(DamageRules.Multiplier(body.zone), 1f) && Mathf.Approximately(DamageRules.Multiplier(HitZone.Limb), 0.7f), "Unexpected head/body/limb damage multipliers (expected 3 / 1 / 0.7)");

            int weaponMask = GetWeaponMask();
            AssertRayHitsPart(head, weaponMask);
            AssertRayHitsPart(body, weaponMask);
        });

        Check(results, "sandbox_obstruction_is_collidable_by_weapon_mask", () =>
        {
            var obstruction = GameObject.Find("Near obstruction");
            Require(obstruction != null, "Generated Near obstruction missing");
            Require(Vector3.Distance(obstruction.transform.position, new Vector3(4, 1, 3)) < 0.001f, "Near obstruction moved from (4,1,3)");
            var collider = obstruction.GetComponent<BoxCollider>();
            Require(collider != null && collider.enabled, "Near obstruction has no enabled collider");
            Require(Vector3.Distance(collider.size, new Vector3(2, 2, 1)) < 0.001f, "Near obstruction collider size is not (2,2,1)");
            int weaponMask = GetWeaponMask();
            var origin = new Vector3(4f, 1f, -2f);
            var destination = new Vector3(4f, 1f, 7f);
            RaycastHit hit;
            Require(Physics.Linecast(origin, destination, out hit, weaponMask, QueryTriggerInteraction.Ignore), "Weapon mask ray did not encounter obstruction");
            Require(hit.collider == collider, "Expected Near obstruction first, got " + hit.collider.name);
        });

        Check(results, "aim_pivot_hierarchy_player_aimpivot_camera_viewmodel", () =>
        {
            Require(player != null, "Cannot run: no player");
            var pivot = player.transform.Find("AimPivot");
            Require(pivot != null, "AimPivot missing under Player");
            Require(camera != null && camera.transform.parent == pivot, "Camera is not a child of AimPivot");
            var viewmodel = camera.transform.Find("Viewmodel");
            Require(viewmodel != null && viewmodel.GetComponent<ViewmodelPresenter>() != null, "Viewmodel (with ViewmodelPresenter) missing under Camera");
            Require(camera.GetComponent<CameraEffects>() != null, "CameraEffects missing on Camera");
            Require(player.GetComponent<PlayerMotor>() != null, "PlayerMotor missing");
            Require(Vector3.Distance(player.transform.position, new Vector3(0, 0.05f, -4)) < 0.001f, "Player spawn moved from (0,0.05,-4)");
        });

        Check(results, "zombie_director_present_and_navmesh_builds", () =>
        {
            var director = UnityEngine.Object.FindAnyObjectByType<ZombieDirector>();
            Require(director != null, "ZombieDirector missing from scene");
            Require(director.player == player && director.zombiePrefab != null, "ZombieDirector player/prefab not assigned");
            try
            {
                Require(director.BuildNavMesh(), "ZombieDirector.BuildNavMesh() failed in edit mode");
                NavMeshHit hit;
                Require(NavMesh.SamplePosition(new Vector3(0, 0, 8), out hit, 1.5f, NavMesh.AllAreas), "Built NavMesh has no point near the yard centre");
                Require(!NavMesh.SamplePosition(new Vector3(4, 1.0f, 3), out hit, 0.2f, NavMesh.AllAreas) || hit.position.y < 0.5f, "NavMesh covers the top of the obstruction");
            }
            finally { director.ReleaseNavMesh(); }
        });

        Check(results, "every_zombie_ragdoll_starts_kinematic", () =>
        {
            foreach (var zombie in zombies)
            {
                var ragdoll = zombie.GetComponent<ZombieRagdoll>();
                Require(ragdoll != null, HierarchyPath(zombie.transform) + " lacks ZombieRagdoll");
                Require(ragdoll.Bodies.Length >= 10, HierarchyPath(zombie.transform) + " has only " + ragdoll.Bodies.Length + " ragdoll bodies");
                Require(ragdoll.AllKinematic, HierarchyPath(zombie.transform) + " has non-kinematic ragdoll bodies at start");
                Require(zombie.GetComponentsInChildren<CharacterJoint>(true).Length >= 9, HierarchyPath(zombie.transform) + " has too few joints");
                foreach (var body in ragdoll.Bodies)
                {
                    Require(body.GetComponent<ZombiePart>() != null, body.name + " has a rigidbody but no ZombiePart");
                    Require(body.GetComponent<Collider>() != null, body.name + " has no collider");
                    Require(body.gameObject.layer == Layers.ZombieHitbox, body.name + " is not on the ZombieHitbox layer");
                }
                var animator = zombie.GetComponent<ZombieAnimator>();
                Require(animator != null && animator.hips != null && animator.head != null && animator.leftUpperArm != null && animator.rightUpperLeg != null, HierarchyPath(zombie.transform) + " ZombieAnimator bones not assigned");
            }
        });

        Check(results, "global_volume_has_post_profile", () =>
        {
            Volume global = null;
            foreach (var v in UnityEngine.Object.FindObjectsByType<Volume>()) if (v.isGlobal && v.sharedProfile != null) global = v;
            Require(global != null, "No global Volume with a profile");
            VolumeProfile profile = global.sharedProfile;
            Tonemapping tone; Bloom bloom; Vignette vignette; MotionBlur blur;
            Require(profile.TryGet(out tone) && tone.mode.value == TonemappingMode.ACES, "Tonemapping ACES missing");
            Require(profile.TryGet(out bloom) && bloom.intensity.value > 0f, "Bloom missing");
            Require(profile.TryGet(out vignette) && vignette.intensity.value > 0f, "Vignette missing");
            Require(!profile.TryGet(out blur) || !blur.active || blur.intensity.value == 0f, "Motion blur must be off");
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            Require(data != null && data.renderPostProcessing, "Camera post processing is off");
            Require(data.antialiasing == AntialiasingMode.TemporalAntiAliasing || data.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing, "Camera antialiasing is not TAA/SMAA");
        });

        Check(results, "polyhaven_materials_contain_textures", () =>
        {
            bool texturesPresent = File.Exists("Assets/ThirdParty/PolyHaven/asphalt_02/asphalt_02_albedo.jpg");
            var ground = GameObject.Find("Ground");
            Require(ground != null, "Ground missing");
            var material = ground.GetComponent<Renderer>().sharedMaterial;
            Require(material != null, "Ground has no material");
            if (!texturesPresent) return; // flat-colour fallback is the expected result without texture files
            Require(material.GetTexture("_BaseMap") != null, "Ground material has no _BaseMap");
            Require(material.GetTexture("_BumpMap") != null, "Ground material has no _BumpMap");
            Require(material.GetTexture("_MetallicGlossMap") != null, "Ground material has no _MetallicGlossMap");
            Require(material.GetTexture("_OcclusionMap") != null, "Ground material has no _OcclusionMap");
            foreach (var id in new[] { "concrete_floor_worn_001", "concrete_wall_008", "red_brick_03", "painted_concrete", "rusty_metal_02", "metal_plate" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Materials/PBR_" + id + ".mat");
                Require(m != null && m.GetTexture("_BaseMap") != null, "PBR material for " + id + " missing or untextured");
            }
            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh != null && mesh.tangents.Length == mesh.vertexCount, "Ground mesh has no tangents");
            float maxU = 0f;
            foreach (var uv in mesh.uv) maxU = Mathf.Max(maxU, uv.x);
            Require(Mathf.Abs(maxU - 45f / 3f) < 0.01f, "Ground UVs are not world scaled (max U " + maxU + ", expected 15)");
        });

        Check(results, "effects_audio_and_sky_present", () =>
        {
            var fx = UnityEngine.Object.FindAnyObjectByType<EffectPool>();
            Require(fx != null && fx.flashMaterial != null && fx.dustMaterial != null && fx.sparkMaterial != null && fx.bloodMaterial != null && fx.decalMaterial != null, "EffectPool or its materials missing");
            Require(UnityEngine.Object.FindAnyObjectByType<SfxBank>() != null, "SfxBank missing");
            Require(RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox, "Ambient mode is not Skybox");
            Require(RenderSettings.skybox != null || !File.Exists("Assets/ThirdParty/PolyHaven/HDRI/kloofendal_overcast_puresky_2k.hdr"), "Skybox material not assigned");
            Require(UnityEngine.Object.FindAnyObjectByType<ReflectionProbe>() != null, "ReflectionProbe missing");
        });

        Check(results, "viewmodel_arms_pistol_animators_and_materials", () =>
        {
            var viewmodel = camera.transform.Find("Viewmodel");
            var presenter = viewmodel.GetComponent<ViewmodelPresenter>();
            if (!ViewmodelFactory.AssetsPresent()) return; // primitive pistol fallback is the expected result without the LPSP files
            Require(presenter.animated, "LPSP files exist but the viewmodel is not the animated arms path");
            Require(presenter.armsAnimator != null && presenter.armsAnimator.runtimeAnimatorController != null, "Arms Animator or controller missing");
            Require(presenter.pistolAnimator != null && presenter.pistolAnimator.runtimeAnimatorController != null, "Pistol Animator or controller missing");
            Require(presenter.muzzle != null && presenter.muzzle.parent != null && presenter.muzzle.parent.name == "SOCKET_Muzzle", "SOCKET_Muzzle missing");
            Require(presenter.eject != null && presenter.eject.parent != null && presenter.eject.parent.name == "SOCKET_Eject", "SOCKET_Eject missing");
            Require(presenter.armsAnimator.GetComponent<ViewmodelAnimationEvents>() != null && presenter.pistolAnimator.GetComponent<ViewmodelAnimationEvents>() != null, "ViewmodelAnimationEvents missing");
            int renderers = 0;
            foreach (var r in viewmodel.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                foreach (var m in r.sharedMaterials) Require(m != null, "Null material on viewmodel renderer " + HierarchyPath(r.transform));
                Require(r.shadowCastingMode == ShadowCastingMode.Off, "Viewmodel renderer casts shadows: " + HierarchyPath(r.transform));
            }
            Require(renderers >= 3, "Expected arms, pistol and magazine renderers, found " + renderers);
        });

        Check(results, "viewmodel_survivalist_arms_set", () =>
        {
            var presenter = camera.transform.Find("Viewmodel").GetComponent<ViewmodelPresenter>();
            if (!ViewmodelFactory.AssetsPresent() || presenter.survivalist == null || presenter.survivalist.root == null) return; // optional trial set
            var set = presenter.survivalist;
            Require(!set.root.activeSelf, "Survivalist arms must be built inactive (ViewmodelPresenter.Awake activates them as the default)");
            Require(presenter.defaultArmsRoot != null && presenter.defaultArmsRoot.activeSelf, "LPSP fallback arms root missing or inactive in the built scene");
            Require(set.armsAnimator != null && set.armsAnimator.runtimeAnimatorController != null && set.pistolAnimator != null && set.pistolAnimator.runtimeAnimatorController != null, "Survivalist animators/controllers missing");
            Require(set.muzzle != null && set.eject != null, "Survivalist muzzle/eject anchors missing");
            Require(set.root.transform.Find("Armature/root/ik_hand_root/ik_hand_gun") != null, "Survivalist ik_hand_gun path missing");
            int renderers = 0;
            foreach (var r in set.root.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                foreach (var m in r.sharedMaterials) Require(m != null, "Null material on Survivalist renderer " + HierarchyPath(r.transform));
                Require(r.shadowCastingMode == ShadowCastingMode.Off, "Survivalist renderer casts shadows: " + HierarchyPath(r.transform));
            }
            Require(renderers >= 4, "Expected Survivalist arms, shirt, pistol and magazine renderers, found " + renderers);
            Require(set.root.GetComponentsInChildren<Collider>(true).Length == 0, "Survivalist arms have colliders");
        });

        Check(results, "zombie_prefabs_model_animator_material_and_limbs", () =>
        {
            var director = UnityEngine.Object.FindAnyObjectByType<ZombieDirector>();
            var prefabs = director.zombiePrefabs != null && director.zombiePrefabs.Length > 0 ? director.zombiePrefabs : new[] { director.zombiePrefab };
            bool custom = File.Exists("Assets/Art/Characters/Zombie/zombie.fbx");
            bool pxl = !custom && File.Exists(ZombieFactory.PxlModels[0]);
            bool rio = !custom && !pxl && File.Exists(ZombieFactory.RioPath);
            if (pxl) Require(prefabs.Length == 3, "Expected 3 Pxltiger zombie prefabs, found " + prefabs.Length);
            foreach (var prefab in prefabs)
            {
                Require(prefab != null, "Null zombie prefab");
                var smrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (pxl || rio)
                {
                    Require(smrs.Length >= 1, prefab.name + " has no SkinnedMeshRenderer");
                    foreach (var smr in smrs)
                        foreach (var m in smr.sharedMaterials)
                            Require(m != null && m.name == (pxl ? "Zombie_Pxl" : "Zombie_Rio"), prefab.name + " material is " + (m != null ? m.name : "null"));
                }
                if (pxl)
                {
                    var an = prefab.GetComponentInChildren<Animator>(true);
                    Require(an != null && an.runtimeAnimatorController != null, prefab.name + " Animator controller missing");
                    Require(an.avatar != null && an.avatar.isValid && an.avatar.isHuman, prefab.name + " avatar is not a valid humanoid");
                    Require(!an.applyRootMotion, prefab.name + " applies root motion");
                }
                var za = prefab.GetComponent<ZombieAnimator>();
                Require(za != null, prefab.name + " lacks ZombieAnimator");
                Require(za.leftUpperArm.GetComponent<ZombiePart>().limb == HumanLimb.LeftArm && za.leftLowerArm.GetComponent<ZombiePart>().limb == HumanLimb.LeftArm, prefab.name + " left arm limb tag");
                Require(za.rightUpperArm.GetComponent<ZombiePart>().limb == HumanLimb.RightArm && za.rightLowerArm.GetComponent<ZombiePart>().limb == HumanLimb.RightArm, prefab.name + " right arm limb tag");
                Require(za.leftUpperLeg.GetComponent<ZombiePart>().limb == HumanLimb.LeftLeg && za.leftLowerLeg.GetComponent<ZombiePart>().limb == HumanLimb.LeftLeg, prefab.name + " left leg limb tag");
                Require(za.rightUpperLeg.GetComponent<ZombiePart>().limb == HumanLimb.RightLeg && za.rightLowerLeg.GetComponent<ZombiePart>().limb == HumanLimb.RightLeg, prefab.name + " right leg limb tag");
            }
        });

        string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/SandboxValidation.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, string.Join(Environment.NewLine, results) + Environment.NewLine, Encoding.UTF8);
        foreach (var result in results) Debug.Log("SANDBOX_VALIDATION " + result);
        Debug.Log("SANDBOX_VALIDATION_REPORT " + reportPath);

        if (results.Exists(r => r.StartsWith("FAIL ", StringComparison.Ordinal)))
        {
            Debug.Log("SANDBOX_VALIDATION_RESULT FAIL");
            EditorApplication.Exit(1);
            throw new InvalidOperationException("Sandbox validation failed. See " + reportPath);
        }
        Debug.Log("SANDBOX_VALIDATION_RESULT PASS");
        EditorApplication.Exit(0);
    }

    private static void AssertRayHitsPart(ZombiePart expected, int mask)
    {
        var bounds = expected.GetComponent<Collider>().bounds;
        var origin = bounds.center - Vector3.forward * 2.5f;
        var direction = (bounds.center - origin).normalized;
        RaycastHit hit;
        Require(Physics.Raycast(origin, direction, out hit, 3f, mask, QueryTriggerInteraction.Ignore), "No ray hit for " + expected.name);
        Require(hit.collider.gameObject == expected.gameObject, "Ray hit " + hit.collider.name + " instead of " + expected.name);
        Require(hit.collider.GetComponent<ZombiePart>() == expected, "Ray did not resolve ZombiePart for " + expected.name);
        Require(hit.collider.GetComponent<CharacterController>() == null, "Ray resolved movement controller for " + expected.name);
    }

    private static int GetWeaponMask()
    {
        int zombieMoverLayer = LayerMask.NameToLayer("ZombieMover");
        int playerLayer = LayerMask.NameToLayer("Player");
        Require(zombieMoverLayer >= 0, "Project layer ZombieMover is not configured");
        Require(playerLayer >= 0, "Project layer Player is not configured");
        Require(zombieMoverLayer != playerLayer, "ZombieMover and Player layers must differ");
        Require(LayerMask.NameToLayer("Corpse") == Layers.Corpse && LayerMask.NameToLayer("ZombieHitbox") == Layers.ZombieHitbox && LayerMask.NameToLayer("Environment") == Layers.Environment, "Corpse/ZombieHitbox/Environment layers are not registered at the expected indices");
        return Physics.DefaultRaycastLayers & ~((1 << zombieMoverLayer) | (1 << playerLayer));
    }

    private static void Check(List<string> results, string name, Action action)
    {
        try
        {
            action();
            results.Add("PASS " + name);
        }
        catch (Exception e)
        {
            results.Add("FAIL " + name + " :: " + e.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
