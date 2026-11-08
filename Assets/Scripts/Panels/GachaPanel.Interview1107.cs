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
            // 2026-11-08: the front badge is its own rect, pivoted on the lanyard clip so it can swing. The photo sits
            // in a rotated rect behind the badge's tilted window and is clipped by the window's own shape
            // (iv-card-0-window, cut from the card art), so the portrait never spills past the frame.
            Rect cardRect = Iv(0, 1700, 1947, 4593);
            RectTransform front = kit.NewRect("CandidatePortrait", page);
            front.anchorMin = front.anchorMax = new Vector2(0f, 1f);
            front.pivot = BadgeClipPivot1108;
            front.sizeDelta = cardRect.size;
            front.anchoredPosition = new Vector2(cardRect.x + cardRect.width * front.pivot.x, -(cardRect.y + cardRect.height * (1f - front.pivot.y)));
            RectTransform window = kit.NewRect("CandidateWindow", front);
            PanelKit.Stretch(window);
            Image windowShape = window.gameObject.AddComponent<Image>();
            windowShape.sprite = Resources.Load<Sprite>(InterviewArt1107 + "iv-card-0-window");
            windowShape.raycastTarget = false;
            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            float px = cardRect.width / BadgeArtSize1108.x;
            RectTransform photo = kit.NewRect("CandidatePhoto", window);
            photo.anchorMin = photo.anchorMax = new Vector2(0f, 1f);
            photo.pivot = new Vector2(.5f, .5f);
            photo.anchoredPosition = new Vector2(BadgeHoleCentre1108.x * px, -BadgeHoleCentre1108.y * px);
            photo.sizeDelta = BadgeHoleSize1108 * px * 1.06f;
            photo.localEulerAngles = new Vector3(0f, 0f, BadgeTilt1108);
            // 2026-11-08 (少爷: "工牌不要镂空…弄成纯蓝色就是工牌设计本身那个颜色"): cut-out busts stand on a badge-blue photo backdrop.
            Image photoBg = kit.NewImage("CandidatePhotoBg", photo, Resources.Load<Sprite>(InterviewArt1107 + "iv-photo-bg"), Color.white);
            PanelKit.Stretch(photoBg.rectTransform);
            photoBg.raycastTarget = false;
            Image badgeFrame = kit.NewImage("InterviewCard0", front, Resources.Load<Sprite>(InterviewArt1107 + "iv-card-0"), Color.white);
            PanelKit.Stretch(badgeFrame.rectTransform);
            badgeFrame.raycastTarget = false;
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

            Image portrait = kit.NewImage("CandidatePortraitArt", photo, CandidateSprite(member), Color.white);
            PanelKit.Stretch(portrait.rectTransform);
            portrait.raycastTarget = false;
            if (portrait.sprite != null) PanelKit.FrameBustPortrait(portrait, photo, member.Id);
            // 2026-11-08 (少爷: "底部两张也要有角色立绘"): the back cards show the next two candidates.
            for (int k = 1; k <= 2 && k < candidates.Count; k++)
            {
                var back = stack.Find("InterviewCard" + k) as RectTransform;
                if (back != null) BackCardPortrait1108(back, k, candidates[(interviewCandidateIndex + k) % candidates.Count]);
            }

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
            GameObject signHit = InterviewBoardHotspot("SignCandidate", actions, signRect.x, signRect.y, signRect.width, signRect.height,
                () => SignInterviewCandidate(memberIndex, cost));
            IvBakedMotion1108(signHit, page, signRect, actions.Find("SigningPrice"));

            // ‹ 1/10 › under the card stack (the shared switch arrows), plus a swipe on the stack.
            Rect nav = Iv(40, 5880, 1560, 6260);
            Text counter = kit.NewPlacedText(page, $"{interviewCandidateIndex + 1} / {candidates.Count}", 26, Color.white,
                nav.x + 70f, nav.y, nav.width - 140f, nav.height, TextAnchor.MiddleCenter, FontStyle.Bold);
            counter.name = "CandidateCounter";
            counter.gameObject.AddComponent<Outline>().effectColor = new Color32(20, 10, 40, 255);
            InterviewBadgeSwipe1108 swipe = AttachBadgeSwipe1108(page, Iv(0, 1700, 1700, 5800));
            Button previous = SwitchArrow1107.Create(page, "PreviousCandidate", -1, new Rect(nav.x, nav.y, 70f, nav.height), () => ThrowBadge1108(swipe, -1));
            Button next = SwitchArrow1107.Create(page, "NextCandidate", 1, new Rect(nav.xMax - 70f, nav.y, 70f, nav.height), () => ThrowBadge1108(swipe, 1));
            previous.interactable = next.interactable = candidates.Count > 1;
        }

        // iv-card-0 (623 × 925 px): the lanyard clip, the tilted window's centre / size and its tilt (measured from the art).
        private static readonly Vector2 BadgeArtSize1108 = new Vector2(623f, 925f);
        private static readonly Vector2 BadgeClipPivot1108 = new Vector2(180f / 623f, 1f - 18f / 925f);
        // 2026-11-08 (少爷: "工牌边缘有点脏"): the window is redrawn as a crisp rounded rect with a clean purple rim.
        private static readonly Vector2 BadgeHoleCentre1108 = new Vector2(297.6f, 495.5f);
        private static readonly Vector2 BadgeHoleSize1108 = new Vector2(424f, 581f);
        private const float BadgeTilt1108 = 15.5f;
        // The two back cards (iv-card-1 / -2, 530 × 710 px) and their windows.
        private static readonly Vector2 BackCardArt1108 = new Vector2(530f, 710f);
        private static readonly Vector2[] BackHoleCentre1108 = { new Vector2(273f, 392.1f), new Vector2(275f, 392.3f) };
        private static readonly Vector2 BackHoleSize1108 = new Vector2(317.7f, 443.6f);

        /// <summary>A back card's masked photo: badge-blue backdrop + the candidate's bust, tilted like its window.</summary>
        private void BackCardPortrait1108(RectTransform card, int k, int memberIndex)
        {
            if (memberIndex < 0 || memberIndex >= GameModel.Members.Length) return;
            float px = card.sizeDelta.x / BackCardArt1108.x;
            RectTransform window = kit.NewRect("BackCardWindow", card);
            PanelKit.Stretch(window);
            Image shape = window.gameObject.AddComponent<Image>();
            shape.sprite = Resources.Load<Sprite>(InterviewArt1107 + "iv-card-" + k + "-window");
            shape.raycastTarget = false;
            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform photo = kit.NewRect("BackCardPhoto", window);
            photo.anchorMin = photo.anchorMax = new Vector2(0f, 1f);
            photo.pivot = new Vector2(.5f, .5f);
            Vector2 centre = BackHoleCentre1108[k - 1];
            photo.anchoredPosition = new Vector2(centre.x * px, -centre.y * px);
            photo.sizeDelta = BackHoleSize1108 * px * 1.06f;
            photo.localEulerAngles = new Vector3(0f, 0f, BadgeTilt1108);
            Image bg = kit.NewImage("BackCardPhotoBg", photo, Resources.Load<Sprite>(InterviewArt1107 + "iv-photo-bg"), Color.white);
            PanelKit.Stretch(bg.rectTransform);
            bg.raycastTarget = false;
            MemberDefinition member = GameModel.Members[memberIndex];
            Image bust = kit.NewImage("BackCardPortrait", photo, CandidateSprite(member), Color.white);
            PanelKit.Stretch(bust.rectTransform);
            bust.raycastTarget = false;
            if (bust.sprite != null) PanelKit.FrameBustPortrait(bust, photo, member.Id);
        }

        private bool promoting1108;

        /// <summary>The tapped back card rises over the stack into the front badge's place while the front badge slides off.</summary>
        private System.Collections.IEnumerator PromoteBackCard1108(RectTransform page, int steps)
        {
            var stack = page != null ? page.Find("CandidateCard") as RectTransform : null;
            var card = stack != null ? stack.Find(steps >= 2 ? "InterviewCard2" : "InterviewCard1") as RectTransform : null;
            var front = page != null ? page.Find("CandidatePortrait") as RectTransform : null;
            if (card == null || front == null || ChoSiren.BattlePresentationOptions.ReduceMotion) { MoveInterviewCandidate(steps); yield break; }
            promoting1108 = true;
            card.SetParent(page, true);
            card.SetAsLastSibling();
            Vector2 from = card.anchoredPosition;
            Rect target = Iv(0, 1700, 1947, 4593);
            Vector2 to = new Vector2(target.x, -target.y);
            float grow = target.width / Mathf.Max(1f, card.sizeDelta.x);
            CanvasGroup fade = front.GetComponent<CanvasGroup>();
            if (fade == null) fade = front.gameObject.AddComponent<CanvasGroup>();
            Vector2 frontFrom = front.anchoredPosition;
            Quaternion frontTurn = front.localRotation;
            const float seconds = .42f;
            for (float t = 0f; t < seconds;)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, .05f);
                float k = Mathf.Clamp01(t / seconds);
                float c = 1.4f, e = 1f + (c + 1f) * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f); // ease-out-back
                float arc = Mathf.Sin(k * Mathf.PI);
                card.anchoredPosition = Vector2.LerpUnclamped(from, to, e) + new Vector2(0f, arc * 36f);
                card.localScale = Vector3.one * Mathf.LerpUnclamped(1f, grow, e);
                card.localEulerAngles = new Vector3(0f, 0f, -arc * 5f);
                front.anchoredPosition = frontFrom + new Vector2(-280f * k * k, -24f * k);
                front.localRotation = frontTurn * Quaternion.Euler(0f, 0f, 14f * k);
                fade.alpha = 1f - k;
                yield return null;
            }
            promoting1108 = false;
            ChoSiren.UI.InterviewBadgeSwipe1108.PendingEntry = 0;
            MoveInterviewCandidate(steps);
        }

        /// <summary>Drag surface over the badge stack: the badge swings on its clip and is thrown to change candidate.</summary>
        private InterviewBadgeSwipe1108 AttachBadgeSwipe1108(RectTransform page, Rect bounds)
        {
            var badge = page.Find("CandidatePortrait") as RectTransform;
            var stack = page.Find("CandidateCard") as RectTransform;
            if (badge == null) return null;
            Image hit = kit.NewImage("CandidateSwipeSurface", page, null, Color.clear);
            PanelKit.PlaceTop(hit.rectTransform, bounds.x, bounds.y, bounds.width, bounds.height);
            hit.raycastTarget = true;
            var swipe = hit.gameObject.AddComponent<InterviewBadgeSwipe1108>();
            // The back cards grow into the place in front of them as the badge is dragged away (windows matched by centre
            // and width, so the photos land exactly where the rebuilt page puts them).
            var c1 = stack != null ? stack.Find("InterviewCard1") as RectTransform : null;
            var c2 = stack != null ? stack.Find("InterviewCard2") as RectTransform : null;
            if (c1 != null && InterviewCandidates().Count > 1)
            {
                Rect frontRect = Iv(0, 1700, 1947, 4593);
                float px0 = frontRect.width / BadgeArtSize1108.x, px1 = c1.sizeDelta.x / BackCardArt1108.x;
                Vector2 frontWindow = new Vector2(frontRect.x + BadgeHoleCentre1108.x * px0, -(frontRect.y + BadgeHoleCentre1108.y * px0));
                Vector2 hole1 = new Vector2(BackHoleCentre1108[0].x * px1, -BackHoleCentre1108[0].y * px1);
                float s1 = BadgeHoleSize1108.x * px0 / (BackHoleSize1108.x * px1);
                Vector2 t1 = frontWindow - hole1 * s1;
                Vector2 t2 = Vector2.zero; float s2 = 1f;
                if (c2 != null)
                {
                    float px2 = c2.sizeDelta.x / 532f;
                    Vector2 hole2 = new Vector2(BackHoleCentre1108[1].x * px2, -BackHoleCentre1108[1].y * px2);
                    s2 = px1 / px2;
                    t2 = c1.anchoredPosition + hole1 - hole2 * s2;
                }
                swipe.SetBackCards(c1, t1, s1, c2, t2, s2);
            }
            swipe.Configure(badge, stack, () => !closing && !promoting1108 && signModal == null && InterviewCandidates().Count > 1, MoveInterviewCandidate);
            // 2026-11-08 (少爷: "点击底部第一张要有这张到最顶部的动画"): a tapped back card flies up to the front.
            swipe.TapPromote = steps => StartCoroutine(PromoteBackCard1108(page, steps));
            // Tapping the visible strip of the first back card skips one candidate, the second back card two.
            swipe.TapSteps = e =>
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(page, e.position, e.pressEventCamera, out Vector2 p)) return 0;
                float y = -p.y;
                int count = InterviewCandidates().Count;
                if (y >= Iv(0, 5085, 1, 5086).y) return count > 2 ? 2 : 1;
                if (y >= Iv(0, 4593, 1, 4594).y) return 1;
                return 0;
            };
            return swipe;
        }

        /// <summary>‹ ›: change at once (the counter and buttons answer immediately); the new badge swings in from the stack.</summary>
        private void ThrowBadge1108(InterviewBadgeSwipe1108 swipe, int direction)
        {
            if (InterviewCandidates().Count > 1) InterviewBadgeSwipe1108.PendingEntry = direction;
            MoveInterviewCandidate(direction);
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
                IvBakedMotion1108(hit, page, pools[i]);
                if (i != interviewPoolIndex) continue;
                Image glow = ReferencePunkFx.Deco(page, "SelectedChannelGlow", ReferencePunkFx.Glow(), new Color(.11f, .95f, .93f, .4f),
                    pools[i].x, pools[i].yMax - 14f, pools[i].width, 26f);
                Image mark = kit.NewImage("SelectedInterviewChannel", page, null, Brand1025.Teal);
                PanelKit.PlaceTop(mark.rectTransform, pools[i].x + 24f, pools[i].yMax - 4f, pools[i].width - 48f, 6f);
                mark.raycastTarget = false;
                hit.transform.SetAsLastSibling();
            }
        }

        /// <summary>2026-11-08 (少爷: "按键哪里有触屏没有反馈的都要优化"): the baked board art under a hotspot lifts / pops on press.</summary>
        private static void IvBakedMotion1108(GameObject hit, RectTransform page, Rect pageRect, params Transform[] extras)
        {
            Image board = page.Find("InterviewBoard1107")?.GetComponent<Image>();
            if (hit == null || board == null || board.sprite == null) return;
            Rect b = Iv(0, 700, 4320, 7100);
            float k = board.sprite.rect.width / b.width;
            var region = new Rect((pageRect.x - b.x) * k, (pageRect.y - b.y) * k, pageRect.width * k, pageRect.height * k);
            extras = System.Array.FindAll(extras, t => t != null);
            if (BakedRegionMotion038.Attach(hit, board, region, extras) == null) return;
            Transform body = hit.transform.Find(BakedRegionMotion038.BodyName);
            if (body != null) body.SetAsFirstSibling();
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
