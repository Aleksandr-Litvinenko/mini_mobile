using UnityEngine;

namespace Moba
{
    /// Virtual joystick + skill buttons for touch screens (and desktop testing).
    /// Input is collected in UpdateInput (multitouch via Input.touches, mouse as
    /// fallback); HudController draws the controls in OnGUI.
    public static class TouchHud
    {
        public static bool Enabled;

        public static Vector2 Move;     // -1..1, world XZ
        public static bool AttackHeld;

        static bool _q, _w, _e;
        public static bool ConsumeQ() { var v = _q; _q = false; return v; }
        public static bool ConsumeW() { var v = _w; _w = false; return v; }
        public static bool ConsumeE() { var v = _e; _e = false; return v; }

        // layout in virtual (GUI) coordinates, recomputed each frame
        static Vector2 _joyCenter, _atkCenter, _qCenter, _wCenter, _eCenter;
        const float JoyR = 100f, JoyKnobR = 42f, AtkR = 64f, SkillR = 46f;

        static int _joyFinger = -1;
        static Vector2 _joyKnob;

        public static Vector2 JoyCenter => _joyCenter;
        public static Vector2 JoyKnob => _joyKnob;
        public static Vector2 AtkCenter => _atkCenter;
        public static Vector2 QCenter => _qCenter;
        public static Vector2 WCenter => _wCenter;
        public static Vector2 ECenter => _eCenter;
        public const float JoyRadius = JoyR;
        public const float JoyKnobRadius = JoyKnobR;
        public const float AtkRadius = AtkR;
        public const float SkillRadius = SkillR;

        public static void UpdateInput(float scale, float vw, float vh, bool inGame)
        {
            Move = Vector2.zero;
            AttackHeld = false;
            if (!Enabled || !inGame)
            {
                _joyFinger = -1;
                _joyKnob = _joyCenter;
                return;
            }

            _joyCenter = new Vector2(190f, vh - 190f);
            _atkCenter = new Vector2(vw - 130f, vh - 130f);
            _qCenter = new Vector2(vw - 286f, vh - 104f);
            _wCenter = new Vector2(vw - 258f, vh - 248f);
            _eCenter = new Vector2(vw - 110f, vh - 292f);
            _joyKnob = _joyCenter;

            if (Input.touchCount > 0)
            {
                bool joySeen = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    var v = ToVirtual(t.position, scale, vh);
                    if (t.phase == TouchPhase.Began)
                        HandlePress(v, t.fingerId);
                    if (t.fingerId == _joyFinger &&
                        (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary ||
                         t.phase == TouchPhase.Began))
                    {
                        ApplyJoystick(v);
                        joySeen = true;
                    }
                    if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled &&
                        Inside(v, _atkCenter, AtkR * 1.15f))
                        AttackHeld = true;
                    if ((t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) &&
                        t.fingerId == _joyFinger)
                        _joyFinger = -1;
                }
                if (!joySeen && _joyFinger == -1)
                    Move = Vector2.zero;
            }
            else
            {
                // mouse fallback for desktop testing of the mobile HUD
                var v = ToVirtual(Input.mousePosition, scale, vh);
                if (Input.GetMouseButtonDown(0))
                    HandlePress(v, 999);
                if (Input.GetMouseButton(0))
                {
                    if (_joyFinger == 999) ApplyJoystick(v);
                    if (Inside(v, _atkCenter, AtkR * 1.15f)) AttackHeld = true;
                }
                else
                    _joyFinger = -1;
            }
        }

        static void HandlePress(Vector2 v, int fingerId)
        {
            if (Inside(v, _joyCenter, JoyR * 1.5f) && _joyFinger == -1)
                _joyFinger = fingerId;
            else if (Inside(v, _qCenter, SkillR * 1.2f)) _q = true;
            else if (Inside(v, _wCenter, SkillR * 1.2f)) _w = true;
            else if (Inside(v, _eCenter, SkillR * 1.2f)) _e = true;
        }

        static void ApplyJoystick(Vector2 v)
        {
            var d = v - _joyCenter;
            if (d.magnitude > JoyR) d = d.normalized * JoyR;
            _joyKnob = _joyCenter + d;
            // virtual Y grows downwards; world Z grows "up the screen"
            Move = new Vector2(d.x / JoyR, -d.y / JoyR);
        }

        static Vector2 ToVirtual(Vector2 screenPos, float scale, float vh)
        {
            return new Vector2(screenPos.x / scale, vh - screenPos.y / scale);
        }

        static bool Inside(Vector2 p, Vector2 center, float r)
        {
            return (p - center).sqrMagnitude <= r * r;
        }
    }
}
