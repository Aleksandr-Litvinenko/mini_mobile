using UnityEngine;

namespace Moba
{
    /// Defensive tower. Prefers minions, locks onto a target until it dies or leaves range.
    public class Tower : UnitBase
    {
        const float Range = 9f;
        const float AttackInterval = 1.5f;
        const float HeroDamage = 170f;
        const float MinionDamage = 105f;

        UnitBase _target;
        float _nextShot;

        public override float HealthBarHeight => 5.2f;
        public override float BodyRadius => 1.3f;

        void Update()
        {
            if (!IsServer || !IsSpawned || Hp.Value <= 0f) return;
            if (!GameManager.Playing) return;

            if (_target == null || !_target.Alive ||
                GameConstants.Dist2D(transform.position, _target.Pos) > Range + _target.BodyRadius)
                _target = PickTarget();

            if (_target == null || Time.time < _nextShot) return;
            _nextShot = Time.time + AttackInterval;
            float dmg = _target is Hero ? HeroDamage : MinionDamage;
            Projectile.ServerSpawnHoming(transform.position + Vector3.up * 4.2f,
                _target, this, dmg, 22f, Team, 0.5f);
        }

        UnitBase PickTarget()
        {
            UnitBase bestMinion = null, bestHero = null;
            float minionDist = float.MaxValue, heroDist = float.MaxValue;
            foreach (var u in All)
            {
                if (u == null || !u.Alive || u.Team == Team || u.Team == Team.None) continue;
                float d = GameConstants.Dist2D(transform.position, u.Pos) - u.BodyRadius;
                if (d > Range) continue;
                if (u is Minion && d < minionDist) { minionDist = d; bestMinion = u; }
                else if (u is Hero && d < heroDist) { heroDist = d; bestHero = u; }
            }
            return bestMinion != null ? bestMinion : bestHero;
        }

        protected override void OnServerDeath(UnitBase killer)
        {
            foreach (var u in All)
                if (u is Hero h && h.Team != Team && h.IsSpawned)
                {
                    h.ServerAddGold(200);
                    h.ServerAddXp(150);
                }
            base.OnServerDeath(killer);
        }
    }
}
