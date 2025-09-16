using UnityEngine;

public class Billboard : MonoBehaviour
{
    Camera cam;
    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
