using UnityEngine;

namespace Zombosland
{
    // Fixed-capacity VFX pools. Nothing is instantiated or destroyed during play; the oldest entry is recycled.
    public class EffectPool : MonoBehaviour
    {
        public static EffectPool Instance { get; private set; }

        const int FlashCount = 4, WorldImpactCount = 16, BloodCount = 16, DecalCount = 64;
        const float FlashSeconds = 0.05f;
        // Blood burst particle counts (head hits splash more; MakeBurst allows up to 3x the normal count).
        const int BloodNormalBurst = 14, BloodHeadBurst = 24;
        // Ground blood stains left by dying zombies: fixed ring, oldest is recycled.
        const int StainCount = 12;
        const float StainLift = 0.015f, StainMinScale = 0.6f, StainMaxScale = 1.2f, StainRayUp = 0.4f, StainRayLength = 2.5f;
        // Level geometry only: no zombies / corpses / hitboxes / triggers, and not the player.
        const int StainMask = Layers.SightMask & ~(1 << Layers.Player);
        static readonly Color StainColor = new Color(0.22f, 0.01f, 0.01f, 0.8f);

        public Material flashMaterial, dustMaterial, sparkMaterial, bloodMaterial, decalMaterial;
        // Optional shell casings (12 pooled physics bodies). Left empty = feature off.
        public Mesh casingMesh;
        public Material casingMaterial;

        const int CasingCount = 12;
        const float CasingHideSeconds = 3.5f;
        Transform[] casings; Rigidbody[] casingBodies;
        float[] casingSoundAt, casingHideAt;
        int nextCasing;
        bool casingsReady;

        Transform[] flashes; Light[] flashLights; float[] flashOff;
        ParticleSystem[][] worldImpacts; ParticleSystem[] blood;
        Transform[] decals, stains;
        int[] bloodBursts;
        int nextFlash, nextWorld, nextBlood, nextDecal, nextStain;
        bool ready;

        void Awake()
        {
            Instance = this;
            BuildCasings();
            if (flashMaterial == null || dustMaterial == null || sparkMaterial == null || bloodMaterial == null || decalMaterial == null)
            {
                Debug.LogWarning("EffectPool materials missing; VFX disabled.");
                return;
            }
            BuildFlashes();
            BuildImpacts();
            BuildDecals();
            BuildStains();
            ready = true;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnApplicationQuit()
        {
            ready = false;
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t != transform) t.gameObject.SetActive(false);
        }

        void Update()
        {
            UpdateCasings();
            if (!ready) return;
            for (int i = 0; i < flashes.Length; i++)
                if (flashes[i].gameObject.activeSelf && Time.time >= flashOff[i])
                {
                    flashes[i].gameObject.SetActive(false);
                    flashes[i].SetParent(transform, false);
                }
        }

        // ---- shell casings ----

        void BuildCasings()
        {
            if (casingMesh == null || casingMaterial == null) return;
            // Casings live on Ignore Raycast (layer 2): outside the shot mask, and they never push the player or zombies.
            Physics.IgnoreLayerCollision(2, Layers.Player, true);
            Physics.IgnoreLayerCollision(2, Layers.ZombieMover, true);
            Physics.IgnoreLayerCollision(2, Layers.ZombieHitbox, true);
            Physics.IgnoreLayerCollision(2, Layers.Corpse, true);
            casings = new Transform[CasingCount];
            casingBodies = new Rigidbody[CasingCount];
            casingSoundAt = new float[CasingCount];
            casingHideAt = new float[CasingCount];
            for (int i = 0; i < CasingCount; i++)
            {
                var go = new GameObject("Casing " + i);
                go.layer = 2;
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = casingMesh;
                var rend = go.AddComponent<MeshRenderer>();
                rend.sharedMaterial = casingMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var box = go.AddComponent<BoxCollider>();
                Bounds b = casingMesh.bounds;
                box.center = b.center;
                box.size = Vector3.Max(b.size, Vector3.one * 0.008f);
                var body = go.AddComponent<Rigidbody>();
                body.mass = 0.01f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                go.SetActive(false);
                casings[i] = go.transform;
                casingBodies[i] = body;
                casingHideAt[i] = -1f;
                casingSoundAt[i] = -1f;
            }
            casingsReady = true;
        }

        // Ejects one casing from the socket (right + up, small random spread, random spin).
        public void EjectCasing(Transform eject)
        {
            if (!casingsReady || eject == null) return;
            int i = nextCasing; nextCasing = (nextCasing + 1) % CasingCount;
            Transform t = casings[i];
            t.gameObject.SetActive(false);
            t.SetPositionAndRotation(eject.position, eject.rotation * Random.rotation);
            t.gameObject.SetActive(true);
            Rigidbody body = casingBodies[i];
            Vector3 jitter = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f));
            body.linearVelocity = eject.right * 1.6f + eject.up * 1.2f + jitter;
            body.angularVelocity = Random.onUnitSphere * 20f;
            casingSoundAt[i] = Time.time + Random.Range(0.45f, 0.7f);
            casingHideAt[i] = Time.time + CasingHideSeconds;
        }

        void UpdateCasings()
        {
            if (!casingsReady) return;
            float now = Time.time;
            for (int i = 0; i < CasingCount; i++)
            {
                if (!casings[i].gameObject.activeSelf) continue;
                if (casingSoundAt[i] > 0f && now >= casingSoundAt[i])
                {
                    casingSoundAt[i] = -1f;
                    if (SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.Casing, casings[i].position, 0.35f, Random.Range(0.92f, 1.08f), true);
                }
                if (now >= casingHideAt[i]) casings[i].gameObject.SetActive(false);
            }
        }

        // ---- muzzle flash ----

        void BuildFlashes()
        {
            flashes = new Transform[FlashCount];
            flashLights = new Light[FlashCount];
            flashOff = new float[FlashCount];
            for (int i = 0; i < FlashCount; i++)
            {
                var root = new GameObject("MuzzleFlash " + i).transform;
                root.SetParent(transform, false);
                // Two crossed quads (visible from the side and from behind the gun).
                for (int q = 0; q < 2; q++)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(quad.GetComponent<Collider>());
                    quad.transform.SetParent(root, false);
                    quad.transform.localPosition = new Vector3(0, 0, 0.06f);
                    quad.transform.localRotation = Quaternion.Euler(0, 0, q * 90f);
                    quad.transform.localScale = Vector3.one * 0.18f;
                    var rend = quad.GetComponent<Renderer>();
                    rend.sharedMaterial = flashMaterial;
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                }
                // Also a quad facing along the barrel axis for head-on views.
                var cap = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(cap.GetComponent<Collider>());
                cap.transform.SetParent(root, false);
                cap.transform.localPosition = new Vector3(0, 0, 0.03f);
                cap.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                cap.transform.localScale = Vector3.one * 0.1f;
                var capRend = cap.GetComponent<Renderer>();
                capRend.sharedMaterial = flashMaterial;
                capRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                capRend.receiveShadows = false;

                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.localPosition = new Vector3(0, 0, 0.1f);
                light.type = LightType.Point;
                light.range = 6f;
                light.intensity = 8f;
                light.color = new Color(1f, 0.72f, 0.4f);
                light.shadows = LightShadows.None;
                flashLights[i] = light;
                flashes[i] = root;
                root.gameObject.SetActive(false);
            }
        }

        public void MuzzleFlash(Transform muzzle)
        {
            if (!ready || muzzle == null) return;
            int i = nextFlash; nextFlash = (nextFlash + 1) % FlashCount;
            Transform f = flashes[i];
            f.SetParent(muzzle, false);
            f.localPosition = Vector3.zero;
            f.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f)) ;
            f.localScale = Vector3.one * Random.Range(0.85f, 1.2f);
            f.gameObject.SetActive(true);
            flashOff[i] = Time.time + FlashSeconds;
        }

        // ---- particles ----

        void BuildImpacts()
        {
            worldImpacts = new ParticleSystem[WorldImpactCount][];
            for (int i = 0; i < WorldImpactCount; i++)
            {
                var root = new GameObject("WorldImpact " + i).transform;
                root.SetParent(transform, false);
                var dust = MakeBurst("Dust", root, dustMaterial, 6, 0.8f, 1.2f, 0.18f, new Color(0.62f, 0.58f, 0.52f, 0.55f), new Color(0.45f, 0.42f, 0.38f, 0.4f), -0.05f, 35f, true);
                var sparks = MakeBurst("Sparks", root, sparkMaterial, 8, 0.3f, 5f, 0.025f, new Color(1f, 0.8f, 0.4f, 1f), new Color(1f, 0.55f, 0.2f, 1f), 1.5f, 30f, false);
                var sr = sparks.GetComponent<ParticleSystemRenderer>();
                sr.renderMode = ParticleSystemRenderMode.Stretch;
                sr.lengthScale = 3f;
                worldImpacts[i] = new[] { dust, sparks };
            }
            blood = new ParticleSystem[BloodCount];
            bloodBursts = new int[BloodCount];
            for (int i = 0; i < BloodCount; i++)
            {
                bloodBursts[i] = BloodNormalBurst;
                var root = new GameObject("BloodImpact " + i).transform;
                root.SetParent(transform, false);
                blood[i] = MakeBurst("Blood", root, bloodMaterial, BloodNormalBurst, 0.7f, 3.2f, 0.05f, new Color(0.45f, 0.02f, 0.02f, 0.95f), new Color(0.25f, 0.0f, 0.0f, 0.9f), 1.0f, 38f, false);
            }
        }

        static ParticleSystem MakeBurst(string name, Transform parent, Material mat, int count, float life, float speed, float size,
                                        Color c0, Color c1, float gravity, float cone, bool grow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 3;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone;
            shape.radius = 0.02f;
            var colorLife = ps.colorOverLifetime;
            colorLife.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorLife.color = new ParticleSystem.MinMaxGradient(fade);
            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, grow ? AnimationCurve.Linear(0, 0.6f, 1, 1.8f) : AnimationCurve.Linear(0, 1f, 1, 0.3f));
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return ps;
        }

        static void Fire(ParticleSystem ps, Vector3 point, Vector3 normal)
        {
            Transform t = ps.transform.parent != null ? ps.transform.parent : ps.transform;
            t.position = point + normal * 0.01f;
            t.rotation = Quaternion.LookRotation(normal);
            ps.Clear(true);
            ps.Play(true);
        }

        public void WorldImpact(Vector3 point, Vector3 normal)
        {
            if (!ready) return;
            ParticleSystem[] set = worldImpacts[nextWorld]; nextWorld = (nextWorld + 1) % WorldImpactCount;
            Transform root = set[0].transform.parent;
            root.position = point + normal * 0.01f;
            root.rotation = Quaternion.LookRotation(normal);
            foreach (ParticleSystem ps in set) { ps.Clear(true); ps.Play(true); }
        }

        public void BloodImpact(Vector3 point, Vector3 normal) { BloodImpact(point, normal, false); }

        // `strong` (head hits) emits a bigger burst; the burst count is only rewritten when it changes.
        public void BloodImpact(Vector3 point, Vector3 normal, bool strong)
        {
            if (!ready) return;
            int i = nextBlood; nextBlood = (nextBlood + 1) % BloodCount;
            int want = strong ? BloodHeadBurst : BloodNormalBurst;
            if (bloodBursts[i] != want)
            {
                var emission = blood[i].emission;
                emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)want));
                bloodBursts[i] = want;
            }
            Fire(blood[i], point, normal);
        }

        // ---- ground blood stains ----

        void BuildStains()
        {
            stains = new Transform[StainCount];
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", StainColor); // the decal material is dark grey; stains are dark red
            for (int i = 0; i < StainCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "BloodStain " + i;
                quad.transform.SetParent(transform, false);
                var rend = quad.GetComponent<Renderer>();
                rend.sharedMaterial = decalMaterial;
                rend.SetPropertyBlock(block);
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                quad.SetActive(false);
                stains[i] = quad.transform;
            }
        }

        // One downward ray from the body; the oldest stain is recycled when the ring is full.
        public void BloodStain(Vector3 bodyPosition)
        {
            if (!ready || stains == null) return;
            RaycastHit hit;
            if (!Physics.Raycast(bodyPosition + Vector3.up * StainRayUp, Vector3.down, out hit, StainRayLength, StainMask, QueryTriggerInteraction.Ignore)) return;
            if (hit.normal.y < 0.5f) return; // floors only
            Transform s = stains[nextStain]; nextStain = (nextStain + 1) % StainCount;
            s.position = hit.point + hit.normal * StainLift;
            s.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            s.localScale = Vector3.one * Random.Range(StainMinScale, StainMaxScale);
            s.gameObject.SetActive(true);
        }

        // ---- decals ----

        void BuildDecals()
        {
            decals = new Transform[DecalCount];
            for (int i = 0; i < DecalCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "BulletDecal " + i;
                quad.transform.SetParent(transform, false);
                quad.transform.localScale = Vector3.one * 0.06f;
                var rend = quad.GetComponent<Renderer>();
                rend.sharedMaterial = decalMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                quad.SetActive(false);
                decals[i] = quad.transform;
            }
        }

        public void BulletDecal(Vector3 point, Vector3 normal)
        {
            if (!ready) return;
            Transform d = decals[nextDecal]; nextDecal = (nextDecal + 1) % DecalCount;
            d.position = point + normal * 0.002f;
            // A Unity quad shows its front face toward -Z, so look into the surface.
            d.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            d.gameObject.SetActive(true);
        }
    }
}
