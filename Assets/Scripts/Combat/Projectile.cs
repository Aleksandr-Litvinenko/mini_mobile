using Unity.Netcode;
using UnityEngine;

namespace Moba
{
    /// Server-driven projectile: homing (auto attacks, tower shots) or linear skillshot (Q).
    public class Projectile : NetworkBehaviour
    {
        public NetworkVariable<byte> TeamId = new NetworkVariable<byte>(0);
        public NetworkVariable<float> VisScale = new NetworkVariable<float>(0.35f);

        // server-only config
        UnitBase _homingTarget;
        UnitBase _owner;
        float _damage, _speed, _maxRange, _hitRadius, _slowSeconds;
        Vector3 _dir;
        bool _homing;
        float _traveled;
        byte _pendingTeam;
        float _pendingScale;

        public static void ServerSpawnHoming(Vector3 pos, UnitBase target, UnitBase owner,
            float damage, float speed, Team team, float visScale)
        {
            var p = Create(pos, owner, damage, speed, team, visScale);
            if (p == null) return;
            p._homing = true;
            p._homingTarget = target;
            p._maxRange = 60f;
            p.Finish();
        }

        public static void ServerSpawnLinear(Vector3 pos, Vector3 dir, UnitBase owner,
            float damage, float speed, float maxRange, Team team, float visScale,
            float slowSeconds = 0f)
        {
            var p = Create(pos, owner, damage, speed, team, visScale);
            if (p == null) return;
            p._homing = false;
            p._dir = GameConstants.Flat(dir).normalized;
            p._maxRange = maxRange;
            p._hitRadius = 0.9f;
            p._slowSeconds = slowSeconds;
            p.Finish();
        }

        static Projectile Create(Vector3 pos, UnitBase owner, float damage, float speed,
            Team team, float visScale)
        {
            var prefab = GamePrefabs.Projectile;
            if (prefab == null) return null;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            var p = go.GetComponent<Projectile>();
            p._owner = owner;
            p._damage = damage;
            p._speed = speed;
            p._pendingTeam = (byte)team;
            p._pendingScale = visScale;
            return p;
        }

        void Finish()
        {
            GetComponent<NetworkObject>().Spawn(true);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                TeamId.Value = _pendingTeam;
                VisScale.Value = _pendingScale;
            }
            ApplyVisual();
            TeamId.OnValueChanged += (a, b) => ApplyVisual();
            VisScale.OnValueChanged += (a, b) => ApplyVisual();
        }

        void ApplyVisual()
        {
            transform.localScale = Vector3.one * Mathf.Max(0.1f, VisScale.Value);
            var c = GameConstants.TeamColor((Team)TeamId.Value);
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var m = r.material;
                m.color = c;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 1.6f);
            }
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;
            float dt = Time.fixedDeltaTime;

            if (_homing)
            {
                if (_homingTarget == null || !_homingTarget.Alive)
                {
                    Despawn();
                    return;
                }
                Vector3 aim = _homingTarget.Pos + Vector3.up * 1.1f;
                _dir = (aim - transform.position).normalized;
                transform.position += _dir * _speed * dt;
                _traveled += _speed * dt;
                if (Vector3.Distance(transform.position, aim) < 0.7f)
                {
                    Hit(_homingTarget);
                    return;
                }
            }
            else
            {
                transform.position += _dir * _speed * dt;
                _traveled += _speed * dt;
                UnitBase hit = null;
                float best = float.MaxValue;
                foreach (var u in UnitBase.All)
                {
                    if (u == null || !u.Alive || (byte)u.Team == TeamId.Value || u.Team == Team.None) continue;
                    float d = GameConstants.Dist2D(transform.position, u.Pos);
                    if (d < _hitRadius + u.BodyRadius && d < best)
                    {
                        best = d;
                        hit = u;
                    }
                }
                if (hit != null)
                {
                    Hit(hit);
                    return;
                }
            }

            if (_traveled >= _maxRange)
                Despawn();
        }

        void Hit(UnitBase target)
        {
            HitFxRpc(target.Pos + Vector3.up * 1f);
            if (_slowSeconds > 0f)
            {
                float until = NetworkManager.ServerTime.TimeAsFloat + _slowSeconds;
                if (target is Hero th) th.SlowUntilTime.Value = until;
                if (target is Minion tm) tm.ServerSlow(_slowSeconds);
            }
            target.ServerTakeDamage(_damage, _owner);
            Despawn();
        }

        void Despawn()
        {
            if (NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void HitFxRpc(Vector3 pos)
        {
            FxBurst.Spawn(pos, GameConstants.TeamColor((Team)TeamId.Value), 0.9f, 0.2f);
        }
    }
}
