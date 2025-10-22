using UnityEngine;

public class RotateOnAxes : MonoBehaviour
{
    [Header("Rotation speed per axis (degrees per second)")]
    public Vector3 rotationSpeed = new Vector3(0f, 60f, 30f);

    void Start()
    {
        // Apply a random offset to starting rotation
        transform.Rotate(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
    }

    void Update()
    {
        // Rotate independently along each local axis
        transform.Rotate(
            rotationSpeed.x * Time.deltaTime,
            rotationSpeed.y * Time.deltaTime,
            rotationSpeed.z * Time.deltaTime,
            Space.Self
        );
    }
}
