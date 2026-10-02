using UnityEngine;

namespace AdamsHaven.Tower
{
    // Fallout Shelter-style fake depth inside a room: the camera stays orthographic and side-on,
    // residents roam between the back wall (d=0) and the front of the room (d=1). Back sits higher on
    // screen, smaller and behind; front sits lower, full size and in front.
    public static class TowerRoomDepth
    {
        public const float BackFeet = -0.70f, FrontFeet = -0.90f; // feet offset from the floor's y
        public const float BackScale = 0.88f;
        public const float BaseZ = -0.30f, ZSpan = 0.25f;
        private const float BodyHalf = 0.65f;                     // half of the 1.30 tall chibi quad
        private const float WanderPeriod = 7f, WalkShare = 0.35f;
        private const float EdgeMargin = 0.28f;

        public static Vector3 Project(float floorY, float worldX, float d)
        {
            d = Mathf.Clamp01(d);
            float scale = Mathf.Lerp(BackScale, 1f, d);
            float feet = floorY + Mathf.Lerp(BackFeet, FrontFeet, d);
            return new Vector3(worldX, feet + BodyHalf * scale, BaseZ - d * ZSpan);
        }

        // The depth scale is recoverable from z, so poses only need to carry a position.
        public static float ScaleFromZ(float z)
        {
            float d = Mathf.Clamp01((BaseZ - z) / ZSpan);
            return Mathf.Lerp(BackScale, 1f, d);
        }

        // Stable depth for residents standing at a work or bed spot.
        public static float StationDepth(int id) { return 0.25f + 0.5f * Hash01(id * 31 + 7); }

        // Idle wandering: a pure function of (id, clock), so it needs no saved state. Each period the
        // resident strolls to a new point in the room's walkable rect, then pauses.
        public static void Wander(int id, float clock, out float x01, out float d)
        {
            float shifted = clock + id * 1.7f;
            int segment = Mathf.FloorToInt(shifted / WanderPeriod);
            float local = shifted / WanderPeriod - segment;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(local / WalkShare));
            float fromX, fromD, toX, toD;
            Waypoint(id, segment - 1, out fromX, out fromD);
            Waypoint(id, segment, out toX, out toD);
            x01 = Mathf.Lerp(fromX, toX, t);
            d = Mathf.Lerp(fromD, toD, t);
        }

        // Local x (0..1) to world x across a room spanning [left, right].
        public static float WorldXInRoom(float left, float right, float x01)
        {
            return Mathf.Lerp(left + EdgeMargin, right - EdgeMargin, x01);
        }

        private static void Waypoint(int id, int segment, out float x01, out float d)
        {
            x01 = Hash01(id * 7919 + segment * 104729);
            d = Hash01(id * 613 + segment * 977 + 5);
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFFu) / 65535f;
            }
        }
    }
}
