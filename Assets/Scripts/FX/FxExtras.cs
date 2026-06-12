using UnityEngine;

namespace Moba
{
    /// Scrolls the material's main texture — animated lava, streams, etc.
    public class UvScroller : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0.08f, 0.16f);
        Renderer _r;

        void Start()
        {
            _r = GetComponent<Renderer>();
        }

        void Update()
        {
            if (_r != null)
                _r.material.mainTextureOffset += speed * Time.deltaTime;
        }
    }

    /// Destroys the object after a delay (used by one-shot FX roots).
    public class TimedDestroy : MonoBehaviour
    {
        public float life = 3f;

        void Start()
        {
            Destroy(gameObject, life);
        }
    }

    /// Expanding, spinning, fading magic circle on the ground.
    public class RingFx : MonoBehaviour
    {
        public float duration = 0.7f;
        public float maxScale = 10f;
        public float spinSpeed = 90f;

        float _life;
        Renderer _r;
        Color _color = Color.white;

        public static void Spawn(Vector3 pos, Color color, float radius, float duration = 0.7f)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Fx/FxRing");
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, pos + Vector3.up * 0.08f, prefab.transform.rotation);
            var fx = go.GetComponent<RingFx>();
            if (fx == null) fx = go.AddComponent<RingFx>();
            fx.maxScale = radius * 2f;
            fx.duration = duration;
            fx._color = color;
        }

        void Start()
        {
            _r = GetComponentInChildren<Renderer>();
            ApplyColor(1f);
        }

        void Update()
        {
            _life += Time.deltaTime;
            float t = Mathf.Clamp01(_life / duration);
            float ease = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(maxScale * 0.25f, maxScale, ease);
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            ApplyColor(1f - t);
            if (_life >= duration)
                Destroy(gameObject);
        }

        void ApplyColor(float alpha)
        {
            if (_r == null) return;
            var c = _color * alpha; // additive shader: fade towards black
            c.a = alpha;
            var m = _r.material;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
            if (m.HasProperty("_Color")) m.color = c;
        }
    }
}
