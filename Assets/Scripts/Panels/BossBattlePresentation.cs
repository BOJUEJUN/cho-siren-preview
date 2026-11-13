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
        public void SetLayoutOrigin(Vector2 position)
        { basePosition = position; if (motionRoot != null) motionRoot.anchoredPosition = position; }
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
        private Coroutine minorHitRoutine;
        // Hit < attack anticipation < phase transition < outcome. Receiving damage must
        // remain visible without cancelling the telegraph the player is trying to read.
        private int reactionPriority;
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
            // The hit flash pushes the whole figure to white (same shader as the minions).
            if (flashMaterial == null && Application.isPlaying && portrait.material == portrait.defaultMaterial)
            {
                Shader flash = Resources.Load<Shader>("Shaders/UIFlash");
                if (flash != null && flash.isSupported) flashMaterial = new Material(flash) { name = "UIFlash-Boss" };
            }
            if (flashMaterial != null) { portrait.material = flashMaterial; SetFlash(0f); }
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

        /// <summary>
        /// False when the battle draws its own hit effects on the boss (Fx1023, 2026-10-23): the boss then only
        /// recoils and flashes, without the old painted slash/heart art, rings and slash trails on every hit.
        /// </summary>
        public bool DecoratedHits { get; set; } = true;

        public void PlayHit(int amount, bool critical, int diceMultiplierPermille = 1000)
            => PlayHitFrom(amount, critical, diceMultiplierPermille, Vector2.zero, critical ? 1 : 0);

        /// <summary>
        /// 2026-10-31 (赵金: "受击做上动画看起来才过瘾"): <paramref name="direction"/> is the blow's travel direction on
        /// screen (attacker → boss), <paramref name="power"/> 0 basic, 1 skill / crit, 2 ultimate.
        /// </summary>
        public void PlayHitFrom(int amount, bool critical, int diceMultiplierPermille, Vector2 direction, int power)
        {
            if (!configured || outcomeLocked) return;
            HitReactionCount++;
            hitDirection = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.up;
            hitPower = Mathf.Clamp(Mathf.Max(power, critical ? 1 : 0), 0, 2);
            if (reactionPriority >= 2)
            {
                if (minorHitRoutine != null) StopCoroutine(minorHitRoutine);
                minorHitRoutine = StartCoroutine(MinorHitRoutine(critical));
            }
            else StartReaction(HitRoutine(critical), 1);
            SpawnDamageNumber(amount, critical, diceMultiplierPermille);
        }

        private Vector2 hitDirection = Vector2.up;
        private int hitPower;
        private Vector2 actionSquash = Vector2.one;
        private Material flashMaterial;
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        private void SetFlash(float amount)
        {
            if (flashMaterial == null) return;
            flashMaterial.SetColor(FlashColorId, Color.white);
            flashMaterial.SetFloat(FlashId, Mathf.Clamp01(amount));
        }

        public void PlayCharge(string skillName)
        {
            if (!configured || outcomeLocked) return;
            ChargeReactionCount++;
            StartReaction(ChargeRoutine(string.IsNullOrEmpty(skillName) ? "终曲" : skillName), 2);
        }

        public void PlayPhaseSurge(int phase)
        {
            if (!configured || outcomeLocked) return;
            PhaseReactionCount++;
            StartReaction(PhaseRoutine(Mathf.Clamp(phase, 2, 3)), 3);
        }

        public void PlayOutcome(bool playerVictory)
        {
            if (!configured || outcomeLocked) return;
            outcomeLocked = true;
            OutcomeReactionCount++;
            StopReaction();
            reactionPriority = 4;
            reactionVersion++;
            reactionRoutine = StartCoroutine(playerVictory ? DefeatRoutine() : VictoryRoutine());
        }

        /// <summary>True once the fallen boss has left the stage (see <see cref="PlayDeath"/>).</summary>
        public bool Vanished { get; private set; }

        /// <summary>
        /// 2026-11-12 (原画同事反馈: BOSS 已被打死，却要等身边小怪也打完才消失): the boss leaves the stage the moment she
        /// falls, while her adds keep fighting. <paramref name="shattered"/>: a KO break already plays on a ghost copy
        /// of her portrait, so she vanishes at once; otherwise she slumps, fades out, then vanishes.
        /// </summary>
        public void PlayDeath(bool shattered)
        {
            if (!configured || Vanished) return;
            outcomeLocked = true;
            OutcomeReactionCount++;
            StopReaction();
            reactionPriority = 4;
            reactionVersion++;
            if (shattered || !isActiveAndEnabled) { Vanish(); return; }
            reactionRoutine = StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            IEnumerator slump = DefeatRoutine();
            while (slump.MoveNext()) yield return slump.Current;
            float startAlpha = portrait.color.a, elapsed = 0f;
            const float fade = 0.3f;
            while (elapsed < fade)
            {
                elapsed += AnimationDelta();
                Color color = portrait.color;
                color.a = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / fade));
                portrait.color = color;
                yield return null;
            }
            reactionRoutine = null;
            Vanish();
        }

        private void Vanish()
        {
            Vanished = true;
            State = BossVisualState.Defeated;
            SetStateLabel(string.Empty, Color.clear, 0f);
            if (motionRoot != null) motionRoot.gameObject.SetActive(false);
            foreach (Graphic part in new Graphic[] { echo, auraBack, auraCore, shadow, stagePulse, lowHealthVignette,
                hitSlashArt, heartImpactArt, chargeAuraArt })
                if (part != null) part.gameObject.SetActive(false);
            foreach (Image ring in rings) if (ring != null) ring.gameObject.SetActive(false);
            foreach (Image trail in slashTrails) if (trail != null) trail.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!configured || Vanished || IsPaused()) return;
            animationClock += AnimationDelta();
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
            motionRoot.localScale = new Vector3(idleScale * actionScale * actionSquash.x, idleScale * actionScale * actionSquash.y, 1f);
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

        private void StartReaction(IEnumerator routine, int priority)
        {
            if (priority < reactionPriority) return;
            StopReaction();
            reactionPriority = priority;
            int version = ++reactionVersion;
            reactionRoutine = StartCoroutine(RunReaction(version, routine));
        }

        private IEnumerator RunReaction(int version, IEnumerator routine)
        {
            while (version == reactionVersion && routine.MoveNext()) yield return routine.Current;
            if (version != reactionVersion) yield break;
            reactionRoutine = null;
            reactionPriority = 0;
            ResetActionPose();
            State = LowHealth ? BossVisualState.LowHealth : BossVisualState.Idle;
            SetStateLabel(LowHealth ? "危险 · 终曲暴走" : string.Empty,
                new Color32(255, 86, 173, 255), LowHealth ? 1f : 0f);
        }

        private void StopReaction()
        {
            if (reactionRoutine != null) StopCoroutine(reactionRoutine);
            reactionRoutine = null;
            reactionPriority = 0;
            if (minorHitRoutine != null) StopCoroutine(minorHitRoutine);
            minorHitRoutine = null;
            ResetActionPose();
        }

        private IEnumerator MinorHitRoutine(bool critical)
        {
            // This independent echo does not write the primary pose, label, charge rings
            // or portrait tint, so simultaneous hits cannot erase an attack/phase cue.
            float elapsed = 0f;
            const float duration = 0.2f;
            while (elapsed < duration)
            {
                elapsed += AnimationDelta();
                float fade = 1f - Mathf.Clamp01(elapsed / duration);
                SetFlash(fade * fade * (critical ? .72f : .55f));
                if (echo != null)
                {
                    echo.rectTransform.localPosition = new Vector3(-6f * fade, 2f * fade, 0f);
                    echo.rectTransform.localScale = Vector3.one * (1f + fade * 0.025f);
                    echo.color = new Color32(255, critical ? (byte)174 : (byte)80, 218,
                        (byte)Mathf.RoundToInt((critical ? 165f : 115f) * fade));
                }
                yield return null;
            }
            if (echo != null)
            {
                echo.color = Color.clear;
                echo.rectTransform.localPosition = Vector3.zero;
                echo.rectTransform.localScale = Vector3.one;
            }
            SetFlash(0f);
            minorHitRoutine = null;
        }

        private IEnumerator HitRoutine(bool critical)
        {
            State = BossVisualState.Hit;
            SetStateLabel(critical ? "暴击 · 失衡" : "命中", critical ? CombatFeedbackPalette.Critical : CombatFeedbackPalette.Attack, 1f);
            float strength = critical ? 1.35f : 1f;
            if (DecoratedHits)
            {
                ShowSlashTrails(strength);
                ShowRings(new Color32(255, 73, 205, 220), 0.55f);
                ShowAiHitArt(critical);
            }

            // 2026-10-31: a heavy body takes the blow — white flash, a short shove along the blow that leans back on
            // the throne, a squash that springs back through one soft overshoot. All times are animation time
            // (AnimationDelta runs at Pace, so 0.025 here is ~50 ms on screen at 1x).
            int power = hitPower;
            Vector2 dir = hitDirection;
            float push = power == 2 ? 24f : power == 1 ? 16f : 10f;
            float lean = power == 2 ? 4f : power == 1 ? 2.6f : 1.5f;
            float squeeze = power == 2 ? .06f : power == 1 ? .045f : .03f;
            float settle = power == 2 ? .3f : power == 1 ? .25f : .2f;
            float flashSpan = power == 2 ? .15f : power == 1 ? .12f : .085f;
            float duration = Mathf.Max(.025f + settle, .02f + flashSpan, .27f);
            bool calm = BattlePresentationOptions.ReduceMotion;
            float elapsed = 0f;
            while (elapsed <= duration)
            {
                float t = elapsed;
                float k = EnemyUnitPresentation.Recoil(t, .025f, settle);
                float height = Mathf.Max(1f, motionRoot.rect.height);
                if (calm) { actionOffset = Vector2.zero; actionRotation = 0f; actionSquash = Vector2.one; actionScale = 1f; }
                else
                {
                    float tilt = Mathf.Clamp(-dir.x * 1.6f, -1f, 1f) * lean * k;
                    actionRotation = tilt;
                    actionSquash = new Vector2(1f + squeeze * k, 1f - squeeze * k);
                    actionScale = 1f - .012f * k; // pushed back, a touch further away
                    float tremor = Mathf.Clamp01(1f - t / .08f);
                    Vector2 across = new Vector2(-dir.y, dir.x);
                    actionOffset = dir * (push * k)
                        + new Vector2(-Mathf.Sin(tilt * Mathf.Deg2Rad) * height * .5f, -(1f - actionSquash.y) * height * .5f)
                        + across * (Mathf.Sin(t * Mathf.PI * 2f * 26f) * tremor * tremor * (power == 2 ? 4f : power == 1 ? 3f : 1.6f));
                }
                float flash = t < .02f ? 1f : Mathf.Clamp01(1f - (t - .02f) / flashSpan);
                flash *= flash;
                if (calm) flash *= .7f;
                if (flashMaterial != null)
                {
                    SetFlash(flash * (power == 2 ? .9f : power == 1 ? .8f : .66f)); // a big figure reads white long before 1.0
                    portrait.color = Color.white;
                }
                else portrait.color = Color.Lerp(Color.white, new Color32(255, 142, 220, 255), flash * 0.48f);
                if (echo != null)
                {
                    float trail = Mathf.Clamp01(1f - t / .18f);
                    echo.rectTransform.localPosition = new Vector3(-actionOffset.x * 0.7f, -actionOffset.y * .4f, 0f);
                    echo.rectTransform.localScale = Vector3.one * (1.01f + trail * 0.05f * strength);
                    echo.color = new Color32(255, 38, 184, (byte)Mathf.RoundToInt(110f * trail * (calm ? .5f : 1f)));
                }

                float tn = Mathf.Clamp01(t / duration);
                AnimateTransientEffects(tn, strength);
                AnimateAiHitArt(tn, strength);
                yield return null;
                elapsed += AnimationDelta();
            }
            SetFlash(0f);
        }

        private IEnumerator ChargeRoutine(string skillName)
        {
            State = BossVisualState.Charging;
            SetStateLabel("蓄力 · " + skillName, CombatFeedbackPalette.Control, 1f);
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
            if (DecoratedHits)
            {
                ShowRings(new Color32(112, 191, 255, 190), 0.42f);
                ShowAiHitArt(true);
            }
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

        /// <summary>False while the battle shows the hit with its own big numbers (BigDamageNumbers1101, 2026-11-01).</summary>
        public bool FloatingNumbers { get; set; } = true;

        private void SpawnDamageNumber(int amount, bool critical, int diceMultiplierPermille)
        {
            if (!FloatingNumbers) return;
            int capacity = Math.Min(damageTexts.Length, damageRoutines.Length);
            if (capacity == 0) return;
            int slot = damageCursor++ % capacity;
            Text text = damageTexts[slot];
            if (text == null) return;
            if (slot < damageRoutines.Length && damageRoutines[slot] != null)
                StopCoroutine(damageRoutines[slot]);
            damageRoutines[slot] = StartCoroutine(
                DamageNumberRoutine(text, amount, critical, slot, diceMultiplierPermille));
        }

        private IEnumerator DamageNumberRoutine(Text text, int amount, bool critical, int slot,
            int diceMultiplierPermille)
        {
            RectTransform rect = text.rectTransform;
            // Six fixed lanes: independent clocks can produce many hits on the same frame.
            // Row spacing exceeds text height plus its entire rise, even when births differ.
            Vector2 origin = new Vector2(slot % 2 == 0 ? 245f : 475f, -230f - slot / 2 * 110f);
            rect.anchoredPosition = origin;
            // 2026-10-23 review: the boss numbers were squeezed by best-fit down to ~18 — now full size.
            rect.sizeDelta = new Vector2(300f, 70f);
            rect.localScale = Vector3.one;
            text.text = critical ? $"暴击  -{amount:N0}" : $"-{amount:N0}";
            if (diceMultiplierPermille > 1000)
                text.text += $"  ×{diceMultiplierPermille / 1000f:0.##}";
            text.fontSize = critical ? 40 : 34;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = critical ? CombatFeedbackPalette.Critical : CombatFeedbackPalette.Attack;
            text.gameObject.SetActive(true);
            text.transform.SetAsLastSibling();
            float elapsed = 0f;
            const float duration = 0.82f;
            while (elapsed < duration && text.gameObject.activeSelf)
            {
                elapsed += AnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = origin + new Vector2(0f, 52f * t);
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
            actionSquash = Vector2.one;
            SetFlash(0f);
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
            // A hit-stop holds the boss on its impact frame too (2026-10-31).
            return ChoSiren.BattleHitStop.Delta(Time.unscaledDeltaTime) * multiplier * ChoSiren.BattlePresentationOptions.Pace;
        }

        private void OnDestroy()
        {
            if (flashMaterial != null) Destroy(flashMaterial);
        }

        private void OnDisable()
        {
            if (!configured) return;
            StopAllCoroutines();
            reactionRoutine = null;
            minorHitRoutine = null;
            reactionPriority = 0;
            SetFlash(0f);
        }
    }
}
