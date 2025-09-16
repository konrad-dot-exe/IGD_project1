using UnityEngine;
using UnityEngine.UI;

namespace EarFPS
{
    public class CrosshairUI : MonoBehaviour
    {
        [SerializeField] TurretController turret;    // drag Turret here
        [SerializeField] Image image;                // drag this Image
        [SerializeField] Color normal = new Color(1,1,1,0.6f);
        [SerializeField] Color locked = new Color(0f,1f,1f,1f); // cyan when locked
        [SerializeField] float scaleWhenLocked = 1.2f;

        Vector3 baseScale;

        void Awake()
        {
            if (!image) image = GetComponent<Image>();
            baseScale = transform.localScale;
        }

        void Update()
        {
            bool hasTarget = turret && turret.CurrentTarget != null;
            if (image) image.color = hasTarget ? locked : normal;
            transform.localScale = hasTarget ? baseScale * scaleWhenLocked : baseScale;
        }
    }
}
