using Unity.Netcode;
using UnityEngine;

namespace Moba
{
    /// Scene entry point: resets static state, builds the visual map, registers network prefabs.
    public class GameBootstrap : MonoBehaviour
    {
        static bool _prefabsRegistered;

        void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            UnitBase.All.Clear();
            Hero.Local = null;
            MapBuilder.Build();
        }

        void Start()
        {
            // NetworkManager survives scene reloads (DontDestroyOnLoad), register only once per run
            if (_prefabsRegistered || NetworkManager.Singleton == null) return;
            _prefabsRegistered = true;
            RegisterPrefab(GamePrefabs.Minion);
            RegisterPrefab(GamePrefabs.Tower);
            RegisterPrefab(GamePrefabs.BaseCore);
            RegisterPrefab(GamePrefabs.Projectile);
            RegisterPrefab(Resources.Load<GameObject>("Prefabs/LavaPool"));
        }

        static void RegisterPrefab(GameObject prefab)
        {
            // the auto-generated DefaultNetworkPrefabs list may already include it
            if (prefab != null && !NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(prefab))
                NetworkManager.Singleton.AddNetworkPrefab(prefab);
        }
    }
}
