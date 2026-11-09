using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-07 练习室 (赵金 练习室界面.psd): a camera view of the practice room. The four leads sing or dance in
    /// cut-out clips (StreamingAssets/Practice1107, alternating at random) over the PSD's room art, under the
    /// REC camera frame. Tapping a lead picks her as the trainee; any owned member can be picked with the ‹ › on
    /// the member card (<see cref="Panels.MemberPracticePanel"/>), and only a lead shows the ▼ marker.
    /// Coordinates below are the PSD's (2072 wide; the camera window starts at y 40, the room art at 117).
    /// </summary>
    public sealed class PracticeVideoStage1107 : MonoBehaviour
    {
        public const string ArtRoot = "Art/Ui1107/";
        private const float PsdW = 2072f, PsdTop = 40f, PsdWindowH = 1736f; // camera frame top to room bottom
        /// <summary>The leads left to right as they stand in both clips.</summary>
        public static readonly string[] LeadIds = { "wubai", "feiyin", "xingli", "yeying" };
        private static readonly Rect[] LeadBoxes =
        {
            new Rect(50, 652, 530, 649), new Rect(565, 577, 441, 702), new Rect(1021, 572, 560, 711), new Rect(1586, 577, 473, 703),
        };
        private static readonly Rect[] ShadowBoxes =
        {
            new Rect(36, 1170, 487, 219), new Rect(578, 1163, 487, 219), new Rect(1057, 1172, 487, 219), new Rect(1574, 1174, 487, 219),
        };
        // Where each clip's 1280x720 frame sits so its four figures stand on the PSD's spots (frame-0 bounds).
        private static readonly (string File, Rect Box)[] ClipBoxes =
        {
            ("Practice1107/sing.mp4", new Rect(-215, 218, 2483, 1397)),
            ("Practice1107/dance.mp4", new Rect(-76, 389, 2202, 1238)),
        };

        private GameModel model;
        private Action<int> onPick;
        private List<int> roster = new List<int>();
        private int selected = -1;
        private bool practicing, reduced;
        private RectTransform content, marker;
        // 2026-11-09 (少爷: "能打光吗？现在只有一个箭头不好"): a stage spotlight on the chosen lead — the room dims,
        // a soft beam falls on her from above and a warm pool lights the floor under her feet.
        private RawImage spotDim, spotBeam, spotPool;
        private int spotLead = -2;
        private float spotFade = 1f;
        private static readonly Dictionary<int, Texture2D[]> SpotTextures = new Dictionary<int, Texture2D[]>();
        private Image room;
        private readonly RectTransform[] shadows = new RectTransform[4];
        private readonly RectTransform[] clipRects = new RectTransform[2];
        private readonly RectTransform[] leadHits = new RectTransform[4];
        private RectTransform cameraFrame, title;
        private Text timecode;
        private StackedAlphaClips1107 clips;
        private float scale = 1f, clock;

        public int SelectedMember => selected;
        public bool Practicing => practicing;
        public IReadOnlyList<int> Roster => roster;
        public StackedAlphaClips1107 Clips => clips;

        public static PracticeVideoStage1107 Build(Transform parent, Font font, GameModel gameModel,
            IList<int> owned, int selectedMember, Action<int> picked)
        {
            // Its own canvas: the clips re-batch only the stage, never the static cards (WebGL).
            var node = new GameObject("PracticeStage", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster),
                typeof(RectMask2D), typeof(PracticeVideoStage1107));
            node.transform.SetParent(parent, false);
            var stage = node.GetComponent<PracticeVideoStage1107>();
            stage.model = gameModel;
            stage.onPick = picked;
            stage.roster = new List<int>(owned ?? Array.Empty<int>());
            stage.BuildStage(font);
            stage.SetSelected(selectedMember, true);
            return stage;
        }

        /// <summary>Owned members in team order first (the ‹ › cycle order).</summary>
        public static List<int> OwnedRoster(GameModel gameModel, int mustInclude) => SirenPracticeScene.OwnedRoster(gameModel, mustInclude);

        /// <summary>The member index of a lead (−1 when the catalog lacks her).</summary>
        public static int LeadMember(int lead)
        {
            if (lead < 0 || lead >= LeadIds.Length) return -1;
            for (int i = 0; i < GameModel.Members.Length; i++) if (GameModel.Members[i].Id == LeadIds[lead]) return i;
            return -1;
        }

        public void SetPracticing(bool active, bool reduceMotion)
        {
            practicing = active;
            reduced = reduceMotion;
            clips?.SetPaused(reduceMotion);
        }

        public bool SetSelected(int member, bool instant = false)
        {
            if (member < 0 || member >= GameModel.Members.Length) return false;
            selected = member;
            if (!roster.Contains(member)) roster.Insert(0, member);
            PlaceMarker();
            return true;
        }

        public void RefreshLabels() => PlaceMarker();

        /// <summary>The hit rectangle over a lead (tests click it).</summary>
        public RectTransform LeadRect(int lead) => lead >= 0 && lead < leadHits.Length ? leadHits[lead] : null;

        public void Relayout(float width, float height)
        {
            scale = width / PsdW;
            // The room window keeps the PSD's proportions, centred when the stage is taller or shorter.
            float windowH = PsdWindowH * scale;
            float offsetY = (height - windowH) * .5f;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = new Vector2(0f, -offsetY);
            content.sizeDelta = new Vector2(width, windowH);
            Place(room.rectTransform, new Rect(0, 117, 2101, 1659));
            for (int i = 0; i < 4; i++) Place(shadows[i], ShadowBoxes[i]);
            for (int i = 0; i < clipRects.Length; i++) Place(clipRects[i], ClipBoxes[i].Box);
            for (int i = 0; i < 4; i++) Place(leadHits[i], LeadBoxes[i]);
            foreach (RawImage spot in new[] { spotDim, spotBeam, spotPool })
                if (spot != null) Place(spot.rectTransform, new Rect(0, 117, 2072, PsdWindowH - 77));
            Place(cameraFrame, new Rect(0, 40, 2072, 1851));
            Place(title, new Rect(38, 48, 659, 109));
            Place((RectTransform)timecode.transform, new Rect(828, 1659, 400, 90)); // over the erased PSD timecode
            timecode.fontSize = Mathf.Max(10, Mathf.RoundToInt(46 * scale));
            PlaceMarker();
        }

        private void BuildStage(Font font)
        {
            var ground = NewImage("PracticeGround", transform, null, new Color32(9, 6, 22, 255));
            Stretch(ground.rectTransform);
            content = NewRect("PracticeCamera", transform);
            room = NewImage("PracticeRoom", content, Resources.Load<Sprite>(ArtRoot + "practice-room"), Color.white);
            for (int i = 0; i < 4; i++)
                shadows[i] = NewImage("LeadShadow" + i, content, Resources.Load<Sprite>(ArtRoot + "practice-shadow-" + i), Color.white).rectTransform;

            clips = StackedAlphaClips1107.Create(gameObject);
            var clipLayer = NewRect("PracticeClips", content);
            Stretch(clipLayer);
            for (int i = 0; i < ClipBoxes.Length; i++)
            {
                var go = new GameObject("Clip-" + System.IO.Path.GetFileNameWithoutExtension(ClipBoxes[i].File),
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                go.transform.SetParent(clipLayer, false);
                clipRects[i] = (RectTransform)go.transform;
                clips.Add(ClipBoxes[i].File, go.GetComponent<RawImage>());
            }

            spotPool = NewRaw("SpotPool", clipLayer);
            spotPool.transform.SetAsFirstSibling();
            spotDim = NewRaw("SpotDim", content);
            spotBeam = NewRaw("SpotBeam", content);

            marker = NewRect("LeadMarker", content);
            var markerText = NewText("Glyph", marker, font, "▼", 30, new Color32(28, 242, 236, 255));
            markerText.enabled = false; // the spotlight marks her now; the node stays for layout and tests
            Stretch(markerText.rectTransform);
            markerText.gameObject.AddComponent<Outline>().effectColor = new Color32(9, 6, 22, 220);

            for (int i = 0; i < 4; i++)
            {
                int lead = i;
                var hit = NewImage("Lead-" + LeadIds[i], content, null, new Color(1f, 1f, 1f, 0f));
                hit.raycastTarget = true;
                hit.gameObject.AddComponent<Button>().onClick.AddListener(() => PickLead(lead));
                leadHits[i] = hit.rectTransform;
            }

            var frame = NewImage("CameraFrame", content, Resources.Load<Sprite>(ArtRoot + "practice-camera"), Color.white);
            frame.raycastTarget = false;
            cameraFrame = frame.rectTransform;
            timecode = NewText("CameraTimecode", content, font, "00:00:00:00", 14, Color.white);
            timecode.fontStyle = FontStyle.Bold;
            var titleImage = NewImage("PracticeTitle", content, Resources.Load<Sprite>(ArtRoot + "practice-title"), Color.white);
            titleImage.preserveAspect = true;
            title = titleImage.rectTransform;
        }

        private void PickLead(int lead)
        {
            if (practicing) return;
            int member = LeadMember(lead);
            if (member < 0) return;
            onPick?.Invoke(member);
        }

        private void PlaceMarker()
        {
            if (marker == null) return;
            int lead = Array.IndexOf(LeadIds, selected >= 0 && selected < GameModel.Members.Length ? GameModel.Members[selected].Id : null);
            marker.gameObject.SetActive(lead >= 0);
            SetSpot(lead);
            if (lead < 0) return;
            Rect box = LeadBoxes[lead];
            Place(marker, new Rect(box.center.x - 60, box.y - 110, 120, 100));
        }

        private void SetSpot(int lead)
        {
            if (spotDim == null || lead == spotLead) return;
            spotLead = lead;
            bool on = lead >= 0;
            spotDim.enabled = spotBeam.enabled = spotPool.enabled = on;
            if (!on) return;
            Texture2D[] tex = SpotTexturesFor(lead);
            spotDim.texture = tex[0]; spotBeam.texture = tex[1]; spotPool.texture = tex[2];
            spotFade = reduced ? 1f : 0f;
            ApplySpot(Time.unscaledTime);
        }

        private void ApplySpot(float t)
        {
            if (spotDim == null || spotLead < 0) return;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(spotFade), 3f);
            float flicker = reduced ? 1f : .94f + .06f * Mathf.Sin(t * 2.1f) * Mathf.Sin(t * 3.7f + 1f);
            spotDim.color = new Color(1f, 1f, 1f, k);
            spotBeam.color = new Color(1f, 1f, 1f, k * flicker);
            spotPool.color = new Color(1f, 1f, 1f, k * flicker);
        }

        /// <summary>Dim (dark everywhere but her cone), beam (soft light cone) and floor pool textures for a lead.</summary>
        private static Texture2D[] SpotTexturesFor(int lead)
        {
            if (SpotTextures.TryGetValue(lead, out Texture2D[] cached) && cached[0] != null) return cached;
            const int W = 256, H = 202; // the room window (2072 × 1659) at low resolution; bilinear keeps it soft
            float top = 117f, span = PsdWindowH - 77f;
            Rect box = LeadBoxes[lead], foot = ShadowBoxes[lead];
            float cx = box.center.x / PsdW;
            float feetY = (foot.center.y - top) / span;
            float halfTop = 40f / PsdW, halfFeet = (box.width * .62f) / PsdW;
            var dim = new Color32[W * H]; var beam = new Color32[W * H]; var pool = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = 1f - (y + .5f) / H; // 0 at top
                float along = Mathf.Clamp01(v / Mathf.Max(.01f, feetY));
                float half = Mathf.Lerp(halfTop, halfFeet, along);
                for (int x = 0; x < W; x++)
                {
                    float u = (x + .5f) / W;
                    float d = Mathf.Abs(u - cx) / half;
                    float cone = v <= feetY ? 1f - Smooth(.7f, 1.15f, d) : 0f;
                    // floor pool: an ellipse around her feet
                    float ex = (u - cx) / (halfFeet * 1.15f), ey = (v - feetY) / .085f;
                    float poolV = 1f - Smooth(.55f, 1.05f, Mathf.Sqrt(ex * ex + ey * ey));
                    float lit = Mathf.Max(cone * (.55f + .45f * along), poolV);
                    dim[y * W + x] = new Color32(6, 4, 18, (byte)(Mathf.Clamp01(.5f * (1f - lit)) * 255));
                    float beamA = cone * (.10f + .14f * (1f - along)) * (1f - Smooth(.35f, 1f, d) * .6f);
                    beam[y * W + x] = new Color32(255, 246, 225, (byte)(Mathf.Clamp01(beamA) * 255));
                    pool[y * W + x] = new Color32(255, 236, 200, (byte)(Mathf.Clamp01(poolV * .42f) * 255));
                }
            }
            Texture2D Make(Color32[] px, string name)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                t.SetPixels32(px); t.Apply(false, true); return t;
            }
            var set = new[] { Make(dim, "SpotDim" + lead), Make(beam, "SpotBeam" + lead), Make(pool, "SpotPool" + lead) };
            SpotTextures[lead] = set;
            return set;
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static RawImage NewRaw(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false; raw.enabled = false;
            return raw;
        }

        private void Update()
        {
            if (spotLead >= 0)
            {
                if (spotFade < 1f) spotFade = Mathf.Min(1f, spotFade + Time.unscaledDeltaTime / .35f);
                ApplySpot(Time.unscaledTime);
            }
            if (marker != null && marker.gameObject.activeSelf && !reduced)
            {
                var glyph = marker.GetChild(0) as RectTransform;
                if (glyph != null) glyph.anchoredPosition = new Vector2(0f, Mathf.Sin(Time.unscaledTime * 4f) * 4f);
            }
            if (timecode == null) return;
            // The REC timecode runs like a camera (hh:mm:ss:frames at 25 fps).
            if (!reduced) clock += Time.unscaledDeltaTime;
            int frames = (int)(clock * 25f);
            timecode.text = $"{frames / 90000 % 100:00}:{frames / 1500 % 60:00}:{frames / 25 % 60:00}:{frames % 25:00}";
        }

        private void Place(RectTransform rect, Rect psd)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(psd.x * scale, -(psd.y - PsdTop) * scale);
            rect.sizeDelta = new Vector2(psd.width * scale, psd.height * scale);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, Font font, string value, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
