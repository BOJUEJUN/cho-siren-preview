using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Hand-drawn hit flipbooks (2026-10-21): GPT-drawn 4×4 sprite sheets in Resources/UI/Fx1022
    /// (hit-charm / hit-demon / hit-bloodelf / hit-mermaid / hit-enemy / ult-{race}, all 1024 px for phone memory),
    /// black background converted to alpha.
    /// Plays 16 frames over the target at ~30 fps; glows add light, water uses normal blending.
    /// </summary>
    public sealed class Flipbook1022 : MonoBehaviour
    {
        const int Columns = 4, Frames = 16, MaxLive = 8;
        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        public static int Live { get; private set; }

        Sprite[] frames;
        Image image;
        Func<bool> paused;
        float clock, fps;

        /// <summary>Frames cut from Resources/UI/Fx1022/{key}.png, or null when the sheet is missing.</summary>
        public static Sprite[] Load(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (cache.TryGetValue(key, out Sprite[] cached)) return cached;
            var sheet = Resources.Load<Texture2D>("UI/Fx1022/" + key);
            Sprite[] result = null;
            if (sheet != null)
            {
                result = new Sprite[Frames];
                float cw = sheet.width / (float)Columns, ch = sheet.height / (float)Columns;
                for (int i = 0; i < Frames; i++)
                {
                    int col = i % Columns, row = i / Columns;
                    // Sprite rects start bottom-left; the sheet reads top-left first.
                    var rect = new Rect(col * cw, sheet.height - (row + 1) * ch, cw, ch);
                    result[i] = Sprite.Create(sheet, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                    result[i].name = key + "-" + i;
                }
            }
            cache[key] = result;
            return result;
        }

        public static Flipbook1022 Play(RectTransform layer, RectTransform target, string key, float size, bool additive,
            float rotation, Func<bool> isPaused, float framesPerSecond = 30f)
        {
            if (layer == null || target == null || !Application.isPlaying || Live >= MaxLive) return null;
            Sprite[] sprites = Load(key);
            if (sprites == null) return null;
            var go = new GameObject("Flipbook1022-" + key, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(layer, false); go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.localEulerAngles = new Vector3(0, 0, rotation);
            Vector3 world = target.TransformPoint(target.rect.center);
            rect.anchoredPosition = (Vector2)layer.InverseTransformPoint(world) - layer.rect.center;
            var fx = go.AddComponent<Flipbook1022>();
            fx.frames = sprites; fx.paused = isPaused; fx.fps = Mathf.Max(1f, framesPerSecond);
            fx.image = go.GetComponent<Image>();
            fx.image.raycastTarget = false;
            fx.image.sprite = sprites[0];
            if (additive && BattleFxArt1021.Additive != null) fx.image.material = BattleFxArt1021.Additive;
            return fx;
        }

        void Awake() => Live++;
        void OnDestroy() => Live = Mathf.Max(0, Live - 1);

        void Update()
        {
            if (paused != null && paused()) return;
            clock += BattleHitStop.Delta(Mathf.Min(Time.unscaledDeltaTime, .05f));
            int frame = (int)(clock * fps);
            if (frame >= frames.Length) { Destroy(gameObject); return; }
            image.sprite = frames[frame];
        }
    }
}
