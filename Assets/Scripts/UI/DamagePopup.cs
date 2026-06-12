using UnityEngine;

namespace Moba
{
    /// Floating combat text, purely local.
    public class DamagePopup : MonoBehaviour
    {
        float _life;
        float _maxLife = 0.9f;
        TextMesh _text;

        public static void Spawn(Vector3 pos, string text, Color color, float size)
        {
            var go = new GameObject("Popup");
            go.transform.position = pos + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().material = font.material;
            tm.text = text;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 46;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = 0.045f * size;
            var p = go.AddComponent<DamagePopup>();
            p._text = tm;
        }

        void Update()
        {
            _life += Time.deltaTime;
            transform.position += Vector3.up * 1.6f * Time.deltaTime;
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;
            if (_text != null)
            {
                var c = _text.color;
                c.a = Mathf.Clamp01(1.6f - _life / _maxLife * 1.6f);
                _text.color = c;
            }
            if (_life >= _maxLife)
                Destroy(gameObject);
        }
    }
}
