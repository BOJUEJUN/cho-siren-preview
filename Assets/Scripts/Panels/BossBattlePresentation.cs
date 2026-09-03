using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Event-driven 2.5D presentation for a single illustrated boss.  The source art stays intact;
    /// pose, echo, stage light, shockwave, slash and damage-number layers create readable combat
    /// feedback without native plugins, skeletal authoring or platform-specific video playback.
    /// </summary>
    public sealed class BossBattlePresentation : MonoBehaviour
    {
        public enum BossVisualState
        {
            Idle,
            Charging,
            Hit,
            LowHealth,
            Defeated,
            VictoryPose,
        }

        private RectTransform motionRoot;
        private Image portrait;
        private Image echo;
        private Image auraBack;
        private Image auraCore;
        private Image shadow;
        private Image stagePulse;
        private Image lowHealthVignette;
        private Image hitSlashArt;
        private Image heartImpactArt;
        private Image chargeAuraArt;
        private Text stateLabel;
        private Image[] rings = Array.Empty<Image>();
        private Image[] slashTrails = Array.Empty<Image>();
        private Text[] damageTexts = Array.Empty<Text>();
        private Func<bool> pauseProbe;
        private Func<int> speedProbe;

        private Vector2 basePosition;
        private Vector2 actionOffset;
        private float actionScale = 1f;
        private float actionRotation;
        private float animationClock;
        private float healthRatio = 1f;
        private bool configured;
        private bool outcomeLocked;
        private int reactionVersion;
        private int damageCursor;
        private Coroutine reactionRoutine;
        private readonly Coroutine[] damageRoutines = new Coroutine[6];

        public BossVisualState State { get; private set; } = BossVisualState.Idle;
        public bool LowHealth => healthRatio <= 0.3f && healthRatio > 0f;
        public int HitReactionCount { get; private set; }
        public int ChargeReactionCount { get; private set; }
        public int PhaseReactionCount { get; private set; }
        public int OutcomeReactionCount { get; private set; }
        public RectTransform MotionRoot => motionRoot;

        public void Configure(RectTransform animatedRoot, Image mainPortrait, Image echoPortrait,
            Image rearAura, Image coreAura, Image groundShadow, Image pulse, Image dangerVignette,
            Image aiHitSlash, Image aiHeartImpact, Image aiChargeAura, Text status,
            Image[] effectRings, Image[] streaks, Text[] floatingDamage,
            Func<bool> isPaused = null, Func<int> currentSpeed = null)
        {
            motionRoot = animatedRoot;
            portrait = mainPortrait;
            echo = echoPortrait;
            auraBack = rearAura;
            auraCore = coreAura;
            shadow = groundShadow;
            stagePulse = pulse;
            lowHealthVignette = dangerVignette;
            hitSlashArt = aiHitSlash;
            heartImpactArt = aiHeartImpact;
            chargeAuraArt = aiChargeAura;
            stateLabel = status;
            rings = effectRings ?? Array.Empty<Image>();
            slashTrails = streaks ?? Array.Empty<Image>();
            damageTexts = floatingDamage ?? Array.Empty<Text>();
            pauseProbe = isPaused;
            speedProbe = currentSpeed;

            if (motionRoot == null || portrait == null) return;
            basePosition = motionRoot.anchoredPosition;
            ResetTransientVisuals();
            configured = true;
        }

        public void SetHealthRatio(float normalized)
        {
            healthRatio = Mathf.Clamp01(normalized);
            if (outcomeLocked) return;
            if (reactionRoutine == null)
                State = LowHealth ? BossVisualState.LowHealth : BossVisualState.Idle;

            if (stateLabel != null && reactionRoutine == null)
                SetStateLabel(LowHealth ? "危险 · 终曲暴走" : string.Empty,
                    new Color32(255, 86, 173, 255), LowHealth ? 1f : 0f);
        }

        public void PlayHit(int amount, bool critical)
        {
            if (!configured || outcomeLocked) return;
            HitReactionCount++;
            StartReaction(HitRoutine(critical));
            SpawnDamageNumber(amount, critical);
        }

        public void PlayCharge(string skillName)
        {
            if (!configured || outcomeLocked) return;
            ChargeReactionCount++;
            StartReaction(ChargeRoutine(string.IsNullOrEmpty(skillName) ? "终曲" : skillName));
        }

        public void PlayPhaseSurge(int phase)
        {
            if (!configured || outcomeLocked) return;
            PhaseReactionCount++;
            StartReaction(PhaseRoutine(Mathf.Clamp(phase, 2, 3)));
        }

        public void PlayOutcome(bool playerVictory)
        {
            if (!configured || outcomeLocked) return;
            outcomeLocked = true;
            OutcomeReactionCount++;
            StopReaction();
            reactionVersion++;
            reactionRoutine = StartCoroutine(playerVictory ? DefeatRoutine() : VictoryRoutine());
        }

        private void Update()
        {
            if (!configured || IsPaused()) return;
            animationClock += Time.unscaledDeltaTime;
            ApplyCompositePose();
        }

        private void ApplyCompositePose()
        {
            float lowWeight = LowHealth && !outcomeLocked ? 1f : 0f;
            float breathe = Mathf.Sin(animationClock * (1.55f + lowWeight * 0.75f));
            float drift = Mathf.Sin(animationClock * 0.71f + 0.8f);
            float idleWeight = outcomeLocked ? 0.18f : 1f;
            float idleY = (4.5f + lowWeight * 2.5f) * breathe * idleWeight;
            float idleX = 1.8f * drift * idleWeight;
            float idleScale = 1f + breathe * (0.006f + lowWeight * 0.004f) * idleWeight;
            float idleRotation = drift * 0.42f * idleWeight;

            motionRoot.anchoredPosition = basePosition + new Vector2(idleX, idleY) + actionOffset;
            motionRoot.localScale = Vector3.one * (idleScale * actionScale);
            motionRoot.localEulerAngles = new Vector3(0f, 0f, idleRotation + actionRotation);

            float pulse = 0.5f + Mathf.Sin(animationClock * (2.2f + lowWeight * 1.2f)) * 0.5f;
            if (auraBack != null)
            {
                float scale = Mathf.Lerp(0.96f, 1.055f + lowWeight * 0.025f, pulse);
                auraBack.rectTransform.localScale = Vector3.one * scale;
                Color color = Color.Lerp(new Color32(95, 71, 255, 34),
                    lowWeight > 0f ? new Color32(255, 35, 133, 116) : new Color32(255, 72, 212, 84), pulse);
                color.a *= outcomeLocked ? 0.55f : 1f;
                auraBack.color = color;
                auraBack.rectTransform.localEulerAngles = new Vector3(0f, 0f, animationClock * 3.5f);
            }

            if (auraCore != null)
            {
                auraCore.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.04f, pulse);
                Color core = lowWeight > 0f
                    ? new Color32(255, 45, 139, (byte)Mathf.RoundToInt(Mathf.Lerp(26f, 74f, pulse)))
                    : new Color32(114, 102, 255, (byte)Mathf.RoundToInt(Mathf.Lerp(22f, 54f, pulse)));
                auraCore.color = core;
            }

            if (stagePulse != null)
            {
                Color color = stagePulse.color;
                color.a = (outcomeLocked ? 0.05f : 0.08f) + pulse * (0.10f + lowWeight * 0.08f);
                stagePulse.color = color;
                stagePulse.rectTransform.localScale = new Vector3(Mathf.Lerp(0.92f, 1.08f, pulse),
                    Mathf.Lerp(0.96f, 1.04f, pulse), 1f);
            }

            if (shadow != null)
            {
                shadow.rectTransform.localScale = new Vector3(Mathf.Lerp(1.06f, 0.94f, pulse),
                    Mathf.Lerp(0.9f, 1.05f, pulse), 1f);
                Color color = shadow.color;
                color.a = Mathf.Lerp(0.25f, 0.48f, pulse);
                shadow.color = color;
            }

            if (lowHealthVignette != null)
            {
                Color danger = lowHealthVignette.color;
                float warningPulse = 0.5f + Mathf.Sin(animationClock * 5.4f) * 0.5f;
                danger.a = lowWeight * Mathf.Lerp(0.08f, 0.25f, warningPulse);
                lowHealthVignette.color = danger;
            }
        }

        private void StartReaction(IEnumerator routine)
        {
            StopReaction();
            int version = ++reactionVersion;
            reactionRoutine = StartCoroutine(RunReaction(version, routine));
        }

        private IEnumerator RunReaction(int version, IEnumerator routine)
        {
            while (version == reactionVersion && routine.MoveNext()) yield return routine.Current;
            if (version != reactionVersion) yield break;
            reactionRoutine = null;
            ResetActionPose();
            State = LowHealth ? BossVisualState.LowHealth : BossVisualState.Idle;
            SetStateLabel(LowHealth ? "危险 · 终曲暴走" : string.Empty,
                new Color32(255, 86, 173, 255), LowHealth ? 1f : 0f);
        }

        private void StopReaction()
        {
            if (reactionRoutine != null) StopCoroutine(reactionRoutine);
            reactionRoutine = null;
            ResetActionPose();
        }

        private IEnumerator HitRoutine(bool critical)
        {
            State = BossVisualState.Hit;
            SetStateLabel(critical ? "暴击 · 失衡" : "受击", new Color32(255, 188, 232, 255), 1f);
            float strength = critical ? 1.35f : 1f;
            ShowSlashTrails(strength);
            ShowRings(new Color32(255, 73, 205, 220), 0.55f);
            ShowAiHitArt(critical);

            float elapsed = 0f;
            const float duration = 0.54f;
            while (elapsed < duration)
            {
                elapsed += AnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                float recoil = Mathf.Sin(Mathf.Clamp01(t / 0.25f) * Mathf.PI * 0.5f);
                float settle = Mathf.Clamp01((1f - t) / 0.75f);
                float shake = Mathf.Sin(t * Mathf.PI * 9f) * settle;
                actionOffset = new Vector2((20f * recoil + 10f * shake) * strength,
                    (8f * recoil + 3f * Mathf.Sin(t * Mathf.PI * 6f)) * strength);
                actionRotation = (4.6f * recoil + 2.2f * shake) * strength;
                actionScale = 1f - 0.026f * recoil + 0.012f * Mathf.Sin(t * Mathf.PI);

                float flash = 1f - Mathf.Clamp01(t / 0.34f);
                portrait.color = Color.Lerp(Color.white, new Color32(255, 142, 220, 255), flash * 0.48f);
                if (echo != null)
                {
                    echo.rectTransform.localPosition = new Vector3(-actionOffset.x * 0.7f, 3f, 0f);
                    echo.rectTransform.localScale = Vector3.one * (1.01f + flash * 0.055f);
                    echo.color = new Color32(255, 38, 184, (byte)Mathf.RoundToInt(130f * flash));
                }

                AnimateTransientEffects(t, strength);
                AnimateAiHitArt(t, strength);
                yield return null;
            }
        }

        private IEnumerator ChargeRoutine(string skillName)
        {
            State = BossVisualState.Charging;
            SetStateLabel("蓄力 · " + skillName, new Color32(255, 153, 226, 255), 1f);
            ShowRings(new Color32(116, 198, 255, 210), 0.42f);
            ShowChargeArt();
            float elapsed = 0f;
            // The panel grants an enemy a two-beat anticipation window. Keep the charge alive
            // for most of that window so the Boss does not snap back to idle long before impact.
            const float duration = 1.65f;
            while (elapsed < duration)
            {
                elapsed += AnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                float gather = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.62f));
                float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, t));
                actionScale = 1f - gather * 0.035f + release * 0.085f;
                actionOffset = new Vector2(0f, -gather * 10f + release * 16f);
                actionRotation = Mathf.Sin(t * Mathf.PI * 4f) * gather * 1.4f;
                portrait.color = Color.Lerp(Color.white, new Color32(196, 204, 255, 255), gather * 0.28f);
                AnimateChargeRings(t, gather, release);
                AnimateChargeArt(t, gather, release);
                yield return null;
            }
        }

        private IEnumerator PhaseRoutine(int phase)
        {
            State = BossVisualState.Charging;
            SetStateLabel(phase >= 3 ? "最终乐章 · 全频共振" : "转调 · 力量解放",
                phase >= 3 ? new Color32(255, 92, 190, 255) : new Color32(117, 222, 255, 255), 1f);
            ShowRings(phase >= 3 ? new Color32(255, 43, 166, 235) : new Color32(83, 215, 255, 220), 0.36f);
            ShowChargeArt();

            float elapsed = 0f;
            float duration = phase >= 3 ? 1f : 0.82f;
            while (elapsed < duration)
            {
                elapsed += AnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(t * Mathf.PI);
                actionScale = 1f + wave * (phase >= 3 ? 0.105f : 0.072f);
                actionOffset = new Vector2(0f, wave * 18f);
                actionRotation = Mathf.Sin(t * Mathf.PI * 2f) * wave * 1.8f;
                portrait.color = Color.Lerp(Color.white,
                    phase >= 3 ? new Color32(255, 125, 214, 255) : new Color32(134, 226, 255, 255), wave * 0.34f);
                AnimateChargeRings(t, wave, t);
                AnimateChargeArt(t, wave, t);
                yield return null;
            }
        }

        private IEnumerator DefeatRoutine()
        {
            State = BossVisualState.Defeated;
            SetStateLabel("演出终止", new Color32(186, 205, 255, 255), 1f);
            ShowRings(new Color32(112, 191, 255, 190), 0.42f);
            ShowAiHitArt(true);
            float elapsed = 0f;
            const float duration = 0.88f;
            while (elapsed < duration)
            {
                elapsed += AnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                actionOffset = new Vector2(34f * eased, -42f * eased);
                actionRotation = 9f * eased;
                actionScale = Mathf.Lerp(1f, 0.92f, eased);
                Color color = portrait.color;
                color.r = Mathf.Lerp(1f, 0.42f, eased);
                color.g = Mathf.Lerp(1f, 0.49f, eased);
                color.b = Mathf.Lerp(1f, 0.72f, eased);
                color.a = Mathf.Lerp(1f, 0.32f, eased);
                portrait.color = color;
                if (echo != null)
                    echo.color = new Color32(76, 212, 255, (byte)Mathf.RoundToInt(120f * (1f - t)));
                AnimateTransientEffects(t, 1.25f);
                AnimateAiHitArt(t, 1.25f);
                yield return null;
            }
            reactionRoutine = null;
        }

        private IEnumerator VictoryRoutine()
        {
            State = BossVisualState.VictoryPose;
            SetStateLabel("安可 · 绝对压制", new Color32(255, 118, 211, 255), 1f);
            ShowRings(new Color32(255, 54, 190, 230), 0.35f);
            ShowChargeArt();
            float elapsed = 0f;
            const float duration = 0.88f;
            while (elapsed < duration)
            {
                elapsed += AnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                float flourish = Mathf.Sin(t * Mathf.PI);
                actionOffset = new Vector2(0f, flourish * 24f + t * 8f);
                actionScale = 1f + flourish * 0.09f + t * 0.025f;
                actionRotation = Mathf.Sin(t * Mathf.PI * 2f) * flourish * 1.4f;
                portrait.color = Color.Lerp(Color.white, new Color32(255, 181, 231, 255), flourish * 0.24f);
                AnimateChargeRings(t, flourish, t);
                AnimateChargeArt(t, flourish, t);
                yield return null;
            }
            reactionRoutine = null;
        }

        private void ShowRings(Color color, float initialScale)
        {
            for (int index = 0; index < rings.Length; index++)
            {
                if (rings[index] == null) continue;
                rings[index].gameObject.SetActive(true);
                rings[index].color = color;
                rings[index].rectTransform.localScale = Vector3.one * (initialScale + index * 0.12f);
            }
        }

        private void ShowSlashTrails(float strength)
        {
            for (int index = 0; index < slashTrails.Length; index++)
            {
                if (slashTrails[index] == null) continue;
                slashTrails[index].gameObject.SetActive(true);
                slashTrails[index].color = index % 2 == 0
                    ? new Color32(255, 225, 250, (byte)Mathf.RoundToInt(235f * Mathf.Min(1f, strength)))
                    : new Color32(93, 220, 255, (byte)Mathf.RoundToInt(210f * Mathf.Min(1f, strength)));
                slashTrails[index].rectTransform.localScale = new Vector3(0.05f, 1f, 1f);
            }
        }

        private void ShowAiHitArt(bool critical)
        {
            if (hitSlashArt != null)
            {
                hitSlashArt.gameObject.SetActive(true);
                hitSlashArt.color = new Color32(255, 255, 255, 255);
                hitSlashArt.rectTransform.localScale = Vector3.one * (critical ? 0.68f : 0.58f);
                hitSlashArt.rectTransform.localEulerAngles = new Vector3(0f, 0f, critical ? -5f : 4f);
            }
            if (heartImpactArt != null)
            {
                heartImpactArt.gameObject.SetActive(true);
                heartImpactArt.color = new Color32(255, 255, 255, critical ? (byte)245 : (byte)205);
                heartImpactArt.rectTransform.localScale = Vector3.one * 0.42f;
            }
        }

        private void AnimateAiHitArt(float t, float strength)
        {
            if (hitSlashArt != null && hitSlashArt.gameObject.activeSelf)
            {
                float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 4.5f));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.9f, t));
                hitSlashArt.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.48f, 1.08f * strength, reveal);
                Color color = hitSlashArt.color;
                color.a = fade;
                hitSlashArt.color = color;
                if (fade <= 0.01f) hitSlashArt.gameObject.SetActive(false);
            }

            if (heartImpactArt != null && heartImpactArt.gameObject.activeSelf)
            {
                float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 5f));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.78f, t));
                heartImpactArt.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.18f * strength, reveal);
                heartImpactArt.rectTransform.localEulerAngles = new Vector3(0f, 0f, t * 24f);
                Color color = heartImpactArt.color;
                color.a = fade * 0.9f;
                heartImpactArt.color = color;
                if (fade <= 0.01f) heartImpactArt.gameObject.SetActive(false);
            }
        }

        private void ShowChargeArt()
        {
            if (chargeAuraArt == null) return;
            chargeAuraArt.gameObject.SetActive(true);
            chargeAuraArt.color = new Color32(255, 255, 255, 210);
            chargeAuraArt.rectTransform.localScale = Vector3.one * 1.18f;
        }

        private void AnimateChargeArt(float t, float gather, float release)
        {
            if (chargeAuraArt == null || !chargeAuraArt.gameObject.activeSelf) return;
            float scale = Mathf.Lerp(1.18f, 0.78f, gather);
            scale = Mathf.Lerp(scale, 1.34f, release);
            chargeAuraArt.rectTransform.localScale = Vector3.one * scale;
            chargeAuraArt.rectTransform.localEulerAngles = new Vector3(0f, 0f, t * 26f);
            Color color = chargeAuraArt.color;
            color.a = Mathf.Clamp01(0.38f + gather * 0.45f - release * 0.78f);
            chargeAuraArt.color = color;
            if (release >= 0.99f) chargeAuraArt.gameObject.SetActive(false);
        }

        private void AnimateTransientEffects(float t, float strength)
        {
            for (int index = 0; index < rings.Length; index++)
            {
                Image ring = rings[index];
                if (ring == null || !ring.gameObject.activeSelf) continue;
                float delayed = Mathf.Clamp01(t * 1.3f - index * 0.12f);
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.48f, 1.48f + index * 0.12f, delayed);
                ring.rectTransform.localEulerAngles = new Vector3(0f, 0f, (index % 2 == 0 ? 1f : -1f) * t * 90f);
                Color color = ring.color;
                color.a = (1f - delayed) * 0.78f;
                ring.color = color;
                if (delayed >= 0.99f) ring.gameObject.SetActive(false);
            }

            for (int index = 0; index < slashTrails.Length; index++)
            {
                Image trail = slashTrails[index];
                if (trail == null || !trail.gameObject.activeSelf) continue;
                float delayed = Mathf.Clamp01(t * 1.5f - index * 0.08f);
                trail.rectTransform.localScale = new Vector3(Mathf.Lerp(0.04f, 1.15f * strength, delayed),
                    Mathf.Lerp(1.5f, 0.55f, delayed), 1f);
                Color color = trail.color;
                color.a = (1f - delayed) * 0.9f;
                trail.color = color;
                if (delayed >= 0.99f) trail.gameObject.SetActive(false);
            }
        }

        private void AnimateChargeRings(float t, float gather, float release)
        {
            for (int index = 0; index < rings.Length; index++)
            {
                Image ring = rings[index];
                if (ring == null) continue;
                float direction = index % 2 == 0 ? 1f : -1f;
                ring.rectTransform.localEulerAngles = new Vector3(0f, 0f, direction * (45f + index * 24f) * t);
                float start = 1.25f + index * 0.16f;
                float gathered = Mathf.Lerp(start, 0.55f + index * 0.06f, gather);
                float scale = Mathf.Lerp(gathered, 1.45f + index * 0.12f, release);
                ring.rectTransform.localScale = Vector3.one * scale;
                Color color = ring.color;
                color.a = Mathf.Clamp01((1f - release) * 0.74f + Mathf.Sin(t * Mathf.PI) * 0.22f);
                ring.color = color;
                ring.gameObject.SetActive(color.a > 0.01f);
            }
        }

        private void SpawnDamageNumber(int amount, bool critical)
        {
            if (damageTexts.Length == 0) return;
            int slot = damageCursor++ % damageTexts.Length;
            Text text = damageTexts[slot];
            if (text == null) return;
            if (slot < damageRoutines.Length && damageRoutines[slot] != null)
                StopCoroutine(damageRoutines[slot]);
            damageRoutines[slot] = StartCoroutine(DamageNumberRoutine(text, amount, critical));
        }

        private IEnumerator DamageNumberRoutine(Text text, int amount, bool critical)
        {
            RectTransform rect = text.rectTransform;
            Vector2 origin = new Vector2(360f + UnityEngine.Random.Range(-52f, 53f), -330f);
            rect.anchoredPosition = origin;
            rect.localScale = Vector3.one * (critical ? 0.62f : 0.78f);
            text.text = critical ? $"暴击  -{amount:N0}" : $"-{amount:N0}";
            text.fontSize = critical ? 34 : 27;
            text.color = critical ? new Color32(255, 221, 103, 255) : new Color32(255, 128, 213, 255);
            text.gameObject.SetActive(true);
            text.transform.SetAsLastSibling();
            float elapsed = 0f;
            const float duration = 0.82f;
            while (elapsed < duration && text.gameObject.activeSelf)
            {
                elapsed += AnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = origin + new Vector2(Mathf.Sin(t * Mathf.PI) * 10f, 72f * t);
                float pop = Mathf.Sin(Mathf.Min(1f, t * 3.4f) * Mathf.PI * 0.5f);
                rect.localScale = Vector3.one * Mathf.Lerp(0.62f, critical ? 1.18f : 1f, pop);
                Color color = text.color;
                color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1f, t));
                text.color = color;
                yield return null;
            }
            text.gameObject.SetActive(false);
        }

        private void ResetActionPose()
        {
            actionOffset = Vector2.zero;
            actionScale = 1f;
            actionRotation = 0f;
            if (portrait != null) portrait.color = Color.white;
            if (echo != null)
            {
                echo.color = new Color32(255, 50, 190, 0);
                echo.rectTransform.localPosition = Vector3.zero;
                echo.rectTransform.localScale = Vector3.one;
            }
            if (hitSlashArt != null) hitSlashArt.gameObject.SetActive(false);
            if (heartImpactArt != null) heartImpactArt.gameObject.SetActive(false);
            if (chargeAuraArt != null) chargeAuraArt.gameObject.SetActive(false);
            for (int index = 0; index < rings.Length; index++)
                if (rings[index] != null) rings[index].gameObject.SetActive(false);
            for (int index = 0; index < slashTrails.Length; index++)
                if (slashTrails[index] != null) slashTrails[index].gameObject.SetActive(false);
        }

        private void ResetTransientVisuals()
        {
            ResetActionPose();
            if (lowHealthVignette != null)
            {
                Color color = lowHealthVignette.color;
                color.a = 0f;
                lowHealthVignette.color = color;
            }
            if (stateLabel != null) stateLabel.gameObject.SetActive(false);
            for (int index = 0; index < damageTexts.Length; index++)
                if (damageTexts[index] != null) damageTexts[index].gameObject.SetActive(false);
        }

        private void SetStateLabel(string value, Color color, float alpha)
        {
            if (stateLabel == null) return;
            stateLabel.text = value;
            color.a *= Mathf.Clamp01(alpha);
            stateLabel.color = color;
            stateLabel.gameObject.SetActive(!string.IsNullOrEmpty(value) && color.a > 0.001f);
        }

        private bool IsPaused() => pauseProbe != null && pauseProbe();

        private float AnimationDelta()
        {
            if (IsPaused()) return 0f;
            int multiplier = speedProbe != null ? Mathf.Clamp(speedProbe(), 1, 2) : 1;
            return Time.unscaledDeltaTime * multiplier;
        }

        private void OnDisable()
        {
            if (!configured) return;
            StopAllCoroutines();
            reactionRoutine = null;
        }
    }
}
