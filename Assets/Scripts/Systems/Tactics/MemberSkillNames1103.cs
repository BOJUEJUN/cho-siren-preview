using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// 2026-11-03 (用户: 角色的技能错误的叫成一个名字): the realtime mechanics stay per race (four small/big pairs),
    /// but every member shows her own two skill names so sixteen girls no longer share "狐影瞬击". The original four
    /// keep the race names their voice lines and videos were made for. Unknown ids fall back to the race name.
    /// </summary>
    public static class MemberSkillNames1103
    {
        private static readonly Dictionary<string, (string Small, string Big)> Names =
            new Dictionary<string, (string Small, string Big)>(StringComparer.Ordinal)
        {
            ["yaoguang"] = ("战斧回旋", "瑶光倾城"),
            ["hupo"] = ("毒弹炮击", "琥珀舰歌"),
            ["xianyue"] = ("莲露轻愈", "弦月水幕"),
            ["chuxue"] = ("金弓穿心", "初雪终曲"),
            ["chengxia"] = ("碧枪点刺", "澄夏流光"),
            ["hero-0002"] = ("霜刃侵蚀", "霜华寒狱"),
            ["hero-0003"] = ("萤光愈泉", "流萤结界"),
            ["hero-0005"] = ("魔爪撕裂", "星澜深渊"),
            ["hero-0007"] = ("汐月轻挑", "月下迷梦"),
            ["hero-0008"] = ("赤刃毒吻", "绮梦堕夜"),
            ["hero-0010"] = ("云雾甘霖", "云歌冰壁"),
            ["hero-0011"] = ("凌音裂斧", "绯红断音"),
            ["hero-0012"] = ("兔影连弦", "紫苑心跳"),
            ["hero-0024"] = ("毒酒一杯", "夕颜残梦"),
            ["hero-0031"] = ("圣水祷言", "青璃庇佑"),
            ["hero-0032"] = ("黑兔突袭", "洛羽魅舞"),
            ["hero-0033"] = ("星光一瞥", "眠星低语"),
            ["hero-0034"] = ("甜毒冰沙", "雪见糖衣"),
            ["hero-0035"] = ("霜羽抚慰", "绫霜羽幕"),
            ["hero-0037"] = ("诗刃一线", "诗音落樱"),
            ["hero-0038"] = ("团扇轻拂", "露华浓香"),
            ["hero-0200"] = ("赤音蚀骨", "天音魔咒"),
            ["hero-0201"] = ("月泉回响", "幻月镜界"),
            ["hero-0202"] = ("巨刃贯穿", "瑾歌谢幕"),
            ["hero-0203"] = ("铁拳重拍", "澜心共振"),
            ["hero-0205"] = ("毒爪横扫", "晴岚瘴风"),
            ["hero-0207"] = ("千夜祈愈", "圣潮护壁"),
            ["hero-0208"] = ("血羽穿杨", "绯月猎杀"),
            ["hero-0209"] = ("琉璃圣刃", "琉光迷途"),
            ["hero-0604"] = ("酒杯碎响", "雾凛夜宴"),
            ["hero-0605"] = ("暖茶治愈", "若雪茶会"),
            ["hero-0607"] = ("齿轮钻刺", "春弦机神"),
            ["hero-0608"] = ("狙心一击", "紫苏醉红"),
            ["hero-0610"] = ("魔杖蚀光", "晨曦暗咒"),
            ["hero-0611"] = ("星露回春", "星遥光盾"),
            ["hero-0613"] = ("狐铃轻响", "月铃千尾"),
            ["hero-0614"] = ("夜弦穿林", "夜澜迷踪"),
            ["hero-0630"] = ("毒弹连发", "霁月硝烟"),
            ["hero-0633"] = ("花雨润心", "花音蔚蓝"),
            ["hero-0634"] = ("龙息蚀珠", "蓝羽毒雾"),
            ["hero-0651"] = ("虎啸斩击", "晴歌狂澜"),
            ["hero-0664"] = ("浪花清凉", "霜音伞幕"),
            ["hero-0668"] = ("花束祝福", "星织纱帘"),
            ["hero-0670"] = ("夕刃血樱", "夕璃断弦"),
            ["hero-0671"] = ("羽翼突刺", "莲歌圣颂"),
            ["hero-0672"] = ("铃鼓毒舞", "羽澄蛇舞"),
            ["hero-0673"] = ("疾影踢击", "紫月蛊惑"),
            ["hero-0674"] = ("圣杯血刺", "晓星审判"),
            ["hero-0675"] = ("极速漂移", "云裳轰鸣"),
            ["hero-0676"] = ("毒剑突刺", "沧歌终幕"),
            ["hero-1006"] = ("黑羽刺击", "雨音堕羽"),
            ["hero-1008"] = ("金币弹射", "玲珑心巧"),
            ["hero-1011"] = ("蜂机点射", "初晴空袭"),
            ["hero-1013"] = ("墨刃浸毒", "墨染夜色"),
            ["hero-1014"] = ("清泉低吟", "清歌暗潮"),
            ["hero-1016"] = ("荆棘血鞭", "梦璃终夜"),
            ["hero-1034"] = ("轻步短刃", "星语心愿"),
            ["hero-1035"] = ("毒斧劈砍", "月华蚀野"),
            ["hero-1036"] = ("雪融愈歌", "雪歌潮汐"),
            ["hero-1037"] = ("书页利刃", "灵音裁决"),
            // 2026-11-11 赵金: the three new members (kits in Chapter3Battle1111).
            ["huansha"] = ("虚音幻幕", "千纱迷声"),
            ["huanhuoling"] = ("媚音破防", "万籁迷阵"),
            ["linyin"] = ("裂甲音刃", "破界绝响"),
        };

        public static string Name(string memberId, CombatRace race, bool big)
        {
            if (memberId != null && Names.TryGetValue(memberId, out var pair)) return big ? pair.Big : pair.Small;
            return BattleSimulator.ActiveSkillName(race, big);
        }

        /// <summary>Which member and slot a member-specific name belongs to (icons stay keyed by race skill).</summary>
        public static bool TryFind(string skillName, out string memberId, out bool big)
        {
            foreach (var entry in Names)
            {
                if (entry.Value.Small != skillName && entry.Value.Big != skillName) continue;
                memberId = entry.Key; big = entry.Value.Big == skillName;
                return true;
            }
            memberId = null; big = false;
            return false;
        }
    }
}
