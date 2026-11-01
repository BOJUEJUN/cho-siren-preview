using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-01 (visual-b, 赵金 1101c): the audition page says less and shows more — channel
    /// subtitles become a channel icon + price pips, the refresh line becomes a clock + time, the
    /// candidate identity is race badge + role chip + battle-position chip, and salary / risk /
    /// recommendation are icon rows. Long explanations sit behind the page's 「?」.
    /// </summary>
    public sealed partial class GachaPanel
    {
        private static readonly string[] AuditionHelpLines =
        {
            "线上面试：看视频选人，报价低、风险高",
            "线下面试：当面试镜，报价高、更稳",
            "◆ 越多报价越高；每日 18:00 刷新名单",
            "★ 为最强项；金币为每 7 天一期的预估月薪",
            "⚠ 风险越高，经营事件越容易出状况",
        };

        private Transform AuditionPoolSubtitle1101(RectTransform page, int pool, float x, float y)
        {
            RectTransform row = VisualB1101.Rect("PoolSubtitle-" + pool, page);
            PanelKit.PlaceTop(row, x, y, 330f, 40f);
            Color tint = pool == 0 ? new Color32(160, 230, 255, 255) : new Color32(222, 186, 255, 255);
            int pips = pool == 0 ? 1 : 3;
            float width = 40f + 10f + pips * 26f;
            float start = (330f - width) * .5f;
            VisualB1101.NeonIcon(row, "Channel", pool == 0 ? "video" : "spotlight", tint, start, 0f, 40f, false);
            Sprite gem = PanelKit.CurrencyIcon("diamond");
            for (int i = 0; i < pips; i++)
            {
                Image pip = VisualB1101.Img("Pip-" + i, row, gem, Color.white);
                pip.preserveAspect = true;
                PanelKit.PlaceTop(pip.rectTransform, start + 50f + i * 26f, 8f, 26f, 26f);
            }
            return row;
        }

        private void AuditionRefresh1101(RectTransform page)
        {
            RectTransform row = VisualB1101.Rect("InterviewRefreshRow", page);
            PanelKit.PlaceTop(row, 300f, 312f, 260f, 42f);
            VisualB1101.NeonIcon(row, "Icon", "refresh", PanelKit.Muted, 0f, 6f, 30f, false);
            Text refresh = VisualB1101.Label(row, "InterviewRefresh", "每日 18:00", 20, PanelKit.Muted, 38f, 0f, 200f, 42f);
            refresh.name = "InterviewRefresh";
            VisualB1101.HelpButton(page, "AuditionHelp", 772f, 314f, 40f, "面试说明", AuditionHelpLines);
        }

        private void AuditionIdentity1101(RectTransform card, MemberDefinition member, int memberIndex, string career, float y)
        {
            string race = InterviewShortRace(member, memberIndex);
            Color raceColor = VisualB1101.RaceColor(race);
            float x = 22f;
            Sprite raceIcon = VisualB1101.RaceIcon(race);
            Image badge = VisualB1101.Img("CandidateRace", card, raceIcon, Color.white);
            badge.preserveAspect = true;
            PanelKit.PlaceTop(badge.rectTransform, x - 4f, y - 10f, 56f, 56f);
            if (raceIcon == null) badge.enabled = false; else x += 56f;
            RectTransform chip = VisualB1101.Chip(card, "CandidateCareerChip", null, career, VisualB1101.Teal, x, y, 38f,
                out Text careerText, new Color32(20, 64, 72, 245), 20);
            careerText.name = "CandidateCareer";
            x += chip.sizeDelta.x + 8f;
            string position = InterviewPosition(career, memberIndex).Replace(" / ", "").Replace("/", "");
            VisualB1101.Chip(card, "CandidatePositionChip", VisualB1101.PositionIcon(race), position, raceColor,
                x, y, 38f, out Text positionText, null, 18).name = "CandidatePositionChip";
            positionText.name = "CandidatePosition";
        }

        private void AuditionManagement1101(RectTransform card, MemberDefinition member, int memberIndex, string career)
        {
            ReferencePunkFx.Beam(card, "ManagementBeam", new Vector2(122, 586), new Vector2(345, 586),
                1.2f, new Color32(153, 133, 204, 96));
            // Salary per 7-day period: coin + number.
            VisualB1101.NeonIcon(card, "SalaryIcon", "coin", PanelKit.Gold, 132, 600, 30, false);
            Text salary = VisualB1101.Label(card, "CandidateSalary", model.EstimatedMonthlySalary(memberIndex).ToString("N0"),
                24, PanelKit.Gold, 170, 596, 120, 38);
            salary.rectTransform.sizeDelta = new Vector2(salary.preferredWidth + 2f, 38f);
            VisualB1101.Label(card, "SalaryUnit", "/期", 16, PanelKit.Muted, 174 + salary.preferredWidth, 604, 60, 30);

            // Risk: warning glyph in the tier colour + bar + value.
            int risk = model.InterviewRisk(interviewPoolIndex, memberIndex);
            string tier = GameModel.InterviewRiskLabel(risk);
            Color riskColor = tier == "高" ? new Color32(255, 108, 128, 255)
                : tier == "中" ? new Color32(255, 196, 92, 255) : new Color32(104, 226, 160, 255);
            VisualB1101.NeonIcon(card, "RiskIcon", "warn", riskColor, 132, 646, 28, false);
            Image track = kit.NewBar("CandidateRiskBar", card, 170, 658, 150, 8, new Color32(83, 78, 126, 86), riskColor, 6);
            track.fillAmount = risk / 100f;
            VisualB1101.Label(card, "CandidateRisk", risk.ToString(), 22, riskColor, 326, 642, 60, 36);

            // Recommendation: the team lacks this role → gold star chip; otherwise a quiet fit chip.
            bool needed = TeamNeedsCareer(career);
            VisualB1101.Chip(card, "CandidateRecommendation", needed ? "star" : "team", needed ? "缺" + career : "适配",
                needed ? PanelKit.Gold : PanelKit.Muted, 132, 694, 36, out _, null, 18);
        }
    }
}
