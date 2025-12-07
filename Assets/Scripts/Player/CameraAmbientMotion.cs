using UnityEngine;

namespace EarFPS
{
    /// <summary>
    /// Adds subtle ambient motion to the camera: slow Y-axis rotation drift and vertical breathing motion.
    /// Compatible with CameraShake (applies in LateUpdate, additive).
    /// </summary>
    public class CameraAmbientMotion : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform to apply motion to. If null, uses this transform.")]
        [SerializeField] Transform target;

        [Header("Rotation Drift (Y-axis)")]
        [Tooltip("Enable/disable rotation drift")]
        [SerializeField] bool enableDrift = true;
        
        [Tooltip("Maximum drift angle in degrees (oscillates between -maxDriftAngle and +maxDriftAngle)")]
        [SerializeField, Range(0.5f, 5f)] float maxDriftAngle = 1.5f;
        
        [Tooltip("Drift speed in cycles per second")]
        [SerializeField, Range(0.01f, 0.5f)] float driftSpeed = 0.05f;
        
        [Tooltip("Random phase offset for drift (0-360 degrees)")]
        [SerializeField] float driftPhaseOffset = 0f;

        [Header("Breathing Motion (Vertical)")]
        [Tooltip("Enable/disable vertical breathing motion")]
        [SerializeField] bool enableBreathing = true;
        
        [Tooltip("Vertical breathing amplitude in world units")]
        [SerializeField, Range(0.001f, 0.5f)] float breathingAmplitude = 0.05f;
        
        [Tooltip("Breathing frequency in cycles per second")]
        [SerializeField, Range(0.1f, 2f)] float breathingFrequency = 0.5f;
        
        [Tooltip("Random phase offset for breathing (0-360 degrees)")]
        [SerializeField] float breathingPhaseOffset = 0f;

        [Header("Debug")]
        [SerializeField] bool debugLogs = false;

        Vector3 basePosition;
        Quaternion baseRotation;
        float driftTime;
        float breathingTime;
        bool isInitialized = false;
        CameraIntro cameraIntro;

        void Awake()
        {
            if (!target) target = transform;
            
            // Initialize time with random phase offsets
            driftTime = driftPhaseOffset * Mathf.Deg2Rad;
            breathingTime = breathingPhaseOffset * Mathf.Deg2Rad;
        }

        void Start()
        {
            // Find CameraIntro component to check if we need to wait for it
            // Do this in Start() to ensure CameraIntro has been initialized
            if (cameraIntro == null)
            {
                cameraIntro = target.GetComponent<CameraIntro>();
                if (cameraIntro == null)
                {
                    // Check parent if target is a child
                    cameraIntro = target.GetComponentInParent<CameraIntro>();
                }
            }
            
            if (debugLogs)
            {
                Debug.Log($"[CameraAmbientMotion] Start() on {target.name}. CameraIntro found: {cameraIntro != null}");
            }
        }

        void LateUpdate()
        {
            if (!target) return;

            // Check if CameraIntro exists and wait for it to complete before applying motion
            if (cameraIntro != null && !cameraIntro.HasPlayedIntro)
            {
                // Intro hasn't completed yet - don't apply motion, but update base transform
                // so we're ready when it completes
                if (!isInitialized)
                {
                    basePosition = target.position;
                    baseRotation = target.rotation;
                }
                return;
            }

            // Intro has completed (or doesn't exist) - initialize base transform if not done yet
            if (!isInitialized)
            {
                basePosition = target.position;
                baseRotation = target.rotation;
                isInitialized = true;
                
                if (debugLogs)
                {
                    Debug.Log($"[CameraAmbientMotion] Base transform captured after intro completion on {target.name}");
                }
            }

            // Update time accumulators
            float deltaTime = Time.deltaTime;
            driftTime += deltaTime * driftSpeed * 2f * Mathf.PI; // Convert to radians
            breathingTime += deltaTime * breathingFrequency * 2f * Mathf.PI;

            // Calculate rotation drift (Y-axis oscillation)
            float yRotation = 0f;
            if (enableDrift)
            {
                yRotation = Mathf.Sin(driftTime) * maxDriftAngle;
            }

            // Calculate vertical breathing motion
            float verticalOffset = 0f;
            if (enableBreathing)
            {
                verticalOffset = Mathf.Sin(breathingTime) * breathingAmplitude;
            }

            // Apply rotation (additive to base rotation)
            // Note: This overwrites any rotation changes from other scripts.
            // If you need to combine with other rotation scripts, consider using a child transform.
            if (enableDrift)
            {
                target.rotation = baseRotation * Quaternion.Euler(0f, yRotation, 0f);
            }

            // Apply position (additive to base position)
            // Note: This overwrites any position changes from other scripts.
            // If you need to combine with other position scripts, consider using a child transform.
            if (enableBreathing)
            {
                target.position = basePosition + Vector3.up * verticalOffset;
            }
        }

        /// <summary>
        /// Enable or disable all ambient motion
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            enableDrift = enabled;
            enableBreathing = enabled;
        }

        /// <summary>
        /// Reset base position and rotation to current transform values
        /// </summary>
        public void ResetBaseTransform()
        {
            if (!target) target = transform;
            basePosition = target.position;
            baseRotation = target.rotation;
            isInitialized = true;
            
            if (debugLogs)
            {
                Debug.Log($"[CameraAmbientMotion] Reset base transform on {target.name}");
            }
        }

        void OnEnable()
        {
            // Find CameraIntro if not already found
            if (cameraIntro == null && target != null)
            {
                cameraIntro = target.GetComponent<CameraIntro>();
                if (cameraIntro == null)
                {
                    cameraIntro = target.GetComponentInParent<CameraIntro>();
                }
            }
            
            // Re-initialize if CameraIntro doesn't exist or has already completed
            if (target != null)
            {
                if (cameraIntro == null || cameraIntro.HasPlayedIntro)
                {
                    basePosition = target.position;
                    baseRotation = target.rotation;
                    isInitialized = true;
                }
                else
                {
                    // Intro exists but hasn't completed - wait for it
                    isInitialized = false;
                }
            }
        }
    }
}

