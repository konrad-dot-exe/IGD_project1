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

        float yaw, pitch;
        public EnemyShip CurrentTarget { get; private set; }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * sensitivity;
            pitch -= delta.y * sensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // soft-lock nearest enemy within cone
            CurrentTarget = EnemyShip.FindNearestInCone(cam.position, cam.forward, softLockAngle, 200f);
        }
    }
}
