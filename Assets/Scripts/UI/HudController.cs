using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Moba
{
    /// Full IMGUI interface: menu with hero select, in-game HUD with icons,
    /// minimap, eruption warnings and the end screen.
    public class HudController : MonoBehaviour
    {
        enum UiState { Menu, Connecting, Waiting, Playing, Ended, Leaving }

        static string _warnText = "";
        static float _warnUntil;
        static bool _autoStartDone;

        public static void Warn(string text, float duration)
        {
            _warnText = text;
            _warnUntil = Time.time + duration;
        }

        string _ip = "127.0.0.1";
        string _statusMessage = "";
        string _localIps = "";
        bool _wasInSession;
        bool _showHelp;
        bool _confirmLeave;
        bool _stylesReady;

        GUIStyle _title, _h1, _h2, _label, _small, _button, _bigButton, _panel, _warn;
        readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();

        Texture2D Tex(string name)
        {
            if (!_tex.TryGetValue(name, out var t))
            {
                t = Resources.Load<Texture2D>("UI/" + name);
                _tex[name] = t;
            }
            return t;
        }

        void Start()
        {
            _ip = PlayerPrefs.GetString("moba_last_ip", "127.0.0.1");
            Hero.LocalChoice = (HeroKind)PlayerPrefs.GetInt("moba_hero", 0);
            _localIps = DetectLocalIps();
            HandleCommandLine();
        }

        // testing helpers: MobaGame.app -host | -join <ip> [-hero 0|1]
        void HandleCommandLine()
        {
            if (_autoStartDone) return;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-hero" && i + 1 < args.Length && byte.TryParse(args[i + 1], out var hk))
                    Hero.LocalChoice = (HeroKind)hk;
            }
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host")
                {
                    _autoStartDone = true;
                    StartHost();
                }
                else if (args[i] == "-join" && i + 1 < args.Length)
                {
                    _autoStartDone = true;
                    _ip = args[i + 1];
                    StartClient();
                }
            }
        }

        void Update()
        {
            var nm = NetworkManager.Singleton;
            bool inSession = nm != null && (nm.IsServer || nm.IsClient);
            if (_wasInSession && !inSession && string.IsNullOrEmpty(_statusMessage))
                _statusMessage = "Соединение потеряно";
            _wasInSession = inSession;

            if (Input.GetKeyDown(KeyCode.F1)) _showHelp = !_showHelp;
            if (Input.GetKeyDown(KeyCode.Escape) && State() == UiState.Playing)
                _confirmLeave = !_confirmLeave;
        }

        UiState State()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return UiState.Menu;
            var gm = GameManager.Instance;
            if (gm != null && gm.IsSpawned && gm.Winner.Value != 0) return UiState.Ended;
            if (nm.IsServer || nm.IsHost)
                return gm != null && gm.IsSpawned && gm.GameStarted.Value ? UiState.Playing : UiState.Waiting;
            if (nm.IsClient)
            {
                if (!nm.IsConnectedClient) return UiState.Connecting;
                return gm != null && gm.IsSpawned && gm.GameStarted.Value ? UiState.Playing : UiState.Waiting;
            }
            return UiState.Menu;
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Max(0.7f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            switch (State())
            {
                case UiState.Menu: DrawMenu(w, h); break;
                case UiState.Connecting: DrawConnecting(w, h); break;
                case UiState.Waiting: DrawWaiting(w, h); break;
                case UiState.Playing: DrawGameHud(w, h); break;
                case UiState.Ended: DrawEnd(w, h); break;
                case UiState.Leaving: DrawCenterText(w, h, "Выход..."); break;
            }

            if (Time.time < _warnUntil && State() == UiState.Playing)
            {
                float blink = 0.7f + Mathf.PingPong(Time.time * 3f, 0.3f);
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.5f, 0.15f, blink);
                GUI.Label(new Rect(w / 2f - 420, h * 0.16f, 840, 50), _warnText, _warn);
                GUI.color = prev;
            }

            if (_showHelp) DrawHelp(w, h);
        }

        // ============================== MENU ==============================

        void DrawMenu(float w, float h)
        {
            var bg = Tex("menu_bg");
            if (bg != null)
                GUI.DrawTexture(new Rect(0, 0, w, h), bg, ScaleMode.ScaleAndCrop);

            float cx = w / 2f;
            GUI.Label(new Rect(cx - 400, h * 0.05f, 800, 80), "ЛИНИЯ БИТВЫ", _title);
            GUI.Label(new Rect(cx - 400, h * 0.05f + 68, 800, 32),
                "1v1 MOBA между двух вулканов", _h2);

            // ---- hero select ----
            GUI.Label(new Rect(cx - 400, h * 0.21f, 800, 30), "ВЫБЕРИ ГЕРОЯ:", _h1);
            float py = h * 0.21f + 38;
            DrawHeroCard(new Rect(cx - 330, py, 320, 170), HeroKind.Xardaras);
            DrawHeroCard(new Rect(cx + 10, py, 320, 170), HeroKind.Belial);

            // selected hero skills
            float sy = py + 184;
            var k = Hero.LocalChoice;
            for (int i = 0; i < 3; i++)
            {
                var meta = HeroData.Skill(k, i);
                var icon = Tex(meta.icon);
                var r = new Rect(cx - 330, sy + i * 46, 660, 42);
                if (icon != null)
                    GUI.DrawTexture(new Rect(r.x, r.y, 40, 40), icon, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(r.x + 50, r.y + 2, 620, 38),
                    "QWE"[i] + " — " + meta.name + ": " + meta.desc, _label);
            }

            // ---- connect ----
            float y = sy + 3 * 46 + 16;
            if (GUI.Button(new Rect(cx - 170, y, 340, 60), "СОЗДАТЬ ИГРУ (ХОСТ)", _bigButton))
                StartHost();

            y += 78;
            _ip = GUI.TextField(new Rect(cx - 170, y, 220, 44), _ip);
            if (GUI.Button(new Rect(cx + 60, y, 110, 44), "ВОЙТИ", _button))
                StartClient();

            if (!string.IsNullOrEmpty(_localIps))
                GUI.Label(new Rect(cx - 320, y + 52, 640, 28),
                    "Ваш IP в локальной сети: " + _localIps, _small);
            if (!string.IsNullOrEmpty(_statusMessage))
                GUI.Label(new Rect(cx - 320, y + 78, 640, 28), _statusMessage, _small);

            GUI.Label(new Rect(cx - 320, h - 36, 640, 28),
                "F1 — управление  |  порт " + GameConstants.Port, _small);
        }

        void DrawHeroCard(Rect r, HeroKind k)
        {
            bool selected = Hero.LocalChoice == k;
            var panel = Tex("panel");
            if (panel != null)
                GUI.DrawTexture(r, panel, ScaleMode.StretchToFill, true, 0,
                    selected ? Color.white : new Color(1f, 1f, 1f, 0.55f), 0, 12);
            var portrait = Tex(HeroData.Portrait(k));
            if (portrait != null)
                GUI.DrawTexture(new Rect(r.x + 12, r.y + 12, 146, 146), portrait,
                    ScaleMode.ScaleToFit);
            GUI.Label(new Rect(r.x + 168, r.y + 26, r.width - 178, 34), HeroData.Name(k), _h1);
            GUI.Label(new Rect(r.x + 168, r.y + 62, r.width - 178, 30), HeroData.Title(k), _h2);
            if (selected)
                GUI.Label(new Rect(r.x + 168, r.y + 104, r.width - 178, 30), "< ВЫБРАН >", _h2);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                Hero.LocalChoice = k;
                PlayerPrefs.SetInt("moba_hero", (int)k);
            }
        }

        void StartHost()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            _statusMessage = "";
            var utp = nm.GetComponent<UnityTransport>();
            utp.SetConnectionData("127.0.0.1", GameConstants.Port, "0.0.0.0");
            if (!nm.StartHost())
                _statusMessage = "Не удалось создать игру (порт занят?)";
        }

        void StartClient()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            string ip = _ip.Trim();
            if (string.IsNullOrEmpty(ip)) return;
            PlayerPrefs.SetString("moba_last_ip", ip);
            _statusMessage = "";
            var utp = nm.GetComponent<UnityTransport>();
            utp.SetConnectionData(ip, GameConstants.Port);
            if (!nm.StartClient())
                _statusMessage = "Не удалось начать подключение";
        }

        void DrawConnecting(float w, float h)
        {
            DrawCenterText(w, h, "Подключение к " + _ip.Trim() + "...");
            if (GUI.Button(new Rect(w / 2f - 90, h / 2f + 60, 180, 48), "ОТМЕНА", _button))
                Leave();
        }

        void DrawWaiting(float w, float h)
        {
            DrawCenterText(w, h, "Ожидание второго игрока...");
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsHost && !string.IsNullOrEmpty(_localIps))
                GUI.Label(new Rect(w / 2f - 400, h / 2f + 40, 800, 32),
                    "Сообщите сопернику ваш IP: " + _localIps, _h2);
            GUI.Label(new Rect(w / 2f - 400, h / 2f + 76, 800, 30),
                "Ваш герой: " + HeroData.Name(Hero.LocalChoice), _h2);
            if (GUI.Button(new Rect(w / 2f - 90, h / 2f + 120, 180, 48), "ВЫЙТИ", _button))
                Leave();
        }

        // ============================== GAME HUD ==============================

        void DrawGameHud(float w, float h)
        {
            var hero = Hero.Local;
            DrawMinimap(w, h);
            DrawTopBar(w, h, hero);
            if (hero == null) return;

            if (hero.Dead.Value)
            {
                float remain = Mathf.Max(0f,
                    hero.RespawnAtTime.Value - NetworkManager.Singleton.ServerTime.TimeAsFloat);
                DrawCenterText(w, h, "Возрождение через " + Mathf.CeilToInt(remain) + "...");
            }

            DrawBarsAndSkills(w, h, hero);
            DrawUpgrades(w, h, hero);

            if (_confirmLeave)
            {
                DrawPanel(new Rect(w / 2f - 180, h / 2f - 80, 360, 160));
                GUI.Label(new Rect(w / 2f - 180, h / 2f - 65, 360, 36), "Покинуть матч?", _h1);
                if (GUI.Button(new Rect(w / 2f - 150, h / 2f, 140, 48), "ДА", _button))
                    Leave();
                if (GUI.Button(new Rect(w / 2f + 10, h / 2f, 140, 48), "НЕТ", _button))
                    _confirmLeave = false;
            }
        }

        void DrawTopBar(float w, float h, Hero hero)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                int t = Mathf.FloorToInt(gm.MatchTime);
                GUI.Label(new Rect(w / 2f - 60, 8, 120, 30),
                    string.Format("{0}:{1:00}", t / 60, t % 60), _h1);
            }
            if (hero != null)
            {
                var coin = Tex("coin");
                var star = Tex("star");
                if (star != null) GUI.DrawTexture(new Rect(14, 8, 26, 26), star, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(44, 8, 80, 28), "" + hero.Level.Value, _label);
                if (coin != null) GUI.DrawTexture(new Rect(92, 8, 26, 26), coin, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(122, 8, 110, 28), "" + hero.Gold.Value, _label);
                GUI.Label(new Rect(232, 8, 200, 28),
                    "У/С: " + hero.Kills.Value + "/" + hero.Deaths.Value, _label);
                if (hero.Level.Value < Hero.MaxLevel)
                    DrawBar(new Rect(16, 40, 220, 8), (float)hero.Xp.Value / hero.XpToNext,
                        new Color(0.8f, 0.6f, 1f));
            }
            GUI.Label(new Rect(16, 52, 300, 26), "F1 — управление, Esc — меню", _small);
        }

        void DrawBarsAndSkills(float w, float h, Hero hero)
        {
            float cx = w / 2f;
            float y = h - 150;

            // portrait
            var portrait = Tex(HeroData.Portrait(hero.HeroType));
            if (portrait != null)
            {
                GUI.DrawTexture(new Rect(cx - 340, y - 6, 74, 74), portrait, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(cx - 340, y + 66, 74, 24), HeroData.Name(hero.HeroType), _small);
            }

            // HP / mana
            DrawBar(new Rect(cx - 260, y, 520, 22), hero.Hp.Value / Mathf.Max(1f, hero.MaxHp.Value),
                new Color(0.25f, 0.85f, 0.3f));
            GUI.Label(new Rect(cx - 260, y - 2, 520, 22),
                Mathf.CeilToInt(hero.Hp.Value) + " / " + Mathf.CeilToInt(hero.MaxHp.Value), _small);
            y += 26;
            DrawBar(new Rect(cx - 260, y, 520, 14), hero.Mana.Value / Mathf.Max(1f, hero.MaxMana.Value),
                new Color(0.3f, 0.5f, 1f));

            // skills
            y += 26;
            var k = hero.HeroType;
            DrawSkill(new Rect(cx - 230, y, 84, 84), "Q", HeroData.Skill(k, 0).icon,
                hero.CdQUntil, hero.QCd, hero.QCost, hero, true);
            DrawSkill(new Rect(cx - 130, y, 84, 84), "W", HeroData.Skill(k, 1).icon,
                hero.CdWUntil, hero.WCd, hero.WCost, hero, true);
            DrawSkill(new Rect(cx - 30, y, 84, 84), "E", HeroData.Skill(k, 2).icon,
                hero.CdEUntil, hero.ECd, hero.ECost, hero, hero.UltReady);
            DrawSkill(new Rect(cx + 70, y, 84, 84), "SPACE", "attack",
                hero.CdAttackUntil, hero.AttackCooldown, 0f, hero, true);
        }

        void DrawSkill(Rect r, string key, string iconName, float cdUntil, float cdTotal,
            float manaCost, Hero hero, bool unlocked)
        {
            var icon = Tex(iconName);
            if (icon != null)
            {
                var prev = GUI.color;
                GUI.color = unlocked ? Color.white : new Color(0.35f, 0.35f, 0.35f);
                GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
                GUI.color = prev;
            }
            else
                DrawPanel(r);
            GUI.Label(new Rect(r.x + 4, r.y + 2, r.width - 8, 20), key, _small);
            if (!unlocked)
            {
                GUI.Label(new Rect(r.x, r.y + r.height / 2f - 11, r.width, 22), "с 4 ур.", _small);
                return;
            }
            if (manaCost > 0f)
            {
                bool enough = hero.Mana.Value >= manaCost;
                var prev = GUI.color;
                GUI.color = enough ? new Color(0.55f, 0.75f, 1f) : new Color(1f, 0.4f, 0.4f);
                GUI.Label(new Rect(r.x, r.y + r.height - 24, r.width, 22), manaCost.ToString("0"), _small);
                GUI.color = prev;
            }
            float remain = cdUntil - Time.time;
            if (remain > 0f)
            {
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.65f);
                GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8,
                    (r.height - 8) * Mathf.Clamp01(remain / cdTotal)), Texture2D.whiteTexture);
                GUI.color = prev;
                GUI.Label(new Rect(r.x, r.y + r.height / 2f - 14, r.width, 28),
                    remain.ToString("0.0"), _h1);
            }
        }

        void DrawUpgrades(float w, float h, Hero hero)
        {
            float x = w - 270;
            float y = h - 180;
            GUI.Label(new Rect(x, y - 28, 260, 24), "Покупки (клавиши 1-3):", _small);
            for (int i = 0; i < 3; i++)
            {
                int count = i == 0 ? hero.AtkUp.Value : i == 1 ? hero.HpUp.Value : hero.AsUp.Value;
                bool can = hero.Gold.Value >= Hero.UpgradeCost[i] && count < Hero.UpgradeCap[i];
                var r = new Rect(x, y + i * 52, 260, 46);
                GUI.enabled = can;
                if (GUI.Button(r, GUIContent.none, _button))
                    hero.RequestBuy(i);
                GUI.enabled = true;
                var icon = Tex(Hero.UpgradeIcon[i]);
                var prev = GUI.color;
                GUI.color = can ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                if (icon != null)
                    GUI.DrawTexture(new Rect(r.x + 4, r.y + 3, 40, 40), icon, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(r.x + 50, r.y + 3, 150, 22),
                    (i + 1) + ". " + Hero.UpgradeName[i], _small);
                GUI.Label(new Rect(r.x + 50, r.y + 23, 150, 22),
                    Hero.UpgradeCost[i] + "з  [" + count + "/" + Hero.UpgradeCap[i] + "]", _small);
                GUI.color = prev;
            }
        }

        void DrawMinimap(float w, float h)
        {
            float mw = 190f, mh = 80f;
            var r = new Rect(w - mw - 12, 12, mw, mh);
            DrawPanel(r);
            foreach (var u in UnitBase.All)
            {
                if (u == null || !u.Alive) continue;
                if (u is Hero eh && !eh.IsVisibleLocally) continue;
                float px = r.x + (u.Pos.x + 35f) / 70f * mw;
                float pz = r.y + mh - (u.Pos.z + 13f) / 26f * mh;
                float size = u is BaseCore ? 10f : u is Tower ? 8f : u is Hero ? 7f : 4f;
                var prev = GUI.color;
                GUI.color = u == (UnitBase)Hero.Local
                    ? Color.white
                    : GameConstants.TeamColor(u.Team);
                GUI.DrawTexture(new Rect(px - size / 2f, pz - size / 2f, size, size),
                    Texture2D.whiteTexture);
                GUI.color = prev;
            }
        }

        // ============================== END / MISC ==============================

        void DrawEnd(float w, float h)
        {
            var gm = GameManager.Instance;
            var hero = Hero.Local;
            bool win = gm != null && hero != null && gm.Winner.Value == (byte)hero.Team;
            GUI.Label(new Rect(w / 2f - 400, h * 0.3f, 800, 90),
                win ? "ПОБЕДА!" : "ПОРАЖЕНИЕ", _title);
            if (gm != null && hero == null)
                GUI.Label(new Rect(w / 2f - 400, h * 0.3f + 90, 800, 40),
                    "Победили " + (gm.Winner.Value == (byte)Team.Blue ? "синие" : "красные"), _h2);
            if (GUI.Button(new Rect(w / 2f - 130, h * 0.55f, 260, 60), "В МЕНЮ", _bigButton))
                Leave();
        }

        void DrawHelp(float w, float h)
        {
            var r = new Rect(w / 2f - 300, h / 2f - 210, 600, 420);
            DrawPanel(r);
            GUI.Label(new Rect(r.x, r.y + 12, r.width, 34), "УПРАВЛЕНИЕ", _h1);
            string[] lines =
            {
                "WASD / стрелки или зажать ПКМ — движение",
                "Пробел / ЛКМ — атака ближайшей цели",
                "Q — снаряд (в сторону курсора)",
                "W — Ксардарас: телепорт; Белиал: похищение жизни",
                "E — ульта вокруг героя (с 4 уровня)",
                "1 / 2 / 3 — купить улучшение за золото",
                "Колесо мыши — зум камеры",
                "",
                "ЛАВА: после извержения не стойте в оранжевых лужах!",
                "Кусты скрывают вас от врага.",
                "У базы быстро восстанавливаются HP и мана.",
                "Цель — уничтожить вражескую базу!"
            };
            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(r.x + 24, r.y + 56 + i * 26, r.width - 48, 26), lines[i], _label);
            if (GUI.Button(new Rect(r.x + r.width / 2f - 80, r.y + r.height - 56, 160, 42),
                    "ЗАКРЫТЬ (F1)", _button))
                _showHelp = false;
        }

        void DrawCenterText(float w, float h, string text)
        {
            GUI.Label(new Rect(w / 2f - 400, h / 2f - 60, 800, 60), text, _h1);
        }

        void DrawPanel(Rect r)
        {
            var p = Tex("panel");
            if (p != null)
                GUI.DrawTexture(r, p, ScaleMode.StretchToFill, true, 0, Color.white, 0, 12);
            else
                GUI.Box(r, "");
        }

        void DrawBar(Rect r, float frac, Color color)
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(frac), r.height - 2),
                Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void Leave()
        {
            _confirmLeave = false;
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.Shutdown();
            StartCoroutine(ReloadSoon());
        }

        IEnumerator ReloadSoon()
        {
            yield return new WaitForSeconds(0.3f);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _title.normal.textColor = new Color(1f, 0.85f, 0.6f);
            _h1 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _h2 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19, alignment = TextAnchor.MiddleCenter
            };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleCenter
            };
            _warn = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
            _bigButton = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            var btn = Tex("btn");
            var btnHi = Tex("btn_hi");
            if (btn != null)
            {
                foreach (var s in new[] { _button, _bigButton })
                {
                    s.normal.background = btn;
                    s.hover.background = btnHi != null ? btnHi : btn;
                    s.active.background = btnHi != null ? btnHi : btn;
                    s.border = new RectOffset(14, 14, 14, 14);
                    s.normal.textColor = new Color(1f, 0.92f, 0.8f);
                    s.hover.textColor = Color.white;
                    s.active.textColor = Color.white;
                }
            }
            _h2.normal.textColor = new Color(0.95f, 0.85f, 0.75f);
            _small.normal.textColor = new Color(0.85f, 0.76f, 0.68f);
        }

        static string DetectLocalIps()
        {
            try
            {
                var result = "";
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string s = addr.Address.ToString();
                        if (s.StartsWith("169.254")) continue;
                        if (result.Length > 0) result += ", ";
                        result += s;
                    }
                }
                return result;
            }
            catch
            {
                return "";
            }
        }
    }
}
