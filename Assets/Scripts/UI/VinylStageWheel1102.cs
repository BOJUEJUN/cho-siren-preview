using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>A direct-touch record selector. The record turns; its readable labels never rotate.</summary>
    public sealed class VinylStageWheel1102 : MonoBehaviour, IPointerDownHandler,
        IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float Step = 36f, Radius = 348f, Duration = .42f;
        RectTransform disc;
        RectTransform[] nodes;
        CanvasGroup[] visibility;
        Action<int> select;
        Func<int, bool> canSelect;
        int referenceStage;
        float angle, fromAngle, targetAngle, animationTime;
        float lastPointerAngle, lastPointerTime, velocity;
        bool animating, tickWhenSettled;
        bool applicationPaused, hasFocus = true;
        GameAudio audio;

        public int SelectedStage { get; private set; }
        public float Angle => angle;
        public bool IsDragging { get; private set; }

        public void Configure(RectTransform record, RectTransform[] stageNodes, Button[] buttons,
            int initial, Action<int> onSelect, Func<int, bool> selectable)
        {
            disc = record; nodes = stageNodes; select = onSelect; canSelect = selectable;
            audio = UnityNull.Real(UnityEngine.Object.FindAnyObjectByType<GameAudio>());
            referenceStage = SelectedStage = Mathf.Clamp(initial, 1, nodes.Length);
            angle = targetAngle = 0;
            visibility = new CanvasGroup[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                visibility[i] = UnityNull.Real(nodes[i].GetComponent<CanvasGroup>()) ?? nodes[i].gameObject.AddComponent<CanvasGroup>();
            }
            Pose();
        }

        public void ShowStage(int stage, bool animated = true)
        {
            if (nodes == null) return;
            SelectedStage = Mathf.Clamp(stage, 1, nodes.Length);
            float next = (SelectedStage - referenceStage) * Step;
            if (IsDragging) return;
            if (!animated)
            {
                animating = false; angle = targetAngle = next; Pose(); return;
            }
            if (animating && Mathf.Abs(targetAngle - next) < .01f) return;
            if (Mathf.Abs(angle - next) < .01f) { Pose(); return; }
            fromAngle = angle; targetAngle = next; animationTime = 0; animating = true;
            tickWhenSettled = Mathf.Abs(angle - next) > .5f;
        }

        void Update()
        {
            UpdateScrubSound();
            if (!animating || applicationPaused || !hasFocus) return;
            animationTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(animationTime / Duration);
            angle = Mathf.LerpUnclamped(fromAngle, targetAngle, 1f - Mathf.Pow(1f - t, 3f));
            if (t >= 1f)
            {
                angle = targetAngle; animating = false;
                if (tickWhenSettled && audio != null) audio.PlayVinylTick();
                tickWhenSettled = false;
            }
            Pose();
        }

        public void OnPointerDown(PointerEventData e)
        {
            animating = false; velocity = 0;
            lastPointerAngle = PointerAngle(e.position, e.pressEventCamera);
            lastPointerTime = Time.unscaledTime;
            if (audio != null) audio.ResumeAfterUserGesture();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!IsDragging) ShowStage(SelectedStage);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            IsDragging = true; animating = false; velocity = 0;
            if (audio != null) audio.ResumeAfterUserGesture();
            lastPointerAngle = PointerAngle(e.pressPosition, e.pressEventCamera);
            lastPointerTime = Time.unscaledTime;
            e.eligibleForClick = false;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!IsDragging || nodes == null) return;
            e.eligibleForClick = false;
            float now = Time.unscaledTime;
            float pointer = PointerAngle(e.position, e.pressEventCamera);
            float delta = Mathf.DeltaAngle(lastPointerAngle, pointer);
            velocity = Mathf.Lerp(velocity, delta / Mathf.Max(.008f, now - lastPointerTime), .4f);
            angle = Mathf.Clamp(angle + delta, (1 - referenceStage) * Step - 14f,
                (nodes.Length - referenceStage) * Step + 14f);
            lastPointerAngle = pointer; lastPointerTime = now;
            Pose();
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!IsDragging || nodes == null) return;
            IsDragging = false; e.eligibleForClick = false;
            if (audio != null) audio.StopVinylScrub();
            if (Time.unscaledTime - lastPointerTime > .12f) velocity = 0;
            float projection = angle + Mathf.Clamp(velocity * .11f, -Step * 1.3f, Step * 1.3f);
            int target = Mathf.Clamp(Mathf.RoundToInt(projection / Step + referenceStage), 1, nodes.Length);
            if (canSelect != null && !canSelect(target))
            {
                int closest = SelectedStage;
                float distance = float.MaxValue;
                for (int i = 1; i <= nodes.Length; i++)
                    if (canSelect(i) && Mathf.Abs(i - target) < distance)
                    { closest = i; distance = Mathf.Abs(i - target); }
                target = closest;
            }
            select?.Invoke(target);
            // Also snaps when the model rejects a requested stage or selection did not change.
            ShowStage(SelectedStage);
        }

        float PointerAngle(Vector2 screen, Camera camera)
        {
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out Vector2 local);
            Vector2 offset = local - rect.rect.center;
            return Mathf.Atan2(-offset.y, offset.x) * Mathf.Rad2Deg;
        }

        void Pose()
        {
            if (disc == null || nodes == null) return;
            disc.localEulerAngles = new Vector3(0, 0, -angle);
            Vector2 centre = ((RectTransform)transform).rect.center;
            float current = referenceStage + angle / Step;
            for (int i = 0; i < nodes.Length; i++)
            {
                float radians = (-180f - (i + 1 - referenceStage) * Step + angle) * Mathf.Deg2Rad;
                Vector2 position = centre + new Vector2(Mathf.Cos(radians) * Radius, -Mathf.Sin(radians) * Radius);
                Vector2 size = nodes[i].rect.size;
                nodes[i].anchoredPosition = position + new Vector2(-size.x * .5f, size.y * .5f);
                float distance = Mathf.Abs(i + 1 - current);
                float alpha = Mathf.Clamp01((2.45f - distance) / .45f);
                visibility[i].alpha = alpha;
                visibility[i].blocksRaycasts = alpha > .25f;
                visibility[i].interactable = alpha > .25f;
            }
        }

        void UpdateScrubSound()
        {
            if (audio == null) return;
            bool moving = IsDragging && !applicationPaused && hasFocus && !AudioListener.pause &&
                Time.timeScale > 0 && Time.unscaledTime - lastPointerTime < .075f && Mathf.Abs(velocity) > 2f;
            if (moving) audio.SetVinylScrub(Mathf.Clamp01(Mathf.Abs(velocity) / 300f));
            else audio.StopVinylScrub();
        }

        void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused) { IsDragging = false; if (audio != null) audio.StopVinylScrub(); ShowStage(SelectedStage); }
        }

        void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused) { IsDragging = false; if (audio != null) audio.StopVinylScrub(); ShowStage(SelectedStage); }
        }

        void OnDisable()
        {
            IsDragging = false; animating = false; tickWhenSettled = false;
            if (audio != null) audio.StopVinylScrub();
        }
    }
}
