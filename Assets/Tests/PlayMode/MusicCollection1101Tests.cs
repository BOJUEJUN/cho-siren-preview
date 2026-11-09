using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 「A · 唱片收藏」: play modes, favourites, 首页曲, seeking and paging on the real Home transport,
    /// plus screenshots of the lobby entry (playing / paused) and the collection's first page into
    /// Builds/qa-logs/zj-1101/music-a/ for comparison with the approved mock (music-1031/shots2/A-open.png).
    /// </summary>
    public sealed class MusicCollection1101Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameAudio> suspended = new List<GameAudio>();
        private GameObject host;
        private GameAudio audio;
        private GameModel model;
        private InMemorySaveStore store;
        private AudioSource music;

        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/zj-1101/music-a"));

        private IEnumerator MakeTransport(bool musicOn, string saveJson = null)
        {
            foreach (GameAudio current in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None))
                if (current.enabled) { suspended.Add(current); current.enabled = false; }
            store = new InMemorySaveStore();
            if (saveJson != null) store.SetString(GameModel.SaveKey, saveJson);
            model = new GameModel(() => DateTime.Now, store);
            // Fixtures open on the first song (list order); cases about shuffle switch it on themselves.
            if (saveJson == null) model.SetMusicPlayMode(HomeMusicPlaylist.PlayMode.ListLoop);
            if (model.Save.MusicEnabled != musicOn) model.ToggleMusic();
            host = new GameObject("MusicCollection1101Fixture", typeof(RectTransform));
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            audio = host.AddComponent<GameAudio>();
            audio.Initialize(model);
            music = (AudioSource)typeof(GameAudio).GetField("musicSource", Private).GetValue(audio);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var page in Object.FindObjectsByType<MusicCollectionPage1101>(FindObjectsSortMode.None)) Object.DestroyImmediate(page.gameObject);
            if (host != null) Object.DestroyImmediate(host);
            HomeMusicPlaylist.SetTracksForTesting(null);
            yield return null;
            foreach (GameAudio current in suspended) if (current != null) current.enabled = true;
            suspended.Clear();
        }

        [Test]
        public void OldSaveWithoutMusicFieldsLoadsDefaultsAndChoicesPersist()
        {
            var old = new InMemorySaveStore();
            // A save written before 2026-11-01 has none of the music fields.
            old.SetString(GameModel.SaveKey, "{\"SchemaVersion\":5,\"Diamonds\":10,\"MusicEnabled\":true}");
            var first = new GameModel(() => DateTime.Now, old);
            Assert.That(first.Save.Diamonds, Is.EqualTo(10));
            Assert.That(first.MusicPlayMode, Is.EqualTo(HomeMusicPlaylist.PlayMode.Shuffle), "2026-11-08: shuffle is the default");
            Assert.That(first.IsMusicFavorite("my-light-my-rules"), Is.False);
            Assert.That(HomeMusicPlaylist.StartIndex(first), Is.EqualTo(0));

            Assert.That(first.ToggleMusicFavorite("chase-the-glow-a"), Is.True);
            first.SetHomeMusicId("chase-the-glow-a");
            first.SetMusicPlayMode(HomeMusicPlaylist.PlayMode.Shuffle);
            Assert.That(first.MarkMusicHeard("chase-the-glow-a"), Is.True);
            Assert.That(first.MarkMusicHeard("chase-the-glow-a"), Is.False);

            var reloaded = new GameModel(() => DateTime.Now, old);
            Assert.That(reloaded.IsMusicFavorite("chase-the-glow-a"), Is.True);
            Assert.That(reloaded.Save.HomeMusicId, Is.EqualTo("chase-the-glow-a"));
            Assert.That(reloaded.MusicPlayMode, Is.EqualTo(HomeMusicPlaylist.PlayMode.Shuffle));
            Assert.That(reloaded.IsMusicHeard("chase-the-glow-a"), Is.True);
            Assert.That(HomeMusicPlaylist.StartIndex(reloaded), Is.EqualTo(1));
            Assert.That(reloaded.ToggleMusicFavorite("chase-the-glow-a"), Is.False);
            Assert.That(new GameModel(() => DateTime.Now, old).IsMusicFavorite("chase-the-glow-a"), Is.False);
            // An id that is not in the list (renamed / removed song) falls back to the first song.
            reloaded.SetHomeMusicId("no-such-song");
            Assert.That(HomeMusicPlaylist.StartIndex(reloaded), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TransportStartsOnTheSavedHomeSongWithoutDownloadingWhileMuted()
        {
            yield return MakeTransport(false, JsonUtility.ToJson(new GameSave { HomeMusicId = "chase-the-glow-a", MusicEnabled = false,
                MusicRandomOpening1108 = true }));
            Assert.That(audio.HomeMusicTrackIndex, Is.EqualTo(HomeMusicPlaylist.IndexOf("chase-the-glow-a")));
            Assert.That(audio.HomeMusicTitle, Is.EqualTo("輝きを追いかけて"));
            yield return null;
            Assert.That(music.isPlaying, Is.False);
            Assert.That(typeof(GameAudio).GetField("webMusicRequest", Private).GetValue(audio), Is.Null);
        }

        [Test]
        public void ShuffleOpensOnARandomSongUnlessAHomeSongWasChosen()
        {
            // 2026-11-08 (少爷: "进去还是光的规则…要随机的").
            var seen = new HashSet<int>();
            for (int i = 0; i < 80; i++)
            {
                var fresh = new GameModel(() => DateTime.Now, new InMemorySaveStore());
                seen.Add(HomeMusicPlaylist.OpeningIndex(fresh));
            }
            Assert.That(seen.Count, Is.GreaterThan(2), "a shuffled game opens on different songs");
            var old = new InMemorySaveStore();
            old.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { HomeMusicId = "my-light-my-rules" }));
            var migrated = new GameModel(() => DateTime.Now, old);
            HomeMusicPlaylist.OpeningIndex(migrated);
            Assert.That(migrated.Save.HomeMusicId, Is.Empty, "a 首页曲 saved before random openings is dropped once");
            migrated.SetHomeMusicId("chase-the-glow-a");
            Assert.That(HomeMusicPlaylist.OpeningIndex(migrated), Is.EqualTo(HomeMusicPlaylist.IndexOf("chase-the-glow-a")),
                "a 首页曲 chosen in the player is kept");
        }

        [UnityTest]
        public IEnumerator ShuffleNeverRepeatsAndLockedSongsAreSkipped()
        {
            yield return MakeTransport(false);
            model.SetMusicPlayMode(HomeMusicPlaylist.PlayMode.Shuffle);
            int previous = audio.HomeMusicTrackIndex;
            var seen = new HashSet<int> { previous };
            for (int i = 0; i < 300; i++) // 13 songs since 2026-11-08: enough draws to reach every one
            {
                Assert.That(audio.NextHomeMusic(), Is.True);
                Assert.That(audio.HomeMusicTrackIndex, Is.Not.EqualTo(previous), "Shuffle must not pick the same song twice in a row.");
                previous = audio.HomeMusicTrackIndex;
                seen.Add(previous);
            }
            Assert.That(seen.Count, Is.EqualTo(HomeMusicPlaylist.Count), "Shuffle should reach every song.");

            // A locked song (unlock rule not met) is never chosen by ◀ ▶ or shuffle.
            audio.SelectHomeMusic(0);
            var t = new[]
            {
                new HomeMusicPlaylist.Track("a", "A", "", "", "home-f19d89ea8124a31b.mp3", "", null, false, "", "", 100),
                new HomeMusicPlaylist.Track("b", "B", "", "", "home-chase-5d360e8e7acbc7c6.mp3", "", null, false, "stage:stage-9-9", "通关 9-9", 100),
                new HomeMusicPlaylist.Track("c", "C", "", "", "boss-707b8a25a7adf39d.mp3", "", null, false, "", "", 100),
            };
            HomeMusicPlaylist.SetTracksForTesting(t);
            Assert.That(HomeMusicPlaylist.IsUnlocked(1, model), Is.False);
            Assert.That(HomeMusicPlaylist.NextIndex(0, model), Is.EqualTo(2));
            Assert.That(HomeMusicPlaylist.PreviousIndex(2, model), Is.EqualTo(0));
            for (int i = 0; i < 20; i++) Assert.That(HomeMusicPlaylist.ShuffleIndex(0, model), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SingleLoopRestartsTheSameSongAndListLoopAdvances()
        {
            yield return MakeTransport(true);
            yield return Audible(0);
            model.SetMusicPlayMode(HomeMusicPlaylist.PlayMode.SingleLoop);
            bool restarted = false;
            audio.HomeMusicRestarted += () => restarted = true;
            music.time = music.clip.length - .16f;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!restarted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(restarted, Is.True, "Single loop must start the song over at its end.");
            yield return Audible(0);
            Assert.That(music.time, Is.LessThan(2f));

            model.SetMusicPlayMode(HomeMusicPlaylist.PlayMode.ListLoop);
            music.time = music.clip.length - .16f;
            deadline = Time.realtimeSinceStartup + 3f;
            while (audio.HomeMusicTrackIndex == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(audio.HomeMusicTrackIndex, Is.EqualTo(1), "List loop advances to the next song.");
        }

        [UnityTest]
        public IEnumerator SeekMovesThePlayheadAndPausedOrUnloadedSongsKeepItAsABookmark()
        {
            yield return MakeTransport(true);
            yield return Audible(0);
            Assert.That(audio.SeekHomeMusic(.5f), Is.True);
            float expected = .5f * (music.clip.length - .4f);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(music.time, Is.InRange(expected - .1f, expected + 1f));
            Assert.That(audio.TryGetHomeMusicProgress(out float time, out float length), Is.True);
            Assert.That(time / length, Is.InRange(.45f, .56f));

            audio.ToggleHomeMusicPaused();
            yield return null;
            Assert.That(audio.SeekHomeMusic(.25f), Is.False, "A paused song moves its bookmark, not a live playhead.");
            audio.TryGetHomeMusicProgress(out time, out length);
            Assert.That(time, Is.EqualTo(.25f * (length - .4f)).Within(.05f));
            Assert.That(music.isPlaying, Is.False, "Seeking must not resume a paused record.");
            audio.ToggleHomeMusicPaused();
            yield return Audible(0);
            Assert.That(music.time, Is.InRange(.25f * (length - .4f) - .1f, .25f * (length - .4f) + 1.5f));

            // A song whose audio is not decoded yet (muted, never downloaded) degrades to a bookmark, no error.
            audio.SetMusicEnabled(false);
            audio.SelectHomeMusic(1);
            yield return null;
            Assert.DoesNotThrow(() => audio.SeekHomeMusic(.4f));
            audio.TryGetHomeMusicProgress(out time, out length);
            Assert.That(length, Is.EqualTo(HomeMusicPlaylist.Get(1).Duration).Within(.01f));
            Assert.That(time, Is.EqualTo(.4f * (length - .4f)).Within(.05f));
        }

        [UnityTest]
        public IEnumerator CollectionFiltersPagesFavoritesAndPlaysTappedRecords()
        {
            yield return MakeTransport(false);
            var tracks = new List<HomeMusicPlaylist.Track>();
            for (int i = 0; i < 15; i++)
                tracks.Add(new HomeMusicPlaylist.Track("song-" + i, "歌 " + i, "莉莉丝 LILITH", "Art/Music1101/cover-lilith",
                    i % 2 == 0 ? "home-f19d89ea8124a31b.mp3" : "home-chase-5d360e8e7acbc7c6.mp3", "",
                    i < 3 ? new[] { "battle" } : new[] { "char" }, i == 4, "", "", 120 + i));
            HomeMusicPlaylist.SetTracksForTesting(tracks.ToArray());
            audio.SelectHomeMusic(0);

            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(host.transform, false);
            var safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(canvas.transform, false);
            var safeRect = (RectTransform)safe.transform;
            safeRect.anchorMin = Vector2.zero; safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
            var toasts = new List<string>();
            var owner = new GameObject("Owner");
            owner.transform.SetParent(host.transform, false);
            MusicCollectionPage1101 page = MusicCollectionPage1101.Open(safeRect, audio, model,
                Resources.Load<Font>("Fonts/NotoSansSC-Subset"), toasts.Add, null, owner);
            yield return null;

            // 2026-11-08 player redesign: five song rows per page.
            Assert.That(page.PageCount, Is.EqualTo(3));
            Assert.That(page.VisibleTracks.Count, Is.EqualTo(15));
            page.TurnPage(1);
            Assert.That(page.PageIndex, Is.EqualTo(1));
            Assert.That(CellTrack(page, 0), Is.EqualTo(5));
            Assert.That(CellTrack(page, 4), Is.EqualTo(9));
            page.TurnPage(1); page.TurnPage(1);
            Assert.That(page.PageIndex, Is.EqualTo(0), "Paging wraps.");
            page.TurnPage(-1);
            Assert.That(page.PageIndex, Is.EqualTo(2));

            page.SelectCategory(1); // 收藏
            Assert.That(page.PageIndex, Is.EqualTo(0));
            Assert.That(page.VisibleTracks.Count, Is.EqualTo(0));
            page.ToggleFavorite();
            Assert.That(model.IsMusicFavorite("song-0"), Is.True);
            Assert.That(page.VisibleTracks, Is.EquivalentTo(new[] { 0 }));
            Assert.That(toasts[toasts.Count - 1], Is.EqualTo("已收藏"));
            page.SelectCategory(0);

            page.SetHomeSong();
            Assert.That(model.Save.HomeMusicId, Is.EqualTo("song-0"));
            HomeMusicPlaylist.PlayMode before = model.MusicPlayMode;
            page.CycleMode();
            Assert.That(model.MusicPlayMode, Is.EqualTo((HomeMusicPlaylist.PlayMode)(((int)before + 1) % 3)));
            page.CycleMode(); page.CycleMode();
            Assert.That(model.MusicPlayMode, Is.EqualTo(before), "list loop → single loop → shuffle cycle");

            page.TurnPage(1); // rows 5-9
            page.TapCell(0);
            Assert.That(audio.HomeMusicTrackIndex, Is.EqualTo(5));
            Assert.That(audio.HomeMusicPaused, Is.False);
            page.TapCell(0);
            Assert.That(audio.HomeMusicPaused, Is.True, "Tapping the current record pauses it.");
            page.TapCell(0);
            Assert.That(audio.HomeMusicPaused, Is.False);
            Assert.That(audio.MusicEnabled, Is.False, "The collection never turns a muted preference on.");
            Assert.That(toasts[toasts.Count - 1], Is.EqualTo("音乐已在设置中关闭"));

            page.Close();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (page != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(page == null, Is.True, "Closing fades out and removes the page.");
        }

        private static int CellTrack(MusicCollectionPage1101 page, int slot)
        {
            var cells = (Array)typeof(MusicCollectionPage1101).GetField("cells", Private).GetValue(page);
            object cell = cells.GetValue(slot);
            return (int)cell.GetType().GetField("Track").GetValue(cell);
        }

        private IEnumerator Audible(int index)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((audio.HomeMusicTrackIndex != index || music.clip == null || !music.isPlaying || music.mute) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(audio.HomeMusicTrackIndex, Is.EqualTo(index));
            Assert.That(music.clip, Is.Not.Null);
            Assert.That(music.isPlaying, Is.True, "This test requires an advancing native audio backend.");
        }

        // ------------------------------------------------------------------ screenshots

        [UnityTest]
        public IEnumerator CaptureLobbyEntryAndCollectionPage()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey);
            string save = PlayerPrefs.GetString(GameModel.SaveKey);
            bool hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { HomeCharacterIndex = 0, StoryProgress = 100 }));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            var app = new GameObject("MusicCollection1101App").AddComponent<ChoSirenApp>();
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            var camera = new GameObject("Music1101Camera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(1080, 2288, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                {
                    fitter.enabled = false;
                    var safe = (RectTransform)fitter.transform;
                    safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                    safe.offsetMin = safe.offsetMax = Vector2.zero;
                }
                canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0f;
                yield return null; Canvas.ForceUpdateCanvases();
                typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private)?.Invoke(app, new object[] { "lobby" });
                yield return null;
                typeof(ChoSirenApp).GetMethod("CloseModal", Private)?.Invoke(app, null);
                yield return new WaitForSecondsRealtime(1.2f);
                GameObject home = GameObject.Find("PsdHome20260921");
                Assert.That(home, Is.Not.Null);
                home.GetComponent<PsdHomeMotion>().CancelCharacterInteraction();
                var gameAudio = (GameAudio)typeof(ChoSirenApp).GetField("gameAudio", Private).GetValue(app);
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!gameAudio.HomeMusicPlaying && Time.realtimeSinceStartup < deadline) yield return null;
                gameAudio.SeekHomeMusic(.35f);
                yield return new WaitForSecondsRealtime(1.4f);
                Transform music = home.transform.Find("MusicNext");
                Assert.That(music.Find("MusicDiscToggle"), Is.Not.Null);
                // 2026-11-08: 少爷's hearted Suno songs arrive flagged new, so the record carries the NEW sticker until heard.
                bool anyNew = Enumerable.Range(0, HomeMusicPlaylist.Count).Any(i => HomeMusicPlaylist.Get(i).IsNew);
                Assert.That(music.GetComponent<HomeMusicEntry1101>().Sticker.activeSelf, Is.EqualTo(anyNew));
                Capture(camera, rt, "lobby-playing.png", null);
                Capture(camera, rt, "lobby-playing-zoom.png", new RectInt(0, 140, 540, 340));

                // The record toggles pause; the bar opens the collection.
                music.Find("MusicDiscToggle").GetComponent<Button>().onClick.Invoke();
                Assert.That(gameAudio.HomeMusicPaused, Is.True);
                yield return new WaitForSecondsRealtime(.4f);
                Capture(camera, rt, "lobby-paused-zoom.png", new RectInt(0, 140, 540, 340));
                music.Find("MusicDiscToggle").GetComponent<Button>().onClick.Invoke();
                Assert.That(gameAudio.HomeMusicPaused, Is.False);

                music.GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.6f);
                var page = Object.FindAnyObjectByType<MusicCollectionPage1101>();
                Assert.That(page, Is.Not.Null, "Tapping the song bar opens 唱片收藏.");
                yield return new WaitForSecondsRealtime(.6f);
                Capture(camera, rt, "collection-page1.png", null);
                page.ToggleFavorite();
                page.SetHomeSong();
                page.SelectCategory(1);
                yield return new WaitForSecondsRealtime(.5f);
                Capture(camera, rt, "collection-fav.png", null);
                page.Close();
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(page == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
                Object.Destroy(app.gameObject);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                PlayerPrefs.Save();
            }
            yield return null;
        }

        private static void Capture(Camera camera, RenderTexture rt, string name, RectInt? crop)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            RectInt r = crop ?? new RectInt(0, 0, rt.width, rt.height);
            var tex = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
            // crop is given from the top-left; ReadPixels reads from the bottom-left.
            tex.ReadPixels(new Rect(r.x, rt.height - r.y - r.height, r.width, r.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Path.Combine(Out, name), tex.EncodeToPNG());
            RenderTexture.active = old; Object.DestroyImmediate(tex);
        }
    }
}
