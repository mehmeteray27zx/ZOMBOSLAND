using UnityEngine;

namespace Zombosland
{
    // Procedural additive animation on top of the rest pose (no clips). Offsets are expressed in zombie space
    // (X = right, Y = up, Z = forward) so they work for primitive bones and for humanoid FBX rigs alike.
    // Arms are posed by direction targeting, so an A-pose rig (Rio) and a hanging-arms rig look the same.
    public class ZombieAnimator : MonoBehaviour
    {
        public Transform hips, spine, chest, head;
        public Transform leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm, leftHand, rightHand;
        public Transform leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg;

        // Clip data measured by the editor (Pxltiger zombie): root-motion speeds of the walk/run clips and the attack strike time.
        public float clipWalkSpeed = 1.2f, clipWalk1Speed = 1.2f, clipRunSpeed = 3f, attackStrike01;

        const float FlinchStiffness = 900f, FlinchDamping = 42f, FlinchKick = 65f;
        const float ArmFlinchDegrees = 34f;
        // Attack telegraph (clip mode): over the first 70 percent of the windup the torso leans back 12 deg (spine + chest) and the head tips up 8 deg.
        const float TelegraphPortion = 0.7f, TelegraphTorsoDegrees = 12f, TelegraphHeadDegrees = 8f;
        const float StepSeconds = 0.2f;
        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int AnimRateParam = Animator.StringToHash("AnimRate");
        static readonly int AttackParam = Animator.StringToHash("Attack");
        static readonly int HeavyHitParam = Animator.StringToHash("HeavyHit");
        static readonly int VariantParam = Animator.StringToHash("Variant");
        static readonly int LocomotionState = Animator.StringToHash("Locomotion");
        static readonly int AttackState = Animator.StringToHash("Attack");

        // Clip mode: an Animator controller drives the pose; only flinch / heavy-push offsets are added on top.
        public bool ClipMode { get; private set; }
        Animator clipAnimator;
        float variant;

        ZombieBrain brain;
        Transform[] bones;
        Quaternion[] rest;
        Vector3 hipsRestLocal;
        // Rest direction (bone-local) from each arm bone to its child: 0/1 upper arms, 2/3 lower arms.
        readonly Vector3[] aimRest = new Vector3[4];
        float phase, stagger, staggerVelocity, flinch, flinchVelocity;
        float moveAmount, windup, lastFlinchSeen = -100f, staggerStart = -100f;
        ZombieState lastState = ZombieState.Idle;
        readonly float phaseOffset = 0f;

        void Awake()
        {
            brain = GetComponent<ZombieBrain>();
            TryFillFromAnimator();
            bones = new[] { hips, spine, chest, head, leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm, leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg };
            rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) rest[i] = bones[i].localRotation;
            if (hips != null) hipsRestLocal = hips.localPosition;
            aimRest[0] = LocalDir(leftUpperArm, leftLowerArm);
            aimRest[1] = LocalDir(rightUpperArm, rightLowerArm);
            aimRest[2] = LocalDir(leftLowerArm, leftHand);
            aimRest[3] = LocalDir(rightLowerArm, rightHand);
            phase = Random.value * 6.28f + phaseOffset;
            clipAnimator = GetComponentInChildren<Animator>();
            ClipMode = clipAnimator != null && clipAnimator.runtimeAnimatorController != null;
        }

        // ---- clip mode bridge (called by ZombieBrain) ----

        public void SetVariant(int v)
        {
            variant = v;
            if (ClipMode) clipAnimator.SetFloat(VariantParam, variant);
        }

        // Speed in m/s; AnimRate keeps the clip's foot speed close to the real movement speed (less sliding).
        public void DriveClip(float speed, bool frozen)
        {
            if (!ClipMode || !clipAnimator.enabled) return;
            clipAnimator.speed = frozen ? 0f : 1f;
            clipAnimator.SetFloat(SpeedParam, frozen ? 0f : speed);
            clipAnimator.SetFloat(AnimRateParam, frozen ? 1f : AnimRate(speed));
        }

        float AnimRate(float s)
        {
            if (s < 0.05f) return 1f;
            float walkNom = Mathf.Lerp(clipWalkSpeed, clipWalk1Speed, variant);
            float nominal;
            if (s <= clipWalkSpeed) nominal = s / clipWalkSpeed * walkNom;
            else if (s < clipRunSpeed) { float t = (s - clipWalkSpeed) / (clipRunSpeed - clipWalkSpeed); nominal = (1f - t) * walkNom + t * clipRunSpeed; }
            else nominal = clipRunSpeed;
            return Mathf.Clamp(s / Mathf.Max(0.01f, nominal), 0.6f, 1.6f);
        }

        public void TriggerAttack() { if (ClipMode && clipAnimator.enabled) { clipAnimator.ResetTrigger(HeavyHitParam); clipAnimator.SetTrigger(AttackParam); } }
        public void TriggerHeavyHit() { if (ClipMode && clipAnimator.enabled) { clipAnimator.ResetTrigger(AttackParam); clipAnimator.SetTrigger(HeavyHitParam); } }
        public void CancelAttack() { if (ClipMode && clipAnimator.enabled) { clipAnimator.ResetTrigger(AttackParam); clipAnimator.CrossFadeInFixedTime(LocomotionState, 0.1f); } }

        // Ragdoll takes over: the Animator must stop writing bones.
        public void StopClips() { if (clipAnimator != null) clipAnimator.enabled = false; }

        // QA only: shows the attack clip at its strike moment (works while the brain is frozen).
        internal void QaPlayAttack()
        {
            if (!ClipMode) return;
            clipAnimator.Play(AttackState, 0, attackStrike01);
            clipAnimator.Update(0f);
        }

        static Vector3 LocalDir(Transform bone, Transform child)
        {
            if (bone == null || child == null) return Vector3.zero;
            Vector3 d = bone.InverseTransformPoint(child.position);
            return d.sqrMagnitude < 1e-8f ? Vector3.zero : d.normalized;
        }

        void TryFillFromAnimator()
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            if (hips == null) hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (spine == null) spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (head == null) head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (leftUpperArm == null) leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (rightUpperArm == null) rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (leftLowerArm == null) leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (rightLowerArm == null) rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (leftHand == null) leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (rightHand == null) rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (leftUpperLeg == null) leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            if (rightUpperLeg == null) rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (leftLowerLeg == null) leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            if (rightLowerLeg == null) rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }

        void LateUpdate()
        {
            if (brain == null || brain.IsDead || (brain.Player != null && brain.Player.IsPaused)) return;
            float now = Time.time;
            float dt = Time.deltaTime;
            float speed = brain.PlanarSpeed;
            float target = Mathf.Clamp01(speed / 1.8f);
            moveAmount = Mathf.Lerp(moveAmount, target, 1f - Mathf.Exp(-8f * dt));
            phase += dt * Mathf.Lerp(2.5f, 5.5f, Mathf.Clamp01(speed / 2.2f)) * moveAmount;
            windup = Mathf.Lerp(windup, brain.AttackWindup01, 1f - Mathf.Exp(-14f * dt));

            // Heavy stagger is a damped spring driven toward 1 while the brain is staggered.
            float goal = brain.State == ZombieState.Stagger ? 1f : 0f;
            staggerVelocity += ((goal - stagger) * 260f - staggerVelocity * 22f) * Mathf.Min(dt, 0.02f);
            stagger += staggerVelocity * Mathf.Min(dt, 0.02f);
            if (brain.State == ZombieState.Stagger && lastState != ZombieState.Stagger) staggerStart = now;
            lastState = brain.State;
            float step = Mathf.Sin(Mathf.PI * Mathf.Clamp01((now - staggerStart) / StepSeconds)); // 0.2 s back step

            // Flinch (light hit) is its own spring, kicked once per hit, gone in about a quarter second.
            if (brain.LastFlinchTime != lastFlinchSeen) { lastFlinchSeen = brain.LastFlinchTime; flinchVelocity += FlinchKick; }
            for (float left = Mathf.Min(dt, 0.05f); left > 0f; left -= 0.01f)
            {
                float h = Mathf.Min(left, 0.01f);
                flinchVelocity += (-FlinchStiffness * flinch - FlinchDamping * flinchVelocity) * h;
                flinch += flinchVelocity * h;
            }

            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float walk = moveAmount;

            // Hit reactions.
            Quaternion bodyFlinch = Quaternion.identity, chestFlinch = Quaternion.identity, headFlinch = Quaternion.identity, hipsFlinch = Quaternion.identity;
            float leftArmFlinch = 0f, rightArmFlinch = 0f;
            Vector3 fdir = brain.FlinchDirection;
            Quaternion twist = Quaternion.AngleAxis(brain.FlinchSide * 6f * flinch, Vector3.up);
            switch (brain.FlinchZone)
            {
                case HitZone.Head:
                    headFlinch = PushRotation(fdir, flinch, 32f);
                    chestFlinch = PushRotation(fdir, flinch, 6f);
                    break;
                case HitZone.Limb:
                    if (brain.FlinchLimb == HumanLimb.LeftArm) leftArmFlinch = flinch;
                    else if (brain.FlinchLimb == HumanLimb.RightArm) rightArmFlinch = flinch;
                    else hipsFlinch = Quaternion.Euler(5f * flinch, 0, 0);
                    break;
                default:
                    chestFlinch = PushRotation(fdir, flinch, 20f) * twist;
                    bodyFlinch = PushRotation(fdir, flinch, 10f);
                    break;
            }

            // Torso: forward lean (more when moving), lurching sway, hit push (heavy: 30 deg body, 45 deg head).
            Quaternion pushSpine = PushRotation(brain.StaggerPush, stagger, 18f);
            Quaternion pushChest = PushRotation(brain.StaggerPush, stagger, 12f);
            Quaternion headPush = PushRotation(brain.StaggerPush, stagger, 45f);

            if (ClipMode)
            {
                // Animated pose is already in the bones: add only the reaction offsets on top of it.
                float lean = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(windup / TelegraphPortion));
                Quaternion leanSpine = Quaternion.AngleAxis(-TelegraphTorsoDegrees * 0.5f * lean, Vector3.right);
                Quaternion leanHead = Quaternion.AngleAxis(-TelegraphHeadDegrees * lean, Vector3.right);
                Offset(0, hipsFlinch);
                Offset(1, leanSpine * pushSpine * bodyFlinch);
                Offset(2, leanSpine * pushChest * chestFlinch);
                Offset(3, leanHead * headPush * headFlinch);
                if (leftArmFlinch > 0.001f) Offset(4, Quaternion.AngleAxis(ArmFlinchDegrees * leftArmFlinch, Vector3.right));
                if (rightArmFlinch > 0.001f) Offset(5, Quaternion.AngleAxis(ArmFlinchDegrees * rightArmFlinch, Vector3.right));
                if (hips != null && step > 0.001f) hips.position += transform.TransformVector(0f, 0f, -0.06f * step);
                if (step > 0.001f)
                {
                    Offset(8, Quaternion.Euler(16f * step, 0, 0));
                    Offset(9, Quaternion.Euler(-6f * step, 0, 0));
                }
                return;
            }
            Set(0, Quaternion.Euler(6f * walk + 3f, 0, Mathf.Sin(phase * 0.5f) * 4f * walk) * hipsFlinch);
            Set(1, Quaternion.Euler(10f * walk + 5f, s * 6f * walk, 0) * pushSpine * bodyFlinch);
            Set(2, Quaternion.Euler(6f * walk, -s * 4f * walk, 0) * pushChest * chestFlinch);
            Set(3, Quaternion.Euler(8f * walk, Mathf.Sin(phase * 0.7f) * 12f * walk, Mathf.Sin(phase * 1.3f) * 10f * walk) * headPush * headFlinch);

            // Back step on a heavy stagger: hips shift back a little (the legs below swing with it).
            if (hips != null)
            {
                hips.localPosition = hipsRestLocal;
                if (step > 0.001f) hips.position += transform.TransformVector(0f, 0f, -0.06f * step);
            }

            // Arms reach forward; raised overhead during the attack windup. Direction targets in zombie space.
            AimArm(4, 0, -1f, windup, c * 6f * walk, leftArmFlinch);
            AimArm(5, 1, 1f, windup, -c * 6f * walk, rightArmFlinch);

            // Legs: shuffling swing, knees bend on the forward step.
            Set(8, Quaternion.Euler(s * 26f * walk + 16f * step, 0, 0));
            Set(9, Quaternion.Euler(-s * 26f * walk - 6f * step, 0, 0));
            Set(10, Quaternion.Euler(Mathf.Max(0f, -c) * 30f * walk + 3f, 0, 0));
            Set(11, Quaternion.Euler(Mathf.Max(0f, c) * 30f * walk + 3f, 0, 0));
        }

        // side: -1 left, +1 right. i = upper arm bone index (lower arm is i + 2), a = aim slot of the upper arm.
        void AimArm(int i, int a, float side, float windupAmount, float swingDegrees, float limbFlinch)
        {
            Vector3 upper = Vector3.Lerp(new Vector3(side * 0.18f, -0.35f, 1f).normalized, new Vector3(side * 0.15f, 0.75f, 0.6f).normalized, windupAmount);
            Vector3 lower = Vector3.Lerp(new Vector3(side * 0.1f, -0.1f, 1f).normalized, new Vector3(side * 0.1f, 0.9f, 0.4f).normalized, windupAmount);
            Quaternion swing = Quaternion.AngleAxis(swingDegrees + ArmFlinchDegrees * limbFlinch, Vector3.right); // limb hit: upper arm swings back
            upper = swing * upper;
            if (aimRest[a] == Vector3.zero || aimRest[a + 2] == Vector3.zero)
            {
                // No usable child direction (incomplete rig): old additive pose.
                float armX = Mathf.Lerp(-72f, -150f, windupAmount) + swingDegrees;
                Set(i, Quaternion.Euler(armX, 0, -side * 6f));
                Set(i + 2, Quaternion.Euler(Mathf.Lerp(-18f, -35f, windupAmount), 0, 0));
                return;
            }
            Aim(i, a, upper, 1f);
            Aim(i + 2, a + 2, lower, 1f);
        }

        // Rotates the bone so that its rest direction toward `child` points along targetZombieSpace.
        void Aim(int boneIndex, int aimIndex, Vector3 targetZombieSpace, float weight)
        {
            Transform bone = bones[boneIndex];
            if (bone == null || bone.parent == null) return;
            Quaternion restWorld = bone.parent.rotation * rest[boneIndex];
            Vector3 restDir = restWorld * aimRest[aimIndex];
            Vector3 targetWorld = transform.TransformDirection(targetZombieSpace.normalized);
            Quaternion aimed = Quaternion.FromToRotation(restDir, targetWorld) * restWorld;
            bone.rotation = Quaternion.Slerp(restWorld, aimed, weight);
        }

        // Rotation about the zombie's horizontal axis perpendicular to the push direction (tips away from the shooter).
        Quaternion PushRotation(Vector3 pushDirection, float amount, float degrees)
        {
            if (pushDirection.sqrMagnitude < 0.0001f) return Quaternion.identity;
            Vector3 axis = Vector3.Cross(Vector3.up, pushDirection.normalized);
            // World-space axis -> zombie space, because offsets are applied in zombie space.
            axis = transform.InverseTransformDirection(axis);
            return Quaternion.AngleAxis(degrees * amount, axis);
        }

        // Applies a zombie-space rotation offset on top of the bone's current (animated) rotation.
        void Offset(int i, Quaternion zombieSpaceOffset)
        {
            Transform bone = bones[i];
            if (bone == null) return;
            Quaternion offsetWorld = transform.rotation * zombieSpaceOffset * Quaternion.Inverse(transform.rotation);
            bone.rotation = offsetWorld * bone.rotation;
        }

        // Applies a zombie-space rotation offset to the rest pose of bone i.
        void Set(int i, Quaternion zombieSpaceOffset)
        {
            Transform bone = bones[i];
            if (bone == null || bone.parent == null) return;
            Quaternion restWorld = bone.parent.rotation * rest[i];
            Quaternion offsetWorld = transform.rotation * zombieSpaceOffset * Quaternion.Inverse(transform.rotation);
            bone.rotation = offsetWorld * restWorld;
        }
    }
}
