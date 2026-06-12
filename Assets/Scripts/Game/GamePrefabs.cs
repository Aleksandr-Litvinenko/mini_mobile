using UnityEngine;

namespace Moba
{
    /// Runtime access to networked prefabs stored under Assets/Resources/Prefabs.
    public static class GamePrefabs
    {
        static GameObject _hero, _minion, _tower, _base, _projectile;

        public static GameObject Hero => _hero != null ? _hero : _hero = Load("Hero");
        public static GameObject Minion => _minion != null ? _minion : _minion = Load("Minion");
        public static GameObject Tower => _tower != null ? _tower : _tower = Load("Tower");
        public static GameObject BaseCore => _base != null ? _base : _base = Load("BaseCore");
        public static GameObject Projectile => _projectile != null ? _projectile : _projectile = Load("Projectile");

        static GameObject Load(string name)
        {
            var go = Resources.Load<GameObject>("Prefabs/" + name);
            if (go == null)
                Debug.LogError("[Moba] Prefab not found: " + name + ". Run menu MOBA/Rebuild All in the editor.");
            return go;
        }
    }
}
