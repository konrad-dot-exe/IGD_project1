using UnityEngine;

public class MinimapIcon : MonoBehaviour
{
    void LateUpdate()
    {
        var p = transform.parent ? transform.parent.position : transform.position;
        transform.position = new Vector3(p.x, 0f, p.z); // stick to ground plane
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // face up
    }
}
