using UnityEngine;

namespace Moba
{
    /// Lane minion. All AI runs on the server; transform is synced via NetworkTransform.
    public class Minion : UnitBase
    {
        const float AggroRange = 8f;
        const float MeleeRange = 1.9f;
        const float AttackInterval = 1.2f;
        const float MoveSpeed = 4.3f;

        // set by the spawner before Spawn()
        [HideInInspector] public float PendingDamage = 26f;

        UnitBase _target;
        float _nextAttack;
        float _nextRetarget;
        float _slowUntil;

        public override float HealthBarHeight => 1.7f;
        public override float BodyRadius => 0.5f;

        public void ServerSlow(float seconds)
        {
            _slowUntil = Time.time + seconds;
        }

        void Update()
        {
            if (!IsServer || !IsSpawned || Hp.Value <= 0f) return;
            if (!GameManager.Playing) return;

            float dt = Time.deltaTime;
            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + 0.3f;
                _target = PickTarget();
            }

            float speed = MoveSpeed * (Time.time < _slowUntil ? 0.5f : 1f);

            if (_target != null && _target.Alive)
            {
                float range = MeleeRange + _target.BodyRadius;
                float dist = GameConstants.Dist2D(transform.position, _target.Pos);
                Face(_target.Pos);
                if (dist > range)
                    MoveTowards(_target.Pos, speed, dt);
                else if (Time.time >= _nextAttack)
                {
                    _nextAttack = Time.time + AttackInterval;
                    MeleeFxRpc();
                    _target.ServerTakeDamage(PendingDamage, this);
                }
            }
            else
            {
                // push down the lane towards the enemy base
                Vector3 goal = GameConstants.BasePos(GameConstants.Enemy(Team));
                Face(goal);
                MoveTowards(goal, speed, dt);
            }
        }

        UnitBase PickTarget()
        {
            UnitBase best = null;
            float bestScore = float.MaxValue;
            foreach (var u in All)
            {
                if (u == null || !u.Alive || u.Team == Team || u.Team == Team.None) continue;
                float d = GameConstants.Dist2D(transform.position, u.Pos) - u.BodyRadius;
                if (d > AggroRange && !(u is BaseCore)) continue;
                float score = u is Hero ? d * 1.5f : d; // slightly prefer non-hero targets
                if (u is BaseCore) score = d; // base counts at any distance once nothing else is near
                if (d > AggroRange) score += 1000f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = u;
                }
            }
            return best;
        }

        void MoveTowards(Vector3 goal, float speed, float dt)
        {
            Vector3 dir = GameConstants.Flat(goal - transform.position);
            if (dir.sqrMagnitude < 0.01f) return;
            Vector3 pos = transform.position + dir.normalized * speed * dt;
            pos.x = Mathf.Clamp(pos.x, -GameConstants.ClampX, GameConstants.ClampX);
            pos.z = Mathf.Clamp(pos.z, -GameConstants.ClampZ, GameConstants.ClampZ);
            pos.y = 0f;
            transform.position = pos;
        }

        void Face(Vector3 at)
        {
            Vector3 dir = GameConstants.Flat(at - transform.position);
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        }

        [Unity.Netcode.Rpc(Unity.Netcode.SendTo.ClientsAndHost)]
        void MeleeFxRpc()
        {
            var anim = GetComponent<UnitAnimator>();
            if (anim != null) anim.TriggerAttack();
        }

        protected override void OnServerDeath(UnitBase killer)
        {
            if (killer is Hero h && h.IsSpawned && h.Team != Team)
                h.ServerAddGold(40);
            GameManager.AwardXpAround(GameConstants.Enemy(Team), transform.position, 62);
            base.OnServerDeath(killer);
        }
    }
}
