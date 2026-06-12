using UnityEngine;

namespace Moba
{
    /// Marker: this renderer should be tinted with the owning unit's team color.
    public class TeamTint : MonoBehaviour
    {
    }

    /// Applies a fixed color, optional emission and optional Resources texture at startup.
    public class AutoColor : MonoBehaviour
    {
        public Color color = Color.white;
        public float emission;
        public string textureResource = "";
        public float tiling = 1f;

        void Start()
        {
            var r = GetComponent<Renderer>();
            if (r == null) return;
            var m = r.material;
            m.color = color;
            if (!string.IsNullOrEmpty(textureResource))
            {
                var tex = Resources.Load<Texture2D>(textureResource);
                if (tex != null)
                {
                    m.mainTexture = tex;
                    m.mainTextureScale = Vector2.one * tiling;
                }
            }
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
            }
        }
    }
}
