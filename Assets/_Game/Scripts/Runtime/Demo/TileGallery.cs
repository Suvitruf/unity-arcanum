using System;
using System.Collections.Generic;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Runtime.Art;
using UnityEngine;

namespace Arcanum.Runtime.Demo
{
    /// <summary>
    /// Standalone test scene — shows one representative (base) tile for every terrain type in the tile-name table
    /// (<c>art/tile/tilename.mes</c>), labelled, as a grid. Lets you eyeball that every terrain resolves and
    /// decodes, and spot any that are missing a base tile. Drop this on a single GameObject in an otherwise empty
    /// scene and press Play: it mounts the data via <see cref="GameDataLocator"/> and self-configures the camera.
    /// Reads from your own legitimate Arcanum install; bundles nothing.
    /// </summary>
    public sealed class TileGallery : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Archive holding tile art + art/tile/tilename.mes (default arcanum2.dat).")]
        [SerializeField]
        private string TileArchive = "arcanum2.dat";

        [Header("Layout")]
        [Range(1, 40)]
        [SerializeField]
        private int Columns = 14;

        [SerializeField]
        private float CellWidth = 1.2f;

        [SerializeField]
        private float CellHeight = 1.0f;

        [SerializeField]
        private float PixelsPerUnit = 100f;

        [SerializeField]
        private Color Background = new Color(0.10f, 0.12f, 0.10f, 1f);

        private DatVirtualFileSystem _vfs;
        private Camera _cam;
        private readonly List<(Vector3 pos, string text)> _labels = new List<(Vector3, string)>();

        private void Start()
        {
            string path = GameDataLocator.Find(TileArchive);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"TileGallery: couldn't locate '{TileArchive}'. Point the data locator at your Arcanum install.");
                return;
            }

            _vfs = new DatVirtualFileSystem();
            _vfs.MountFile(path);

            MesFile mes = MesReader.Read(_vfs.ReadAllBytes("art/tile/tilename.mes"));
            TileNameTable table = TileNameTable.FromMes(mes);

            // Unique terrain names across the four buckets, in table order (outdoor first, then indoor).
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[][] buckets = { table.OutdoorFlippable, table.OutdoorNonFlippable, table.IndoorFlippable, table.IndoorNonFlippable, };
            foreach (string[] bucket in buckets)
                foreach (string bucketName in bucket)
                    if (!string.IsNullOrWhiteSpace(bucketName) && seen.Add(bucketName))
                        names.Add(bucketName);

            int cols = Mathf.Max(1, Columns);
            int rows = Mathf.CeilToInt(names.Count / (float)cols);
            int missing = 0;
            for (int i = 0; i < names.Count; i++)
            {
                Vector3 cell = CellPos(i % cols, i / cols, cols, rows);
                Sprite sprite = LoadBaseTile(names[i]);
                if (sprite == null) missing++;
                PlaceTile(names[i], sprite, cell);
            }

            Debug.Log($"TileGallery: {names.Count} terrain types ({missing} without a base tile).");
            ConfigureCamera(cols, rows);
        }

        // The base, non-blended tile of a terrain (engine "<tileName>bse0a"): edge 0, variant a. See TileArtPathResolver.
        private Sprite LoadBaseTile(string tileName)
        {
            try
            {
                string p = $"art/tile/{tileName}bse0a.art";
                if (!_vfs.Exists(p)) return null;
                ArtFile art = ArtReader.Read(_vfs.ReadAllBytes(p));
                if (art.Rotations.Count == 0 || art.Rotations[0].Frames.Length == 0) return null;
                return ArtTextureFactory.CreateSprite(art.Rotations[0].Frames[0], art.PrimaryPalette, PixelsPerUnit);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"TileGallery: '{tileName}' failed: {ex.Message}");
                return null;
            }
        }

        private Vector3 CellPos(int col, int row, int cols, int rows)
        {
            float x = (col - (cols - 1) * 0.5f) * CellWidth;
            float y = ((rows - 1) * 0.5f - row) * CellHeight;
            return new Vector3(x, y, 0f);
        }

        private void PlaceTile(string tileName, Sprite sprite, Vector3 pos)
        {
            var go = new GameObject($"Tile_{tileName}");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            _labels.Add((pos + Vector3.down * (CellHeight * 0.42f), tileName));
        }

        private void ConfigureCamera(int cols, int rows)
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("GalleryCamera") { tag = "MainCamera" };
                _cam = camGo.AddComponent<Camera>();
            }

            _cam.orthographic = true;
            _cam.transform.position = new Vector3(0f, 0f, -10f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Background;

            float halfW = cols * CellWidth * 0.5f + 0.5f;
            float halfH = rows * CellHeight * 0.5f + 0.5f;
            float aspect = _cam.aspect <= 0f ? 16f / 9f : _cam.aspect;
            _cam.orthographicSize = Mathf.Max(halfH, halfW / aspect);
        }

        // Labels via IMGUI so the scene needs no font asset; projected from each cell's world position.
        private void OnGUI()
        {
            if (_cam == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            style.normal.textColor = Color.white;
            foreach ((Vector3 pos, string text) in _labels)
            {
                Vector3 sp = _cam.WorldToScreenPoint(pos);
                if (sp.z < 0f) continue;
                GUI.Label(new Rect(sp.x - 40f, Screen.height - sp.y - 7f, 80f, 16f), text, style);
            }
        }
    }
}
