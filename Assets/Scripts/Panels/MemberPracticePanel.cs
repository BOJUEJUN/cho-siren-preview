using System;
using System.Collections;
using System.Collections.Generic;
using ChoSiren.Systems.Presentation;
using UnityEngine;
using UnityEngine.UI;
using ChoSiren.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 练习室 (2026-11-01 rebuild). A stage of every owned member on top — tap a figure to pick the
    /// trainee — then the trainee's card (bust, level bar, power, 声能/律动/气场/共鸣 radar + bars), the
    /// career practice item with its gold quote, one big 开始练习 and a settlement card that rolls the
    /// before → after numbers. The economy is unchanged: one tap commits the same gold-only level-up
    /// (GameModel.Train); reduce-motion keeps the same information with short fades and no loops.
    /// </summary>
    public sealed partial class MemberPracticePanel : MonoBehaviour
    {
        private const string ReduceMotionKey = MemberTrainingPractice.ReduceMotionPreferenceKey;

        private PanelKit kit;
        private GameModel model;
        private int memberIndex;
        private Action onFinished;
        private Action<string> onMessage;

        private bool reduceMotion;
        private bool animating;
        private int quoteGold;
        // 2026-11-04: the cost header and the 只扣星光币 line switch to the breakthrough's materials.
        private Text costLabel, noDiamondLabel;
        private MemberPracticePlan plan;
        private MemberPracticeOutcome lastOutcome;
        private Coroutine swapRoutine;

        public bool ReduceMotion => reduceMotion;
        public MemberPracticeOutcome LastOutcome => lastOutcome;
        public string LastHeadline => lastOutcome != null ? lastOutcome.Headline : string.Empty;
        public string LastCostLine => resultCost != null ? resultCost.text : string.Empty;
        public bool IsAnimating => animating;
        /// <summary>The member currently being trained (changes when a stage figure is tapped).</summary>
        public int MemberIndex => memberIndex;
        public PracticeVideoStage1107 Stage => stage;
        public bool ResultVisible => resultGroup != null && resultGroup.activeSelf;

        public static MemberPracticePanel Open(Transform host, GameModel gameModel, int member,
            Action finished = null, Action<string> message = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));
            if (member < 0 || member >= GameModel.Members.Length)
                throw new ArgumentOutOfRangeException(nameof(member));

            MemberPracticePanel existing = host.GetComponentInChildren<MemberPracticePanel>(true);
            if (existing != null) DestroyPanelObject(existing.gameObject);

            GameObject root = PanelKit.CreateOverlayRoot("MemberPracticePanel", host);
            MemberPracticePanel panel = root.AddComponent<MemberPracticePanel>();
            panel.Initialize(gameModel, member, finished, message);
            return panel;
        }

        private void Initialize(GameModel gameModel, int member, Action finished, Action<string> message)
        {
            model = gameModel;
            memberIndex = member;
            onFinished = finished;
            onMessage = message;
            reduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
            kit = new PanelKit("MemberPractice");
            BuildPage();
            RefreshMember();
            Relayout();
        }

        private void OnDestroy()
        {
            if (kit != null) kit.Dispose();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (page != null) Relayout();
        }

        // ---------------------------------------------------------------- state

        /// <summary>
        /// Switches the trainee. Called by a tap on a stage figure; refuses while a practice run is
        /// playing so the committed level-up and its animation always belong to the same member.
        /// </summary>
        /// <summary>‹ / ›: the previous or next owned member (team order first), wrapping around.</summary>
        public bool StepMember(int direction)
        {
            if (animating || stage == null || stage.Roster.Count < 2) return false;
            var roster = stage.Roster;
            int at = 0;
            for (int i = 0; i < roster.Count; i++) if (roster[i] == memberIndex) at = i;
            for (int step = 1; step < roster.Count; step++)
            {
                int candidate = roster[((at + direction * step) % roster.Count + roster.Count) % roster.Count];
                if (model.IsUnlocked(candidate)) return SelectMember(candidate);
            }
            return false;
        }

        /// <summary>A tap on a lead in the camera view; a lead not yet signed says so.</summary>
        private void PickFromStage(int member)
        {
            if (member >= 0 && !model.IsUnlocked(member)) { Notify($"{GameModel.Members[member].Name} 尚未签约"); return; }
            SelectMember(member);
        }

        public bool SelectMember(int member)
        {
            if (animating || member < 0 || member >= GameModel.Members.Length || !model.IsUnlocked(member))
                return false;
            if (member == memberIndex)
            {
                stage?.SetSelected(member);
                return true;
            }

            memberIndex = member;
            lastOutcome = null;
            HideResult();
            stage?.SetSelected(member, reduceMotion);
            RefreshMember();
            kit.PlayClick();
            if (!reduceMotion && !BattlePresentationOptions.ReduceMotion && Application.isPlaying)
            {
                if (swapRoutine != null) StopCoroutine(swapRoutine);
                swapRoutine = StartCoroutine(SwapCardIn());
            }
            return true;
        }

        private void RefreshMember()
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            int level = model.LevelOf(memberIndex);
            Sprite art = Resources.Load<Sprite>(member.ResourcePath);
            if (portrait != null)
            {
                portrait.sprite = art;
                portrait.enabled = art != null;
                PanelKit.FrameBustPortrait(portrait, portraitWindow, member.Id);
            }

            memberName.text = member.Name;
            rarityText.text = string.IsNullOrEmpty(member.Rarity) ? "R" : member.Rarity;
            rarityText.color = member.Rarity == "SSR" ? new Color32(255, 150, 52, 255)
                : member.Rarity == "SR" ? new Color32(205, 128, 255, 255) : (Color)SirenStickerUi.Cyan;
            string race = RaceFamily(member);
            Sprite raceIcon = RaceIcon(race);
            raceBadge.sprite = raceIcon;
            raceBadge.enabled = raceIcon != null;
            bool captain = model.Save.Team.Count > 0 && model.Save.Team[0] == memberIndex;
            string team = captain ? " · 队长" : model.IsInTeam(memberIndex) ? " · 出战中" : string.Empty;
            memberMeta.text = $"{(string.IsNullOrEmpty(race) ? "未知种族" : race)} · {member.Career}{team}";
            careerChipText.text = member.Career;
            int cap = model.LevelCapOf1104(memberIndex);
            levelText.text = $"<size=20>LV.</size>{level}<size=20><color=#B7AECF> / {cap}</color></size>";
            float progress = Mathf.Clamp01(level / (float)Mathf.Max(1, cap));
            levelFill.fillAmount = progress;
            SecondaryTasks038Chrome.SetProgressMarker(levelMarker, LevelBarX, LevelBarY, LevelBarW, LevelBarH, progress,
                SirenStickerUi.Cyan);
            levelCaption.text = level >= GameModel.MaxMemberLevel ? "已满级"
                : level < cap ? $"本阶段还能升 {cap - level} 级"
                : model.NeedsBreakthrough1104(memberIndex) ? "可以突破"
                : model.LevelCapReason1104(memberIndex);
            powerText.text = model.PowerOf(memberIndex).ToString("N0");

            ReadStats(memberIndex, level, out int vocal, out int rhythm, out int presence, out int resonance);
            int[] values = { vocal, rhythm, presence, resonance };
            for (int i = 0; i < 4; i++)
            {
                statValues[i].text = values[i].ToString();
                statFills[i].fillAmount = Mathf.Clamp01(values[i] / (float)GameModel.StageStatMax);
            }

            radar.SetValues(RadarScale(vocal), RadarScale(rhythm), RadarScale(presence), RadarScale(resonance));
            RefreshQuote();
            stage?.RefreshLabels();
        }

        private void RefreshQuote()
        {
            if (model.NeedsBreakthrough1104(memberIndex) && !animating)
            {
                RefreshBreakthroughQuote1104();
                return;
            }
            if (costLabel != null) costLabel.text = "单次消耗";
            specialtyChip.gameObject.SetActive(true);
            int remnants = GameModel.RemnantCostAtLevel1107(model.LevelOf(memberIndex));
            if (noDiamondLabel != null)
            {
                noDiamondLabel.text = remnants > 0 ? $"另需敌人残魂 {model.Save.VoiceCrystals}/{remnants} · 不消耗星钻" : "只扣星光币 · 不消耗星钻";
                noDiamondLabel.color = remnants > model.Save.VoiceCrystals ? new Color32(255, 150, 120, 255) : (Color)SirenStickerUi.Muted;
            }
            bool canTrain = model.CanTrain(memberIndex, out quoteGold, out string reason);
            MemberDefinition member = GameModel.Members[memberIndex];
            plan = MemberTrainingPractice.BuildPlan(member.Career, quoteGold, reduceMotion);
            actionText.text = plan.ActionName;
            focusText.text = $"{member.Career}专长 · {plan.FocusLabel}";
            projectGlyph.sprite = ReferencePunkFx.StatGlyph(GlyphFor(plan.ActionName));
            float chipX = ActionX + Mathf.Min(actionText.preferredWidth, 250f) + 12f;
            PanelKit.PlaceTop(specialtyChip.rectTransform, chipX, 52f, 62f, 28f);

            bool showCost = canTrain || quoteGold > 0;
            quoteText.text = showCost ? quoteGold.ToString("N0") : reason;
            quoteText.fontSize = showCost ? 34 : 20;
            quoteText.resizeTextMaxSize = quoteText.fontSize;
            quoteText.alignment = showCost ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            if (showCost) PanelKit.PlaceTop(quoteText.rectTransform, 534f, 40f, 126f, 54f);
            else PanelKit.PlaceTop(quoteText.rectTransform, 482f, 38f, 176f, 62f);
            quoteText.color = canTrain ? PanelKit.Gold : new Color32(255, 190, 92, 255);
            quoteIcon.enabled = showCost;
            goldOwned.text = $"持有 {model.Save.Gold:N0}";
            goldOwned.color = canTrain || !showCost ? SirenStickerUi.Muted : new Color32(255, 150, 120, 255);
            goldChipValue.text = model.Save.Gold.ToString("N0");
            if (plan.DiamondCost != 0) quoteText.text += " · 异常：出现星钻消耗";

            string startText = animating ? "练习中…" : canTrain ? (lastOutcome == null ? "开始练习" : "再练一次")
                : model.LevelOf(memberIndex) >= GameModel.MaxMemberLevel ? "已满级"
                : model.LevelOf(memberIndex) >= model.LevelCapOf1104(memberIndex) ? model.LevelCapReason1104(memberIndex)
                : reason.StartsWith("敌人残魂", StringComparison.Ordinal) ? "残魂不足"
                : "星光币不足";
            if (animating) canTrain = false;
            SecondaryTasks038Chrome.SetState(startObject, canTrain,
                canTrain ? (Color)Brand1025.Teal : SecondaryTasks038Chrome.EdgeDisabled,
                canTrain ? (Color)Brand1025.Teal : SecondaryTasks038Chrome.InsetDisabled,
                canTrain ? (Color)Brand1025.Ink : PanelKit.White, startText);
            if (againObject != null)
                SecondaryTasks038Chrome.SetState(againObject, canTrain, SecondaryTasks038Chrome.EdgeViolet,
                    SecondaryTasks038Chrome.InsetViolet, canTrain ? PanelKit.White : SirenStickerUi.Muted,
                    canTrain ? "再练一次" : startText);
        }

        /// <summary>At a breakthrough level the cost panel shows gold plus the materials owned / needed.</summary>
        private void RefreshBreakthroughQuote1104()
        {
            bool can = model.CanBreakthrough1104(memberIndex, out GameModel.BreakCost1104 cost, out string reason);
            quoteGold = cost.Gold;
            int nextCap = Mathf.Min(model.ProgressLevelCap1104, model.BreakLevelOf1104(memberIndex) + GameModel.BreakSpacing1104);
            actionText.text = "等级突破";
            focusText.text = $"突破后等级上限 Lv{nextCap}";
            specialtyChip.gameObject.SetActive(false);
            if (costLabel != null) costLabel.text = "突破消耗";
            quoteText.text = cost.Gold.ToString("N0");
            quoteText.fontSize = 34;
            quoteText.resizeTextMaxSize = 34;
            quoteText.alignment = TextAnchor.MiddleLeft;
            PanelKit.PlaceTop(quoteText.rectTransform, 534f, 40f, 126f, 54f);
            quoteText.color = model.Save.Gold >= cost.Gold ? PanelKit.Gold : new Color32(255, 190, 92, 255);
            quoteIcon.enabled = true;
            goldOwned.text = $"持有 {model.Save.Gold:N0}";
            goldOwned.color = model.Save.Gold >= cost.Gold ? SirenStickerUi.Muted : new Color32(255, 150, 120, 255);
            goldChipValue.text = model.Save.Gold.ToString("N0");
            var parts = new System.Collections.Generic.List<string>();
            parts.Add($"{GameModel.CurrencyName(cost.SoulId)} {model.Balance(cost.SoulId)}/{cost.Souls}");
            if (model.Save.VoiceCores > 0) parts.Add($"通用生魂 {model.Save.VoiceCores}");
            if (noDiamondLabel != null)
            {
                noDiamondLabel.text = "突破材料：" + string.Join(" · ", parts);
                noDiamondLabel.color = can || reason.StartsWith("星光币", StringComparison.Ordinal)
                    ? (Color)SirenStickerUi.Cyan : new Color32(255, 150, 120, 255);
            }
            string label = can ? "突破" : reason.StartsWith("星光币", StringComparison.Ordinal) ? "星光币不足" : "材料不足";
            SecondaryTasks038Chrome.SetState(startObject, can,
                can ? (Color)Brand1025.Teal : SecondaryTasks038Chrome.EdgeDisabled,
                can ? (Color)Brand1025.Teal : SecondaryTasks038Chrome.InsetDisabled,
                can ? (Color)Brand1025.Ink : PanelKit.White, label);
            if (againObject != null)
                SecondaryTasks038Chrome.SetState(againObject, false, SecondaryTasks038Chrome.EdgeViolet,
                    SecondaryTasks038Chrome.InsetViolet, SirenStickerUi.Muted, label);
        }

        private void RefreshReduceMotionLabel()
        {
            if (reduceMotionLabel != null)
                reduceMotionLabel.text = reduceMotion
                    ? "减弱动画：开"
                    : "减弱动画：关";
            SecondaryTasks038Chrome.SetState(motionObject, true,
                reduceMotion ? SecondaryTasks038Chrome.EdgeCyan : SecondaryTasks038Chrome.EdgeViolet,
                reduceMotion ? SecondaryTasks038Chrome.InsetCyan : SecondaryTasks038Chrome.InsetViolet,
                PanelKit.White);
        }

        public void ToggleReduceMotion()
        {
            reduceMotion = !reduceMotion;
            PlayerPrefs.SetInt(ReduceMotionKey, reduceMotion ? 1 : 0);
            PlayerPrefs.Save();
            stage?.SetPracticing(false, reduceMotion);
            RefreshReduceMotionLabel();
            RefreshQuote();
        }

        /// <summary>
        /// Commits exactly one level-up through the existing gold-only API and builds the
        /// before/after outcome. Public so EditMode tests can verify economy and copy without
        /// waiting for the coroutine.
        /// </summary>
        public bool CommitTraining()
        {
            if (animating) return false;
            if (model.NeedsBreakthrough1104(memberIndex))
            {
                bool done = model.Breakthrough1104(memberIndex, out string breakMessage);
                Notify(breakMessage);
                if (done) ModalPolish038Chrome.PlaySuccess();
                RefreshMember();
                return false;
            }
            if (!model.CanTrain(memberIndex, out int cost, out string reason))
            {
                Notify(reason);
                RefreshQuote();
                return false;
            }

            int levelBefore = model.LevelOf(memberIndex);
            int powerBefore = model.PowerOf(memberIndex);
            ReadStats(memberIndex, levelBefore, out int vocalBefore, out int rhythmBefore,
                out int presenceBefore, out int resonanceBefore);

            if (!model.Train(memberIndex, out string message))
            {
                Notify(message);
                RefreshQuote();
                return false;
            }

            int levelAfter = model.LevelOf(memberIndex);
            int powerAfter = model.PowerOf(memberIndex);
            ReadStats(memberIndex, levelAfter, out int vocalAfter, out int rhythmAfter,
                out int presenceAfter, out int resonanceAfter);
            lastOutcome = MemberTrainingPractice.BuildOutcome(levelBefore, levelAfter, powerBefore, powerAfter,
                vocalBefore, vocalAfter, rhythmBefore, rhythmAfter, presenceBefore, presenceAfter,
                resonanceBefore, resonanceAfter, cost);

            // The settlement card states the level-up, so the success toast would only cover the page.
            FillOutcome(lastOutcome);
            ModalPolish038Chrome.PlaySuccess();
            RefreshMember();
            return true;
        }

        public void StartPractice()
        {
            if (animating) return;
            HideResult();
            if (!CommitTraining()) return;
            if (!Application.isPlaying)
            {
                ShowResultFinal();
                return;
            }

            StartCoroutine(PlayPracticeAnimation());
        }

        private IEnumerator PlayPracticeAnimation()
        {
            animating = true;
            stage?.SetPracticing(true, reduceMotion);
            RefreshQuote();
            float duration = plan != null ? plan.DurationSeconds : MemberTrainingPractice.StandardDurationSeconds;
            Vector3 baseScale = Vector3.one;
            float elapsed = 0f;
            skipRequested1107 = false;
            if (skipButton1107 != null) { skipButton1107.SetActive(true); skipButton1107.transform.SetAsLastSibling(); }
            while (elapsed < duration && !skipRequested1107)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                if (portraitHolder != null)
                {
                    float pulse = 1f + Mathf.Sin(t * Mathf.PI * (reduceMotion ? 1f : 3f)) * (reduceMotion ? .015f : .045f);
                    portraitHolder.localScale = baseScale * pulse;
                }

                if (portraitGlow != null)
                    portraitGlow.color = new Color(.38f, .95f, 1f, .2f + .45f * Mathf.Sin(t * Mathf.PI));

                for (int index = 0; index < rings.Count; index++)
                {
                    Image ring = rings[index];
                    if (ring == null) continue;
                    float local = Mathf.Repeat(t * (reduceMotion ? 1f : 2f) - index * .22f, 1f);
                    ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(.55f, 1.45f, local);
                    ring.color = new Color(0.47f, 0.95f, 1f, Mathf.Clamp01(1f - local) * (reduceMotion ? .3f : .75f));
                }

                if (vinyl != null && !reduceMotion) vinyl.localRotation = Quaternion.Euler(0f, 0f, -elapsed * 540f);
                if (actionText != null)
                {
                    float flash = .75f + .25f * Mathf.Sin(t * Mathf.PI * 6f);
                    actionText.color = Color.Lerp(SirenStickerUi.Paper, SirenStickerUi.Cyan, flash);
                }

                yield return null;
            }

            if (portraitHolder != null) portraitHolder.localScale = baseScale;
            if (portraitGlow != null) portraitGlow.color = new Color(.38f, .95f, 1f, .18f);
            for (int index = 0; index < rings.Count; index++)
                if (rings[index] != null) rings[index].color = new Color(0.47f, 0.95f, 1f, 0f);
            if (actionText != null) actionText.color = SirenStickerUi.Paper;

            animating = false;
            stage?.SetPracticing(false, reduceMotion);
            RefreshQuote();
            if (lastOutcome != null)
            {
                // 2026-11-07 会议 (跳过动画): a skip lands straight on the settled result card.
                if (skipRequested1107) ShowResultFinal();
                else RevealResult();
            }
            if (skipButton1107 != null) skipButton1107.SetActive(skipRequested1107 ? false : resultRoutine != null);
        }

        private bool skipRequested1107;
        private GameObject skipButton1107;

        /// <summary>The small 跳过 ›› chip in the stage corner: ends the practice run's animation (and the roll-in).</summary>
        public void SkipAnimation1107()
        {
            skipRequested1107 = true;
            if (!animating && resultRoutine != null)
            {
                StopCoroutine(resultRoutine);
                resultRoutine = null;
                ShowResultFinal();
            }
            if (!animating && skipButton1107 != null) skipButton1107.SetActive(false);
        }

        private IEnumerator SwapCardIn()
        {
            if (memberCard == null) yield break;
            CanvasGroup group = UnityNull.Real(memberCard.GetComponent<CanvasGroup>()) ?? memberCard.gameObject.AddComponent<CanvasGroup>();
            float t = 0f;
            Vector2 home = memberCardHome;
            while (t < .22f && memberCard != null)
            {
                t += Time.unscaledDeltaTime;
                float k = PracticeFx1101.EaseOutCubic(t / .22f);
                group.alpha = Mathf.Lerp(.25f, 1f, k);
                memberCard.anchoredPosition = home + new Vector2(Mathf.Lerp(26f, 0f, k), 0f);
                yield return null;
            }

            if (memberCard == null) yield break;
            group.alpha = 1f;
            memberCard.anchoredPosition = home;
            swapRoutine = null;
        }

        private void Update()
        {
            UpdateResultAmbient();
            if (vinyl == null || animating || reduceMotion || BattlePresentationOptions.ReduceMotion) return;
            vinyl.localRotation = Quaternion.Euler(0f, 0f, vinyl.localEulerAngles.z - Time.unscaledDeltaTime * 30f);
        }

        private void ReadStats(int member, int level, out int vocal, out int rhythm,
            out int presence, out int resonance)
        {
            GameModel.StageStats(GameModel.Members[member], member, model.SigningChannelOf(member) == 0 ? 0 : 1, level,
                out vocal, out rhythm, out presence, out resonance, out _);
        }

        /// <summary>Signing floors start at 30, so the diamond spans 30–100 to keep the shape readable.</summary>
        private static float RadarScale(int value) => Mathf.Clamp01((value - 30f) / (GameModel.StageStatMax - 30f));

        private static string RaceFamily(MemberDefinition member)
        {
            string race = member?.Race ?? string.Empty;
            int dot = race.IndexOf('·');
            return (dot >= 0 ? race.Substring(0, dot) : race).Trim();
        }

        private static Sprite RaceIcon(string race)
        {
            string key = race == "魅族" ? "charm" : race == "魔族" ? "demon" : race == "海灵族" ? "sea"
                : race == "血精灵" ? "blood" : null;
            return key == null ? null : ReferenceArt038.Load("Art/Reference038/race-" + key);
        }

        private static ReferencePunkFx.StatGlyphKind GlyphFor(string action)
        {
            switch (action)
            {
                case MemberTrainingPractice.VocalPractice: return ReferencePunkFx.StatGlyphKind.Mic;
                case MemberTrainingPractice.DancePractice: return ReferencePunkFx.StatGlyphKind.Note;
                case MemberTrainingPractice.RapPractice: return ReferencePunkFx.StatGlyphKind.Wave;
                case MemberTrainingPractice.FacePractice: return ReferencePunkFx.StatGlyphKind.Gem;
                default: return ReferencePunkFx.StatGlyphKind.Spark;
            }
        }

        private void Close()
        {
            if (animating) return;
            Action finished = onFinished;
            onFinished = null;
            DestroyPanelObject(gameObject);
            finished?.Invoke();
        }

        private static void DestroyPanelObject(GameObject target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void Notify(string message)
        {
            if (!string.IsNullOrEmpty(message)) onMessage?.Invoke(message);
        }
    }
}
