using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Presentation-only animation on a dedicated child rig, never the battle layout or clock.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class EnemyUnitPresentation : MonoBehaviour
    {
        private const float DefeatDuration = .45f;
        private RectTransform motionRoot;
        private Image portrait;
        private Material flashMaterial;
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private bool invertHit;
        private Func<bool> paused;
        private Func<int> speed;
        private string enemyType;
        private Vector2 origin;
        private Vector3 originalScale;
        private Quaternion originalRotation;
        private Color originalColor;
        private Image[] chargeLines;
        private Image slash;
        private SkillEffectGraphic sigil;
        private float visualTime;
        private float attackTime = -1f;
        private float hitTime = -1f;
        private float defeatTime = -1f;
        private bool heavy;
        private bool critical;
        private bool configured;
        private bool controlled;
        public void SetControlled(bool value) => controlled = value;

        public bool IsAttacking => attackTime >= 0f && defeatTime < 0f;
        public bool IsDefeating => defeatTime >= 0f && defeatTime < DefeatDuration;
        public bool IsDefeated => defeatTime >= DefeatDuration;
        /// <summary>Number of accepted attack presentations since configure/disable.</summary>
        public int ActionCount { get; private set; }
        public int HitReactionCount { get; private set; }
        public float VisualElapsedSeconds => visualTime;
        public float DefeatProgress => defeatTime < 0f ? 0f : Mathf.Clamp01(defeatTime / DefeatDuration);

        public void Configure(Image image, Func<bool> isPaused, Func<int> battleSpeed, string type)
        {
            if (configured) RestoreBaseline();
            if (image == null) throw new ArgumentNullException(nameof(image));
            motionRoot = GetComponent<RectTransform>();
            // The caller must keep HP bars and selection layouts outside this child rig.
            if (image.transform == transform || !image.transform.IsChildOf(transform))
                throw new ArgumentException("Portrait must be a child of the dedicated motion root.", nameof(image));
            portrait = image;
            paused = isPaused;
            speed = battleSpeed;
            enemyType = type ?? string.Empty;
            origin = motionRoot.anchoredPosition;
            originalScale = motionRoot.localScale;
            originalRotation = motionRoot.localRotation;
            originalColor = portrait.color;
            // 2026-10-19 meeting: a hit flashes the target white (needs a flash-capable material).
            if (flashMaterial == null && Application.isPlaying)
            {
                Shader flash = Resources.Load<Shader>("Shaders/UIFlash");
                if (flash != null && flash.isSupported) flashMaterial = new Material(flash) { name = "UIFlash-" + type };
            }
            if (flashMaterial != null) portrait.material = flashMaterial;
            BuildEffects();
            configured = true;
            ResetActions();
            RestoreBaseline();
        }

        /// <summary>Reanchors the resting pose after an authored layout change without resetting combat effects.</summary>
        public void SetLayoutOrigin(Vector2 position)
        {
            origin = position;
            if (motionRoot != null) motionRoot.anchoredPosition = position;
        }

        public void PlayAttack(bool isHeavy)
        {
            if (!configured || !isActiveAndEnabled || defeatTime >= 0f) return;
            heavy = isHeavy;
            attackTime = 0f;
            ActionCount++;
        }

        public void PlayHit(bool isCritical) => PlayHit(isCritical, false);

        /// <summary>invert: the first two frames flash black (P5 light impact frame) before the white flash.</summary>
        public void PlayHit(bool isCritical, bool invert) => PlayHit(isCritical, invert, Vector2.zero, isCritical ? 1 : 0);

        /// <summary>
        /// 2026-10-31 (赵金: "打击感、受击感不够"): a weighted hit reaction. <paramref name="direction"/> is the blow's
        /// travel direction in screen space (attacker → this unit); <paramref name="power"/> 0 = basic, 1 = skill / crit,
        /// 2 = ultimate. The impact pose is applied at once, so a hit-stop freezes on the impact frame.
        /// </summary>
        public void PlayHit(bool isCritical, bool invert, Vector2 direction, int power)
        {
            if (!configured || !isActiveAndEnabled || defeatTime >= 0f) return;
            critical = isCritical;
            invertHit = invert;
            hitDirection = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.up;
            hitPower = Mathf.Clamp(Mathf.Max(power, isCritical ? 1 : 0), 0, 2);
            hitTime = 0f;
            HitReactionCount++;
            if (portrait != null && (paused == null || !paused()) &&
                !enemyType.StartsWith("performer-", StringComparison.OrdinalIgnoreCase)) Pose(0f);
        }

        private Vector2 hitDirection = Vector2.up;
        private int hitPower;

        /// <summary>Impact → peak → one soft overshoot → rest. 0.6 of the peak is already there at the moment of contact.</summary>
        internal static float Recoil(float t, float attack, float settle)
        {
            if (t < 0f) return 0f;
            if (t < attack) { float a = t / attack; return Mathf.Lerp(.6f, 1f, 1f - (1f - a) * (1f - a)); }
            float u = (t - attack) / settle;
            if (u >= 1f) return 0f;
            return Mathf.Cos(u * Mathf.PI * 1.35f) * (1f - u) * (1f - u);
        }

        public void PlayDefeat() => PlayDefeat(false);

        /// <summary>instant: a KO effect has taken over the visual, so the figure vanishes at once.</summary>
        public void PlayDefeat(bool instant)
        {
            if (!configured || !isActiveAndEnabled || defeatTime >= 0f) return;
            defeatTime = instant ? DefeatDuration - .0001f : 0f;
            attackTime = hitTime = -1f;
        }

        private void Update() => AdvancePresentation(BattleHitStop.Delta(Time.unscaledDeltaTime));

        /// <summary>Advances only this visual rig; exposed for deterministic presentation tests.</summary>
        public void AdvancePresentation(float unscaledSeconds)
        {
            if (!configured || !isActiveAndEnabled || portrait == null ||
                (paused != null && paused()) || unscaledSeconds <= 0f ||
                float.IsNaN(unscaledSeconds) || float.IsInfinity(unscaledSeconds)) return;
            Pose(unscaledSeconds * Mathf.Clamp(speed != null ? speed() : 1, 1, 4));
        }

        private void Pose(float delta)
        {
            visualTime += delta;
            if (defeatTime >= 0f)
            {
                defeatTime = Mathf.Min(DefeatDuration, defeatTime + delta);
                float fade = DefeatProgress;
                motionRoot.anchoredPosition = origin + Vector2.down * (18f * fade);
                motionRoot.localRotation = originalRotation * Quaternion.Euler(0f, 0f, -7f * fade);
                motionRoot.localScale = originalScale * Mathf.Lerp(1f, .72f, fade);
                Color color = originalColor;
                color.a *= 1f - fade;
                portrait.color = color;
                SetEffects(0f, 0f);
                if (flashMaterial != null) flashMaterial.SetFloat(FlashId, 0f);
                return;
            }

            if (controlled)
            {
                attackTime = -1f;
                motionRoot.anchoredPosition = origin;
                motionRoot.localRotation = originalRotation;
                motionRoot.localScale = originalScale;
                portrait.color = Color.Lerp(originalColor, CombatFeedbackPalette.Control, .2f);
                SetEffects(0f, 0f);
                if (flashMaterial != null) flashMaterial.SetFloat(FlashId, 0f);
                return;
            }
            bool drone = enemyType.IndexOf("drone", StringComparison.OrdinalIgnoreCase) >= 0;
            bool wraith = enemyType.IndexOf("wraith", StringComparison.OrdinalIgnoreCase) >= 0;
            bool performer = enemyType.StartsWith("performer-", StringComparison.OrdinalIgnoreCase);
            float rhythm = visualTime * (drone ? 2.8f : wraith ? 1.65f : 1.4f);
            // Idle cannot look like everyone is attacking. Player portraits stay still;
            // floating enemies retain only a sub-pixel ambient drift.
            Vector2 offset = performer ? Vector2.zero : new Vector2(Mathf.Sin(rhythm * .63f) * .18f,
                Mathf.Sin(rhythm) * (drone ? .6f : wraith ? .8f : .2f));
            float rotation = performer ? 0f : Mathf.Sin(rhythm * .8f) * .12f;
            float scale = 1f + (performer ? 0f : Mathf.Sin(rhythm) * .0015f);
            float charge = 0f;
            float slashAlpha = 0f;
            float flash = 0f;
            Color tint = originalColor;

            if (attackTime >= 0f)
            {
                attackTime += delta;
                float duration = heavy ? .62f : .46f;
                float t = Mathf.Clamp01(attackTime / duration);
                if (t < .42f)
                {
                    float windup = t / .42f;
                    // The attacker reads red while winding up; the target answers in white.
                    tint = Color.Lerp(originalColor, new Color(1f, .42f, .42f, originalColor.a), windup * .7f);
                    offset += Vector2.up * (7f * windup);
                    rotation -= 4f * windup;
                    scale += .045f * windup;
                    charge = windup * (heavy ? .8f : .5f);
                }
                else
                {
                    float strike = Mathf.Sin((t - .42f) / .58f * Mathf.PI);
                    offset += (performer ? new Vector2(.4f, 2.6f) : new Vector2(-3.6f, -1.5f)) *
                        (strike * (heavy ? 8f : 6f));
                    if (performer)
                    {
                        scale += strike * (heavy ? .16f : .035f);
                        offset.y += strike * (heavy ? 24f : 0);
                    }
                    rotation += strike * (performer ? 1.5f : heavy ? 7f : 4f);
                    slashAlpha = strike * (heavy ? .85f : .6f);
                    charge = (1f - t) * .35f;
                }
                if (t >= 1f) attackTime = -1f;
            }

            Vector2 squash = Vector2.one;
            if (hitTime >= 0f && performer)
            {
                // Member portraits keep their original light flinch (our cards are presented elsewhere).
                hitTime += delta;
                float t = Mathf.Clamp01(hitTime / .24f);
                float strength = 1f - t;
                offset.x += Mathf.Sin(t * Mathf.PI * 5f) * strength * (critical ? 9f : 6f);
                offset.y -= Mathf.Sin(t * Mathf.PI) * (critical ? 6f : 4f);
                scale -= Mathf.Sin(t * Mathf.PI) * .025f;
                if (flashMaterial != null)
                {
                    bool black = invertHit && hitTime < .035f;
                    flashMaterial.SetColor(FlashColorId, black ? Color.black : Color.white);
                    flash = black ? 1f : Mathf.Pow(strength, 1.4f) * (critical ? 1f : .85f);
                }
                else tint = Color.Lerp(originalColor, new Color(1f, .53f, .68f, originalColor.a), strength * .82f);
                if (t >= 1f) hitTime = -1f;
            }
            else if (hitTime >= 0f)
            {
                hitTime += delta;
                float t = hitTime;
                bool calm = BattlePresentationOptions.ReduceMotion;
                // Basic / skill-or-crit / ultimate: each step is visibly heavier, never a random jitter.
                float push = hitPower == 2 ? 26f : hitPower == 1 ? 18f : 12f;
                float lean = hitPower == 2 ? 6f : hitPower == 1 ? 4f : 2.4f;
                float squeeze = hitPower == 2 ? .085f : hitPower == 1 ? .065f : .042f;
                float settle = hitPower == 2 ? .52f : hitPower == 1 ? .42f : .34f;
                float k = Recoil(t, .045f, settle);
                if (!calm)
                {
                    float height = Mathf.Max(1f, motionRoot.rect.height);
                    offset += hitDirection * (push * k);
                    // Lean away from the blow, pivoting on the feet rather than the figure's centre.
                    float tilt = Mathf.Clamp(-hitDirection.x * 1.6f, -1f, 1f) * lean * k;
                    rotation += tilt;
                    offset.x -= Mathf.Sin(tilt * Mathf.Deg2Rad) * height * .5f;
                    // Squash on contact (wider, shorter), springing back through a small stretch.
                    squash = new Vector2(1f + squeeze * k, 1f - squeeze * k);
                    offset.y -= (1f - squash.y) * height * .5f;
                    // A short, decaying tremor across the blow — only in the first 0.16 s.
                    float tremor = Mathf.Clamp01(1f - t / .16f);
                    Vector2 across = new Vector2(-hitDirection.y, hitDirection.x);
                    offset += across * (Mathf.Sin(t * Mathf.PI * 2f * 26f) * tremor * tremor *
                        (hitPower == 2 ? 5f : hitPower == 1 ? 3.5f : 2f));
                }
                float flashSpan = hitPower == 2 ? .26f : hitPower == 1 ? .2f : .14f;
                float strength = t < .04f ? 1f : Mathf.Clamp01(1f - (t - .04f) / flashSpan);
                strength *= strength;
                if (calm) strength *= .7f;
                if (flashMaterial != null)
                {
                    bool black = invertHit && hitTime < .035f && !calm;
                    flashMaterial.SetColor(FlashColorId, black ? Color.black : Color.white);
                    flash = black ? 1f : strength * (hitPower == 2 ? .95f : hitPower == 1 ? .86f : .74f);
                }
                else tint = Color.Lerp(originalColor, new Color(1f, .53f, .68f, originalColor.a), strength * .82f);
                if (t >= .045f + settle && t >= .04f + flashSpan) hitTime = -1f;
            }
            motionRoot.anchoredPosition = origin + offset;
            motionRoot.localRotation = originalRotation * Quaternion.Euler(0f, 0f, rotation);
            motionRoot.localScale = new Vector3(originalScale.x * scale * squash.x, originalScale.y * scale * squash.y, originalScale.z * scale);
            portrait.color = tint;
            if (flashMaterial != null) flashMaterial.SetFloat(FlashId, flash);
            SetEffects(charge, slashAlpha);
        }

        private void BuildEffects()
        {
            if (chargeLines != null) return;
            chargeLines = new Image[8];
            for (int i = 0; i < chargeLines.Length; i++)
            {
                float angle = i * Mathf.PI / 4f;
                Image line = MakeLine("EnemyChargeLine-" + i, new Vector2(14f, 2f));
                RectTransform rect = line.rectTransform;
                Vector2 anchor = new Vector2(.5f + Mathf.Cos(angle) * .32f, .5f + Mathf.Sin(angle) * .38f);
                rect.anchorMin = rect.anchorMax = anchor;
                rect.localRotation = Quaternion.Euler(0f, 0f, i * 45f + 90f);
                rect.SetAsFirstSibling();
                chargeLines[i] = line;
            }
            slash = MakeLine("EnemyAttackSlash", new Vector2(45f, 3f));
            slash.rectTransform.anchorMin = slash.rectTransform.anchorMax = new Vector2(.36f, .4f);
            slash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            if (!enemyType.StartsWith("performer-", StringComparison.OrdinalIgnoreCase))
            {
                var go = new GameObject("EnemySkillSigil", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkillEffectGraphic));
                go.transform.SetParent(transform, false); sigil = go.GetComponent<SkillEffectGraphic>();
                sigil.raycastTarget = false; sigil.rectTransform.anchorMin = sigil.rectTransform.anchorMax = new Vector2(.5f,.45f);
                sigil.rectTransform.sizeDelta = Vector2.one * 240;
                sigil.Kind = enemyType.Contains("drone") ? SkillVisualKind.Prism
                    : enemyType.Contains("golem") ? SkillVisualKind.Rift
                    : enemyType.Contains("hexer") ? SkillVisualKind.Hex
                    : enemyType.Contains("guard") ? SkillVisualKind.Shield
                    : enemyType.Contains("scout") ? SkillVisualKind.Slash : SkillVisualKind.Tide;
                sigil.Strong = true;
            }
        }

        private Image MakeLine(string name, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(transform, false);
            Image image = go.GetComponent<Image>();
            image.rectTransform.sizeDelta = size;
            image.raycastTarget = false;
            image.color = Color.clear;
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            return image;
        }

        private void SetEffects(float charge, float strike)
        {
            if (chargeLines == null) return;
            for (int i = 0; i < chargeLines.Length; i++)
                if (chargeLines[i] != null)
                    chargeLines[i].color = i % 2 == 0
                        ? new Color(.25f, .86f, 1f, charge)
                        : new Color(.77f, .4f, 1f, charge);
            if (slash != null) slash.color = new Color(.5f, .92f, 1f, strike);
            if (sigil != null)
            {
                sigil.color = new Color(.73f,.4f,1f,heavy ? Mathf.Max(charge,strike) * .9f : 0);
                sigil.SetFrame(attackTime < 0 ? 0 : Mathf.Clamp01(attackTime/.62f),
                    Mathf.Clamp(motionRoot.rect.width * .36f, 40, 95));
            }
        }

        private void ResetActions()
        {
            attackTime = hitTime = defeatTime = -1f;
            visualTime = 0f;
            ActionCount = HitReactionCount = 0;
            heavy = critical = false;
        }

        private void RestoreBaseline()
        {
            if (motionRoot != null)
            {
                motionRoot.anchoredPosition = origin;
                motionRoot.localScale = originalScale;
                motionRoot.localRotation = originalRotation;
            }
            if (portrait != null) portrait.color = originalColor;
            if (flashMaterial != null) flashMaterial.SetFloat(FlashId, 0f);
            SetEffects(0f, 0f);
        }

        private void OnDestroy()
        {
            if (flashMaterial != null) Destroy(flashMaterial);
        }

        private void OnDisable()
        {
            if (!configured) return;
            RestoreBaseline();
            ResetActions();
        }
    }
}
