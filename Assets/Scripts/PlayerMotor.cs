using UnityEngine;

namespace Zombosland
{
    // Look + movement. Yaw lives on the body, pitch and recoil on AimPivot (the shot origin).
    public class PlayerMotor : MonoBehaviour
    {
        // Jump and air movement tuning.
        const float JumpHeight = 0.9f;   // apex in metres
        const float Gravity = 20f;       // m/s^2 while rising; stronger than Physics.gravity so the jump is short
        const float FallGravity = 32f;   // m/s^2 while falling; heavier than the rise so the way down never floats
        const float MaxFallSpeed = 30f;  // m/s
        const float CoyoteTime = 0.12f;  // a jump is still allowed this long after leaving the ground
        const float JumpBuffer = 0.12f;  // a press this long before landing still jumps
        const float AirSteer = 10f;      // m/s^2 the horizontal velocity can change while airborne
        const float WalkSpeed = 4f, SprintSpeed = 6f, CrouchSpeed = 2.2f;

        // Stamina: sprinting and jumping spend it; it refills after a short rest.
        const float MaxStamina = 100f;
        const float SprintDrain = 12f;       // per second: about 8 s (50 m) of sprint from full
        const float StaminaRegen = 18f;      // per second, once RegenDelay has passed since the last spend
        const float RegenDelay = 1f;
        const float JumpCost = 12f;
        const float RecoverThreshold = 25f;  // after running dry, sprint stays locked until this much is back
        float stamina = MaxStamina, lastSpendAt = -10f;
        public float Stamina01 { get { return stamina / MaxStamina; } }
        public bool Exhausted { get; private set; }

        CharacterController controller;
        Transform aimPivot;
        float pitch, vertical;
        float clock, lastGroundedAt = -10f, jumpPressedAt = -10f, airCap = WalkSpeed;
        Vector3 planar;                  // horizontal velocity, world space

        public float Recoil;                       // gameplay aim kick in degrees, independent of settings
        public bool InputEnabled = true;           // false in perf mode
        public float ScriptedYawSpeed = float.NaN; // deg/s, used by the perf harness
        public float HorizontalSpeed { get; private set; }
        public bool Grounded { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Crouching { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            aimPivot = transform.Find("AimPivot");
        }

        // QA only (--qa-screens): fixed look pitch in degrees (positive = down).
        internal void QaSetPitch(float degrees) { pitch = degrees; }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            vertical = 0;
            planar = Vector3.zero;
        }

        public void Tick(float dt)
        {
            if (!float.IsNaN(ScriptedYawSpeed)) transform.Rotate(0, ScriptedYawSpeed * dt, 0);
            else if (InputEnabled)
            {
                float sensitivity = GameSettings.Sensitivity;
                transform.Rotate(0, Input.GetAxisRaw("Mouse X") * sensitivity, 0);
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sensitivity * (GameSettings.InvertY ? -1f : 1f), -85, 85);
            }
            Recoil = Mathf.MoveTowards(Recoil, 0, 12 * dt);
            aimPivot.localRotation = Quaternion.Euler(pitch - Recoil, 0, 0);

            bool crouch = InputEnabled && Input.GetKey(KeyCode.LeftControl);
            if (!crouch && controller.height < 1.75f)
            {
                foreach (Collider ceiling in Physics.OverlapSphere(transform.position + Vector3.up * 1.6f, 0.28f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (ceiling != controller && !ceiling.transform.IsChildOf(transform)) { crouch = true; break; }
            }
            Crouching = crouch;
            controller.height = Mathf.Lerp(controller.height, crouch ? 1.2f : 1.8f, 12 * dt);
            controller.center = Vector3.up * controller.height / 2;
            aimPivot.localPosition = Vector3.up * (controller.height - 0.15f);

            Vector3 move = InputEnabled ? Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")), 1) : Vector3.zero;
            Sprinting = InputEnabled && !crouch && !Exhausted && Input.GetKey(KeyCode.LeftShift) && move.z > 0.1f;
            float speed = crouch ? CrouchSpeed : Sprinting ? SprintSpeed : WalkSpeed;

            clock += dt;
            if (Sprinting)
            {
                stamina = Mathf.Max(0f, stamina - SprintDrain * dt);
                lastSpendAt = clock;
                if (stamina <= 0f) Exhausted = true;
            }
            else if (clock - lastSpendAt >= RegenDelay) stamina = Mathf.Min(MaxStamina, stamina + StaminaRegen * dt);
            if (Exhausted && stamina >= RecoverThreshold) Exhausted = false;

            bool onGround = controller.isGrounded;
            if (onGround)
            {
                lastGroundedAt = clock;
                if (vertical < 0) vertical = -2;
            }
            // isGrounded flickers on slopes and the press lasts one frame, so remember both for a moment.
            if (InputEnabled && Input.GetButtonDown("Jump")) jumpPressedAt = clock;
            if (!crouch && stamina >= JumpCost && clock - jumpPressedAt <= JumpBuffer && clock - lastGroundedAt <= CoyoteTime)
            {
                stamina -= JumpCost;
                lastSpendAt = clock;
                vertical = Mathf.Sqrt(2f * Gravity * JumpHeight);
                jumpPressedAt = lastGroundedAt = -10f;
                onGround = false;
            }
            vertical = Mathf.Max(vertical - (vertical > 0 ? Gravity : FallGravity) * dt, -MaxFallSpeed);

            // Ground: instant response. Air: keep momentum, steer slowly, never faster than at take-off.
            Vector3 wish = transform.TransformDirection(move) * speed;
            if (onGround)
            {
                planar = wish;
                airCap = Mathf.Max(wish.magnitude, WalkSpeed);
            }
            else if (move.sqrMagnitude > 0.01f)
                planar = Vector3.MoveTowards(planar, Vector3.ClampMagnitude(wish, airCap), AirSteer * dt);

            CollisionFlags hit = controller.Move((planar + Vector3.up * vertical) * dt);
            if ((hit & CollisionFlags.Above) != 0 && vertical > 0) vertical = 0; // head hit: fall at once, do not hang
            Grounded = controller.isGrounded || (vertical <= 0 && clock - lastGroundedAt <= CoyoteTime);
            Vector3 v = controller.velocity;
            HorizontalSpeed = new Vector2(v.x, v.z).magnitude;
        }
    }
}
