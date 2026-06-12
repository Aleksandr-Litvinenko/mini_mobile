using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Moba
{
    /// Player-controlled hero. Movement is owner-authoritative, combat is server-authoritative.
    /// Three kits: Xardaras (fire), Belial (dark, lifesteal), Adanos (water, sustain).
    /// Can be driven by HeroBot in training mode (server-owned, input skipped).
    public class Hero : UnitBase
    {
        public static Hero Local;
        public static HeroKind LocalChoice = HeroKind.Xardaras;

        // ---- networked state ----
        public NetworkVariable<byte> Kind = new NetworkVariable<byte>(0);
        public NetworkVariable<bool> IsBot = new NetworkVariable<bool>(false);
        public NetworkVariable<int> Level = new NetworkVariable<int>(1);
        public NetworkVariable<int> Xp = new NetworkVariable<int>(0);
        public NetworkVariable<int> Gold = new NetworkVariable<int>(0);
        public NetworkVariable<float> Mana = new NetworkVariable<float>(100f);
        public NetworkVariable<float> MaxMana = new NetworkVariable<float>(100f);
        public NetworkVariable<bool> Dead = new NetworkVariable<bool>(false);
        public NetworkVariable<float> RespawnAtTime = new NetworkVariable<float>(0f);
        public NetworkVariable<float> SlowUntilTime = new NetworkVariable<float>(0f);
        public NetworkVariable<float> HasteUntilTime = new NetworkVariable<float>(0f);
        public NetworkVariable<int> AtkUp = new NetworkVariable<int>(0);
        public NetworkVariable<int> HpUp = new NetworkVariable<int>(0);
        public NetworkVariable<int> AsUp = new NetworkVariable<int>(0);
        public NetworkVariable<int> Kills = new NetworkVariable<int>(0);
        public NetworkVariable<int> Deaths = new NetworkVariable<int>(0);
        public NetworkVariable<int> QLvl = new NetworkVariable<int>(1);
        public NetworkVariable<int> WLvl = new NetworkVariable<int>(1);
        public NetworkVariable<int> ELvl = new NetworkVariable<int>(1);
        public NetworkVariable<int> SkillPoints = new NetworkVariable<int>(0);

        // set by the spawner before Spawn() (bot in training mode)
        [HideInInspector] public bool PendingBot;

        // ---- tuning ----
        public const int MaxLevel = 15;
        public const int UltLevel = 4;
        public const int SkillCap = 5;
        public const float BaseMoveSpeed = 7f;
        public const float LifestealFraction = 0.12f; // Belial only

        public static readonly int[] UpgradeCost = { 500, 400, 450 };
        public static readonly int[] UpgradeCap = { 6, 6, 4 };
        public static readonly string[] UpgradeName = { "+15 атаки", "+200 здоровья", "+12% скор. атаки" };
        public static readonly string[] UpgradeIcon = { "up_atk", "up_hp", "up_as" };

        // ---- derived stats (branch by hero kind) ----
        public HeroKind HeroType => (HeroKind)Kind.Value;
        bool IsBelial => Kind.Value == (byte)HeroKind.Belial;
        bool IsAdanos => Kind.Value == (byte)HeroKind.Adanos;

        public float AttackDamage =>
            (IsBelial ? 66f + 13f * (Level.Value - 1)
             : IsAdanos ? 58f + 11f * (Level.Value - 1)
             : 60f + 12f * (Level.Value - 1)) + 15f * AtkUp.Value;
        public float AttackRange => IsBelial ? 5.0f : IsAdanos ? 5.5f : 6.0f;
        public float AttackCooldown => 1.1f * Mathf.Pow(0.88f, AsUp.Value);
        // skill damage scales with the skill's own level (upgraded with skill points)
        public float QDamage =>
            IsBelial ? 75f + 38f * QLvl.Value
            : IsAdanos ? 55f + 28f * QLvl.Value
            : 60f + 30f * QLvl.Value;
        public float WDrainDamage => 45f + 28f * WLvl.Value;
        public float WFlowHeal => 55f + 30f * WLvl.Value;
        public float EDamage =>
            IsBelial ? 105f + 40f * ELvl.Value
            : IsAdanos ? 100f + 38f * ELvl.Value
            : 130f + 48f * ELvl.Value;
        public float EHealPerHit => 50f + 18f * ELvl.Value;
        public float QCd => IsBelial ? 6f : IsAdanos ? 4.5f : 5f;
        public float WCd => IsBelial ? 7f : 10f - 0.5f * WLvl.Value; // blink cd drops per level
        public float ECd => IsAdanos ? 28f : 30f;
        public float QCost => IsBelial ? 45f : IsAdanos ? 35f : 40f;
        public float WCost => IsBelial ? 40f : IsAdanos ? 45f : 35f;
        public float ECost => 100f;
        public bool UltReady => Level.Value >= UltLevel;
        float StatMaxHp =>
            (IsBelial ? 1150f + 165f * (Level.Value - 1)
             : IsAdanos ? 1050f + 155f * (Level.Value - 1)
             : 1000f + 150f * (Level.Value - 1)) + 200f * HpUp.Value;
        float StatMaxMana => (IsAdanos ? 460f : 400f) + (IsAdanos ? 65f : 55f) * (Level.Value - 1);
        public int XpToNext => 90 + 70 * (Level.Value - 1);
        public bool IsSlowed => NetworkManager != null && ServerTimeF < SlowUntilTime.Value;
        public bool IsHasted => NetworkManager != null && ServerTimeF < HasteUntilTime.Value;
        public float CurrentMoveSpeed => BaseMoveSpeed * (IsSlowed ? 0.5f : 1f) * (IsHasted ? 1.35f : 1f);
        public override bool Alive => IsSpawned && !Dead.Value && Hp.Value > 0f;
        public override float HealthBarHeight => 2.6f;
        public override float BodyRadius => 0.55f;

        public Color KindColor =>
            IsBelial ? new Color(0.85f, 0.2f, 0.45f)
            : IsAdanos ? new Color(0.3f, 0.75f, 1f)
            : new Color(1f, 0.55f, 0.2f);

        // ---- owner-side cooldown estimates for the HUD ----
        [HideInInspector] public float CdAttackUntil, CdQUntil, CdWUntil, CdEUntil;

        // server-side cooldowns / accumulators
        float _srvNextAttack, _srvNextQ, _srvNextW, _srvNextE;
        float _goldAcc, _xpAcc;
        bool _kindLocked;

        // owner-side movement
        Vector3 _faceDir = Vector3.right;
        bool _placed;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                if (PendingBot) IsBot.Value = true;
                TeamId.Value = PendingTeam != 0
                    ? PendingTeam
                    : OwnerClientId == NetworkManager.ServerClientId ? (byte)Team.Blue : (byte)Team.Red;
                ApplyTeamColor();
                ServerRecalcStats();
                Hp.Value = MaxHp.Value;
                Mana.Value = MaxMana.Value;
            }
            if (IsOwner && !PendingBot && !IsBot.Value)
            {
                Local = this;
                SelectHeroRpc((byte)LocalChoice);
                Debug.Log("[Moba] Local hero spawned, team " + Team);
            }
            Dead.OnValueChanged += OnDeadChanged;
            Kind.OnValueChanged += OnKindChanged;
            ApplyKindVisuals();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            Dead.OnValueChanged -= OnDeadChanged;
            Kind.OnValueChanged -= OnKindChanged;
            if (Local == this) Local = null;
        }

        /// Server-side: lock the bot's hero kind (training mode).
        public void ServerSetKind(HeroKind k)
        {
            if (!IsServer) return;
            _kindLocked = true;
            Kind.Value = (byte)k;
            ServerRecalcStats();
            Hp.Value = MaxHp.Value;
            Mana.Value = MaxMana.Value;
        }

        void OnDeadChanged(bool oldV, bool newV)
        {
            if (newV)
            {
                DamagePopup.Spawn(transform.position + Vector3.up * 2.8f, "ПОВЕРЖЕН",
                    new Color(1f, 0.25f, 0.2f), 1.6f);
                FxBurst.Spawn(transform.position, new Color(0.7f, 0.1f, 0.1f), 2.4f, 0.5f);
            }
        }

        void OnKindChanged(byte oldV, byte newV)
        {
            ApplyKindVisuals();
        }

        void ApplyKindVisuals()
        {
            var visual = transform.Find("Visual");
            if (visual == null) return;
            var kx = visual.Find("KindX");
            var kb = visual.Find("KindB");
            var ka = visual.Find("KindA");
            if (kx != null) kx.gameObject.SetActive(!IsBelial && !IsAdanos);
            if (kb != null) kb.gameObject.SetActive(IsBelial);
            if (ka != null) ka.gameObject.SetActive(IsAdanos);
        }

        // ============================== OWNER ==============================

        void Update()
        {
            if (IsOwner)
                OwnerUpdate();
            if (IsServer && IsSpawned)
                ServerUpdate();
        }

        void LateUpdate()
        {
            UpdateLocalVisibility();
        }

        void OwnerUpdate()
        {
            if (!_placed && TeamId.Value != 0)
            {
                _placed = true;
                _faceDir = Team == Team.Blue ? Vector3.right : Vector3.left;
                TeleportLocal(GameConstants.HeroSpawn(Team));
            }

            if (IsBot.Value) return; // HeroBot drives this one on the server
            if (Dead.Value) return;

            float dt = Time.deltaTime;
            float speed = CurrentMoveSpeed;

            // movement: WASD / arrows, RMB towards cursor, or the touch joystick
            Vector3 input = Vector3.zero;
            input.x = Input.GetAxisRaw("Horizontal");
            input.z = Input.GetAxisRaw("Vertical");
            if (TouchHud.Enabled && TouchHud.Move.sqrMagnitude > 0.04f)
                input = new Vector3(TouchHud.Move.x, 0f, TouchHud.Move.y);
            if (input.sqrMagnitude < 0.01f && !TouchHud.Enabled &&
                Input.GetMouseButton(1) && MouseGroundPoint(out var mp))
            {
                var to = GameConstants.Flat(mp - transform.position);
                if (to.magnitude > 0.4f) input = to;
            }

            Vector3 pos = transform.position;
            if (input.sqrMagnitude > 0.01f)
            {
                input.Normalize();
                pos += input * speed * dt;
                _faceDir = input;
            }

            pos.x = Mathf.Clamp(pos.x, -GameConstants.ClampX, GameConstants.ClampX);
            pos.z = Mathf.Clamp(pos.z, -GameConstants.ClampZ, GameConstants.ClampZ);
            pos.y = 0f;
            pos = MapBuilder.ResolvePosition(pos, BodyRadius);
            transform.position = pos;
            if (_faceDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(_faceDir), 12f * dt);

            if (!GameManager.Playing) return;

            bool attackPressed = Input.GetKey(KeyCode.Space) ||
                                 (!TouchHud.Enabled && Input.GetMouseButton(0) &&
                                  GUIUtility.hotControl == 0);
            if (TouchHud.Enabled && TouchHud.AttackHeld) attackPressed = true;
            if (attackPressed) TryAttack();
            if (Input.GetKeyDown(KeyCode.Q)) TryCastQ(false);
            if (Input.GetKeyDown(KeyCode.W)) TryCastW(false);
            if (Input.GetKeyDown(KeyCode.E)) TryCastE();
            if (TouchHud.ConsumeQ()) TryCastQ(true);
            if (TouchHud.ConsumeW()) TryCastW(true);
            if (TouchHud.ConsumeE()) TryCastE();
            if (Input.GetKeyDown(KeyCode.Alpha1)) RequestBuy(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) RequestBuy(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) RequestBuy(2);
        }

        public void RequestBuy(int index)
        {
            BuyUpgradeRpc(index);
        }

        public int GetSkillLevel(int slot) =>
            slot == 0 ? QLvl.Value : slot == 1 ? WLvl.Value : ELvl.Value;

        public bool CanUpgradeSkill(int slot)
        {
            if (SkillPoints.Value <= 0) return false;
            if (slot == 2 && Level.Value < UltLevel) return false;
            return GetSkillLevel(slot) < SkillCap;
        }

        /// Full tooltip with live numbers for the HUD.
        public string SkillTooltip(int slot)
        {
            var meta = HeroData.Skill(HeroType, slot);
            string head = meta.name + " (ур. " + GetSkillLevel(slot) + ")\n" + meta.desc + "\n";
            string body;
            switch (slot)
            {
                case 0:
                    body = "Урон: " + Mathf.RoundToInt(QDamage) +
                           (HeroType != HeroKind.Xardaras ? ", замедляет" : "") +
                           "\nМана: " + QCost + "   КД: " + QCd + " c";
                    break;
                case 1:
                    if (IsBelial)
                        body = "Урон и лечение: " + Mathf.RoundToInt(WDrainDamage) +
                               "\nМана: " + WCost + "   КД: " + WCd + " c";
                    else if (IsAdanos)
                        body = "Лечение: " + Mathf.RoundToInt(WFlowHeal) + " + ускорение 35% (2 c)" +
                               "\nМана: " + WCost + "   КД: " + WCd + " c";
                    else
                        body = "Перенос до 7 м\nМана: " + WCost + "   КД: " + WCd + " c";
                    break;
                default:
                    body = "Урон: " + Mathf.RoundToInt(EDamage) +
                           (IsBelial ? " + лечение " + Mathf.RoundToInt(EHealPerHit) + " за врага" : "") +
                           (IsAdanos ? ", замедление 3 c" : ", замедление 2 c") +
                           "\nМана: " + ECost + "   КД: " + ECd + " c" +
                           (Level.Value < UltLevel ? "\nОткроется на 4 уровне" : "");
                    break;
            }
            return head + body;
        }

        void TryAttack()
        {
            if (Time.time < CdAttackUntil) return;
            var target = FindNearestEnemy(AttackRange);
            if (target == null) return;
            CdAttackUntil = Time.time + AttackCooldown;
            _faceDir = GameConstants.Flat(target.Pos - transform.position).normalized;
            AttackRpc(target.NetworkObjectId);
        }

        public UnitBase FindNearestEnemy(float range)
        {
            UnitBase best = null;
            float bestDist = float.MaxValue;
            foreach (var u in All)
            {
                if (u == null || !u.Alive || u.Team == Team || u.Team == Team.None) continue;
                if (u is Hero h && Local == this && !h.IsVisibleLocally) continue; // bush stealth
                float d = GameConstants.Dist2D(transform.position, u.Pos) - u.BodyRadius;
                if (d <= range && d < bestDist)
                {
                    bestDist = d;
                    best = u;
                }
            }
            return best;
        }

        Vector3 AutoAimDir()
        {
            var target = FindNearestEnemy(15f);
            if (target != null)
                return GameConstants.Flat(target.Pos - transform.position).normalized;
            return _faceDir;
        }

        void TryCastQ(bool autoAim)
        {
            if (Time.time < CdQUntil || Mana.Value < QCost) return;
            Vector3 dir = _faceDir;
            if (autoAim)
                dir = AutoAimDir();
            else if (MouseGroundPoint(out var mp))
            {
                var to = GameConstants.Flat(mp - transform.position);
                if (to.magnitude > 0.3f) dir = to.normalized;
            }
            CdQUntil = Time.time + QCd;
            _faceDir = dir;
            CastQRpc(dir);
        }

        void TryCastW(bool autoAim)
        {
            if (Time.time < CdWUntil || Mana.Value < WCost) return;
            if (IsBelial)
            {
                var target = FindNearestEnemy(7f);
                if (target == null) return; // nothing to drain — don't waste the cast
                CdWUntil = Time.time + WCd;
                DrainRpc(target.NetworkObjectId);
            }
            else if (IsAdanos)
            {
                CdWUntil = Time.time + WCd;
                FlowRpc();
            }
            else
            {
                // blink towards the cursor (or movement direction on touch), up to 7 units
                Vector3 from = transform.position;
                Vector3 dir = _faceDir;
                float dist = 7f;
                if (!autoAim && MouseGroundPoint(out var mp))
                {
                    var to = GameConstants.Flat(mp - from);
                    if (to.magnitude > 0.5f)
                    {
                        dir = to.normalized;
                        dist = Mathf.Min(7f, to.magnitude);
                    }
                }
                Vector3 to2 = from + dir * dist;
                to2.x = Mathf.Clamp(to2.x, -GameConstants.ClampX, GameConstants.ClampX);
                to2.z = Mathf.Clamp(to2.z, -GameConstants.ClampZ, GameConstants.ClampZ);
                to2 = MapBuilder.ResolvePosition(to2, BodyRadius);
                CdWUntil = Time.time + WCd;
                _faceDir = dir;
                TeleportLocal(to2);
                BlinkRpc(from, to2);
            }
        }

        void TryCastE()
        {
            if (!UltReady || Time.time < CdEUntil || Mana.Value < ECost) return;
            CdEUntil = Time.time + ECd;
            CastERpc();
        }

        public void TeleportLocal(Vector3 pos)
        {
            var nt = GetComponent<NetworkTransform>();
            var rot = Quaternion.LookRotation(_faceDir);
            if (nt != null && IsSpawned)
                nt.Teleport(pos, rot, transform.localScale);
            else
                transform.SetPositionAndRotation(pos, rot);
        }

        static bool MouseGroundPoint(out Vector3 point)
        {
            point = Vector3.zero;
            var cam = Camera.main;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;
            point = ray.origin + ray.direction * t;
            return true;
        }

        // visibility: enemies hidden while inside a bush (unless we share it or stand next to them)
        void UpdateLocalVisibility()
        {
            bool visible = !Dead.Value;
            if (visible && Local != null && Local != this && Team != Local.Team)
            {
                int myBush = MapBuilder.BushIndex(transform.position);
                if (myBush >= 0)
                {
                    int localBush = MapBuilder.BushIndex(Local.transform.position);
                    float dist = GameConstants.Dist2D(transform.position, Local.transform.position);
                    if (localBush != myBush && dist > 3f)
                        visible = false;
                }
            }
            SetVisible(visible);
        }

        // ============================== RPCs ==============================
        // Public so the training bot can invoke them directly on the server
        // (RPCs targeting the server execute locally when called from it).

        [Rpc(SendTo.Server)]
        void SelectHeroRpc(byte kind)
        {
            if (_kindLocked || kind >= HeroData.Count) return;
            _kindLocked = true;
            Kind.Value = kind;
            ServerRecalcStats();
            Hp.Value = MaxHp.Value;
            Mana.Value = MaxMana.Value;
        }

        [Rpc(SendTo.Server)]
        public void AttackRpc(ulong targetId)
        {
            if (Dead.Value || !GameManager.Playing) return;
            if (ServerTimeF < _srvNextAttack - 0.08f) return;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var no)) return;
            var target = no.GetComponent<UnitBase>();
            if (target == null || !target.Alive || target.Team == Team) return;
            if (GameConstants.Dist2D(transform.position, target.Pos) > AttackRange + 2f) return;

            _srvNextAttack = ServerTimeF + AttackCooldown;
            SwingFxRpc();
            Projectile.ServerSpawnHoming(transform.position + Vector3.up * 1.4f,
                target, this, AttackDamage, 24f, Team, 0.3f);
        }

        [Rpc(SendTo.Server)]
        public void CastQRpc(Vector3 dir)
        {
            if (Dead.Value || !GameManager.Playing) return;
            if (ServerTimeF < _srvNextQ - 0.08f || Mana.Value < QCost) return;
            dir = GameConstants.Flat(dir).normalized;
            if (dir.sqrMagnitude < 0.5f) return;
            _srvNextQ = ServerTimeF + QCd;
            Mana.Value -= QCost;
            SwingFxRpc();
            if (IsBelial)
                Projectile.ServerSpawnLinear(transform.position + Vector3.up * 1.2f,
                    dir, this, QDamage, 14f, 13f, Team, 0.65f, 1.6f);
            else if (IsAdanos)
                Projectile.ServerSpawnLinear(transform.position + Vector3.up * 1.2f,
                    dir, this, QDamage, 17f, 14f, Team, 0.55f, 1.2f);
            else
                Projectile.ServerSpawnLinear(transform.position + Vector3.up * 1.2f,
                    dir, this, QDamage, 19f, 15f, Team, 0.55f, 0f);
        }

        [Rpc(SendTo.Server)]
        public void BlinkRpc(Vector3 from, Vector3 to)
        {
            if (Dead.Value || !GameManager.Playing || IsBelial || IsAdanos) return;
            if (ServerTimeF < _srvNextW - 0.08f || Mana.Value < WCost) return;
            if (Vector3.Distance(from, to) > 8.5f) return;
            _srvNextW = ServerTimeF + WCd;
            Mana.Value -= WCost;
            BlinkFxRpc(from, to);
        }

        [Rpc(SendTo.Server)]
        public void DrainRpc(ulong targetId)
        {
            if (Dead.Value || !GameManager.Playing || !IsBelial) return;
            if (ServerTimeF < _srvNextW - 0.08f || Mana.Value < WCost) return;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var no)) return;
            var target = no.GetComponent<UnitBase>();
            if (target == null || !target.Alive || target.Team == Team) return;
            if (GameConstants.Dist2D(transform.position, target.Pos) > 9f) return;
            _srvNextW = ServerTimeF + WCd;
            Mana.Value -= WCost;
            float dmg = WDrainDamage;
            BeamFxRpc(transform.position + Vector3.up * 1.4f, target.Pos + Vector3.up * 1.1f);
            target.ServerTakeDamage(dmg, this);
            ServerHeal(dmg);
        }

        [Rpc(SendTo.Server)]
        public void FlowRpc()
        {
            if (Dead.Value || !GameManager.Playing || !IsAdanos) return;
            if (ServerTimeF < _srvNextW - 0.08f || Mana.Value < WCost) return;
            _srvNextW = ServerTimeF + WCd;
            Mana.Value -= WCost;
            ServerHeal(WFlowHeal);
            HasteUntilTime.Value = ServerTimeF + 2f;
            FlowFxRpc();
        }

        [Rpc(SendTo.Server)]
        public void CastERpc()
        {
            if (Dead.Value || !GameManager.Playing || !UltReady) return;
            if (ServerTimeF < _srvNextE - 0.08f || Mana.Value < ECost) return;
            _srvNextE = ServerTimeF + ECd;
            Mana.Value -= ECost;

            Vector3 center = transform.position;
            float radius = IsAdanos ? 5.5f : 5f;
            float slowDur = IsAdanos ? 3f : 2f;
            UltFxRpc(center, radius);
            int hits = 0;
            // snapshot: ServerTakeDamage can despawn units and mutate All
            var targets = All.ToArray();
            foreach (var u in targets)
            {
                if (u == null || !u.Alive || u.Team == Team || u.Team == Team.None) continue;
                if (GameConstants.Dist2D(center, u.Pos) > radius + u.BodyRadius) continue;
                if (u is Hero h) h.SlowUntilTime.Value = ServerTimeF + slowDur;
                if (u is Minion m) m.ServerSlow(slowDur);
                u.ServerTakeDamage(EDamage, this);
                hits++;
            }
            if (IsBelial && hits > 0)
                ServerHeal(EHealPerHit * hits);
        }

        [Rpc(SendTo.Server)]
        public void UpgradeSkillRpc(int slot)
        {
            if (slot < 0 || slot > 2 || SkillPoints.Value <= 0) return;
            if (slot == 2 && Level.Value < UltLevel) return;
            var nv = slot == 0 ? QLvl : slot == 1 ? WLvl : ELvl;
            if (nv.Value >= SkillCap) return;
            nv.Value++;
            SkillPoints.Value--;
        }

        [Rpc(SendTo.Server)]
        public void BuyUpgradeRpc(int index)
        {
            if (index < 0 || index > 2 || Dead.Value) return;
            var counter = index == 0 ? AtkUp : index == 1 ? HpUp : AsUp;
            if (counter.Value >= UpgradeCap[index]) return;
            if (Gold.Value < UpgradeCost[index]) return;
            Gold.Value -= UpgradeCost[index];
            counter.Value++;
            ServerRecalcStats();
        }

        [Rpc(SendTo.Owner)]
        void TeleportOwnerRpc(Vector3 pos)
        {
            TeleportLocal(pos);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void SwingFxRpc()
        {
            var anim = GetComponent<UnitAnimator>();
            if (anim != null) anim.TriggerAttack();
        }

        [Rpc(SendTo.ClientsAndHost)]
        void BlinkFxRpc(Vector3 from, Vector3 to)
        {
            RingFx.Spawn(from, new Color(0.4f, 0.7f, 1f), 1.6f, 0.5f);
            RingFx.Spawn(to, new Color(0.6f, 0.85f, 1f), 1.9f, 0.6f);
            ParticleFx.SpawnTinted("FxSparkBurst", to + Vector3.up, new Color(0.6f, 0.85f, 1f));
        }

        [Rpc(SendTo.ClientsAndHost)]
        void BeamFxRpc(Vector3 from, Vector3 to)
        {
            FxBeam.Spawn(from, to, new Color(1f, 0.2f, 0.3f));
            ParticleFx.SpawnTinted("FxSparkBurst", to, new Color(1f, 0.25f, 0.3f), 0.7f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void FlowFxRpc()
        {
            RingFx.Spawn(transform.position, new Color(0.35f, 0.9f, 0.95f), 2.4f, 0.7f);
            ParticleFx.Spawn("FxWaterBlast", transform.position, null, 0.7f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void UltFxRpc(Vector3 pos, float radius)
        {
            string blast = IsBelial ? "FxDarkBlast" : IsAdanos ? "FxWaterBlast" : "FxFireBlast";
            ParticleFx.Spawn(blast, pos);
            RingFx.Spawn(pos, KindColor, radius, 0.9f);
            CameraRig.Shake(0.15f, 0.4f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void LevelUpFxRpc(int newLevel)
        {
            DamagePopup.Spawn(transform.position + Vector3.up * 3f, "УРОВЕНЬ " + newLevel,
                new Color(1f, 0.95f, 0.3f), 1.4f);
            RingFx.Spawn(transform.position, new Color(1f, 0.92f, 0.4f), 2.4f, 0.8f);
            ParticleFx.SpawnTinted("FxSparkBurst", transform.position + Vector3.up,
                new Color(1f, 0.92f, 0.4f));
        }

        // ============================== SERVER ==============================

        void ServerUpdate()
        {
            float dt = Time.deltaTime;

            if (Dead.Value)
            {
                if (ServerTimeF >= RespawnAtTime.Value)
                    ServerRespawn();
                return;
            }

            // regen (Adanos passively restores mana much faster)
            ServerHeal((2.5f + 0.5f * Level.Value) * dt);
            float manaRegen = (3f + 0.4f * Level.Value) * (IsAdanos ? 1.6f : 1f);
            Mana.Value = Mathf.Min(MaxMana.Value, Mana.Value + manaRegen * dt);

            // fountain
            if (GameConstants.Dist2D(transform.position, GameConstants.BasePos(Team)) < GameConstants.FountainRadius)
            {
                ServerHeal(60f * dt);
                Mana.Value = Mathf.Min(MaxMana.Value, Mana.Value + 45f * dt);
            }

            // passive income while the game is running
            if (GameManager.Playing)
            {
                _goldAcc += 1.2f * dt;
                _xpAcc += 4f * dt;
                if (_goldAcc >= 1f)
                {
                    int g = (int)_goldAcc;
                    _goldAcc -= g;
                    Gold.Value += g;
                }
                if (_xpAcc >= 1f)
                {
                    int x = (int)_xpAcc;
                    _xpAcc -= x;
                    ServerAddXp(x);
                }
            }
        }

        void ServerRecalcStats()
        {
            MaxHp.Value = StatMaxHp;
            MaxMana.Value = StatMaxMana;
            Hp.Value = Mathf.Min(Hp.Value, MaxHp.Value);
            Mana.Value = Mathf.Min(Mana.Value, MaxMana.Value);
        }

        public void ServerAddGold(int amount)
        {
            if (!IsServer || !IsSpawned) return;
            Gold.Value += amount;
        }

        public void ServerAddXp(int amount)
        {
            if (!IsServer || !IsSpawned || Level.Value >= MaxLevel) return;
            Xp.Value += amount;
            while (Level.Value < MaxLevel && Xp.Value >= XpToNext)
            {
                Xp.Value -= XpToNext;
                Level.Value++;
                SkillPoints.Value++;
                ServerRecalcStats();
                ServerHeal(MaxHp.Value * 0.25f);
                Mana.Value = Mathf.Min(MaxMana.Value, Mana.Value + MaxMana.Value * 0.25f);
                LevelUpFxRpc(Level.Value);
            }
            if (Level.Value >= MaxLevel) Xp.Value = 0;
        }

        protected override void OnServerDeath(UnitBase killer)
        {
            // heroes respawn instead of despawning
            Dead.Value = true;
            Deaths.Value++;
            RespawnAtTime.Value = ServerTimeF + 4f + 1.5f * Level.Value;
            if (killer is Hero hk && hk.IsSpawned && hk.Team != Team)
            {
                hk.Kills.Value++;
                hk.ServerAddGold(250);
                hk.ServerAddXp(150);
            }
        }

        void ServerRespawn()
        {
            ServerRecalcStats();
            Hp.Value = MaxHp.Value;
            Mana.Value = MaxMana.Value;
            Dead.Value = false;
            TeleportOwnerRpc(GameConstants.HeroSpawn(Team));
        }
    }
}
