using System;
using System.Collections;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        private GameObject workshopMusicPanel1105;
        private readonly WorkshopMusic1105.Options workshopMusicOptions1105 = new WorkshopMusic1105.Options();
        private WorkshopMusic1105.Track[] workshopMusicTracks1105;
        private bool workshopMusicBusy1105, workshopMusicConfigured1105, workshopMusicEnabled1105, workshopMusicConfigLoading1105;
        private float workshopMusicPrice1105;
        private string workshopMusicCurrency1105 = "USD", workshopMusicStatus1105 = "先听旋律，再继续创作";
        private Text workshopMusicStatusText1105;
        private AudioSource workshopMusicSource1105;
        private AudioClip workshopMusicClip1105;
        private Coroutine workshopMusicLoad1105;

        private void OpenWorkshopMusic1105()
        {
            CloseWorkshopMusic1105();
            if (workshopRoot1104 == null) return;
            var root = (RectTransform)workshopRoot1104.transform;
            var panel = new GameObject("WorkshopMusicPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(root, false);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0f); panel.pivot = new Vector2(.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 118f); panel.sizeDelta = new Vector2(720f, 430f);
            panel.GetComponent<Image>().color = new Color(.035f, .035f, .065f, .98f);
            panel.GetComponent<Image>().raycastTarget = true;
            workshopMusicPanel1105 = panel.gameObject;
            if (workshopStage1104 != null) workshopStage1104.BottomBand = 570f;
            MusicContextScope.Attach(panel, GameAudio.MusicContext.Silent);
            Image edge = WorkshopPoster1105.Block(panel, "Edge", null, Brand1025.Teal);
            WorkshopPoster1105.Corner(edge.rectTransform, new Vector2(0f, 1f), 0, 0, 720, 3);
            Text title = WorkshopPoster1105.Word(panel, "Title", "作曲", 28, WorkshopPoster1105.Paper, TextAnchor.MiddleLeft);
            WorkshopPoster1105.Corner(title.rectTransform, new Vector2(0f, 1f), 24, -7, 110, 42);
            Text modelLabel = WorkshopPoster1105.Word(panel, "Model", "Suno6", 16, Brand1025.Teal, TextAnchor.MiddleLeft);
            WorkshopPoster1105.Corner(modelLabel.rectTransform, new Vector2(0f, 1f), 132, -13, 120, 32);
            MusicButton1105(panel, "Collapse", "收起", 590, -8, 106, 40, false, CollapseWorkshopEditor1105, true);

            var viewport = new GameObject("Parameters", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
            viewport.SetParent(panel, false); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(18, 106); viewport.offsetMax = new Vector2(-18, -56);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, .001f);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f); content.anchoredPosition = Vector2.zero;
            var scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f; scroll.decelerationRate = .12f;
            float y = 0;
            if (workshopMusicTracks1105 == null) workshopMusicTracks1105 = WorkshopMusic1105.LastTracks();
            if (workshopMusicTracks1105 != null)
            {
                for (int t = 0; t < workshopMusicTracks1105.Length; t++)
                {
                    WorkshopMusic1105.Track track = workshopMusicTracks1105[t];
                    MusicLabel1105(content, "Track" + t, track.title, 22, 0, y, 350, 42);
                    MusicButton1105(content, "Play" + t, "试听", 376, y, 118, 44, false, () => PreviewWorkshopMusic1105(track));
                    MusicButton1105(content, "Save" + t, "存本机", 510, y, 148, 44, false, () => SaveWorkshopMusic1105(track));
                    y -= 56;
                }
            }
            MusicInput1105(content, "SongTitle", workshopMusicOptions1105.title, "给这首歌取个名字", y, 42, 40,
                s => workshopMusicOptions1105.title = s); y -= 54;
            MusicButton1105(content, "Instrumental", "纯音乐", 0, y, 196, 44, workshopMusicOptions1105.instrumental,
                () => { workshopMusicOptions1105.instrumental = true; OpenWorkshopMusic1105(); });
            MusicButton1105(content, "JapaneseVoice", "日语女声", 212, y, 196, 44, !workshopMusicOptions1105.instrumental,
                () => { workshopMusicOptions1105.instrumental = false; OpenWorkshopMusic1105(); });
            MusicLabel1105(content, "DurationCaption", "时长", 18, 430, y, 62, 44);
            foreach (int seconds in new[] { 40, 80 })
            {
                int d = seconds;
                MusicButton1105(content, "Duration" + d, d + "秒", d == 40 ? 492 : 582, y, 78, 44,
                    workshopMusicOptions1105.duration == d, () => { workshopMusicOptions1105.duration = d; OpenWorkshopMusic1105(); });
            }
            y -= 56;
            for (int i = 0; i < WorkshopMusic1105.Genres.Length; i++)
            {
                string genre = WorkshopMusic1105.Genres[i];
                MusicButton1105(content, "Genre" + i, genre, i * 168, y, 154, 44, workshopMusicOptions1105.genre == genre,
                    () => { workshopMusicOptions1105.genre = genre; OpenWorkshopMusic1105(); });
            }
            y -= 56;
            for (int i = 0; i < WorkshopMusic1105.Moods.Length; i++)
            {
                string mood = WorkshopMusic1105.Moods[i];
                MusicButton1105(content, "Mood" + i, mood, i * 112, y, 102, 44, workshopMusicOptions1105.mood == mood,
                    () => { workshopMusicOptions1105.mood = mood; OpenWorkshopMusic1105(); });
            }
            for (int i = 0; i < WorkshopMusic1105.Tempos.Length; i++)
            {
                int tempo = WorkshopMusic1105.Tempos[i];
                string tempoName = i == 0 ? "舒缓" : i == 1 ? "中速" : "动感";
                MusicButton1105(content, "Tempo" + i, tempoName, 350 + i * 105, y, 96, 44,
                    workshopMusicOptions1105.tempo == tempo, () => { workshopMusicOptions1105.tempo = tempo; OpenWorkshopMusic1105(); });
            }
            y -= 56;
            if (!workshopMusicOptions1105.instrumental)
            {
                MusicInput1105(content, "Lyrics", workshopMusicOptions1105.lyrics,
                    "歌词（选填，留空使用原创日语短句）", y, 100, 1600, s => workshopMusicOptions1105.lyrics = s);
                y -= 112;
            }
            MusicLabel1105(content, "Note", "目标时长可能略有差异 · 女声为偏好，不克隆角色声线", 16, 0, y, 660, 30); y -= 34;
            content.sizeDelta = new Vector2(0, -y + 8);

            workshopMusicStatusText1105 = WorkshopPoster1105.Word(panel, "Status", workshopMusicStatus1105, 18, VisualA1101.Muted, TextAnchor.MiddleLeft);
            WorkshopPoster1105.Corner(workshopMusicStatusText1105.rectTransform, Vector2.zero, 24, 74, 670, 29);
            string action = workshopMusicBusy1105 ? "作曲中…" : WorkshopMusic1105.HasPending ? "继续查询" :
                !workshopMusicEnabled1105 ? "连接作曲服务" : "生成" + workshopMusicOptions1105.duration + "秒试听";
            string price = workshopMusicPrice1105 > 0 ? "约" + workshopMusicCurrency1105 + " " + workshopMusicPrice1105.ToString("0.00") + "/次" : null;
            Button generate = WorkshopPoster1105.Ticket(panel, "GenerateSong", action, WorkshopMusic1105.HasPending ? null : price, 24, StartWorkshopMusic1105);
            WorkshopPoster1105.Corner((RectTransform)generate.transform, Vector2.zero, 24, 14, 486, 54);
            generate.interactable = !workshopMusicBusy1105 && !workshopMusicConfigLoading1105;
            MusicButton1105(panel, "Stop", "停止试听", 528, 14, 166, 54, false, StopWorkshopMusic1105, false);
            if (!workshopMusicConfigured1105)
            {
                workshopMusicConfigured1105 = true;
                workshopMusicConfigLoading1105 = true;
                generate.interactable = false;
                StartCoroutine(WorkshopMusic1105.Config((reply, error) =>
                {
                    workshopMusicConfigLoading1105 = false;
                    workshopMusicEnabled1105 = error == null && reply != null && reply.enabled;
                    if (reply != null) { workshopMusicPrice1105 = reply.price; workshopMusicCurrency1105 = reply.currency ?? "USD"; }
                    if (error != null) workshopMusicStatus1105 = error;
                    if (workshopMusicPanel1105 != null) OpenWorkshopMusic1105();
                }));
            }
        }

        private void CloseWorkshopMusic1105()
        {
            if (workshopMusicLoad1105 != null) StopCoroutine(workshopMusicLoad1105);
            workshopMusicLoad1105 = null;
            StopWorkshopMusic1105();
            if (workshopMusicPanel1105 != null) Destroy(workshopMusicPanel1105);
            workshopMusicPanel1105 = null; workshopMusicStatusText1105 = null; workshopMusicSource1105 = null;
        }

        private void StartWorkshopMusic1105()
        {
            if (workshopMusicBusy1105) return;
            if (!WorkshopMusic1105.HasPending && !workshopMusicEnabled1105)
            { workshopMusicConfigured1105 = false; OpenWorkshopMusic1105(); return; }
            workshopMusicBusy1105 = true;
            bool resume = WorkshopMusic1105.HasPending;
            string member = WorkshopSubjectId1104;
            Action<string> progress = s => { workshopMusicStatus1105 = s; if (workshopMusicStatusText1105 != null) workshopMusicStatusText1105.text = s; };
            Action<WorkshopMusic1105.Track[], string> done = (tracks, error) =>
            {
                workshopMusicBusy1105 = false;
                if (tracks != null) workshopMusicTracks1105 = tracks;
                workshopMusicStatus1105 = error ?? "歌曲已完成，点试听后可存到本机";
                if (workshopMusicPanel1105 != null) OpenWorkshopMusic1105();
            };
            StartCoroutine(resume ? WorkshopMusic1105.Resume(progress, done) : WorkshopMusic1105.Generate(member, workshopMusicOptions1105, progress, done));
            OpenWorkshopMusic1105();
        }

        private void PreviewWorkshopMusic1105(WorkshopMusic1105.Track track)
        {
            if (workshopMusicLoad1105 != null) StopCoroutine(workshopMusicLoad1105);
            workshopMusicStatus1105 = "正在准备试听…";
            workshopMusicLoad1105 = StartCoroutine(WorkshopMusic1105.Load(track, (clip, error) =>
            {
                workshopMusicLoad1105 = null;
                if (error != null) { workshopMusicStatus1105 = error; if (workshopMusicStatusText1105 != null) workshopMusicStatusText1105.text = error; return; }
                StopWorkshopMusic1105(); workshopMusicClip1105 = clip;
                if (workshopMusicPanel1105 == null) { Destroy(clip); workshopMusicClip1105 = null; return; }
                if (workshopMusicSource1105 == null) workshopMusicSource1105 = workshopMusicPanel1105.AddComponent<AudioSource>();
                workshopMusicSource1105.playOnAwake = false; workshopMusicSource1105.loop = false; workshopMusicSource1105.volume = 1f;
                workshopMusicSource1105.clip = clip; workshopMusicSource1105.Play();
                workshopMusicStatus1105 = "正在试听 · " + track.title;
                if (workshopMusicStatusText1105 != null) workshopMusicStatusText1105.text = workshopMusicStatus1105;
            }));
        }

        private void StopWorkshopMusic1105()
        {
            if (workshopMusicSource1105 != null) { workshopMusicSource1105.Stop(); workshopMusicSource1105.clip = null; }
            if (workshopMusicClip1105 != null) Destroy(workshopMusicClip1105);
            workshopMusicClip1105 = null;
        }

        private void SaveWorkshopMusic1105(WorkshopMusic1105.Track track)
        {
            string error;
            Toast(WorkshopMusic1105.Save(track, out error) ? "歌曲已存到本机" : error);
        }

        private Text MusicLabel1105(Transform parent, string name, string caption, int size, float x, float y, float w, float h)
        {
            Text t = WorkshopPoster1105.Word(parent, name, caption, size, WorkshopPoster1105.Paper, TextAnchor.MiddleLeft);
            WorkshopPoster1105.Corner(t.rectTransform, new Vector2(0, 1), x, y, w, h); return t;
        }

        private Button MusicButton1105(Transform parent, string name, string caption, float x, float y, float w, float h,
            bool selected, Action click, bool topRight = false)
        {
            Button b = WorkshopPoster1105.Ticket(parent, name, caption, null, 20, click, Brand1025.Teal,
                selected ? (Color)Brand1025.Teal : WorkshopPoster1105.Ink, selected ? WorkshopPoster1105.Ink : (Color)Brand1025.Teal);
            // Content rows are positioned downwards from top; the panel's stop button uses bottom coordinates.
            Vector2 anchor = parent == workshopMusicPanel1105?.transform && name == "Stop" ? Vector2.zero : new Vector2(0, 1);
            WorkshopPoster1105.Corner((RectTransform)b.transform, anchor, x, y, w, h); return b;
        }

        private void MusicInput1105(Transform parent, string name, string text, string hint, float y, float height, int limit, Action<string> changed)
        {
            Image box = WorkshopPoster1105.Block(parent, name, null, new Color(1f, 1f, 1f, .09f)); box.raycastTarget = true;
            WorkshopPoster1105.Corner(box.rectTransform, new Vector2(0, 1), 0, y, 660, height);
            Text value = WorkshopPoster1105.Word(box.transform, "Text", "", 21, WorkshopPoster1105.Paper, TextAnchor.UpperLeft);
            WorkshopPoster1105.Fill(value.rectTransform, 14, 6, -14, -6); value.supportRichText = false;
            Text placeholder = WorkshopPoster1105.Word(box.transform, "Placeholder", hint, 18, VisualA1101.Muted, TextAnchor.UpperLeft);
            WorkshopPoster1105.Fill(placeholder.rectTransform, 14, 6, -14, -6);
            InputField input = box.gameObject.AddComponent<InputField>(); input.targetGraphic = box; input.textComponent = value; input.placeholder = placeholder;
            input.characterLimit = limit; input.lineType = height > 60 ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.caretColor = Brand1025.Teal; input.customCaretColor = true; input.caretWidth = 3; input.text = text;
            input.onValueChanged.AddListener(s => changed(s));
        }
    }
}
