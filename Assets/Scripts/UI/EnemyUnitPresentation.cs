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
        private Func<bool> paused;
        private Func<int> speed;
        private string enemyType;
        private Vector2 origin;
        private Vector3 originalScale;
        private Quaternion originalRotation;
        private Color originalColor;
        private Image[] chargeLines;
        private Image slash;
        private float visualTime;
        private float attackTime = -1f;
        private float hitTime = -1f;
        private float defeatTime = -1f;
        private bool heavy;
        private bool critical;
        private bool configured;

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
            BuildEffects();
            configured = true;
            ResetActions();
            RestoreBaseline();
        }

        public void PlayAttack(bool isHeavy)
        {
            if (!configured || !isActiveAndEnabled || defeatTime >= 0f) return;
            heavy = isHeavy;
            attackTime = 0f;
            ActionCount++;
        }

        public void PlayHit(bool isCritical)
        {
            if (!configured || !isActiveAndEnabled || defeatTime >= 0f) return;
            critical = isCritical;
            hitTime = 0f;
            HitReactionCount++;
        }

        public void PlayDefeat()
        {
            if (!configured || !isActiveAndEnabled || defeatTime >= 0f) return;
            defeatTime = 0f;
            attackTime = hitTime = -1f;
        }

        private void Update() => AdvancePresentation(Time.unscaledDeltaTime);

        /// <summary>Advances only this visual rig; exposed for deterministic presentation tests.</summary>
        public void AdvancePresentation(float unscaledSeconds)
        {
            if (!configured || !isActiveAndEnabled || portrait == null ||
                (paused != null && paused()) || unscaledSeconds <= 0f ||
                float.IsNaN(unscaledSeconds) || float.IsInfinity(unscaledSeconds)) return;
            float delta = unscaledSeconds * Mathf.Clamp(speed != null ? speed() : 1, 1, 4);
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
            Color tint = originalColor;

            if (attackTime >= 0f)
            {
                attackTime += delta;
                float duration = heavy ? .62f : .46f;
                float t = Mathf.Clamp01(attackTime / duration);
                if (t < .42f)
                {
                    float windup = t / .42f;
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
                    if (performer) scale += strike * (heavy ? .08f : .05f);
                    rotation += strike * (performer ? 1.5f : heavy ? 7f : 4f);
                    slashAlpha = strike * (heavy ? .85f : .6f);
                    charge = (1f - t) * .35f;
                }
                if (t >= 1f) attackTime = -1f;
            }

            if (hitTime >= 0f)
            {
                hitTime += delta;
                float t = Mathf.Clamp01(hitTime / .24f);
                float strength = 1f - t;
                offset.x += Mathf.Sin(t * Mathf.PI * 5f) * strength * (critical ? 9f : 6f);
                offset.y -= Mathf.Sin(t * Mathf.PI) * (critical ? 6f : 4f);
                scale -= Mathf.Sin(t * Mathf.PI) * .025f;
                tint = Color.Lerp(originalColor, new Color(1f, .53f, .68f, originalColor.a), strength * .82f);
                if (t >= 1f) hitTime = -1f;
            }
            motionRoot.anchoredPosition = origin + offset;
            motionRoot.localRotation = originalRotation * Quaternion.Euler(0f, 0f, rotation);
            motionRoot.localScale = originalScale * scale;
            portrait.color = tint;
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
            SetEffects(0f, 0f);
        }

        private void OnDisable()
        {
            if (!configured) return;
            RestoreBaseline();
            ResetActions();
        }
    }
}
