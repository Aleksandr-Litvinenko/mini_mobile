using UnityEngine;

namespace Moba
{
    /// Server-side AI driving a server-owned hero in training mode.
    /// Calls the hero's server RPCs directly (they execute locally on the server).
    public class HeroBot : MonoBehaviour
    {
        Hero _h;
        float _nextThink, _nextBuy;
        Vector3 _moveTarget;
        bool _retreating;

        void Awake()
        {
            _h = GetComponent<Hero>();
        }

        void Update()
        {
            if (_h == null || !_h.IsServer || !_h.IsSpawned) return;
            if (_h.Dead.Value || !GameManager.Playing) return;

            if (Time.time >= _nextThink)
            {
                _nextThink = Time.time + 0.25f;
                Think();
            }
            MoveStep(Time.deltaTime);
        }

        void Think()
        {
            Vector3 pos = transform.position;
            float hpFrac = _h.Hp.Value / Mathf.Max(1f, _h.MaxHp.Value);
            Vector3 home = GameConstants.BasePos(_h.Team);
            Vector3 push = GameConstants.BasePos(GameConstants.Enemy(_h.Team));

            Hero enemyHero = null;
            UnitBase nearest = null;
            float heroDist = float.MaxValue, nearDist = float.MaxValue;
            foreach (var u in UnitBase.All)
            {
                if (u == null || !u.Alive || u.Team == _h.Team || u.Team == Team.None) continue;
                float d = GameConstants.Dist2D(pos, u.Pos);
                if (u is Hero he && d < heroDist) { heroDist = d; enemyHero = he; }
                if (d < nearDist) { nearDist = d; nearest = u; }
            }

            // retreat / re-engage hysteresis
            if (_retreating)
            {
                if (hpFrac > 0.85f) _retreating = false;
            }
            else if (hpFrac < 0.32f)
                _retreating = true;

            if (_retreating)
            {
                _moveTarget = home;
            }
            else if (nearest != null && nearDist < 12f)
            {
                float hold = _h.AttackRange - 0.5f;
                Vector3 away = GameConstants.Flat(pos - nearest.Pos).normalized;
                if (nearDist > hold)
                    _moveTarget = nearest.Pos;
                else if (hpFrac < 0.55f || nearDist < hold - 2f)
                    _moveTarget = pos + away * 3f; // kite back
                else
                    _moveTarget = pos;

                // attack the closest valid target
                var target = _h.FindNearestEnemy(_h.AttackRange + 0.3f);
                if (target != null)
                    _h.AttackRpc(target.NetworkObjectId);

                // skills
                if (enemyHero != null && heroDist < 13f && _h.Mana.Value >= _h.QCost)
                    _h.CastQRpc(GameConstants.Flat(enemyHero.Pos - pos).normalized);
                switch (_h.HeroType)
                {
                    case HeroKind.Belial:
                        if (enemyHero != null && heroDist < 8f && _h.Mana.Value >= _h.WCost)
                            _h.DrainRpc(enemyHero.NetworkObjectId);
                        break;
                    case HeroKind.Adanos:
                        if (hpFrac < 0.7f && _h.Mana.Value >= _h.WCost)
                            _h.FlowRpc();
                        break;
                    case HeroKind.Xardaras:
                        if (enemyHero != null && heroDist > 9f && heroDist < 14f &&
                            _h.Mana.Value >= _h.WCost && !_retreating)
                        {
                            Vector3 to = pos + GameConstants.Flat(enemyHero.Pos - pos).normalized * 7f;
                            to = MapBuilder.ResolvePosition(to, _h.BodyRadius);
                            _h.TeleportLocal(to);
                            _h.BlinkRpc(pos, to);
                        }
                        break;
                }
                if (enemyHero != null && heroDist < 4.5f && _h.UltReady && _h.Mana.Value >= _h.ECost)
                    _h.CastERpc();
            }
            else
            {
                _moveTarget = Vector3.Lerp(push, new Vector3(push.x, 0f, 0f), 0.5f);
            }

            // never stand in (incoming) lava
            foreach (var lp in LavaPool.ActivePools)
            {
                if (lp == null) continue;
                float d = GameConstants.Dist2D(pos, lp.transform.position);
                if (d < lp.Radius.Value + 1.2f)
                {
                    Vector3 away = GameConstants.Flat(pos - lp.transform.position);
                    if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                    _moveTarget = pos + away.normalized * 4f;
                    break;
                }
            }

            // shopping
            if (Time.time >= _nextBuy)
            {
                _nextBuy = Time.time + 4f;
                for (int i = 0; i < 3; i++)
                    _h.BuyUpgradeRpc(i);
            }
        }

        void MoveStep(float dt)
        {
            Vector3 dir = GameConstants.Flat(_moveTarget - transform.position);
            if (dir.magnitude < 0.5f) return;
            dir.Normalize();
            Vector3 pos = transform.position + dir * _h.CurrentMoveSpeed * dt;
            pos.x = Mathf.Clamp(pos.x, -GameConstants.ClampX, GameConstants.ClampX);
            pos.z = Mathf.Clamp(pos.z, -GameConstants.ClampZ, GameConstants.ClampZ);
            pos.y = 0f;
            pos = MapBuilder.ResolvePosition(pos, _h.BodyRadius);
            transform.position = pos;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 10f * dt);
        }
    }
}
