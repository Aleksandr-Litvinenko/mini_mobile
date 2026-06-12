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
        // Map layout (lane runs along the X axis)
        public const float ClampX = 34.5f;
        public const float ClampZ = 11.8f;
        public const float BaseX = 30f;
        public const float TowerX = 14f;
        public const float HeroSpawnX = 26.5f;
        public const float FountainRadius = 7.5f;

        public const ushort Port = 7777;

        public static Color TeamColor(Team t)
        {
            return t == Team.Blue ? new Color(0.27f, 0.55f, 1f) : new Color(1f, 0.33f, 0.27f);
        }

        public static Vector3 BasePos(Team t)
        {
            return new Vector3(t == Team.Blue ? -BaseX : BaseX, 0f, 0f);
        }

        public static Vector3 TowerPos(Team t)
        {
            return new Vector3(t == Team.Blue ? -TowerX : TowerX, 0f, 0f);
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
