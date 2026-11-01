using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>A lobby-owned label subscription; destroying the lobby leaves no audio callback.</summary>
    public sealed class HomeMusicSwitcher : MonoBehaviour
    {
        private GameAudio audio;
        private Text label;
        private Text pausedMarker;

        public void Bind(GameAudio transport, Text caption, Text pauseGlyph = null)
        {
            Unsubscribe();
            audio = transport;
            label = caption;
            pausedMarker = pauseGlyph;
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            if (audio == null || !isActiveAndEnabled) return;
            audio.HomeMusicTrackChanged -= TrackChanged;
            audio.MusicEnabledChanged -= SettingChanged;
            audio.HomeMusicPausedChanged -= SettingChanged;
            audio.HomeMusicTrackChanged += TrackChanged;
            audio.MusicEnabledChanged += SettingChanged;
            audio.HomeMusicPausedChanged += SettingChanged;
        }

        private void Unsubscribe()
        {
            if (audio == null) return;
            audio.HomeMusicTrackChanged -= TrackChanged;
            audio.MusicEnabledChanged -= SettingChanged;
            audio.HomeMusicPausedChanged -= SettingChanged;
        }

        private void TrackChanged(int index) => Refresh();
        private void SettingChanged(bool enabled) => Refresh();
        private void Refresh()
        {
            if (label == null) return;
            string title = audio != null ? audio.HomeMusicTitle : HomeMusicPlaylist.Get(0).Title;
            label.text = title;
            if (pausedMarker != null)
                pausedMarker.gameObject.SetActive(audio != null && (audio.HomeMusicPaused || !audio.MusicEnabled));
        }

        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();
    }

    /// <summary>
    /// Turns the lobby record at a restrained constant rate and lets
    /// small notes rise off its rim while a song is playing. Notes come from a small reused pool
    /// (2026-11-01) so the lobby does not allocate while music plays.
    /// </summary>
    public sealed class HomeMusicDiscSpin : MonoBehaviour
    {
        private const int PoolSize = 5;
        private const float NoteLife = 1.6f;
        private GameAudio audio;
        public const float DegreesPerSecond = 18f;
        private float noteClock;
        private Font font;
        private struct Note { public RectTransform rect; public Text text; public float age; public Vector2 drift; public bool live; }
        private readonly Note[] notes = new Note[PoolSize];

        public void Bind(GameAudio transport, Font noteFont = null)
        {
            audio = transport;
            font = noteFont;
            // Spin about the record's centre without moving it.
            var rect = (RectTransform)transform;
            Vector2 size = rect.rect.size, delta = (new Vector2(.5f, .5f) - rect.pivot) * size;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition += delta;
        }

        private bool Playing => audio != null && audio.HomeMusicPlaying;

        private void Update() => AdvancePresentation(Time.unscaledDeltaTime);

        public void AdvancePresentation(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (BattlePresentationOptions.ReduceMotion || !Playing)
            {
                for (int i = 0; i < notes.Length; i++) Hide(i);
                noteClock = 0f;
                return;
            }
            transform.Rotate(0f, 0f, -DegreesPerSecond * dt);
            noteClock += dt;
            if (font != null && noteClock > .55f)
            {
                noteClock = 0f;
                SpawnNote();
            }
            for (int i = 0; i < notes.Length; i++)
            {
                if (!notes[i].live) continue;
                Note n = notes[i];
                n.age += dt;
                float k = n.age / NoteLife;
                n.rect.anchoredPosition += n.drift * dt;
                n.rect.localScale = Vector3.one * Mathf.Lerp(.7f, 1.15f, k);
                var c = n.text.color; c.a = Mathf.Clamp01(Mathf.Min(k * 5f, (1f - k) * 2f)); n.text.color = c;
                notes[i] = n;
                if (k >= 1f) Hide(i);
            }
        }

        private void Hide(int i)
        {
            if (!notes[i].live) return;
            notes[i].live = false;
            if (notes[i].rect != null) notes[i].rect.gameObject.SetActive(false);
        }

        private void SpawnNote()
        {
            int slot = -1;
            for (int i = 0; i < notes.Length; i++) if (!notes[i].live) { slot = i; break; }
            if (slot < 0) return;
            var disc = (RectTransform)transform;
            Note n = notes[slot];
            if (n.rect == null)
            {
                var go = new GameObject("MusicNote", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(disc.parent, false);
                n.rect = (RectTransform)go.transform;
                n.text = go.GetComponent<Text>();
                n.text.font = font; n.text.fontSize = 26; n.text.alignment = TextAnchor.MiddleCenter;
                n.text.raycastTarget = false;
                n.text.horizontalOverflow = HorizontalWrapMode.Overflow; n.text.verticalOverflow = VerticalWrapMode.Overflow;
                go.AddComponent<Outline>().effectColor = new Color(.1f, 0, .2f, .8f);
            }
            n.rect.gameObject.SetActive(true);
            n.rect.SetSiblingIndex(disc.GetSiblingIndex() + 1);
            n.rect.anchorMin = disc.anchorMin; n.rect.anchorMax = disc.anchorMax; n.rect.pivot = new Vector2(.5f, .5f);
            float r = disc.rect.width * .42f, angle = Random.Range(20f, 80f) * Mathf.Deg2Rad;
            n.rect.anchoredPosition = disc.anchoredPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            n.rect.sizeDelta = new Vector2(40, 40);
            n.rect.localScale = Vector3.one * .7f;
            n.text.text = Random.value < .5f ? "\u266A" : "\u266B";
            n.text.color = Random.value < .5f ? new Color32(255, 120, 220, 0) : new Color32(120, 230, 255, 0);
            n.age = 0f;
            n.drift = new Vector2(Random.Range(10f, 35f), Random.Range(45f, 70f));
            n.live = true;
            notes[slot] = n;
        }
    }

    /// <summary>Song title inside the slanted window; scrolls sideways when it does not fit.</summary>
    public sealed class HomeMusicMarquee : MonoBehaviour
    {
        private const float Gap = 60f, Speed = 38f, Pause = 1.2f;
        private Text first, second;
        private float offset, hold;

        public void Bind(Text label)
        {
            first = label;
            second = Instantiate(label.gameObject, label.transform.parent).GetComponent<Text>();
            second.name = "SongTitleLoop";
        }

        private void LateUpdate()
        {
            if (first == null) return;
            if (second.text != first.text) { second.text = first.text; offset = 0f; hold = Pause; }
            float view = ((RectTransform)transform).rect.width, width = first.preferredWidth;
            bool scroll = width > view && !BattlePresentationOptions.ReduceMotion;
            second.gameObject.SetActive(scroll);
            if (!scroll) { Place(first, 0f, view, width); return; }
            if (hold > 0f) hold -= Time.unscaledDeltaTime;
            else offset += Speed * Time.unscaledDeltaTime;
            if (offset >= width + Gap) { offset = 0f; hold = Pause; }
            Place(first, -offset, view, width);
            Place(second, width + Gap - offset, view, width);
        }

        private static void Place(Text text, float x, float view, float width)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, .5f);
            rect.sizeDelta = new Vector2(Mathf.Max(width, view) + 4f, 0f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
