using System;
using UnityEngine;

namespace ChoSiren.Systems.Presentation
{
    /// <summary>Animation names every presenter must understand; extra names are presenter-specific.</summary>
    public static class StageAnimations
    {
        public const string Idle = "idle";
        public const string Touch = "touch";
        public const string Greeting = "greeting";
        public const string SkillCutIn = "skill";
    }

    /// <summary>
    /// The lobby/battle only talks to this. Today it is backed by the PNG sequence; the Spine
    /// implementation (skeleton + skins) drops in behind the same surface, so swapping the
    /// character technology never requires touching ChoSirenApp.
    /// </summary>
    public interface ICharacterStagePresenter
    {
        bool IsReady { get; }
        event Action<float> LoadProgress;
        void WhenReady(Action callback);

        /// <summary>Plays an animation; unsupported names fall back to idle so the character never freezes.</summary>
        void Play(string animation, bool loop);

        /// <summary>Costume/skin id. Presenters without skins ignore it and return false.</summary>
        bool TrySetSkin(string skinId);

        /// <summary>Touch interaction from the lobby; presenters decide how to react.</summary>
        void OnTouched();
    }

    /// <summary>
    /// Adapter around the existing frame-sequence player. It has one animation, so every request
    /// maps to the idle loop; touch just restarts the fade for a visible reaction.
    /// </summary>
    [RequireComponent(typeof(SpriteSequencePlayer))]
    public sealed class SpriteSequenceStagePresenter : MonoBehaviour, ICharacterStagePresenter
    {
        private SpriteSequencePlayer player;
        private bool ready;
        private event Action readyCallbacks;

        public bool IsReady => ready;
        public event Action<float> LoadProgress;

        private void Awake()
        {
            player = GetComponent<SpriteSequencePlayer>();
            player.WarmupProgress += progress => LoadProgress?.Invoke(progress);
            player.WhenReady(() =>
            {
                ready = true;
                Action callbacks = readyCallbacks;
                readyCallbacks = null;
                callbacks?.Invoke();
            });
        }

        public void WhenReady(Action callback)
        {
            if (callback == null) return;
            if (ready) callback();
            else readyCallbacks += callback;
        }

        public void Play(string animation, bool loop)
        {
            // Single-clip presenter: nothing to switch. Kept explicit so callers can be written
            // against the Spine contract today.
        }

        public bool TrySetSkin(string skinId) => false;

        public void OnTouched()
        {
            // No touch clip in the frame sequence; a Spine presenter plays StageAnimations.Touch here.
        }
    }
}
