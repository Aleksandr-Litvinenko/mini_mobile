using UnityEngine;

namespace Moba
{
    /// Short-lived beam of glowing orbs between two points (life drain etc). Purely local.
    public class FxBeam : MonoBehaviour
    {
        float _life;
        const float Duration = 0.45f;
        Renderer[] _orbs;

        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            var root = new GameObject("FxBeam");
            var beam = root.AddComponent<FxBeam>();
            int n = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(from, to) * 1.6f), 5, 14);
            beam._orbs = new Renderer[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(orb.GetComponent<Collider>());
                orb.transform.SetParent(root.transform, false);
                orb.transform.position = Vector3.Lerp(from, to, t) +
                                         Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.4f;
                orb.transform.localScale = Vector3.one * Mathf.Lerp(0.32f, 0.14f, t);
                var m = orb.GetComponent<Renderer>().material;
                m.color = color;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 2.2f);
                beam._orbs[i] = orb.GetComponent<Renderer>();
            }
        }

        void Update()
        {
            _life += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_life / Duration);
            foreach (var r in _orbs)
                if (r != null)
                    r.transform.localScale *= 0.93f;
            if (_life >= Duration)
                Destroy(gameObject);
        }
    }
}
