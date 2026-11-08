using System.Collections.Generic;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-07 面试 (赵金 选秀.psd + 会议): the ID-card stack with the candidate's portrait, a paper card with
    /// name, "18岁 学生 舞蹈爱好者", SSR/SR, 声乐 / 舞蹈 / 说唱 / 名气 with rank letters (the strongest gets the
    /// heart), 血型 / 生日, 风险 and 体力 bars, 薪水 per month, and the 面试 button with its 星钻 price; 线上 / 线下
    /// 面试 below. No ten-pull: one candidate is hired at a time. The board art has its dynamic text painted out;
    /// live text sits on it. Coordinates are the PSD's (4320 wide) divided by six, origin at PSD y 534.
    /// </summary>
    public sealed partial class GachaPanel
    {
        private const string InterviewArt1107 = "Art/Ui1107/";
        private const float IvScale = 1f / 6f, IvTop = 534f, IvPageH = (7100f - 534f) / 6f;

        private static Rect Iv(float x0, float y0, float x1, float y1) =>
            new Rect(x0 * IvScale, (y0 - IvTop) * IvScale, (x1 - x0) * IvScale, (y1 - y0) * IvScale);

        private void BuildInterview1107(RectTransform host)
        {
            Canvas.ForceUpdateCanvases();
            float hostW = host.rect.width > 1 ? host.rect.width : 720f;
            float hostH = host.rect.height > 1 ? host.rect.height : 1290f;
            float scale = Mathf.Min(hostW / 720f, hostH / IvPageH);
            RectTransform page = kit.NewRect("Interview1107Page", host);
            page.anchorMin = page.anchorMax = page.pivot = new Vector2(.5f, 1f);
            page.sizeDelta = new Vector2(720f, IvPageH);
            page.anchoredPosition = new Vector2(0f, -Mathf.Max(0f, (hostH - IvPageH * scale) * .5f));
            page.localScale = Vector3.one * scale;

            // The shared dark-top / light-bottom page art fills the host; PageAmbient carries it out to the screen edges.
            Sprite pageBg = Resources.Load<Sprite>(InterviewArt1107 + "page-bg-board");
            Image backdrop = kit.NewImage("InterviewBackdrop1107", host, pageBg, Color.white);
            PanelKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;
            backdrop.transform.SetAsFirstSibling();
            if (pageBg != null) ChoSiren.UI.PageAmbient.Set(gameObject, pageBg, featherEdges: backdrop.rectTransform);
            Sprite board = Resources.Load<Sprite>(InterviewArt1107 + "iv-board");
            Image boardImage = IvImage(page, "InterviewBoard1107", board, Iv(0, 700, 4320, 7100));

            List<int> candidates = InterviewCandidates();
            // The stack moves with the candidate swipe: CandidateCard holds the back cards, CandidatePortrait holds the
            // masked window (her bust) and the front card frame over it.
            RectTransform stack = kit.NewRect("CandidateCard", page);
            PanelKit.PlaceTop(stack, 0f, 0f, 720f, IvPageH);
            IvImage(stack, "InterviewCard2", Resources.Load<Sprite>(InterviewArt1107 + "iv-card-2"), Iv(0, 3515, 1665, 5734));
            IvImage(stack, "InterviewCard1", Resources.Load<Sprite>(InterviewArt1107 + "iv-card-1"), Iv(0, 2866, 1658, 5085));
            RectTransform front = kit.NewRect("CandidatePortrait", page);
            PanelKit.PlaceTop(front, 0f, 0f, 720f, IvPageH);
            RectTransform window = kit.NewRect("CandidateWindow", front);
            Rect windowRect = Iv(168, 2325, 1681, 4163);
            PanelKit.PlaceTop(window, windowRect.x, windowRect.y, windowRect.width, windowRect.height);
            window.gameObject.AddComponent<RectMask2D>();
            IvImage(front, "InterviewCard0", Resources.Load<Sprite>(InterviewArt1107 + "iv-card-0"), Iv(0, 1700, 1947, 4593));
            IvImage(page, "InterviewPig", Resources.Load<Sprite>(InterviewArt1107 + "iv-pig"), Iv(1214, 534, 2302, 1486));

            BuildInterviewPools1107(page);
            // Daily refresh line (每日 18:00) right of the channel tabs.
            AuditionRefresh1101(page);
            var refreshRow = page.Find("InterviewRefreshRow") as RectTransform;
            if (refreshRow != null) { Rect r = Iv(3300, 6420, 4300, 6680); PanelKit.PlaceTop(refreshRow, r.x, r.y, r.width, r.height); }
            var help = page.Find("AuditionHelp") as RectTransform;
            if (help != null) { Rect h = Iv(4020, 1880, 4260, 2120); PanelKit.PlaceTop(help, h.x, h.y, 40f, 40f); }
            if (candidates.Count == 0) { BuildInterviewComplete(page, 300f, 300f); return; }
            interviewCandidateIndex = Mathf.Clamp(interviewCandidateIndex, 0, candidates.Count - 1);
            int memberIndex = candidates[interviewCandidateIndex];
            MemberDefinition member = GameModel.Members[memberIndex];
            MemberProfile1107 profile = MemberProfile1107.Of(member.Id);

            Image portrait = kit.NewImage("CandidatePortraitArt", window, CandidateSprite(member), Color.white);
            PanelKit.Stretch(portrait.rectTransform);
            portrait.raycastTarget = false;
            if (portrait.sprite != null) PanelKit.FrameBustPortrait(portrait, window, member.Id);

            Text name = IvText(page, "CandidateName", member.Name, 50, Color.white, Iv(1980, 1440, 2680, 1800), TextAnchor.MiddleCenter, FontStyle.Bold);
            name.gameObject.AddComponent<Outline>().effectColor = new Color32(60, 30, 110, 255);
            Text line = IvText(page, "CandidateProfile", profile.Line, 22, Color.white, Iv(1900, 1820, 3080, 2000), TextAnchor.MiddleCenter, FontStyle.Italic);
            line.rectTransform.localEulerAngles = new Vector3(0f, 0f, 4f);
            bool ssr = member.Rarity == "SSR";
            Text rarity = IvText(page, "CandidateRarity", ssr ? "SSR" : member.Rarity, 76, ssr ? new Color32(255, 132, 206, 255) : new Color32(150, 210, 255, 255),
                Iv(3200, 1100, 4160, 1600), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Outline rarityRim = rarity.gameObject.AddComponent<Outline>();
            rarityRim.effectColor = Color.white; rarityRim.effectDistance = new Vector2(2.5f, -2.5f);

            // 声乐 / 舞蹈 / 说唱 / 名气 (vocal, presence, rhythm, resonance from the stage stats).
            CandidateStats(member, memberIndex, interviewPoolIndex, out int vocal, out int rhythm, out int presence, out int resonance, out _);
            int[] values = { vocal, presence, rhythm, resonance };
            // Names keep the stat keys the rest of the game uses (presence = 舞蹈, rhythm = 说唱, resonance = 名气).
            string[] keys = { "Vocal", "Presence", "Rhythm", "Resonance" };
            Color[] bars = { new Color32(56, 152, 176, 255), new Color32(140, 98, 222, 255), new Color32(118, 86, 214, 255), new Color32(232, 162, 58, 255) };
            float[] rowY = { 2140f, 2540f, 2940f, 3340f };
            int strongest = 0;
            for (int i = 1; i < values.Length; i++) if (values[i] > values[strongest]) strongest = i;
            for (int i = 0; i < values.Length; i++)
            {
                float y = rowY[i];
                Text rank = IvText(page, "StatRank-" + keys[i], MemberProfile1107.Rank(values[i]), 36, new Color32(70, 66, 80, 255),
                    Iv(2660, y + 30, 2830, y + 250), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
                rank.gameObject.AddComponent<Outline>().effectColor = new Color32(255, 255, 255, 200);
                Text value = IvText(page, "StatValue-" + keys[i], values[i].ToString(), 28, Color.white,
                    Iv(2900, y + 20, 3250, y + 160), TextAnchor.MiddleLeft, FontStyle.Bold);
                value.gameObject.AddComponent<Outline>().effectColor = new Color32(40, 30, 60, 255);
                Image fill = kit.NewBar("StatBar-" + keys[i], page, (2860f) * IvScale, (y + 170 - IvTop) * IvScale, 700f * IvScale, 9f,
                    new Color32(255, 255, 255, 110), bars[i], 0);
                fill.fillAmount = Mathf.Clamp01(values[i] / 100f);
                if (i == strongest)
                    IvImage(page, "StrongestStat", Resources.Load<Sprite>(InterviewArt1107 + "iv-heart"), Iv(3720, y - 80, 3960, y + 140));
            }

            Color ink = new Color32(40, 30, 64, 255);
            IvText(page, "CandidateBlood", profile.Blood, 22, ink, Iv(2860, 3840, 3600, 3980), TextAnchor.MiddleLeft, FontStyle.Bold);
            IvText(page, "CandidateBirthday", profile.Birthday, 22, ink, Iv(2860, 4040, 3600, 4180), TextAnchor.MiddleLeft, FontStyle.Bold);
            // 会议: 幸福感 → 风险.
            IvText(page, "RiskLabel", "风险", 20, ink, Iv(2200, 4360, 2500, 4500), TextAnchor.MiddleLeft, FontStyle.Normal);
            int risk = model.InterviewRisk(interviewPoolIndex, memberIndex);
            IvText(page, "CandidateRisk", risk.ToString(), 24, new Color32(110, 52, 230, 255), Iv(2760, 4300, 2920, 4448), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            Image riskFill = kit.NewBar("CandidateRiskBar", page, 2990f * IvScale, (4340f - IvTop) * IvScale, 840f * IvScale, 12f,
                new Color32(232, 210, 220, 255), new Color32(222, 62, 112, 255), 0);
            riskFill.fillAmount = Mathf.Clamp01(risk / 100f);
            const int staminaMax = 120;
            IvText(page, "CandidateStamina", staminaMax.ToString(), 24, new Color32(240, 92, 140, 255), Iv(2760, 4540, 2930, 4690), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            Image staminaFill = kit.NewBar("CandidateStaminaBar", page, 3020f * IvScale, (4585f - IvTop) * IvScale, 980f * IvScale, 12f,
                new Color32(232, 210, 200, 255), new Color32(240, 150, 40, 255), 0);
            staminaFill.fillAmount = 1f;
            IvText(page, "CandidateSalary", $"{model.EstimatedMonthlySalary(memberIndex):N0}<size=18> /月</size>", 32, ink,
                Iv(2900, 4920, 3700, 5120), TextAnchor.MiddleRight, FontStyle.BoldAndItalic);

            int cost = InterviewCost(memberIndex);
            RectTransform actions = kit.NewRect("InterviewActions", page);
            PanelKit.PlaceTop(actions, 0f, 0f, 720f, IvPageH);
            IvText(actions, "SigningPrice", cost.ToString("N0"), 34, new Color32(70, 230, 240, 255), Iv(3480, 5440, 4000, 5700), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            Rect signRect = Iv(1700, 5300, 4270, 6190);
            InterviewBoardHotspot("SignCandidate", actions, signRect.x, signRect.y, signRect.width, signRect.height,
                () => SignInterviewCandidate(memberIndex, cost));

            // ‹ 1/10 › under the card stack (the shared switch arrows), plus a swipe on the stack.
            Rect nav = Iv(40, 5880, 1560, 6260);
            Text counter = kit.NewPlacedText(page, $"{interviewCandidateIndex + 1} / {candidates.Count}", 26, Color.white,
                nav.x + 70f, nav.y, nav.width - 140f, nav.height, TextAnchor.MiddleCenter, FontStyle.Bold);
            counter.name = "CandidateCounter";
            counter.gameObject.AddComponent<Outline>().effectColor = new Color32(20, 10, 40, 255);
            Button previous = SwitchArrow1107.Create(page, "PreviousCandidate", -1, new Rect(nav.x, nav.y, 70f, nav.height), () => MoveInterviewCandidate(-1));
            Button next = SwitchArrow1107.Create(page, "NextCandidate", 1, new Rect(nav.xMax - 70f, nav.y, 70f, nav.height), () => MoveInterviewCandidate(1));
            previous.interactable = next.interactable = candidates.Count > 1;
            AttachCandidateSwipe1102(page, Iv(0, 1700, 1700, 5800));
        }

        /// <summary>线上面试 / 线下面试 are baked into the board: hits over each, the chosen one gets a teal underline.</summary>
        private void BuildInterviewPools1107(RectTransform page)
        {
            Rect[] pools = { Iv(890, 6310, 2260, 6770), Iv(2110, 6340, 3260, 6750) };
            for (int i = 0; i < pools.Length; i++)
            {
                int pool = i;
                GameObject hit = InterviewBoardHotspot("InterviewPool-" + i, page, pools[i].x, pools[i].y, pools[i].width, pools[i].height,
                    () => SelectInterviewPool(pool));
                if (i != interviewPoolIndex) continue;
                Image glow = ReferencePunkFx.Deco(page, "SelectedChannelGlow", ReferencePunkFx.Glow(), new Color(.11f, .95f, .93f, .4f),
                    pools[i].x, pools[i].yMax - 14f, pools[i].width, 26f);
                Image mark = kit.NewImage("SelectedInterviewChannel", page, null, Brand1025.Teal);
                PanelKit.PlaceTop(mark.rectTransform, pools[i].x + 24f, pools[i].yMax - 4f, pools[i].width - 48f, 6f);
                mark.raycastTarget = false;
                hit.transform.SetAsLastSibling();
            }
        }

        private Image IvImage(Transform parent, string name, Sprite sprite, Rect rect)
        {
            Image image = kit.NewImage(name, parent, sprite, Color.white);
            PanelKit.PlaceTop(image.rectTransform, rect.x, rect.y, rect.width, rect.height);
            image.raycastTarget = false;
            return image;
        }

        private Text IvText(Transform parent, string name, string value, int size, Color color, Rect rect, TextAnchor anchor, FontStyle style)
        {
            Text text = kit.NewPlacedText(parent, value, size, color, rect.x, rect.y, rect.width, rect.height, anchor, style);
            text.name = name;
            text.supportRichText = true;
            text.raycastTarget = false;
            // The PSD zones are tight; never let Unity hide a line that is a few pixels too tall.
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
