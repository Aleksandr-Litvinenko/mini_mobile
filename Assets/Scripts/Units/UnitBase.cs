using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Moba
{
    /// Base class for every damageable networked unit (hero, minion, tower, base core).
    public class UnitBase : NetworkBehaviour
    {
        public static readonly List<UnitBase> All = new List<UnitBase>();

        public NetworkVariable<byte> TeamId = new NetworkVariable<byte>(0);
        public NetworkVariable<float> Hp = new NetworkVariable<float>(100f);
        public NetworkVariable<float> MaxHp = new NetworkVariable<float>(100f);

        // Set by the server-side spawner right before Spawn().
        [HideInInspector] public byte PendingTeam;
        [HideInInspector] public float PendingMaxHp;

        public Team Team => (Team)TeamId.Value;
        public virtual bool Alive => IsSpawned && Hp.Value > 0f;
        public Vector3 Pos => transform.position;

        public virtual float HealthBarHeight => 2.3f;
        public virtual float BodyRadius => 0.6f;

        protected float ServerTimeF => NetworkManager != null ? NetworkManager.ServerTime.TimeAsFloat : 0f;

        Renderer[] _renderers;
        bool _visible = true;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer)
            {
                if (PendingTeam != 0) TeamId.Value = PendingTeam;
                if (PendingMaxHp > 0f)
                {
                    MaxHp.Value = PendingMaxHp;
                    Hp.Value = PendingMaxHp;
                }
            }
            TeamId.OnValueChanged += OnTeamChanged;
            ApplyTeamColor();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            TeamId.OnValueChanged -= OnTeamChanged;
        }

        void OnTeamChanged(byte oldV, byte newV)
        {
            ApplyTeamColor();
        }

        protected void ApplyTeamColor()
        {
            var c = GameConstants.TeamColor(Team);
            foreach (var tint in GetComponentsInChildren<TeamTint>(true))
            {
                var r = tint.GetComponent<Renderer>();
                if (r != null) r.material.color = c;
            }
        }

        protected Renderer[] Renderers
        {
            get
            {
                if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
                return _renderers;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            foreach (var r in Renderers)
                if (r != null) r.enabled = visible;
        }

        public bool IsVisibleLocally => _visible;

        // ---- server-side combat ----

        public void ServerTakeDamage(float amount, UnitBase attacker)
        {
            if (!IsServer || !IsSpawned || Hp.Value <= 0f) return;
            amount = ModifyIncomingDamage(amount);
            Hp.Value = Mathf.Max(0f, Hp.Value - amount);
            DamageFxRpc(amount, transform.position + Vector3.up * HealthBarHeight,
                attacker != null && attacker.IsSpawned ? attacker.NetworkObjectId : 0UL);
            // Belial's dark magic drains life from living victims
            if (attacker is Hero ah && ah.IsSpawned && ah.Team != Team &&
                ah.HeroType == HeroKind.Belial && (this is Hero || this is Minion))
                ah.ServerHeal(amount * Hero.LifestealFraction);
            if (Hp.Value <= 0f)
                OnServerDeath(attacker);
        }

        public void ServerHeal(float amount)
        {
            if (!IsServer || !IsSpawned || Hp.Value <= 0f) return;
            Hp.Value = Mathf.Min(MaxHp.Value, Hp.Value + amount);
        }

        protected virtual float ModifyIncomingDamage(float amount)
        {
            return amount;
        }

        protected virtual void OnServerDeath(UnitBase killer)
        {
            DeathFxRpc(transform.position);
            if (NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void DeathFxRpc(Vector3 pos)
        {
            ParticleFx.SpawnTinted("FxSparkBurst", pos + Vector3.up * 0.5f,
                new Color(0.85f, 0.25f, 0.12f), 0.8f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void DamageFxRpc(float amount, Vector3 pos, ulong attackerId)
        {
            // your own hits pop big and bright, damage you take pops red
            var c = new Color(1f, 0.85f, 0.35f);
            float size = 0.9f;
            var local = Hero.Local;
            if (local != null && (UnitBase)local == this)
            {
                c = new Color(1f, 0.3f, 0.25f);
                size = 1.3f;
            }
            else if (local != null && attackerId != 0 && attackerId == local.NetworkObjectId)
            {
                c = new Color(1f, 1f, 0.8f);
                size = 1.35f;
            }
            DamagePopup.Spawn(pos, Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), c, size);
        }
    }
}
