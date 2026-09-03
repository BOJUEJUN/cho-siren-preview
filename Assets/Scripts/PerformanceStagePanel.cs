using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChoSiren
{
    public enum PerformanceJudgement
    {
        Perfect,
        Great,
        Miss,
    }

    /// <summary>
    /// A lightweight portrait rhythm stage. Rewards are settled only after the final note;
    /// leaving early never consumes stamina.
    /// </summary>
    public sealed class PerformanceStagePanel : MonoBehaviour
    {
        private const int TotalNotes = 6;
        private const float RoundTimeout = 2.35f;
        private const float TrackLeft = 86f;
        private const float TrackWidth = 548f;

        private static readonly Color White = new Color32(249, 247, 255, 255);
        private static readonly Color Muted = new Color32(202, 194, 229, 255);
        private static readonly Color Pink = new Color32(255, 82, 194, 255);
        private static readonly Color Cyan = new Color32(80, 220, 255, 255);
        private static readonly Color Purple = new Color32(145, 83, 223, 255);

        private readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();
        private readonly List<UnityEngine.Object> generatedAssets = new List<UnityEngine.Object>();

        private GameModel model;
        private Action onBack;
        private Action<string> onMessage;
        private Font font;
        private GameAudio gameAudio;

        private RectTransform markerRect;
        private RectTransform heroRect;
        private Image targetGlow;
        private Image tapButtonImage;
        private Button tapButton;
        private Text scoreText;
        private Text comboText;
        private Text noteText;
        private Text judgementText;
        private Text hintText;
        private GameObject resultOverlay;
        private Text resultGradeText;
        private Text resultStatsText;
        private Text resultRewardText;

        private int noteIndex;
        private int score;
        private int combo;
        private int maxCombo;
        private int perfectCount;
        private int greatCount;
        private int missCount;
        private float roundStart;
        private bool acceptingInput;
        private bool resolving;
        private bool settled;
        private bool closing;
        private float heroBaseY;

        public static PerformanceStagePanel Open(Transform host, GameModel gameModel, Action back = null,
            Action<string> message = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));

            PerformanceStagePanel existing = host.GetComponentInChildren<PerformanceStagePanel>(true);
            if (existing != null) Destroy(existing.gameObject);

            if (!CanStartPerformance(gameModel.Save.Stamina))
            {
                message?.Invoke("体力不足，暂时无法开始演出");
                return null;
            }

            GameObject panelObject = new GameObject("PerformanceStagePanel", typeof(RectTransform), typeof(CanvasGroup));
            panelObject.transform.SetParent(host, false);
            Stretch(panelObject.GetComponent<RectTransform>());
            CanvasGroup group = panelObject.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            PerformanceStagePanel panel = panelObject.AddComponent<PerformanceStagePanel>();
            panel.model = gameModel;
            panel.onBack = back;
            panel.onMessage = message;
            panel.Build();
            panel.BeginPerformance();
            return panel;
        }

        public static PerformanceJudgement JudgeDistance(float normalizedDistance)
        {
            normalizedDistance = Mathf.Abs(normalizedDistance);
            if (normalizedDistance <= 0.085f) return PerformanceJudgement.Perfect;
            if (normalizedDistance <= 0.225f) return PerformanceJudgement.Great;
            return PerformanceJudgement.Miss;
        }

        public static bool CanStartPerformance(int stamina)
        {
            return stamina >= GameModel.PerformanceStaminaCost;
        }

        private void Build()
        {
            font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ??
                   Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gameAudio = FindAnyObjectByType<GameAudio>();

            Image background = NewImage("StageBackground", transform, Resources.Load<Sprite>("Art/LobbyBackground"), White);
            Stretch(background.rectTransform);
            background.preserveAspect = false;
            background.raycastTarget = true;

            Image shade = NewImage("StageShade", transform, null, new Color32(4, 6, 30, 92));
            Stretch(shade.rectTransform);

            Image spotlight = NewImage("Spotlight", transform, CreateRadialSprite(192),
                new Color32(255, 121, 229, 82));
            PlaceTop(spotlight.rectTransform, 40, 112, 640, 800);

            BuildHeader();
            BuildStage();
            BuildTimingTrack();
            BuildResult();
        }

        private void BuildHeader()
        {
            Image header = NewImage("PerformanceHeader", transform, null, new Color32(5, 10, 40, 232));
            PlaceTop(header.rectTransform, 0, 0, 720, 112);

            GameObject back = NewButton("Back", header.transform, "返回", 17,
                new Color32(71, 47, 119, 240), White, AbortPerformance, 18);
            PlaceTop(back.GetComponent<RectTransform>(), 18, 27, 88, 56);

            NewPlacedText(header.transform, "现场演出", 13, new Color32(255, 174, 226, 255),
                128, 18, 190, 22, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(header.transform, "星潮共鸣", 27, White,
                128, 39, 190, 42, TextAnchor.MiddleLeft, FontStyle.Bold);

            scoreText = NewPlacedText(header.transform, "分数 000000", 17, Cyan,
                350, 26, 190, 30, TextAnchor.MiddleRight, FontStyle.Bold);
            comboText = NewPlacedText(header.transform, "连击 0", 14, new Color32(255, 174, 226, 255),
                350, 57, 190, 25, TextAnchor.MiddleRight, FontStyle.Bold);

            GameObject stamina = NewPanel("Stamina", header.transform, new Color32(44, 30, 91, 240), 18);
            PlaceTop(stamina.GetComponent<RectTransform>(), 558, 28, 144, 54);
            NewPlacedText(stamina.transform, $"体力 {model.Save.Stamina}/{GameModel.MaxStamina}", 15, White,
                8, 7, 128, 40, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildStage()
        {
            NewPlacedText(transform, "闪耀舞台计划 · 现场演出", 15, new Color32(255, 190, 233, 255),
                80, 135, 560, 28, TextAnchor.MiddleCenter, FontStyle.Bold);

            Image rearRing = NewImage("RearRing", transform, CreateRadialSprite(192),
                new Color32(102, 79, 255, 70));
            PlaceTop(rearRing.rectTransform, 105, 155, 510, 610);

            Image hero = NewImage("StageHero", transform, Resources.Load<Sprite>("Art/HeroFallback"), White);
            heroRect = hero.rectTransform;
            PlaceTop(heroRect, 120, 164, 480, 590);
            hero.preserveAspect = true;
            hero.useSpriteMesh = true;
            heroBaseY = heroRect.anchoredPosition.y;

            GameObject banner = NewPanel("RoundBanner", transform, new Color32(28, 22, 76, 224), 22);
            PlaceTop(banner.GetComponent<RectTransform>(), 116, 704, 488, 72);
            noteText = NewPlacedText(banner.transform, "节拍 1 / 6", 15, new Color32(255, 179, 229, 255),
                16, 8, 160, 25, TextAnchor.MiddleLeft, FontStyle.Bold);
            judgementText = NewPlacedText(banner.transform, "准备应援", 25, White,
                150, 10, 322, 48, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildTimingTrack()
        {
            GameObject trackPanel = NewPanel("TimingPanel", transform, new Color32(15, 14, 61, 238), 26);
            PlaceTop(trackPanel.GetComponent<RectTransform>(), 38, 796, 644, 236);

            NewPlacedText(trackPanel.transform, "节拍到达中心高亮区时点击", 15, Muted,
                22, 16, 600, 28, TextAnchor.MiddleCenter);

            Image track = NewImage("TimingTrack", transform, RoundedSprite(14), new Color32(65, 54, 122, 245));
            PlaceTop(track.rectTransform, TrackLeft, 877, TrackWidth, 34);
            track.type = Image.Type.Sliced;

            targetGlow = NewImage("PerfectZoneGlow", transform, RoundedSprite(16), new Color32(255, 91, 209, 115));
            PlaceTop(targetGlow.rectTransform, 316, 858, 88, 72);
            targetGlow.type = Image.Type.Sliced;

            Image perfectZone = NewImage("PerfectZone", transform, RoundedSprite(10), new Color32(255, 196, 237, 245));
            PlaceTop(perfectZone.rectTransform, 338, 870, 44, 48);
            perfectZone.type = Image.Type.Sliced;

            Image marker = NewImage("BeatMarker", transform, RoundedSprite(14), Cyan);
            markerRect = marker.rectTransform;
            PlaceTop(markerRect, TrackLeft - 13, 861, 26, 66);
            marker.type = Image.Type.Sliced;

            hintText = NewPlacedText(trackPanel.transform, "等待节拍…", 14, new Color32(128, 221, 255, 255),
                22, 132, 600, 28, TextAnchor.MiddleCenter, FontStyle.Bold);

            GameObject tap = NewButton("PerformanceTap", transform, "♫  点击应援  ♫", 30,
                new Color32(231, 45, 174, 252), White, TapBeat, 32);
            PlaceTop(tap.GetComponent<RectTransform>(), 94, 1058, 532, 142);
            tapButton = tap.GetComponent<Button>();
            tapButtonImage = tap.GetComponent<Image>();
            Outline outline = tap.AddComponent<Outline>();
            outline.effectColor = new Color32(255, 176, 232, 135);
            outline.effectDistance = new Vector2(0, 5);

            GameObject stats = NewPanel("StageStats", transform, new Color32(28, 23, 74, 232), 24);
            PlaceTop(stats.GetComponent<RectTransform>(), 46, 1234, 628, 104);
            NewPlacedText(stats.transform, "完美", 13, new Color32(255, 165, 225, 255),
                18, 16, 160, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(stats.transform, "优秀", 13, Cyan,
                234, 16, 160, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(stats.transform, "失误", 13, Muted,
                450, 16, 160, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(stats.transform, "中心 ±8%", 12, White,
                18, 48, 160, 26, TextAnchor.MiddleCenter);
            NewPlacedText(stats.transform, "中心 ±22%", 12, White,
                234, 48, 160, 26, TextAnchor.MiddleCenter);
            NewPlacedText(stats.transform, "偏离节拍", 12, White,
                450, 48, 160, 26, TextAnchor.MiddleCenter);
        }

        private void BuildResult()
        {
            Image overlay = NewImage("PerformanceResult", transform, null, new Color32(3, 4, 23, 232));
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            resultOverlay = overlay.gameObject;

            GameObject panel = NewPanel("ResultCard", overlay.transform, new Color32(39, 27, 91, 252), 30);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = Vector2.one * 0.5f;
            panelRect.sizeDelta = new Vector2(610, 720);

            NewPlacedText(panel.transform, "演出完成", 14, new Color32(255, 174, 226, 255),
                45, 38, 520, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            resultGradeText = NewPlacedText(panel.transform, "卓越", 70, White,
                45, 82, 520, 150, TextAnchor.MiddleCenter, FontStyle.Bold);
            resultGradeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            resultGradeText.verticalOverflow = VerticalWrapMode.Overflow;
            Outline gradeOutline = resultGradeText.gameObject.AddComponent<Outline>();
            gradeOutline.effectColor = new Color32(255, 74, 196, 150);
            gradeOutline.effectDistance = new Vector2(0, 4);
            NewPlacedText(panel.transform, "演出评级", 17, Muted,
                45, 230, 520, 32, TextAnchor.MiddleCenter);
            resultStatsText = NewPlacedText(panel.transform, string.Empty, 20, White,
                55, 292, 500, 112, TextAnchor.UpperCenter, FontStyle.Bold);

            GameObject reward = NewPanel("Reward", panel.transform, new Color32(96, 58, 147, 230), 22);
            PlaceTop(reward.GetComponent<RectTransform>(), 48, 430, 514, 112);
            resultRewardText = NewPlacedText(reward.transform, string.Empty, 17, White,
                20, 14, 474, 82, TextAnchor.MiddleCenter, FontStyle.Bold);

            GameObject done = NewButton("ReturnLobby", panel.transform, "返回大厅", 21, Pink, White,
                ReturnToLobby, 24);
            PlaceTop(done.GetComponent<RectTransform>(), 140, 592, 330, 70);
            resultOverlay.SetActive(false);
        }

        private void BeginPerformance()
        {
            noteIndex = 0;
            score = 0;
            combo = 0;
            maxCombo = 0;
            perfectCount = 0;
            greatCount = 0;
            missCount = 0;
            settled = false;
            acceptingInput = true;
            resolving = false;
            tapButton.interactable = true;
            roundStart = Time.unscaledTime;
            RefreshHud();
        }

        private void Update()
        {
            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.5f;
            if (targetGlow != null)
            {
                Color color = targetGlow.color;
                color.a = Mathf.Lerp(0.28f, 0.66f, pulse);
                targetGlow.color = color;
                targetGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.08f, pulse);
            }

            if (tapButtonImage != null && acceptingInput)
            {
                Color color = tapButtonImage.color;
                color.a = Mathf.Lerp(0.92f, 1f, pulse);
                tapButtonImage.color = color;
            }

            if (heroRect != null)
            {
                Vector2 position = heroRect.anchoredPosition;
                position.y = heroBaseY + Mathf.Sin(Time.unscaledTime * 2.3f) * 7f;
                heroRect.anchoredPosition = position;
                float scale = 1f + Mathf.Sin(Time.unscaledTime * 2.3f) * 0.008f;
                heroRect.localScale = new Vector3(scale, scale, 1f);
            }

            if (!acceptingInput || resolving || markerRect == null) return;

            float elapsed = Time.unscaledTime - roundStart;
            float phase = Mathf.PingPong(elapsed * (0.72f + noteIndex * 0.055f), 1f);
            Vector2 markerPosition = markerRect.anchoredPosition;
            markerPosition.x = TrackLeft - 13f + phase * TrackWidth;
            markerRect.anchoredPosition = markerPosition;
            hintText.text = Mathf.Abs(phase - 0.5f) <= 0.085f ? "现在 · 完美时机" : "跟随光标，把握中心节拍";

            if (elapsed >= RoundTimeout) ResolveBeat(PerformanceJudgement.Miss);
        }

        private void TapBeat()
        {
            gameAudio?.PlayClick();
            if (!acceptingInput || resolving) return;
            float phase = Mathf.InverseLerp(TrackLeft - 13f, TrackLeft - 13f + TrackWidth,
                markerRect.anchoredPosition.x);
            ResolveBeat(JudgeDistance(phase - 0.5f));
        }

        private void ResolveBeat(PerformanceJudgement judgement)
        {
            if (!acceptingInput || resolving) return;
            resolving = true;
            tapButton.interactable = false;

            switch (judgement)
            {
                case PerformanceJudgement.Perfect:
                    perfectCount++;
                    combo++;
                    score += 1200 + combo * 80;
                    judgementText.text = "完美！";
                    judgementText.color = new Color32(255, 167, 226, 255);
                    gameAudio?.PlaySuccess();
                    break;
                case PerformanceJudgement.Great:
                    greatCount++;
                    combo++;
                    score += 760 + combo * 45;
                    judgementText.text = "优秀";
                    judgementText.color = Cyan;
                    break;
                default:
                    missCount++;
                    combo = 0;
                    score += 100;
                    judgementText.text = "失误";
                    judgementText.color = Muted;
                    break;
            }

            maxCombo = Mathf.Max(maxCombo, combo);
            RefreshHud();
            StartCoroutine(AdvanceAfterFeedback());
        }

        private IEnumerator AdvanceAfterFeedback()
        {
            yield return new WaitForSecondsRealtime(0.48f);
            noteIndex++;
            if (noteIndex >= TotalNotes)
            {
                CompletePerformance();
                yield break;
            }

            resolving = false;
            tapButton.interactable = true;
            roundStart = Time.unscaledTime;
            judgementText.text = "节拍准备";
            judgementText.color = White;
            RefreshHud();
        }

        private void CompletePerformance()
        {
            if (settled) return;
            acceptingInput = false;
            resolving = false;
            settled = true;
            tapButton.interactable = false;

            bool succeeded = model.Perform(out string message);
            string grade = ScoreGrade(score, missCount);
            resultGradeText.text = GradeLabel(grade);
            resultGradeText.color = grade == "S" ? new Color32(255, 158, 224, 255) : White;
            resultStatsText.text = $"总分  {score:N0}\n完美 {perfectCount}   优秀 {greatCount}   失误 {missCount}\n最高连击  {maxCombo}";
            resultRewardText.text = succeeded ? message : $"奖励结算失败\n{message}";
            resultOverlay.SetActive(true);
            resultOverlay.transform.SetAsLastSibling();
            if (succeeded) gameAudio?.PlaySuccess();
        }

        private static string ScoreGrade(int finalScore, int misses)
        {
            if (misses == 0 && finalScore >= 6000) return "S";
            if (misses <= 1 && finalScore >= 4300) return "A";
            if (finalScore >= 2600) return "B";
            return "C";
        }

        private static string GradeLabel(string grade)
        {
            if (grade == "S") return "卓越";
            if (grade == "A") return "优秀";
            if (grade == "B") return "良好";
            return "达成";
        }

        private void RefreshHud()
        {
            if (scoreText != null) scoreText.text = $"分数 {score:000000}";
            if (comboText != null) comboText.text = $"连击 {combo}";
            if (noteText != null) noteText.text = $"节拍 {Mathf.Min(noteIndex + 1, TotalNotes)} / {TotalNotes}";
        }

        private void AbortPerformance()
        {
            if (settled)
            {
                ReturnToLobby();
                return;
            }

            ClosePanel("已退出演出，本次未消耗体力");
        }

        private void ReturnToLobby()
        {
            ClosePanel(settled ? "演出成绩与奖励已保存" : null);
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

        private Sprite CreateRadialSprite(int size)
        {
            Texture2D texture = NewTexture("Performance-Radial", size, size);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.8f);
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
            Texture2D texture = NewTexture($"Performance-Rounded-{radius}", size, size);
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
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.AddListener(action);
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
