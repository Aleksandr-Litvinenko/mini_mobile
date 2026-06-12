using Unity.Netcode;
using UnityEngine;

namespace Moba
{
    /// Lava pool spawned by volcano eruptions. Warning phase (dark pulsing circle),
    /// then active lava dealing damage per tick to heroes and minions standing on it.
    public class LavaPool : NetworkBehaviour
    {
        public NetworkVariable<float> Radius = new NetworkVariable<float>(2.5f);
        public NetworkVariable<float> ActiveAt = new NetworkVariable<float>(0f);
        public NetworkVariable<float> DespawnAt = new NetworkVariable<float>(0f);

        // set by the spawner before Spawn()
        [HideInInspector] public float PendingRadius = 2.5f;
        [HideInInspector] public float PendingWarn = 2.5f;
        [HideInInspector] public float PendingLife = 10f;

        public Transform disc;

        const float DamagePerTick = 30f; // every 0.5s -> 60 dps

        float _nextTick;
        bool _embersSpawned;
        Renderer _discR;

        float Now => NetworkManager != null ? NetworkManager.ServerTime.TimeAsFloat : 0f;
        bool IsActivePhase => IsSpawned && Now >= ActiveAt.Value;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Radius.Value = PendingRadius;
                ActiveAt.Value = Now + PendingWarn;
                DespawnAt.Value = ActiveAt.Value + PendingLife;
            }
            if (disc != null)
                _discR = disc.GetComponent<Renderer>();
        }

        void Update()
        {
            if (!IsSpawned) return;
            UpdateVisuals();

            if (!IsServer) return;
            float now = Now;
            if (IsActivePhase && now >= _nextTick)
            {
                _nextTick = now + 0.5f;
                var targets = UnitBase.All.ToArray();
                foreach (var u in targets)
                {
                    if (u == null || !u.Alive) continue;
                    if (!(u is Hero) && !(u is Minion)) continue;
                    if (GameConstants.Dist2D(transform.position, u.Pos) <= Radius.Value)
                        u.ServerTakeDamage(DamagePerTick, null);
                }
            }
            if (now >= DespawnAt.Value && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
        }

        void UpdateVisuals()
        {
            if (disc == null) return;
            float r = Radius.Value;
            if (!IsActivePhase)
            {
                // warning: pulsing dark circle growing towards full size
                float remain = Mathf.Max(0f, ActiveAt.Value - Now);
                float pulse = 0.75f + Mathf.PingPong(Time.time * 2.2f, 0.25f);
                float k = Mathf.Clamp01(1.6f - remain);
                disc.localScale = new Vector3(r * 2f * k * pulse, 0.06f, r * 2f * k * pulse);
                if (_discR != null)
                {
                    _discR.material.color = new Color(0.45f, 0.1f, 0.05f);
                    _discR.material.SetColor("_EmissionColor",
                        new Color(0.7f, 0.15f, 0.05f) * (0.4f + pulse * 0.4f));
                }
            }
            else
            {
                disc.localScale = new Vector3(r * 2f, 0.08f, r * 2f);
                if (_discR != null)
                {
                    _discR.material.color = new Color(1f, 0.75f, 0.45f);
                    _discR.material.SetColor("_EmissionColor",
                        new Color(1f, 0.45f, 0.12f) * 1.6f);
                }
                if (!_embersSpawned)
                {
                    _embersSpawned = true;
                    ParticleFx.Spawn("FxEmbers", transform.position, transform, r / 1.4f);
                    FxBurst.Spawn(transform.position, new Color(1f, 0.5f, 0.15f), r, 0.4f);
                }
            }
        }
    }
}
