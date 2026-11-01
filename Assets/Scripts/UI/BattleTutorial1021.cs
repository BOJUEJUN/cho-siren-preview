using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// First-battle guide (2026-10-21): dims the stage, frames one part of the HUD at a time and
    /// explains it on a P5 callout (red title band, black slanted plate). Tap anywhere to continue.
    /// </summary>
    public sealed class BattleTutorial1021 : MonoBehaviour, IPointerClickHandler
    {
        public sealed class Step
        {
            public string Title, Body;
            public RectTransform[] Targets;
        }

        RectTransform root, frame, callout;
        Text title, body, footer;
        Image dim;
        readonly List<Step> steps = new List<Step>();
        int index = -1;
        float clock;
        Action done;
        Font font;
        public bool IsShowing => index >= 0 && index < steps.Count;

        public static BattleTutorial1021 Show(RectTransform host, Font font, IEnumerable<Step> steps, Action onDone)
        {
            var go = new GameObject("BattleTutorial1021", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(host, false);
            go.transform.SetAsLastSibling();
            var tutorial = go.AddComponent<BattleTutorial1021>();
            tutorial.font = font;
            tutorial.done = onDone;
            foreach (Step step in steps)
                if (step != null) tutorial.steps.Add(step);
            tutorial.Build();
            tutorial.Advance();
            return tutorial;
        }

        void Build()
        {
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            dim = GetComponent<Image>();
            dim.color = new Color(.02f, .01f, .05f, .62f);
            dim.raycastTarget = true;
            frame = NewRect("Focus", transform);
            for (int i = 0; i < 4; i++)
            {
                var edge = NewImage("Edge-" + i, frame, i % 2 == 0 ? Color.white : Brand1025.Teal);
                var r = edge.rectTransform;
                bool horizontal = i < 2;
                r.anchorMin = horizontal ? new Vector2(0, i == 0 ? 1 : 0) : new Vector2(i == 2 ? 0 : 1, 0);
                r.anchorMax = horizontal ? new Vector2(1, i == 0 ? 1 : 0) : new Vector2(i == 2 ? 0 : 1, 1);
                r.sizeDelta = horizontal ? new Vector2(12, 6) : new Vector2(6, 12);
                r.anchoredPosition = Vector2.zero;
            }
            callout = NewRect("Callout", transform);
            callout.sizeDelta = new Vector2(600, 230);
            var plateGo = new GameObject("Plate", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPlate1021));
            plateGo.transform.SetParent(callout, false);
            var plate = plateGo.GetComponent<SkewedPlate1021>();
            plate.color = new Color32(12, 6, 20, 255); // linear space: <255 shows through plate.raycastTarget = false;
            Stretch(plate.rectTransform);
            var band = NewImage("TitleBand", callout, Brand1025.Teal);
            band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0, 1);
            band.rectTransform.pivot = new Vector2(0, 1);
            band.rectTransform.sizeDelta = new Vector2(300, 58);
            band.rectTransform.anchoredPosition = new Vector2(-14, 26);
            band.rectTransform.localEulerAngles = new Vector3(0, 0, 3f);
            title = NewText("Title", band.transform, 32, Brand1025.Ink, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform);
            body = NewText("Body", callout, 24, Color.white, TextAnchor.UpperLeft);
            body.rectTransform.anchorMin = Vector2.zero; body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.offsetMin = new Vector2(30, 46); body.rectTransform.offsetMax = new Vector2(-30, -44);
            body.horizontalOverflow = HorizontalWrapMode.Wrap; body.lineSpacing = 1.15f;
            footer = NewText("Footer", callout, 18, new Color32(255, 212, 90, 255), TextAnchor.LowerRight);
            footer.rectTransform.anchorMin = Vector2.zero; footer.rectTransform.anchorMax = new Vector2(1, 0);
            footer.rectTransform.pivot = new Vector2(.5f, 0);
            footer.rectTransform.sizeDelta = new Vector2(-40, 36); footer.rectTransform.anchoredPosition = new Vector2(0, 10);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (clock < .25f) return; // avoid skipping a step with the same tap
            Advance();
        }

        void Advance()
        {
            index++;
            clock = 0f;
            if (index >= steps.Count)
            {
                Action callback = done; done = null;
                Destroy(gameObject);
                try { callback?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                return;
            }
            Step step = steps[index];
            title.text = step.Title;
            body.text = step.Body;
            footer.text = $"点击继续 ▼  {index + 1}/{steps.Count}";
            Place(step);
        }

        void Place(Step step)
        {
            Rect area = root.rect;
            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            var corners = new Vector3[4];
            if (step.Targets != null)
                foreach (RectTransform target in step.Targets)
                {
                    if (target == null || !target.gameObject.activeInHierarchy) continue;
                    target.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector2 p = root.InverseTransformPoint(corner);
                        if (!any) { min = max = p; any = true; }
                        else { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                    }
                }
            frame.gameObject.SetActive(any);
            float calloutY;
            if (any)
            {
                min -= new Vector2(10, 10); max += new Vector2(10, 10);
                frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
                frame.pivot = new Vector2(.5f, .5f);
                frame.anchoredPosition = (min + max) * .5f - area.center;
                frame.sizeDelta = max - min;
                // Callout on the side with more room.
                bool below = (min.y - area.yMin) > (area.yMax - max.y);
                calloutY = below ? min.y - 150f : max.y + 150f;
            }
            else calloutY = area.center.y;
            callout.anchorMin = callout.anchorMax = new Vector2(.5f, .5f);
            callout.pivot = new Vector2(.5f, .5f);
            float halfH = callout.sizeDelta.y * .5f + 30f;
            calloutY = Mathf.Clamp(calloutY, area.yMin + halfH, area.yMax - halfH);
            callout.anchoredPosition = new Vector2(0f, calloutY - area.center.y);
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (frame == null || !frame.gameObject.activeSelf) return;
            float pulse = 1f + .025f * Mathf.Sin(clock * 6f);
            frame.localScale = new Vector3(pulse, pulse, 1f);
        }

        RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }

        Text NewText(string name, Transform parent, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font; text.fontSize = size; text.color = color; text.alignment = anchor;
            text.fontStyle = FontStyle.Bold; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    }
}
