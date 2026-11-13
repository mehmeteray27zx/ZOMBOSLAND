using UnityEngine;
using UnityEngine.AI;

namespace Zombosland
{
    public enum ZombieState { Idle, Wander, Chase, Attack, Stagger, Dead, Investigate, BreakBarricade }

    // Zombie gameplay: perception, explicit state machine, damage. Animation/ragdoll only read it.
    public class ZombieBrain : MonoBehaviour, IDamageable
    {
        public Sandbox player;
        // Wander radius around the home point in metres (scene placement sets it; 6 is the old fixed value).
        [SerializeField] float wanderRadius = 6f;

        // Tunable numbers live in a ZombieDef asset. Empty (old prefabs/scenes) = built-in defaults, identical to the old constants.
        [SerializeField] ZombieDef def;
        ZombieDef resolved; // looked up once: the Unity null check is too slow to repeat per value per frame
        ZombieDef D { get { if ((object)resolved == null) resolved = def != null ? def : ZombieDef.Fallback; return resolved; } }
        public ZombieDef Def { get { return D; } }
        float SightRange { get { return D.sightRange; } }
        float SightHalfAngle { get { return D.sightHalfAngle; } }
        float CloseSense { get { return D.closeSense; } }
        float AttackRange { get { return D.attackRange; } }
        float AttackCooldown { get { return D.attackCooldown; } }
        float AttackDamage { get { return D.attackDamage; } }
        float WalkSpeed { get { return D.walkSpeed; } }
        float InvestigateSpeedFactor { get { return D.investigateSpeedFactor; } }
        float WindupSeconds { get { return D.attackWindupSeconds; } }
        // Readable telegraph: the attack clip speed in ZombieFactory is derived from the windup (default value kept here for the editor).
        public const float AttackWindupSeconds = ZombieDef.DefaultAttackWindupSeconds;
        const float LoseTargetSeconds = 6f;
        const float InvestigateWaitSeconds = 3f, InvestigateTimeout = 20f;
        const float BarricadeReach = 1.8f, BreakRepathSeconds = 0.4f, PartialRouteHoldSeconds = 1f;
        bool partialRouteHeld;

        // Created on first use, not in Awake: IsDead/Health may be read before Awake runs (inactive zombie, script order).
        HealthState healthState;
        HealthState health { get { return healthState ?? (healthState = new HealthState(D.maxHealth)); } }
        NavMeshAgent agent;
        ZombieAnimator animator;
        ZombieRagdoll ragdoll;
        Collider mover;
        ZombieDirector director;

        bool active, fallback;
        float chaseSpeed, senseTimer, repathTimer, growlTimer, wanderTimer;
        float staggerEnd, windupEnd, attackEnd, lastSeenAt, fallbackSpeed;
        bool strikeDone, canSee;
        float poise, poiseWindowStart = -100f, lastHeavyTime = -100f, flinchSlowUntil;
        Vector3 lastKnown, home, lastPosition;
        Vector3 investigateTarget;
        float investigateStart, investigateArrivedAt = -1f;
        BuildSlot breakTarget;
        bool breakStrikeDone;
        float breakNextRepath;

        public ZombieState State { get; private set; } = ZombieState.Wander;
        public bool IsDead { get { return health.IsDead; } }
        // Read-only view of the last perception result (updated every ~0.15 s while the brain runs).
        public bool CanSeePlayer { get { return canSee; } }
        public float WanderRadius { get { return wanderRadius; } }
        public float Health { get { return health.Current; } }
        public Sandbox Player { get { return player; } }
        public float PlanarSpeed { get; private set; }
        public float AttackWindup01 { get; private set; }
        public Vector3 StaggerPush { get; private set; }

        // Light hit reaction (flinch): published for the animator; gameplay state is unchanged.
        public float LastFlinchTime { get; private set; } = -100f;
        public Vector3 FlinchDirection { get; private set; }
        public HitZone FlinchZone { get; private set; }
        public HumanLimb FlinchLimb { get; private set; }
        public float FlinchSide { get; private set; } // -1 = hit on the zombie's left, +1 = right

        // Called by ZombieDirector once the NavMesh exists (or failed to build).
        public void Activate(ZombieDirector owner)
        {
            director = owner;
            if (player == null) player = owner.Player;
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<ZombieAnimator>();
            ragdoll = GetComponent<ZombieRagdoll>();
            mover = GetComponent<CapsuleCollider>();
            chaseSpeed = Random.Range(D.chaseSpeedMin, D.chaseSpeedMax);
            ApplyTint();
            if (animator != null) animator.SetVariant(Random.value < 0.5f ? 0 : 1);
            fallback = !owner.NavMeshReady || agent == null;
            if (!fallback)
            {
                agent.enabled = true;
                agent.acceleration = D.acceleration;
                agent.angularSpeed = D.turnSpeed;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas)) agent.Warp(hit.position);
                else { agent.enabled = false; fallback = true; }
            }
            home = transform.position;
            lastPosition = transform.position;
            growlTimer = Random.Range(2f, 8f);
            wanderTimer = Random.Range(0.5f, 3f);
            senseTimer = Random.value * 0.2f;
            active = true;
            NoiseEvents.Heard -= OnNoise;
            NoiseEvents.Heard += OnNoise;
            NoiseEvents.Distraction -= OnDistraction;
            NoiseEvents.Distraction += OnDistraction;
        }

        void OnDestroy()
        {
            NoiseEvents.Heard -= OnNoise;
            NoiseEvents.Distraction -= OnDistraction;
        }

        // Per-instance skin variation (brightness +-10 %, slight hue) through a property block; materials stay shared.
        void ApplyTint()
        {
            foreach (SkinnedMeshRenderer rend in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material m = rend.sharedMaterial;
                if (m == null || !m.HasProperty("_BaseColor")) continue;
                float v = Random.Range(0.9f, 1.1f);
                Color tint = new Color(v * Random.Range(0.97f, 1.03f), v, v * Random.Range(0.97f, 1.03f), 1f);
                var block = new MaterialPropertyBlock();
                rend.GetPropertyBlock(block);
                block.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                rend.SetPropertyBlock(block);
            }
        }

        // QA only: forces the attack wind-up pose value while the brain is frozen.
        internal void QaSetWindup(float value) { AttackWindup01 = value; }

        public void Alert()
        {
            if (IsDead || player == null) return;
            lastKnown = player.transform.position;
            lastSeenAt = Time.time;
            State = ZombieState.Chase;
        }

        // ---------- damage ----------

        public bool ApplyDamage(in DamageInfo info) { return ApplyDamage(info, null); }

        // `part` (may be null) tells the animator which limb was hit; it never changes damage.
        public bool ApplyDamage(in DamageInfo info, ZombiePart part)
        {
            if (health.IsDead) return false;
            float amount = info.Amount * DamageRules.Multiplier(info.Zone);
            bool killed = health.Damage(amount);
            if (killed) { Die(info); return true; }
            float now = Time.time;
            if (now - poiseWindowStart > DamageRules.PoiseWindow) { poise = 0f; poiseWindowStart = now; }
            poise += amount;
            bool heavy = (info.Zone == HitZone.Head || poise >= DamageRules.PoiseThreshold) && now - lastHeavyTime >= DamageRules.HeavyStaggerCooldown;
            if (heavy)
            {
                poise = 0f;
                lastHeavyTime = now;
                EnterStagger(info);
            }
            else Flinch(info, part);
            return false;
        }

        // Light reaction: keep the state, slow movement briefly; only a head hit interrupts an attack wind-up.
        void Flinch(in DamageInfo info, ZombiePart part)
        {
            float now = Time.time;
            LastFlinchTime = now;
            Vector3 dir = info.Direction; dir.y = 0;
            FlinchDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            FlinchZone = info.Zone;
            FlinchLimb = part != null ? part.limb : HumanLimb.None;
            FlinchSide = Vector3.Dot(info.Point - transform.position, transform.right) < 0f ? -1f : 1f;
            flinchSlowUntil = now + 0.30f;
            if (player != null) { lastKnown = player.transform.position; lastSeenAt = now; }
            if (info.Zone == HitZone.Head && State == ZombieState.Attack && !strikeDone)
            {
                AttackWindup01 = 0f;
                attackEnd = Mathf.Max(attackEnd, now + 0.4f);
                if (animator != null) animator.CancelAttack();
                State = ZombieState.Chase;
            }
        }

        void EnterStagger(in DamageInfo info)
        {
            float duration = DamageRules.HeavyStaggerSeconds(info.Zone);
            staggerEnd = Mathf.Max(staggerEnd, Time.time + duration);
            Vector3 push = info.Direction; push.y = 0;
            StaggerPush = push.sqrMagnitude > 0.0001f ? push.normalized : Vector3.zero;
            // A heavy hit cancels any attack in progress and tells the zombie where the player is.
            AttackWindup01 = 0f;
            strikeDone = false;
            State = ZombieState.Stagger;
            if (player != null) { lastKnown = player.transform.position; lastSeenAt = Time.time; }
            if (animator != null) animator.TriggerHeavyHit();
            StopMoving();
        }

        void Die(in DamageInfo info)
        {
            State = ZombieState.Dead;
            AttackWindup01 = 0f;
            NoiseEvents.Heard -= OnNoise;
            NoiseEvents.Distraction -= OnDistraction;
            if (agent != null) agent.enabled = false;
            if (mover != null) mover.enabled = false;
            if (animator != null) { animator.StopClips(); animator.enabled = false; }
            if (ragdoll != null) ragdoll.Activate(info);
            if (SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.ZombieDeath, transform.position + Vector3.up, 1f, Random.Range(0.92f, 1.05f), true);
            if (EffectPool.Instance != null) EffectPool.Instance.BloodStain(transform.position);
            if (director != null) director.RegisterCorpse(this);
        }

        // ---------- update ----------

        void OnNoise(Vector3 position, float radius)
        {
            if (!active || IsDead) return;
            float sqrDistance = (position - transform.position).sqrMagnitude;
            if (sqrDistance > radius * radius) return;
            bool wakes = State == ZombieState.Idle || State == ZombieState.Wander || State == ZombieState.Investigate;
            if (!wakes && !(State == ZombieState.Chase && !canSee)) return;
            // Walls dull the shot: the effective radius halves. At most one ray per zombie per shot, and only when it can matter.
            if (sqrDistance > radius * radius * 0.25f && NoiseBlocked(position)) return;
            lastKnown = position;
            lastSeenAt = Time.time;
            State = ZombieState.Chase;
            if (wakes) NoiseEvents.RaiseZombieHeard(this); // only a real switch to Chase counts as "heard"
        }

        // Thrown stone: walk to the point, look around for a moment, then go back to wandering (no chase, no "heard" hint).
        void OnDistraction(Vector3 position, float radius)
        {
            if (!active || IsDead) return;
            if (State == ZombieState.Attack || State == ZombieState.Stagger || State == ZombieState.Dead || State == ZombieState.BreakBarricade) return;
            if (State == ZombieState.Chase && canSee) return;
            float sqrDistance = (position - transform.position).sqrMagnitude;
            if (sqrDistance > radius * radius) return;
            if (sqrDistance > radius * radius * 0.25f && NoiseBlocked(position)) return; // walls halve the effective radius
            Vector3 target = position;
            if (!fallback)
            {
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(position, out hit, 1.5f, NavMesh.AllAreas)) return;
                target = hit.position;
            }
            investigateTarget = target;
            investigateStart = Time.time;
            investigateArrivedAt = -1f;
            State = ZombieState.Investigate;
            if (player != null && !player.ZombiesFrozen) MoveTo(target, WalkSpeed * InvestigateSpeedFactor, true);
        }

        // True when level geometry (SightMask) lies between the shot and this zombie's head.
        // The ray starts 0.5 m out from the shooter so it never starts inside the player's own capsule.
        bool NoiseBlocked(Vector3 source)
        {
            Vector3 target = transform.position + Vector3.up * 1.6f;
            Vector3 d = target - source;
            float distance = d.magnitude;
            if (distance < 1f) return false;
            Vector3 dir = d / distance;
            return Physics.Raycast(source + dir * 0.5f, dir, distance - 0.5f, Layers.SightMask, QueryTriggerInteraction.Ignore);
        }

        void Update()
        {
            if (!active || State == ZombieState.Dead) return;
            if (player == null || player.ZombiesFrozen)
            {
                PlanarSpeed = 0f;
                if (animator != null) animator.DriveClip(0f, true);
                if (State == ZombieState.Stagger && Time.time >= staggerEnd) State = ZombieState.Idle; // QA freeze: let reactions end
                return;
            }
            float now = Time.time;
            float dt = Time.deltaTime;

            senseTimer -= dt;
            if (senseTimer <= 0f) { senseTimer = 0.15f + Random.value * 0.05f; canSee = CanSee(); }

            switch (State)
            {
                case ZombieState.Idle: UpdateIdle(dt); break;
                case ZombieState.Wander: UpdateWander(dt); break;
                case ZombieState.Chase: UpdateChase(now); break;
                case ZombieState.Attack: UpdateAttack(now); break;
                case ZombieState.Investigate: UpdateInvestigate(now); break;
                case ZombieState.BreakBarricade: UpdateBreakBarricade(now); break;
                case ZombieState.Stagger:
                    if (now >= staggerEnd) State = ZombieState.Chase;
                    break;
            }

            UpdateGrowl(dt);
            UpdateSpeedEstimate(dt);
            if (animator != null) animator.DriveClip(PlanarSpeed, false);
        }

        void UpdateIdle(float dt)
        {
            StopMoving();
            if (canSee) { Alert(); return; }
            wanderTimer -= dt;
            if (wanderTimer <= 0f) { State = ZombieState.Wander; PickWanderTarget(); }
        }

        void UpdateWander(float dt)
        {
            if (canSee) { Alert(); return; }
            if (HasReachedTarget() || (wanderTimer -= dt) <= 0f)
            {
                wanderTimer = Random.Range(2f, 5f);
                State = ZombieState.Idle;
                StopMoving();
            }
        }

        void PickWanderTarget()
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            MoveTo(home + new Vector3(offset.x, 0, offset.y), WalkSpeed, true);
            wanderTimer = 6f;
        }

        void UpdateChase(float now)
        {
            if (canSee) { lastKnown = player.transform.position; lastSeenAt = now; }
            else if (now - lastSeenAt > LoseTargetSeconds)
            {
                State = ZombieState.Wander;
                home = transform.position;
                PickWanderTarget();
                return;
            }

            if (canSee && PlanarDistance(player.transform.position) <= AttackRange && now >= attackEnd)
            {
                State = ZombieState.Attack;
                windupEnd = now + WindupSeconds;
                strikeDone = false;
                if (animator != null) animator.TriggerAttack();
                StopMoving();
                if (SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.ZombieAttack, transform.position + Vector3.up, 0.9f, Random.Range(0.9f, 1.1f), true);
                return;
            }
            if (TryBeginBreak(now)) return;
            MoveTo(lastKnown, chaseSpeed, false);
        }

        // ---------- barricade ----------

        // Chasing with no complete route and a built barricade right here: the way is shut, so break it.
        bool TryBeginBreak(float now)
        {
            if (fallback || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            if (agent.pathPending || agent.pathStatus == NavMeshPathStatus.PathComplete) return false;
            BuildSlot slot = BuildSlot.NearestBuiltBarricade(transform.position, BarricadeReach);
            if (slot == null) return false;
            breakTarget = slot;
            State = ZombieState.BreakBarricade;
            breakNextRepath = now + BreakRepathSeconds;
            StartBreakStrike(now);
            StopMoving();
            return true;
        }

        void StartBreakStrike(float now)
        {
            breakStrikeDone = false;
            windupEnd = now + WindupSeconds;
            if (animator != null) animator.TriggerAttack();
            if (SfxBank.Instance != null) SfxBank.Instance.Play(Sfx.ZombieAttack, transform.position + Vector3.up, 0.9f, Random.Range(0.9f, 1.1f), true);
        }

        void LeaveBreak()
        {
            breakTarget = null;
            AttackWindup01 = 0f;
            if (animator != null) animator.CancelAttack();
            State = ZombieState.Chase;
        }

        void UpdateBreakBarricade(float now)
        {
            if (breakTarget == null || !breakTarget.Built || fallback || agent == null || !agent.enabled || !agent.isOnNavMesh) { LeaveBreak(); return; }
            if (canSee)
            {
                lastKnown = player.transform.position;
                lastSeenAt = now;
                if (PlanarDistance(player.transform.position) <= AttackRange && now >= attackEnd) { LeaveBreak(); return; } // the player is in reach: normal attack
            }
            else if (now - lastSeenAt > LoseTargetSeconds) { LeaveBreak(); return; }

            // Re-plan now and then; a complete route (barricade gone, another way open) ends the siege.
            if (now >= breakNextRepath)
            {
                breakNextRepath = now + BreakRepathSeconds;
                agent.SetDestination(lastKnown);
            }
            else if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathComplete) { LeaveBreak(); return; }

            FaceTowards(breakTarget.transform.position);
            if (!breakStrikeDone)
            {
                AttackWindup01 = Mathf.Clamp01(1f - (windupEnd - now) / WindupSeconds);
                if (now >= windupEnd)
                {
                    breakStrikeDone = true;
                    AttackWindup01 = 0f;
                    attackEnd = now + AttackCooldown;
                    breakTarget.ApplyDamage(AttackDamage);
                }
            }
            else if (now >= attackEnd) StartBreakStrike(now);
        }

        void UpdateInvestigate(float now)
        {
            if (canSee) { Alert(); return; }
            if (now - investigateStart > InvestigateTimeout) { EndInvestigate(); return; }
            if (investigateArrivedAt < 0f)
            {
                bool arrived = PlanarDistance(investigateTarget) <= 1.2f || (now - investigateStart > 0.3f && HasReachedTarget());
                if (arrived)
                {
                    investigateArrivedAt = now;
                    StopMoving();
                }
                else MoveTo(investigateTarget, WalkSpeed * InvestigateSpeedFactor, false);
            }
            else if (now - investigateArrivedAt >= InvestigateWaitSeconds) EndInvestigate();
        }

        void EndInvestigate()
        {
            home = transform.position;
            State = ZombieState.Wander;
            PickWanderTarget();
        }

        void UpdateAttack(float now)
        {
            FaceTowards(player.transform.position);
            if (!strikeDone)
            {
                AttackWindup01 = Mathf.Clamp01(1f - (windupEnd - now) / WindupSeconds);
                if (now >= windupEnd)
                {
                    strikeDone = true;
                    AttackWindup01 = 0f;
                    attackEnd = now + AttackCooldown;
                    if (PlanarDistance(player.transform.position) <= AttackRange + 0.3f && HasLineOfSight())
                    {
                        var info = new DamageInfo { Amount = AttackDamage, Point = player.transform.position + Vector3.up, Direction = transform.forward, Zone = HitZone.Body, Source = this };
                        player.ApplyDamage(info);
                    }
                }
            }
            else if (now >= attackEnd) State = ZombieState.Chase;
        }

        void UpdateGrowl(float dt)
        {
            growlTimer -= dt;
            if (growlTimer > 0f) return;
            growlTimer = Random.Range(4f, 9f);
            if (SfxBank.Instance != null && PlanarDistance(player.transform.position) < 30f)
                SfxBank.Instance.Play(Sfx.ZombieGrowl, transform.position + Vector3.up * 1.5f, 0.8f, Random.Range(0.9f, 1.1f), true);
        }

        void UpdateSpeedEstimate(float dt)
        {
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0;
            lastPosition = transform.position;
            float measured = dt > 0.0001f ? delta.magnitude / dt : 0f;
            PlanarSpeed = Mathf.Lerp(PlanarSpeed, measured, 1f - Mathf.Exp(-10f * dt));
        }

        // ---------- perception ----------

        bool CanSee()
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0;
            float distance = toPlayer.magnitude;
            if (distance > SightRange) return false;
            if (distance > CloseSense && Vector3.Angle(transform.forward, toPlayer) > SightHalfAngle) return false;
            return HasLineOfSight();
        }

        bool HasLineOfSight()
        {
            Vector3 from = transform.position + Vector3.up * 1.6f;
            Vector3 to = player.transform.position + Vector3.up * 1.2f;
            Vector3 dir = to - from;
            RaycastHit hit;
            if (!Physics.Raycast(from, dir.normalized, out hit, dir.magnitude + 0.1f, Layers.SightMask, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider.GetComponentInParent<Sandbox>() == player;
        }

        float PlanarDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0;
            return d.magnitude;
        }

        // ---------- movement (NavMeshAgent, or straight-line fallback) ----------

        void MoveTo(Vector3 destination, float speed, bool immediate)
        {
            if (fallback)
            {
                Vector3 d = destination - transform.position;
                d.y = 0;
                if (d.magnitude < 0.3f) return;
                FaceTowards(destination);
                transform.position += d.normalized * (Time.time < flinchSlowUntil ? speed * 0.55f : speed) * Time.deltaTime;
                return;
            }
            agent.isStopped = false;
            agent.speed = Time.time < flinchSlowUntil ? speed * 0.55f : speed;
            repathTimer -= Time.deltaTime;
            if (immediate || repathTimer <= 0f)
            {
                // A target with no complete route makes the search cover the whole map and outlast the repath interval.
                // Restarting it would starve the agent, so it finishes first and its partial route is walked for a moment.
                if (!immediate)
                {
                    if (agent.pathPending) return;
                    if (agent.pathStatus == NavMeshPathStatus.PathPartial && !partialRouteHeld)
                    {
                        partialRouteHeld = true;
                        repathTimer = PartialRouteHoldSeconds;
                        return;
                    }
                }
                partialRouteHeld = false;
                repathTimer = 0.2f;
                agent.SetDestination(destination);
            }
        }

        void StopMoving()
        {
            if (!fallback && agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
        }

        bool HasReachedTarget()
        {
            if (fallback) return false;
            return agent.enabled && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;
        }

        void FaceTowards(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
        }
    }
}
