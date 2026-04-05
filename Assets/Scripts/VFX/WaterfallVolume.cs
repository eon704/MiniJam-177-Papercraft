using Bitgem.Core;
using Bitgem.VFX.StylisedWater;
using System.Collections.Generic;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Companion to WaterVolumeBase. Generates waterfall quads on selected faces,
    /// auto-syncing colors and normal map from the water material (Bitgem).
    ///
    /// Add this component to the same GameObject as WaterVolumeBox.
    /// Assign WaterfallMaterial (Assets/Art/Materials/Waterfall.mat).
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(WaterVolumeBase))]
    public class WaterfallVolume : MonoBehaviour
    {
        // Bitgem ShaderGraph internal property names (from material serialization)
        private const string PROP_WATER_COLOR = "Color_7D9A58EC";
        private const string PROP_FOAM_COLOR  = "Color_F01C36BF";
        private const string PROP_NORMAL_MAP  = "Texture2D_6490A223";
        private const string PROP_WAVE_SPEED  = "_WaveSpeed";

        // ──────────────────────────────────────────────────────────────────────
        [Header("Waterfall Faces")]
        [FlagEnum]
        public WaterVolumeBase.TileFace WaterfallFaces =
            WaterVolumeBase.TileFace.NegX | WaterVolumeBase.TileFace.PosX |
            WaterVolumeBase.TileFace.NegZ | WaterVolumeBase.TileFace.PosZ;

        [Header("Foam Edges")]
        [Tooltip("Show foam on top edge (water flowing over)")]
        [FlagEnum]
        public WaterVolumeBase.TileFace TopFoam =
            WaterVolumeBase.TileFace.NegX | WaterVolumeBase.TileFace.PosX |
            WaterVolumeBase.TileFace.NegZ | WaterVolumeBase.TileFace.PosZ;

        [Tooltip("Show foam on bottom edge (splash at base)")]
        [FlagEnum]
        public WaterVolumeBase.TileFace BottomFoam;

        // ──────────────────────────────────────────────────────────────────────
        [Header("Mesh Quality")]
        [Range(1, 20)] public int SubdivisionsHeight = 6;
        [Range(1, 10)] public int SubdivisionsWidth  = 1;

        [Tooltip("Outward offset to prevent z-fighting with water side faces")]
        [Range(0, 0.1f)] public float FaceOffset = 0.005f;

        // ──────────────────────────────────────────────────────────────────────
        [Header("Material")]
        public Material WaterfallMaterial;

        [Tooltip("Copy water color, foam color, normal map and flow speed from the water material automatically")]
        public bool AutoSyncFromWater = true;

        [Tooltip("Transparency of water (0 = fully transparent, 1 = opaque). Applied even in AutoSync mode")]
        [Range(0, 1)] public float WaterAlpha = 0.75f;

        // ──────────────────────────────────────────────────────────────────────
        [Header("Visual Settings")]
        [Tooltip("Used for all settings when AutoSync = false; FlowSpeed / NormalStrength / Tiling / Foam always used")]
        public Color WaterColorOverride = new Color(0.039f, 0.603f, 0.933f, 0.75f);
        public Color FoamColorOverride  = new Color(0.8f, 0.95f, 1f, 1f);

        [Range(0, 5)]     public float FlowSpeed      = 1.5f;
        [Range(0, 3)]     public float NormalStrength  = 0.6f;
        [Range(0.1f, 10)] public float TileX           = 1f;
        [Range(0.1f, 10)] public float TileY           = 2f;
        [Range(0, 0.5f)]  public float FoamWidth      = 0.15f;
        [Range(0.1f, 10)] public float FoamNoise      = 2f;
        [Range(0, 1)]     public float FoamThreshold  = 0.15f;
        [Range(0.01f, 0.5f)] public float FoamSharpness = 0.3f;
        [Range(0, 2)]     public float FoamSpeed      = 0.3f;
        [Range(0, 1)]     public float BodyFoam       = 0f;
        [Range(0, 1)]     public float Turbulence     = 0.2f;

        // ──────────────────────────────────────────────────────────────────────
        private GameObject        _child;
        private MeshFilter        _childFilter;
        private MeshRenderer      _childRenderer;
        private Mesh              _mesh;
        private MaterialPropertyBlock _propBlock;
        private bool              _dirty      = true;
        private int               _lastVertCount = -1;

        // ──────────────────────────────────────────────────────────────────────
        void OnValidate() => _dirty = true;
        void OnEnable()   => _dirty = true;

        [ContextMenu("Rebuild Waterfall")]
        public void ForceRebuild() { _dirty = true; Rebuild(); }

        void Update()
        {
            // Detect when WaterVolumeBase rebuilds its mesh
            var wf = GetComponent<MeshFilter>();
            if (wf && wf.sharedMesh != null)
            {
                int vc = wf.sharedMesh.vertexCount;
                if (vc != _lastVertCount)
                {
                    _lastVertCount = vc;
                    _dirty = true;
                }
            }

            if (_dirty)
                Rebuild();
        }

        // ──────────────────────────────────────────────────────────────────────
        public void Rebuild()
        {
            EnsureChild();

            var waterFilter = GetComponent<MeshFilter>();
            if (waterFilter == null || waterFilter.sharedMesh == null || waterFilter.sharedMesh.vertexCount == 0)
                return; // Water not ready yet

            Bounds b = waterFilter.sharedMesh.bounds;
            float x0 = b.min.x, x1 = b.max.x;
            float y0 = b.min.y, y1 = b.max.y;
            float z0 = b.min.z, z1 = b.max.z;

            _mesh.Clear();
            var verts   = new List<Vector3>();
            var norms   = new List<Vector3>();
            var uvs     = new List<Vector2>();
            var colors  = new List<Color>();
            var indices = new List<int>();

            float o = FaceOffset;

            // NegX  (normal = -X)
            if ((WaterfallFaces & WaterVolumeBase.TileFace.NegX) != 0)
                AddFace(verts, norms, uvs, colors, indices,
                    origin: new Vector3(x0 - o, y0, z1),
                    right:  new Vector3(0, 0, -(z1 - z0)),
                    up:     new Vector3(0, y1 - y0, 0),
                    normal: Vector3.left,
                    topFoam: (TopFoam    & WaterVolumeBase.TileFace.NegX) != 0,
                    botFoam: (BottomFoam & WaterVolumeBase.TileFace.NegX) != 0);

            // PosX  (normal = +X)
            if ((WaterfallFaces & WaterVolumeBase.TileFace.PosX) != 0)
                AddFace(verts, norms, uvs, colors, indices,
                    origin: new Vector3(x1 + o, y0, z0),
                    right:  new Vector3(0, 0, z1 - z0),
                    up:     new Vector3(0, y1 - y0, 0),
                    normal: Vector3.right,
                    topFoam: (TopFoam    & WaterVolumeBase.TileFace.PosX) != 0,
                    botFoam: (BottomFoam & WaterVolumeBase.TileFace.PosX) != 0);

            // NegZ  (normal = -Z)
            if ((WaterfallFaces & WaterVolumeBase.TileFace.NegZ) != 0)
                AddFace(verts, norms, uvs, colors, indices,
                    origin: new Vector3(x0, y0, z0 - o),
                    right:  new Vector3(x1 - x0, 0, 0),
                    up:     new Vector3(0, y1 - y0, 0),
                    normal: Vector3.back,
                    topFoam: (TopFoam    & WaterVolumeBase.TileFace.NegZ) != 0,
                    botFoam: (BottomFoam & WaterVolumeBase.TileFace.NegZ) != 0);

            // PosZ  (normal = +Z)
            if ((WaterfallFaces & WaterVolumeBase.TileFace.PosZ) != 0)
                AddFace(verts, norms, uvs, colors, indices,
                    origin: new Vector3(x1, y0, z1 + o),
                    right:  new Vector3(-(x1 - x0), 0, 0),
                    up:     new Vector3(0, y1 - y0, 0),
                    normal: Vector3.forward,
                    topFoam: (TopFoam    & WaterVolumeBase.TileFace.PosZ) != 0,
                    botFoam: (BottomFoam & WaterVolumeBase.TileFace.PosZ) != 0);

            _mesh.SetVertices(verts);
            _mesh.SetNormals(norms);
            _mesh.SetUVs(0, uvs);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(indices, 0);
            _mesh.RecalculateBounds();
            _mesh.RecalculateTangents();
            _childFilter.sharedMesh = _mesh;

            SyncMaterial();
            _dirty = false;
        }

        // ──────────────────────────────────────────────────────────────────────
        private void AddFace(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
            List<Color> colors, List<int> idx,
            Vector3 origin, Vector3 right, Vector3 up, Vector3 normal,
            bool topFoam, bool botFoam)
        {
            int vx   = SubdivisionsWidth  + 1;
            int vy   = SubdivisionsHeight + 1;
            int base0 = verts.Count;

            for (int iy = 0; iy < vy; iy++)
            {
                float ty = (float)iy / SubdivisionsHeight;
                for (int ix = 0; ix < vx; ix++)
                {
                    float tx = (float)ix / SubdivisionsWidth;
                    verts.Add(origin + right * tx + up * ty);
                    norms.Add(normal);
                    uvs.Add(new Vector2(tx, ty));
                    float r = (iy == SubdivisionsHeight && topFoam) ? 1f : 0f;
                    float g = (iy == 0                 && botFoam) ? 1f : 0f;
                    colors.Add(new Color(r, g, 0f, 1f));
                }
            }

            for (int iy = 0; iy < SubdivisionsHeight; iy++)
            {
                for (int ix = 0; ix < SubdivisionsWidth; ix++)
                {
                    int bl = base0 + iy * vx + ix;
                    int br = bl + 1;
                    int tl = bl + vx;
                    int tr = tl + 1;
                    idx.Add(bl); idx.Add(tl); idx.Add(br);
                    idx.Add(br); idx.Add(tl); idx.Add(tr);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        private void SyncMaterial()
        {
            if (!_childRenderer || !WaterfallMaterial)
                return;

            _childRenderer.sharedMaterial = WaterfallMaterial;

            if (_propBlock == null)
                _propBlock = new MaterialPropertyBlock();
            _childRenderer.GetPropertyBlock(_propBlock);

            // Sync colors / texture from Bitgem water material
            var wRend = GetComponent<MeshRenderer>();
            var wMat  = wRend != null ? wRend.sharedMaterial : null;

            if (AutoSyncFromWater && wMat != null)
            {
                if (wMat.HasProperty(PROP_WATER_COLOR))
                {
                    Color c = wMat.GetColor(PROP_WATER_COLOR);
                    c.a = WaterAlpha;
                    _propBlock.SetColor("_WaterColor", c);
                }
                if (wMat.HasProperty(PROP_FOAM_COLOR))
                    _propBlock.SetColor("_FoamColor", wMat.GetColor(PROP_FOAM_COLOR));
                if (wMat.HasProperty(PROP_NORMAL_MAP))
                {
                    var tex = wMat.GetTexture(PROP_NORMAL_MAP);
                    if (tex != null)
                        _propBlock.SetTexture("_MainTex", tex);
                }
                if (wMat.HasProperty(PROP_WAVE_SPEED))
                    _propBlock.SetFloat("_FlowSpeed", wMat.GetFloat(PROP_WAVE_SPEED));
                else
                    _propBlock.SetFloat("_FlowSpeed", FlowSpeed);
            }
            else
            {
                Color c = WaterColorOverride;
                c.a = WaterAlpha;
                _propBlock.SetColor("_WaterColor", c);
                _propBlock.SetColor("_FoamColor",  FoamColorOverride);
                _propBlock.SetFloat("_FlowSpeed",  FlowSpeed);
            }

            // These settings always come from the component
            _propBlock.SetFloat("_NormalStrength", NormalStrength);
            _propBlock.SetFloat("_TileX",          TileX);
            _propBlock.SetFloat("_TileY",          TileY);
            _propBlock.SetFloat("_FoamWidth",     FoamWidth);
            _propBlock.SetFloat("_FoamNoise",     FoamNoise);
            _propBlock.SetFloat("_FoamThreshold", FoamThreshold);
            _propBlock.SetFloat("_FoamSharpness", FoamSharpness);
            _propBlock.SetFloat("_FoamSpeed",     FoamSpeed);
            _propBlock.SetFloat("_BodyFoam",      BodyFoam);
            _propBlock.SetFloat("_Turbulence",    Turbulence);

            _childRenderer.SetPropertyBlock(_propBlock);
        }

        // ──────────────────────────────────────────────────────────────────────
        private void EnsureChild()
        {
            // Use Unity's ! operator (not == null) to catch fake-null destroyed objects
            if (!_child)
            {
                _child = new GameObject("_WaterfallMesh");
                _child.hideFlags = HideFlags.DontSave;
                _child.transform.SetParent(transform, false);
                _childFilter   = null;
                _childRenderer = null;
                _mesh          = null;
            }

            if (!_childFilter)
            {
                var f = _child.GetComponent<MeshFilter>();
                _childFilter = f ? f : _child.AddComponent<MeshFilter>();
            }

            if (!_childRenderer)
            {
                var r = _child.GetComponent<MeshRenderer>();
                _childRenderer = r ? r : _child.AddComponent<MeshRenderer>();
            }

            if (!_mesh)
                _mesh = new Mesh { name = "WaterfallVolumeMesh-" + gameObject.GetInstanceID() };
        }

        void OnDestroy()
        {
            if (_child != null)
                DestroyImmediate(_child);
        }
    }
}
