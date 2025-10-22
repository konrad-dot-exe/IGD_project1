using UnityEngine;
using UnityEngine.InputSystem;

namespace EarFPS
{
    public class TurretController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] Transform cam;

        [Header("Look")]
        [SerializeField] float sensitivity = 0.12f;

        // Absolute pitch limits (safety net)
        [SerializeField] float minPitch = -60f;   // look down limit (negative)
        [SerializeField] float maxPitch = 60f;    // look up limit (positive)

        // Soft-lock cone for target acquisition
        [SerializeField] float softLockAngle = 5f;

        [Header("Yaw Clamp (relative)")]
        [SerializeField] bool clampYaw = true;
        [SerializeField] float yawClampHalfAngle = 90f; // degrees around start yaw

        [Header("Pitch Clamp (relative)")]
        [SerializeField] bool clampPitch = true;
        [SerializeField] float pitchClampHalfAngle = 35f; // degrees around start pitch

        // Public so other systems can query
        public float ClampCenterYaw { get; private set; }
        public Vector3 ClampCenterDir { get; private set; }   // world-space forward on XZ
        public float YawClampHalfAngle => yawClampHalfAngle;

        float startYaw;
        float startPitch;

        float yaw, pitch;

        public EnemyShip CurrentTarget { get; private set; }

        void Awake()
        {
            // Project world forward onto XZ (ignore pitch) and normalize
            var flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            ClampCenterDir = flatFwd.sqrMagnitude > 0.0001f ? flatFwd.normalized : Vector3.forward;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Initialize yaw from turret object, pitch from camera local X
            startYaw   = yaw   = transform.eulerAngles.y;

            // Note: Unity uses 0..360; use localEulerAngles then treat via DeltaAngle in Update
            startPitch = pitch = cam.localEulerAngles.x;

            ClampCenterYaw = startYaw;
        }

        void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw   += delta.x * sensitivity;
            pitch -= delta.y * sensitivity;

            // --- Absolute (safety) clamp first ---
            pitch = Mathf.Clamp(pitch, startPitch + minPitch, startPitch + maxPitch);

            // --- Relative yaw clamp (centered on start yaw) ---
            if (clampYaw)
            {
                float dYaw = Mathf.DeltaAngle(startYaw, yaw);
                dYaw = Mathf.Clamp(dYaw, -yawClampHalfAngle, yawClampHalfAngle);
                yaw = startYaw + dYaw;
            }

            // --- Relative pitch clamp (centered on start pitch) ---
            if (clampPitch)
            {
                float dPitch = Mathf.DeltaAngle(startPitch, pitch);
                dPitch = Mathf.Clamp(dPitch, -pitchClampHalfAngle, pitchClampHalfAngle);
                pitch = startPitch + dPitch;
            }

            // Apply rotations
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);       // yaw on turret
            cam.localRotation       = Quaternion.Euler(pitch, 0f, 0f);     // pitch on camera

            // Soft-lock nearest enemy within cone
            CurrentTarget = EnemyShip.FindNearestInCone(cam.position, cam.forward, softLockAngle, 200f);
        }

        // If you ever need to re-center clamps mid-run (e.g., on checkpoint/respawn)
        public void RecenterClampAnchors()
        {
            startYaw   = transform.eulerAngles.y;
            startPitch = cam.localEulerAngles.x;
            ClampCenterYaw = startYaw;
        }
    }
}
