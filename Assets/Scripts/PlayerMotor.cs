using UnityEngine;

namespace Zombosland
{
    // Look + movement. Yaw lives on the body, pitch and recoil on AimPivot (the shot origin).
    public class PlayerMotor : MonoBehaviour
    {
        CharacterController controller;
        Transform aimPivot;
        float pitch, vertical;

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
        }

        public void Tick(float dt)
        {
            if (!float.IsNaN(ScriptedYawSpeed)) transform.Rotate(0, ScriptedYawSpeed * dt, 0);
            else if (InputEnabled)
            {
                float sensitivity = GameSettings.Sensitivity;
                transform.Rotate(0, Input.GetAxisRaw("Mouse X") * sensitivity, 0);
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, -85, 85);
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
            Sprinting = InputEnabled && !crouch && Input.GetKey(KeyCode.LeftShift) && move.z > 0.1f;
            float speed = crouch ? 2.2f : Sprinting ? 6f : 4f;
            if (controller.isGrounded && vertical < 0) vertical = -2;
            if (InputEnabled && controller.isGrounded && Input.GetButtonDown("Jump") && !crouch) vertical = 5;
            vertical += Physics.gravity.y * dt;
            controller.Move((transform.TransformDirection(move) * speed + Vector3.up * vertical) * dt);
            Grounded = controller.isGrounded;
            Vector3 v = controller.velocity;
            HorizontalSpeed = new Vector2(v.x, v.z).magnitude;
        }
    }
}
