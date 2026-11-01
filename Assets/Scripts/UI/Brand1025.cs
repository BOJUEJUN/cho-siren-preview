using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// Brand colour (2026-10-25, 赵金: "把红色…换成品牌色 蓝绿色 不要用红色"; 少爷: "UI特效颜色是这个蓝绿色"): the aqua
    /// glow around the lobby idol (averaged #1CF2EC) replaces the P5 red on bands, plates, ribbons, buttons, badges and
    /// UI effects. (0.8.4–0.9.0 used the lime #2AFF66 behind 「开始演出」 by mistake.) Text on a teal plate is ink — white
    /// disappears on it. Danger feedback (hurt numbers, low HP, the hit edge flash) and race colours stay red on purpose.
    /// </summary>
    public static class Brand1025
    {
        public static readonly Color32 Teal = new Color32(28, 242, 236, 255);
        /// <summary>For halftones, fills and shadows that sit under bright teal or white.</summary>
        public static readonly Color32 TealDeep = new Color32(0, 160, 170, 255);
        public static readonly Color32 Ink = new Color32(14, 8, 20, 255);
        /// <summary>
        /// Former notification-dot red (2026-10-28). Since 2026-10-31 (赵金) every notice is the torn-paper teal
        /// <see cref="ChoSiren.UI.NoticeSticker1031"/>; this colour is no longer used for notices.
        /// </summary>
        public static readonly Color32 NoticeRed = new Color32(255, 46, 92, 255);

        public static Color32 TealA(byte alpha) => new Color32(28, 242, 236, alpha);
    }
}
