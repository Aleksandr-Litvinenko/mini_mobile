using UnityEngine;

namespace Moba
{
    /// World-space billboard health bar; quads are wired up by the editor prefab builder.
    public class HealthBar : MonoBehaviour
    {
        public Transform fillPivot;
        public Renderer fillRenderer;
        public Renderer bgRenderer;

        UnitBase _unit;
        bool _inited;

        void Start()
        {
            _unit = GetComponentInParent<UnitBase>();
        }

        void LateUpdate()
        {
            if (_unit == null) return;
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;

            if (!_inited && bgRenderer != null)
            {
                bgRenderer.material.color = new Color(0.08f, 0.08f, 0.08f);
                _inited = true;
            }

            float frac = _unit.MaxHp.Value > 0f ? Mathf.Clamp01(_unit.Hp.Value / _unit.MaxHp.Value) : 0f;
            if (fillPivot != null)
            {
                var s = fillPivot.localScale;
                s.x = frac;
                fillPivot.localScale = s;
            }
            if (fillRenderer != null)
            {
                Color c;
                if (Hero.Local != null && _unit.Team == Hero.Local.Team)
                    c = _unit == (UnitBase)Hero.Local
                        ? new Color(0.35f, 1f, 0.35f)
                        : new Color(0.3f, 0.75f, 1f);
                else
                    c = new Color(1f, 0.3f, 0.25f);
                fillRenderer.material.color = c;
            }
        }
    }
}
