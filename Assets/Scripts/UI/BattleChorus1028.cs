using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>A brief wordless harmonized response after an ultimate's callout, never a second song.</summary>
    public sealed class BattleChorus1028 : MonoBehaviour
    {
        public const float RepeatGapSeconds = 7f;
        private GameAudio audio;
        private Func<bool> blocked;
        private string owner, pendingMember;
        private float earliest, deadline, nextAt;
        private readonly Dictionary<string, int> takes = new Dictionary<string, int>();

        public void Configure(GameAudio transport, Func<bool> isBlocked)
        {
            Release();
            audio = transport;
            blocked = isBlocked;
            owner = "battle-chorus:" + Guid.NewGuid().ToString("N");
            nextAt = float.NegativeInfinity;
            if (audio != null) audio.BattleChorusInterrupted += CancelPending;
        }

        public void Queue(string memberId)
        {
            if (!Ready || pendingMember != null || Time.unscaledTime < nextAt) return;
            if (memberId != "xingli" && memberId != "feiyin" && memberId != "wubai" && memberId != "yeying") return;
            pendingMember = memberId;
            earliest = Time.unscaledTime + .45f;
            deadline = Time.unscaledTime + 4.5f;
        }

        private bool Ready => isActiveAndEnabled && audio != null && audio.BattleChorusAvailable &&
            owner != null && (blocked == null || !blocked());

        private void LateUpdate()
        {
            if (!Ready) { pendingMember = null; audio?.StopBattleChorus(owner); return; }
            if (pendingMember == null) return;
            float now = Time.unscaledTime;
            if (now > deadline) { pendingMember = null; return; }
            if (now < earliest || audio.IsVoicePlaying || audio.IsBattleChorusPlaying) return;
            string member = pendingMember;
            pendingMember = null;
            takes.TryGetValue(member, out int take);
            if (!audio.PlayBattleChorus(member, take, owner)) return;
            takes[member] = take + 1;
            nextAt = now + RepeatGapSeconds;
        }

        public void Release()
        {
            if (audio != null) audio.BattleChorusInterrupted -= CancelPending;
            CancelPending();
            owner = null;
        }

        public void CancelPending()
        {
            pendingMember = null;
            audio?.StopBattleChorus(owner);
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
