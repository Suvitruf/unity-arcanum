using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>
    /// The isometric tile→screen projection, matching the original engine's
    /// <c>location_xy</c> (arcanum-ce <c>src/game/location.c</c>):
    /// <code>
    /// sx = 40 * (y - x - 1)   sy = 20 * (y + x)     // screen Y points down
    /// </code>
    /// Using the engine's exact projection everywhere means its hardcoded pixel
    /// constants — roof's <c>-120/-200</c>, object <c>OBJ_F_OFFSET_X/Y</c>, and art
    /// hotspots — apply <em>directly</em>, with no per-feature fudge factors, and the
    /// render matches Arcanum's true (non-mirrored) orientation.
    /// </summary>
    public static class IsoProjection
    {
        /// <summary>Engine isometric half-steps, in pixels.</summary>
        public const float HalfWidth = 40f;

        public const float HalfHeight = 20f;

        /// <summary>World position (Unity Y-up) of tile (x, y) at the given pixels-per-unit.</summary>
        public static Vector3 TileToWorld(int x, int y, float pixelsPerUnit) => TileToWorld((float)x, y, pixelsPerUnit);

        /// <summary>As <see cref="TileToWorld(int,int,float)"/> but fractional, for smooth movement between tiles.</summary>
        public static Vector3 TileToWorld(float x, float y, float pixelsPerUnit)
        {
            float sx = HalfWidth * (y - x - 1f);
            float sy = HalfHeight * (y + x);
            return new Vector3(sx / pixelsPerUnit, -sy / pixelsPerUnit, 0f);
        }

        /// <summary>Inverse of <see cref="TileToWorld(int,int,float)"/>: nearest tile under a world point.</summary>
        public static Vector2Int WorldToTile(Vector3 world, float pixelsPerUnit)
        {
            float sx = world.x * pixelsPerUnit;
            float sy = -world.y * pixelsPerUnit;
            float ymx = sx / HalfWidth + 1f; // y - x
            float ypx = sy / HalfHeight;     // y + x
            return new Vector2Int(Mathf.RoundToInt((ypx - ymx) * 0.5f), Mathf.RoundToInt((ypx + ymx) * 0.5f));
        }

        /// <summary>Converts an engine screen-space offset (x right, y down) into a world delta.</summary>
        public static Vector3 ScreenOffset(float sx, float sy, float pixelsPerUnit)
            => new Vector3(sx / pixelsPerUnit, -sy / pixelsPerUnit, 0f);

        /// <summary>Tile deltas for the 8 facing rotations, from engine <c>location_in_dir</c>.</summary>
        public static readonly Vector2Int[] DirDelta =
        {
            new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1),
        };

        /// <summary>Rotation (0–7) whose direction best matches a tile-space step.</summary>
        public static int DirFromDelta(int dx, int dy)
        {
            dx = Mathf.Clamp(dx, -1, 1);
            dy = Mathf.Clamp(dy, -1, 1);
            for (int i = 0; i < 8; i++)
                if (DirDelta[i].x == dx && DirDelta[i].y == dy)
                    return i;
            return 3; // (0,0) → no movement; keep a sane default
        }
    }
}
