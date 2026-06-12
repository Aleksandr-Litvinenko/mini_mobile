using UnityEngine;

namespace Moba
{
    /// Procedural animation: walk bobbing, idle breathing and attack lunges.
    /// Animates the "Visual" child so health bars stay still. Purely local.
    public class UnitAnimator : MonoBehaviour
    {
        public Transform body;

        Vector3 _basePos;
        Quaternion _baseRot;
        Vector3 _lastPos;
        float _phase;
        float _attackT;

        public void TriggerAttack()
        {
            _attackT = 1f;
        }

        void Start()
        {
            if (body == null)
            {
                var v = transform.Find("Visual");
                body = v != null ? v : null;
            }
            if (body != null)
            {
                _basePos = body.localPosition;
                _baseRot = body.localRotation;
            }
            _lastPos = transform.position;
        }

        void Update()
        {
            if (body == null) return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 vel = (transform.position - _lastPos) / dt;
            _lastPos = transform.position;
            float speed = new Vector2(vel.x, vel.z).magnitude;
            bool moving = speed > 0.6f;

            _phase += dt * (moving ? 10f : 2f);
            float bob = moving ? Mathf.Abs(Mathf.Sin(_phase)) * 0.14f : Mathf.Sin(_phase) * 0.03f;
            float rock = moving ? Mathf.Sin(_phase) * 4f : 0f;

            Vector3 pos = _basePos + Vector3.up * bob;
            Quaternion rot = _baseRot * Quaternion.Euler(0f, 0f, rock);

            if (_attackT > 0f)
            {
                _attackT -= dt * 4f;
                float k = Mathf.Sin(Mathf.Clamp01(_attackT) * Mathf.PI);
                pos += Vector3.forward * 0.4f * k;
                rot *= Quaternion.Euler(14f * k, 0f, 0f);
            }

            body.localPosition = pos;
            body.localRotation = rot;
        }
    }
}
