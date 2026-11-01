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
        private const float RollDuration = .85f;
        // 3D tumble: 16 rendered frames per final value (4x4 sheet), the die lands face-on.
        private const int RollFrames = 16;
        private const float RollEnd = .8f;
        private static readonly Sprite[][] RollSheets = new Sprite[7][];
        private Image face;
        private RectTransform motionRoot;
        private Func<bool> paused;
        private Func<int> speed;
        private Func<bool> reduceMotion;
        private IReadOnlyList<Sprite> faces;
        private Sprite finalFace;
        private Color restingTint;
        private Vector2 origin;
        private Vector3 originalScale;
        private Quaternion originalRotation;
        private Image[] sparks;
        private float elapsed;
        private float idleClock;
        private float delay;
        private int ordinal;
        private bool participating;
        private bool configured;
        private bool useRenderedRoll = true;

        public bool IsRolling { get; private set; }
        /// <summary>First hard landing of any die (owner pick B2): the battle shakes the stage.</summary>
        public static event Action<DiceRollPresentation> HardLanding;
        private DiceCube3D cube;
        private bool cubeRoll, cubeFacesFresh;
        private const float CubePlaybackSpeed = 1f;
        public int FinalValue { get; private set; }
        public int RollCount { get; private set; }
        public float Elapsed => elapsed;

        public void Configure(Image image, RectTransform childMotionRoot, Func<bool> isPaused, Func<int> battleSpeed,
            Func<bool> reducedMotion = null, bool renderedRoll = true)
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
            reduceMotion = reducedMotion;
            useRenderedRoll = renderedRoll;
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

        private bool pendingRoll;
        private int pendingOrdinal;

        /// <summary>A hidden opening roll remains pending until it can really be shown.</summary>
        public bool Busy => IsRolling || pendingRoll;

        public void PlayRoll(Sprite result, int value, IReadOnlyList<Sprite> availableFaces, int dieOrdinal, bool inHand)
        {
            if (!configured) return;
            if (!isActiveAndEnabled)
            {
                // The battle's opening roll arrives before the dice row is shown: keep it and
                // play it the moment the die appears, instead of silently skipping to the result.
                finalFace = result; FinalValue = value; faces = availableFaces; participating = inHand;
                pendingRoll = true; pendingOrdinal = dieOrdinal;
                return;
            }
            finalFace = result;
            FinalValue = value;
            faces = availableFaces;
            participating = inHand;
            RollCount++;
            // Reduced motion lands on the authoritative face immediately instead of tumbling.
            if (reduceMotion != null && reduceMotion())
            {
                Settle();
                return;
            }
            // Automatic planning may produce several model rerolls in one frame. Keep one
            // continuous visual roll and land on the newest authoritative result, never replay stale ones.
            if (IsRolling) return;
            ordinal = Mathf.Max(0, dieOrdinal);
            // The opening roll of a battle waits a beat so the stage is on screen first.
            delay = Mathf.Min(.18f, ordinal * .04f) + (RollCount == 1 ? .4f : 0f);
            elapsed = 0f;
            IsRolling = true;
            // Hold the first tumble frame (not the result) while waiting for the delay.
            Sprite[] tumble3d = useRenderedRoll ? RollFramesFor(value) : null;
            if (tumble3d != null) { face.sprite = tumble3d[0]; face.enabled = true; face.color = Color.white; motionRoot.localScale = originalScale * 1.22f; }
            cubeRoll = false;
            if (tumble3d == null && faces != null && faces.Count >= 6 && value >= 1 && value <= 6)
            {
                if (cube == null)
                {
                    cube = new DiceCube3D(motionRoot);
                    cube.Landed += _ => HardLanding?.Invoke(this);
                }
                Rect rect = motionRoot.rect;
                float dieSize = Mathf.Max(24f, Mathf.Min(rect.width, rect.height) * .92f);
                cube.Begin(UnityEngine.Random.Range(1, int.MaxValue), dieSize, value, faces);
                cubeFacesFresh = false;
                cube.SetVisible(false); // shown when the delay ends
                cubeRoll = true;
                face.enabled = false; // the toss reveals the value; never show it early
                delay = RollCount == 1 ? .4f : 0f; // every die tosses and lands together
            }
        }

        /// <summary>Refreshes model state; while rolling only the final landing target is updated.</summary>
        public void SetRestingFace(Sprite result, int value, bool inHand, Color? tint = null)
        {
            if (!configured) return;
            finalFace = result;
            FinalValue = value;
            participating = inHand;
            if (tint.HasValue) restingTint = tint.Value;
            if (!IsRolling)
            {
                face.sprite = finalFace;
                face.color = restingTint;
                face.enabled = finalFace != null;
                ApplyIdlePose();
            }
        }

        public void CancelRoll(bool settle = true)
        {
            if (!configured) return;
            pendingRoll = false;
            cubeRoll = false;
            cube?.SetVisible(false);
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
            if (!configured || !isActiveAndEnabled || face == null ||
                (paused != null && paused()) || unscaledSeconds <= 0f ||
                float.IsNaN(unscaledSeconds) || float.IsInfinity(unscaledSeconds)) return;
            if (reduceMotion != null && reduceMotion()) { Settle(); return; }
            if (!IsRolling)
            {
                idleClock += Mathf.Min(unscaledSeconds, .1f);
                ApplyIdlePose();
                return;
            }
            // Clamp the step: the battle's first frames hitch while art loads, and an unclamped
            // one-second frame would finish the whole tumble before anyone sees it.
            elapsed += Mathf.Min(unscaledSeconds, 1f / 30f) * Mathf.Clamp(speed != null ? speed() : 1, 1, 4);
            float activeTime = elapsed - delay;
            if (activeTime < 0f) return;
            if (cubeRoll && cube != null)
            {
                float ct = activeTime * CubePlaybackSpeed;
                if (ct >= cube.Duration) { Settle(); return; }
                if (!cubeFacesFresh) { cube.SetFaces(faces); cubeFacesFresh = true; }
                cube.SetVisible(true);
                face.enabled = false;
                SetSparkAlpha(0f);
                cube.Apply(ct, motionRoot, origin, originalScale);
                return;
            }
            float t = Mathf.Clamp01(activeTime / RollDuration);
            if (t >= 1f)
            {
                Settle();
                return;
            }
            Sprite[] tumble3d = useRenderedRoll ? RollFramesFor(FinalValue) : null;
            if (tumble3d != null)
            {
                // Rendered 3D crystal die tumbles through the air, lands face-on, then the authored
                // die art takes over under a short flash.
                float h = t < RollEnd ? Mathf.Sin(t / RollEnd * Mathf.PI) * 58f : 0f;
                float bounce = t >= RollEnd ? Mathf.Sin(Mathf.Clamp01((t - RollEnd) / (1f - RollEnd)) * Mathf.PI) * 7f : 0f;
                motionRoot.anchoredPosition = origin + Vector2.up * (h + bounce);
                motionRoot.localRotation = originalRotation;
                float touch = t >= RollEnd ? Mathf.Sin(Mathf.Clamp01((t - RollEnd) / .12f) * Mathf.PI) : 0f;
                // The rendered cube fills ~81% of its square frame; scale it to the authored crystal.
                float frameScale = t < RollEnd ? 1.22f : 1f;
                motionRoot.localScale = Vector3.Scale(originalScale,
                    new Vector3(frameScale * (1f + touch * .14f), frameScale * (1f - touch * .1f), 1f));
                int index = Mathf.Clamp(Mathf.FloorToInt(t / RollEnd * (RollFrames - 1)), 0, RollFrames - 1);
                Sprite shown = t < RollEnd ? tumble3d[index] : finalFace;
                face.sprite = shown;
                face.enabled = shown != null;
                float flash = t >= RollEnd ? 1f - Mathf.Clamp01((t - RollEnd) / (1f - RollEnd)) : 0f;
                Color hot = participating ? new Color(1f, .9f, .6f, restingTint.a) : new Color(.8f, .95f, 1f, restingTint.a);
                face.color = t < RollEnd ? Color.white : Color.Lerp(restingTint, hot, flash * .7f);
                SetSparkAlpha(t >= RollEnd ? flash : 0f);
                return;
            }
            float fade = 1f - t;
            float spin = t * Mathf.PI * 6f;
            float perspective = Mathf.Lerp(.57f + .43f * Mathf.Abs(Mathf.Cos(spin)), 1f,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.68f, 1f, t)));
            // Toss: a high arc (first 68%), a hard landing, then one small rebound.
            const float land = .68f;
            float height = t < land ? Mathf.Sin(t / land * Mathf.PI) * 52f
                : Mathf.Sin((t - land) / (1f - land) * Mathf.PI) * 9f;
            motionRoot.anchoredPosition = origin + Vector2.up * height;
            // Tumbles one and a half turns, unwinding exactly onto the resting angle.
            float tumble = fade * fade * (540f + ordinal * 37f) * (ordinal % 2 == 0 ? 1f : -1f);
            motionRoot.localRotation = originalRotation * Quaternion.Euler(0f, 0f, tumble);
            // Squash on impact, stretch while airborne.
            float impact = t >= land ? Mathf.Sin(Mathf.Clamp01((t - land) / .14f) * Mathf.PI) : 0f;
            float airborne = t < land ? Mathf.Sin(t / land * Mathf.PI) : 0f;
            motionRoot.localScale = Vector3.Scale(originalScale, new Vector3(
                perspective * (1f + impact * .2f) * (1f + airborne * .1f),
                (1f - impact * .18f) * (1f + airborne * .1f), 1f));
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

        /// <summary>Slices vfx-die-roll-N (4x4 grid of 192px frames) once; null when the art is absent.</summary>
        private static Sprite[] RollFramesFor(int value)
        {
            if (value < 1 || value > 6) return null;
            if (RollSheets[value] != null) return RollSheets[value].Length > 0 ? RollSheets[value] : null;
            Sprite sheet = ReferenceArt038.Load("Art/Reference038/vfx-die-roll-" + value);
            if (sheet == null || sheet.texture == null) { RollSheets[value] = new Sprite[0]; return null; }
            Rect r = sheet.textureRect;
            float cell = r.width / 4f;
            var frames = new Sprite[RollFrames];
            for (int i = 0; i < RollFrames; i++)
            {
                // Grid row 0 is the top of the image; texture y runs bottom-up.
                var rect = new Rect(r.x + (i % 4) * cell, r.y + r.height - (i / 4 + 1) * cell, cell, cell);
                frames[i] = Sprite.Create(sheet.texture, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                frames[i].hideFlags = HideFlags.DontSave;
            }
            return RollSheets[value] = frames;
        }

        private void ApplyIdlePose()
        {
            if (motionRoot == null) return;
            if (reduceMotion != null && reduceMotion()) { RestorePose(); return; }
            float phase = idleClock * 1.25f + ordinal * .7f;
            motionRoot.anchoredPosition = origin + Vector2.up * (Mathf.Sin(phase) * 2.2f);
            motionRoot.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(phase * .65f) * .7f);
            motionRoot.localScale = originalScale;
        }

        private void Settle()
        {
            IsRolling = false;
            cubeRoll = false;
            cube?.SetVisible(false);
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
                rect.sizeDelta = new Vector2(14f, 3f);
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

        private void OnDisable()
        {
            // The battle re-parents the dice row while building its layout (disable → enable);
            // a roll interrupted that way replays when the die is shown again.
            bool replay = IsRolling || pendingRoll;
            int replayOrdinal = IsRolling ? ordinal : pendingOrdinal;
            if (IsRolling) RollCount = Mathf.Max(0, RollCount - 1);
            CancelRoll();
            if (replay) { pendingRoll = true; pendingOrdinal = replayOrdinal; }
        }

        private void OnEnable()
        {
            if (!pendingRoll) return;
            pendingRoll = false;
            PlayRoll(finalFace, FinalValue, faces, pendingOrdinal, participating);
        }
    }
}
