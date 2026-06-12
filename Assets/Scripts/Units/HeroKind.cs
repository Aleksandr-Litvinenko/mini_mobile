namespace Moba
{
    public enum HeroKind : byte
    {
        Xardaras = 0, // маг огня
        Belial = 1    // тёмный маг
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
        public static string Name(HeroKind k) =>
            k == HeroKind.Belial ? "Белиал" : "Ксардарас";

        public static string Title(HeroKind k) =>
            k == HeroKind.Belial ? "тёмный маг" : "маг";

        public static string Portrait(HeroKind k) =>
            k == HeroKind.Belial ? "portrait_b" : "portrait_x";

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

        public static SkillMeta Skill(HeroKind k, int slot) =>
            k == HeroKind.Belial ? Belial[slot] : Xardaras[slot];
    }
}
