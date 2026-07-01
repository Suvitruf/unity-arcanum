using Arcanum.Formats.Art;    // FacadeArtResolver
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime;        // GameDataLocator
using UnityEngine;

namespace Arcanum.World.Demo
{
    /// <summary>
    /// Standalone test scene for the terrain/tile generator. Loads one real Arcanum sector and renders it through
    /// the exact in-game <see cref="TileMapRenderer"/> — the same batched, blended, mirror-edged terrain mesh the
    /// full game builds — so you can eyeball that tiles resolve, blends route, and the mesh stitches correctly,
    /// without booting the rest of the game.
    /// <para>
    /// Drop this on a single GameObject in an otherwise empty scene and press Play. It mounts your own legitimate
    /// Arcanum install via <see cref="GameDataLocator"/> and bundles nothing. Drag to pan, scroll to zoom.
    /// </para>
    /// </summary>
    public sealed class TileMapDemo : MonoBehaviour
    {
        [Header("Source archives (auto-located if left at defaults)")]
        [Tooltip("Archive with tile art + art/tile/tilename.mes + art/facade/facadename.mes (default arcanum2.dat).")]
        [SerializeField]
        private string AssetsArchive = "arcanum2.dat";

        [Tooltip("Module archive holding the sectors (default modules/Arcanum.dat).")]
        [SerializeField]
        private string ModuleArchive = "modules/Arcanum.dat";

        [Tooltip("Sector to render, as a path inside the module archive (a real .sec under maps/<name>/).")]
        [SerializeField]
        private string SectorPath = "maps/arcanum1-024-fixed/101602821844.sec";

        [Header("Rendering")]
        [Tooltip("One batched mesh per sector (in-game path). Off = one SpriteRenderer per tile (heavy fallback).")]
        [SerializeField]
        private bool BatchTerrain = true;

        [SerializeField]
        private float PixelsPerUnit = 100f;

        [SerializeField]
        private Color Background = new Color(0.16f, 0.17f, 0.20f, 1f);

        private DatVirtualFileSystem _vfs;
        private TileMapRenderer _tileMap;
        private Camera _cam;
        private float _zoom = 1f;
        private Vector3 _dragOrigin;

        /// <summary>The module archive sectors are read from (for editor tooling like the Sector Browser).</summary>
        public string ModuleArchiveName => ModuleArchive;

        /// <summary>The sector currently selected/rendered.</summary>
        public string CurrentSector => SectorPath;

        private void Start()
        {
            if (EnsureData()) RenderCurrentSector();
        }

        /// <summary>
        /// Selects and renders a different sector at runtime, tearing down the current one. Called by the editor
        /// Sector Browser's per-row Load button. No-op with an error log if the data can't be mounted.
        /// </summary>
        public void LoadSector(string sectorPath)
        {
            if (!string.IsNullOrEmpty(sectorPath)) SectorPath = sectorPath;
            if (EnsureData()) RenderCurrentSector();
        }

        // Locates + mounts the archives and builds the resolvers once; cached across reloads. False on failure.
        private bool EnsureData()
        {
            if (_tileMap != null) return true;

            string assetsPath = GameDataLocator.Find(AssetsArchive);
            string modulePath = GameDataLocator.Find(ModuleArchive);
            if (string.IsNullOrEmpty(assetsPath) || string.IsNullOrEmpty(modulePath))
            {
                Debug.LogError($"TileMapDemo: could not locate '{AssetsArchive}' and/or '{ModuleArchive}'. " +
                               "Point the data locator at your Arcanum install (or copy the .dat files into <project>/GameData/).", this);
                return false;
            }
            Debug.Log($"TileMapDemo: assets='{assetsPath}', module='{modulePath}'.", this);

            _vfs = new DatVirtualFileSystem();
            _vfs.MountFile(modulePath);  // sectors
            _vfs.MountFile(assetsPath);  // tile art + .mes tables

            TileArtPathResolver tileResolver =
                TileArtPathResolver.FromMes(MesReader.Read(_vfs.ReadAllBytes("art/tile/tilename.mes")));
            FacadeArtResolver facades = _vfs.Exists("art/facade/facadename.mes")
                ? FacadeArtResolver.FromMes(MesReader.Read(_vfs.ReadAllBytes("art/facade/facadename.mes")))
                : null;
            _tileMap = new TileMapRenderer(_vfs, tileResolver, facades, PixelsPerUnit);
            return true;
        }

        // Tears down any previously rendered geometry and renders the current SectorPath, then frames the camera.
        private void RenderCurrentSector()
        {
            if (_tileMap == null) return;

            if (!_vfs.Exists(SectorPath))
            {
                Debug.LogError($"TileMapDemo: sector '{SectorPath}' not found in '{ModuleArchive}'.", this);
                return;
            }

            SectorTerrain terrain;
            try { terrain = SectorReader.ReadTerrain(_vfs.ReadAllBytes(SectorPath)); }
            catch (System.Exception ex) { Debug.LogError($"TileMapDemo: terrain read failed: {ex.Message}", this); return; }

            ClearRendered();
            // Render at the world origin (the global tile offset only matters when stitching adjacent sectors),
            // so keep the host object's transform identity — the batched mesh positions its own quads.
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            _tileMap.RenderSector(terrain, 0, 0, transform, DefaultSpriteMaterial(), sortingOrder: 0, batch: BatchTerrain,
                placeTile: PlaceTile);

            Debug.Log($"TileMapDemo: rendered '{SectorPath}' ({SectorTerrain.Size}×{SectorTerrain.Size} tiles, " +
                      $"{_tileMap.BlendMisses} cumulative blend miss(es)).", this);
            ConfigureCamera();
        }

        // Destroys previously rendered children (terrain mesh / per-tile sprites) before a reload.
        private void ClearRendered()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // The material an actual SpriteRenderer uses — under URP's 2D renderer this is the correct default sprite
        // material (a raw "Sprites/Default" from Shader.Find can render black in a 2D pipeline). Matches the game.
        private static Material DefaultSpriteMaterial()
        {
            var probe = new GameObject("~spriteMatProbe") { hideFlags = HideFlags.HideAndDontSave };
            Material mat = probe.AddComponent<SpriteRenderer>().sharedMaterial;
            Destroy(probe);
            return mat;
        }

        // Per-tile placement for the non-batched fallback path; a plain sprite at a per-tile depth.
        private SpriteRenderer PlaceTile(string name, Transform parent, Sprite sprite, Vector3 localPos, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private void ConfigureCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("DemoCamera") { tag = "MainCamera" };
                _cam = camGo.AddComponent<Camera>();
            }

            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Background;
            _cam.transform.rotation = Quaternion.identity;
            _cam.farClipPlane = Mathf.Max(_cam.farClipPlane, 100f);

            // Frame the ACTUAL rendered geometry (world-space renderer bounds), so framing can't drift from where
            // the mesh landed. If nothing rendered, say so loudly instead of leaving a silent black screen.
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError("TileMapDemo: nothing was rendered (0 renderers under this object). The terrain " +
                               "read/resolve produced no geometry — check the logs above for data-path or sector errors.", this);
                return;
            }

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

            _cam.transform.position = new Vector3(b.center.x, b.center.y, -10f);
            float aspect = _cam.aspect <= 0f ? 16f / 9f : _cam.aspect;
            _zoom = Mathf.Max(b.extents.y + 1f, (b.extents.x + 1f) / aspect);
            _cam.orthographicSize = _zoom;
            Debug.Log($"TileMapDemo: framed {renderers.Length} renderer(s); bounds center {b.center}, size {b.size}.", this);
        }

        private void Update()
        {
            if (_cam == null) return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _zoom = Mathf.Clamp(_zoom * (1f - scroll * 0.1f), 1f, 200f);
                _cam.orthographicSize = _zoom;
            }

            if (Input.GetMouseButtonDown(0)) _dragOrigin = _cam.ScreenToWorldPoint(Input.mousePosition);
            else if (Input.GetMouseButton(0))
            {
                Vector3 now = _cam.ScreenToWorldPoint(Input.mousePosition);
                _cam.transform.position += _dragOrigin - now;
            }
        }

        private void OnDestroy() => _vfs?.Dispose();
    }
}
