using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-10-31 (赵金: "加强这个角色是主角的感受"): when a member acts, the bust inside her card swells to fill
    /// the sleeve, breaks a little out over its top edge, takes a short jolt at the moment of contact, then springs
    /// back to its exact resting pose. Only this dedicated rig moves — the frame, bars, icons and captain plate stay
    /// put. Strength rises with the action: basic attack (light) &lt; small skill &lt; ultimate.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HeroCardLunge1031 : MonoBehaviour
    {
        public enum Tier { Basic = 0, Skill = 1, Ultimate = 2 }

        // Per tier (battle seconds at 1x): swell, shortest hold, spring back; then peak scale, rise (px), lean (deg), jolt.
        static readonly float[] RiseTime = { .12f, .14f, .16f };
        static readonly float[] HoldTime = { .2f, .26f, .32f };
        static readonly float[] ReturnTime = { .2f, .23f, .27f };
        static readonly float[] PeakScale = { 1.24f, 1.38f, 1.5f };
        static readonly float[] Lift = { 12f, 20f, 28f };
        static readonly float[] Lean = { 1.2f, 2f, 3f };
        static readonly float[] Jolt = { .035f, .05f, .07f };
        /// <summary>Length of the contact jolt (battle seconds).</summary>
        public const float JoltSeconds = .14f;
        /// <summary>How long the held pose waits for the strike to land before jolting on its own.</summary>
        public const float ImpactWait = .16f;
        const float CoverLimit = 12f, RetriggerGuard = .15f;

        enum Phase { Rest, Waiting, Rise, Hold, Return }

        RectTransform rig;
        RectMask2D clip;
        Func<bool> paused, halted, alive, covered;
        Func<int> speed;
        Tier tier;
        Phase phase;
        float clock, from, amount, jolt = -1f, waited, startedAt = -10f;
        bool jolted, impactPending, rewaited;

        /// <summary>Lean direction: +1 tips the head to the right (towards the stage centre for left-hand cards).</summary>
        public float LeanSign { get; set; } = 1f;
        public bool IsActive => phase != Phase.Rest;
        public bool IsWaiting => phase == Phase.Waiting;
        public int PlayCount { get; private set; }
        public int JoltCount { get; private set; }
        public Tier CurrentTier => tier;
        /// <summary>0 at rest, 1 at the authored peak (briefly above 1 while the back-ease overshoots).</summary>
        public float Amount => amount;
        public static float SwellSeconds(Tier t) => RiseTime[(int)t];
        public static float ShortestSeconds(Tier t) => RiseTime[(int)t] + HoldTime[(int)t] + ReturnTime[(int)t];

        /// <param name="mask">Optional clip, enabled only while acting out (keeps the art's side crop edges inside the sleeve).</param>
        public void Configure(Func<bool> isPaused, Func<bool> isHalted, Func<int> battleSpeed,
            Func<bool> isAlive, Func<bool> isCovered, RectMask2D mask = null)
        {
            rig = (RectTransform)transform;
            clip = mask;
            paused = isPaused; halted = isHalted; speed = battleSpeed; alive = isAlive; covered = isCovered;
            Rest();
        }

        /// <summary>Starts (or, mid-lunge, re-launches from the current pose) the act-out.</summary>
        public bool Play(Tier strength)
        {
            if (rig == null || !isActiveAndEnabled) return false;
            if (BattlePresentationOptions.ReduceMotion || (halted?.Invoke() ?? false) || !(alive?.Invoke() ?? true))
            {
                Rest();
                return false;
            }
            // The action-start and the strike launch can both ask for the same cast in one beat.
            if (IsActive && Time.unscaledTime - startedAt < RetriggerGuard && strength <= tier) return true;
            tier = IsActive && strength < tier ? tier : strength;
            startedAt = Time.unscaledTime;
            PlayCount++;
            rewaited = false;
            if (covered?.Invoke() ?? false) { Rest(); phase = Phase.Waiting; waited = 0f; return true; }
            Launch();
            return true;
        }

        /// <summary>The strike connected: a short jolt on the held pose (queued if it lands while still swelling).</summary>
        public void Impact()
        {
            if (BattlePresentationOptions.ReduceMotion || jolted) return;
            if (phase == Phase.Rise || phase == Phase.Waiting) impactPending = true;
            else if (phase == Phase.Hold) StartJolt();
        }

        /// <summary>Back to the exact resting pose at once (cancel / close / defeat / reduced motion).</summary>
        public void Rest()
        {
            phase = Phase.Rest; clock = 0f; jolt = -1f; amount = 0f; from = 0f;
            jolted = impactPending = false;
            SetClip(false);
            if (rig == null) return;
            rig.anchoredPosition = Vector2.zero;
            rig.localScale = Vector3.one;
            rig.localRotation = Quaternion.identity;
        }

        void Launch()
        {
            from = Mathf.Clamp(amount, 0f, 1.2f);
            phase = Phase.Rise; clock = 0f; jolt = -1f; jolted = false;
            SetClip(true);
            Pose();
        }

        void StartJolt()
        {
            jolted = true; impactPending = false; jolt = 0f; JoltCount++;
        }

        void SetClip(bool on)
        {
            if (clip != null && clip.enabled != on) clip.enabled = on;
        }

        void Update() => Advance(Mathf.Min(Time.unscaledDeltaTime, .05f));

        /// <summary>Advances the act-out by real seconds (scaled by the battle speed); exposed for tests.</summary>
        public void Advance(float unscaledSeconds)
        {
            if (rig == null || !IsActive) return;
            if (BattlePresentationOptions.ReduceMotion || (halted?.Invoke() ?? false) || !(alive?.Invoke() ?? true))
            {
                Rest();
                return;
            }
            if (paused?.Invoke() ?? false) return;   // frozen mid-pose; it resumes where it was
            if (unscaledSeconds <= 0f || float.IsNaN(unscaledSeconds)) return;
            float delta = BattleHitStop.Delta(unscaledSeconds) * Mathf.Clamp(speed?.Invoke() ?? 1, 1, 3);
            int i = (int)tier;
            if (phase == Phase.Waiting)
            {
                // A full-screen cut-in is up: act out right after it, or drop the beat if it never clears.
                waited += unscaledSeconds;
                if (!(covered?.Invoke() ?? false)) Launch();
                else if (waited > CoverLimit) Rest();
                return;
            }
            if (phase == Phase.Rise && !rewaited && (covered?.Invoke() ?? false))
            {
                // The ultimate cut-in opened right after the cast began: act out once it clears instead of unseen.
                bool pending = impactPending;
                Rest(); phase = Phase.Waiting; waited = 0f; rewaited = true; impactPending = pending;
                return;
            }
            clock += delta;
            if (jolt >= 0f) jolt += delta;
            if (phase == Phase.Rise && clock >= RiseTime[i])
            {
                phase = Phase.Hold; clock -= RiseTime[i];
                if (impactPending) StartJolt();
            }
            if (phase == Phase.Hold)
            {
                if (!jolted && clock >= ImpactWait) StartJolt();
                if (clock >= HoldTime[i] && jolted && jolt >= JoltSeconds) { phase = Phase.Return; clock = 0f; }
            }
            if (phase == Phase.Return && clock >= ReturnTime[i]) { Rest(); return; }
            Pose();
        }

        void Pose()
        {
            if (rig == null) return;
            int i = (int)tier;
            switch (phase)
            {
                case Phase.Rise: amount = Mathf.LerpUnclamped(from, 1f, OutBack(clock / RiseTime[i], 1.9f)); break;
                case Phase.Hold: amount = 1f; break;
                case Phase.Return: amount = 1f - OutBack(clock / ReturnTime[i], 1.25f); break;
                default: amount = 0f; break;
            }
            float scale = 1f + (PeakScale[i] - 1f) * amount;
            float sx = scale, sy = scale, shake = 0f;
            if (jolt >= 0f && jolt < JoltSeconds)
            {
                // Contact: a quick squash-and-kick plus a two-pixel tremor that dies out fast.
                float k = jolt / JoltSeconds, decay = (1f - k) * (1f - k);
                float squash = Jolt[i] * Mathf.Sin(k * Mathf.PI * 2.2f) * decay;
                sx *= 1f + squash; sy *= 1f - squash * .8f;
                shake = Mathf.Sin(jolt * 95f) * (1.2f + i * .6f) * decay;
            }
            rig.localScale = new Vector3(sx, sy, 1f);
            rig.anchoredPosition = new Vector2(shake + LeanSign * 8f * amount, Lift[i] * amount);
            rig.localRotation = Quaternion.Euler(0f, 0f, -Lean[i] * amount * LeanSign);
        }

        /// <summary>Back-out ease: overshoots its target by a little before settling (s ≈ 1.7 → ~10%).</summary>
        static float OutBack(float x, float s)
        {
            x = Mathf.Clamp01(x) - 1f;
            return x * x * ((s + 1f) * x + s) + 1f;
        }

        void OnDisable() => Rest();
    }
}
