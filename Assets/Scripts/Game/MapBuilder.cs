using System.Collections.Generic;
using UnityEngine;

namespace Moba
{
    /// Builds the visual map locally on every client: a basalt arena between two
    /// erupting volcanoes. Nothing here is networked.
    public static class MapBuilder
    {
        public static readonly List<Bounds> Bushes = new List<Bounds>();
        static readonly List<Vector3> Craters = new List<Vector3>();

        public static int BushIndex(Vector3 p)
        {
            for (int i = 0; i < Bushes.Count; i++)
            {
                var b = Bushes[i];
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z)
                    return i;
            }
            return -1;
        }

        public static void EruptVolcanoes()
        {
            foreach (var c in Craters)
            {
                FxBurst.Spawn(c, new Color(1f, 0.45f, 0.1f), 9f, 0.9f);
                ParticleFx.SpawnOneShot(c);
            }
        }

        public static void Build()
        {
            Bushes.Clear();
            Craters.Clear();
            var root = new GameObject("Map");

            // volcanic atmosphere
            RenderSettings.ambientLight = new Color(0.5f, 0.35f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.22f, 0.1f, 0.08f);
            RenderSettings.fogDensity = 0.006f;

            // ground and lane
            Block(root, "Ground", new Vector3(0f, -0.15f, 0f), new Vector3(74f, 0.3f, 28f),
                new Color(0.5f, 0.45f, 0.42f), 0f, "Textures/rock", 9f);
            Block(root, "Lane", new Vector3(0f, -0.13f, 0f), new Vector3(64f, 0.32f, 7.6f),
                new Color(0.75f, 0.66f, 0.58f), 0f, "Textures/lane", 6f);

            // obsidian walls
            var wallC = new Color(0.55f, 0.45f, 0.6f);
            Block(root, "WallN", new Vector3(0f, 0.8f, 14.2f), new Vector3(74f, 1.6f, 1f), wallC, 0f, "Textures/obsidian", 5f);
            Block(root, "WallS", new Vector3(0f, 0.8f, -14.2f), new Vector3(74f, 1.6f, 1f), wallC, 0f, "Textures/obsidian", 5f);
            Block(root, "WallE", new Vector3(37.2f, 0.8f, 0f), new Vector3(1f, 1.6f, 29.4f), wallC, 0f, "Textures/obsidian", 5f);
            Block(root, "WallW", new Vector3(-37.2f, 0.8f, 0f), new Vector3(1f, 1.6f, 29.4f), wallC, 0f, "Textures/obsidian", 5f);

            // base platforms + fountains
            foreach (Team t in new[] { Team.Blue, Team.Red })
            {
                var c = GameConstants.TeamColor(t) * 0.55f;
                c.a = 1f;
                var pos = GameConstants.BasePos(t);
                Cylinder(root, "Platform" + t, pos + Vector3.down * 0.05f,
                    new Vector3(11f, 0.1f, 11f), c);
                Cylinder(root, "Fountain" + t, pos + Vector3.down * 0.01f,
                    new Vector3(4.5f, 0.12f, 4.5f), GameConstants.TeamColor(t), 0.8f);
                ParticleFx.Spawn("FxSparkles", pos + Vector3.up * 0.3f, root.transform);
            }

            // scorched bushes (gameplay stealth zones)
            var bushSize = new Vector3(4.6f, 0.5f, 2.4f);
            foreach (float x in new[] { -9f, 9f })
                foreach (float z in new[] { 6.3f, -6.3f })
                {
                    var center = new Vector3(x, 0.25f, z);
                    Block(root, "Bush", center, bushSize, new Color(0.22f, 0.34f, 0.16f));
                    for (int i = 0; i < 3; i++)
                        Sphere(root, "BushTuft", center + new Vector3((i - 1) * 1.4f, 0.35f, 0f),
                            1.3f, new Color(0.26f, 0.4f, 0.18f));
                    Bushes.Add(new Bounds(center, bushSize + new Vector3(0.6f, 2f, 0.6f)));
                }

            // volcanic rocks
            foreach (var p in new[]
                     {
                         new Vector3(-20f, 0.5f, 9f), new Vector3(20f, 0.5f, -9f),
                         new Vector3(-4f, 0.6f, 10.5f), new Vector3(4f, 0.6f, -10.5f),
                         new Vector3(-26f, 0.4f, -8f), new Vector3(26f, 0.4f, 8f)
                     })
            {
                var rock = Block(root, "Rock", p, Vector3.one * Random.Range(1.1f, 1.9f),
                    new Color(0.45f, 0.4f, 0.4f), 0f, "Textures/rock", 1.5f);
                rock.transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f),
                    Random.Range(0f, 360f), Random.Range(-12f, 12f));
            }

            // the two volcanoes the arena sits between
            BuildVolcano(root, new Vector3(-34f, 0f, 27f));
            BuildVolcano(root, new Vector3(34f, 0f, 27f));

            // drifting ash and embers above the battlefield
            var ash = ParticleFx.Spawn("FxAsh", new Vector3(0f, 11f, 0f), root.transform);
            if (ash != null) ash.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        static void BuildVolcano(GameObject root, Vector3 basePos)
        {
            var rockC = new Color(0.32f, 0.26f, 0.26f);
            float[] radii = { 16f, 12.5f, 9f, 6f, 3.6f };
            float h = 0f;
            for (int i = 0; i < radii.Length; i++)
            {
                float step = 3.2f;
                Cylinder(root, "VolcanoTier", basePos + Vector3.up * (h + step / 2f),
                    new Vector3(radii[i] * 2f, step / 2f, radii[i] * 2f), rockC, 0f,
                    "Textures/rock", 3f);
                h += step;
            }
            // glowing crater
            var crater = basePos + Vector3.up * (h + 0.1f);
            Cylinder(root, "Crater", crater, new Vector3(6f, 0.3f, 6f),
                new Color(1f, 0.6f, 0.25f), 1.8f, "Textures/lava", 1.5f);
            Craters.Add(crater + Vector3.up * 0.5f);

            // lava streams down the slopes
            for (int i = 0; i < 4; i++)
            {
                float ang = 35f + i * 80f + Random.Range(-15f, 15f);
                var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                var stream = Block(root, "LavaStream",
                    basePos + dir * 9f + Vector3.up * (h * 0.45f),
                    new Vector3(1.1f, 0.25f, 11f), new Color(1f, 0.55f, 0.2f), 1.4f,
                    "Textures/lava", 1f);
                stream.transform.rotation = Quaternion.LookRotation(dir) *
                                            Quaternion.Euler(-52f, 0f, 0f);
            }

            ParticleFx.Spawn("FxSmoke", crater + Vector3.up * 0.6f, root.transform, 2f);
            ParticleFx.Spawn("FxEmbers", crater + Vector3.up * 0.4f, root.transform, 2.5f);
        }

        static GameObject Primitive(GameObject root, string name, PrimitiveType type,
            Vector3 pos, Vector3 scale, Color color, float emission, string tex, float tiling)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var m = go.GetComponent<Renderer>().material;
            m.color = color;
            if (!string.IsNullOrEmpty(tex))
            {
                var t = Resources.Load<Texture2D>(tex);
                if (t != null)
                {
                    m.mainTexture = t;
                    m.mainTextureScale = Vector2.one * tiling;
                }
            }
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
            }
            return go;
        }

        static GameObject Block(GameObject root, string name, Vector3 pos, Vector3 scale,
            Color color, float emission = 0f, string tex = null, float tiling = 1f)
            => Primitive(root, name, PrimitiveType.Cube, pos, scale, color, emission, tex, tiling);

        static GameObject Cylinder(GameObject root, string name, Vector3 pos, Vector3 scale,
            Color color, float emission = 0f, string tex = null, float tiling = 1f)
            => Primitive(root, name, PrimitiveType.Cylinder, pos, scale, color, emission, tex, tiling);

        static GameObject Sphere(GameObject root, string name, Vector3 pos, float scale, Color color)
            => Primitive(root, name, PrimitiveType.Sphere, pos, Vector3.one * scale, color, 0f, null, 1f);
    }
}
