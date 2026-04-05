using Bitgem.Core;
using Bitgem.VFX.StylisedWater;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Activates splash particle systems at manually placed points,
    /// only on the sides where WaterfallVolume has active waterfall faces
    /// AND where no adjacent board cell exists (open edge only).
    ///
    /// Setup:
    ///   1. Add to the same GameObject as WaterVolumeBox + WaterfallVolume.
    ///   2. Place 4 empty GameObjects at the desired steam positions (one per side).
    ///   3. Assign them to NegX/PosX/NegZ/PosZ Point fields.
    ///   4. Assign SplashPrefab.
    ///   5. The component auto-detects neighbor cells via CellPrefab + BoardPrefab.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(WaterfallVolume))]
    public class WaterfallSplash : MonoBehaviour
    {
        [Header("Splash Prefab")]
        public ParticleSystem SplashPrefab;

        [Header("Spawn Points (place manually at ground level)")]
        public Transform PointNegX;
        public Transform PointPosX;
        public Transform PointNegZ;
        public Transform PointPosZ;

        // ── Private ──────────────────────────────────────────────────────────
        private ParticleSystem _psNegX;
        private ParticleSystem _psPosX;
        private ParticleSystem _psNegZ;
        private ParticleSystem _psPosZ;

        private bool _dirty = true;

        void OnValidate() => _dirty = true;
        void OnEnable()   => _dirty = true;

        [ContextMenu("Rebuild Splash")]
        public void ForceRebuild() { _dirty = true; Rebuild(); }

        void Update()
        {
            if (_dirty) Rebuild();
        }

        public void Rebuild()
        {
            if (!SplashPrefab) { _dirty = false; return; }

            var wv = GetComponent<WaterfallVolume>();
            if (!wv) { _dirty = false; return; }

            // Neighbor detection via board model (accounts for random cell rotation)
            var cellPrefab  = GetComponentInParent<CellPrefab>();
            var boardPrefab = Object.FindObjectOfType<BoardPrefab>();

            UpdateFace(WaterVolumeBase.TileFace.NegX, PointNegX, ref _psNegX,
                       extraYRot: 90f,
                       neighborExists: HasNeighbor(cellPrefab, boardPrefab, WaterVolumeBase.TileFace.NegX));

            UpdateFace(WaterVolumeBase.TileFace.PosX, PointPosX, ref _psPosX,
                       extraYRot: 90f,
                       neighborExists: HasNeighbor(cellPrefab, boardPrefab, WaterVolumeBase.TileFace.PosX));

            UpdateFace(WaterVolumeBase.TileFace.NegZ, PointNegZ, ref _psNegZ,
                       extraYRot: 0f,
                       neighborExists: HasNeighbor(cellPrefab, boardPrefab, WaterVolumeBase.TileFace.NegZ));

            UpdateFace(WaterVolumeBase.TileFace.PosZ, PointPosZ, ref _psPosZ,
                       extraYRot: 0f,
                       neighborExists: HasNeighbor(cellPrefab, boardPrefab, WaterVolumeBase.TileFace.PosZ));

            _dirty = false;
        }

        private void UpdateFace(WaterVolumeBase.TileFace flag, Transform point, ref ParticleSystem ps,
                                 float extraYRot, bool neighborExists)
        {
            var wv    = GetComponent<WaterfallVolume>();
            bool active = (wv.WaterfallFaces & flag) != 0 && point != null && !neighborExists;

            if (!active)
            {
                if (ps) ps.gameObject.SetActive(false);
                return;
            }

            Quaternion rot = point.rotation * Quaternion.Euler(0f, extraYRot, 0f);

            // Create instance if needed
            if (!ps)
            {
                var go = Instantiate(SplashPrefab.gameObject, point.position, rot,
                                     transform.parent != null ? transform.parent : transform);
                go.name = $"_SplashPS_{flag}_{gameObject.name}";
                ps      = go.GetComponent<ParticleSystem>();
            }

            ps.gameObject.SetActive(true);
            ps.transform.position = point.position;
            ps.transform.rotation = rot;

            if (!ps.isPlaying) ps.Play();
        }

        // Returns true if a non-empty board cell exists in the world direction of this face.
        // Accounts for the cell's random Y-rotation by transforming the local face direction to world space.
        private bool HasNeighbor(CellPrefab cellPrefab, BoardPrefab boardPrefab, WaterVolumeBase.TileFace face)
        {
            if (cellPrefab == null || boardPrefab == null || cellPrefab.Cell == null)
                return false;

            Vector2Int offset = FaceToWorldBoardOffset(face);
            var neighbor = boardPrefab.Board?.GetCell(cellPrefab.Cell.Position + offset);
            return neighbor != null && neighbor.Terrain != TerrainType.Empty;
        }

        // Converts a TileFace (local-space direction) to a board grid offset in world space,
        // accounting for the transform's rotation.
        private Vector2Int FaceToWorldBoardOffset(WaterVolumeBase.TileFace face)
        {
            Vector3 localDir = face switch
            {
                WaterVolumeBase.TileFace.NegX => Vector3.left,
                WaterVolumeBase.TileFace.PosX => Vector3.right,
                WaterVolumeBase.TileFace.NegZ => Vector3.back,
                WaterVolumeBase.TileFace.PosZ => Vector3.forward,
                _                             => Vector3.zero
            };
            // TransformDirection rotates by world rotation, ignoring scale/position
            Vector3 worldDir = transform.TransformDirection(localDir);
            // Board X = world X, Board Y = world Z
            return new Vector2Int(Mathf.RoundToInt(worldDir.x), Mathf.RoundToInt(worldDir.z));
        }

        void OnDestroy()
        {
            DestroyPS(ref _psNegX);
            DestroyPS(ref _psPosX);
            DestroyPS(ref _psNegZ);
            DestroyPS(ref _psPosZ);
        }

        private void DestroyPS(ref ParticleSystem ps)
        {
            if (ps) DestroyImmediate(ps.gameObject);
            ps = null;
        }

        void OnDrawGizmosSelected()
        {
            DrawPoint(PointNegX, "NegX");
            DrawPoint(PointPosX, "PosX");
            DrawPoint(PointNegZ, "NegZ");
            DrawPoint(PointPosZ, "PosZ");
        }

        private void DrawPoint(Transform t, string label)
        {
            if (!t) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(t.position, 0.1f);
            Gizmos.DrawLine(t.position, t.position + t.up * 0.3f);
        }
    }
}
