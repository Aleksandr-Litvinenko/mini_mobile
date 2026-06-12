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

            var body = AddPart(visual, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), Vector3.one);
            body.AddComponent<TeamTint>();
            var nose = AddPart(visual, PrimitiveType.Cube, new Vector3(0f, 1.15f, 0.42f),
                new Vector3(0.28f, 0.28f, 0.5f));
            SetColor(nose, new Color(0.85f, 0.85f, 0.8f));

            // ---- Xardaras: fire mage (hat, cyan orb, fire staff) ----
            var kx = new GameObject("KindX");
            kx.transform.SetParent(visual.transform, false);
            var hatBrim = AddPart(kx, PrimitiveType.Cylinder, new Vector3(0f, 1.95f, 0f),
                new Vector3(0.95f, 0.06f, 0.95f));
            SetColor(hatBrim, new Color(0.2f, 0.25f, 0.62f));
            var hatTop = AddPart(kx, PrimitiveType.Cylinder, new Vector3(0f, 2.25f, 0f),
                new Vector3(0.45f, 0.3f, 0.45f));
            SetColor(hatTop, new Color(0.24f, 0.3f, 0.7f));
            var hatTip = AddPart(kx, PrimitiveType.Sphere, new Vector3(0f, 2.62f, 0f),
                Vector3.one * 0.22f);
            SetColor(hatTip, new Color(0.55f, 0.8f, 1f), 1.6f);
            var orbX = AddPart(kx, PrimitiveType.Sphere, new Vector3(0.62f, 1.8f, 0.1f),
                Vector3.one * 0.28f);
            SetColor(orbX, new Color(0.45f, 0.8f, 1f), 1.8f);
            AddIdle(orbX, 0.14f, 2.4f, 90f, 0.08f);
            var staffX = AddPart(kx, PrimitiveType.Cylinder, new Vector3(-0.55f, 1.05f, 0.15f),
                new Vector3(0.08f, 0.95f, 0.08f));
            SetColor(staffX, new Color(0.4f, 0.26f, 0.16f));
            var staffTipX = AddPart(kx, PrimitiveType.Sphere, new Vector3(-0.55f, 2.1f, 0.15f),
                Vector3.one * 0.3f);
            SetColor(staffTipX, new Color(1f, 0.55f, 0.15f), 2f);
            AddIdle(staffTipX, 0.05f, 3f, 0f, 0.12f);

            // ---- Belial: dark mage (horns, cloak, red orb, dark staff) ----
            var kb = new GameObject("KindB");
            kb.transform.SetParent(visual.transform, false);
            foreach (int sx in new[] { -1, 1 })
            {
                var horn = AddPart(kb, PrimitiveType.Cube, new Vector3(sx * 0.32f, 2.05f, 0f),
                    new Vector3(0.14f, 0.5f, 0.14f));
                horn.transform.localRotation = Quaternion.Euler(0f, 0f, sx * -24f);
                SetColor(horn, new Color(0.42f, 0.12f, 0.12f));
                var hornTip = AddPart(kb, PrimitiveType.Sphere,
                    new Vector3(sx * 0.43f, 2.32f, 0f), Vector3.one * 0.12f);
                SetColor(hornTip, new Color(1f, 0.35f, 0.2f), 1.8f);
            }
            var cloak = AddPart(kb, PrimitiveType.Cube, new Vector3(0f, 1.05f, -0.38f),
                new Vector3(0.95f, 1.65f, 0.18f));
            SetColor(cloak, new Color(0.16f, 0.08f, 0.14f));
            var orbB = AddPart(kb, PrimitiveType.Sphere, new Vector3(0.62f, 1.8f, 0.1f),
                Vector3.one * 0.3f);
            SetColor(orbB, new Color(0.9f, 0.15f, 0.3f), 2f);
            AddIdle(orbB, 0.16f, 2f, -110f, 0.1f);
            var staffB = AddPart(kb, PrimitiveType.Cylinder, new Vector3(-0.55f, 1.05f, 0.15f),
                new Vector3(0.08f, 0.95f, 0.08f));
            SetColor(staffB, new Color(0.12f, 0.08f, 0.12f));
            var staffTipB = AddPart(kb, PrimitiveType.Sphere, new Vector3(-0.55f, 2.1f, 0.15f),
                Vector3.one * 0.3f);
            SetColor(staffTipB, new Color(0.7f, 0.2f, 1f), 2f);
            AddIdle(staffTipB, 0.05f, 3f, 0f, 0.12f);
            kb.SetActive(false);

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

            var body = AddPart(visual, PrimitiveType.Capsule, new Vector3(0f, 0.55f, 0f),
                new Vector3(0.6f, 0.5f, 0.6f));
            body.AddComponent<TeamTint>();
            var head = AddPart(visual, PrimitiveType.Sphere, new Vector3(0f, 1.05f, 0.1f),
                Vector3.one * 0.4f);
            SetColor(head, new Color(0.22f, 0.18f, 0.2f));
            foreach (int sx in new[] { -1, 1 }) // glowing ember eyes
            {
                var eye = AddPart(visual, PrimitiveType.Sphere,
                    new Vector3(sx * 0.09f, 1.1f, 0.27f), Vector3.one * 0.09f);
                SetColor(eye, new Color(1f, 0.5f, 0.1f), 2.5f);
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
            var pillar = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 2f, 0f),
                new Vector3(1.9f, 2f, 1.9f));
            SetColor(pillar, new Color(0.4f, 0.36f, 0.4f), 0f, "Textures/obsidian", 2f);
            var ring = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f),
                new Vector3(2.6f, 0.25f, 2.6f));
            ring.AddComponent<TeamTint>();
            var lavaRing = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 3.6f, 0f),
                new Vector3(2.1f, 0.12f, 2.1f));
            SetColor(lavaRing, new Color(1f, 0.55f, 0.2f), 1.5f, "Textures/lava", 1f);
            var orb = AddPart(root, PrimitiveType.Sphere, new Vector3(0f, 4.4f, 0f),
                Vector3.one * 1.1f);
            orb.AddComponent<TeamTint>();
            AddIdle(orb, 0.1f, 1.6f, 45f, 0.06f);

            AddHealthBar(root, 5.2f, 2.2f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<Tower>();
            SavePrefab(root, "Tower");
        }

        static void BuildBasePrefab()
        {
            var root = new GameObject("BaseCore");
            var podium = AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f),
                new Vector3(4.6f, 1f, 4.6f));
            SetColor(podium, new Color(0.38f, 0.34f, 0.38f), 0f, "Textures/obsidian", 3f);
            var crystal = AddPart(root, PrimitiveType.Sphere, new Vector3(0f, 3f, 0f),
                Vector3.one * 1.9f);
            crystal.AddComponent<TeamTint>();
            AddIdle(crystal, 0.18f, 1.2f, 30f, 0.05f);

            AddHealthBar(root, 4.6f, 3f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<BaseCore>();
            SavePrefab(root, "BaseCore");
        }

        static void BuildProjectilePrefab()
        {
            var root = new GameObject("Projectile");
            AddPart(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one);
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

        static void BuildFxPrefabs()
        {
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
            });

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
            });

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
            });

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
                sh.scale = new Vector3(72f, 27f, 1f);
                FadeOut(ps);
            });

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
            });
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

        static void BuildFx(string name, System.Action<ParticleSystem> configure)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial =
                AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
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

        [MenuItem("MOBA/Build macOS Player")]
        public static void BuildMacPlayer()
        {
            PlayerSettings.productName = "MobaGame";
            PlayerSettings.companyName = "Moba";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/MobaGame.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Moba] Build: " + report.summary.result +
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
