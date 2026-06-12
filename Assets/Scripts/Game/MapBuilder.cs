using System.Collections.Generic;
using UnityEngine;

namespace Moba
{
    /// Builds the visual map locally on every client: a large basalt arena between two
    /// erupting volcanoes. Built only from spheres, capsules and cylinders — no cubes.
    /// Also owns the static obstacle list used for movement collision.
    public static class MapBuilder
    {
        public static readonly List<Bounds> Bushes = new List<Bounds>();
        static readonly List<Vector3> Craters = new List<Vector3>();

        struct Obstacle
        {
            public Vector2 pos;
            public float radius;
        }

        static readonly List<Obstacle> Obstacles = new List<Obstacle>();

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

        /// Push a unit position out of static obstacles and alive structures.
        public static Vector3 ResolvePosition(Vector3 p, float bodyRadius)
        {
            for (int i = 0; i < Obstacles.Count; i++)
            {
                var o = Obstacles[i];
                p = PushOut(p, o.pos.x, o.pos.y, o.radius + bodyRadius);
            }
            var units = UnitBase.All;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || !u.Alive) continue;
                if (!(u is Tower) && !(u is BaseCore)) continue;
                p = PushOut(p, u.Pos.x, u.Pos.z, u.BodyRadius + 0.25f + bodyRadius);
            }
            return p;
        }

        static Vector3 PushOut(Vector3 p, float cx, float cz, float minDist)
        {
            float dx = p.x - cx;
            float dz = p.z - cz;
            float sq = dx * dx + dz * dz;
            if (sq >= minDist * minDist) return p;
            if (sq < 0.0001f)
            {
                p.x = cx + minDist;
                return p;
            }
            float d = Mathf.Sqrt(sq);
            p.x = cx + dx / d * minDist;
            p.z = cz + dz / d * minDist;
            return p;
        }

        public static void EruptVolcanoes()
        {
            foreach (var c in Craters)
            {
                FxBurst.Spawn(c, new Color(1f, 0.45f, 0.1f), 10f, 0.9f);
                ParticleFx.SpawnOneShot(c);
            }
        }

        public static void Build()
        {
            Bushes.Clear();
            Craters.Clear();
            Obstacles.Clear();
            var root = new GameObject("Map");

            // volcanic atmosphere
            RenderSettings.ambientLight = new Color(0.5f, 0.35f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.22f, 0.1f, 0.08f);
            RenderSettings.fogDensity = 0.005f;

            // ground and lane (flat cylinders, no cubes anywhere)
            Cylinder(root, "Ground", new Vector3(0f, -0.18f, 0f), new Vector3(136f, 0.15f, 58f),
                new Color(0.5f, 0.45f, 0.42f), 0f, "Textures/rock", 12f);
            Cylinder(root, "Lane", new Vector3(0f, -0.12f, 0f), new Vector3(112f, 0.14f, 13.2f),
                new Color(0.75f, 0.66f, 0.58f), 0f, "Textures/lane", 8f);

            // basalt column walls around the arena
            BuildColumnWall(root, -57f, 57f, 20.2f, true);
            BuildColumnWall(root, -57f, 57f, -20.2f, true);
            BuildColumnWall(root, -19.5f, 19.5f, 56.4f, false);
            BuildColumnWall(root, -19.5f, 19.5f, -56.4f, false);

            // base platforms + fountains
            foreach (Team t in new[] { Team.Blue, Team.Red })
            {
                var c = GameConstants.TeamColor(t) * 0.55f;
                c.a = 1f;
                var pos = GameConstants.BasePos(t);
                Cylinder(root, "Platform" + t, pos + Vector3.down * 0.05f,
                    new Vector3(15f, 0.1f, 15f), c);
                Cylinder(root, "Fountain" + t, pos + Vector3.down * 0.01f,
                    new Vector3(5.6f, 0.12f, 5.6f), GameConstants.TeamColor(t), 0.8f);
                ParticleFx.Spawn("FxSparkles", pos + Vector3.up * 0.3f, root.transform);
            }

            // leafy bushes (stealth zones)
            foreach (var bp in new[]
                     {
                         new Vector3(-13.8f, 0f, 9f), new Vector3(13.8f, 0f, 9f),
                         new Vector3(-13.8f, 0f, -9f), new Vector3(13.8f, 0f, -9f),
                         new Vector3(-31f, 0f, 9f), new Vector3(31f, 0f, -9f)
                     })
                BuildBush(root, bp);

            // boulder clusters off the lane (impassable)
            foreach (var rp in new[]
                     {
                         new Vector3(-23f, 0f, 14.4f), new Vector3(23f, 0f, -14.4f),
                         new Vector3(-39f, 0f, -12.6f), new Vector3(39f, 0f, 12.6f),
                         new Vector3(0f, 0f, 15.5f), new Vector3(0f, 0f, -15.5f),
                         new Vector3(-7f, 0f, 13.6f), new Vector3(7f, 0f, -13.6f)
                     })
                BuildBoulders(root, rp);

            // scorched forest along the walls
            BuildTrees(root);

            // the two volcanoes the arena sits between
            BuildVolcano(root, new Vector3(-52f, 0f, 36f));
            BuildVolcano(root, new Vector3(52f, 0f, 36f));

            // drifting ash and embers above the battlefield
            var ash = ParticleFx.Spawn("FxAsh", new Vector3(0f, 12f, 0f), root.transform);
            if (ash != null) ash.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        static void BuildColumnWall(GameObject root, float from, float to, float fixedCoord,
            bool alongX)
        {
            var rnd = new System.Random(alongX ? (int)fixedCoord : 7000 + (int)fixedCoord);
            for (float c = from; c <= to; c += 2.3f)
            {
                float h = 1.4f + (float)rnd.NextDouble() * 1.3f;
                float r = 0.55f + (float)rnd.NextDouble() * 0.25f;
                Vector3 pos = alongX
                    ? new Vector3(c, h / 2f - 0.1f, fixedCoord)
                    : new Vector3(fixedCoord, h / 2f - 0.1f, c);
                var col = Cylinder(root, "WallColumn", pos, new Vector3(r * 2f, h / 2f, r * 2f),
                    new Color(0.55f, 0.45f, 0.6f), 0f, "Textures/obsidian", 1.5f);
                col.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                // glowing cap on some columns
                if (rnd.NextDouble() > 0.72)
                    Sphere(root, "ColumnEmber", pos + Vector3.up * (h / 2f + 0.1f), r * 0.8f,
                        new Color(1f, 0.5f, 0.15f), 1.4f);
            }
        }

        static void BuildBush(GameObject root, Vector3 center)
        {
            // a proper leafy bush: trunks + layered canopies from dark to light green
            var rnd = new System.Random((int)(center.x * 31 + center.z * 17));
            foreach (var off in new[] { new Vector3(-1f, 0f, 0.2f), new Vector3(0.9f, 0f, -0.3f) })
                Cylinder(root, "BushTrunk", center + off + Vector3.up * 0.3f,
                    new Vector3(0.3f, 0.35f, 0.3f), new Color(0.32f, 0.22f, 0.13f));
            var dark = new Color(0.14f, 0.27f, 0.11f);
            var mid = new Color(0.2f, 0.38f, 0.15f);
            var light = new Color(0.3f, 0.5f, 0.2f);
            for (int i = 0; i < 6; i++) // bottom canopy
            {
                float a = i / 6f * Mathf.PI * 2f;
                Sphere(root, "BushLeaf", center + new Vector3(Mathf.Cos(a) * 1.7f, 0.55f,
                        Mathf.Sin(a) * 1.1f), 1.7f + (float)rnd.NextDouble() * 0.5f, dark, 0f, 0.75f);
            }
            for (int i = 0; i < 4; i++) // middle
            {
                float a = i / 4f * Mathf.PI * 2f + 0.5f;
                Sphere(root, "BushLeaf", center + new Vector3(Mathf.Cos(a) * 1.1f, 1.05f,
                        Mathf.Sin(a) * 0.75f), 1.6f + (float)rnd.NextDouble() * 0.4f, mid, 0f, 0.8f);
            }
            Sphere(root, "BushLeaf", center + new Vector3(0f, 1.55f, 0f), 1.7f, light, 0f, 0.8f);
            Sphere(root, "BushLeaf", center + new Vector3(0.6f, 1.45f, 0.4f), 1.1f,
                new Color(0.36f, 0.56f, 0.24f), 0f, 0.85f);
            Bushes.Add(new Bounds(center + Vector3.up * 0.6f, new Vector3(5.8f, 3.2f, 4f)));
        }

        static void BuildBoulders(GameObject root, Vector3 center)
        {
            var rnd = new System.Random((int)(center.x * 13 + center.z * 7));
            // ready-made rock models (Quaternius, CC0)
            int n = 2 + rnd.Next(2);
            for (int i = 0; i < n; i++)
            {
                var off = new Vector3(((float)rnd.NextDouble() - 0.5f) * 2.4f, 0f,
                    ((float)rnd.NextDouble() - 0.5f) * 2.4f);
                SpawnModel(root, rnd.Next(2) == 0 ? "Rock1" : "Rock2", center + off,
                    1.5f + (float)rnd.NextDouble() * 1.2f, (float)rnd.NextDouble() * 360f);
            }
            Obstacles.Add(new Obstacle { pos = new Vector2(center.x, center.z), radius = 1.9f });
        }

        static void BuildTrees(GameObject root)
        {
            var rnd = new System.Random(99);
            foreach (float side in new[] { -1f, 1f })
                for (float x = -48f; x <= 48f; x += 9.5f)
                {
                    if (Mathf.Abs(x) < 7f) continue; // keep mid bushes clear
                    float z = side * (16.6f + (float)rnd.NextDouble() * 1.6f);
                    string model = rnd.Next(2) == 0 ? "Tree1" : "PineTree";
                    SpawnModel(root, model, new Vector3(x, 0f, z),
                        4.4f + (float)rnd.NextDouble() * 2f, (float)rnd.NextDouble() * 360f);
                    Obstacles.Add(new Obstacle { pos = new Vector2(x, z), radius = 0.9f });
                }
        }

        /// Instantiate a Resources/Models FBX at pos, scaled to targetHeight, feet at y=0.
        static GameObject SpawnModel(GameObject root, string model, Vector3 pos,
            float targetHeight, float yaw)
        {
            var asset = Resources.Load<GameObject>("Models/" + model);
            if (asset == null) return null;
            var inst = Object.Instantiate(asset, root.transform);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var b = ModelBounds(inst);
            float s = targetHeight / Mathf.Max(0.01f, b.size.y);
            inst.transform.localScale = Vector3.one * s;
            b = ModelBounds(inst);
            inst.transform.position = new Vector3(pos.x, pos.y - b.min.y, pos.z);
            return inst;
        }

        static Bounds ModelBounds(GameObject inst)
        {
            var rends = inst.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(inst.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in rends)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static void BuildVolcano(GameObject root, Vector3 basePos)
        {
            var rockC = new Color(0.32f, 0.26f, 0.26f);
            float[] radii = { 19f, 15f, 11f, 7.2f, 4.2f };
            float h = 0f;
            for (int i = 0; i < radii.Length; i++)
            {
                float step = 3.6f;
                Cylinder(root, "VolcanoTier", basePos + Vector3.up * (h + step / 2f),
                    new Vector3(radii[i] * 2f, step / 2f, radii[i] * 2f), rockC, 0f,
                    "Textures/rock", 3f);
                h += step;
            }
            var crater = basePos + Vector3.up * (h + 0.1f);
            Cylinder(root, "Crater", crater, new Vector3(7f, 0.3f, 7f),
                new Color(1f, 0.6f, 0.25f), 1.8f, "Textures/lava", 1.5f);
            Craters.Add(crater + Vector3.up * 0.5f);

            // lava streams down the slopes (capsules)
            for (int i = 0; i < 4; i++)
            {
                float ang = 35f + i * 80f + Random.Range(-15f, 15f);
                var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                var stream = Capsule(root, "LavaStream",
                    basePos + dir * 11f + Vector3.up * (h * 0.45f),
                    new Vector3(1.2f, 7.2f, 1.2f), new Color(1f, 0.55f, 0.2f), 1.4f);
                stream.transform.rotation = Quaternion.LookRotation(dir) *
                                            Quaternion.Euler(38f, 0f, 0f);
                SetTexture(stream, "Textures/lava", 1f);
                stream.AddComponent<UvScroller>().speed = new Vector2(0f, -0.25f);
            }

            ParticleFx.Spawn("FxSmoke", crater + Vector3.up * 0.6f, root.transform, 2.2f);
            ParticleFx.Spawn("FxEmbers", crater + Vector3.up * 0.4f, root.transform, 2.8f);
        }

        // ---------- primitive helpers (spheres / capsules / cylinders only) ----------

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

        static GameObject Cylinder(GameObject root, string name, Vector3 pos, Vector3 scale,
            Color color, float emission = 0f, string tex = null, float tiling = 1f)
            => Primitive(root, name, PrimitiveType.Cylinder, pos, scale, color, emission, tex, tiling);

        static GameObject Capsule(GameObject root, string name, Vector3 pos, Vector3 scale,
            Color color, float emission = 0f)
            => Primitive(root, name, PrimitiveType.Capsule, pos, scale, color, emission, null, 1f);

        static GameObject Sphere(GameObject root, string name, Vector3 pos, float scale,
            Color color, float emission = 0f, float squashY = 1f)
            => Primitive(root, name, PrimitiveType.Sphere, pos,
                new Vector3(scale, scale * squashY, scale), color, emission, null, 1f);

        static void SetTexture(GameObject go, string tex, float tiling)
        {
            var t = Resources.Load<Texture2D>(tex);
            if (t == null) return;
            var m = go.GetComponent<Renderer>().material;
            m.mainTexture = t;
            m.mainTextureScale = Vector2.one * tiling;
        }
    }
}
