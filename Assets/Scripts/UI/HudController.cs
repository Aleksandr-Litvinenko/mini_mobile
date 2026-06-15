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
    /// Full IMGUI interface: animated main menu, mode select (1v1 / training),
    /// hero select, in-game HUD with icons, touch controls, minimap, end screen.
    public class HudController : MonoBehaviour
    {
        enum UiState { Menu, Connecting, Waiting, Playing, Ended, Leaving }

        enum MenuScreen { Main, Mode, Setup }

        static string _warnText = "";
        static float _warnUntil;
        static bool _autoStartDone;

        public static void Warn(string text, float duration)
        {
            _warnText = text;
            _warnUntil = Time.time + duration;
        }

        MenuScreen _screen = MenuScreen.Main;
        bool _training;
        float _nextMenuErupt;
        string _ip = "127.0.0.1";
        string _statusMessage = "";
        string _localIps = "";
        bool _wasInSession;
        bool _showHelp;
        bool _confirmLeave;
        bool _showSkillInfo;
        bool _stylesReady;
        float _scale = 1f;
        string _tooltip;
        Rect _tooltipAnchor;

        GUIStyle _title, _h1, _h2, _label, _small, _button, _bigButton, _warn;
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
            Hero.LocalChoice = (HeroKind)Mathf.Clamp(PlayerPrefs.GetInt("moba_hero", 0), 0,
                HeroData.Count - 1);
            TouchHud.Enabled = Application.isMobilePlatform ||
                               PlayerPrefs.GetInt("moba_touch", 0) == 1;
            _localIps = DetectLocalIps();
            HandleCommandLine();
        }

        // testing helpers: MiniMoba.app -host | -join <ip> | -training [-hero 0|1|2]
        void HandleCommandLine()
        {
            if (_autoStartDone) return;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-hero" && i + 1 < args.Length && byte.TryParse(args[i + 1], out var hk))
                    Hero.LocalChoice = (HeroKind)Mathf.Clamp(hk, 0, HeroData.Count - 1);
            }
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host")
                {
                    _autoStartDone = true;
                    GameManager.TrainingMode = false;
                    StartHost();
                }
                else if (args[i] == "-join" && i + 1 < args.Length)
                {
                    _autoStartDone = true;
                    _ip = args[i + 1];
                    StartClient();
                }
                else if (args[i] == "-training")
                {
                    _autoStartDone = true;
                    GameManager.TrainingMode = true;
                    StartHost();
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
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                var st = State();
                if (st == UiState.Playing) _confirmLeave = !_confirmLeave;
                else if (st == UiState.Menu && _screen != MenuScreen.Main)
                    _screen = _screen == MenuScreen.Setup ? MenuScreen.Mode : MenuScreen.Main;
            }

            _scale = Mathf.Max(0.7f, Screen.height / 900f) *
                     (Application.isMobilePlatform ? 1.25f : 1f);
            float vw = Screen.width / _scale;
            float vh = Screen.height / _scale;
            TouchHud.UpdateInput(_scale, vw, vh,
                State() == UiState.Playing && Hero.Local != null && !Hero.Local.Dead.Value);

            // the menu lives inside the live volcano scene — erupt now and then
            if (State() == UiState.Menu && Time.time >= _nextMenuErupt)
            {
                _nextMenuErupt = Time.time + 6.5f;
                MapBuilder.EruptVolcanoes();
                CameraRig.Shake(0.1f, 1f);
            }
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
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float w = Screen.width / _scale;
            float h = Screen.height / _scale;

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

        void Shade(float w, float h)
        {
            // darken top/bottom of the live 3D scene so text stays readable
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, w, h * 0.24f), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, h * 0.7f, w, h * 0.3f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawMenu(float w, float h)
        {
            Shade(w, h);
            switch (_screen)
            {
                case MenuScreen.Main: DrawMenuMain(w, h); break;
                case MenuScreen.Mode: DrawMenuMode(w, h); break;
                case MenuScreen.Setup: DrawMenuSetup(w, h); break;
            }
        }

        void DrawMenuMain(float w, float h)
        {
            float cx = w / 2f;
            float pulse = 0.5f + Mathf.PingPong(Time.time * 0.6f, 0.5f);
            var prev = GUI.color;
            GUI.color = Color.Lerp(new Color(1f, 0.75f, 0.45f), new Color(1f, 0.95f, 0.7f), pulse);
            GUI.Label(new Rect(cx - 420, h * 0.07f, 840, 100), "MINI MOBA", _title);
            GUI.color = prev;
            GUI.Label(new Rect(cx - 400, h * 0.07f + 86, 800, 32), "битва на вулканах", _h2);

            if (GUI.Button(new Rect(cx - 160, h * 0.74f, 320, 64), "ИГРАТЬ", _bigButton))
            {
                _statusMessage = "";
                _screen = MenuScreen.Mode;
            }

            string touchLabel = "Сенсорное управление: " + (TouchHud.Enabled ? "ВКЛ" : "ВЫКЛ");
            if (GUI.Button(new Rect(cx - 160, h * 0.74f + 76, 320, 40), touchLabel, _button))
            {
                TouchHud.Enabled = !TouchHud.Enabled;
                PlayerPrefs.SetInt("moba_touch", TouchHud.Enabled ? 1 : 0);
            }

            GUI.Label(new Rect(cx - 320, h - 32, 640, 26),
                "F1 — управление  |  порт " + GameConstants.Port, _small);
        }

        void DrawMenuMode(float w, float h)
        {
            float cx = w / 2f;
            GUI.Label(new Rect(cx - 400, h * 0.1f, 800, 50), "ВЫБОР РЕЖИМА", _h1);

            var r1 = new Rect(cx - 380, h * 0.24f, 360, 300);
            var r2 = new Rect(cx + 20, h * 0.24f, 360, 300);

            DrawPanel(r1);
            var bg = Tex("menu_bg");
            if (bg != null)
                GUI.DrawTexture(new Rect(r1.x + 14, r1.y + 14, r1.width - 28, 150), bg,
                    ScaleMode.ScaleAndCrop);
            GUI.Label(new Rect(r1.x, r1.y + 172, r1.width, 34), "1 НА 1", _h1);
            GUI.Label(new Rect(r1.x + 16, r1.y + 208, r1.width - 32, 60),
                "Битва на вулканах: сетевой матч против друга по IP", _h2);
            if (GUI.Button(new Rect(r1.x + 60, r1.y + 248, r1.width - 120, 42), "ВЫБРАТЬ", _button))
            {
                _training = false;
                _screen = MenuScreen.Setup;
            }

            DrawPanel(r2);
            var pb = Tex("portrait_b");
            if (pb != null)
                GUI.DrawTexture(new Rect(r2.x + r2.width / 2f - 75, r2.y + 14, 150, 150), pb,
                    ScaleMode.ScaleToFit);
            GUI.Label(new Rect(r2.x, r2.y + 172, r2.width, 34), "ТРЕНИРОВКА", _h1);
            GUI.Label(new Rect(r2.x + 16, r2.y + 208, r2.width - 32, 60),
                "Одиночный бой против компьютера — освой героев", _h2);
            if (GUI.Button(new Rect(r2.x + 60, r2.y + 248, r2.width - 120, 42), "ВЫБРАТЬ", _button))
            {
                _training = true;
                _screen = MenuScreen.Setup;
            }

            if (GUI.Button(new Rect(cx - 90, h * 0.24f + 320, 180, 42), "НАЗАД", _button))
                _screen = MenuScreen.Main;
        }

        void DrawMenuSetup(float w, float h)
        {
            float cx = w / 2f;
            GUI.Label(new Rect(cx - 400, h * 0.045f, 800, 40),
                _training ? "ТРЕНИРОВКА — ВЫБЕРИ ГЕРОЯ" : "1 НА 1 — ВЫБЕРИ ГЕРОЯ", _h1);

            float cardW = 300f, gap = 14f;
            float left = cx - (cardW * 3 + gap * 2) / 2f;
            float py = h * 0.045f + 50;
            for (int i = 0; i < HeroData.Count; i++)
                DrawHeroCard(new Rect(left + i * (cardW + gap), py, cardW, 168), (HeroKind)i);

            // selected hero details
            float sy = py + 182;
            var k = Hero.LocalChoice;
            GUI.Label(new Rect(cx - 380, sy, 760, 26),
                "Пассивно: " + HeroData.Passive(k), _h2);
            sy += 32;
            for (int i = 0; i < 3; i++)
            {
                var meta = HeroData.Skill(k, i);
                var icon = Tex(meta.icon);
                var r = new Rect(cx - 360, sy + i * 44, 720, 40);
                if (icon != null)
                    GUI.DrawTexture(new Rect(r.x, r.y, 38, 38), icon, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(r.x + 48, r.y + 2, 680, 36),
                    "QWE"[i] + " — " + meta.name + ": " + meta.desc, _label);
            }

            float y = sy + 3 * 44 + 14;
            if (_training)
            {
                if (GUI.Button(new Rect(cx - 170, y, 340, 58), "НАЧАТЬ ТРЕНИРОВКУ", _bigButton))
                {
                    GameManager.TrainingMode = true;
                    StartHost();
                }
                y += 66;
            }
            else
            {
                if (GUI.Button(new Rect(cx - 170, y, 340, 52), "СОЗДАТЬ ИГРУ (ХОСТ)", _bigButton))
                {
                    GameManager.TrainingMode = false;
                    StartHost();
                }
                y += 62;
                _ip = GUI.TextField(new Rect(cx - 170, y, 220, 42), _ip);
                if (GUI.Button(new Rect(cx + 58, y, 112, 42), "ВОЙТИ", _button))
                {
                    GameManager.TrainingMode = false;
                    StartClient();
                }
                y += 48;
                if (!string.IsNullOrEmpty(_localIps))
                    GUI.Label(new Rect(cx - 340, y, 680, 24),
                        "Ваш IP в локальной сети: " + _localIps, _small);
                y += 24;
            }
            if (!string.IsNullOrEmpty(_statusMessage))
                GUI.Label(new Rect(cx - 340, y, 680, 24), _statusMessage, _small);

            if (GUI.Button(new Rect(20, h - 62, 140, 42), "НАЗАД", _button))
                _screen = MenuScreen.Mode;
        }

        void DrawHeroCard(Rect r, HeroKind k)
        {
            bool selected = Hero.LocalChoice == k;
            var panel = Tex("panel");
            if (panel != null)
                GUI.DrawTexture(r, panel, ScaleMode.StretchToFill, true, 0,
                    selected ? Color.white : new Color(1f, 1f, 1f, 0.45f), 0, 12);
            var portrait = Tex(HeroData.Portrait(k));
            if (portrait != null)
            {
                var prev = GUI.color;
                GUI.color = selected ? Color.white : new Color(0.75f, 0.75f, 0.75f);
                GUI.DrawTexture(new Rect(r.x + 10, r.y + 10, 148, 148), portrait,
                    ScaleMode.ScaleToFit);
                GUI.color = prev;
            }
            GUI.Label(new Rect(r.x + 162, r.y + 22, r.width - 170, 32), HeroData.Name(k), _h1);
            GUI.Label(new Rect(r.x + 162, r.y + 58, r.width - 170, 28), HeroData.Title(k), _h2);
            if (selected)
                GUI.Label(new Rect(r.x + 162, r.y + 102, r.width - 170, 28), "< ВЫБРАН >", _h2);
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
            Shade(w, h);
            DrawCenterText(w, h, "Подключение к " + _ip.Trim() + "...");
            if (GUI.Button(new Rect(w / 2f - 90, h / 2f + 60, 180, 48), "ОТМЕНА", _button))
                Leave();
        }

        void DrawWaiting(float w, float h)
        {
            Shade(w, h);
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
            if (TouchHud.Enabled)
                DrawTouchControls(w, h, hero);

            // stealth indicator
            if (!hero.Dead.Value && MapBuilder.BushIndex(hero.transform.position) >= 0)
            {
                var eye = Tex("eye");
                float ix = w / 2f - 110;
                if (eye != null)
                    GUI.DrawTexture(new Rect(ix, h * 0.24f, 34, 34), eye, ScaleMode.ScaleToFit);
                var prevC = GUI.color;
                GUI.color = new Color(0.5f, 1f, 0.55f, 0.7f + Mathf.PingPong(Time.time, 0.3f));
                GUI.Label(new Rect(ix + 40, h * 0.24f, 260, 34), "ВЫ СКРЫТЫ В КУСТАХ", _h2);
                GUI.color = prevC;
            }

            if (hero.SkillPoints.Value > 0 && !TouchHud.Enabled)
                GUI.Label(new Rect(w / 2f - 260, h - 196, 520, 24),
                    "Очки навыков: " + hero.SkillPoints.Value + " — жмите «+» на иконке", _h2);

            DrawTooltip(w, h);
            if (_showSkillInfo)
                DrawSkillInfo(w, h, hero);

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
            if (!TouchHud.Enabled)
                GUI.Label(new Rect(16, 52, 300, 26), "F1 — управление, Esc — меню", _small);
            else if (GUI.Button(new Rect(16, 52, 90, 34), "МЕНЮ", _button))
                _confirmLeave = !_confirmLeave;
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

            if (TouchHud.Enabled) return; // skills are on the touch buttons instead

            y += 26;
            var k = hero.HeroType;
            DrawSkill(new Rect(cx - 230, y, 84, 84), "Q", HeroData.Skill(k, 0).icon,
                hero.CdQUntil, hero.QCd, hero.QCost, hero, true, 0);
            DrawSkill(new Rect(cx - 130, y, 84, 84), "W", HeroData.Skill(k, 1).icon,
                hero.CdWUntil, hero.WCd, hero.WCost, hero, true, 1);
            DrawSkill(new Rect(cx - 30, y, 84, 84), "E", HeroData.Skill(k, 2).icon,
                hero.CdEUntil, hero.ECd, hero.ECost, hero, hero.UltReady, 2);
            DrawSkill(new Rect(cx + 70, y, 84, 84), "SPACE", "attack",
                hero.CdAttackUntil, hero.AttackCooldown, 0f, hero, true, -1);
        }

        void DrawTooltip(float w, float h)
        {
            if (string.IsNullOrEmpty(_tooltip)) return;
            var size = _h2.CalcSize(new GUIContent(_tooltip));
            float tw = Mathf.Max(size.x + 36, 260);
            float th = size.y + 28;
            float tx = Mathf.Clamp(_tooltipAnchor.x + _tooltipAnchor.width / 2f - tw / 2f,
                10, w - tw - 10);
            float ty = _tooltipAnchor.y - th - 10;
            DrawPanel(new Rect(tx, ty, tw, th));
            GUI.Label(new Rect(tx + 18, ty + 12, tw - 36, th - 24), _tooltip, _h2);
            _tooltip = null;
        }

        void DrawSkillInfo(float w, float h, Hero hero)
        {
            var r = new Rect(w / 2f - 270, h * 0.16f, 540, 420);
            DrawPanel(r);
            GUI.Label(new Rect(r.x, r.y + 10, r.width, 32), "СПОСОБНОСТИ", _h1);
            GUI.Label(new Rect(r.x, r.y + 42, r.width, 26),
                "Очки навыков: " + hero.SkillPoints.Value, _h2);
            for (int i = 0; i < 3; i++)
            {
                float sy = r.y + 76 + i * 102;
                var icon = Tex(HeroData.Skill(hero.HeroType, i).icon);
                if (icon != null)
                    GUI.DrawTexture(new Rect(r.x + 18, sy, 64, 64), icon, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(r.x + 96, sy - 4, r.width - 190, 96),
                    hero.SkillTooltip(i), _label);
                if (hero.CanUpgradeSkill(i) &&
                    GUI.Button(new Rect(r.x + r.width - 76, sy + 8, 58, 48), "+", _bigButton))
                    hero.UpgradeSkillRpc(i);
            }
            if (GUI.Button(new Rect(r.x + r.width / 2f - 80, r.y + r.height - 50, 160, 40),
                    "ЗАКРЫТЬ", _button))
                _showSkillInfo = false;
        }

        void DrawTouchControls(float w, float h, Hero hero)
        {
            if (hero.Dead.Value) return;
            var prev = GUI.color;

            // joystick
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            DrawCircle(TouchHud.JoyCenter, TouchHud.JoyRadius, new Color(0.1f, 0.08f, 0.08f, 0.55f));
            DrawCircle(TouchHud.JoyKnob, TouchHud.JoyKnobRadius, new Color(1f, 0.6f, 0.25f, 0.7f));

            // attack + skills
            GUI.color = Color.white;
            DrawTouchButton(TouchHud.AtkCenter, TouchHud.AtkRadius, "attack",
                hero.CdAttackUntil, hero.AttackCooldown, true);
            var k = hero.HeroType;
            DrawTouchButton(TouchHud.QCenter, TouchHud.SkillRadius, HeroData.Skill(k, 0).icon,
                hero.CdQUntil, hero.QCd, hero.Mana.Value >= hero.QCost);
            DrawTouchButton(TouchHud.WCenter, TouchHud.SkillRadius, HeroData.Skill(k, 1).icon,
                hero.CdWUntil, hero.WCd, hero.Mana.Value >= hero.WCost);
            DrawTouchButton(TouchHud.ECenter, TouchHud.SkillRadius, HeroData.Skill(k, 2).icon,
                hero.CdEUntil, hero.ECd, hero.UltReady && hero.Mana.Value >= hero.ECost);
            GUI.color = prev;

            // skill info / upgrade panel toggle (touch can't hover)
            string infoLabel = hero.SkillPoints.Value > 0
                ? "НАВЫКИ +" + hero.SkillPoints.Value
                : "НАВЫКИ";
            if (GUI.Button(new Rect(w - 170, h * 0.32f, 150, 44), infoLabel, _button))
                _showSkillInfo = !_showSkillInfo;
        }

        void DrawCircle(Vector2 c, float r, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0, GUI.color, 0, r);
            GUI.color = prev;
        }

        void DrawTouchButton(Vector2 c, float r, string iconName, float cdUntil, float cdTotal,
            bool ready)
        {
            var icon = Tex(iconName);
            var rect = new Rect(c.x - r, c.y - r, r * 2f, r * 2f);
            var prev = GUI.color;
            GUI.color = ready ? new Color(1f, 1f, 1f, 0.92f) : new Color(0.45f, 0.45f, 0.45f, 0.8f);
            if (icon != null)
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
            float remain = cdUntil - Time.time;
            if (remain > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0,
                    GUI.color, 0, r);
                GUI.color = Color.white;
                GUI.Label(new Rect(c.x - 40, c.y - 14, 80, 28), remain.ToString("0.0"), _h1);
            }
            GUI.color = prev;
        }

        void DrawSkill(Rect r, string key, string iconName, float cdUntil, float cdTotal,
            float manaCost, Hero hero, bool unlocked, int slot)
        {
            var icon = Tex(iconName);
            if (icon != null)
            {
                var prev2 = GUI.color;
                GUI.color = unlocked ? Color.white : new Color(0.35f, 0.35f, 0.35f);
                GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
                GUI.color = prev2;
            }
            else
                DrawPanel(r);
            GUI.Label(new Rect(r.x + 4, r.y + 2, r.width - 8, 20), key, _small);

            // hover tooltip with live numbers
            if (slot >= 0 && r.Contains(Event.current.mousePosition))
            {
                _tooltip = hero.SkillTooltip(slot);
                _tooltipAnchor = r;
            }
            // skill level + upgrade button
            if (slot >= 0)
            {
                GUI.Label(new Rect(r.x + 4, r.y + r.height - 22, 44, 20),
                    "ур." + hero.GetSkillLevel(slot), _small);
                if (hero.CanUpgradeSkill(slot))
                {
                    var plus = new Rect(r.x + r.width - 30, r.y - 6, 34, 34);
                    var prevC = GUI.color;
                    GUI.color = new Color(0.5f, 1f, 0.5f, 0.8f + Mathf.PingPong(Time.time * 2f, 0.2f));
                    if (GUI.Button(plus, "+", _button))
                        hero.UpgradeSkillRpc(slot);
                    GUI.color = prevC;
                }
            }
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
            // touch layout keeps the bottom corners free for the thumb controls
            float x = TouchHud.Enabled ? 16f : w - 270f;
            float y = TouchHud.Enabled ? 120f : h - 180f;
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
            float mw = 200f, mh = 76f;
            var r = new Rect(w - mw - 12, 12, mw, mh);
            DrawPanel(r);
            foreach (var u in UnitBase.All)
            {
                if (u == null || !u.Alive) continue;
                if (u is Hero eh && !eh.IsVisibleLocally) continue;
                float px = r.x + (u.Pos.x + GameConstants.MapHalfW) / (GameConstants.MapHalfW * 2f) * mw;
                float pz = r.y + mh - (u.Pos.z + GameConstants.MapHalfH) / (GameConstants.MapHalfH * 2f) * mh;
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
            Shade(w, h);
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
            var r = new Rect(w / 2f - 300, h / 2f - 215, 600, 430);
            DrawPanel(r);
            GUI.Label(new Rect(r.x, r.y + 12, r.width, 34), "УПРАВЛЕНИЕ", _h1);
            string[] lines =
            {
                "WASD / стрелки / ПКМ / джойстик — движение",
                "Пробел / ЛКМ / кнопка атаки — атака ближайшей цели",
                "Q — снаряд (мышь: в курсор; сенсор: в ближайшего врага)",
                "W — Ксардарас: телепорт; Белиал: похищение жизни;",
                "      Аданос: лечение + ускорение",
                "E — ульта вокруг героя (с 4 уровня)",
                "1 / 2 / 3 — купить улучшение за золото",
                "Колесо мыши — зум камеры",
                "",
                "ЛАВА: после извержения не стойте в оранжевых лужах!",
                "Кусты скрывают вас от врага, валуны непроходимы.",
                "Цель — уничтожить вражескую базу за двумя башнями!"
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
                fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _title.normal.textColor = new Color(1f, 0.85f, 0.6f);
            _h1 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _h2 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true
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
                    s.focused.background = btnHi != null ? btnHi : btn;
                    s.border = new RectOffset(14, 14, 14, 14);
                }
            }
            // explicit text colors for EVERY state — otherwise text vanishes on
            // hover/press on some platforms (notably WebGL/mobile)
            SetAllTextColors(_button, new Color(1f, 0.92f, 0.8f), Color.white);
            SetAllTextColors(_bigButton, new Color(1f, 0.92f, 0.8f), Color.white);
            SetAllTextColors(_title, _title.normal.textColor, _title.normal.textColor);
            SetAllTextColors(_h1, Color.white, Color.white);
            SetAllTextColors(_h2, new Color(0.95f, 0.85f, 0.75f), new Color(0.95f, 0.85f, 0.75f));
            SetAllTextColors(_label, Color.white, Color.white);
            SetAllTextColors(_small, new Color(0.85f, 0.76f, 0.68f), new Color(0.85f, 0.76f, 0.68f));
            SetAllTextColors(_warn, Color.white, Color.white);

            // proper game font with Cyrillic (Russo One, OFL) — the built-in font
            // renders Cyrillic poorly on WebGL/mobile, this fixes "буквы"
            var font = Resources.Load<Font>("Fonts/RussoOne");
            if (font != null)
                foreach (var s in new[] { _title, _h1, _h2, _label, _small, _warn, _button, _bigButton })
                {
                    s.font = font;
                    s.fontStyle = FontStyle.Normal; // the font is already bold/display
                }
        }

        static void SetAllTextColors(GUIStyle s, Color normal, Color highlight)
        {
            s.normal.textColor = normal;
            s.hover.textColor = highlight;
            s.active.textColor = highlight;
            s.focused.textColor = highlight;
            s.onNormal.textColor = normal;
            s.onHover.textColor = highlight;
            s.onActive.textColor = highlight;
            s.onFocused.textColor = highlight;
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
