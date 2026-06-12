using Unity.Netcode;
using UnityEngine;

namespace Moba
{
    /// In-scene networked singleton: match state, structure spawning, minion waves, win handling.
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance;

        public NetworkVariable<bool> GameStarted = new NetworkVariable<bool>(false);
        public NetworkVariable<byte> Winner = new NetworkVariable<byte>(0);
        public NetworkVariable<float> StartTime = new NetworkVariable<float>(0f);

        const float WaveInterval = 25f;
        const int MinionsPerWave = 4;
        const int MaxMinionsPerTeam = 30;

        float _nextWave;
        float _nextEruption;

        public static bool Playing =>
            Instance != null && Instance.IsSpawned &&
            Instance.GameStarted.Value && Instance.Winner.Value == 0;

        public float MatchTime =>
            !IsSpawned || !GameStarted.Value ? 0f
            : NetworkManager.ServerTime.TimeAsFloat - StartTime.Value;

        void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            ServerSpawnStructures();
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            CheckStart();
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!IsServer || !IsSpawned || !Playing) return;
            float now = NetworkManager.ServerTime.TimeAsFloat;
            if (now >= _nextWave)
            {
                _nextWave = now + WaveInterval;
                SpawnWave(Team.Blue);
                SpawnWave(Team.Red);
            }
            if (now >= _nextEruption)
            {
                _nextEruption = now + Random.Range(45f, 75f);
                ServerErupt();
            }
        }

        // ---- volcano eruptions ----

        void ServerErupt()
        {
            EruptionFxRpc();
            var lavaPrefab = Resources.Load<GameObject>("Prefabs/LavaPool");
            if (lavaPrefab == null) return;
            int pools = Random.Range(4, 7);
            for (int i = 0; i < pools; i++)
            {
                var pos = new Vector3(Random.Range(-22f, 22f), 0.02f, Random.Range(-10f, 10f));
                var go = Instantiate(lavaPrefab, pos, Quaternion.identity);
                var lp = go.GetComponent<LavaPool>();
                lp.PendingRadius = Random.Range(2.2f, 3.2f);
                lp.PendingWarn = 2.5f;
                lp.PendingLife = Random.Range(8f, 12f);
                go.GetComponent<NetworkObject>().Spawn(true);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        void EruptionFxRpc()
        {
            HudController.Warn("ИЗВЕРЖЕНИЕ ВУЛКАНА! Не стойте в лаве!", 4.5f);
            CameraRig.Shake(0.4f, 2.4f);
            MapBuilder.EruptVolcanoes();
        }

        // ---- server ----

        void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.ConnectedClientsIds.Count > 2)
            {
                NetworkManager.DisconnectClient(clientId);
                return;
            }
            CheckStart();
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (!GameStarted.Value || Winner.Value != 0) return;
            // remaining player wins by forfeit
            foreach (var u in UnitBase.All)
                if (u is Hero h && h.IsSpawned && h.OwnerClientId != clientId)
                {
                    Winner.Value = (byte)h.Team;
                    return;
                }
        }

        void CheckStart()
        {
            if (GameStarted.Value || NetworkManager.ConnectedClientsIds.Count < 2) return;
            GameStarted.Value = true;
            StartTime.Value = NetworkManager.ServerTime.TimeAsFloat;
            _nextWave = StartTime.Value + 3f;
            _nextEruption = StartTime.Value + 35f;
            Debug.Log("[Moba] Match started with 2 players");
        }

        void ServerSpawnStructures()
        {
            foreach (Team t in new[] { Team.Blue, Team.Red })
            {
                SpawnStructure(GamePrefabs.BaseCore, GameConstants.BasePos(t), t, 3200f);
                SpawnStructure(GamePrefabs.Tower, GameConstants.TowerPos(t), t, 2200f);
            }
        }

        void SpawnStructure(GameObject prefab, Vector3 pos, Team team, float hp)
        {
            if (prefab == null) return;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            var unit = go.GetComponent<UnitBase>();
            unit.PendingTeam = (byte)team;
            unit.PendingMaxHp = hp;
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        void SpawnWave(Team team)
        {
            int alive = 0;
            foreach (var u in UnitBase.All)
                if (u is Minion && u.Team == team && u.Alive) alive++;
            if (alive >= MaxMinionsPerTeam) return;

            float minutes = MatchTime / 60f;
            float hp = 280f + 30f * minutes;
            float dmg = 26f + 3f * minutes;
            float dirX = team == Team.Blue ? 1f : -1f;
            Vector3 origin = GameConstants.BasePos(team) + new Vector3(dirX * 4f, 0f, 0f);

            for (int i = 0; i < MinionsPerWave; i++)
            {
                Vector3 pos = origin + new Vector3(-dirX * (i % 2) * 1.2f, 0f, (i - 1.5f) * 1.3f);
                var go = Instantiate(GamePrefabs.Minion, pos,
                    Quaternion.LookRotation(new Vector3(dirX, 0f, 0f)));
                var m = go.GetComponent<Minion>();
                m.PendingTeam = (byte)team;
                m.PendingMaxHp = hp;
                m.PendingDamage = dmg;
                go.GetComponent<NetworkObject>().Spawn(true);
            }
        }

        public void ServerBaseDestroyed(Team destroyedTeam)
        {
            if (!IsServer || Winner.Value != 0) return;
            Winner.Value = (byte)GameConstants.Enemy(destroyedTeam);
        }

        public static void AwardXpAround(Team team, Vector3 pos, int xp)
        {
            foreach (var u in UnitBase.All)
                if (u is Hero h && h.Team == team && h.Alive &&
                    GameConstants.Dist2D(pos, h.Pos) < 13f)
                    h.ServerAddXp(xp);
        }
    }
}
