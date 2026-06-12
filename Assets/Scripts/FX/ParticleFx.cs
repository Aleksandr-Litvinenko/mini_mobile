using UnityEngine;

namespace Moba
{
    /// Spawns pre-built particle prefabs from Resources/Prefabs/Fx
    /// (built by the editor project builder so materials survive player builds).
    public static class ParticleFx
    {
        public static GameObject Spawn(string name, Vector3 pos, Transform parent = null,
            float scale = 1f)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Fx/" + name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, pos, prefab.transform.rotation, parent);
            if (scale != 1f)
                go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// One-shot burst that destroys itself, optionally tinted.
        public static void SpawnOneShot(Vector3 pos, Color? tint = null)
        {
            var go = Spawn("FxBurstPs", pos);
            if (go != null && tint.HasValue)
                Tint(go, tint.Value);
        }

        public static void SpawnTinted(string name, Vector3 pos, Color tint, float scale = 1f)
        {
            var go = Spawn(name, pos, null, scale);
            if (go != null)
                Tint(go, tint);
        }

        static void Tint(GameObject go, Color tint)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    tint, Color.Lerp(tint, Color.white, 0.5f));
            }
        }
    }
}
