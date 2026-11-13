using System.Collections;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-12 (少爷: "新手引导没有做好，用户都不知道干嘛…骰子是干嘛的也不知道，尽量不要用那么多文字来说明用视觉化的。
    /// 然后现在是我方在攻击还是对方在攻击也不知道，要有提示").
    /// 1. Whose turn: the side that acts glows — teal along our cards at the bottom, red along the enemy row at the top —
    ///    and whenever the turn passes to the other side a slanted P5 plate sweeps across: 我方回合 / 敌方回合.
    /// 2. The first fight opens with one picture card (three rows: dice → hand → captain hits harder; four energy bars,
    ///    the front of the line fills first → tap her for the ultimate; teal = us, red = them). Tap anywhere to start.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        private static readonly Color32 EnemyRed1112 = new Color32(255, 52, 84, 255);
        private RectTransform sideSweep1112;
        private Image ourGlow1112, theirGlow1112;
        private Text sweepTitle1112, sweepSub1112;
        private Graphic sweepStripe1112;
        private BattleSide? shownSide1112;
        private float sweepClock1112 = -1f, glowClock1112;
        private const string PrimerFlag1112 = "guide-battle-1112-primer";

        private void BuildSideCue1112()
        {
            if (ourGlow1112 != null) return;
            RectTransform host = (RectTransform)transform;
            ourGlow1112 = EdgeGlow1112(host, "OurTurnGlow1112", Brand1025.Teal, false);
            theirGlow1112 = EdgeGlow1112(host, "EnemyTurnGlow1112", EnemyRed1112, true);

            sideSweep1112 = kit.NewRect("SideSweep1112", host);
            sideSweep1112.anchorMin = sideSweep1112.anchorMax = new Vector2(.5f, .5f);
            sideSweep1112.pivot = new Vector2(.5f, .5f);
            sideSweep1112.sizeDelta = new Vector2(760f, 132f);
            sideSweep1112.anchoredPosition = new Vector2(0f, 70f);
            P5Letters1023.Plate(sideSweep1112, "Shadow", new Vector2(8f, -8f), new Vector2(620f, 112f), Color.black, -14f);
            P5Letters1023.Plate(sideSweep1112, "Plate", Vector2.zero, new Vector2(620f, 112f), Brand1025.Ink, -14f);
            P5Letters1023.Plate(sideSweep1112, "Stripe", new Vector2(-6f, -40f), new Vector2(560f, 14f), Brand1025.Teal, -14f);
            sweepStripe1112 = sideSweep1112.Find("Stripe").GetComponent<Graphic>(); // skewed plates are not Images
            sweepTitle1112 = VisualBattle1101.NewText("Title", sideSweep1112, kit.Font, "我方回合", 58, Color.white);
            VisualBattle1101.Place(sweepTitle1112.rectTransform, new Vector2(-30f, 10f), new Vector2(520f, 72f));
            sweepTitle1112.fontStyle = FontStyle.BoldAndItalic;
            sweepSub1112 = VisualBattle1101.NewText("Sub", sideSweep1112, kit.Font, "YOUR TURN", 20, Brand1025.Teal);
            VisualBattle1101.Place(sweepSub1112.rectTransform, new Vector2(170f, -26f), new Vector2(220f, 26f));
            sweepSub1112.fontStyle = FontStyle.BoldAndItalic;
            foreach (Graphic g in sideSweep1112.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            sideSweep1112.gameObject.SetActive(false);
        }

        /// <summary>A soft band of colour along one edge (top = enemy row, bottom = our cards); taps pass through.</summary>
        private Image EdgeGlow1112(RectTransform host, string name, Color color, bool top)
        {
            Image glow = kit.NewImage(name, host, ChoSiren.UI.VisualA1101.Glow, new Color(color.r, color.g, color.b, 0f));
            RectTransform r = glow.rectTransform;
            r.anchorMin = new Vector2(-.1f, top ? .78f : -.06f);
            r.anchorMax = new Vector2(1.1f, top ? 1.06f : .34f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            glow.raycastTarget = false;
            glow.transform.SetSiblingIndex(Mathf.Min(1, host.childCount - 1));
            return glow;
        }

        /// <summary>Called whenever a unit starts its turn.</summary>
        private void ShowTurnSide1112(BattleUnit actor)
        {
            if (actor == null) return;
            BuildSideCue1112();
            BattleSide side = actor.Side;
            if (shownSide1112 == side) return;
            shownSide1112 = side;
            bool ours = side == BattleSide.Player;
            Color tone = ours ? (Color)Brand1025.Teal : EnemyRed1112;
            sweepTitle1112.text = ours ? "我方回合" : "敌方回合";
            sweepSub1112.text = ours ? "YOUR TURN" : "ENEMY TURN";
            sweepSub1112.color = tone;
            sweepStripe1112.color = tone;
            if (BattlePresentationOptions.ReduceMotion) return;
            sideSweep1112.SetAsLastSibling();
            if (pauseOverlay != null) pauseOverlay.transform.SetAsLastSibling();
            if (rulesOverlay != null) rulesOverlay.transform.SetAsLastSibling();
            sideSweep1112.gameObject.SetActive(true);
            sweepClock1112 = 0f;
        }

        /// <summary>Per frame (from Update): the sweep and the breathing edge glows.</summary>
        private void AdvanceSideCue1112()
        {
            if (ourGlow1112 == null) return;
            float dt = paused ? 0f : Time.unscaledDeltaTime * Mathf.Max(1, speed);
            glowClock1112 += dt;
            float breathe = .22f + .1f * Mathf.Sin(glowClock1112 * 3.2f);
            bool battleOn = battle != null && battle.Outcome == BattleOutcome.Ongoing && shownSide1112.HasValue;
            SetGlow1112(ourGlow1112, battleOn && shownSide1112 == BattleSide.Player ? breathe : 0f, dt);
            SetGlow1112(theirGlow1112, battleOn && shownSide1112 == BattleSide.Enemy ? breathe : 0f, dt);
            if (sweepClock1112 < 0f) return;
            sweepClock1112 += dt;
            const float inT = .16f, hold = .5f, outT = .16f;
            float t = sweepClock1112, x;
            if (t < inT) x = Mathf.Lerp(-820f, 0f, 1f - Mathf.Pow(1f - t / inT, 3f));
            else if (t < inT + hold) x = Mathf.Lerp(0f, 24f, (t - inT) / hold);
            else x = Mathf.Lerp(24f, 860f, Mathf.Pow((t - inT - hold) / outT, 2f));
            sideSweep1112.anchoredPosition = new Vector2(x, 70f);
            if (t >= inT + hold + outT) { sideSweep1112.gameObject.SetActive(false); sweepClock1112 = -1f; }
        }

        private static void SetGlow1112(Image glow, float target, float dt)
        {
            Color c = glow.color;
            c.a = Mathf.MoveTowards(c.a, target, dt * 1.6f);
            glow.color = c;
        }

        // ------------------------------------------------------------------ first-fight picture card

        private bool PrimerPending1112 => OnboardingAllowed1101 && !model.HasStoryFlag(PrimerFlag1112);

        private IEnumerator PlayPrimer1112()
        {
            if (!PrimerPending1112) yield break;
            model.FirstTime(PrimerFlag1112);
            RectTransform host = (RectTransform)transform;
            Image shade = kit.NewImage("BattlePrimer1112", host, null, new Color(.03f, .01f, .08f, .9f));
            PanelKit.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            RectTransform card = kit.NewRect("Card", shade.transform);
            card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
            card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(640f, 860f);

            RectTransform head = kit.NewRect("Head", card);
            PanelKit.PlaceTop(head, 40, 0, 300, 70);
            P5Letters1023.Plate(head, "Shadow", new Vector2(6f, -6f), new Vector2(300f, 70f), EnemyRed1112, -14f);
            P5Letters1023.Plate(head, "Plate", Vector2.zero, new Vector2(300f, 70f), Brand1025.Ink, -14f);
            Text title = VisualBattle1101.NewText("Title", head, kit.Font, "怎么打", 44, Color.white);
            VisualBattle1101.Place(title.rectTransform, Vector2.zero, new Vector2(280f, 66f));
            title.fontStyle = FontStyle.BoldAndItalic;

            PrimerRow1112(card, 0, "凑牌型 → 队长打得更狠", RulesDice1101);
            PrimerRow1112(card, 1, "排在前面的先攒满 → 点她放大招", PrimerEnergy1112);
            PrimerRow1112(card, 2, "看颜色：青 = 我方出手，红 = 敌人出手", PrimerSides1112);

            Text tap = VisualBattle1101.NewText("Tap", card, kit.Font, "点任意处开始", 24, Brand1025.Teal);
            VisualBattle1101.Place(tap.rectTransform, new Vector2(0f, -400f), new Vector2(400f, 36f));
            if (pauseOverlay != null) pauseOverlay.transform.SetAsLastSibling();

            bool done = false;
            Button close = shade.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.targetGraphic = shade;
            close.onClick.AddListener(() => done = true);
            var group = shade.gameObject.AddComponent<CanvasGroup>();
            for (float t = 0f; t < .25f && !closing; t += Time.unscaledDeltaTime) { group.alpha = t / .25f; yield return null; }
            group.alpha = 1f;
            float shown = Time.unscaledTime;
            while (!closing && !(done && Time.unscaledTime - shown > .4f))
            {
                if (done && Time.unscaledTime - shown <= .4f) done = false; // a stray double tap does not skip it
                tap.color = new Color(tap.color.r, tap.color.g, tap.color.b, .55f + .45f * Mathf.Sin(Time.unscaledTime * 4f));
                yield return null;
            }
            for (float t = 0f; t < .2f && !closing; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / .2f; yield return null; }
            if (shade != null) Destroy(shade.gameObject);
        }

        private void PrimerRow1112(RectTransform card, int index, string line, System.Action<RectTransform> art)
        {
            RectTransform row = kit.NewRect("Row-" + index, card);
            PanelKit.PlaceTop(row, 0, 104 + index * 228, 640, 210);
            Image plate = kit.NewImage("Plate", row, kit.RoundedSprite(18), new Color32(22, 12, 40, 255));
            PanelKit.Stretch(plate.rectTransform); plate.raycastTarget = false;
            RectTransform picture = kit.NewRect("Picture", row);
            PanelKit.PlaceTop(picture, 14, 16, 220, 178);
            art(picture);
            if (index == 0) picture.localScale = Vector3.one * 1.5f; // the dice tile art is drawn for a smaller tile
            Text t = VisualBattle1101.NewText("Line", row, kit.Font, line, 28, Color.white, TextAnchor.MiddleLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(254f, -24f);
            t.rectTransform.sizeDelta = new Vector2(360f, 162f);
            foreach (Graphic g in row.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        /// <summary>Four energy bars in formation order, the first one fuller and fastest, then a finger tapping the full one.</summary>
        private void PrimerEnergy1112(RectTransform p)
        {
            float[] fill = { 1f, .78f, .58f, .42f };
            for (int i = 0; i < 4; i++)
            {
                float y = 60f - i * 38f;
                Text n = VisualBattle1101.NewText("N" + i, p, kit.Font, (i + 1).ToString(), 22, i == 0 ? (Color)Brand1025.Teal : Color.white);
                VisualBattle1101.Place(n.rectTransform, new Vector2(-92f, y), new Vector2(26f, 30f));
                Image track = VisualBattle1101.NewImage("Track" + i, p, null, new Color(1f, 1f, 1f, .14f));
                VisualBattle1101.Place(track.rectTransform, new Vector2(-12f, y), new Vector2(130f, 14f));
                Image bar = VisualBattle1101.NewImage("Bar" + i, p, null, i == 0 ? new Color32(255, 214, 110, 255) : new Color32(90, 170, 255, 255));
                bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(.5f, .5f);
                bar.rectTransform.pivot = new Vector2(0f, .5f);
                bar.rectTransform.anchoredPosition = new Vector2(-77f, y);
                bar.rectTransform.sizeDelta = new Vector2(130f * fill[i], 14f);
            }
            Image burst = Pic1101(p, "Burst", BattlePsdArt928.Sprite("icon-star-burst"), new Vector2(84f, 60f), new Vector2(40f, 40f));
            Sprite hand = Resources.Load<Sprite>(CoachMark1101.HandResource);
            if (hand != null) Pic1101(p, "Hand", hand, new Vector2(84f, 18f), new Vector2(54f, 54f), 12f);
            burst.transform.SetAsLastSibling();
        }

        /// <summary>A teal plate under our cards and a red plate over the enemy row.</summary>
        private void PrimerSides1112(RectTransform p)
        {
            P5Letters1023.Plate(p, "Enemy", new Vector2(0f, 46f), new Vector2(170f, 46f), EnemyRed1112, -14f);
            Text e = VisualBattle1101.NewText("EnemyText", p, kit.Font, "敌方回合", 24, Color.white);
            VisualBattle1101.Place(e.rectTransform, new Vector2(0f, 46f), new Vector2(160f, 40f));
            P5Letters1023.Plate(p, "Ours", new Vector2(0f, -30f), new Vector2(170f, 46f), Brand1025.Teal, -14f);
            Text o = VisualBattle1101.NewText("OursText", p, kit.Font, "我方回合", 24, Brand1025.Ink);
            VisualBattle1101.Place(o.rectTransform, new Vector2(0f, -30f), new Vector2(160f, 40f));
            foreach (Outline outline in p.GetComponentsInChildren<Outline>(true)) outline.enabled = false;
        }
    }
}
