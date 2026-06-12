namespace Moba
{
    public enum HeroKind : byte
    {
        Xardaras = 0, // маг огня
        Belial = 1,   // тёмный маг
        Adanos = 2    // маг воды
    }

    public struct SkillMeta
    {
        public string name;
        public string icon;
        public string desc;

        public SkillMeta(string name, string icon, string desc)
        {
            this.name = name;
            this.icon = icon;
            this.desc = desc;
        }
    }

    public static class HeroData
    {
        public const int Count = 3;

        public static string Name(HeroKind k) =>
            k == HeroKind.Belial ? "Белиал" : k == HeroKind.Adanos ? "Аданос" : "Ксардарас";

        public static string Title(HeroKind k) =>
            k == HeroKind.Belial ? "тёмный маг" : k == HeroKind.Adanos ? "маг воды" : "маг огня";

        public static string Passive(HeroKind k) =>
            k == HeroKind.Belial ? "вампиризм 12% со всего урона"
            : k == HeroKind.Adanos ? "ускоренное восстановление маны"
            : "самая дальняя атака";

        public static string Portrait(HeroKind k) =>
            k == HeroKind.Belial ? "portrait_b" : k == HeroKind.Adanos ? "portrait_a" : "portrait_x";

        static readonly SkillMeta[] Xardaras =
        {
            new SkillMeta("Огненный снаряд", "skill_x_q", "быстрый снаряд в сторону курсора"),
            new SkillMeta("Телепорт", "skill_x_w", "мгновенный перенос к курсору"),
            new SkillMeta("Метеоритный шторм", "skill_x_e", "мощный урон и замедление вокруг (с 4 ур.)")
        };

        static readonly SkillMeta[] Belial =
        {
            new SkillMeta("Сгусток тьмы", "skill_b_q", "тяжёлый снаряд, замедляет цель"),
            new SkillMeta("Похищение жизни", "skill_b_w", "крадёт здоровье ближайшего врага"),
            new SkillMeta("Жатва душ", "skill_b_e", "урон вокруг, лечит за каждого врага (с 4 ур.)")
        };

        static readonly SkillMeta[] Adanos =
        {
            new SkillMeta("Ледяная стрела", "skill_a_q", "снаряд, замедляющий цель"),
            new SkillMeta("Живительный поток", "skill_a_w", "лечит и ускоряет на 2 секунды"),
            new SkillMeta("Цунами", "skill_a_e", "урон и сильное замедление вокруг (с 4 ур.)")
        };

        public static SkillMeta Skill(HeroKind k, int slot) =>
            k == HeroKind.Belial ? Belial[slot]
            : k == HeroKind.Adanos ? Adanos[slot]
            : Xardaras[slot];
    }
}
