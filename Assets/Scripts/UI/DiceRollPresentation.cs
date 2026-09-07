using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Animates authored dice art only. The supplied model result always wins.</summary>
    [DisallowMultipleComponent]
    public sealed class DiceRollPresentation : MonoBehaviour
    {
        private const float RollDuration = .65f;
        private Image face;
        private RectTransform motionRoot;
        private Func<bool> paused;
        private Func<int> speed;
        private IReadOnlyList<Sprite> faces;
        private Sprite finalFace;
        private Color restingTint;
        private Vector2 origin;
        private Vector3 originalScale;
        private Quaternion originalRotation;
        private Image[] sparks;
        private float elapsed;
        private float delay;
        private int ordinal;
        private bool participating;
        private bool configured;

        public bool IsRolling { get; private set; }
        public int FinalValue { get; private set; }
        public int RollCount { get; private set; }
        public float Elapsed => elapsed;

        public void Configure(Image image, RectTransform childMotionRoot, Func<bool> isPaused, Func<int> battleSpeed)
        {
            if (configured) CancelRoll();
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (childMotionRoot == null) throw new ArgumentNullException(nameof(childMotionRoot));
            if (image.transform != childMotionRoot && !image.transform.IsChildOf(childMotionRoot))
                throw new ArgumentException("Dice face must belong to the child motion rig.", nameof(image));
            if (motionRoot != null && motionRoot != childMotionRoot && sparks != null)
            {
                foreach (Image spark in sparks) if (spark != null) Destroy(spark.gameObject);
                sparks = null;
            }
            face = image;
            motionRoot = childMotionRoot;
            paused = isPaused;
            speed = battleSpeed;
            origin = motionRoot.anchoredPosition;
            originalScale = motionRoot.localScale;
            originalRotation = motionRoot.localRotation;
            restingTint = face.color;
            finalFace = face.sprite;
            FinalValue = 0;
            RollCount = 0;
            elapsed = 0f;
            configured = true;
            BuildSparks();
            RestorePose();
        }

        public void PlayRoll(Sprite result, int value, IReadOnlyList<Sprite> availableFaces, int dieOrdinal, bool inHand)
        {
            if (!configured || !isActiveAndEnabled) return;
            finalFace = result;
            FinalValue = value;
            faces = availableFaces;
            participating = inHand;
            RollCount++;
            // Automatic planning may produce several model rerolls in one frame. Keep one
            // continuous visual roll and land on the newest authoritative result, never replay stale ones.
            if (IsRolling) return;
            ordinal = Mathf.Max(0, dieOrdinal);
            delay = Mathf.Min(.18f, ordinal * .04f);
            elapsed = 0f;
            IsRolling = true;
        }

        /// <summary>Refreshes model state; while rolling only the final landing target is updated.</summary>
        public void SetRestingFace(Sprite result, int value, bool inHand, Color? tint = null)
        {
            if (!configured) return;
            finalFace = result;
            FinalValue = value;
            participating = inHand;
            if (tint.HasValue) restingTint = tint.Value;
            if (!IsRolling) Settle();
        }

        public void CancelRoll(bool settle = true)
        {
            if (!configured) return;
            IsRolling = false;
            elapsed = 0f;
            RestorePose();
            if (face != null)
            {
                face.color = restingTint;
                if (settle)
                {
                    face.sprite = finalFace;
                    face.enabled = finalFace != null;
                }
            }
        }

        private void Update() => AdvancePresentation(Time.unscaledDeltaTime);

        public void AdvancePresentation(float unscaledSeconds)
        {
            if (!configured || !IsRolling || !isActiveAndEnabled || face == null ||
                (paused != null && paused()) || unscaledSeconds <= 0f ||
                float.IsNaN(unscaledSeconds) || float.IsInfinity(unscaledSeconds)) return;
            elapsed += unscaledSeconds * Mathf.Clamp(speed != null ? speed() : 1, 1, 4);
            float activeTime = elapsed - delay;
            if (activeTime < 0f) return;
            float t = Mathf.Clamp01(activeTime / RollDuration);
            if (t >= 1f)
            {
                Settle();
                return;
            }
            float fade = 1f - t;
            float spin = t * Mathf.PI * 6f;
            float perspective = Mathf.Lerp(.57f + .43f * Mathf.Abs(Mathf.Cos(spin)), 1f,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.68f, 1f, t)));
            float bounce = Mathf.Sin(t * Mathf.PI) * 14f;
            motionRoot.anchoredPosition = origin + Vector2.up * bounce;
            motionRoot.localRotation = originalRotation * Quaternion.Euler(0f, 0f,
                Mathf.Sin(spin * .7f + ordinal * .3f) * 21f * fade);
            motionRoot.localScale = Vector3.Scale(originalScale,
                new Vector3(perspective, 1f + Mathf.Sin(t * Mathf.PI) * .08f, 1f));
            Sprite displayed = finalFace;
            if (t < .8f && faces != null && faces.Count > 0)
            {
                int index = (Mathf.FloorToInt(activeTime * 22f) + ordinal * 2) % faces.Count;
                if (faces[index] != null) displayed = faces[index];
            }
            face.sprite = displayed;
            face.enabled = displayed != null;
            float landing = t > .75f ? Mathf.Sin((t - .75f) / .25f * Mathf.PI) : 0f;
            Color glow = participating ? new Color(1f, .88f, .54f, restingTint.a)
                : new Color(.68f, .94f, 1f, restingTint.a);
            face.color = Color.Lerp(restingTint, glow, landing * .28f);
            SetSparkAlpha(participating ? landing * .8f : Mathf.Sin(t * Mathf.PI) * .25f);
        }

        private void Settle()
        {
            IsRolling = false;
            RestorePose();
            if (face == null) return;
            face.sprite = finalFace;
            face.enabled = finalFace != null;
            face.color = restingTint;
        }

        private void RestorePose()
        {
            if (motionRoot != null)
            {
                motionRoot.anchoredPosition = origin;
                motionRoot.localScale = originalScale;
                motionRoot.localRotation = originalRotation;
            }
            SetSparkAlpha(0f);
        }

        private void BuildSparks()
        {
            if (sparks != null) return;
            sparks = new Image[4];
            for (int i = 0; i < sparks.Length; i++)
            {
                GameObject go = new GameObject("DiceLandingSpark-" + i,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(motionRoot, false);
                Image image = go.GetComponent<Image>();
                RectTransform rect = image.rectTransform;
                float angle = Mathf.PI * (.25f + i * .5f);
                Vector2 anchor = new Vector2(.5f + Mathf.Cos(angle) * .44f, .5f + Mathf.Sin(angle) * .44f);
                rect.anchorMin = rect.anchorMax = anchor;
                rect.sizeDelta = new Vector2(7f, 2f);
                rect.localRotation = Quaternion.Euler(0f, 0f, 45f + i * 90f);
                image.raycastTarget = false;
                image.color = Color.clear;
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                sparks[i] = image;
            }
        }

        private void SetSparkAlpha(float alpha)
        {
            if (sparks == null) return;
            Color color = participating ? new Color(1f, .82f, .35f, alpha) : new Color(.45f, .85f, 1f, alpha);
            foreach (Image spark in sparks) if (spark != null) spark.color = color;
        }

        private void OnDisable() => CancelRoll();
    }
}
