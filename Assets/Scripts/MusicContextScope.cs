using System;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>A panel-owned context lease; disabling or destroying it releases only its own lease.</summary>
    public sealed class MusicContextScope : MonoBehaviour
    {
        private GameAudio audio;
        private GameAudio.MusicContext context;
        private string owner;
        private bool configured;
        private bool acquired;

        public static MusicContextScope Attach(GameObject host, GameAudio.MusicContext context)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var scope = host.GetComponent<MusicContextScope>();
            if (scope == null) scope = host.AddComponent<MusicContextScope>();
            scope.Bind(context);
            return scope;
        }

        public static MusicContextScope Attach(Transform host, GameAudio.MusicContext context)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            return Attach(host.gameObject, context);
        }

        private void Bind(GameAudio.MusicContext value)
        {
            GameAudio parentAudio = GetComponentInParent<GameAudio>(true);
            if (configured && audio == parentAudio && context == value) { Acquire(); return; }
            Release();
            audio = parentAudio;
            context = value;
            configured = true;
            if (string.IsNullOrEmpty(owner)) owner = "context:" + Guid.NewGuid().ToString("N");
            Acquire();
        }

        private void Acquire()
        {
            if (!configured || acquired || !isActiveAndEnabled) return;
            if (audio == null) audio = GetComponentInParent<GameAudio>(true);
            if (audio == null) return; // Standalone presentation fixtures need no global app transport.
            audio.SetContextMusicActive(owner, context, true);
            acquired = true;
        }

        public void Release()
        {
            if (!acquired) return;
            if (audio != null) audio.SetContextMusicActive(owner, context, false);
            acquired = false;
        }

        private void OnEnable() => Acquire();
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
