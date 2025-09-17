using UnityEngine;
using UnityEngine.InputSystem;

namespace EarFPS
{
    public class TurretController : MonoBehaviour
    {
        [SerializeField] Transform cam;
        [SerializeField] float sensitivity = 0.12f;
        [SerializeField] float minPitch = -60f;
        [SerializeField] float maxPitch = 60f;
        [SerializeField] float softLockAngle = 5f;   // degrees cone for soft lock

        [SerializeField] bool clampYaw = true;
        [SerializeField] float yawClampHalfAngle = 90f; // degrees
        // public read-only so GameManager can query
        public float ClampCenterYaw { get; private set; }
        public Vector3 ClampCenterDir { get; private set; }   // world-space forward on XZ
        public float YawClampHalfAngle => yawClampHalfAngle;
        float startYaw;

        float yaw, pitch;
        public EnemyShip CurrentTarget { get; private set; }

        void Awake()
        {
            // Project world forward onto XZ (ignore pitch) and normalize
            var flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            ClampCenterDir = flatFwd.sqrMagnitude > 0.0001f ? flatFwd.normalized : Vector3.forward;

            // If you capture start yaw, keep your code—no change needed to clamp logic
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector3 eTurret = transform.localEulerAngles;
            startYaw = yaw = eTurret.y;

            startYaw = yaw = transform.eulerAngles.y;
            ClampCenterYaw = startYaw;

        }

        void Update()
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * sensitivity;
            pitch -= delta.y * sensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            if (clampYaw)
            {   
                float deltaFromStart = Mathf.DeltaAngle(startYaw, yaw);
                deltaFromStart = Mathf.Clamp(deltaFromStart, -yawClampHalfAngle, yawClampHalfAngle);
                yaw = startYaw + deltaFromStart;
            }

            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // soft-lock nearest enemy within cone
            CurrentTarget = EnemyShip.FindNearestInCone(cam.position, cam.forward, softLockAngle, 200f);
        }
    }
}
