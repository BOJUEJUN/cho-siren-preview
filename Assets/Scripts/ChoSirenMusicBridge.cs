using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>Explicit browser music controls, independent of source buffer sizes or page lifetimes.</summary>
    public sealed class ChoSirenMusicBridge : MonoBehaviour
    {
        public const string ObjectName = "ChoSirenMusicBridge";
        private GameAudio audio;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ChoSirenMusicBridgeBind(string objectName, int enabled, int hasSoundtrack);
        [DllImport("__Internal")]
        private static extern void ChoSirenMusicBridgeSync(int enabled);
        [DllImport("__Internal")]
        private static extern void ChoSirenMusicBridgeUnbind(string objectName);
        [DllImport("__Internal")]
        private static extern int ChoSirenMusicBridgeHasMediaOwner();
#endif

        internal static void Attach(GameAudio owner)
        {
            ChoSirenMusicBridge bridge = owner.GetComponentInChildren<ChoSirenMusicBridge>(true);
            if (bridge == null)
            {
                var node = new GameObject(ObjectName);
                node.transform.SetParent(owner.transform, false);
                bridge = node.AddComponent<ChoSirenMusicBridge>();
            }
            if (bridge.audio != null) bridge.audio.MusicEnabledChanged -= bridge.SyncMusicEnabled;
            bridge.audio = owner;
            bridge.gameObject.name = ObjectName;
            owner.MusicEnabledChanged += bridge.SyncMusicEnabled;
#if UNITY_WEBGL && !UNITY_EDITOR
            owner.SetMediaMusicActive("browser:initial", ChoSirenMusicBridgeHasMediaOwner() != 0);
#endif
            bridge.Bind();
        }

        private void Bind()
        {
            if (audio == null) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenMusicBridgeBind(ObjectName, audio.MusicEnabled ? 1 : 0, audio.HasSoundtrack ? 1 : 0);
#endif
        }

        public void SetMusicDucked(string value)
        {
            if (TrySwitch(value, out bool enabled)) audio?.SetMusicDucked(enabled);
        }

        public void SetMediaMusicOwners(string owners) => audio?.SetBrowserMediaMusicOwners(owners);

        public void SetMusicEnabled(string value)
        {
            if (TrySwitch(value, out bool enabled)) audio?.SetMusicEnabled(enabled);
            // Also acknowledge an unchanged preference so the title never displays a stale pending choice.
            if (audio != null) SyncMusicEnabled(audio.MusicEnabled);
        }

        private static bool TrySwitch(string value, out bool enabled)
        {
            if (value == "1") { enabled = true; return true; }
            if (value == "0") { enabled = false; return true; }
            return bool.TryParse(value, out enabled);
        }

        private void SyncMusicEnabled(bool enabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenMusicBridgeSync(enabled ? 1 : 0);
#endif
        }

        private void OnEnable() => Bind();

        private void OnDisable()
        {
            audio?.SetMusicDucked(false);
            audio?.SetBrowserMediaMusicOwners(string.Empty);
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenMusicBridgeUnbind(ObjectName);
#endif
        }

        private void OnDestroy()
        {
            if (audio != null) audio.MusicEnabledChanged -= SyncMusicEnabled;
        }
    }
}
