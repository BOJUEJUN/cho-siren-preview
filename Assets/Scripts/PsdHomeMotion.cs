using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Scene gestures, subtle breathing, dialogue and effects for the authored PSD homepage.</summary>
    public sealed class PsdHomeMotion : MonoBehaviour
    {
        private static readonly string[] Lines =
        {
            "呐，今天也愿意和我一起唱吗？",
            "这束光，一定也会照到你。",
            "来吧，开始属于我们的舞台！"
        };

        // The second lobby idol has no performance clips yet; she answers with her own lines.
        private static readonly string[] LilithLines =
        {
            "{player}～今天也要乖乖陪我排练哦？",
            "听说你又签了新人……我可是会吃醋的。",
            "舞台的主角，当然只能是我啦！",
            "再盯着我看，就要收门票了哦～"
        };

        /// <summary>Fills {player} with the manager's signed name (set by the app).</summary>
        public Func<string, string> Personalize;

        private static readonly string[] SpeakerIds = { "xingli", "feiyin", "yeying", "wubai", "huanhuoling", "linyin" };
        public static string SpeakerName(int character)
        {
            string id = GameModel.HomeMemberId1113(character);
            int index = GameModel.IndexOfMember(id);
            return index >= 0 ? GameModel.Members[index].Name : string.Empty;
        }
        // Japanese recordings are paired by the returned index; subtitles stay personalized Chinese.
        private static readonly string[][] GuestLines =
        {
            new[] { "月光下的旋律，只唱给你听。", "精灵的耳朵可是很灵的，别偷偷叹气哦。", "{player}，今晚的舞台，我会让它发光。" },
            new[] { "要和我一起去海里唱歌吗？", "尾巴上的鳞片，今天也闪闪发亮～", "{player}！伸出手，接住我的歌声！" },
            new[] { "喵～被我的眼神迷住了吧？", "{player}，再靠近一点，就把你变成我的头号粉丝。", "今晚的舞台，谁都别想移开视线。" },
            new[] { "跟上我的鼓点，不准掉队。", "{player}，什么盾挡在前面，我都给你砸开。", "别愣着，下一首要更炸！" },
        };
        private int guestLast = -1;
        private Text speakerText;
        private int lilithLast = -1;

        private RectTransform stage, character, outline, background;
        private Vector2 characterPosition;
        private Vector3 characterScale;
        private Vector2 outlinePosition, backgroundPosition, pointerOffset;
        private readonly List<CanvasGroup> ui = new List<CanvasGroup>();
        private readonly Dictionary<CanvasGroup, Vector2> uiPositions = new Dictionary<CanvasGroup, Vector2>();
        private Action<int> onDialogue;
        private Font font;
        private PsdHomeDialogue1031.Parts dialogue;
        private CanvasGroup bubble;
        private Text bubbleText;
        private float bubbleUntil, queuedDialogueAt = -1f, uiFade = 1f, lastUiToggleAt = -1f;
        private int lastLine = -1;

        public bool UiHidden { get; private set; }

        public void Initialize(RectTransform root, Action<int> dialogue, Sprite rounded, Font textFont)
        {
            stage = root;
            onDialogue = dialogue;
            font = textFont;
        }

        public void RegisterUi(GameObject item)
        {
            CanvasGroup group = item.GetComponent<CanvasGroup>();
            if (group == null) group = item.AddComponent<CanvasGroup>();
            ui.Add(group);
            if (item.transform is RectTransform rect) uiPositions[group] = rect.anchoredPosition;
        }

        /// <summary>
        /// Shrinks a registered entry around the given pivot without moving that edge, and
        /// re-records its rest position so the hide/show slide returns it there.
        /// </summary>
        public void RescaleUi(GameObject item, float scale, Vector2 pivot)
        {
            if (item == null || !(item.transform is RectTransform rect)) return;
            Vector2 size = rect.rect.size;
            rect.anchoredPosition += Vector2.Scale(pivot - rect.pivot, size);
            rect.pivot = pivot;
            rect.localScale = new Vector3(scale, scale, 1f);
            CanvasGroup group = item.GetComponent<CanvasGroup>();
            if (group != null && uiPositions.ContainsKey(group)) uiPositions[group] = rect.anchoredPosition;
        }

        private readonly List<CanvasGroup> fadeOnly = new List<CanvasGroup>();

        /// <summary>Shared chrome (header, nav dock) that fades with the lobby UI but never slides.</summary>
        public void RegisterFadeOnly(GameObject item)
        {
            if (item == null) return;
            CanvasGroup group = UnityNull.Real(item.GetComponent<CanvasGroup>()) ?? item.AddComponent<CanvasGroup>();
            if (!fadeOnly.Contains(group)) fadeOnly.Add(group);
        }

        private void OnDestroy()
        {
            foreach (CanvasGroup group in fadeOnly)
                if (group != null) { group.alpha = 1f; group.blocksRaycasts = true; }
        }

        public void SetCharacter(RectTransform rect)
        {
            character = rect;
            characterPosition = rect.anchoredPosition;
            characterScale = rect.localScale;
            AttachBreath();
        }

        // The portrait and its rim bend with one idle field, measured on the portrait.
        private void AttachBreath()
        {
            foreach (RectTransform rect in new[] { character, outline })
            {
                if (rect == null) continue;
                if (!rect.TryGetComponent(out PsdHomeCharacterBreath breath))
                    breath = rect.gameObject.AddComponent<PsdHomeCharacterBreath>();
                breath.Reference = character;
            }
        }

        // Called by the in-page WebGL performance overlay with how far the clip has
        // taken over (0 = authored portrait, 1 = clip). The portrait fades out under
        // the clip and back in on stop, so the moving cutout replaces it in place
        // instead of drawing a second character over the first one.
        // rimHidden defaults to the portrait's; clips framed on the portrait keep the rim
        // while the figure holds the portrait's pose. While a clip is on screen the rim
        // stops bending with the idle breath, since the clip does not.
        public void SetCharacterPlayback(float playing, float rimHidden = -1f)
        {
            float visible = 1f - Mathf.Clamp01(playing);
            float rimVisible = 1f - Mathf.Clamp01(rimHidden < 0f ? playing : rimHidden);
            if (character != null && character.TryGetComponent(out Image portrait))
            {
                portrait.enabled = visible > 0f;
                portrait.color = new Color(1f, 1f, 1f, visible);
            }
            if (outline != null && outline.TryGetComponent(out Image rim))
            {
                rim.enabled = rimVisible > 0f;
                rim.color = new Color(1f, 1f, 1f, rimVisible);
                if (outline.TryGetComponent(out PsdHomeCharacterBreath breath)) breath.Amount = visible;
            }
        }

        // How far the idle bob and pointer parallax have moved the portrait from rest.
        public Vector2 CharacterOffset => character != null ? character.anchoredPosition - characterPosition : Vector2.zero;

        // The portrait's resting place in the stage (without the idle bob), for clips
        // that are framed on it.
        public bool TryGetCharacterRest(out Vector2 topLeft, out Vector2 size)
        {
            topLeft = characterPosition;
            size = character != null ? character.rect.size : Vector2.zero;
            return character != null;
        }

        public void SetOutline(RectTransform rect)
        {
            outline = rect;
            outlinePosition = rect.anchoredPosition;
            AttachBreath();
        }

        public void SetBackground(RectTransform rect)
        {
            background = rect;
            backgroundPosition = rect.anchoredPosition;
        }

        public void BuildDialogue()
        {
            // 2026-10-31 (赵金): Persona-style box — slanted teal name plate, translucent violet box, spinning record.
            dialogue = PsdHomeDialogue1031.Build(stage, font);
            bubble = dialogue.Group;
            speakerText = dialogue.Speaker;
            speakerText.text = SpeakerName(0);
            PsdHomeDialogue1031.FitTag(dialogue);
            bubbleText = dialogue.Line;
            // 2026-11-01: a tap on the shown box closes it early.
            if (dialogue.Dismiss != null) dialogue.Dismiss.Clicked = DismissDialogue;
        }

        /// <summary>Closes the shown line early (a tap on the box).</summary>
        public void DismissDialogue() { bubbleUntil = 0; }

        private void HoldBubble()
        {
            PsdHomeDialogue1031.FitLine(dialogue);
            bubbleUntil = Time.unscaledTime + PsdHomeDialogue1031.HoldSeconds(bubbleText.text);
        }

        // The browser also reports dblclick; the shared time gate avoids duplicate toggles.
        public void ToggleUiFromBrowser() { SceneClicked(2, false); }

        /// <summary>The lobby's eye button: hide every lobby control (or bring them back).</summary>
        public void ToggleUiHidden()
        {
            if (Time.unscaledTime - lastUiToggleAt < .16f) return;
            lastUiToggleAt = Time.unscaledTime;
            queuedDialogueAt = -1;
            UiHidden = !UiHidden;
            if (UiHidden) bubbleUntil = 0;
            PsdHomeStageBridge.SetUiHidden(UiHidden);
        }

        public void SceneClicked(int clickCount, bool characterHit)
        {
            if (clickCount >= 2 && clickCount % 2 == 0)
            {
                // Some input backends send both count 2 and count 4 for a single
                // physical double-click. A short gate prevents an instant undo.
                if (Time.unscaledTime - lastUiToggleAt < .16f) return;
                lastUiToggleAt = Time.unscaledTime;
                queuedDialogueAt = -1;
                UiHidden = !UiHidden;
                if (UiHidden) bubbleUntil = 0;
                PsdHomeStageBridge.SetUiHidden(UiHidden);
                return;
            }
            if (!UiHidden && characterHit) queuedDialogueAt = Time.unscaledTime + .27f;
        }

        // Lines (and the performance that goes with each) come in a random order,
        // never the same one twice in a row.
        private int NextLine()
        {
            int next = UnityEngine.Random.Range(0, Lines.Length - (lastLine >= 0 ? 1 : 0));
            if (lastLine >= 0 && next >= lastLine) next++;
            lastLine = next;
            return next;
        }

        public void DisplayDialogue(int index)
        {
            if (bubble == null || UiHidden) return;
            bubbleText.text = Lines[index % Lines.Length];
            dialogue?.Reveal?.Restart();
            HoldBubble();
        }

        /// <summary>Switches the bubble's name tag to the lobby idol now on screen.</summary>
        public void SetSpeaker(int character)
        {
            if (speakerText == null) return;
            speakerText.text = SpeakerName(character);
            PsdHomeDialogue1031.FitTag(dialogue);
            // 2026-11-01: each idol's box sits clear of her own face.
            PsdHomeDialogue1031.PlaceFor(dialogue, character);
        }

        /// <summary>One of Lilith's lines, never the same one twice in a row.</summary>
        /// <returns>The line index shown, or -1 when nothing was shown.</returns>
        public int DisplayLilithLine()
        {
            if (bubble == null || UiHidden) return -1;
            int next = UnityEngine.Random.Range(0, LilithLines.Length - (lilithLast >= 0 ? 1 : 0));
            if (lilithLast >= 0 && next >= lilithLast) next++;
            lilithLast = next;
            bubbleText.text = Personalize?.Invoke(LilithLines[next]) ?? LilithLines[next];
            dialogue?.Reveal?.Restart();
            HoldBubble();
            return next;
        }

        /// <summary>A line for the elf (2) or mermaid (3) idol, never the same one twice in a row.</summary>
        public int DisplayGuestLine(int character)
        {
            if (bubble == null || UiHidden) return -1;
            if (character >= SpeakerIds.Length)
            {
                bubbleText.text = (Personalize?.Invoke("{player}，今天也一起准备舞台吧。") ?? "一起准备舞台吧。");
                dialogue?.Reveal?.Restart(); HoldBubble();
                return -1;
            }
            string[] lines = GuestLines[Mathf.Clamp(character - 2, 0, GuestLines.Length - 1)];
            int next = UnityEngine.Random.Range(0, lines.Length);
            if (next == guestLast) next = (next + 1) % lines.Length;
            guestLast = next;
            bubbleText.text = Personalize?.Invoke(lines[next]) ?? lines[next];
            dialogue?.Reveal?.Restart();
            HoldBubble();
            return next;
        }

        public void CancelCharacterInteraction()
        {
            queuedDialogueAt = -1;
            bubbleUntil = 0;
            if (bubble != null) bubble.alpha = 0;
        }

        public void PlayClick(string action)
        {
            RectTransform hit = stage.Find(action) as RectTransform;
            if (hit == null) return;
            Vector3 world = hit.TransformPoint(hit.rect.center);
            Vector2 point = stage.InverseTransformPoint(world);
            // The album has its own ink impact; generic rings would obscure it.
            if (action == "AlbumProduction") return;
            PsdHomeStageBridge.PlayClick(action, point.x / stage.rect.width + .5f,
                .5f - point.y / stage.rect.height);
            Color tint;
            int shape;
            float size = 250;
            switch (action)
            {
                case "AlbumProduction": tint = new Color(.40f, 1f, .20f); shape = 1; size = 320; break;
                case "Tasks": tint = new Color(1f, .40f, .84f); shape = 2; break;
                case "PracticeRoom": tint = new Color(.28f, .94f, 1f); shape = 3; break;
                case "LiveOnStage": tint = new Color(.68f, .58f, 1f); shape = 4; size = 440; break;
                default: tint = new Color(.95f, .57f, 1f); shape = 0; size = 170; break;
            }
            Spawn(point, tint, shape, size, .62f);
            Spawn(point, Color.white, shape, size * .47f, .38f);
        }

        public void PlayStageTransition()
        {
            Vector2 center = new Vector2(0, 0);
            Spawn(center, new Color(.18f, .85f, 1f), 4, 1250, .78f);
            Spawn(center, new Color(.81f, .44f, 1f), 3, 960, .7f);
        }

        private void Spawn(Vector2 point, Color tint, int shape, float size, float duration)
        {
            GameObject gameObject = new GameObject("PsdClickWave", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(PsdHomePulse));
            gameObject.transform.SetParent(stage, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = point;
            Image image = gameObject.GetComponent<Image>();
            image.sprite = PsdHomePulse.Sprite(shape);
            image.color = tint;
            image.raycastTarget = false;
            gameObject.GetComponent<PsdHomePulse>().Begin(duration);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            Vector2 target = Vector2.zero;
            if (stage != null && Input.mousePresent &&
                RectTransformUtility.RectangleContainsScreenPoint(stage, Input.mousePosition))
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(stage, Input.mousePosition,
                    null, out Vector2 local);
                target = new Vector2(Mathf.Clamp(local.x / (stage.rect.width * .5f), -1f, 1f),
                    Mathf.Clamp(local.y / (stage.rect.height * .5f), -1f, 1f));
            }
            pointerOffset = Vector2.Lerp(pointerOffset, target, 1f - Mathf.Exp(-dt * 7f));
            if (character != null)
            {
                float wave = Mathf.Sin(now * 1.15f);
                character.anchoredPosition = characterPosition + new Vector2(
                    wave * .65f + pointerOffset.x * 2.7f,
                    Mathf.Sin(now * 1.42f) * 1.25f + pointerOffset.y * 2.0f);
                character.localScale = characterScale * (1f + .0025f * wave);
            }
            if (outline != null)
                outline.anchoredPosition = outlinePosition + pointerOffset * 2.4f;
            if (background != null)
                background.anchoredPosition = backgroundPosition - pointerOffset * 1.6f;
            if (queuedDialogueAt > 0 && now >= queuedDialogueAt)
            {
                queuedDialogueAt = -1;
                onDialogue?.Invoke(NextLine());
            }
            uiFade = Mathf.MoveTowards(uiFade, UiHidden ? 0 : 1, dt * 5f);
            foreach (CanvasGroup group in fadeOnly)
            {
                if (group == null) continue;
                group.alpha = uiFade;
                group.blocksRaycasts = !UiHidden;
            }
            foreach (CanvasGroup group in ui)
            {
                if (group == null) continue;
                group.alpha = uiFade;
                group.blocksRaycasts = !UiHidden;
                if (group.transform is RectTransform rect && uiPositions.TryGetValue(group, out Vector2 rest))
                {
                    float center = rest.x + rect.rect.width * .5f;
                    float direction = center < stage.rect.width * .5f ? -1f : 1f;
                    float distance = 72f + Mathf.Min(70f, rect.rect.width * .12f);
                    rect.anchoredPosition = rest + Vector2.right * (direction * distance * (1f - uiFade));
                }
            }
            if (bubble != null)
            {
                bool showing = !UiHidden && now < bubbleUntil;
                bubble.alpha = Mathf.MoveTowards(bubble.alpha, showing ? 1f : 0f, dt * 6f);
                // Only a shown box takes taps (to close it); a hidden one never blocks the art or buttons.
                bubble.blocksRaycasts = showing && bubble.alpha > .05f;
            }
        }
    }

    /// <summary>Two restrained heartbeats move the authored CTA face and glow as one unit.</summary>
    public sealed class PsdHomeCallToActionBeat : MonoBehaviour
    {
        private RectTransform rect;

        private void Awake() { rect = (RectTransform)transform; }

        public static float Energy(float seconds)
        {
            float phase = Mathf.Repeat(seconds, 1.12f) / 1.12f;
            float first = Mathf.Exp(-.5f * Mathf.Pow((phase - .18f) / .065f, 2f));
            float second = Mathf.Exp(-.5f * Mathf.Pow((phase - .36f) / .052f, 2f));
            return Mathf.Clamp01(first + second * .52f);
        }

        private void Update()
        {
            float beat = Energy(Time.unscaledTime);
            rect.localScale = Vector3.one * (1f + .052f * beat);
            rect.anchoredPosition = new Vector2(0, beat * 2.4f);
        }
    }

    /// <summary>The halo follows the moving CTA face instead of pulsing beneath static art.</summary>
    public sealed class PsdHomeBeatEcho : MonoBehaviour
    {
        private Image image;
        private RectTransform rect;

        private void Awake()
        {
            image = GetComponent<Image>();
            rect = (RectTransform)transform;
        }

        private void Update()
        {
            float beat = PsdHomeCallToActionBeat.Energy(Time.unscaledTime);
            image.color = new Color(1f, .83f, 1f, .08f + beat * .25f);
            rect.localScale = Vector3.one * (1.02f + beat * .055f);
        }
    }

    /// <summary>A restrained rotating light behind the separated character art.</summary>
    public sealed class PsdHomeBackdropOrbit : MonoBehaviour
    {
        private Image image;
        private RectTransform rect;

        private void Awake()
        {
            image = GetComponent<Image>();
            rect = (RectTransform)transform;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            rect.localRotation = Quaternion.Euler(0f, 0f, now * 7f);
            rect.localScale = Vector3.one * (1f + .025f * Mathf.Sin(now * 1.55f));
            image.color = new Color(.52f, .85f, 1f, .09f + .035f * Mathf.Sin(now * 1.8f));
        }
    }

    public sealed class PsdHomeSceneTouch : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private PsdHomeMotion motion;
        private Vector2 dragStart;
        private bool dragged;
        // Touch input modules drop pointer data on every release, so eventData.clickCount is
        // always 1 on phones (and mobile browsers): count double taps here instead.
        private const float DoubleTapSeconds = .45f;
        private float lastTapTime = -10f;
        private Vector2 lastTapPosition;
        private int tapCount;
        public void Initialize(PsdHomeMotion owner) { motion = owner; }
        public void OnPointerDown(PointerEventData eventData)
        {
            dragged = false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                eventData.position, eventData.pressEventCamera, out dragStart);
        }
        public void OnBeginDrag(PointerEventData eventData) { dragged = true; motion.CancelCharacterInteraction(); }
        public void OnDrag(PointerEventData eventData) { }
        public void OnEndDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                eventData.position, eventData.pressEventCamera, out var end);
            var delta = end - dragStart;
            if (Mathf.Abs(delta.x) >= 55 && Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.35f)
                motion.GetComponent<PsdHomeCharacterCarousel>()?.Switch(delta.x < 0 ? 1 : -1);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (motion == null || dragged || motion.GetComponent<PsdHomeCharacterCarousel>()?.Switching == true) return;
            RectTransform surface = (RectTransform)transform;
            bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,
                eventData.position, eventData.pressEventCamera, out Vector2 point);
            float x = point.x / surface.rect.width;
            float y = -point.y / surface.rect.height;
            // The PSD's character layer includes transparent full-screen padding.
            // The interactive silhouette sits in this central region; buttons
            // above this surface keep their authored hit rectangles.
            bool characterHit = inside && x >= .21f && x <= .82f && y >= .16f && y <= .80f;
            float now = Time.unscaledTime;
            bool chained = now - lastTapTime <= DoubleTapSeconds && (eventData.position - lastTapPosition).magnitude <= 80f;
            tapCount = chained ? tapCount + 1 : 1;
            lastTapTime = now;
            lastTapPosition = eventData.position;
            motion.SceneClicked(Mathf.Max(tapCount, eventData.clickCount), characterHit);
        }
    }

    /// <summary>Irregular annulus and short rays, drawn once and reused by every click pulse.</summary>
    public sealed class PsdHomePulse : MonoBehaviour
    {
        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();
        private Image image;
        private RectTransform rect;
        private Color tint;
        private float began, duration;

        public static Sprite Sprite(int shape)
        {
            if (Cache.TryGetValue(shape, out Sprite existing)) return existing;
            const int length = 128;
            Texture2D texture = new Texture2D(length, length, TextureFormat.RGBA32, false);
            texture.name = "PsdPulse-" + shape;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[length * length];
            for (int y = 0; y < length; y++)
            for (int x = 0; x < length; x++)
            {
                float dx = (x - 63.5f) / 64f, dy = (y - 63.5f) / 64f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                int lobes = shape == 1 ? 9 : shape == 2 ? 5 : shape == 3 ? 12 : shape == 4 ? 8 : 0;
                float edge = .72f + (lobes == 0 ? 0 : .075f * Mathf.Sin(angle * lobes)) +
                    (shape == 1 ? .035f * Mathf.Sin(angle * 17f + 1.1f) : 0);
                float band = Mathf.Clamp01((radius - edge + .10f) * 90f) * Mathf.Clamp01((edge - radius) * 90f);
                float ray = lobes > 0 ? Mathf.Pow(Mathf.Max(0, Mathf.Cos(angle * lobes)), 20f) *
                    Mathf.Clamp01((radius - edge) * 15f) * Mathf.Clamp01((1f - radius) * 12f) : 0;
                float core = Mathf.Clamp01((.18f - radius) * 18f) * .28f;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Max(band, ray * .85f, core)) * 255);
                pixels[y * length + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, length, length), new Vector2(.5f, .5f), 100);
            Cache[shape] = sprite;
            return sprite;
        }

        public void Begin(float seconds)
        {
            image = GetComponent<Image>();
            rect = (RectTransform)transform;
            tint = image.color;
            began = Time.unscaledTime;
            duration = seconds;
            rect.localScale = Vector3.one * .18f;
        }

        private void Update()
        {
            if (image == null) return;
            float t = Mathf.Clamp01((Time.unscaledTime - began) / duration);
            float expansion = 1f - Mathf.Pow(1f - t, 3f);
            rect.localScale = Vector3.one * Mathf.Lerp(.18f, 1.28f, expansion);
            image.color = new Color(tint.r, tint.g, tint.b, (1f - t) * (1f - t) * tint.a);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
