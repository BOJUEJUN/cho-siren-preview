using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren.Tests
{
    public sealed class NewCharacterMedia1113Tests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest, Timeout(180000)]
        public IEnumerator ApprovedNewUltimateClipsActuallyPlayAndComplete()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey);
            string saved = PlayerPrefs.GetString(GameModel.SaveKey);
            bool reduced = BattlePresentationOptions.ReduceMotion;
            foreach (var app in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(app.gameObject);
            yield return null;
            var host = new GameObject("NewMedia1113", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("NewMediaCamera", typeof(Camera)).GetComponent<Camera>();
            var events = new GameObject("NewMediaEvents", typeof(EventSystem));
            var target = new RenderTexture(720, 1536, 24);
            string output = Path.GetFullPath("Builds/qa-logs/full-release-0955/media");
            Directory.CreateDirectory(output);
            try
            {
                BattlePresentationOptions.ReduceMotion = false;
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                var battle = model.StartStageBattle("stage-1-1", 1113UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .07f, .2f); camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536);
                var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                yield return new WaitForSecondsRealtime(.5f);
                panel.StopAllCoroutines();
                foreach (string field in new[] { "paused", "closing", "coveredByStory", "bossEntrancePlaying1102" })
                    typeof(TacticsBattlePanel).GetField(field, Hidden)?.SetValue(panel, false);
                var show = (BattleUltimateShow1020)typeof(TacticsBattlePanel).GetMethod("EnsureUltimateShow", Hidden).Invoke(panel, null);
                var entries = BattleUltimateVideo1019.SharedManifest.entries.Where(e =>
                    e.characterId == "huansha" || e.characterId == "huanhuoling" || e.characterId == "linyin").ToArray();
                Assert.That(entries.Length, Is.EqualTo(4));
                foreach (var entry in entries)
                {
                    show.Warm(entry.path);
                    yield return new WaitForSecondsRealtime(.8f);
                    show.SetCutIn(entry.characterId, "大招");
                    Sprite art = Resources.Load<Sprite>("Art/Members/member-" + entry.characterId);
                    Assert.That(art, Is.Not.Null, entry.characterId);
                    bool complete = false, captured = false;
                    Assert.That(show.Begin(entry.path, entry.durationSeconds, art, _ => complete = true, true), Is.True);
                    float until = Time.realtimeSinceStartup + 20f, maxTime = 0f;
                    long firstFrame = -1, lastFrame = -1;
                    while (!complete && Time.realtimeSinceStartup < until)
                    {
                        var player = (VideoPlayer)typeof(BattleUltimateShow1020).GetField("player", Hidden).GetValue(show);
                        var video = (RawImage)typeof(BattleUltimateShow1020).GetField("video", Hidden).GetValue(show);
                        if (video.enabled && player != null && player.isPrepared && player.isPlaying && player.frame > 0)
                        {
                            if (firstFrame < 0) firstFrame = player.frame;
                            lastFrame = player.frame; maxTime = Mathf.Max(maxTime, (float)player.time);
                            if (!captured && player.time >= 1.5)
                            {
                                Canvas.ForceUpdateCanvases(); camera.Render();
                                var previous = RenderTexture.active; RenderTexture.active = target;
                                var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                                shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); shot.Apply();
                                RenderTexture.active = previous;
                                File.WriteAllBytes(Path.Combine(output, entry.path + ".png"), shot.EncodeToPNG());
                                Object.Destroy(shot); captured = true;
                            }
                        }
                        yield return null;
                    }
                    Assert.That(complete, Is.True, entry.path + " completes the real ultimate show");
                    Assert.That(lastFrame, Is.GreaterThan(firstFrame + 10), entry.path + " advances beyond its first frame");
                    Assert.That(maxTime, Is.GreaterThan(entry.durationSeconds - .5f), entry.path + " plays through its ending");
                    Assert.That(captured, Is.True, entry.path + " renders the moving video");
                    File.WriteAllText(Path.Combine(output, entry.path + ".txt"), $"frames={firstFrame}..{lastFrame} time={maxTime:F3} complete={complete}");
                    show.Cancel();
                }
            }
            finally
            {
                BattlePresentationOptions.ReduceMotion = reduced;
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(events);
                target.Release(); Object.DestroyImmediate(target);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, saved); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                PlayerPrefs.Save();
            }
        }
    }
}
