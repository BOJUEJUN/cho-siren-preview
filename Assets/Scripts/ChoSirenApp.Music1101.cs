using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 「A · 唱片收藏」 lobby entry: the PSD record bar keeps its authored place and size.
    /// Tapping the cat record plays/pauses, tapping the bar or song window opens the record collection,
    /// ◀ ▶ switch songs; a teal line under the title shows progress and a torn sticker marks new songs.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private UI.MusicCollectionPage1101 musicPage1101;

        /// <summary>Opens the full-screen record collection (no-op while one is open).</summary>
        private void OpenMusicCollection1101()
        {
            if (safeRoot == null || (musicPage1101 != null && !musicPage1101.IsClosing)) return;
            musicPage1101 = UI.MusicCollectionPage1101.Open((RectTransform)safeRoot, gameAudio, model, font, Toast,
                ResumeMediaAfterUserGesture, psdHome);
        }

        private void ToggleHomeMusicFromDisc1101()
        {
            if (psdOpening || psdMotion == null || psdMotion.UiHidden) return;
            gameAudio?.PlayClick();
            ResumeMediaAfterUserGesture();
            if (gameAudio != null && gameAudio.ToggleHomeMusicPaused() && !gameAudio.MusicEnabled)
                Toast("音乐已在设置中关闭");
        }

        private void BuildHomeMusicEntry1101(Transform button, RectTransform window, RectTransform disc)
        {
            RectTransform progress = null;
            if (window != null)
            {
                // Thin teal line on the window's bottom edge (the mock's 1.5 px at 393 wide ≈ 3 here), with a soft glow.
                GameObject glow = NewImage("SongProgressGlow", window, null, new Color32(30, 232, 232, 70));
                RectTransform glowRect = glow.GetComponent<RectTransform>();
                glowRect.anchorMin = Vector2.zero; glowRect.anchorMax = new Vector2(0f, 0f);
                glowRect.pivot = Vector2.zero;
                glowRect.offsetMin = Vector2.zero; glowRect.offsetMax = new Vector2(0f, 7f);
                GameObject line = NewImage("SongProgress", glow.transform, null, new Color32(30, 232, 232, 255));
                RectTransform lineRect = line.GetComponent<RectTransform>();
                lineRect.anchorMin = Vector2.zero; lineRect.anchorMax = new Vector2(1f, 0f);
                lineRect.pivot = Vector2.zero;
                lineRect.offsetMin = Vector2.zero; lineRect.offsetMax = new Vector2(0f, 3f);
                progress = glowRect;
            }
            GameObject sticker = null;
            if (disc != null)
            {
                // The record itself plays/pauses; its hit sits above the bar's open-page hit.
                GameObject hit = NewImage("MusicDiscToggle", disc.parent, null, new Color(0, 0, 0, 0));
                RectTransform hitRect = hit.GetComponent<RectTransform>();
                hitRect.anchorMin = disc.anchorMin; hitRect.anchorMax = disc.anchorMax;
                hitRect.pivot = new Vector2(.5f, .5f);
                hitRect.anchoredPosition = disc.anchoredPosition;
                // Slightly inside the round record so the ◀ arrow at its lower-right keeps its own taps.
                hitRect.sizeDelta = disc.rect.size * .8f;
                Image hitImage = hit.GetComponent<Image>();
                hitImage.raycastTarget = true;
                Button toggle = hit.AddComponent<Button>();
                toggle.targetGraphic = hitImage;
                toggle.transition = Selectable.Transition.None;
                toggle.onClick.AddListener(ToggleHomeMusicFromDisc1101);

                // The torn teal notice sticker slapped on the record's top-right rim (mock: 17 px at 393 wide).
                sticker = UI.NoticeSticker1031.Create("MusicNewSticker", disc.parent, 31f);
                RectTransform stickerRect = (RectTransform)sticker.transform;
                stickerRect.anchorMin = disc.anchorMin; stickerRect.anchorMax = disc.anchorMax;
                stickerRect.anchoredPosition = disc.anchoredPosition + new Vector2(disc.rect.width * .373f, disc.rect.height * .439f);
                stickerRect.sizeDelta = new Vector2(31f, 31f);
            }
            button.gameObject.AddComponent<UI.HomeMusicEntry1101>().Bind(gameAudio, model, progress, sticker);
        }
    }
}
