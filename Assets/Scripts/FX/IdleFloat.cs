using UnityEngine;

namespace Moba
{
    /// Gentle floating + spinning for crystals, orbs and other decorations. Purely local.
    public class IdleFloat : MonoBehaviour
    {
        public float amplitude = 0.12f;
        public float speed = 2.2f;
        public float spin = 60f;
        public float pulse; // optional scale pulse fraction

        Vector3 _basePos;
        Vector3 _baseScale;
        float _seed;

        void Start()
        {
            _basePos = transform.localPosition;
            _baseScale = transform.localScale;
            _seed = Random.Range(0f, 20f);
        }

        void Update()
        {
            float t = Time.time * speed + _seed;
            transform.localPosition = _basePos + Vector3.up * Mathf.Sin(t) * amplitude;
            if (spin != 0f)
                transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.Self);
            if (pulse > 0f)
                transform.localScale = _baseScale * (1f + Mathf.Sin(t * 1.7f) * pulse);
        }
    }
}
