using System.IO;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Moba.EditorTools
{
    /// Generates all prefabs and the main scene from code.
    /// Runs automatically on first project open; can be re-run via the MOBA menu.
    public static class MobaProjectBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string PrefabDir = "Assets/Resources/Prefabs";
        const string FxDir = "Assets/Resources/Prefabs/Fx";

        [InitializeOnLoadMethod]
        static void AutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath))
                {
                    BuildAll();
                    if (!Application.isBatchMode)
                        EditorSceneManager.OpenScene(ScenePath);
                }
                // GlobalObjectIdHash can't be computed in the same session that created the
                // assets (their GlobalObjectId isn't valid yet), so fix it on the next load.
                if (AnyPrefabHashMissing())
                    RefreshGlobalObjectIdHashes();
            };
        }

        [MenuItem("MOBA/Rebuild All (scene + prefabs)")]
        public static void BuildAll()
        {
            BuildPrefabs();
            BuildScene(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Hero.prefab"));
            RefreshGlobalObjectIdHashes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Moba] Scene and prefabs generated.");
        }

        [MenuItem("MOBA/Rebuild Prefabs Only")]
        public static void RebuildPrefabsOnly()
        {
            BuildPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("[Moba] Prefabs rebuilt. Run hash fix afterwards.");
        }

        static void BuildPrefabs()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(FxDir);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            MakeAllParticleMats();

            BuildHeroPrefab();
            BuildMinionPrefab();
            BuildTowerPrefab();
            BuildBasePrefab();
            BuildProjectilePrefab();
            BuildLavaPoolPrefab();
            BuildFxPrefabs();
        }

        // ============================== PREFABS ==============================

        static GameObject BuildHeroPrefab()
        {
            var root = new GameObject("Hero");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            // ---- shared mage figure: robe tiers, torso, head (no boxes anywhere) ----
            var robe1 = AddPart(visual, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f),
                new Vector3(1.05f, 0.3f, 1.05f));
            robe1.AddComponent<TeamTint>();
            var robe2 = AddPart(visual, PrimitiveType.Cylinder, new Vector3(0f, 0.78f, 0f),
                new Vector3(0.85f, 0.26f, 0.85f));
            robe2.AddComponent<TeamTint>();
            var torso = AddPart(visual, PrimitiveType.Capsule, new Vector3(0f, 1.25f, 0f),
                new Vector3(0.72f, 0.45f, 0.72f));
            torso.AddComponent<TeamTint>();
            foreach (int sx in new[] { -1, 1 }) // shoulders
            {
                var sh = AddPart(visual, PrimitiveType.Sphere,
                    new Vector3(sx * 0.42f, 1.52f, 0f), Vector3.one * 0.34f);
                sh.AddComponent<TeamTint>();
            }
            var head = AddPart(visual, PrimitiveType.Sphere, new Vector3(0f, 1.95f, 0f),
                Vector3.one * 0.62f);
            SetColor(head, new Color(0.87f, 0.74f, 0.62f));
            foreach (int sx in new[] { -1, 1 }) // glowing eyes mark the facing direction
            {
                var eye = AddPart(visual, PrimitiveType.Sphere,
                    new Vector3(sx * 0.13f, 2.0f, 0.26f), Vector3.one * 0.1f);
                SetColor(eye, new Color(0.9f, 0.95f, 1f), 1.8f);
            }
            foreach (int sx in new[] { -1, 1 }) // arms reaching towards the staff
            {
                var arm = AddPart(visual, PrimitiveType.Capsule,
                    new Vector3(sx * 0.48f, 1.22f, 0.14f), new Vector3(0.17f, 0.3f, 0.17f));
                arm.transform.localRotation = Quaternion.Euler(30f, 0f, sx * -16f);
                arm.AddComponent<TeamTint>();
                var hand = AddPart(visual, PrimitiveType.Sphere,
                    new Vector3(sx * 0.55f, 0.95f, 0.3f), Vector3.one * 0.17f);
                SetColor(hand, new Color(0.87f, 0.74f, 0.62f));
            }
            var belt = AddPart(visual, PrimitiveType.Cylinder, new Vector3(0f, 0.98f, 0f),
                new Vector3(0.78f, 0.05f, 0.78f));
            SetColor(belt, new Color(0.5f, 0.36f, 0.18f), 0.4f);

            // ---- Xardaras: fire mage (wizard hat, cyan orb, fire staff) ----
            var kx = new GameObject("KindX");
            kx.transform.SetParent(visual.transform, false);
            var hatBrim = AddPart(kx, PrimitiveType.Cylinder, new Vector3(0f, 2.22f, 0f),
                new Vector3(0.95f, 0.06f, 0.95f));
            SetColor(hatBrim, new Color(0.2f, 0.25f, 0.62f));
            var hatTop = AddPart(kx, PrimitiveType.Cylinder, new Vector3(0f, 2.46f, 0f),
                new Vector3(0.46f, 0.24f, 0.46f));
            SetColor(hatTop, new Color(0.24f, 0.3f, 0.7f));
            var hatMid = AddPart(kx, PrimitiveType.Cylinder, new Vector3(0f, 2.68f, 0f),
                new Vector3(0.28f, 0.16f, 0.28f));
            SetColor(hatMid, new Color(0.26f, 0.32f, 0.74f));
            var hatTip = AddPart(kx, PrimitiveType.Sphere, new Vector3(0f, 2.88f, 0f),
                Vector3.one * 0.2f);
            SetColor(hatTip, new Color(0.55f, 0.8f, 1f), 1.6f);
            var beard = AddPart(kx, PrimitiveType.Capsule, new Vector3(0f, 1.62f, 0.2f),
                new Vector3(0.34f, 0.26f, 0.22f));
            SetColor(beard, new Color(0.82f, 0.82f, 0.8f));
            var orbX = AddPart(kx, PrimitiveType.Sphere, new Vector3(0.62f, 1.8f, 0.1f),
                Vector3.one * 0.28f);
            SetColor(orbX, new Color(0.45f, 0.8f, 1f), 1.8f);
            AddIdle(orbX, 0.14f, 2.4f, 90f, 0.08f);
            BuildStaff(kx, new Color(0.4f, 0.26f, 0.16f), new Color(1f, 0.55f, 0.15f));

            // ---- Belial: dark mage (curved horns, cloak, red orb) ----
            var kb = new GameObject("KindB");
            kb.transform.SetParent(visual.transform, false);
            foreach (int sx in new[] { -1, 1 })
            {
                var horn = AddPart(kb, PrimitiveType.Capsule, new Vector3(sx * 0.3f, 2.42f, 0f),
                    new Vector3(0.14f, 0.3f, 0.14f));
                horn.transform.localRotation = Quaternion.Euler(0f, 0f, sx * -26f);
                SetColor(horn, new Color(0.42f, 0.12f, 0.12f));
                var hornTip = AddPart(kb, PrimitiveType.Sphere,
                    new Vector3(sx * 0.43f, 2.66f, 0f), Vector3.one * 0.13f);
                SetColor(hornTip, new Color(1f, 0.35f, 0.2f), 1.8f);
            }
            var cloak = AddPart(kb, PrimitiveType.Capsule, new Vector3(0f, 1.1f, -0.42f),
                new Vector3(0.95f, 0.85f, 0.22f));
            SetColor(cloak, new Color(0.16f, 0.08f, 0.14f));
            var orbB = AddPart(kb, PrimitiveType.Sphere, new Vector3(0.62f, 1.8f, 0.1f),
                Vector3.one * 0.3f);
            SetColor(orbB, new Color(0.9f, 0.15f, 0.3f), 2f);
            AddIdle(orbB, 0.16f, 2f, -110f, 0.1f);
            BuildStaff(kb, new Color(0.12f, 0.08f, 0.12f), new Color(0.7f, 0.2f, 1f));
            kb.SetActive(false);

            // ---- Adanos: water mage (ice crown, frost orbs, water staff) ----
            var ka = new GameObject("KindA");
            ka.transform.SetParent(visual.transform, false);
            for (int i = 0; i < 5; i++) // crown of ice spikes
            {
                float a = (i - 2) * 0.5f;
                var spike = AddPart(ka, PrimitiveType.Capsule,
                    new Vector3(Mathf.Sin(a) * 0.3f, 2.38f + (i % 2 == 0 ? 0.1f : 0f),
                        Mathf.Cos(a) * 0.12f - 0.02f),
                    new Vector3(0.09f, 0.22f, 0.09f));
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, -a * 30f);
                SetColor(spike, new Color(0.75f, 0.92f, 1f), 1.2f);
            }
            var hood = AddPart(ka, PrimitiveType.Sphere, new Vector3(0f, 2.05f, -0.08f),
                new Vector3(0.72f, 0.6f, 0.66f));
            SetColor(hood, new Color(0.2f, 0.45f, 0.66f));
            var orbA = AddPart(ka, PrimitiveType.Sphere, new Vector3(0.62f, 1.8f, 0.1f),
                Vector3.one * 0.28f);
            SetColor(orbA, new Color(0.45f, 0.9f, 1f), 2f);
            AddIdle(orbA, 0.15f, 2.6f, 120f, 0.1f);
            var droplet = AddPart(ka, PrimitiveType.Sphere, new Vector3(-0.62f, 2.05f, 0.05f),
                Vector3.one * 0.16f);
            SetColor(droplet, new Color(0.6f, 0.95f, 1f), 1.6f);
            AddIdle(droplet, 0.2f, 3.2f, -160f, 0.12f);
            BuildStaff(ka, new Color(0.16f, 0.3f, 0.42f), new Color(0.35f, 0.85f, 1f));
            ka.SetActive(false);

            AddHealthBar(root, 2.6f, 1.7f);

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<ClientAuthoritativeNetworkTransform>();
            ConfigureTransformSync(nt);
            root.AddComponent<Hero>();
            var anim = root.AddComponent<UnitAnimator>();
            anim.body = visual.transform;
            return SavePrefab(root, "Hero");
        }

        static void BuildMinionPrefab()
        {
            var root = new GameObject("Minion");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            // little lava imp
            var body = AddPart(visual, PrimitiveType.Sphere, new Vector3(0f, 0.52f, 0f),
                new Vector3(0.78f, 0.66f, 0.78f));
            body.AddComponent<TeamTint>();
            foreach (int sx in new[] { -1, 1 }) // stubby legs
            {
                var leg = AddPart(visual, PrimitiveType.Capsule,
                    new Vector3(sx * 0.2f, 0.16f, 0f), new Vector3(0.16f, 0.16f, 0.16f));
                SetColor(leg, new Color(0.2f, 0.16f, 0.18f));
            }
            foreach (int sx in new[] { -1, 1 }) // little arms
            {
                var arm = AddPart(visual, PrimitiveType.Capsule,
                    new Vector3(sx * 0.42f, 0.6f, 0.1f), new Vector3(0.13f, 0.2f, 0.13f));
                arm.transform.localRotation = Quaternion.Euler(35f, 0f, sx * -30f);
                arm.AddComponent<TeamTint>();
            }
            var head = AddPart(visual, PrimitiveType.Sphere, new Vector3(0f, 1.05f, 0.08f),
                Vector3.one * 0.46f);
            SetColor(head, new Color(0.22f, 0.18f, 0.2f));
            foreach (int sx in new[] { -1, 1 }) // tiny horns
            {
                var horn = AddPart(visual, PrimitiveType.Capsule,
                    new Vector3(sx * 0.15f, 1.32f, 0.02f), new Vector3(0.07f, 0.12f, 0.07f));
                horn.transform.localRotation = Quaternion.Euler(0f, 0f, sx * -22f);
                SetColor(horn, new Color(0.5f, 0.2f, 0.16f));
            }
            foreach (int sx in new[] { -1, 1 }) // glowing ember eyes
            {
                var eye = AddPart(visual, PrimitiveType.Sphere,
                    new Vector3(sx * 0.1f, 1.1f, 0.28f), Vector3.one * 0.1f);
                SetColor(eye, new Color(1f, 0.5f, 0.1f), 2.5f);
            }
            var mouth = AddPart(visual, PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0.3f),
                new Vector3(0.14f, 0.07f, 0.08f));
            SetColor(mouth, new Color(1f, 0.4f, 0.1f), 2f);
            // ember tail
            for (int i = 0; i < 3; i++)
            {
                var seg = AddPart(visual, PrimitiveType.Sphere,
                    new Vector3(0f, 0.45f - i * 0.1f, -0.42f - i * 0.18f),
                    Vector3.one * (0.18f - i * 0.04f));
                SetColor(seg, new Color(1f, 0.45f + 0.15f * i, 0.12f), 1.4f);
            }

            AddHealthBar(root, 1.7f, 1.1f);

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            ConfigureTransformSync(nt);
            root.AddComponent<Minion>();
            var anim = root.AddComponent<UnitAnimator>();
            anim.body = visual.transform;
            SavePrefab(root, "Minion");
        }

        static void BuildTowerPrefab()
        {
            var root = new GameObject("Tower");
            var baseTier = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f),
                new Vector3(5.2f, 0.25f, 5.2f));
            SetColor(baseTier, new Color(0.38f, 0.34f, 0.38f), 0f, "Textures/obsidian", 2f);
            var teamRing = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f),
                new Vector3(4.4f, 0.1f, 4.4f));
            teamRing.AddComponent<TeamTint>();
            for (int i = 0; i < 4; i++) // corner pillars
            {
                float a = (i + 0.5f) / 4f * Mathf.PI * 2f;
                var p = AddPart(root, PrimitiveType.Cylinder,
                    new Vector3(Mathf.Cos(a) * 1.75f, 1.4f, Mathf.Sin(a) * 1.75f),
                    new Vector3(0.7f, 1.2f, 0.7f));
                SetColor(p, new Color(0.42f, 0.38f, 0.44f), 0f, "Textures/obsidian", 1.5f);
                var cap = AddPart(root, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(a) * 1.75f, 2.75f, Mathf.Sin(a) * 1.75f),
                    Vector3.one * 0.4f);
                SetColor(cap, new Color(1f, 0.5f, 0.15f), 1.6f);
            }
            var spire = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 2.6f, 0f),
                new Vector3(2.1f, 1.5f, 2.1f));
            SetColor(spire, new Color(0.45f, 0.4f, 0.46f), 0f, "Textures/obsidian", 2f);
            var lavaRing = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 4.35f, 0f),
                new Vector3(3.2f, 0.12f, 3.2f));
            SetColor(lavaRing, new Color(1f, 0.55f, 0.2f), 1.5f, "Textures/lava", 1f);
            AddIdle(lavaRing, 0.08f, 1.4f, 70f, 0f);
            var orb = AddPart(root, PrimitiveType.Sphere, new Vector3(0f, 5.2f, 0f),
                Vector3.one * 1.25f);
            orb.AddComponent<TeamTint>();
            AddIdle(orb, 0.12f, 1.6f, 45f, 0.06f);

            AddHealthBar(root, 6.1f, 2.2f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<Tower>();
            SavePrefab(root, "Tower");
        }

        static void BuildBasePrefab()
        {
            var root = new GameObject("BaseCore");
            var podium = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f),
                new Vector3(9.4f, 0.3f, 9.4f));
            SetColor(podium, new Color(0.38f, 0.34f, 0.38f), 0f, "Textures/obsidian", 3f);
            var step = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.7f, 0f),
                new Vector3(6.6f, 0.2f, 6.6f));
            SetColor(step, new Color(0.44f, 0.4f, 0.46f), 0f, "Textures/obsidian", 2f);
            for (int i = 0; i < 6; i++) // ring of pillars
            {
                float a = i / 6f * Mathf.PI * 2f;
                var p = AddPart(root, PrimitiveType.Cylinder,
                    new Vector3(Mathf.Cos(a) * 3.9f, 1.4f, Mathf.Sin(a) * 3.9f),
                    new Vector3(0.8f, 1.1f, 0.8f));
                SetColor(p, new Color(0.42f, 0.38f, 0.44f), 0f, "Textures/obsidian", 1.5f);
                var cap = AddPart(root, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(a) * 3.9f, 2.7f, Mathf.Sin(a) * 3.9f),
                    Vector3.one * 0.5f);
                cap.AddComponent<TeamTint>();
            }
            // the great crystal
            var crystal = AddPart(root, PrimitiveType.Sphere, new Vector3(0f, 3.2f, 0f),
                new Vector3(1.7f, 2.8f, 1.7f));
            crystal.AddComponent<TeamTint>();
            AddIdle(crystal, 0.2f, 1.2f, 30f, 0.05f);
            // orbiting shards
            var orbit = new GameObject("Orbit");
            orbit.transform.SetParent(root.transform, false);
            orbit.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            AddIdle(orbit, 0.12f, 1.6f, 55f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                var shard = AddPart(orbit, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(a) * 2.7f, 0f, Mathf.Sin(a) * 2.7f),
                    new Vector3(0.45f, 1f, 0.45f));
                shard.AddComponent<TeamTint>();
            }

            AddHealthBar(root, 5.8f, 3f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<BaseCore>();
            SavePrefab(root, "BaseCore");
        }

        static void BuildProjectilePrefab()
        {
            var root = new GameObject("Projectile");
            AddPart(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one);
            // glowing spark trail (tinted to team color at runtime)
            var trail = AddPs(root, "Trail", _matSpark);
            var main = trail.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var em = trail.emission;
            em.rateOverTime = 45f;
            var sh = trail.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.3f;
            FadeOut(trail);

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            ConfigureTransformSync(nt);
            root.AddComponent<Projectile>();
            SavePrefab(root, "Projectile");
        }

        static void BuildLavaPoolPrefab()
        {
            var root = new GameObject("LavaPool");
            var disc = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f),
                new Vector3(1f, 0.06f, 1f));
            SetColor(disc, new Color(0.45f, 0.1f, 0.05f), 0.5f, "Textures/lava", 1f);
            root.AddComponent<NetworkObject>();
            var lp = root.AddComponent<LavaPool>();
            lp.disc = disc.transform;
            SavePrefab(root, "LavaPool");
        }

        // ============================== PARTICLE FX ==============================

        static Material _matFlame, _matSpark, _matWisp, _matDrop, _matSmoke, _matRune;

        static Material MakeParticleMat(string name, string texFile, bool additive)
        {
            Directory.CreateDirectory("Assets/FxMaterials");
            string path = "Assets/FxMaterials/" + name + ".mat";
            var shader = Shader.Find(additive
                ? "Legacy Shaders/Particles/Additive"
                : "Legacy Shaders/Particles/Alpha Blended");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            else mat.shader = shader;
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FxTextures/" + texFile);
            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        static void MakeAllParticleMats()
        {
            _matFlame = MakeParticleMat("MatFlame", "flame.png", true);
            _matSpark = MakeParticleMat("MatSpark", "spark.png", true);
            _matWisp = MakeParticleMat("MatWisp", "wisp.png", true);
            _matDrop = MakeParticleMat("MatDrop", "drop.png", true);
            _matSmoke = MakeParticleMat("MatSmoke", "smoke.png", false);
            _matRune = MakeParticleMat("MatRune", "rune_ring.png", true);
        }

        static ParticleSystem AddPs(GameObject parent, string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return ps;
        }

        static void OneShot(ParticleSystem ps, int count, float speedMin, float speedMax,
            float sizeMin, float sizeMax, float lifeMin, float lifeMax, Color c1, Color c2,
            float gravity = 0f, float orbital = 0f)
        {
            var main = ps.main;
            main.loop = false;
            main.duration = 0.7f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count + 10;
            main.gravityModifier = gravity;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.5f;
            if (orbital != 0f)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.orbitalY = new ParticleSystem.MinMaxCurve(orbital);
            }
            FadeOut(ps);
        }

        static void BuildBlast(string name, System.Action<GameObject> fill)
        {
            var root = new GameObject(name);
            root.AddComponent<TimedDestroy>(); // children may self-destroy, the root must too
            fill(root);
            string path = FxDir + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        static void BuildFxPrefabs()
        {
            // elemental ult blasts
            BuildBlast("FxFireBlast", root =>
            {
                OneShot(AddPs(root, "Flames", _matFlame), 26, 3f, 8f, 0.7f, 1.6f, 0.45f, 0.85f,
                    new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.3f));
                OneShot(AddPs(root, "Sparks", _matSpark), 40, 6f, 13f, 0.15f, 0.4f, 0.35f, 0.7f,
                    new Color(1f, 0.8f, 0.3f), Color.white);
                OneShot(AddPs(root, "Smoke", _matSmoke), 10, 1f, 2.5f, 1.6f, 2.8f, 0.9f, 1.5f,
                    new Color(0.25f, 0.2f, 0.18f, 0.5f), new Color(0.4f, 0.32f, 0.28f, 0.4f));
            });
            BuildBlast("FxDarkBlast", root =>
            {
                OneShot(AddPs(root, "Wisps", _matWisp), 24, 1.5f, 4f, 0.8f, 1.7f, 0.7f, 1.2f,
                    new Color(0.65f, 0.25f, 1f), new Color(0.95f, 0.3f, 0.5f), 0f, 5f);
                OneShot(AddPs(root, "Sparks", _matSpark), 30, 5f, 10f, 0.12f, 0.35f, 0.4f, 0.7f,
                    new Color(0.9f, 0.3f, 0.6f), new Color(0.7f, 0.4f, 1f));
            });
            BuildBlast("FxWaterBlast", root =>
            {
                OneShot(AddPs(root, "Drops", _matDrop), 34, 4f, 9f, 0.3f, 0.7f, 0.5f, 0.9f,
                    new Color(0.35f, 0.8f, 1f), new Color(0.8f, 0.97f, 1f), 0.8f);
                OneShot(AddPs(root, "Mist", _matSmoke), 8, 1f, 2f, 1.4f, 2.4f, 0.8f, 1.3f,
                    new Color(0.5f, 0.8f, 0.95f, 0.45f), new Color(0.7f, 0.9f, 1f, 0.35f));
            });
            // generic tintable spark burst (hits, deaths, blink)
            BuildBlast("FxSparkBurst", root =>
            {
                OneShot(AddPs(root, "Sparks", _matSpark), 22, 4f, 9f, 0.14f, 0.36f, 0.3f, 0.6f,
                    Color.white, new Color(1f, 0.9f, 0.6f));
            });

            // spinning rune circle (RingFx scales/fades it)
            BuildBlast("FxRing", root =>
            {
                root.AddComponent<RingFx>();
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var col = quad.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                quad.transform.SetParent(root.transform, false);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.GetComponent<Renderer>().sharedMaterial = _matRune;
            });

            BuildFx("FxSmoke", ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.25f, 0.2f, 0.2f, 0.4f), new Color(0.12f, 0.1f, 0.1f, 0.3f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 80;
                var em = ps.emission;
                em.rateOverTime = 8f;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 14f;
                sh.radius = 1.2f;
                FadeOut(ps);
            }, _matSmoke);

            BuildFx("FxEmbers", ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.55f, 0.15f, 1f), new Color(1f, 0.85f, 0.3f, 1f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 120;
                var em = ps.emission;
                em.rateOverTime = 16f;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 28f;
                sh.radius = 1.4f;
                FadeOut(ps);
            }, _matSpark);

            BuildFx("FxSparkles", ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.6f, 0.9f, 1f, 1f), new Color(1f, 1f, 1f, 1f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 60;
                var em = ps.emission;
                em.rateOverTime = 12f;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 32f;
                sh.radius = 2.2f;
                FadeOut(ps);
            }, _matSpark);

            BuildFx("FxAsh", ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.6f, 0.3f, 0.6f), new Color(0.6f, 0.5f, 0.5f, 0.4f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 300;
                main.gravityModifier = 0.015f;
                var em = ps.emission;
                em.rateOverTime = 26f;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(84f, 32f, 1f);
                FadeOut(ps);
            }, _matSmoke);

            BuildFx("FxBurstPs", ps =>
            {
                var main = ps.main;
                main.loop = false;
                main.duration = 0.6f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.4f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.55f, 0.15f, 1f), new Color(1f, 0.9f, 0.4f, 1f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 80;
                main.stopAction = ParticleSystemStopAction.Destroy;
                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.5f;
                FadeOut(ps);
            }, _matSpark);
        }

        static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        static void BuildFx(string name, System.Action<ParticleSystem> configure,
            Material mat = null)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat != null
                ? mat
                : AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            configure(ps);
            string path = FxDir + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        // ============================== SCENE ==============================

        static void BuildScene(GameObject heroPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraRig>();
            camGo.transform.position = new Vector3(0f, 17f, -24f);
            camGo.transform.rotation = Quaternion.Euler(38f, 0f, 0f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.82f, 0.62f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            var netGo = new GameObject("NetworkManager");
            var nm = netGo.AddComponent<NetworkManager>();
            var utp = netGo.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null)
                nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.PlayerPrefab = heroPrefab;
            nm.NetworkConfig.TickRate = 30;

            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<NetworkObject>();
            gmGo.AddComponent<GameManager>();

            var bootGo = new GameObject("Bootstrap");
            bootGo.AddComponent<GameBootstrap>();
            bootGo.AddComponent<HudController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ============================== BUILD ==============================

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Mini Moba";
            PlayerSettings.companyName = "Moba";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
        }

        [MenuItem("MOBA/Build macOS Player")]
        public static void BuildMacPlayer()
        {
            ApplyPlayerSettings();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/MiniMoba.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Moba] Build: " + report.summary.result +
                      ", errors: " + report.summary.totalErrors);
        }

        /// Mobile build: runs in any phone browser, joins desktop hosts over WebSockets.
        [MenuItem("MOBA/Build WebGL (mobile)")]
        public static void BuildWebGL()
        {
            ApplyPlayerSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.runInBackground = true;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Moba] WebGL build: " + report.summary.result +
                      ", errors: " + report.summary.totalErrors);
        }

        // ============================== HASHES ==============================

        static FieldInfo HashField => typeof(NetworkObject).GetField("GlobalObjectIdHash",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        static bool AnyPrefabHashMissing()
        {
            if (!Directory.Exists(PrefabDir)) return false;
            foreach (var file in Directory.GetFiles(PrefabDir, "*.prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                var no = go != null ? go.GetComponent<NetworkObject>() : null;
                if (no != null && (uint)HashField.GetValue(no) == 0)
                    return true;
            }
            return false;
        }

        /// NetworkObject.GlobalObjectIdHash is normally produced by editor validation callbacks,
        /// which don't fire for assets created from code. Recompute it on the saved (persistent)
        /// assets via the internal OnValidate, then persist the result.
        [MenuItem("MOBA/Fix Network Hashes")]
        public static void RefreshGlobalObjectIdHashes()
        {
            var onValidate = typeof(NetworkObject).GetMethod("OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (onValidate == null)
            {
                Debug.LogError("[Moba] NetworkObject.OnValidate not found, network hashes not set!");
                return;
            }

            foreach (var file in Directory.GetFiles(PrefabDir, "*.prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                var no = go != null ? go.GetComponent<NetworkObject>() : null;
                if (no == null) continue;
                onValidate.Invoke(no, null);
                uint hash = (uint)HashField.GetValue(no);
                // SetDirty + SaveAssets is not enough to flush prefab assets in batch mode
                PrefabUtility.SavePrefabAsset(go);
                Debug.Log("[Moba] " + go.name + " GlobalObjectIdHash=" + hash);
            }
            AssetDatabase.SaveAssets();

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath && File.Exists(ScenePath))
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var no in root.GetComponentsInChildren<NetworkObject>(true))
                    onValidate.Invoke(no, null);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Moba] Network hashes refreshed.");
        }

        // ============================== HELPERS ==============================

        static GameObject AddPart(GameObject root, PrimitiveType type, Vector3 localPos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            return go;
        }

        static void SetColor(GameObject go, Color c, float emission = 0f,
            string texture = "", float tiling = 1f)
        {
            var ac = go.AddComponent<AutoColor>();
            ac.color = c;
            ac.emission = emission;
            ac.textureResource = texture;
            ac.tiling = tiling;
        }

        static void BuildStaff(GameObject parent, Color wood, Color tip)
        {
            var staff = AddPart(parent, PrimitiveType.Cylinder, new Vector3(-0.55f, 1.05f, 0.15f),
                new Vector3(0.08f, 0.95f, 0.08f));
            SetColor(staff, wood);
            var orb = AddPart(parent, PrimitiveType.Sphere, new Vector3(-0.55f, 2.1f, 0.15f),
                Vector3.one * 0.3f);
            SetColor(orb, tip, 2f);
            AddIdle(orb, 0.05f, 3f, 0f, 0.12f);
        }

        static void AddIdle(GameObject go, float amp, float speed, float spin, float pulse)
        {
            var f = go.AddComponent<IdleFloat>();
            f.amplitude = amp;
            f.speed = speed;
            f.spin = spin;
            f.pulse = pulse;
        }

        static void AddHealthBar(GameObject root, float height, float width)
        {
            var barRoot = new GameObject("HealthBar");
            barRoot.transform.SetParent(root.transform, false);
            barRoot.transform.localPosition = new Vector3(0f, height, 0f);
            var bar = barRoot.AddComponent<HealthBar>();

            var bg = MakeQuad(barRoot.transform, new Vector3(0f, 0f, 0f),
                new Vector3(width, 0.22f, 1f));
            bar.bgRenderer = bg.GetComponent<Renderer>();

            var pivot = new GameObject("FillPivot");
            pivot.transform.SetParent(barRoot.transform, false);
            pivot.transform.localPosition = new Vector3(-width / 2f, 0f, -0.01f);
            bar.fillPivot = pivot.transform;

            var fill = MakeQuad(pivot.transform, new Vector3(width / 2f, 0f, 0f),
                new Vector3(width - 0.05f, 0.15f, 1f));
            bar.fillRenderer = fill.GetComponent<Renderer>();
        }

        static GameObject MakeQuad(Transform parent, Vector3 localPos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            return go;
        }

        static void ConfigureTransformSync(NetworkTransform nt)
        {
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.Interpolate = true;
        }

        static GameObject SavePrefab(GameObject root, string name)
        {
            string path = PrefabDir + "/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
