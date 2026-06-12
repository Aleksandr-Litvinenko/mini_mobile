using UnityEngine;

namespace Moba
{
    public enum Team : byte
    {
        None = 0,
        Blue = 1,
        Red = 2
    }

    public static class GameConstants
    {
        // Map layout (lane runs along the X axis). Arena is ~115 x 42 units.
        public const float ClampX = 54f;
        public const float ClampZ = 18.6f;
        public const float BaseX = 48.3f;
        public const float Tower1X = 23f;   // forward tower
        public const float Tower2X = 36.8f; // inner tower
        public const float HeroSpawnX = 44.3f;
        public const float FountainRadius = 9f;
        public const float MapHalfW = 57.5f; // minimap mapping
        public const float MapHalfH = 20.7f;

        public const ushort Port = 7777;

        public static Color TeamColor(Team t)
        {
            return t == Team.Blue ? new Color(0.27f, 0.55f, 1f) : new Color(1f, 0.33f, 0.27f);
        }

        public static Vector3 BasePos(Team t)
        {
            return new Vector3(t == Team.Blue ? -BaseX : BaseX, 0f, 0f);
        }

        public static Vector3 HeroSpawn(Team t)
        {
            return new Vector3(t == Team.Blue ? -HeroSpawnX : HeroSpawnX, 0f, 0f);
        }

        public static Team Enemy(Team t)
        {
            return t == Team.Blue ? Team.Red : Team.Blue;
        }

        public static float Dist2D(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
