using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ChoSiren.UI;

namespace ChoSiren.Panels
{
    /// <summary>Handles returned by <see cref="SecondaryStory038Chrome.GlassAction"/>.</summary>
    internal sealed class SecondaryStory038Action
    {
        /// <summary>Named outer node: fixed hit rect + slashed glass socket image.</summary>
        public GameObject Root;
        public Button Button;
        /// <summary>Authored visual group that eases/compresses under pointer feedback.</summary>
        public RectTransform Body;
        public CanvasGroup BodyGroup;
        /// <summary>Slashed socket plate on Root (kept for measured glass surfaces).</summary>
        public Image Socket;
        /// <summary>Accent frame inside Body, revealed as a rim around Face.</summary>
        public Image Edge;
        /// <summary>Dark readable face inset inside Edge.</summary>
        public Image Face;
        public Text Label;
        public CanvasGroup Sheen;
    }

    /// <summary>
    /// secondary_story 0.3.8 chrome shared by <see cref="StoryPanel"/> and
    /// <see cref="PerformanceStagePanel"/>: slashed crystal buttons whose whole authored
    /// face (accent edge + dark inset + caption + sheen) eases, lifts and compresses under
    /// <see cref="ModalPolish038PressFeedback"/> while the outer hit rect and the glass
    /// socket stay fixed — plus static slashed plates with neon corner cuts for readable
    /// glass surfaces that must keep a measured <see cref="Image"/> on the named root.
    /// </summary>
    internal static class SecondaryStory038Chrome
    {
        public static readonly Color SocketDark = new Color32(11, 8, 18, 190);
        public static readonly Color SocketChoice = new Color32(11, 8, 18, 215);
        public static readonly Color SocketUtility = new Color32(11, 8, 18, 220);
        public static readonly Color SocketStage = new Color32(11, 8, 18, 235);
        public static readonly Color EdgeViolet = new Color32(166, 112, 255, 150);
        public static readonly Color EdgePink = new Color32(255, 126, 226, 200);
        public static readonly Color EdgeCyan = new Color32(91, 215, 255, 170);
        public static readonly Color EdgeStage = new Color32(255, 91, 209, 235);
        public static readonly Color FaceDark = new Color32(11, 8, 18, 235);
        public static readonly Color FaceStage = new Color32(11, 8, 18, 250);
        public static readonly Color FaceToggleOn = new Color32(31, 22, 43, 243);
        public static readonly Color TextWhite = new Color32(249, 247, 255, 255);
        public static readonly Color CutViolet = new Color32(152, 105, 226, 120);
        public static readonly Color CutPink = new Color32(255, 126, 226, 110);
        public static readonly Color CutCyan = new Color32(91, 215, 255, 110);
        public static readonly Color CutGold = new Color32(255, 205, 96, 110);

        /// <summary>
        /// Slashed glass button: Root keeps a translucent socket <see cref="Image"/> (so
        /// measured glass surfaces keep their authored alpha) and the fixed hit rect; a
        /// child Body carries the accent edge, dark face, optional caption and a sheen
        /// overlay, all moved coherently by <see cref="ModalPolish038PressFeedback"/>.
        /// </summary>
        public static SecondaryStory038Action GlassAction(Transform parent, string name,
            string label, int fontSize, Color socket, Color edge, UnityAction action,
            bool mirror = false, bool playClick = true, float sheenStrength = .34f)
        {
            GameObject root = Node(name, parent);
            Image socketImage = root.AddComponent<Image>();
            socketImage.sprite = ReferencePunkFx.SlashPanel(mirror);
            socketImage.type = Image.Type.Sliced;
            socketImage.color = socket;
            socketImage.raycastTarget = true;
            Button button = root.AddComponent<Button>();
            button.targetGraphic = socketImage;
            button.transition = Selectable.Transition.None;

            RectTransform body = Node("Body", root.transform).GetComponent<RectTransform>();
            Stretch(body);
            body.pivot = Vector2.one * .5f;
            CanvasGroup bodyGroup = body.gameObject.AddComponent<CanvasGroup>();

            Image edgeImage = NewImage("Edge", body, ReferencePunkFx.SlashPanel(mirror), edge);
            edgeImage.type = Image.Type.Sliced;
            Stretch(edgeImage.rectTransform);

            Image faceImage = NewImage("Face", edgeImage.transform,
                ReferencePunkFx.SlashPanel(mirror), FaceDark);
            faceImage.type = Image.Type.Sliced;
            Stretch(faceImage.rectTransform, 2.5f, 2.5f, -2.5f, -2.5f);

            Text caption = null;
            if (!string.IsNullOrEmpty(label))
            {
                caption = ModalPolish038Chrome.Text(body, "Label", label, fontSize, TextWhite,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
                Stretch(caption.rectTransform, 8f, 4f, -8f, -4f);
                ModalPolish038Chrome.EnableBestFit(caption, Mathf.Max(10, fontSize - 5));
            }

            CanvasGroup sheen = Sheen(body, mirror);

            button.onClick.AddListener(() =>
            {
                if (playClick) ModalPolish038Chrome.PlayClick();
                action?.Invoke();
            });
            root.AddComponent<ModalPolish038PressFeedback>().Configure(body, sheen, sheenStrength);
            if (name != "DialogueBox") SirenStickerUi.SkinButton(root, name.StartsWith("Choice-"), true);

            return new SecondaryStory038Action
            {
                Root = root,
                Button = button,
                Body = body,
                BodyGroup = bodyGroup,
                Socket = socketImage,
                Edge = edgeImage,
                Face = faceImage,
                Label = caption,
                Sheen = sheen,
            };
        }

        /// <summary>Recolours a GlassAction face for persistent states (e.g. 自动：开).</summary>
        public static void SetActionFace(SecondaryStory038Action action, Color edge,
            Color face, string label = null)
        {
            if (action == null) return;
            if (action.Edge != null) action.Edge.color = edge;
            if (action.Face != null) action.Face.color = face;
            if (label != null && action.Label != null) action.Label.text = label;
        }

        /// <summary>
        /// Static slashed glass plate: the measured <see cref="Image"/> stays on the named
        /// node and neon corner cuts give the jagged 0.3.8 frame. No pointer feedback.
        /// When width/height are given the plate is placed before the cuts are measured.
        /// </summary>
        public static Image Plate(Transform parent, string name, Color color, bool mirror,
            float cornerCut, Color cutColor, float x = 0f, float y = 0f,
            float width = 0f, float height = 0f)
        {
            Image plate = NewImage(name, parent, ReferencePunkFx.SlashPanel(mirror), color);
            plate.type = Image.Type.Sliced;
            if (width > 0f && height > 0f) PlaceTop(plate.rectTransform, x, y, width, height);
            SirenStickerUi.SkinPanel(plate.gameObject, cornerCut > 0f);
            plate.color = new Color(1f, 1f, 1f, color.a);
            return plate;
        }

        /// <summary>White slash sheen overlay driven by ModalPolish038PressFeedback.</summary>
        public static CanvasGroup Sheen(Transform body, bool mirror)
        {
            GameObject sheenObject = Node("Sheen", body);
            Stretch(sheenObject.GetComponent<RectTransform>());
            Image sheenImage = sheenObject.AddComponent<Image>();
            sheenImage.sprite = ReferencePunkFx.SlashPanel(mirror);
            sheenImage.type = Image.Type.Sliced;
            sheenImage.color = Color.white;
            sheenImage.raycastTarget = false;
            CanvasGroup group = sheenObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        public static GameObject Node(string name, Transform parent)
        {
            GameObject node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            node.GetComponent<RectTransform>().localScale = Vector3.one;
            return node;
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject node = Node(name, parent);
            Image image = node.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f,
            float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        public static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
