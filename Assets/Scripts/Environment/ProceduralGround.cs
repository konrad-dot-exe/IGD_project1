using UnityEngine;
using UnityEngine.Rendering; // for IndexFormat

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralGround : MonoBehaviour
{
    [SerializeField] float radius = 2500f;
    [SerializeField] int radialSegments = 140;
    [SerializeField] int angularSegments = 220;
    [SerializeField] float noiseScale = 0.01f;
    [SerializeField] float height = 18f;
    [SerializeField] float flatRadius = 22f;

    Mesh mesh;

    void OnEnable()  { Rebuild(); }
    void OnValidate(){ if (isActiveAndEnabled) Rebuild(); }

    void Rebuild()
    {
        int vCount = (radialSegments + 1) * (angularSegments + 1);
        var verts = new Vector3[vCount];
        var uvs   = new Vector2[vCount];

        int idx = 0;
        for (int r = 0; r <= radialSegments; r++)
        {
            float t = r / (float)radialSegments;
            float rad = t * radius;
            for (int a = 0; a <= angularSegments; a++)
            {
                float ang = (a / (float)angularSegments) * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * rad;
                float z = Mathf.Sin(ang) * rad;

                float h = 0f;
                if (rad > flatRadius)
                {
                    float nx = (x + 1000f) * noiseScale;
                    float nz = (z + 1000f) * noiseScale;
                    h = (Mathf.PerlinNoise(nx, nz) - 0.5f) * 2f * height;
                }

                verts[idx] = new Vector3(x, h, z);
                uvs[idx]   = new Vector2(a / (float)angularSegments, t);
                idx++;
            }
        }

        var tris = new int[radialSegments * angularSegments * 6];
        int tIdx = 0;
        for (int r = 0; r < radialSegments; r++)
        {
            for (int a = 0; a < angularSegments; a++)
            {
                int i0 = r * (angularSegments + 1) + a;
                int i1 = i0 + 1;
                int i2 = i0 + (angularSegments + 1);
                int i3 = i2 + 1;

                // Correct winding for upward-facing surface
                tris[tIdx++] = i0; tris[tIdx++] = i1; tris[tIdx++] = i2;
                tris[tIdx++] = i1; tris[tIdx++] = i3; tris[tIdx++] = i2;
            }
        }

        if (mesh == null) mesh = new Mesh();
        else mesh.Clear();

        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;

        // Unity 6: no angle overload; just recalc
        mesh.RecalculateNormals();   // (optional flags overload exists: MeshUpdateFlags.Default)
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}
