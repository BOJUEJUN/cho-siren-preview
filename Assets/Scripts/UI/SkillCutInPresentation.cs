using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>A bounded, presentation-only queue for player ultimate-skill portraits.</summary>
    [DisallowMultipleComponent]
    public sealed class SkillCutInPresentation : MonoBehaviour
    {
        private const int QueueCapacity = 4;
        private const float ExpirySeconds = 6f;
        private const float EnterSeconds = .16f;
        private const float HoldSeconds = 1.05f;
        private const float ExitSeconds = .18f;
        private const float TotalSeconds = EnterSeconds + HoldSeconds + ExitSeconds;
        private const float VisibleX = 24f;
        private const float HiddenX = -536f;

        private sealed class Entry
        {
            public Sprite Portrait;
            public string Actor, Skill;
            public Color Accent;
            public float EnqueuedAt;
        }

        private readonly Queue<Entry> pending = new Queue<Entry>();
        private PanelKit kit;
        private RectTransform banner;
        private CanvasGroup group;
        private Image portrait, accentRail;
        private Outline outline;
        private Text actorName, skillName;
        private Func<bool> pauseProbe;
        private Func<int> speedProbe;
        private Func<bool> reduceMotionProbe;
        private float clock, elapsed;
        private bool configured;

        /// <summary>Waiting cut-ins, excluding the one currently visible.</summary>
        public int PendingCount => pending.Count;
        public bool IsShowing { get; private set; }

        public void Configure(Transform host, Func<bool> paused, Func<int> speed,
            Func<bool> reduceMotion = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            Cancel();
            pauseProbe = paused;
            speedProbe = speed;
            reduceMotionProbe = reduceMotion;
            if (banner == null) Build(host);
            else banner.SetParent(host, false);
            clock = 0f;
            configured = true;
        }

        /// <summary>The caller filters for player ultimates; basics/small skills stay on their actor.</summary>
        public void Enqueue(Sprite image, string actor, string skill, Color accent)
        {
            if (!configured || !isActiveAndEnabled || string.IsNullOrWhiteSpace(skill)) return;
            // Reduced motion keeps the damage/energy result and skips only the portrait banner.
            if (ReduceMotion()) return;
            // Keep recent meaningful actions, never interrupt the cut-in being read.
            while (pending.Count >= QueueCapacity) pending.Dequeue();
            pending.Enqueue(new Entry
            {
                Portrait = image, Actor = actor ?? string.Empty, Skill = skill,
                Accent = accent, EnqueuedAt = clock,
            });
            if (!IsShowing && !IsPaused()) ShowNext();
        }

        public void Cancel()
        {
            pending.Clear();
            IsShowing = false;
            elapsed = 0f;
            if (group != null) group.alpha = 0f;
            if (banner != null) banner.gameObject.SetActive(false);
        }

        private void Update() => AdvancePresentation(Time.unscaledDeltaTime);

        /// <summary>Advances visual state only; never changes simulator time, health or cooldowns.</summary>
        public void AdvancePresentation(float unscaledSeconds)
        {
            if (!configured || !isActiveAndEnabled || IsPaused() || unscaledSeconds <= 0f ||
                float.IsNaN(unscaledSeconds) || float.IsInfinity(unscaledSeconds)) return;
            if (ReduceMotion())
            {
                if (IsShowing || pending.Count > 0) Cancel();
                return;
            }
            clock += unscaledSeconds;
            if (!IsShowing) { ShowNext(); return; }
            // Cap visual acceleration to keep the hold >= 0.7 real seconds even at 2x combat.
            elapsed += unscaledSeconds * Mathf.Clamp(speedProbe != null ? speedProbe() : 1, 1f, 1.5f);
            if (elapsed >= TotalSeconds)
            {
                IsShowing = false;
                banner.gameObject.SetActive(false);
                ShowNext();
                return;
            }
            ApplyPose();
        }

        private bool IsPaused() => pauseProbe != null && pauseProbe();
        private bool ReduceMotion() => reduceMotionProbe != null && reduceMotionProbe();

        private void ShowNext()
        {
            while (pending.Count > 0)
            {
                Entry entry = pending.Dequeue();
                if (clock - entry.EnqueuedAt > ExpirySeconds) continue;
                portrait.sprite = entry.Portrait;
                portrait.enabled = entry.Portrait != null;
                actorName.text = entry.Actor;
                actorName.color = entry.Accent;
                skillName.text = entry.Skill;
                accentRail.color = entry.Accent;
                Color edge = entry.Accent;
                edge.a = .65f;
                outline.effectColor = edge;
                elapsed = 0f;
                IsShowing = true;
                banner.gameObject.SetActive(true);
                ApplyPose();
                return;
            }
            group.alpha = 0f;
        }

        private void ApplyPose()
        {
            float visibility;
            if (elapsed < EnterSeconds)
                visibility = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / EnterSeconds), 3f);
            else if (elapsed < EnterSeconds + HoldSeconds) visibility = 1f;
            else visibility = 1f - Mathf.SmoothStep(0f, 1f,
                (elapsed - EnterSeconds - HoldSeconds) / ExitSeconds);
            banner.anchoredPosition = new Vector2(Mathf.Lerp(HiddenX, VisibleX, visibility), -735f);
            group.alpha = visibility;
        }

        private void Build(Transform host)
        {
            kit = new PanelKit("SkillCutIn");
            GameObject root = kit.NewPanel("SkillCutInBanner", host, new Color32(14, 17, 40, 244), 16);
            banner = root.GetComponent<RectTransform>();
            PanelKit.PlaceTop(banner, HiddenX, 735, 520, 88);
            group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            outline = kit.AddOutline(root, new Color32(117, 180, 245, 160), 1.5f);
            accentRail = kit.NewImage("SkillCutInAccent", banner, null, Color.white);
            PanelKit.PlaceTop(accentRail.rectTransform, 0, 12, 4, 64);
            portrait = kit.NewImage("SkillCutInPortrait", banner, null, Color.white);
            PanelKit.PlaceTop(portrait.rectTransform, 14, -12, 96, 100);
            portrait.preserveAspect = true;
            portrait.useSpriteMesh = true;
            actorName = kit.NewPlacedText(banner, string.Empty, 18, PanelKit.Cyan,
                124, 7, 378, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            actorName.gameObject.name = "SkillCutInActor";
            skillName = kit.NewPlacedText(banner, string.Empty, 26, PanelKit.White,
                124, 36, 378, 43, TextAnchor.MiddleLeft, FontStyle.Bold);
            skillName.gameObject.name = "SkillCutInSkill";
            PanelKit.EnableBestFit(actorName, 16);
            PanelKit.EnableBestFit(skillName, 20);
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            root.SetActive(false);
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            Cancel();
            if (banner != null) Destroy(banner.gameObject);
            kit?.Dispose();
        }
    }
}
