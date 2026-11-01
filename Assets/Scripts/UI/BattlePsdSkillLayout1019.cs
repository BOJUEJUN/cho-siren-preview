using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>Native semantic rows. Compound symbols retain every authored part and offset.</summary>
    public static class BattlePsdSkillLayout1019
    {
        public struct Row
        {
            public readonly string Key;
            public readonly Rect Native;
            public Row(string key, float x, float y, float width, float height)
            { Key = key; Native = new Rect(x, y, width, height); }
        }
        private static readonly Row[][] Rows =
        {
            new[]
            {
                new Row("icon-heart-lock", 632, 3308, 181, 241),
                new Row("icon-demon-mask", 615, 3520, 245, 233),
                new Row("symbol-charm-passive", 651, 3716, 216, 238),
            },
            new[]
            {
                new Row("symbol-elf-ultimate", 1351, 3313, 184, 195),
                new Row("icon-gold-shield", 1344, 3555, 182, 198),
                new Row("symbol-elf-passive", 1249, 3734, 416, 218),
            },
            new[]
            {
                new Row("icon-star-burst", 1952, 3258, 324, 324),
                new Row("icon-purple-drop", 2050, 3569, 195, 192),
                new Row("icon-virus-dice", 2030, 3726, 239, 302),
            },
            new[]
            {
                new Row("icon-cyan-shield", 2657, 3318, 182, 171),
                new Row("icon-water-heal", 2644, 3549, 169, 168),
                new Row("icon-wave-dice", 2635, 3752, 221, 221),
            },
        };

        private static int SourceLane(CombatRace race) => race == CombatRace.BloodElf ? 1
            : race == CombatRace.Demon ? 2 : race == CombatRace.Mermaid ? 3 : 0;

        public static Row For(CombatRace race, int row) => Rows[SourceLane(race)][Mathf.Clamp(row, 0, 2)];

        /// <summary>
        /// 2026-10-26 (赵金: "图标全部都放在光碟的右上方，统一放在一个方向，这样容易辨认"): every cue — ultimate, small skill or
        /// dice passive — appears at the same spot over the record's top-right, its authored aspect kept.
        /// Card-local PSD units (the card's origin is the record's top).
        /// </summary>
        public const float CueCenterX = 600f, CueCenterY = 170f, CueSize = 260f;

        public static Rect CueBounds(CombatRace race, int row)
        {
            Rect native = Rows[SourceLane(race)][Mathf.Clamp(row, 0, 2)].Native;
            float fit = Mathf.Min(CueSize / native.width, CueSize / native.height);
            Vector2 fitted = native.size * fit;
            return new Rect(CueCenterX - fitted.x * .5f, CueCenterY - fitted.y * .5f, fitted.x, fitted.y);
        }

        /// <summary>2026-10-19 feedback: three transient skill kinds in one row above their own card.</summary>
        public static Rect LocalBounds(CombatRace race, int row, int slot)
        {
            row = Mathf.Clamp(row, 0, 2);
            Rect native = Rows[SourceLane(race)][row].Native;
            const float size = 200, spacing = 210, left = 44, top = -220;
            float fit = Mathf.Min(size / native.width, size / native.height);
            Vector2 fitted = native.size * fit;
            // Keep compound authored symbols and their aspect ratio. The caller retains
            // its real cast/passive lifetime; this function never makes an icon permanent.
            return new Rect(left + row * spacing + (size - fitted.x) * .5f,
                top + (size - fitted.y) * .5f, fitted.x, fitted.y);
        }
    }
}
