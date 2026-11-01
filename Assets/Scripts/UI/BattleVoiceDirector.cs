using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>
    /// One short spoken callout at a time. Requests are collected until LateUpdate so a captain or
    /// ultimate from the same simulation frame wins before any lower-priority voice starts.
    /// No simulation timing, targeting or skill release logic lives here.
    /// </summary>
    public sealed class BattleVoiceDirector : MonoBehaviour
    {
        // 2026-10-31: Defeat (阵亡台词) outranks skills; Victory stays last and highest.
        public enum Cue { Active, Ultimate, Defeat, Victory }
        public const float MinimumStartInterval = 1.2f;
        /// <summary>A line actually started: member, cue, 1-based variant, clip seconds (subtitles hook here).</summary>
        public event Action<string, Cue, int, float> Spoke;
        public const float ActiveMemberRepeatSeconds = 8f;

        private static readonly HashSet<string> Members = new HashSet<string>(StringComparer.Ordinal)
            { "xingli", "feiyin", "wubai", "yeying" };
        private readonly Dictionary<string, int> variants = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> nextActiveByMember = new Dictionary<string, float>(StringComparer.Ordinal);
        private GameAudio audio;
        private Func<bool> blocked;
        private string owner;
        private Request pending;
        private Cue currentCue;
        private float nextStartAt;
        private bool released, actionsComplete;

        private sealed class Request
        {
            public string Member;
            public Cue Cue;
            public bool Captain;
            public float ExpiresAt;
            public int Priority => (int)Cue * 10 + (Captain ? 1 : 0);
        }

        public void Configure(GameAudio transport, Func<bool> isBlocked = null)
        {
            Release();
            audio = transport;
            blocked = isBlocked;
            owner = "battle-voice:" + Guid.NewGuid().ToString("N");
            released = false;
            actionsComplete = false;
            nextStartAt = float.NegativeInfinity;
            variants.Clear();
            nextActiveByMember.Clear();
        }

        public void Queue(string memberId, Cue cue, bool isCaptain = false)
        {
            if (released || !isActiveAndEnabled || !Members.Contains(memberId ?? string.Empty) ||
                actionsComplete && cue != Cue.Victory && cue != Cue.Defeat || !CanSpeak()) return;
            float now = Time.unscaledTime;
            // Several small-skill clocks land close together. A long captain line previously
            // expired every other member's request, then won again on the next cycle. Let that
            // same member rest for one cycle so another real action can be heard. Ultimates keep
            // priority and are never blocked by this speech-only cooldown.
            if (cue == Cue.Active && nextActiveByMember.TryGetValue(memberId, out float nextActive) && now < nextActive)
                return;
            var request = new Request
            {
                Member = memberId, Cue = cue, Captain = isCaptain,
                // A fall usually coincides with the enemy's kill taunt on the same voice channel: the last words
                // wait for it (and for a teammate's ultimate line) instead of being dropped.
                ExpiresAt = now + (cue == Cue.Active ? .55f : cue == Cue.Ultimate ? 1.7f : cue == Cue.Defeat ? 3.2f : 3f)
            };
            // The first equal-priority action wins. Repeated requests cannot indefinitely postpone it.
            if (pending != null && pending.ExpiresAt >= now && pending.Priority >= request.Priority) return;
            pending = request;
        }

        public void Complete(bool victory, string memberId)
        {
            actionsComplete = true;
            // A wipe's last 阵亡台词 is the closing line of the battle: let it finish (or still start).
            bool lastWords = !victory && (pending?.Cue == Cue.Defeat ||
                currentCue == Cue.Defeat && audio != null && audio.IsVoicePlayingFor(owner));
            if (lastWords) return;
            pending = null;
            audio?.StopVoice(owner);
            // Victory recordings are optional; never substitute an attack line as a victory line.
            // Until authored clips exist, the existing victory SFX owns the result sound.
            if (victory) Queue(memberId, Cue.Victory, true);
        }

        private bool CanSpeak() => audio != null && audio.isActiveAndEnabled && audio.SfxEnabled &&
            !audio.MediaOwnsMusic && audio.CurrentMusicContext == GameAudio.MusicContext.Battle &&
            (blocked == null || !blocked());

        private void LateUpdate()
        {
            if (released) return;
            if (!CanSpeak())
            {
                pending = null;
                audio?.StopVoice(owner);
                return;
            }
            float now = Time.unscaledTime;
            if (pending == null) return;
            if (now > pending.ExpiresAt) { pending = null; return; }
            if (now < nextStartAt) return;
            if (audio.IsVoicePlaying)
            {
                // Only our own lower-priority speech may be interrupted; unrelated page voices are
                // never stopped, and a normal active skill cannot interrupt another active skill.
                if (!audio.IsVoicePlayingFor(owner) || (int)pending.Cue <= (int)currentCue) return;
                // A fall waits for a teammate's ultimate line instead of cutting it off.
                if (pending.Cue == Cue.Defeat && currentCue == Cue.Ultimate) return;
                audio.StopVoice(owner);
            }
            Request chosen = pending;
            pending = null;
            if (!Play(chosen)) return; // Missing recordings are intentionally silent, without a placeholder tone.
            currentCue = chosen.Cue;
            nextStartAt = now + MinimumStartInterval;
            if (chosen.Cue == Cue.Active) nextActiveByMember[chosen.Member] = now + ActiveMemberRepeatSeconds;
        }

        private bool Play(Request request)
        {
            string kind = request.Cue == Cue.Active ? "active" : request.Cue == Cue.Ultimate ? "ultimate"
                : request.Cue == Cue.Defeat ? "defeat" : "victory";
            string key = request.Member + "-" + kind;
            variants.TryGetValue(key, out int next);
            int first = next % 2 + 1;
            string prefix = "Battle/" + key + "-";
            int variant = first;
            bool played = audio.PlayVoice(prefix + first.ToString("00"), owner);
            // Four approved samples contain variant 01 only; a future second variant must not be
            // required for the current sample build to remain audible.
            if (!played) { variant = first == 1 ? 2 : 1; played = audio.PlayVoice(prefix + variant.ToString("00"), owner); }
            if (played)
            {
                variants[key] = next + 1;
                AudioClip clip = Resources.Load<AudioClip>("Audio/Voice/" + prefix + variant.ToString("00"));
                Spoke?.Invoke(request.Member, request.Cue, variant, clip != null ? clip.length : 2f);
            }
            return played;
        }

        public void Release()
        {
            pending = null;
            released = true;
            audio?.StopVoice(owner);
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
