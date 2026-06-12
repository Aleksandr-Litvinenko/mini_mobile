using UnityEngine;

namespace Moba
{
    /// Quick expanding sphere used for hits, ults and level-ups. Purely local.
    public class FxBurst : MonoBehaviour
    {
        float _life;
        float _duration;
        float _radius;

        public static void Spawn(Vector3 pos, Color color, float radius, float duration = 0.35f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "FxBurst";
            go.transform.position = pos + Vector3.up * 0.4f;
            go.transform.localScale = Vector3.one * 0.4f;
            var m = go.GetComponent<Renderer>().material;
            m.color = color;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2f);
            var fx = go.AddComponent<FxBurst>();
            fx._duration = duration;
            fx._radius = radius;
        }

        void Update()
        {
            _life += Time.deltaTime;
            float t = Mathf.Clamp01(_life / _duration);
            float s = Mathf.Lerp(0.4f, _radius * 2f, 1f - (1f - t) * (1f - t));
            transform.localScale = new Vector3(s, s * 0.35f, s);
            if (_life >= _duration)
                Destroy(gameObject);
        }
    }
}
