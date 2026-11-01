using System;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Small, interruptible manual-release prompt. No energy or combat state is owned here.</summary>
    public sealed class BattleUltimateTimingView1019 : MonoBehaviour
    {
        private RectTransform root, disc, soul, bar, marker;
        private Image discImage, soulImage;
        private Text prompt, feedback;
        private Button hit;
        private Func<bool> paused;
        private Action<UltimateTimingGrade> completed;
        private UltimateTimingChallenge challenge;
        private Vector2 start;
        private float resolvedTime;
        private string bonusLabel = "伤害";
        public bool IsActive => challenge != null;

        public void Configure(Transform host, Font font, Func<bool> isPaused)
        {
            paused = isPaused;
            root = NewRect("ManualUltimateTiming", host);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            root.pivot = Vector2.one * .5f;
            discImage = NewImage("DiscZoom", root, Color.white); disc = discImage.rectTransform;
            soulImage = NewImage("OriginalCharacterSoul", root, new Color(.55f, .9f, 1f, .35f)); soul = soulImage.rectTransform;
            soulImage.preserveAspect = true;
            Centre(soul, new Vector2(130, 100), new Vector2(250, 340));
            bar = NewRect("TimingBar", root);
            bar.anchorMin = bar.anchorMax = new Vector2(.5f, .35f); bar.pivot = Vector2.one * .5f;
            bar.sizeDelta = new Vector2(440, 144);
            var plate = bar.gameObject.AddComponent<Image>(); plate.color = new Color32(14, 8, 34, 248); plate.raycastTarget = true;
            hit = bar.gameObject.AddComponent<Button>(); hit.targetGraphic = plate; hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(Hit);
            prompt = Label("Prompt", bar, font, "点击定格 · 大招增幅", 19, new Rect(10, 10, 420, 30));
            var track = NewImage("Track", bar, new Color32(46, 30, 76, 255)); Place(track.rectTransform, new Rect(24, 55, 392, 20));
            var good = NewImage("GoodWindow", track.transform, new Color32(119, 65, 225, 255));
            Place(good.rectTransform, new Rect(392 * (UltimateTimingChallenge.Target - UltimateTimingChallenge.GoodHalfWidth), 0,
                392 * UltimateTimingChallenge.GoodHalfWidth * 2, 20));
            var perfect = NewImage("PerfectWindow", track.transform, new Color32(255, 61, 168, 255));
            Place(perfect.rectTransform, new Rect(392 * (UltimateTimingChallenge.Target - UltimateTimingChallenge.PerfectHalfWidth), 0,
                392 * UltimateTimingChallenge.PerfectHalfWidth * 2, 20));
            marker = NewImage("TimingNeedle", track.transform, Color.white).rectTransform;
            Place(marker, new Rect(0, -6, 4, 32));
            feedback = Label("Feedback", bar, font, "GOOD +5%  /  PERFECT +10%", 15, new Rect(10, 92, 420, 30));
            root.gameObject.SetActive(false);
        }

        public bool Begin(Sprite album, Sprite originalPortrait, RectTransform sourceDisc, Action<UltimateTimingGrade> onComplete,
            string effectLabel = "伤害")
        {
            if (root == null || IsActive || onComplete == null) return false;
            completed = onComplete; challenge = new UltimateTimingChallenge(); resolvedTime = 0;
            bonusLabel = effectLabel;
            discImage.sprite = album; discImage.preserveAspect = true; discImage.enabled = album != null;
            soulImage.sprite = originalPortrait; soulImage.enabled = originalPortrait != null;
            start = sourceDisc != null ? (Vector2)root.InverseTransformPoint(sourceDisc.TransformPoint(sourceDisc.rect.center))
                : new Vector2(0, -root.rect.height * .3f);
            prompt.text = "点击定格 · 大招" + bonusLabel; feedback.text = "GOOD +5%  /  PERFECT +10%";
            feedback.color = Color.white; hit.interactable = true;
            root.gameObject.SetActive(true); root.SetAsLastSibling(); Pose(); return true;
        }

        public void Hit()
        {
            if (!IsActive || challenge.Resolved || (paused?.Invoke() ?? false)) return;
            challenge.Hit(); ShowGrade();
        }

        private void ShowGrade()
        {
            hit.interactable = false;
            feedback.text = challenge.Grade == UltimateTimingGrade.Perfect ? "PERFECT · 本次" + bonusLabel + " +10%"
                : challenge.Grade == UltimateTimingGrade.Good ? "GOOD · 本次" + bonusLabel + " +5%" : "正常释放";
            feedback.color = challenge.Grade == UltimateTimingGrade.Perfect ? new Color32(255, 87, 185, 255) : Color.white;
        }

        private void Update() => Advance(Mathf.Min(Time.unscaledDeltaTime, .1f));
        public void Advance(float activeSeconds)
        {
            if (!IsActive || activeSeconds <= 0 || (paused?.Invoke() ?? false)) return;
            bool wasResolved = challenge.Resolved;
            challenge.Advance(activeSeconds);
            if (!wasResolved && challenge.Resolved) ShowGrade();
            if (challenge.Resolved)
            {
                resolvedTime += activeSeconds;
                if (resolvedTime >= .16f)
                {
                    var callback = completed; var grade = challenge.Grade; Cancel(); callback?.Invoke(grade); return;
                }
            }
            Pose();
        }

        private void Pose()
        {
            float t = Mathf.Clamp01(challenge.Elapsed / .24f);
            float ease = 1 - Mathf.Pow(1 - t, 3);
            Vector2 destination = new Vector2(-85, root.rect.height * .06f);
            bool reduced = BattlePresentationOptions.ReduceMotion;
            Centre(disc, reduced ? destination : Vector2.Lerp(start, destination, ease),
                Vector2.one * (reduced ? 160 : Mathf.Lerp(86, 230, ease)));
            disc.localEulerAngles = new Vector3(0, 0, reduced ? 0 : ease * 130);
            discImage.color = new Color(1, 1, 1, .9f);
            soulImage.color = new Color(.6f, .9f, 1f, reduced ? .3f : ease * .38f);
            marker.anchoredPosition = new Vector2(challenge.Progress * 392 - 2, 6);
        }

        public void Cancel()
        {
            challenge?.Cancel(); challenge = null; completed = null;
            if (root != null) root.gameObject.SetActive(false);
        }
        private void OnDisable() => Cancel();
        private void OnDestroy() { Cancel(); if (root != null) Destroy(root.gameObject); }
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        private static Image NewImage(string name, Transform parent, Color color)
        {
            var image = NewRect(name, parent).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private static void Centre(RectTransform r, Vector2 position, Vector2 size)
        { r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f; r.anchoredPosition = position; r.sizeDelta = size; }
        private static void Place(RectTransform r, Rect bounds)
        { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(bounds.x, -bounds.y); r.sizeDelta = bounds.size; }
        private static Text Label(string name, Transform parent, Font font, string value, int size, Rect bounds)
        {
            var label = NewRect(name, parent).gameObject.AddComponent<Text>(); Place(label.rectTransform, bounds);
            label.font = font; label.fontSize = size; label.fontStyle = FontStyle.Bold; label.color = Color.white;
            label.text = value; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
    }
}
