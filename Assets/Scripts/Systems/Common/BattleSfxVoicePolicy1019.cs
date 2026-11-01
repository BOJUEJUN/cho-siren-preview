using System;
using System.Collections.Generic;

namespace ChoSiren
{
    /// <summary>
    /// Explicit voice admission, also effective on WebGL where AudioSource.priority cannot
    /// impose a channel limit. This policy never consumes the battle's random generator.
    /// </summary>
    public sealed class BattleSfxVoicePolicy1019
    {
        public const int VoiceLimit = 6;
        public const float SameSoundGap = .07f;
        // ±4.5% (research 2026-10-21 §4.7): repeated hits must not sound copy-pasted.
        private static readonly float[] PitchOffsets = { 0f, .03f, -.025f, .045f, -.04f, .015f };
        private readonly Slot[] slots = new Slot[VoiceLimit];
        private readonly Dictionary<string, float> lastAccepted = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> variants = new Dictionary<string, int>(StringComparer.Ordinal);
        private long sequence;

        private struct Slot
        {
            public string Name;
            public int Priority;
            public float EndsAt;
            public long Sequence;
        }

        public readonly struct Voice
        {
            public readonly int Channel;
            public readonly int Priority;
            public readonly float Pitch;
            public readonly float Gain;
            public readonly bool ReplacesVoice;
            public Voice(int channel, int priority, float pitch, float gain, bool replacesVoice)
            { Channel = channel; Priority = priority; Pitch = pitch; Gain = gain; ReplacesVoice = replacesVoice; }
        }

        public int RejectedDuplicateCount { get; private set; }
        public int RejectedPriorityCount { get; private set; }

        public int ActiveVoiceCount(float now)
        {
            int count = 0;
            for (int i = 0; i < slots.Length; i++) if (Active(slots[i], now)) count++;
            return count;
        }

        public bool TryReserve(string name, float duration, float gain, float now, out Voice voice)
        {
            voice = default;
            if (string.IsNullOrEmpty(name) || !Finite(now) || !Finite(duration) || duration <= 0f ||
                !Finite(gain) || gain <= 0f) return false;
            if (lastAccepted.TryGetValue(name, out float last) && now - last < SameSoundGap - .000001f)
            { RejectedDuplicateCount++; return false; }

            Profile(name, out int priority, out int categoryLimit, out bool varyPitch);
            int free = -1, sameOldest = -1, lowest = -1, sameCount = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (!Active(slot, now)) { if (free < 0) free = i; continue; }
                if (slot.Name == name)
                {
                    sameCount++;
                    if (sameOldest < 0 || slot.Sequence < slots[sameOldest].Sequence) sameOldest = i;
                }
                if (lowest < 0 || slot.Priority < slots[lowest].Priority ||
                    (slot.Priority == slots[lowest].Priority && slot.Sequence < slots[lowest].Sequence)) lowest = i;
            }

            int selected = sameCount >= categoryLimit ? sameOldest : free;
            if (selected < 0)
            {
                if (lowest < 0 || priority < slots[lowest].Priority)
                { RejectedPriorityCount++; return false; }
                selected = lowest;
            }

            variants.TryGetValue(name, out int variant);
            float pitch = varyPitch ? 1f + PitchOffsets[variant % PitchOffsets.Length] : 1f;
            variants[name] = (variant + 1) % PitchOffsets.Length;
            bool replaces = Active(slots[selected], now);
            slots[selected] = new Slot { Name = name, Priority = priority, EndsAt = now + duration / pitch, Sequence = ++sequence };
            lastAccepted[name] = now;
            voice = new Voice(selected, priority, pitch, Math.Min(1f, gain), replaces);
            return true;
        }

        public void Clear()
        {
            Array.Clear(slots, 0, slots.Length);
            lastAccepted.Clear();
            // Keep the effect variation sequence across mute/pause without introducing randomness.
        }

        private static bool Active(Slot slot, float now) => slot.Name != null && slot.EndsAt > now;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Profile(string name, out int priority, out int categoryLimit, out bool varyPitch)
        {
            categoryLimit = 1;
            varyPitch = false;
            switch (name)
            {
                case "hit": priority = 20; categoryLimit = 2; varyPitch = true; break;
                case "hurt": priority = 25; categoryLimit = 2; varyPitch = true; break;
                case "block": priority = 28; categoryLimit = 2; varyPitch = true; break;
                case "crit": priority = 50; categoryLimit = 2; varyPitch = true; break;
                case "debuff": priority = 24; break;
                case "heal": priority = 35; break;
                case "shield": priority = 45; break;
                case "dice": priority = 60; varyPitch = true; break;
                case "strike-charm": case "strike-demon": case "strike-bloodelf": case "strike-mermaid":
                    priority = 30; categoryLimit = 2; varyPitch = true; break;
                case "ultimate": priority = 80; break;
                case "ko": priority = 85; break;
                case "victory": priority = 100; break;
                case "cut": priority = 55; break;
                case "stamp": priority = 40; break;
                case "glitch": priority = 10; break;
                // 2026-10-31: boss impacts, a member falling, and the ultimate judgement stingers.
                case "boss-hit": priority = 52; categoryLimit = 2; varyPitch = true; break;
                case "down": priority = 70; break;
                case "grade-perfect": case "grade-great": case "grade-good": priority = 90; break;
                case "grade-miss": priority = 60; break;
                default: priority = 15; break;
            }
        }
    }
}
