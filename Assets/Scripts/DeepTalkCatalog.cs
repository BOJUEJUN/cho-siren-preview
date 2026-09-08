using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// 深入交流（v0.3.4）羁绊档专属文案池。通用台词 + 成员姓名插值（{name}），
    /// 不按种族/职业分池；本版从简，后续可扩展专属分支。
    /// </summary>
    public static class DeepTalkCatalog
    {
        private static readonly string[] Lines =
        {
            "{name}把排练室的灯调暗了一格：“今天的舞台……你也在台下看着我，对吧？”",
            "“其实我一直想问，”{name}拨了拨发梢，“你会一直留在这里，听我唱下去吗？”",
            "{name}递来一杯温热的茶：“累了就坐一会儿。舞台不会跑，我也不会。”",
            "“第一次拿到应援棒的时候，”{name}笑弯了眼睛，“我就想，一定要把它挥给懂我的人看。”",
            "“你注意到了吗？副歌最后一句，我是看着你的方向唱完的。”——{name}的声音很轻。",
            "{name}翻出练习时的旧笔记：“这些跌倒的地方，因为有人见证，才变成了勋章。”",
            "“如果哪天嗓子哑了，”{name}顿了顿，又扬起笑，“至少还有你，愿意听我说话。”",
            "“下一场巡演的名单，”{name}眨眨眼，“我把你写进了‘最重要的观众’那一栏。”",
            "“谢谢你陪我练到今天。”{name}认真地说，“羁绊这种东西，是我先动的心哦。”",
            "{name}靠在钢琴边哼了一段未发表的新歌：“这段旋律，暂时只唱给你一个人听。”",
        };

        /// <summary>Replaces the {name} placeholder; every line must contain it.</summary>
        public static string RenderLine(string template, MemberDefinition member) =>
            template.Replace("{name}", member == null ? "她" : member.Name);

        /// <summary>Picks a random 羁绊-tier dialogue line for this member.</summary>
        public static string PickLine(MemberDefinition member, DateTime now)
        {
            int index = Mathf.Abs(UnityEngine.Random.Range(0, Lines.Length));
            return RenderLine(Lines[index], member);
        }

        /// <summary>Test/inspection access: every template must interpolate the member name.</summary>
        public static IReadOnlyList<string> Templates => Array.AsReadOnly(Lines);
    }
}
