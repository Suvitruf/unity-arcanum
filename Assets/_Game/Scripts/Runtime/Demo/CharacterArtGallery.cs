using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Runtime.Art;
using Arcanum.Runtime.Character;
using UnityEngine;

namespace Arcanum.Runtime.Demo
{
    /// <summary>
    /// Standalone test scene — renders every player race × gender as a labelled grid of STAND critter sprites,
    /// to eyeball the paper-doll composition and the documented fallbacks (female critter art exists only for the
    /// HUMAN body, so a non-human female falls back to the race's male body; the small races share the halfling
    /// body). Drop this on a single GameObject in an otherwise empty scene and press Play: it mounts the data via
    /// <see cref="GameDataLocator"/> and self-configures the camera. Reads from your own legitimate Arcanum
    /// install; bundles nothing.
    /// </summary>
    public sealed class CharacterArtGallery : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Archive holding critter art (default arcanum1.dat).")]
        [SerializeField]
        private string CritterArchive = "arcanum1.dat";

        [Header("Pose / paper-doll")]
        [Range(0, 7)]
        [SerializeField]
        private int Rotation = 0; // starting facing (it then turns through all 8)

        [Range(0.1f, 3f)]
        [SerializeField]
        private float SecondsPerFacing = 0.6f; // turntable speed — how long each facing is shown

        [Range(0, 8)]
        [SerializeField]
        private int Armor = 0; // 0 = UW base body (shows race/gender clearly); 2 = leather, …

        [Range(0, 14)]
        [SerializeField]
        private int Weapon = 0; // 0 = unarmed

        [SerializeField]
        private float PixelsPerUnit = 100f;

        [Header("Layout")]
        [SerializeField]
        private float CellWidth = 1.6f;

        [SerializeField]
        private float CellHeight = 2.4f;

        [SerializeField]
        private float LabelGap = 0.15f; // gap between the sprite's bottom and its label (world units)

        [SerializeField]
        private Color Background = new Color(0.10f, 0.10f, 0.12f, 1f);

        private static readonly Race[] AllRaces = { Race.Human, Race.Dwarf, Race.Elf, Race.HalfElf, Race.Gnome, Race.Halfling, Race.HalfOrc, Race.HalfOgre, };

        private DatVirtualFileSystem _vfs;
        private Camera _cam;

        private readonly System.Collections.Generic.List<(Vector3 pos, string text)> _labels =
            new System.Collections.Generic.List<(Vector3, string)>();

        private void Start()
        {
            string path = GameDataLocator.Find(CritterArchive);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"CharacterArtGallery: couldn't locate '{CritterArchive}'. Point the data locator at your Arcanum install.");
                return;
            }

            _vfs = new DatVirtualFileSystem();
            _vfs.MountFile(path);
            var resolver = CritterArtResolver.Create();

            int cols = AllRaces.Length; // one column per race; row 0 = female, row 1 = male
            for (int male = 0; male <= 1; male++)
                for (int c = 0; c < cols; c++)
                {
                    Race race = AllRaces[c];
                    Vector3 cell = CellPos(c, male, cols);
                    PlaceCritter(resolver, BuildArtId(race, male == 1), cell, $"{race} {(male == 1 ? "M" : "F")}");
                }

            ConfigureCamera(cols);
        }

        // Critter art-id bit layout (engine tig_art_critter_id_create): type 28–31, gender 27 (1 = male),
        // body 24–26, armour 20–23, weapon 0–3. Anim defaults to 0 (STAND); rotation is selected at read time.
        private uint BuildArtId(Race race, bool male) =>
            ((uint)ArtId.TypeCritter << 28)
            | ((uint)(male ? 1 : 0) << 27)
            | ((uint)RaceArt.BodyType(race) << 24)
            | ((uint)(Armor & 0xF) << 20)
            | (uint)(Weapon & 0xF);

        private Vector3 CellPos(int col, int row, int cols)
        {
            float x = (col - (cols - 1) * 0.5f) * CellWidth;
            float y = (0.5f - row) * CellHeight; // row 0 (female) above, row 1 (male) below
            return new Vector3(x, y, 0f);
        }

        private void PlaceCritter(CritterArtResolver resolver, uint artId, Vector3 pos, string label)
        {
            var go = new GameObject($"Critter_{artId:X8}");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();

            (Sprite[][] rotations, int fps) = LoadRotations(resolver, artId);
            if (rotations == null)
            {
                Debug.LogWarning($"CharacterArtGallery: no art for artId {artId:X8} (path '{resolver.Resolve(artId)}').");
                _labels.Add((pos + Vector3.down * 0.5f, label)); // still label the empty cell
                return;
            }

            go.AddComponent<CritterTurntable>().Init(rotations, fps, SecondsPerFacing, Rotation);

            // Anchor the label just under the sprite's actual rendered bottom (+ a small gap), not a big fixed offset.
            float bottom = sr.sprite != null ? sr.bounds.min.y : pos.y;
            _labels.Add((new Vector3(pos.x, bottom - LabelGap, 0f), label));
        }

        // Decodes every facing into a per-rotation array of frame sprites, plus the art's fps. The reader already
        // expands the 5-of-8 critter mirror into 8 rotations, so art.Rotations[d] is facing d directly (no flip).
        private (Sprite[][] rotations, int fps) LoadRotations(CritterArtResolver resolver, uint artId)
        {
            try
            {
                string p = resolver.Resolve(artId);
                if (string.IsNullOrEmpty(p) || !_vfs.Exists(p)) return (null, 0);
                ArtFile art = ArtReader.Read(_vfs.ReadAllBytes(p));
                var rotations = new Sprite[art.Rotations.Count][];
                for (int r = 0; r < art.Rotations.Count; r++)
                {
                    ArtFrame[] frames = art.Rotations[r].Frames;
                    var sprites = new Sprite[frames.Length];
                    for (int f = 0; f < frames.Length; f++)
                        sprites[f] = ArtTextureFactory.CreateSprite(frames[f], art.PrimaryPalette, PixelsPerUnit);
                    rotations[r] = sprites;
                }

                return (rotations, art.Fps);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"CharacterArtGallery: decode failed for {artId:X8}: {ex.Message}");
                return (null, 0);
            }
        }

        private void ConfigureCamera(int cols)
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
            float halfH = CellHeight + 0.6f; // two rows
            float aspect = _cam.aspect <= 0f ? 16f / 9f : _cam.aspect;
            _cam.orthographicSize = Mathf.Max(halfH, halfW / aspect);
        }

        // Labels via IMGUI so the scene needs no font asset; projected from each cell's world position.
        private void OnGUI()
        {
            if (_cam == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            style.normal.textColor = Color.white;
            foreach ((Vector3 pos, string text) in _labels)
            {
                Vector3 sp = _cam.WorldToScreenPoint(pos);
                if (sp.z < 0f) continue;
                GUI.Label(new Rect(sp.x - 60f, Screen.height - sp.y - 8f, 120f, 18f), text, style);
            }
        }
    }

    /// <summary>Plays a critter's idle frames and slowly turns it through all 8 facings (a turntable), for the
    /// <see cref="CharacterArtGallery"/>. Each facing is a pre-decoded frame array; <c>fps</c> is the art's frame
    /// rate (0 = a static pose). Single-rotation art just animates in place.</summary>
    public sealed class CritterTurntable : MonoBehaviour
    {
        private Sprite[][] _rotations;
        private float _frameDur;  // seconds per animation frame (1/fps)
        private float _facingDur; // seconds per facing
        private SpriteRenderer _sr;
        private int _rot, _frame;
        private float _frameTimer, _facingTimer;

        public void Init(Sprite[][] rotations, int fps, float secondsPerFacing, int startRotation)
        {
            _rotations = rotations;
            _frameDur = fps > 0 ? 1f / fps : 0f;
            _facingDur = Mathf.Max(0.05f, secondsPerFacing);
            _sr = GetComponent<SpriteRenderer>();
            int n = rotations?.Length ?? 0;
            _rot = n > 0 ? ((startRotation % n) + n) % n : 0;
            Show();
        }

        private void Update()
        {
            if (_rotations == null || _rotations.Length == 0) return;

            if (_frameDur > 0f) // idle frame animation within the current facing
            {
                _frameTimer += Time.deltaTime;
                while (_frameTimer >= _frameDur)
                {
                    _frameTimer -= _frameDur;
                    _frame++;
                    Show();
                }
            }

            if (_rotations.Length > 1) // turntable — step to the next facing
            {
                _facingTimer += Time.deltaTime;
                if (_facingTimer >= _facingDur)
                {
                    _facingTimer -= _facingDur;
                    _rot = (_rot + 1) % _rotations.Length;
                    _frame = 0;
                    Show();
                }
            }
        }

        private void Show()
        {
            Sprite[] frames = _rotations[_rot];
            if (frames != null && frames.Length > 0) _sr.sprite = frames[_frame % frames.Length];
        }
    }
}
