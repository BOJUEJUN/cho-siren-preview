using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChoSiren
{
    public enum StoryBattleAction
    {
        Vocal,
        Dance,
        Support,
        Ultimate,
    }

    public readonly struct StoryBattleTurnResult
    {
        public readonly bool Accepted;
        public readonly int BossDamage;
        public readonly int EnemyDamage;
        public readonly string Message;

        public StoryBattleTurnResult(bool accepted, int bossDamage, int enemyDamage, string message)
        {
            Accepted = accepted;
            BossDamage = bossDamage;
            EnemyDamage = enemyDamage;
            Message = message;
        }
    }

    /// <summary>Deterministic battle rules separated from the UI so the whole encounter is testable.</summary>
    public sealed class StoryBattleState
    {
        public int Stage { get; }
        public int TeamPower { get; }
        public int MaxPlayerHp { get; }
        public int MaxBossHp { get; }
        public int PlayerHp { get; private set; }
        public int BossHp { get; private set; }
        public int Resonance { get; private set; }
        public int Combo { get; private set; }
        public int Shield { get; private set; }
        public int Turn { get; private set; }
        public int TotalDamage { get; private set; }
        public bool Victory => BossHp <= 0;
        public bool Defeat => PlayerHp <= 0;
        public bool Finished => Victory || Defeat;

        public StoryBattleState(int stage, int teamPower)
        {
            Stage = Mathf.Clamp(stage, 3, 6);
            TeamPower = Mathf.Max(0, teamPower);
            MaxPlayerHp = Mathf.Clamp(100 + TeamPower / 8000, 100, 125);
            MaxBossHp = 110 + (Stage - 3) * 14;
            PlayerHp = MaxPlayerHp;
            BossHp = MaxBossHp;
        }

        public StoryBattleTurnResult TakeAction(StoryBattleAction action)
        {
            if (Finished) return new StoryBattleTurnResult(false, 0, 0, "战斗已经结束");
            if (action == StoryBattleAction.Ultimate && Resonance < 3)
                return new StoryBattleTurnResult(false, 0, 0, "共鸣不足，需要先积累 3 点共鸣");

            Turn++;
            int damage;
            string actionName;
            switch (action)
            {
                case StoryBattleAction.Dance:
                    damage = 12 + TeamPower / 40000;
                    Combo = Mathf.Min(3, Combo + 1);
                    Resonance = Mathf.Min(5, Resonance + 1);
                    actionName = "绯音 · 流光连舞";
                    break;
                case StoryBattleAction.Support:
                    damage = 5;
                    Shield = Mathf.Min(40, Shield + 20);
                    Resonance = Mathf.Min(5, Resonance + 1);
                    actionName = "雾白 · 星幕守护";
                    break;
                case StoryBattleAction.Ultimate:
                    damage = 42 + TeamPower / 10000 + Combo * 6;
                    Combo = 0;
                    Resonance -= 3;
                    actionName = "全员共鸣 · 幻域终演";
                    break;
                default:
                    damage = 18 + TeamPower / 30000 + Combo * 5;
                    Combo = 0;
                    Resonance = Mathf.Min(5, Resonance + 1);
                    actionName = "星璃 · 星声穿透";
                    break;
            }

            damage = Mathf.Max(1, damage);
            BossHp = Mathf.Max(0, BossHp - damage);
            TotalDamage += damage;
            if (Victory)
                return new StoryBattleTurnResult(true, damage, 0, $"{actionName} 造成 {damage} 伤害，目标已击破！");

            int incoming = 12 + (Stage - 3) * 2 + (Turn % 3 == 0 ? 6 : 0);
            int absorbed = Mathf.Min(Shield, incoming);
            Shield -= absorbed;
            int enemyDamage = incoming - absorbed;
            PlayerHp = Mathf.Max(0, PlayerHp - enemyDamage);
            string suffix = absorbed > 0 ? $"，护盾抵消 {absorbed}" : string.Empty;
            return new StoryBattleTurnResult(true, damage, enemyDamage,
                $"{actionName} 造成 {damage} 伤害；敌方反击 {enemyDamage}{suffix}");
        }

        public int NextEnemyDamage => 12 + (Stage - 3) * 2 + ((Turn + 1) % 3 == 0 ? 6 : 0);
    }

    /// <summary>Portrait story combat with skill decisions, enemy retaliation and real win/loss states.</summary>
    public sealed class StoryBattlePanel : MonoBehaviour
    {
        private static readonly Color White = new Color32(249, 247, 255, 255);
        private static readonly Color Muted = new Color32(196, 190, 220, 255);
        private static readonly Color Pink = new Color32(255, 79, 190, 255);
        private static readonly Color Cyan = new Color32(75, 218, 255, 255);
        private static readonly Color Purple = new Color32(145, 83, 224, 255);

        private readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();
        private readonly List<UnityEngine.Object> generatedAssets = new List<UnityEngine.Object>();
        private readonly List<Button> actionButtons = new List<Button>();

        private GameModel model;
        private StoryBattleState battle;
        private Action onBack;
        private Action<string> onMessage;
        private Font font;
        private GameAudio gameAudio;
        private int stage;
        private bool actionLocked;
        private bool settled;
        private bool closing;

        private Image bossHpFill;
        private Image playerHpFill;
        private Image resonanceFill;
        private Image enemyCore;
        private Text bossHpText;
        private Text playerHpText;
        private Text shieldText;
        private Text turnText;
        private Text battleLogText;
        private Text resonanceText;
        private Text ultimateLabel;
        private Button ultimateButton;
        private Image ultimateBackground;
        private GameObject resultOverlay;
        private Text resultTitle;
        private Text resultGrade;
        private Text resultStats;
        private Text resultReward;
        private GameObject retryButton;
        private Text retryLabel;

        public static StoryBattlePanel Open(Transform host, GameModel gameModel, int selectedStage,
            Action back = null, Action<string> message = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));

            StoryBattlePanel existing = host.GetComponentInChildren<StoryBattlePanel>(true);
            if (existing != null) Destroy(existing.gameObject);

            GameObject panelObject = new GameObject("StoryBattlePanel", typeof(RectTransform), typeof(CanvasGroup));
            panelObject.transform.SetParent(host, false);
            Stretch(panelObject.GetComponent<RectTransform>());
            CanvasGroup group = panelObject.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            StoryBattlePanel panel = panelObject.AddComponent<StoryBattlePanel>();
            panel.model = gameModel;
            panel.stage = Mathf.Clamp(selectedStage, 3, 6);
            panel.onBack = back;
            panel.onMessage = message;
            panel.Build();
            panel.StartBattle();
            return panel;
        }

        private void Build()
        {
            font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ??
                   Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gameAudio = FindAnyObjectByType<GameAudio>();

            Image background = NewImage("BattleBackground", transform,
                Resources.Load<Sprite>("Art/LobbyBackground"), White);
            Stretch(background.rectTransform);
            background.preserveAspect = false;
            background.raycastTarget = true;
            Image shade = NewImage("BattleShade", transform, null, new Color32(3, 6, 28, 128));
            Stretch(shade.rectTransform);
            BuildHeader();
            BuildEnemy();
            BuildPlayerStatus();
            BuildSkills();
            BuildResult();
        }

        private void BuildHeader()
        {
            Image header = NewImage("BattleHeader", transform, null, new Color32(5, 10, 38, 238));
            PlaceTop(header.rectTransform, 0, 0, 720, 112);
            GameObject back = NewButton("BattleBack", header.transform, "返回", 17,
                new Color32(70, 46, 118, 242), White, AbortBattle, 18);
            PlaceTop(back.GetComponent<RectTransform>(), 18, 27, 88, 56);
            NewPlacedText(header.transform, $"剧情  7-{stage}", 13, new Color32(255, 173, 226, 255),
                128, 17, 220, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(header.transform, StageName(stage), 27, White,
                128, 40, 250, 42, TextAnchor.MiddleLeft, FontStyle.Bold);
            turnText = NewPlacedText(header.transform, "第 0 回合", 17, Cyan,
                442, 31, 120, 38, TextAnchor.MiddleRight, FontStyle.Bold);
            GameObject stamina = NewPanel("BattleStamina", header.transform, new Color32(43, 30, 91, 240), 18);
            PlaceTop(stamina.GetComponent<RectTransform>(), 578, 28, 124, 54);
            NewPlacedText(stamina.transform, $"体力 {model.Save.Stamina}", 15, White,
                8, 7, 108, 40, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildEnemy()
        {
            GameObject bossCard = NewPanel("BossStatus", transform, new Color32(30, 21, 76, 238), 24);
            PlaceTop(bossCard.GetComponent<RectTransform>(), 38, 138, 644, 146);
            NewPlacedText(bossCard.transform, "首领 · 霓虹噬梦者", 21, White,
                22, 15, 380, 36, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(bossCard.transform, "弱点：共鸣爆发 / 连击", 13, new Color32(255, 166, 224, 255),
                22, 51, 350, 25, TextAnchor.MiddleLeft);
            bossHpText = NewPlacedText(bossCard.transform, "生命", 14, White,
                430, 18, 188, 32, TextAnchor.MiddleRight, FontStyle.Bold);
            Image bossTrack = NewImage("BossHpTrack", bossCard.transform, RoundedSprite(10),
                new Color32(69, 53, 119, 255));
            PlaceTop(bossTrack.rectTransform, 22, 95, 600, 22);
            bossTrack.type = Image.Type.Sliced;
            bossHpFill = NewImage("BossHpFill", bossTrack.transform, RoundedSprite(10), Pink);
            Stretch(bossHpFill.rectTransform);
            bossHpFill.type = Image.Type.Filled;
            bossHpFill.fillMethod = Image.FillMethod.Horizontal;

            Image outerGlow = NewImage("EnemyGlow", transform, CreateRadialSprite(192),
                new Color32(255, 49, 201, 105));
            PlaceTop(outerGlow.rectTransform, 154, 290, 412, 412);
            enemyCore = NewImage("EnemyCore", transform, CreateRadialSprite(192),
                new Color32(122, 70, 238, 235));
            PlaceTop(enemyCore.rectTransform, 225, 360, 270, 270);

            Image face = NewImage("EnemyFace", transform, RoundedSprite(30), new Color32(25, 17, 71, 248));
            PlaceTop(face.rectTransform, 275, 420, 170, 116);
            face.type = Image.Type.Sliced;
            Image leftEye = NewImage("LeftEye", face.transform, RoundedSprite(10), Cyan);
            PlaceTop(leftEye.rectTransform, 32, 33, 34, 18);
            Image rightEye = NewImage("RightEye", face.transform, RoundedSprite(10), Pink);
            PlaceTop(rightEye.rectTransform, 104, 33, 34, 18);
            NewPlacedText(face.transform, "◇", 34, White, 60, 61, 50, 45,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            GameObject telegraph = NewPanel("EnemyTelegraph", transform, new Color32(28, 22, 73, 230), 18);
            PlaceTop(telegraph.GetComponent<RectTransform>(), 160, 650, 400, 58);
            battleLogText = NewPlacedText(telegraph.transform, "敌方正在锁定目标…", 14, Muted,
                14, 7, 372, 44, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildPlayerStatus()
        {
            GameObject panel = NewPanel("PlayerStatus", transform, new Color32(22, 19, 68, 242), 24);
            PlaceTop(panel.GetComponent<RectTransform>(), 38, 730, 644, 186);
            NewPlacedText(panel.transform, "幻域魅声 · 出战阵容", 15, new Color32(255, 170, 225, 255),
                20, 13, 250, 27, TextAnchor.MiddleLeft, FontStyle.Bold);
            playerHpText = NewPlacedText(panel.transform, "生命", 15, White,
                420, 13, 202, 27, TextAnchor.MiddleRight, FontStyle.Bold);
            Image hpTrack = NewImage("PlayerHpTrack", panel.transform, RoundedSprite(9),
                new Color32(66, 54, 117, 255));
            PlaceTop(hpTrack.rectTransform, 20, 52, 604, 20);
            hpTrack.type = Image.Type.Sliced;
            playerHpFill = NewImage("PlayerHpFill", hpTrack.transform, RoundedSprite(9), Cyan);
            Stretch(playerHpFill.rectTransform);
            playerHpFill.type = Image.Type.Filled;
            playerHpFill.fillMethod = Image.FillMethod.Horizontal;

            NewPlacedText(panel.transform, "共鸣", 14, Muted, 20, 91, 80, 28,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Image resonanceTrack = NewImage("ResonanceTrack", panel.transform, RoundedSprite(9),
                new Color32(66, 54, 117, 255));
            PlaceTop(resonanceTrack.rectTransform, 95, 96, 330, 18);
            resonanceTrack.type = Image.Type.Sliced;
            resonanceFill = NewImage("ResonanceFill", resonanceTrack.transform, RoundedSprite(9), Pink);
            Stretch(resonanceFill.rectTransform);
            resonanceFill.type = Image.Type.Filled;
            resonanceFill.fillMethod = Image.FillMethod.Horizontal;
            resonanceText = NewPlacedText(panel.transform, "0 / 3", 14, White,
                438, 87, 90, 36, TextAnchor.MiddleCenter, FontStyle.Bold);
            shieldText = NewPlacedText(panel.transform, "护盾 0", 14, new Color32(185, 176, 217, 255),
                520, 87, 104, 36, TextAnchor.MiddleRight, FontStyle.Bold);

            NewPlacedText(panel.transform, "战术提示：舞者叠连击，支援抵挡重击，3 点共鸣释放终演。", 13, Muted,
                20, 132, 604, 34, TextAnchor.MiddleLeft);
        }

        private void BuildSkills()
        {
            BuildSkillButton("SkillVocal", "主唱 · 星声穿透", "稳定输出", 38, 946,
                new Color32(191, 63, 180, 248), StoryBattleAction.Vocal);
            BuildSkillButton("SkillDance", "舞者 · 流光连舞", "积累连击", 370, 946,
                new Color32(70, 116, 195, 248), StoryBattleAction.Dance);
            BuildSkillButton("SkillSupport", "支援 · 星幕守护", "获得护盾", 38, 1112,
                new Color32(65, 132, 154, 248), StoryBattleAction.Support);

            GameObject ultimate = BuildSkillButton("SkillUltimate", "全员 · 幻域终演", "需要 3 点共鸣", 370, 1112,
                new Color32(151, 55, 175, 248), StoryBattleAction.Ultimate);
            ultimateButton = ultimate.GetComponent<Button>();
            ultimateBackground = ultimate.GetComponent<Image>();
            ultimateLabel = ultimate.transform.Find("Subtitle").GetComponent<Text>();

            GameObject roster = NewPanel("BattleRoster", transform, new Color32(23, 19, 66, 232), 22);
            PlaceTop(roster.GetComponent<RectTransform>(), 38, 1294, 644, 118);
            for (int slot = 0; slot < 3; slot++)
            {
                int memberIndex = slot < model.Save.Team.Count ? model.Save.Team[slot] : slot;
                memberIndex = Mathf.Clamp(memberIndex, 0, GameModel.Members.Length - 1);
                MemberDefinition member = GameModel.Members[memberIndex];
                Image portrait = NewImage($"BattleMember-{slot}", roster.transform,
                    Resources.Load<Sprite>(member.ResourcePath), White);
                PlaceTop(portrait.rectTransform, 28 + slot * 206, 10, 76, 76);
                portrait.preserveAspect = true;
                NewPlacedText(roster.transform, member.Name, 14, White,
                    109 + slot * 206, 23, 84, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
                NewPlacedText(roster.transform, member.Role, 12, slot == 0 ? Pink : slot == 1 ? Cyan : Muted,
                    109 + slot * 206, 49, 84, 22, TextAnchor.MiddleLeft);
            }

            NewPlacedText(transform, "每次行动后敌人都会反击 · 战败与中途退出不消耗体力", 13,
                new Color32(220, 206, 239, 255), 48, 1430, 624, 36,
                TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private GameObject BuildSkillButton(string name, string title, string subtitle, float x, float y,
            Color color, StoryBattleAction action)
        {
            GameObject button = NewButton(name, transform, string.Empty, 1, color, White,
                () => UseSkill(action), 24);
            PlaceTop(button.GetComponent<RectTransform>(), x, y, 312, 144);
            actionButtons.Add(button.GetComponent<Button>());
            NewPlacedText(button.transform, title, 18, White, 18, 22, 276, 34,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Text sub = NewText("Subtitle", button.transform, subtitle, 13,
                new Color32(237, 218, 247, 255), FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceTop(sub.rectTransform, 18, 70, 276, 30);
            NewPlacedText(button.transform, ">", 24, White, 264, 88, 28, 34,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            return button;
        }

        private void BuildResult()
        {
            Image overlay = NewImage("BattleResult", transform, null, new Color32(2, 4, 22, 236));
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            resultOverlay = overlay.gameObject;

            GameObject panel = NewPanel("BattleResultCard", overlay.transform, new Color32(39, 27, 91, 253), 30);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = Vector2.one * 0.5f;
            panelRect.pivot = Vector2.one * 0.5f;
            panelRect.sizeDelta = new Vector2(610, 760);
            resultTitle = NewPlacedText(panel.transform, "挑战完成", 15,
                new Color32(255, 174, 226, 255), 40, 38, 530, 30,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            resultGrade = NewPlacedText(panel.transform, "卓越", 70, White,
                40, 82, 530, 128, TextAnchor.MiddleCenter, FontStyle.Bold);
            resultGrade.verticalOverflow = VerticalWrapMode.Overflow;
            Outline gradeOutline = resultGrade.gameObject.AddComponent<Outline>();
            gradeOutline.effectColor = new Color32(255, 71, 196, 155);
            gradeOutline.effectDistance = new Vector2(0, 4);
            NewPlacedText(panel.transform, "战斗评级", 17, Muted, 40, 216, 530, 30,
                TextAnchor.MiddleCenter);
            resultStats = NewPlacedText(panel.transform, string.Empty, 19, White,
                52, 272, 506, 112, TextAnchor.UpperCenter, FontStyle.Bold);
            GameObject reward = NewPanel("BattleReward", panel.transform, new Color32(97, 59, 148, 235), 22);
            PlaceTop(reward.GetComponent<RectTransform>(), 48, 414, 514, 112);
            resultReward = NewPlacedText(reward.transform, string.Empty, 17, White,
                20, 13, 474, 84, TextAnchor.MiddleCenter, FontStyle.Bold);
            GameObject done = NewButton("ReturnLevelMap", panel.transform, "返回选关地图", 21,
                Pink, White, CloseAfterResult, 24);
            PlaceTop(done.GetComponent<RectTransform>(), 140, 570, 330, 70);
            retryButton = NewButton("RetryBattle", panel.transform, "重新挑战", 18,
                Purple, White, RetryBattle, 22);
            PlaceTop(retryButton.GetComponent<RectTransform>(), 185, 660, 240, 60);
            retryLabel = retryButton.transform.Find("Label").GetComponent<Text>();
            resultOverlay.SetActive(false);
        }

        private void StartBattle()
        {
            battle = new StoryBattleState(stage, model.TeamPower);
            actionLocked = false;
            settled = false;
            resultOverlay.SetActive(false);
            battleLogText.text = $"敌方下一击预计 {battle.NextEnemyDamage} 伤害 · 请选择技能";
            Refresh();
        }

        private void UseSkill(StoryBattleAction action)
        {
            if (actionLocked || battle == null || battle.Finished) return;
            StoryBattleTurnResult result = battle.TakeAction(action);
            if (!result.Accepted)
            {
                battleLogText.text = result.Message;
                onMessage?.Invoke(result.Message);
                return;
            }

            actionLocked = true;
            battleLogText.text = result.Message;
            if (result.BossDamage >= 40) gameAudio?.PlaySuccess();
            Refresh();
            StartCoroutine(FinishTurn());
        }

        private IEnumerator FinishTurn()
        {
            yield return new WaitForSecondsRealtime(0.48f);
            if (battle.Finished)
            {
                CompleteBattle();
                yield break;
            }

            actionLocked = false;
            battleLogText.text = $"敌方下一击预计 {battle.NextEnemyDamage} 伤害 · 请选择技能";
            Refresh();
        }

        private void CompleteBattle()
        {
            actionLocked = true;
            if (battle.Victory)
            {
                string message = settled ? "本关奖励已结算" : string.Empty;
                bool currentStage = IsCurrentStoryStage(stage, model.Save.StoryProgress);
                if (!settled && !currentStage)
                {
                    message = model.Save.StoryProgress >= GameModel.MaxStoryProgress
                        ? "当前章节已完成，无需重复结算"
                        : "章节进度已变化，请返回选关地图重新选择关卡";
                }

                bool succeeded = !settled && currentStage && model.AdvanceStory(out message);
                settled = true;
                string grade = BattleGrade(battle);
                resultTitle.text = "挑战完成";
                resultGrade.text = GradeLabel(grade);
                resultGrade.color = grade == "S" ? new Color32(255, 158, 224, 255) : White;
                resultStats.text = $"7-{stage}  {StageName(stage)}\n回合  {battle.Turn}    总伤害  {battle.TotalDamage}\n剩余生命  {battle.PlayerHp}/{battle.MaxPlayerHp}";
                resultReward.text = succeeded ? message : $"战斗胜利\n{message}";
                retryButton.SetActive(false);
                gameAudio?.PlaySuccess();
            }
            else
            {
                resultTitle.text = "挑战失败";
                resultGrade.text = "未过关";
                resultGrade.color = new Color32(196, 190, 220, 255);
                resultStats.text = $"7-{stage}  {StageName(stage)}\n回合  {battle.Turn}    总伤害  {battle.TotalDamage}\n请调整技能顺序后重试";
                resultReward.text = "挑战失败，没有消耗体力\n建议用支援抵挡每第 3 回合的重击";
                retryButton.SetActive(true);
                retryLabel.text = "重新挑战";
            }

            resultOverlay.SetActive(true);
            resultOverlay.transform.SetAsLastSibling();
        }

        private void Refresh()
        {
            if (battle == null) return;
            bossHpFill.fillAmount = battle.BossHp / (float)battle.MaxBossHp;
            playerHpFill.fillAmount = battle.PlayerHp / (float)battle.MaxPlayerHp;
            resonanceFill.fillAmount = Mathf.Clamp01(battle.Resonance / 3f);
            bossHpText.text = $"生命 {battle.BossHp}/{battle.MaxBossHp}";
            playerHpText.text = $"生命 {battle.PlayerHp}/{battle.MaxPlayerHp}";
            shieldText.text = $"护盾 {battle.Shield}";
            resonanceText.text = $"{battle.Resonance} / 3";
            turnText.text = $"第 {battle.Turn} 回合";
            bool ultimateReady = battle.Resonance >= 3 && !battle.Finished;
            bool canAct = !actionLocked && !battle.Finished;
            for (int index = 0; index < actionButtons.Count; index++)
                actionButtons[index].interactable = canAct;
            ultimateButton.interactable = ultimateReady && canAct;
            ultimateBackground.color = ultimateReady
                ? new Color32(220, 53, 188, 252)
                : new Color32(87, 65, 117, 242);
            ultimateLabel.text = ultimateReady ? "共鸣已满 · 立即释放" : "需要 3 点共鸣";
        }

        private void Update()
        {
            if (enemyCore == null) return;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.8f) * 0.045f;
            enemyCore.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            Color color = enemyCore.color;
            color.a = Mathf.Lerp(0.72f, 0.96f, 0.5f + Mathf.Sin(Time.unscaledTime * 4.6f) * 0.5f);
            enemyCore.color = color;
        }

        private void RetryBattle()
        {
            if (settled) return;
            gameAudio?.PlayClick();
            StartBattle();
        }

        private void AbortBattle()
        {
            if (settled)
            {
                CloseAfterResult();
                return;
            }

            ClosePanel("已退出剧情战斗，本次未消耗体力");
        }

        private void CloseAfterResult()
        {
            ClosePanel(battle != null && battle.Victory ? "战斗结果与章节进度已保存" : null);
        }

        private void ClosePanel(string message)
        {
            if (closing) return;
            closing = true;
            Action callback = onBack;
            Action<string> notify = onMessage;
            gameObject.SetActive(false);
            Destroy(gameObject);
            callback?.Invoke();
            if (!string.IsNullOrEmpty(message)) notify?.Invoke(message);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            for (int index = 0; index < generatedAssets.Count; index++)
            {
                UnityEngine.Object asset = generatedAssets[index];
                if (asset == null) continue;
                if (Application.isPlaying) Destroy(asset);
                else DestroyImmediate(asset);
            }
        }

        private static string StageName(int value)
        {
            string[] names = { "镜潮街区", "余响天桥", "星幕塔台", "梦核终演" };
            return names[Mathf.Clamp(value - 3, 0, names.Length - 1)];
        }

        public static bool IsCurrentStoryStage(int stage, int progress)
        {
            return progress < GameModel.MaxStoryProgress &&
                   stage == LevelMapPanel.StoryStageForProgress(progress);
        }

        private static string BattleGrade(StoryBattleState state)
        {
            float hpRatio = state.PlayerHp / (float)state.MaxPlayerHp;
            if (hpRatio >= 0.72f && state.Turn <= 6) return "S";
            if (hpRatio >= 0.42f && state.Turn <= 8) return "A";
            if (hpRatio >= 0.18f) return "B";
            return "C";
        }

        private static string GradeLabel(string grade)
        {
            if (grade == "S") return "卓越";
            if (grade == "A") return "优秀";
            if (grade == "B") return "良好";
            return "达成";
        }

        private Sprite CreateRadialSprite(int size)
        {
            Texture2D texture = NewTexture("Battle-Radial", size, size);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - normalized), 1.7f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return NewSprite(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, Vector4.zero);
        }

        private Sprite RoundedSprite(int radius)
        {
            radius = Mathf.Clamp(radius, 4, 32);
            if (roundedSprites.TryGetValue(radius, out Sprite cached)) return cached;
            const int size = 64;
            Texture2D texture = NewTexture($"Battle-Rounded-{radius}", size, size);
            Color32[] pixels = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nearestX = Mathf.Clamp(x + 0.5f, r, size - r);
                    float nearestY = Mathf.Clamp(y + 0.5f, r, size - r);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                        new Vector2(nearestX, nearestY));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - distance + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = NewSprite(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                new Vector4(radius, radius, radius, radius));
            roundedSprites[radius] = sprite;
            return sprite;
        }

        private Texture2D NewTexture(string name, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            generatedAssets.Add(texture);
            return texture;
        }

        private Sprite NewSprite(Texture2D texture, Rect rect, Vector2 pivot, Vector4 border)
        {
            Sprite sprite = Sprite.Create(texture, rect, pivot, 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = texture.name + "-Sprite";
            sprite.hideFlags = HideFlags.DontSave;
            generatedAssets.Add(sprite);
            return sprite;
        }

        private GameObject NewObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result;
        }

        private Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject result = NewObject(name, parent);
            RectTransform rect = result.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            Image image = result.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private GameObject NewPanel(string name, Transform parent, Color color, int radius)
        {
            Image image = NewImage(name, parent, RoundedSprite(radius), color);
            image.type = Image.Type.Sliced;
            return image.gameObject;
        }

        private GameObject NewButton(string name, Transform parent, string label, int fontSize, Color background,
            Color foreground, UnityAction action, int radius)
        {
            GameObject result = NewPanel(name, parent, background, radius);
            Image image = result.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = result.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.80f, 0.80f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                gameAudio?.PlayClick();
                action?.Invoke();
            });
            Text text = NewText("Label", result.transform, label, fontSize, foreground, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6, 4, -6, -4);
            return result;
        }

        private Text NewText(string name, Transform parent, string value, int size, Color color, FontStyle style,
            TextAnchor alignment)
        {
            GameObject result = NewObject(name, parent);
            RectTransform rect = result.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            Text text = result.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private Text NewPlacedText(Transform parent, string value, int size, Color color, float x, float y,
            float width, float height, TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            Text text = NewText("Text", parent, value, size, color, style, alignment);
            PlaceTop(text.rectTransform, x, y, width, height);
            return text;
        }

        private static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect, float left = 0, float bottom = 0,
            float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
