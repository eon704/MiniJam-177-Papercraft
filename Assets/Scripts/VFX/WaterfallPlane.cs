using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Generates a vertical quad mesh for a waterfall effect.
    /// Works alongside the Bitgem water volume system — place on vertical faces
    /// adjacent to WaterVolumeBox/WaterVolumeTransforms objects.
    ///
    /// Vertex colors encode foam edges:
    ///   R channel = top edge foam
    ///   G channel = bottom edge foam
    /// Leave both at 0 to show foam on both top and bottom automatically.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class WaterfallPlane : MonoBehaviour
    {
        public enum FoamEdge { Both, TopOnly, BottomOnly, None }

        [Header("Dimensions")]
        [Min(0.01f)] public float Width  = 1f;
        [Min(0.01f)] public float Height = 1f;

        [Header("Subdivision (for smoother normals)")]
        [Range(1, 20)] public int SubdivisionsX = 1;
        [Range(1, 20)] public int SubdivisionsY = 4;

        [Header("Foam")]
        public FoamEdge Foam = FoamEdge.Both;

        [Header("Facing")]
        public bool FlipNormal = false;

        private Mesh        _mesh;
        private MeshFilter  _filter;
        private bool        _dirty = true;

#if UNITY_EDITOR
        private float _prevWidth;
        private float _prevHeight;
        private int   _prevSubX;
        private int   _prevSubY;
        private FoamEdge _prevFoam;
        private bool  _prevFlip;
#endif

        void OnValidate()
        {
            _dirty = true;
        }

        void OnEnable()
        {
            _dirty = true;
        }

        void Update()
        {
#if UNITY_EDITOR
            // Detect inspector changes in edit mode
            if (!Application.isPlaying)
            {
                if (Width != _prevWidth || Height != _prevHeight ||
                    SubdivisionsX != _prevSubX || SubdivisionsY != _prevSubY ||
                    Foam != _prevFoam || FlipNormal != _prevFlip)
                {
                    _dirty = true;
                }
            }
#endif
            if (_dirty)
                Rebuild();
        }

        public void Rebuild()
        {
            EnsureComponents();

            _mesh.Clear();

            int vertsX = SubdivisionsX + 1;
            int vertsY = SubdivisionsY + 1;
            int vertCount = vertsX * vertsY;

            var vertices = new Vector3[vertCount];
            var normals  = new Vector3[vertCount];
            var uvs      = new Vector2[vertCount];
            var colors   = new Color[vertCount];

            float stepX = Width  / SubdivisionsX;
            float stepY = Height / SubdivisionsY;
            float halfW = Width  * 0.5f;

            // Normal points in -Z by default (facing away from volume)
            Vector3 normal = FlipNormal ? Vector3.forward : Vector3.back;

            for (int iy = 0; iy < vertsY; iy++)
            {
                float y  = iy * stepY;
                float uv_y = (float)iy / SubdivisionsY; // 0 = bottom, 1 = top

                for (int ix = 0; ix < vertsX; ix++)
                {
                    float x   = ix * stepX - halfW;
                    float uv_x = (float)ix / SubdivisionsX;

                    int idx = iy * vertsX + ix;
                    vertices[idx] = new Vector3(x, y, 0f);
                    normals[idx]  = normal;
                    uvs[idx]      = new Vector2(uv_x, uv_y);

                    // Vertex color foam encoding
                    float rTop    = 0f;
                    float gBottom = 0f;
                    switch (Foam)
                    {
                        case FoamEdge.Both:
                            rTop = 1f; gBottom = 1f;
                            break;
                        case FoamEdge.TopOnly:
                            rTop = 1f; gBottom = 0f;
                            break;
                        case FoamEdge.BottomOnly:
                            rTop = 0f; gBottom = 1f;
                            break;
                        case FoamEdge.None:
                            rTop = 0f; gBottom = 0f;
                            break;
                    }
                    // Only mark foam verts at the actual edges
                    float rFinal = (iy == SubdivisionsY) ? rTop    : 0f;
                    float gFinal = (iy == 0)             ? gBottom : 0f;
                    colors[idx] = new Color(rFinal, gFinal, 0f, 1f);
                }
            }

            // Build triangles
            int triCount = SubdivisionsX * SubdivisionsY * 6;
            int[] tris = new int[triCount];
            int t = 0;
            for (int iy = 0; iy < SubdivisionsY; iy++)
            {
                for (int ix = 0; ix < SubdivisionsX; ix++)
                {
                    int bl = iy * vertsX + ix;
                    int br = bl + 1;
                    int tl = bl + vertsX;
                    int tr = tl + 1;

                    if (FlipNormal)
                    {
                        tris[t++] = bl; tris[t++] = tl; tris[t++] = br;
                        tris[t++] = br; tris[t++] = tl; tris[t++] = tr;
                    }
                    else
                    {
                        tris[t++] = bl; tris[t++] = br; tris[t++] = tl;
                        tris[t++] = br; tris[t++] = tr; tris[t++] = tl;
                    }
                }
            }

            _mesh.SetVertices(vertices);
            _mesh.SetNormals(normals);
            _mesh.SetUVs(0, uvs);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();
            _mesh.RecalculateTangents();

            _filter.sharedMesh = _mesh;
            _dirty = false;

#if UNITY_EDITOR
            _prevWidth  = Width;
            _prevHeight = Height;
            _prevSubX   = SubdivisionsX;
            _prevSubY   = SubdivisionsY;
            _prevFoam   = Foam;
            _prevFlip   = FlipNormal;
#endif
        }

        private void EnsureComponents()
        {
            if (_filter == null)
            {
                _filter = GetComponent<MeshFilter>();
                _mesh   = null;
            }
            if (_mesh == null)
            {
                _mesh = _filter.sharedMesh;
                if (_mesh == null || _mesh.name != "Waterfall-" + gameObject.GetInstanceID())
                {
                    _mesh = new Mesh { name = "Waterfall-" + gameObject.GetInstanceID() };
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(new Vector3(0, Height * 0.5f, 0), new Vector3(Width, Height, 0.02f));
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawWireCube(new Vector3(0, Height * 0.5f, 0), new Vector3(Width, Height, 0.02f));
        }
    }
}
