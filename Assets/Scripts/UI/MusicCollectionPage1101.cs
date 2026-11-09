using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-08 redesign (少爷: "重做这个点进去的歌页面…随机播放，支持单曲循环、随机、顺序"): a record player page on the shared
    /// page background — a big spinning vinyl with the cover on its label, the title, a full-width progress bar, the
    /// play-mode / previous / play / next / favourite row, and the song list as rows (no more empty frames).
    /// The original 2026-11-01 notes follow.
    /// 2026-11-01 「A · 唱片收藏」: the full-screen record collection opened from the lobby's song window.
    /// Built on the user's accessory board (Art/Music1101/music-board-a, text erased) at its authored
    /// 858×1832 coordinates (Builds/qa-logs/music-1031/mock2, windows.json "A"): the big torn frame holds the
    /// current cover with a spinning record and level bars, the right frame shows 正在播放 with title, artist,
    /// favourite / 首页曲, a draggable progress bar and the interview board's own prev / play / next buttons,
    /// the crystal frame switches the play mode, and twelve torn frames per page hold the records.
    /// Everything reads and drives the single <see cref="GameAudio"/> Home transport.
    /// </summary>
    public sealed class MusicCollectionPage1101 : MonoBehaviour
    {
        public const float BoardWidth = 858f, BoardHeight = 1832f;
        public const int CellsPerPage = 5;
        private const float RowX = 40f, RowTop = 1172f, RowW = 778f, RowH = 100f, RowStep = 110f;
        private static readonly Vector2 DiscCentre = new Vector2(429f, 438f);
        private const float DiscSize = 480f;
        private const string Art = "Art/Music1101/";
        public static readonly Color32 Teal = new Color32(30, 232, 232, 255);
        private static readonly Color32 Pink = new Color32(255, 61, 154, 255);
        private static readonly Color32 Gold = new Color32(255, 211, 77, 255);
        private static readonly Color32 Ink = new Color32(13, 6, 24, 255);
        private static readonly Color32 ChipOff = new Color32(40, 18, 86, 235);
        private static readonly Color32 ChipOffText = new Color32(233, 226, 255, 255);
        private static readonly Color32 Mint = new Color32(159, 233, 236, 255);
        private static readonly Color32 Lilac = new Color32(182, 169, 220, 255);
        private static readonly Rect HeroWindow = new Rect(16, 189, 397, 445);
        // windows.json "A" c0..c11: [left, top, width, height] of each frame's dark interior.
        private static readonly Rect[] CellWindows =
        {
            new Rect(38, 772, 255, 207), new Rect(304, 776, 244, 200), new Rect(558, 780, 257, 192),
            new Rect(32, 983, 242, 184), new Rect(294, 981, 251, 187), new Rect(559, 983, 252, 183),
            new Rect(33, 1185, 255, 175), new Rect(289, 1185, 260, 171), new Rect(555, 1188, 263, 171),
            new Rect(33, 1367, 258, 169), new Rect(289, 1364, 267, 178), new Rect(554, 1358, 267, 187),
        };
        // 2026-11-01 (用户): only the songs the user supplied are listed, so the tabs stay minimal.
        public static readonly string[] CategoryIds = { "all", "fav" };
        private static readonly string[] CategoryNames = { "全部", "♥" };

        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static Sprite softDot;

        private GameAudio audio;
        private GameModel model;
        private Font font;
        private Action<string> toast;
        private Action userGesture;
        private GameObject owner;
        private RectTransform safeRoot, page, safeBox, board;
        private CanvasGroup group;
        private Vector2 lastSafeMin = new Vector2(-1, -1), lastSafeMax, lastBoxSize;
        private float fitScale = 1f, transition, closingFrom = -1f;
        private bool closing;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<int> visible = new List<int>();

        // now playing
        private RawImage heroCover;
        private Material heroMaterial;
        private RectTransform vinyl;
        private Text titleText, artistText, curTimeText, durTimeText, modeText, pageText;
        private RectTransform infoFlow, favChip, homeChip, seekRoot, seekThumb;
        private MusicQuad1101 favQuad, homeQuad, seekTrack;
        private Text favLabel, homeLabel;
        private MusicGlyph1101 playGlyph;
        private MusicSeekBar1101 seekBar;
        private Image[] heroEq;
        private int shownCur = -1, shownDur = -1;
        private bool eqMoving = true;
        private int titleLines = 1;

        // grid
        private int category, pageIndex;
        private readonly MusicQuad1101[] categoryQuads = new MusicQuad1101[4];
        private readonly Text[] categoryLabels = new Text[4];
        private Cell[] cells;

        private sealed class Cell
        {
            public RectTransform Root;
            public MusicQuad1101 Plate;
            public RawImage Cover;
            public Material Material;
            public Text Title, Sub, LockText, Heart;
            public GameObject Now, Sticker, Eq;
            public Image[] Bars;
            public int Track = -1;
        }

        public int Category => category;
        public int PageIndex => pageIndex;
        public int PageCount => Mathf.Max(1, (visible.Count + CellsPerPage - 1) / CellsPerPage);
        public IReadOnlyList<int> VisibleTracks => visible;
        public RectTransform Board => board;
        public bool IsClosing => closing;

        public static MusicCollectionPage1101 Open(RectTransform safeRoot, GameAudio audio, GameModel model, Font font,
            Action<string> toast, Action userGesture, GameObject owner)
        {
            var go = new GameObject("MusicCollection1101", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.layer = safeRoot.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(safeRoot, false);
            rect.SetAsLastSibling();
            var blocker = go.GetComponent<Image>();
            blocker.color = new Color32(10, 4, 24, 255);
            blocker.raycastTarget = true;
            var view = go.AddComponent<MusicCollectionPage1101>();
            view.Build(rect, safeRoot, audio, model, font, toast, userGesture, owner);
            return view;
        }

        // ------------------------------------------------------------------ build

        private void Build(RectTransform root, RectTransform safe, GameAudio transport, GameModel gameModel, Font uiFont,
            Action<string> toastSink, Action gesture, GameObject lobby)
        {
            page = root; safeRoot = safe; audio = transport; model = gameModel; font = uiFont;
            toast = toastSink; userGesture = gesture; owner = lobby;
            group = GetComponent<CanvasGroup>();
            safeBox = NewRect("SafeBox", page);
            board = NewRect("Board", safeBox);
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(.5f, .5f);
            board.sizeDelta = new Vector2(BoardWidth, BoardHeight);
            FitToScreen(true);

            Sprite boardSprite = Resources.Load<Sprite>("Art/Ui1107/page-bg-board");
            Image boardArt = NewImage("BoardArt", board, boardSprite, 1, 0, BoardWidth - 2, BoardHeight);
            if (boardSprite == null) boardArt.color = new Color32(26, 10, 60, 255);
            else ScreenEdgeExtend.Attach(boardArt.rectTransform, boardSprite);

            BuildNowPlaying();
            BuildCollection();
            Button close = NewButton("Close", board, 780, 14, 70, 70, Close);
            Image x = close.GetComponent<Image>();
            x.sprite = Resources.Load<Sprite>(Art + "x");
            x.preserveAspect = true;
            x.color = Color.white;

            Subscribe();
            RefreshAll();
            PsdHomeStageBridge.SetActive(false);
            transition = BattlePresentationOptions.ReduceMotion ? 1f : 0f;
            ApplyTransition();
        }

        private Image modeIcon;
        private Text modeOne;

        private void BuildNowPlaying()
        {
            NewLabel("PageTitle", board, "唱片机", 42, Color.white, FontStyle.BoldAndItalic, 44, 30, -4f, true);

            // The record: a soft teal glow, a drop shadow, the vinyl (spins while playing) with the cover on its label.
            Image glow = NewImage("DiscGlow", board, SoftDot(), DiscCentre.x - 340f, DiscCentre.y - 340f, 680f, 680f);
            glow.color = new Color(Teal.r / 255f, Teal.g / 255f, Teal.b / 255f, .16f);
            Image shadow = NewImage("VinylShadow", board, SoftDot(), DiscCentre.x - 262f, DiscCentre.y - 250f, 540f, 540f);
            shadow.color = new Color(0, 0, 0, .7f);
            vinyl = NewImage("Vinyl", board, Resources.Load<Sprite>(Art + "vinyl"), DiscCentre.x - DiscSize * .5f,
                DiscCentre.y - DiscSize * .5f, DiscSize, DiscSize).rectTransform;
            CenterPivot(vinyl);
            heroMaterial = CoverMaterial("mask-circle", 0f);
            heroCover = NewCover("HeroCover", vinyl, new Rect(DiscSize * .5f - 100f, DiscSize * .5f - 100f, 200f, 200f), heroMaterial);
            Image hub = NewImage("Hub", vinyl, SoftDot(), DiscSize * .5f - 14f, DiscSize * .5f - 14f, 28f, 28f);
            hub.color = new Color(.05f, .02f, .1f, 1f);
            heroEq = NewEq("HeroLevels", board, DiscCentre.x - 25f, DiscCentre.y + DiscSize * .5f + 6f, 4, 10f, 34f);

            infoFlow = NewRect("Info", board);
            Place(infoFlow, 60, 740, 738, 220);
            titleText = NewText("SongTitle", infoFlow, string.Empty, 44, Color.white, FontStyle.Bold, TextAnchor.UpperCenter);
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.lineSpacing = .85f;
            AddShadow(titleText);
            artistText = NewText("SongArtist", infoFlow, string.Empty, 24, Mint, FontStyle.Normal, TextAnchor.UpperCenter);
            homeChip = NewChip("HomeSongChip", infoFlow, "设为首页曲", out homeQuad, out homeLabel, SetHomeSong);

            seekRoot = NewRect("Seek", board);
            seekTrack = NewQuad("Track", seekRoot, new Color(1, 1, 1, .08f));
            seekTrack.SetInsets(0, 0, .01f, .01f);
            seekTrack.FillColor = Teal;
            Stretch(seekTrack.rectTransform, 0, 18, 0, -18); // 8 px track in a taller touch area
            MusicQuad1101 knob = NewQuad("Thumb", seekRoot, Color.white);
            knob.SetInsets(.08f, 0f, .08f, 0f);
            seekThumb = knob.rectTransform;
            seekThumb.anchorMin = seekThumb.anchorMax = new Vector2(0, .5f);
            seekThumb.pivot = new Vector2(.5f, .5f);
            seekThumb.sizeDelta = new Vector2(26, 10);
            knob.gameObject.AddComponent<Shadow>().effectColor = new Color(Teal.r / 255f, Teal.g / 255f, Teal.b / 255f, .5f);
            seekBar = seekRoot.gameObject.AddComponent<MusicSeekBar1101>();
            Image seekHit = seekRoot.gameObject.AddComponent<Image>();
            seekHit.color = Color.clear;
            seekHit.raycastTarget = true;
            seekBar.Committed = value =>
            {
                userGesture?.Invoke();
                audio?.SeekHomeMusic(value);
            };
            Place(seekRoot, 70, 900, 718, 44);
            curTimeText = NewText("Elapsed", board, "0:00", 22, Lilac, FontStyle.Normal, TextAnchor.UpperLeft);
            Place(curTimeText.rectTransform, 70, 942, 200, 30);
            durTimeText = NewText("Duration", board, "0:00", 22, Lilac, FontStyle.Normal, TextAnchor.UpperRight);
            Place(durTimeText.rectTransform, 588, 942, 200, 30);

            // Transport row: play mode · previous · play / pause · next · favourite.
            const float rowY = 1000f;
            Button modeButton = NewButton("PlayMode", board, 54, rowY - 6, 130, 110, CycleMode);
            modeIcon = NewImage("Icon", modeButton.transform, null, 41, 4, 48, 48);
            modeIcon.preserveAspect = true;
            modeIcon.color = Color.white;
            modeOne = NewText("One", modeButton.transform, "1", 18, Teal, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(modeOne.rectTransform, 77, 0, 24, 24);
            modeText = NewText("PlayModeValue", modeButton.transform, HomeMusicPlaylist.PlayModeNames[0], 20,
                new Color32(255, 122, 208, 255), FontStyle.Bold, TextAnchor.UpperCenter);
            Place(modeText.rectTransform, 0, 60, 130, 30);
            Button prev = NewButton("Previous", board, 222, rowY + 14, 112, 52, () => { Click(); audio?.PreviousHomeMusic(); });
            SetButtonArt(prev, "b-prev");
            Button toggle = NewButton("PlayPause", board, 358, rowY - 10, 142, 104, TogglePause);
            SetButtonArt(toggle, "b-diamond");
            playGlyph = NewGraphic<MusicGlyph1101>("Glyph", toggle.transform);
            playGlyph.raycastTarget = false;
            RectTransform glyph = playGlyph.rectTransform;
            glyph.anchorMin = glyph.anchorMax = glyph.pivot = new Vector2(.5f, .5f);
            glyph.sizeDelta = new Vector2(34, 34);
            Button next = NewButton("Next", board, 524, rowY + 14, 112, 52, () => { Click(); audio?.NextHomeMusic(); });
            SetButtonArt(next, "b-next");
            favChip = NewChip("FavoriteChip", board, "♥ 收藏", out favQuad, out favLabel, ToggleFavorite);
            favChip.anchoredPosition = new Vector2(676, -(rowY + 22));
        }

        private void BuildCollection()
        {
            NewLabel("CollectionTitle", board, "歌单", 34, Color.white, FontStyle.BoldAndItalic, 44, 1106, 0f, true);
            float x = 150f;
            for (int i = 0; i < CategoryIds.Length; i++)
            {
                int index = i;
                RectTransform chip = NewChip("Category-" + CategoryIds[i], board, CategoryNames[i], out categoryQuads[i],
                    out categoryLabels[i], () => SelectCategory(index));
                chip.anchoredPosition = new Vector2(x, -1112f);
                x += chip.sizeDelta.x + 8f;
            }
            cells = new Cell[CellsPerPage];
            for (int k = 0; k < CellsPerPage; k++) cells[k] = BuildCell(k);
            float pagerY = RowTop + RowStep * CellsPerPage + 6f;
            Button previousPage = NewButton("PreviousPage", board, 40, pagerY, 180, 60, () => { Click(); TurnPage(-1); });
            Text pl = NewText("Label", previousPage.transform, "‹ 上一页", 24, ChipOffText, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(pl.rectTransform);
            Button nextPage = NewButton("NextPage", board, 638, pagerY, 180, 60, () => { Click(); TurnPage(1); });
            Text nl = NewText("Label", nextPage.transform, "下一页 ›", 24, ChipOffText, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(nl.rectTransform);
            pageText = NewText("PageNumber", board, "1 / 1", 24, new Color32(207, 194, 255, 255), FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(pageText.rectTransform, 365, pagerY + 8f, 128, 44);
        }

        /// <summary>One song row: a slanted dark plate, the rounded cover, title, artist · length, NOW levels and ♥.</summary>
        private Cell BuildCell(int k)
        {
            var cell = new Cell();
            int slot = k;
            float y = RowTop + k * RowStep;
            Button button = NewButton("Record-" + k, board, RowX, y, RowW, RowH, () => TapCell(slot));
            cell.Root = (RectTransform)button.transform;
            cell.Plate = NewQuad("Plate", cell.Root, ChipOff);
            cell.Plate.SetInsets(.015f, 0f, .015f, 0f);
            Stretch(cell.Plate.rectTransform);
            cell.Material = CoverMaterial("mask-row", 0f);
            cell.Cover = NewCover("Cover", cell.Root, new Rect(22, 10, 80, 80), cell.Material);
            cell.Title = NewText("Title", cell.Root, string.Empty, 26, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(cell.Title.rectTransform, 124, 12, RowW - 330, 40);
            AddShadow(cell.Title);
            cell.Sub = NewText("Artist", cell.Root, string.Empty, 18, Mint, FontStyle.Normal, TextAnchor.MiddleLeft);
            Place(cell.Sub.rectTransform, 124, 54, RowW - 330, 30);
            cell.LockText = NewText("Locked", cell.Root, string.Empty, 20, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(cell.LockText.rectTransform, 124, 0, RowW - 330, RowH);
            cell.Bars = NewEq("Bars", cell.Root, RowW - 190, 32, 3, 8f, 34f);
            cell.Eq = cell.Bars[0].transform.parent.gameObject;

            RectTransform now = NewRect("Now", cell.Root);
            Place(now, RowW - 150, 34, 62, 28);
            now.localEulerAngles = new Vector3(0, 0, 6f);
            MusicQuad1101 plate = NewQuad("Plate", now, Gold);
            Stretch(plate.rectTransform);
            Text nowText = NewText("Label", now, "NOW", 18, Ink, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter);
            nowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Stretch(nowText.rectTransform);
            cell.Now = now.gameObject;

            cell.Heart = NewText("Favorite", cell.Root, "♥", 30, Pink, FontStyle.Normal, TextAnchor.MiddleCenter);
            Place(cell.Heart.rectTransform, RowW - 70, 26, 44, 44);

            GameObject sticker = NoticeSticker1031.Create("NewSticker", cell.Root, 40f);
            RectTransform stickerRect = (RectTransform)sticker.transform;
            stickerRect.anchorMin = stickerRect.anchorMax = new Vector2(0, 1);
            stickerRect.anchoredPosition = new Vector2(96f, -8f);
            stickerRect.sizeDelta = new Vector2(40, 40);
            cell.Sticker = sticker;
            return cell;
        }

        // ------------------------------------------------------------------ actions

        private void Click()
        {
            userGesture?.Invoke();
            audio?.PlayClick();
        }

        private bool CanControl => audio != null && audio.CurrentMusicContext == GameAudio.MusicContext.Home;

        private void WarnIfMuted()
        {
            if (audio != null && !audio.MusicEnabled) toast?.Invoke("音乐已在设置中关闭");
        }

        public void TogglePause()
        {
            Click();
            if (!CanControl) return;
            audio.ToggleHomeMusicPaused();
            WarnIfMuted();
        }

        /// <summary>Plays the song in a grid slot; the current song toggles play/pause instead.</summary>
        public void TapCell(int slot)
        {
            Cell cell = cells[slot];
            if (cell.Track < 0) return;
            PlayTrack(cell.Track);
        }

        public void PlayTrack(int index)
        {
            Click();
            if (!CanControl) return;
            if (!HomeMusicPlaylist.IsUnlocked(index, model))
            {
                HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(index);
                toast?.Invoke(string.IsNullOrEmpty(track.UnlockHint) ? "这首歌还没有解锁" : track.UnlockHint + " 解锁");
                return;
            }
            if (index == audio.HomeMusicTrackIndex)
            {
                audio.ToggleHomeMusicPaused();
                WarnIfMuted();
                return;
            }
            audio.SelectHomeMusic(index);
            if (audio.HomeMusicPaused) audio.ToggleHomeMusicPaused();
            WarnIfMuted();
        }

        public void ToggleFavorite()
        {
            Click();
            if (audio == null || model == null) return;
            string id = HomeMusicPlaylist.Get(audio.HomeMusicTrackIndex).Id;
            toast?.Invoke(model.ToggleMusicFavorite(id) ? "已收藏" : "已取消收藏");
        }

        public void SetHomeSong()
        {
            Click();
            if (audio == null || model == null) return;
            HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(audio.HomeMusicTrackIndex);
            model.SetHomeMusicId(track.Id);
            toast?.Invoke("以后进首页默认放「" + track.Title + "」");
        }

        public void CycleMode()
        {
            Click();
            if (model == null) return;
            var next = (HomeMusicPlaylist.PlayMode)(((int)model.MusicPlayMode + 1) % 3);
            model.SetMusicPlayMode(next);
            toast?.Invoke("播放模式：" + HomeMusicPlaylist.PlayModeNames[(int)next]);
        }

        public void SelectCategory(int index)
        {
            Click();
            if (index < 0 || index >= CategoryIds.Length || index == category) return;
            category = index;
            pageIndex = 0;
            RefreshGrid();
        }

        public void TurnPage(int direction)
        {
            int count = PageCount;
            pageIndex = ((pageIndex + direction) % count + count) % count;
            RefreshGrid();
        }

        public void Close()
        {
            if (closing) return;
            Click();
            closing = true;
            closingFrom = transition;
            if (BattlePresentationOptions.ReduceMotion) Finish();
        }

        private void Finish()
        {
            if (owner != null && owner.activeInHierarchy) PsdHomeStageBridge.SetActive(true);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ refresh

        private void Subscribe()
        {
            if (audio != null)
            {
                audio.HomeMusicTrackChanged += OnTrackChanged;
                audio.HomeMusicPausedChanged += OnFlagChanged;
                audio.MusicEnabledChanged += OnFlagChanged;
            }
            if (model != null) model.Changed += OnModelChanged;
        }

        private void OnDestroy()
        {
            if (audio != null)
            {
                audio.HomeMusicTrackChanged -= OnTrackChanged;
                audio.HomeMusicPausedChanged -= OnFlagChanged;
                audio.MusicEnabledChanged -= OnFlagChanged;
            }
            if (model != null) model.Changed -= OnModelChanged;
            foreach (Material material in materials) if (material != null) Destroy(material);
            materials.Clear();
        }

        private void OnTrackChanged(int index) { RefreshNowPlaying(); RefreshCells(); }
        private void OnFlagChanged(bool value) => RefreshNowPlaying();
        private void OnModelChanged() { RefreshNowPlaying(); RefreshGrid(); }

        private void RefreshAll()
        {
            RefreshNowPlaying();
            RefreshGrid();
        }

        private int CurrentIndex => audio != null ? audio.HomeMusicTrackIndex : 0;

        private void RefreshNowPlaying()
        {
            if (titleText == null) return;
            int index = CurrentIndex;
            HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(index);
            SetCover(heroCover, heroMaterial, track.Cover);
            SetTitle(track.Title);
            artistText.text = track.Artist;
            bool fav = model != null && model.IsMusicFavorite(track.Id);
            // The lobby opens on this song (saved choice, or the first song when none was chosen).
            bool home = HomeMusicPlaylist.StartIndex(model) == index;
            StyleChip(favQuad, favLabel, fav ? Pink : ChipOff, fav ? Color.white : (Color)ChipOffText);
            StyleChip(homeQuad, homeLabel, home ? Teal : ChipOff, home ? (Color)Ink : ChipOffText);
            modeText.text = HomeMusicPlaylist.PlayModeNames[(int)(model != null ? model.MusicPlayMode : 0)];
            LayoutInfo();
            shownCur = shownDur = -1;
        }

        /// <summary>The song title on at most two lines (an ellipsis ends a longer one).</summary>
        private void SetTitle(string value)
        {
            Place(titleText.rectTransform, 0, 0, 738, 110);
            titleText.text = "国";
            float one = titleText.preferredHeight;
            titleText.text = value;
            titleLines = titleText.preferredHeight > one * 1.5f ? 2 : 1;
            if (titleText.preferredHeight <= one * 2.4f) return;
            for (int n = value.Length - 1; n > 0; n--)
            {
                titleText.text = value.Substring(0, n).TrimEnd() + "…";
                if (titleText.preferredHeight <= one * 2.4f) return;
            }
        }

        /// <summary>Centred title (one or two lines), artist and the 首页曲 chip under the record.</summary>
        private void LayoutInfo()
        {
            float y = titleLines * 52f + 4f;
            Place(artistText.rectTransform, 0, y, 738, 32);
            y += 32f + 12f;
            homeChip.anchoredPosition = new Vector2((738f - homeChip.sizeDelta.x) * .5f, -y);
            if (modeIcon != null && model != null)
            {
                var mode = model.MusicPlayMode;
                modeIcon.sprite = VisualB1101.Icon(mode == HomeMusicPlaylist.PlayMode.Shuffle ? "dice" : "refresh");
                modeOne.gameObject.SetActive(mode == HomeMusicPlaylist.PlayMode.SingleLoop);
            }
        }

        private void RefreshGrid()
        {
            if (cells == null) return;
            for (int i = 0; i < CategoryIds.Length; i++)
            {
                bool on = i == category;
                StyleChip(categoryQuads[i], categoryLabels[i], on ? Teal : ChipOff, on ? (Color)Ink : ChipOffText);
            }
            visible.Clear();
            string cat = CategoryIds[category];
            for (int i = 0; i < HomeMusicPlaylist.Count; i++)
            {
                HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(i);
                bool include = cat == "all" || (cat == "fav" ? model != null && model.IsMusicFavorite(track.Id) : track.InCategory(cat));
                if (include) visible.Add(i);
            }
            pageIndex = Mathf.Clamp(pageIndex, 0, PageCount - 1);
            pageText.text = (pageIndex + 1) + " / " + PageCount;
            // One page needs no pager.
            bool paged = PageCount > 1;
            pageText.gameObject.SetActive(paged);
            Transform previousPage = board.Find("PreviousPage"), nextPage = board.Find("NextPage");
            if (previousPage != null) previousPage.gameObject.SetActive(paged);
            if (nextPage != null) nextPage.gameObject.SetActive(paged);
            for (int k = 0; k < CellsPerPage; k++)
            {
                int n = pageIndex * CellsPerPage + k;
                cells[k].Track = n < visible.Count ? visible[n] : -1;
            }
            RefreshCells();
        }

        private void RefreshCells()
        {
            if (cells == null) return;
            int current = CurrentIndex;
            foreach (Cell cell in cells)
            {
                bool used = cell.Track >= 0;
                cell.Root.gameObject.SetActive(used);
                cell.Cover.gameObject.SetActive(used);
                cell.Title.gameObject.SetActive(used);
                cell.Sub.gameObject.SetActive(used);
                cell.Root.GetComponent<Button>().interactable = used;
                if (!used)
                {
                    cell.Now.SetActive(false); cell.Eq.SetActive(false); cell.Heart.gameObject.SetActive(false);
                    cell.Sticker.SetActive(false); cell.LockText.gameObject.SetActive(false);
                    continue;
                }
                HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(cell.Track);
                bool unlocked = HomeMusicPlaylist.IsUnlocked(cell.Track, model);
                SetCover(cell.Cover, cell.Material, track.Cover);
                cell.Material.SetFloat("_Gray", unlocked ? 0f : 1f);
                cell.Title.gameObject.SetActive(unlocked);
                cell.Sub.gameObject.SetActive(unlocked);
                cell.LockText.gameObject.SetActive(!unlocked);
                if (unlocked)
                {
                    FitText(cell.Title, track.Title, cell.Title.rectTransform.rect.width);
                    float length = audio != null ? audio.HomeMusicLength(cell.Track) : track.Duration;
                    FitText(cell.Sub, length > 0f ? track.Artist + " · " + HomeMusicPlaylist.FormatTime(length) : track.Artist,
                        cell.Sub.rectTransform.rect.width);
                }
                else cell.LockText.text = string.IsNullOrEmpty(track.UnlockHint) ? "未解锁" : "未解锁\n" + track.UnlockHint;
                bool isCurrent = cell.Track == current;
                cell.Plate.color = isCurrent ? new Color32(18, 92, 104, 235) : (Color)ChipOff;
                cell.Title.color = isCurrent ? (Color)Teal : Color.white;
                cell.Now.SetActive(isCurrent);
                cell.Eq.SetActive(isCurrent);
                cell.Heart.gameObject.SetActive(model != null && model.IsMusicFavorite(track.Id));
                cell.Sticker.SetActive(track.IsNew && unlocked && (model == null || !model.IsMusicHeard(track.Id)));
            }
        }

        // ------------------------------------------------------------------ per frame (no allocation)

        private void LateUpdate()
        {
            if (owner == null && !closing) { closing = true; closingFrom = transition; }
            FitToScreen(false);
            float dt = Time.unscaledDeltaTime;
            if (closing)
            {
                transition = BattlePresentationOptions.ReduceMotion ? 0f : Mathf.MoveTowards(transition, 0f, dt / .18f);
                ApplyTransition();
                if (transition <= 0f) { Finish(); return; }
            }
            else if (transition < 1f)
            {
                transition = BattlePresentationOptions.ReduceMotion ? 1f : Mathf.MoveTowards(transition, 1f, dt / .28f);
                ApplyTransition();
            }
            if (!closing && Input.GetKeyDown(KeyCode.Escape)) Close();

            bool playing = audio != null && audio.HomeMusicPlaying;
            playGlyph.ShowPause = playing;
            bool motion = playing && !BattlePresentationOptions.ReduceMotion;
            if (motion) vinyl.Rotate(0f, 0f, -dt * 360f / 7f);
            float t = Time.unscaledTime;
            if (motion || motion != eqMoving)
            {
                eqMoving = motion;
                AnimateEq(heroEq, motion, t);
                foreach (Cell cell in cells) if (cell.Eq.activeSelf) AnimateEq(cell.Bars, motion, t);
            }
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            float time = 0f, length = 0f;
            if (audio != null) audio.TryGetHomeMusicProgress(out time, out length);
            if (seekBar.Dragging) time = seekBar.Value * length;
            float p = length > 0f ? Mathf.Clamp01(time / length) : 0f;
            seekTrack.Fill = p;
            seekThumb.anchorMin = seekThumb.anchorMax = new Vector2(p, .5f);
            seekThumb.anchoredPosition = new Vector2(Mathf.Lerp(13f, -13f, p), 0f);
            seekThumb.localScale = seekBar.Dragging ? new Vector3(1.15f, 1.6f, 1f) : Vector3.one;
            int cur = Mathf.FloorToInt(time), dur = Mathf.FloorToInt(length);
            if (cur != shownCur) { shownCur = cur; curTimeText.text = HomeMusicPlaylist.FormatTime(cur); }
            if (dur != shownDur) { shownDur = dur; durTimeText.text = HomeMusicPlaylist.FormatTime(dur); }
        }

        private static void AnimateEq(Image[] bars, bool motion, float t)
        {
            if (bars == null) return;
            for (int i = 0; i < bars.Length; i++)
            {
                float k = motion ? .5f - .5f * Mathf.Cos(2f * Mathf.PI * (t + (i % 3) * .3f) / .9f) : (i % 2 == 0 ? .45f : .7f);
                RectTransform r = bars[i].rectTransform;
                r.anchorMax = new Vector2(r.anchorMax.x, .2f + .8f * k);
            }
        }

        private void ApplyTransition()
        {
            float k = Mathf.Clamp01(transition);
            group.alpha = Mathf.Clamp01(k / .8f);
            group.blocksRaycasts = !closing;
            // cubic-bezier(.2,.9,.2,1)-like ease out from 1.04 to 1.
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            board.localScale = Vector3.one * fitScale * Mathf.Lerp(1.04f, 1f, ease);
        }

        /// <summary>The page covers the whole canvas; the board fits inside the safe area at its own aspect.</summary>
        private void FitToScreen(bool force)
        {
            if (safeRoot == null) return;
            Vector2 a0 = safeRoot.anchorMin, a1 = safeRoot.anchorMax;
            if (force || a0 != lastSafeMin || a1 != lastSafeMax)
            {
                lastSafeMin = a0; lastSafeMax = a1;
                Vector2 span = new Vector2(Mathf.Max(.0001f, a1.x - a0.x), Mathf.Max(.0001f, a1.y - a0.y));
                page.anchorMin = new Vector2(-a0.x / span.x, -a0.y / span.y);
                page.anchorMax = new Vector2((1f - a0.x) / span.x, (1f - a0.y) / span.y);
                page.offsetMin = page.offsetMax = Vector2.zero;
                safeBox.anchorMin = a0; safeBox.anchorMax = a1;
                safeBox.offsetMin = safeBox.offsetMax = Vector2.zero;
            }
            Vector2 size = safeBox.rect.size;
            if (!force && size == lastBoxSize) return;
            lastBoxSize = size;
            if (size.x <= 0f || size.y <= 0f) return;
            fitScale = Mathf.Min(size.x / BoardWidth, size.y / BoardHeight);
            ApplyTransitionScaleOnly();
        }

        private void ApplyTransitionScaleOnly()
        {
            if (board == null || group == null) return;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(transition), 3f);
            board.localScale = Vector3.one * fitScale * Mathf.Lerp(1.04f, 1f, ease);
        }

        // ------------------------------------------------------------------ helpers

        private Material CoverMaterial(string mask, float captionFraction)
        {
            Shader shader = Resources.Load<Shader>("Shaders/UIMaskedCover1101");
            var material = new Material(shader != null ? shader : Shader.Find("UI/Default")) { hideFlags = HideFlags.DontSave };
            material.SetTexture("_MaskTex", LoadTexture(Art + mask));
            material.SetFloat("_CapHeight", captionFraction);
            materials.Add(material);
            return material;
        }

        private RawImage NewCover(string name, Transform parent, Rect window, Material material)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Place(rect, window.x, window.y, window.width, window.height);
            var image = go.GetComponent<RawImage>();
            image.material = material;
            image.raycastTarget = false;
            return image;
        }

        private static void SetCover(RawImage image, Material material, string path)
        {
            Texture2D texture = LoadTexture(string.IsNullOrEmpty(path) ? Art + "cover-catalena" : path) ?? LoadTexture(Art + "cover-catalena");
            if (image.texture == texture) return;
            image.texture = texture;
            if (texture == null) return;
            Vector2 size = image.rectTransform.rect.size;
            float windowAspect = size.x / Mathf.Max(1f, size.y), textureAspect = texture.width / (float)Mathf.Max(1, texture.height);
            float sx = 1f, sy = 1f;
            if (windowAspect > textureAspect) sy = textureAspect / windowAspect; else sx = windowAspect / textureAspect;
            material.SetVector("_CoverST", new Vector4(sx, sy, (1f - sx) * .5f, (1f - sy) * .5f));
        }

        private static Texture2D LoadTexture(string path)
        {
            if (!textures.TryGetValue(path, out Texture2D texture) || texture == null)
                textures[path] = texture = Resources.Load<Texture2D>(path);
            return texture;
        }

        private Image[] NewEq(string name, Transform parent, float x, float y, int count, float barWidth, float height)
        {
            RectTransform root = NewRect(name, parent);
            Place(root, x, y, count * barWidth + (count - 1) * 3f, height);
            var bars = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Bar" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = root.gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(root, false);
                float left = i * (barWidth + 3f) / root.sizeDelta.x, right = (i * (barWidth + 3f) + barWidth) / root.sizeDelta.x;
                rect.anchorMin = new Vector2(left, 0f);
                rect.anchorMax = new Vector2(right, .6f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                Image image = go.GetComponent<Image>();
                image.color = Teal;
                image.raycastTarget = false;
                go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, .45f);
                bars[i] = image;
            }
            return bars;
        }

        private RectTransform NewChip(string name, Transform parent, string label, out MusicQuad1101 quad, out Text text, Action onTap)
        {
            Button button = NewButton(name, parent, 0, 0, 80, 38, onTap);
            RectTransform rect = (RectTransform)button.transform;
            quad = NewQuad("Plate", rect, ChipOff);
            quad.SetInsets(.06f, 0f, .06f, 0f);
            Stretch(quad.rectTransform);
            quad.transform.SetAsFirstSibling();
            text = NewText("Label", rect, label, 20, ChipOffText, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            ResizeChip(rect, text);
            return rect;
        }

        private static void ResizeChip(RectTransform chip, Text label)
        {
            chip.sizeDelta = new Vector2(Mathf.Ceil(label.preferredWidth) + 32f, 38f);
        }

        private static void StyleChip(MusicQuad1101 quad, Text label, Color plate, Color text)
        {
            quad.color = plate;
            label.color = text;
        }

        private MusicQuad1101 NewQuad(string name, Transform parent, Color color)
        {
            MusicQuad1101 quad = NewGraphic<MusicQuad1101>(name, parent);
            quad.color = color;
            quad.raycastTarget = false;
            return quad;
        }

        private static T NewGraphic<T>(string name, Transform parent) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        private Button NewButton(string name, Transform parent, float x, float y, float w, float h, Action onTap)
        {
            Image image = NewImage(name, parent, null, x, y, w, h);
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onTap());
            return button;
        }

        private static void SetButtonArt(Button button, string sprite)
        {
            Image image = button.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(Art + sprite);
            image.preserveAspect = true;
            image.color = Color.white;
        }

        private Image NewImage(string name, Transform parent, Sprite sprite, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Place(rect, x, y, w, h);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private Text NewText(string name, Transform parent, string value, int size, Color color, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        /// <summary>A board label at CSS-style (left, top); rotation turns about the label's own centre.</summary>
        private Text NewLabel(string name, Transform parent, string value, int size, Color color, FontStyle style,
            float x, float y, float degrees, bool shadow)
        {
            Text text = NewText(name, parent, value, size, color, style, TextAnchor.MiddleLeft);
            float w = Mathf.Ceil(text.preferredWidth) + 6f, h = Mathf.Ceil(size * 1.45f);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x + w * .5f, -(y + h * .5f));
            rect.localEulerAngles = new Vector3(0, 0, -degrees);
            if (shadow) AddShadow(text);
            return text;
        }

        private static void AddShadow(Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .55f);
            shadow.effectDistance = new Vector2(0, -2);
        }

        /// <summary>Single-line text cut with an ellipsis to fit (runs on refresh, not per frame).</summary>
        private static void FitText(Text text, string value, float width)
        {
            text.text = value;
            if (width <= 0f || text.preferredWidth <= width) return;
            for (int n = value.Length - 1; n > 0; n--)
            {
                text.text = value.Substring(0, n).TrimEnd() + "…";
                if (text.preferredWidth <= width) return;
            }
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static void CenterPivot(RectTransform rect)
        {
            Vector2 size = rect.sizeDelta;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition += new Vector2(size.x * .5f, -size.y * .5f);
        }

        /// <summary>Soft round shadow (procedural falloff only; no artwork is redrawn).</summary>
        private static Sprite SoftDot()
        {
            if (softDot != null) return softDot;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n * .5f, n * .5f)) / (n * .5f);
                    float a = Mathf.Clamp01((1f - d) / .28f);
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return softDot = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
        }
    }

    /// <summary>Drag / tap on the progress bar; the jump is committed when the finger lifts.</summary>
    public sealed class MusicSeekBar1101 : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<float> Committed;
        public bool Dragging { get; private set; }
        public float Value { get; private set; }

        public void OnPointerDown(PointerEventData eventData) { Dragging = true; Track(eventData); }
        public void OnDrag(PointerEventData eventData) => Track(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!Dragging) return;
            Track(eventData);
            Dragging = false;
            Committed?.Invoke(Value);
        }

        private void OnDisable() => Dragging = false;

        private void Track(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
            Value = Mathf.Clamp01((local.x - rect.rect.xMin) / Mathf.Max(1f, rect.rect.width));
        }

        /// <summary>Same as a tap at <paramref name="normalized"/> (used by tests).</summary>
        public void Commit(float normalized)
        {
            Value = Mathf.Clamp01(normalized);
            Dragging = false;
            Committed?.Invoke(Value);
        }
    }
}
