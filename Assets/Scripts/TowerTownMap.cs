using UnityEngine;

namespace AdamsHaven.Tower
{
    // The TOWN view's ground plan (TOWER_MODE_GDD 19.2, TT 10.3.2 greybox): a square tile grid around the Tower, with a
    // circle-ish buildable ring that widens with the Heart rank. Tile (0, 0) is the Tower's centre; x runs west to
    // east, z south to north. The Tower's two Gates face west and east, so the first roads leave along z = 0; a north to
    // south road opens from Heart rank C. Pure functions, no state yet: town lots arrive with GDD 19.2's rules slice.
    public static class TowerTownMap
    {
        public enum Tile { Wild, Lot, Road, Plaza, Tower, Gate }

        public const int TowerHalf = 2;   // the Tower's footprint: tiles -2..2 on both axes
        public const int CrossRoadRank = 4;   // C: the north-south road opens

        // Buildable radius in tiles for Heart ranks F..SSR.
        private static readonly int[] Radii = { 7, 9, 11, 14, 17, 21, 25, 30, 35 };

        public static int Radius(int heartRank) { return Radii[Mathf.Clamp(heartRank, 1, TowerTiers.MaxRank) - 1]; }
        public static int MaxRadius { get { return Radii[Radii.Length - 1]; } }

        // Inside the ring: the tile's centre lies within the radius (+0.5 so the ring's edge reads round on the grid).
        public static bool InRing(int x, int z, int heartRank)
        {
            float r = Radius(heartRank) + 0.5f;
            return x * x + z * z <= r * r;
        }

        public static Tile At(int x, int z, int heartRank)
        {
            if (!InRing(x, z, heartRank)) return Tile.Wild;
            int ax = Mathf.Abs(x), az = Mathf.Abs(z);
            if (ax <= TowerHalf && az <= TowerHalf) return Tile.Tower;
            if (az == 0 && ax == TowerHalf + 1) return Tile.Gate;
            // A small square in front of each Gate.
            if (ax >= TowerHalf + 2 && ax <= TowerHalf + 4 && az <= 1) return Tile.Plaza;
            if (z == 0) return Tile.Road;
            if (x == 0 && heartRank >= CrossRoadRank) return Tile.Road;
            if (IsRingRoad(x, z, heartRank)) return Tile.Road;
            return Tile.Lot;
        }

        public const int RingRoadEvery = 8;   // concentric ring roads at radius 8, 16, 24, 32 cut the city into blocks

        // A ring road lies one tile wide on each multiple of RingRoadEvery that sits well inside the buildable ring.
        public static bool IsRingRoad(int x, int z, int heartRank)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            int nearest = Mathf.RoundToInt(d / RingRoadEvery) * RingRoadEvery;
            if (nearest == 0 || nearest > Radius(heartRank) - 2) return false;
            return Mathf.Abs(d - nearest) < 0.5f;
        }

        public static bool Buildable(int x, int z, int heartRank) { return At(x, z, heartRank) == Tile.Lot; }

        // Lots on a road or plaza edge (8-neighbourhood) fill first; the town grows outward from the Gates.
        public static bool Frontage(int x, int z, int heartRank)
        {
            if (!Buildable(x, z, heartRank)) return false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var tile = At(x + dx, z + dz, heartRank);
                    if (tile == Tile.Road || tile == Tile.Plaza) return true;
                }
            return false;
        }

        // Buildable lots inside the ring for a Heart rank.
        public static int LotCount(int heartRank)
        {
            int count = 0, r = Radius(heartRank);
            for (int x = -r; x <= r; x++)
                for (int z = -r; z <= r; z++)
                    if (Buildable(x, z, heartRank)) count++;
            return count;
        }
    }
}
