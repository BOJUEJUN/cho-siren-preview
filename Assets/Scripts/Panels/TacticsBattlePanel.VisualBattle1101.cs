using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        // 2026-11-02 (赵金 visual-battle, 用户: "我是视觉视听享受游戏，所有页面都要做到用最少文字的方案，多用设计跟图去表达、
        // 音效去表达"). The PSD battle reads by pictures: the captain band is a race emblem + "+N%", the hand stamp
        // (DiceStamps) + "×N" and the wave target as a mini stamp with a check; statuses land as their PSD symbols (only
        // 禁魔 / 封印 keep a two-character tag); a charging boss wears a ring countdown with an edge warning light and a
        // sound; captions that only named what the art already shows (回合, 本波敌方生命, 已击败, 全员挡一击…) are gone.
        // Reroll counts / pips / the bonus cap belong to dice-endless: the charge bar and the lower ribbon row
        // (DiceRibbonRow1101) are left for it.

        private VisualBattleHud1101 visualHud1101;
        private Image captainRace1101, handStamp1101, handDie1101, targetStamp1101, targetFlag1101, targetCheck1101,
            heatIcon1101, defeatedIcon1101;
        private Text captainBonus1101, handMult1101, targetArrow1101, heatText1101;
        private RectTransform captainChip1101, handChip1101, targetChip1101, heatChip1101;
        /// <summary>The lower ribbon of the dice band, a centred row for small chips (heat here; dice-endless may add).</summary>
        private RectTransform diceRibbonRow1101;
        private Text diceTitle1101;
        private readonly List<Image> guardPips1101 = new List<Image>();
        private EdgeWarning1101 edgeWarning1101;
        private readonly Dictionary<int, ChargeWarning1101> chargeWarnings1101 = new Dictionary<int, ChargeWarning1101>();
        private string shownHand1101;
        private float handPopClock1101 = 9f, targetPopClock1101 = 9f, silenceStampAt1101 = -9f;
        private bool targetWasDone1101;
        private static readonly Regex MovedToPictures1101 = new Regex(
            @"(队长攻击\s*\+[\d.]+%|目标达成✓|目标：[^·]+|热度\d+)", RegexOptions.Compiled);

        /// <summary>Called once at the end of <c>ApplyPsdBattleLayout</c>.</summary>
        private void BuildVisualBattle1101()
        {
            if (psdRoot == null) return;
            Font font = kit.Font;

            // ---- top HUD: the art's clock, the pink bar and its numbers say it; the captions go.
            if (psdCountdownCaption != null) psdCountdownCaption.enabled = false;
            if (enemyHealthCaption != null) enemyHealthCaption.enabled = false;
            if (enemyDefeatedText != null)
            {
                enemyDefeatedText.alignment = TextAnchor.MiddleRight;
                enemyDefeatedText.fontSize = enemyDefeatedText.resizeTextMaxSize = 17;
                defeatedIcon1101 = VisualBattle1101.NewImage("DefeatedIcon1101", enemyDefeatedText.transform,
                    BattlePsdArt928.Sprite("icon-demon-mask"), Color.white);
                var r = defeatedIcon1101.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(1f, .5f); r.pivot = new Vector2(1f, .5f); r.sizeDelta = new Vector2(22f, 22f);
            }

            // ---- captain band: race emblem + bonus on the pink tag
            diceTitle1101 = psdRoot.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.text == "队长演出");
            if (diceTitle1101 != null) diceTitle1101.enabled = false;
            captainChip1101 = kit.NewRect("CaptainBonus1101", psdRoot);
            PsdPlace(captainChip1101, 440, 5010, 560, 210);
            captainRace1101 = VisualBattle1101.NewImage("CaptainRace1101", captainChip1101, null, Color.white);
            captainRace1101.rectTransform.anchorMin = captainRace1101.rectTransform.anchorMax = new Vector2(0f, .5f);
            captainRace1101.rectTransform.pivot = new Vector2(0f, .5f);
            captainRace1101.rectTransform.anchoredPosition = Vector2.zero;
            captainRace1101.rectTransform.sizeDelta = new Vector2(48f, 48f);
            captainBonus1101 = VisualBattle1101.NewText("CaptainBonusValue1101", captainChip1101, font, "+0%", 21, Color.white,
                TextAnchor.MiddleLeft);
            var bonusRect = captainBonus1101.rectTransform;
            bonusRect.anchorMin = bonusRect.anchorMax = new Vector2(0f, .5f); bonusRect.pivot = new Vector2(0f, .5f);
            bonusRect.anchoredPosition = new Vector2(52f, -1f); bonusRect.sizeDelta = new Vector2(80f, 30f);

            // The flat boxes over the band's baked sample words get feathered edges (and room so the words stay in
            // their solid core), so they melt into the band instead of reading as patches.
            SoftenCover1101("DiceHandCover", 1060, 5060, 760, 122);
            SoftenCover1101("DiceChargeCover", 1918, 5062, 420, 118);

            // ---- the hand: its stamp art + multiplier
            if (diceHandText != null) diceHandText.enabled = false;
            handChip1101 = kit.NewRect("HandStamp1101", psdRoot);
            PsdPlace(handChip1101, 1110, 5010, 860, 216);
            handStamp1101 = VisualBattle1101.NewImage("HandStampArt1101", handChip1101, null, Color.white);
            VisualBattle1101.Place(handStamp1101.rectTransform, new Vector2(-28f, 0f), new Vector2(118f, 46f));
            handDie1101 = VisualBattle1101.NewImage("HandRolling1101", handChip1101, BattlePsdArt928.Sprite("icon-dice"), Color.white);
            VisualBattle1101.Place(handDie1101.rectTransform, new Vector2(-28f, 0f), new Vector2(26f, 30f));
            handMult1101 = VisualBattle1101.NewText("HandMultiplier1101", handChip1101, font, "×1", 24,
                new Color32(255, 214, 110, 255), TextAnchor.MiddleLeft);
            var multRect = handMult1101.rectTransform;
            multRect.anchoredPosition = new Vector2(70f, 0f); multRect.sizeDelta = new Vector2(70f, 30f);

            // ---- the wave target: flag + mini hand stamp + ▲ (or better); a check seals it when reached
            targetChip1101 = kit.NewRect("DiceTarget1101", psdRoot);
            PsdPlace(targetChip1101, 1950, 5050, 380, 150);
            targetFlag1101 = VisualBattle1101.NewImage("TargetFlag1101", targetChip1101, ChoSiren.UI.VisualA1101.Icon("flag"), Brand1025.Teal);
            VisualBattle1101.Place(targetFlag1101.rectTransform, new Vector2(-34f, 0f), new Vector2(18f, 18f));
            targetStamp1101 = VisualBattle1101.NewImage("TargetStamp1101", targetChip1101, null, Color.white);
            VisualBattle1101.Place(targetStamp1101.rectTransform, new Vector2(4f, 0f), new Vector2(56f, 24f));
            targetArrow1101 = VisualBattle1101.NewText("TargetUp1101", targetChip1101, font, "▲", 11, Brand1025.Teal);
            VisualBattle1101.Place(targetArrow1101.rectTransform, new Vector2(38f, 4f), new Vector2(14f, 14f));
            targetCheck1101 = VisualBattle1101.NewImage("TargetCheck1101", targetChip1101, ChoSiren.UI.VisualA1101.Icon("check"), Brand1025.Teal);
            VisualBattle1101.Place(targetCheck1101.rectTransform, new Vector2(34f, 2f), new Vector2(26f, 26f));
            var checkGlow = targetCheck1101.gameObject.AddComponent<Outline>();
            checkGlow.effectColor = Brand1025.Ink; checkGlow.effectDistance = new Vector2(1.5f, -1.5f);

            // ---- lower ribbon: a row for small chips (heat now; reroll pips / cap from dice-endless may join)
            Transform ribbon = psdRoot.Find("PsdDiceSubtitle");
            if (ribbon != null && ribbon.Find("DiceSubCover") is RectTransform subCover) PsdPlace(subCover, -20, 2, 1390, 116);
            diceRibbonRow1101 = kit.NewRect("DiceRibbonRow1101", ribbon != null ? ribbon : psdRoot);
            if (ribbon != null) PanelKit.Stretch(diceRibbonRow1101); else PsdPlace(diceRibbonRow1101, 735, 5256, 1350, 126);
            var row = diceRibbonRow1101.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter; row.spacing = 14f;
            row.childControlWidth = row.childControlHeight = false; row.childForceExpandWidth = row.childForceExpandHeight = false;
            heatChip1101 = kit.NewRect("HeatChip1101", diceRibbonRow1101);
            heatChip1101.sizeDelta = new Vector2(52f, 26f);
            heatIcon1101 = VisualBattle1101.NewImage("HeatIcon1101", heatChip1101, BattlePsdArt928.Sprite("icon-star-burst"), Color.white);
            VisualBattle1101.Place(heatIcon1101.rectTransform, new Vector2(-12f, 0f), new Vector2(24f, 24f));
            heatText1101 = VisualBattle1101.NewText("HeatValue1101", heatChip1101, font, "1", 17, new Color32(255, 196, 96, 255), TextAnchor.MiddleLeft);
            VisualBattle1101.Place(heatText1101.rectTransform, new Vector2(16f, 0f), new Vector2(30f, 24f));
            heatChip1101.gameObject.SetActive(false);
            if (diceInstructionText != null) diceInstructionText.transform.SetAsLastSibling();

            // ---- 应急守护: four shield pips (one per member) replace the subtitle sentence
            if (guardHint != null && psdGuardRing != null)
            {
                guardHint.enabled = false;
                Transform plate = psdGuardRing.transform.parent;
                for (int i = 0; i < 4; i++)
                {
                    Image pip = VisualBattle1101.NewImage("GuardPip1101-" + i, plate, BattlePsdArt928.Sprite("icon-cyan-shield"), Color.white);
                    VisualBattle1101.Place(pip.rectTransform, new Vector2((i - 1.5f) * 20f, 0f), new Vector2(17f, 17f));
                    guardPips1101.Add(pip);
                }
            }

            // ---- member status badges: icon + rounds (the symbol is the name)
            edgeWarning1101 = EdgeWarning1101.Create((RectTransform)transform, () => paused || closing);
            edgeWarning1101.transform.SetSiblingIndex(psdRoot.GetSiblingIndex() + 1);

            RestylePause1101();
            BuildRulesPictures1101();

            visualHud1101 = psdRoot.gameObject.AddComponent<VisualBattleHud1101>();
            visualHud1101.Tick = RefreshVisualBattle1101;
            RefreshVisualBattle1101();
        }

        private void SoftenCover1101(string name, float x, float y, float w, float h)
        {
            if (!(psdRoot.Find(name) is RectTransform cover)) return;
            Image image = cover.GetComponent<Image>();
            if (image == null) return;
            image.sprite = VisualBattle1101.SoftCoverSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2.4f; // ≈ 10 canvas px of feather
            PsdPlace(cover, x, y, w, h);
        }

        /// <summary>Every frame after the panel's own refresh: pictures follow the live values.</summary>
        private void RefreshVisualBattle1101()
        {
            if (psdRoot == null || battle == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            bool calm = BattlePresentationOptions.ReduceMotion;

            // top HUD
            if (enemyDefeatedText != null && defeatedIcon1101 != null)
            {
                Match m = Regex.Match(enemyDefeatedText.text ?? string.Empty, @"(\d+)\s*/\s*(\d+)");
                if (m.Success) enemyDefeatedText.text = m.Groups[1].Value + "/" + m.Groups[2].Value;
                defeatedIcon1101.rectTransform.anchoredPosition = new Vector2(-enemyDefeatedText.preferredWidth - 4f, 1f);
            }
            if (enemyHpText != null && enemyHpText.text.StartsWith("Boss", System.StringComparison.Ordinal)) enemyHpText.text = string.Empty;

            // captain band
            bool dice = diceTurn != null && diceTurn.Hand != null;
            BattleUnit leader = battle.IsTurnBased ? battle.FindUnit(battle.CurrentLeaderId) : null;
            CombatRace race = leader != null ? battle.RaceOf(leader) : ReadoutLeaderRace();
            if (captainRace1101 != null)
            {
                captainRace1101.sprite = VisualBattle1101.RaceIcon(race);
                captainRace1101.enabled = captainRace1101.sprite != null;
                int bonus = dice ? diceTurn.AccumulatedBonusPermille : 0;
                captainBonus1101.text = "+" + (bonus / 10f).ToString("0.#") + "%";
                captainBonus1101.color = bonus > 0 ? (Color)new Color32(255, 214, 110, 255) : Color.white;
            }

            // hand stamp + multiplier
            if (handChip1101 != null)
            {
                bool rolling = awaitingDiceLanding || !dice;
                string hand = rolling ? null : diceTurn.Hand.DisplayName;
                Sprite stamp = hand != null ? ChoSiren.UI.DiceComboShow.StampFor(hand) : null;
                if (hand != shownHand1101)
                {
                    if (hand != null && stamp != null && shownHand1101 != null) handPopClock1101 = 0f;
                    shownHand1101 = hand;
                }
                handPopClock1101 += dt;
                handStamp1101.sprite = stamp;
                handStamp1101.enabled = stamp != null;
                handDie1101.enabled = stamp == null;
                float spin = rolling && !calm ? Time.unscaledTime * 540f : 0f;
                handDie1101.rectTransform.localEulerAngles = new Vector3(0f, 0f, -spin);
                float pop = calm ? 1f : handPopClock1101 < .1f ? Mathf.Lerp(1.6f, .92f, handPopClock1101 / .1f)
                    : handPopClock1101 < .22f ? Mathf.Lerp(.92f, 1f, (handPopClock1101 - .1f) / .12f) : 1f;
                handStamp1101.rectTransform.localScale = Vector3.one * pop;
                handStamp1101.rectTransform.localEulerAngles = new Vector3(0f, 0f, -4f);
                handMult1101.text = rolling ? string.Empty : "×" + (diceTurn.DamageMultiplierPermille / 1000f).ToString("0.##");
            }

            // wave target
            if (targetChip1101 != null)
            {
                bool has = battle.HasDiceTarget;
                targetChip1101.gameObject.SetActive(has);
                if (has)
                {
                    bool done = battle.DiceTargetDone;
                    targetStamp1101.sprite = ChoSiren.UI.DiceComboShow.StampFor(battle.DiceTargetLabel);
                    targetStamp1101.enabled = targetStamp1101.sprite != null;
                    targetStamp1101.color = done ? new Color(1f, 1f, 1f, .55f) : Color.white;
                    targetArrow1101.enabled = !done;
                    targetCheck1101.enabled = done;
                    targetFlag1101.color = done ? (Color)new Color32(255, 214, 110, 255) : (Color)Brand1025.Teal;
                    if (done && !targetWasDone1101) targetPopClock1101 = 0f;
                    targetPopClock1101 += dt;
                    float k = calm ? 1f : VisualBattle1101.OutBack(targetPopClock1101 / .3f);
                    targetCheck1101.rectTransform.localScale = Vector3.one * Mathf.Max(0f, k) * (done ? 1f : 0f);
                    targetWasDone1101 = done;
                }
            }

            // lower ribbon: heat chip; whatever text is left (e.g. dice-endless's cap) stays readable
            if (heatChip1101 != null)
            {
                int heat = dice && diceTurn.RhythmMode ? diceTurn.Heat : 0;
                heatChip1101.gameObject.SetActive(heat > 0);
                heatText1101.text = heat.ToString();
            }
            if (diceInstructionText != null)
            {
                string rest = MovedToPictures1101.Replace(diceInstructionText.text ?? string.Empty, string.Empty);
                rest = Regex.Replace(rest, @"(\s*·\s*)+", " · ").Trim().Trim('·').Trim();
                diceInstructionText.text = rest;
                diceInstructionText.enabled = rest.Length > 0;
            }

            // 应急守护 pips: lit = members it will shield (ready) or still shielded (active)
            if (guardPips1101.Count > 0 && battle.IsTurnBased)
            {
                int alive = battle.Units.Count(u => u.Side == BattleSide.Player && u.Alive);
                int guarded = battle.GuardedCount;
                bool ready = !battle.TacticalGuardUsed;
                int lit = ready ? alive : guarded;
                for (int i = 0; i < guardPips1101.Count; i++)
                {
                    bool on = i < lit;
                    guardPips1101[i].color = on ? (ready ? Color.white : (Color)new Color32(120, 245, 255, 255))
                        : new Color(.55f, .55f, .7f, .28f);
                    guardPips1101[i].gameObject.SetActive(i < Mathf.Max(alive, guarded, 1));
                }
            }

            // charge warnings: the edge light holds while any boss still charges
            bool bossCharging = false;
            Color hold = Color.red;
            foreach (var pair in chargeWarnings1101)
                if (pair.Value != null && !pair.Value.Ending && pair.Key == bossUnitId) { bossCharging = true; hold = bossChargeTint1101; }
            edgeWarning1101?.Hold(bossCharging, hold);
        }

        private Color bossChargeTint1101 = Color.red;

        /// <summary>
        /// Buff/debuff landing in the PSD battle: the status symbol pops on the unit (no floating words), 禁魔 / 封印 add a
        /// two-character tag, and a charge (rt-cast) raises the ring countdown. False = use the old text popup.
        /// </summary>
        private bool PresentStatusPicture1101(BattleEvent entry, BattleUnit actor, BattleUnit target)
        {
            if (psdRoot == null || popupLayer == null || entry == null || target == null) return false;
            string skill = entry.SkillId ?? string.Empty;
            if (skill == "rt-cast") return PresentCharge1101(target);
            if (skill == BattleSimulator.GuardBreakSkillId) return PopStatus1101(target, "icon-cyan-shield", null, 60f);
            string key = VisualBattle1101.StatusIconKey(skill);
            if (key == null) return false;
            bool seal = key == "icon-heart-lock";
            if (seal && actor != null && actor.Side == BattleSide.Enemy && battle.IsStageBoss(actor))
            {
                // 群体禁魔: one big stamp over the team, each card a silent lock.
                if (Time.unscaledTime - silenceStampAt1101 > 1.2f)
                {
                    silenceStampAt1101 = Time.unscaledTime;
                    Vector2 centre = TeamCentre1101();
                    StatusPop1101.Spawn(popupLayer, centre, BattlePsdArt928.Sprite(key), VisualBattle1101.StatusGlow(key), "禁魔",
                        kit.Font, () => paused || closing, 118f);
                    edgeWarning1101?.Flash(VisualBattle1101.IntentColor("禁魔"), 1f);
                    Sfx("stamp", .8f);
                    Sfx("debuff", 1f);
                    Shake1101(.14f, 5f);
                }
                return PopStatus1101(target, key, null, 50f);
            }
            if (target.Side == BattleSide.Enemy && seal && battle.IsStageBoss(target))
                foreach (var w in chargeWarnings1101.Values) w?.End(); // 魅语迷心 cancels the boss's charge
            float size = target.Side == BattleSide.Enemy ? 74f : 56f;
            return PopStatus1101(target, key, seal ? "封印" : null, seal ? size + 6f : size);
        }

        private bool PopStatus1101(BattleUnit target, string key, string word, float size)
        {
            Sprite symbol = BattlePsdArt928.Sprite(key);
            if (symbol == null) return false;
            Vector2 at = PopupPoint1101(target);
            StatusPop1101.Spawn(popupLayer, at, symbol, VisualBattle1101.StatusGlow(key), word, kit.Font, () => paused || closing, size);
            return true;
        }

        /// <summary>The point a status lands on: an enemy's upper body, a member's portrait.</summary>
        private Vector2 PopupPoint1101(BattleUnit unit)
        {
            RectTransform visual = unit.Side == BattleSide.Enemy ? UnitVisual(unit) : FindCell(unit)?.Rect;
            if (visual == null) return new Vector2(360f, -500f);
            return LocalTopLeft1101(popupLayer, visual, unit.Side == BattleSide.Enemy ? new Vector2(.5f, .66f) : new Vector2(.5f, .62f));
        }

        private Vector2 TeamCentre1101()
        {
            var rects = cells.Where(c => c.Side == BattleSide.Player && c.Unit != null).Select(c => c.Rect).ToList();
            if (rects.Count == 0) return new Vector2(360f, -1000f);
            Vector2 sum = Vector2.zero;
            foreach (RectTransform r in rects) sum += LocalTopLeft1101(popupLayer, r, new Vector2(.5f, .6f));
            return sum / rects.Count;
        }

        /// <summary>A point inside <paramref name="source"/> (0–1 from its bottom-left) in <paramref name="layer"/>'s top-left space.</summary>
        private static Vector2 LocalTopLeft1101(RectTransform layer, RectTransform source, Vector2 normalized)
        {
            Rect r = source.rect;
            Vector3 world = source.TransformPoint(new Vector3(r.xMin + r.width * normalized.x, r.yMin + r.height * normalized.y, 0f));
            Vector2 local = layer.InverseTransformPoint(world);
            return local - new Vector2(layer.rect.xMin, layer.rect.yMax);
        }

        /// <summary>
        /// 蓄力预警 without a sentence: the threat's symbol inside a ring that drains to the release, an edge light that
        /// flashes then throbs while a boss charges, and a warning sound. Only 禁魔 keeps its two characters.
        /// </summary>
        private bool PresentCharge1101(BattleUnit unit)
        {
            if (unit.Side != BattleSide.Enemy) return false;
            bool boss = unit.Id == bossUnitId || battle.IsStageBoss(unit);
            string intent = battle.EnemyCastIntent(unit);
            Color tint = VisualBattle1101.IntentColor(intent);
            Sprite symbol = BattlePsdArt928.Sprite(VisualBattle1101.IntentIconKey(intent)) ?? BattlePsdArt928.Sprite("icon-claw-red");
            if (chargeWarnings1101.TryGetValue(unit.Id, out ChargeWarning1101 old) && old != null) Destroy(old.gameObject);
            float initial = Mathf.Max(1, battle.CastRemaining(unit));
            int id = unit.Id;
            RectTransform layer = psdRoot;
            ChargeWarning1101 warning = ChargeWarning1101.Show(layer, kit.Font, symbol, tint,
                boss && intent == "禁魔" ? "禁魔" : null, boss ? 104f : 66f,
                () => ChargeAnchor1101(id, boss),
                () =>
                {
                    BattleUnit u = battle?.FindUnit(id);
                    return u != null && u.Alive && !closing && battle.Outcome == BattleOutcome.Ongoing ? battle.CastRemaining(u) / initial : 0f;
                },
                () => paused || closing);
            chargeWarnings1101[id] = warning;
            if (boss)
            {
                bossChargeTint1101 = tint;
                edgeWarning1101?.Flash(tint, 1f);
                Sfx("glitch", .85f);
                Shake1101(.12f, 4f);
            }
            else Sfx("glitch", .45f);
            return true;
        }

        private Vector2 ChargeAnchor1101(int unitId, bool boss)
        {
            BattleUnit unit = battle?.FindUnit(unitId);
            RectTransform visual = unit != null ? UnitVisual(unit) : null;
            if (visual == null || psdRoot == null) return new Vector2(360f, -420f);
            // Beside the boss's shoulder (clear of her face, the name plate and the taunt bubble); over a minion's chest.
            return boss ? LocalTopLeft1101(psdRoot, visual, new Vector2(.86f, .66f))
                : LocalTopLeft1101(psdRoot, visual, new Vector2(.5f, .58f));
        }

        private void Shake1101(float seconds, float strength)
        {
            if (BattlePresentationOptions.ReduceMotion || stageShake != null || !isActiveAndEnabled) return;
            stageShake = StartCoroutine(ShakeStage(seconds, strength));
        }

        /// <summary>Target reached: the check seals the chip with a chime (the 队长演出 banner still plays).</summary>
        private void PresentTargetReached1101()
        {
            if (psdRoot == null) return;
            Sfx("orb-chime", .8f);
        }

        // ------------------------------------------------------------------ pause / rules / defeat

        /// <summary>Pause card: icon + two-character actions, a short title and a five-character stamina note.</summary>
        private void RestylePause1101()
        {
            Transform dialog = pauseOverlay != null ? pauseOverlay.transform.Find("PauseDialog") : null;
            if (dialog == null) return;
            Text title = dialog.Find("PauseTitle")?.GetComponent<Text>();
            if (title != null) title.text = "暂停";
            Text hint = dialog.Find("PauseHint")?.GetComponent<Text>();
            if (hint != null)
            {
                hint.text = "体力不返还";
                PanelKit.PlaceTop(hint.rectTransform, 30, 232, 320, 22);
                hint.fontSize = 13;
            }
            Decorate1101(dialog.Find("ResumeBattle"), "继续", "arrow", 32f, false);
            Decorate1101(dialog.Find("BattleExit"), "退出", "login", 28f, true);
            Decorate1101(dialog.Find("BattleRulesEntry"), "规则", "help", 24f, false);
            Decorate1101(dialog.Find("PsdPauseAbilities"), "能力", "star", 24f, false);
            // Re-flow: title, resume, exit + its note, then the two small entries.
            Place1101(dialog.Find("ResumeBattle"), 30, 82, 320, 64);
            Place1101(dialog.Find("BattleExit"), 30, 160, 320, 54);
            Place1101(dialog.Find("BattleRulesEntry"), 30, 248, 152, 42);
            Place1101(dialog.Find("PsdPauseAbilities"), 198, 248, 152, 42);
            if (title != null) PanelKit.PlaceTop(title.rectTransform, 20, 18, 340, 50);
            if (title != null) title.fontSize = 32;
        }

        private static void Place1101(Transform t, float x, float y, float w, float h)
        {
            if (t is RectTransform r) PanelKit.PlaceTop(r, x, y, w, h);
        }

        /// <summary>Puts an icon before a button's (shortened) label.</summary>
        private void Decorate1101(Transform button, string label, string icon, float size, bool mirror)
        {
            if (button == null) return;
            Text text = PanelKit.LabelOf(button.gameObject);
            if (text == null) return;
            text.text = label;
            Sprite sprite = ChoSiren.UI.VisualA1101.Icon(icon);
            if (sprite == null) return;
            Image glyph = VisualBattle1101.NewImage("Icon1101", text.transform.parent, sprite, text.color);
            glyph.rectTransform.sizeDelta = new Vector2(size, size);
            if (mirror) glyph.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            var follow = glyph.gameObject.AddComponent<IconBesideLabel1101>();
            follow.Bind(text, glyph, size);
        }

        /// <summary>
        /// Rules: four picture tiles first (the same art the battle shows) — 骰子 / 大招 / 队长 / 守护 — and the short
        /// rule lines below them, scrolling, for whoever wants the detail.
        /// </summary>
        private void BuildRulesPictures1101()
        {
            Transform card = rulesOverlay != null ? rulesOverlay.transform.Find("RulesCard") : null;
            RectTransform scroll = card != null ? card.Find("RulesScroll") as RectTransform : null;
            if (card == null || scroll == null) return;
            Text title = card.Find("RulesTitle")?.GetComponent<Text>();
            if (title != null) title.text = "规则";
            RectTransform strip = kit.NewRect("RulesPictures1101", card);
            PanelKit.PlaceTop(strip, 34, 70, 572, 214);
            const float w = 134f, gap = 12.6f;
            Tile1101(strip, 0, w, gap, "骰子", RulesDice1101);
            Tile1101(strip, 1, w, gap, "大招", RulesUltimate1101);
            Tile1101(strip, 2, w, gap, "队长", RulesCaptain1101);
            Tile1101(strip, 3, w, gap, "守护", RulesGuard1101);
            PanelKit.PlaceTop(scroll, 34, 296, 572, 556);
        }

        private void Tile1101(RectTransform strip, int index, float w, float gap, string name, System.Action<RectTransform> art)
        {
            RectTransform tile = kit.NewRect("RulesTile1101-" + index, strip);
            PanelKit.PlaceTop(tile, index * (w + gap), 0, w, 214);
            Image plate = kit.NewImage("TilePlate", tile, kit.RoundedSprite(14), new Color32(22, 12, 40, 255));
            PanelKit.Stretch(plate.rectTransform); plate.raycastTarget = false;
            var rim = plate.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(Brand1025.Teal.r / 255f, Brand1025.Teal.g / 255f, Brand1025.Teal.b / 255f, .45f);
            rim.effectDistance = new Vector2(1.5f, -1.5f);
            RectTransform picture = kit.NewRect("Picture", tile);
            PanelKit.PlaceTop(picture, 6, 8, w - 12, 150);
            art(picture);
            RectTransform tag = kit.NewRect("Tag", tile);
            PanelKit.PlaceTop(tag, 14, 168, w - 28, 34);
            P5Letters1023.Plate(tag, "Shadow", new Vector2(3f, -3f), new Vector2(w - 28, 34), Brand1025.Ink, -14f);
            P5Letters1023.Plate(tag, "Plate", Vector2.zero, new Vector2(w - 28, 34), Brand1025.Teal, -14f);
            Text t = VisualBattle1101.NewText("Name", tag, kit.Font, name, 20, Brand1025.Ink);
            t.GetComponent<Outline>().enabled = false;
            VisualBattle1101.Place(t.rectTransform, Vector2.zero, new Vector2(w - 28, 34));
        }

        private Image Pic1101(RectTransform parent, string name, Sprite sprite, Vector2 centre, Vector2 size, float angle = 0f)
        {
            Image image = VisualBattle1101.NewImage(name, parent, sprite, Color.white);
            VisualBattle1101.Place(image.rectTransform, centre, size);
            image.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
            return image;
        }

        private void Arrow1101(RectTransform parent, Vector2 centre, float angle, float size = 20f)
        {
            Sprite arrow = ChoSiren.UI.VisualA1101.Icon("arrow");
            if (arrow == null) return;
            Image a = Pic1101(parent, "Arrow", arrow, centre, Vector2.one * size, angle);
            a.color = Brand1025.Teal;
        }

        /// <summary>Two dice → the hand stamp → the captain hits harder.</summary>
        private void RulesDice1101(RectTransform p)
        {
            Pic1101(p, "DieA", userDiceFaceSprites[2] ?? BattlePsdArt928.Sprite("die-3"), new Vector2(-36f, 40f), new Vector2(40f, 46f), -8f);
            Pic1101(p, "DieB", userDiceFaceSprites[2] ?? BattlePsdArt928.Sprite("die-3"), new Vector2(0f, 46f), new Vector2(40f, 46f), 6f);
            Pic1101(p, "DieC", userDiceFaceSprites[2] ?? BattlePsdArt928.Sprite("die-3"), new Vector2(36f, 40f), new Vector2(40f, 46f), -3f);
            Arrow1101(p, new Vector2(0f, 2f), -90f, 18f);
            Pic1101(p, "Stamp", ChoSiren.UI.DiceComboShow.StampFor("三条"), new Vector2(-14f, -40f), new Vector2(92f, 42f), -5f);
            Text up = VisualBattle1101.NewText("Up", p, kit.Font, "↑", 26, new Color32(255, 214, 110, 255));
            VisualBattle1101.Place(up.rectTransform, new Vector2(46f, -40f), new Vector2(24f, 30f));
        }

        /// <summary>The record lifting out of the sleeve.</summary>
        private void RulesUltimate1101(RectTransform p)
        {
            Image disc = Pic1101(p, "Disc", BattlePsdArt928.Sprite("album-disc"), new Vector2(0f, 6f), new Vector2(104f, 92f));
            disc.color = new Color(1f, .92f, 1f, 1f);
            Image glow = VisualBattle1101.NewImage("Glow", p, BattleFxArt1021.Dot, new Color(1f, .45f, .85f, .55f));
            glow.preserveAspect = false;
            VisualBattle1101.Place(glow.rectTransform, new Vector2(0f, 6f), new Vector2(150f, 140f));
            glow.transform.SetAsFirstSibling();
            Arrow1101(p, new Vector2(0f, -50f), 90f, 26f);
            Pic1101(p, "Burst", BattlePsdArt928.Sprite("icon-star-burst"), new Vector2(40f, 44f), new Vector2(36f, 36f));
        }

        /// <summary>The 队长 plate and a swap arrow between two race emblems.</summary>
        private void RulesCaptain1101(RectTransform p)
        {
            Pic1101(p, "Plate", BattlePsdArt928.Sprite("captain-plate"), new Vector2(0f, 38f), new Vector2(110f, 42f));
            Text label = VisualBattle1101.NewText("PlateLabel", p, kit.Font, "队长", 18, Color.white);
            VisualBattle1101.Place(label.rectTransform, new Vector2(-8f, 40f), new Vector2(60f, 26f));
            Pic1101(p, "RaceA", VisualBattle1101.RaceIcon(CombatRace.Charm), new Vector2(-34f, -30f), new Vector2(44f, 44f));
            Pic1101(p, "RaceB", VisualBattle1101.RaceIcon(CombatRace.Mermaid), new Vector2(34f, -30f), new Vector2(44f, 44f));
            Sprite swap = ChoSiren.UI.VisualA1101.Icon("exchange");
            if (swap != null) Pic1101(p, "Swap", swap, new Vector2(0f, -30f), new Vector2(22f, 22f)).color = Brand1025.Teal;
        }

        /// <summary>The guard badge over four shields.</summary>
        private void RulesGuard1101(RectTransform p)
        {
            Pic1101(p, "Badge", BattlePsdArt928.Sprite("guard-badge"), new Vector2(0f, 14f), new Vector2(98f, 106f));
            for (int i = 0; i < 4; i++)
                Pic1101(p, "Shield-" + i, BattlePsdArt928.Sprite("icon-cyan-shield"), new Vector2((i - 1.5f) * 24f, -54f), new Vector2(22f, 22f));
        }

        /// <summary>Defeat: the four ways back as icon + two characters.</summary>
        private void RestyleRecovery1101()
        {
            if (recoveryActions == null) return;
            string[] routes = { "training", "accessory", "team", "farm" };
            string[] labels = { "升级", "装备", "编队", "返回" };
            string[] icons = { "dumbbell", "shield", "team", "flag" };
            for (int i = 0; i < routes.Length; i++)
            {
                Transform action = recoveryActions.transform.Find("Recovery-" + routes[i]);
                if (action == null || action.Find("Icon1101") != null) continue;
                Decorate1101(action, labels[i], icons[i], 32f, false);
            }
        }
    }

    /// <summary>Keeps an icon just left of its label's actual text (labels are centred and may change).</summary>
    internal sealed class IconBesideLabel1101 : MonoBehaviour
    {
        const float Gap = 8f;
        Text label; Image icon; float size; bool shifted;

        public void Bind(Text text, Image glyph, float iconSize)
        {
            label = text; icon = glyph; size = iconSize;
            var r = icon.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            Apply();
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (label == null || icon == null) return;
            RectTransform lr = label.rectTransform;
            if (!shifted)
            {
                // make room for the icon: the label's centre moves right by half the icon + gap
                if (lr.anchorMin.x != lr.anchorMax.x) lr.offsetMin = new Vector2(lr.offsetMin.x + size + Gap, lr.offsetMin.y);
                else lr.anchoredPosition += new Vector2((size + Gap) * .5f, 0f);
                shifted = true;
            }
            Vector2 centre = (Vector2)lr.localPosition + lr.rect.center;
            float textWidth = Mathf.Min(label.preferredWidth, lr.rect.width);
            float x = label.alignment == TextAnchor.MiddleLeft ? lr.localPosition.x + lr.rect.xMin : centre.x - textWidth * .5f;
            icon.rectTransform.localPosition = new Vector3(x - Gap - size * .5f, centre.y, 0f);
            Color c = label.color;
            icon.color = new Color(c.r, c.g, c.b, icon.color.a);
            if (icon.transform.GetSiblingIndex() != icon.transform.parent.childCount - 1) icon.transform.SetAsLastSibling();
        }
    }
}
