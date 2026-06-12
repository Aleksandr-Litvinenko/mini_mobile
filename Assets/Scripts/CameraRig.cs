using UnityEngine;

namespace Moba
{
    /// Isometric camera: orbits the map in the menu, follows the local hero in game.
    public class CameraRig : MonoBehaviour
    {
        static float _shakeUntil;
        static float _shakeAmp;

        Vector3 _lookTarget;
        float _zoom = 1f;

        public static void Shake(float amplitude, float duration)
        {
            _shakeAmp = amplitude;
            _shakeUntil = Time.time + duration;
        }

        void Start()
        {
            _lookTarget = Vector3.zero;
        }

        void LateUpdate()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                _zoom = Mathf.Clamp(_zoom - scroll * 0.8f, 0.65f, 1.45f);

            if (Hero.Local != null)
            {
                _lookTarget = Vector3.Lerp(_lookTarget, Hero.Local.transform.position,
                    10f * Time.deltaTime);
                Vector3 offset = new Vector3(0f, 14.5f, -10.5f) * _zoom;
                transform.position = Vector3.Lerp(transform.position, _lookTarget + offset,
                    10f * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(_lookTarget + Vector3.up * 1f - transform.position),
                    10f * Time.deltaTime);
            }
            else
            {
                // slow menu orbit around the arena
                float a = Time.time * 0.08f;
                Vector3 pos = new Vector3(Mathf.Sin(a) * 26f, 17f, Mathf.Cos(a) * 26f);
                transform.position = Vector3.Lerp(transform.position, pos, 2f * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(Vector3.zero + Vector3.up * 0.5f - transform.position),
                    2f * Time.deltaTime);
            }

            if (Time.time < _shakeUntil)
            {
                float k = (_shakeUntil - Time.time) / 2.4f;
                transform.position += Random.insideUnitSphere * _shakeAmp * Mathf.Clamp01(k);
            }
        }
    }
}
