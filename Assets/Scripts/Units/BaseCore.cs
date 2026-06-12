using UnityEngine;

namespace Moba
{
    /// The team's main structure. Destroying it ends the match.
    /// Takes heavily reduced damage while the team's tower still stands.
    public class BaseCore : UnitBase
    {
        public override float HealthBarHeight => 4.6f;
        public override float BodyRadius => 2.6f;

        protected override float ModifyIncomingDamage(float amount)
        {
            foreach (var u in All)
                if (u is Tower t && t.Team == Team && t.Alive)
                    return amount * 0.35f;
            return amount;
        }

        protected override void OnServerDeath(UnitBase killer)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ServerBaseDestroyed(Team);
            base.OnServerDeath(killer);
        }
    }
}
