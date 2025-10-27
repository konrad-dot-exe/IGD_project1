using UnityEngine;
using UnityEngine.Rendering; // IndexFormat
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralGround : MonoBehaviour
{
    [SerializeField] float radius = 2500f;
    [SerializeField] int radialSegments = 140;
    [SerializeField] int angularSegments = 220;
    [SerializeField] float noiseScale = 0.01f;
    [SerializeField] float height = 18f;
    [SerializeField] float flatRadius = 22f;

    Mesh _mesh;
    MeshFilter _mf;

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
    }

    void OnEnable()
    {
        // Runtime path: build and assign to the *instance* mesh to avoid editor-time hazards.
        BuildOrRebuildMesh();
        if (_mf) _mf.mesh = _mesh; // use instance at runtime
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Editor path: schedule after validation to avoid "SendMessage during OnValidate" issues.
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;           // object could have been deleted
            if (!gameObject) return;
            if (!_mf) _mf = GetComponent<MeshFilter>();

            BuildOrRebuildMesh();
            if (_mf) _mf.sharedMesh = _mesh;    // assign sharedMesh in editor for scene persistence
        };
    }
#endif

    void OnDisable()
    {
        // If you want the mesh to live across disable/enable, remove this cleanup.
        // Otherwise, dispose to keep things tidy (and prevent double frees).
        DisposeMesh();
    }

    void OnDestroy()
    {
        DisposeMesh();
    }

    void DisposeMesh()
    {
        if (_mesh == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(_mesh);
        else
            Destroy(_mesh);
#else
        Destroy(_mesh);
#endif
        _mesh = null;
    }

    // --- Builds/updates the generated mesh. Does NOT write to the MeshFilter. ---
    void BuildOrRebuildMesh()
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

                // Upward-facing winding
                tris[tIdx++] = i0; tris[tIdx++] = i1; tris[tIdx++] = i2;
                tris[tIdx++] = i1; tris[tIdx++] = i3; tris[tIdx++] = i2;
            }
        }

        if (_mesh == null)
        {
            _mesh = new Mesh
            {
#if UNITY_EDITOR
                name = "ProceduralGround (generated)"
#endif
            };
            // Prevent accidental asset save in editor
#if UNITY_EDITOR
            _mesh.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
#endif
        }
        else
        {
            _mesh.Clear();
        }

        _mesh.indexFormat = IndexFormat.UInt32;
        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris;

        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }
}
