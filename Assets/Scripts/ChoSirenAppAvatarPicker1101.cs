using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 (赵金: "这个头像要支持点击头像换头像，而不是需要点击改名进去才能换头像"):
    /// 顶栏头像和档案页头像都直接开这张「换头像」大图网格，点一下就换上并立即刷新；改名仍留在档案里。
    /// 网格 = 预置证件照 + 已拥有成员的立绘（按 PortraitFace1101 的人脸锚点裁成头像）。
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        /// <summary>当前生效的头像：优先「佩戴的成员脸」，否则预置证件照。</summary>
        private Sprite PlayerAvatarSpriteNow()
        {
            if (model == null) return null;
            string memberId = model.PlayerAvatarMemberId;
            if (!string.IsNullOrEmpty(memberId))
            {
                int index = GameModel.MemberIndexFor1101(memberId);
                if (index >= 0)
                {
                    Sprite art = Resources.Load<Sprite>(GameModel.Members[index].ResourcePath);
                    Sprite face = PortraitFace1101.CropSprite(memberId, art, 1f, PortraitFace1101.Framing.Head);
                    if (face != null) return face;
                }
            }
            return PlayerAvatarSprite(model.PlayerAvatar);
        }

        /// <summary>点头像即开：选中就换，右上角可取消。</summary>
        private void OpenAvatarPicker1101()
        {
            if (safeRoot == null || model == null) return;
            gameAudio?.PlayClick();
            GameObject overlay = null;
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "AvatarPicker1101", "换头像",
                "经纪人头像", () => { if (overlay != null) Destroy(overlay); }, 700f, closeName: "Close");
            overlay = frame.Overlay;
            PlaceTop(frame.CloseButton.GetComponent<RectTransform>(), frame.Width * .895f - 88f, 30f, 88f, 80f);
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);

            var faces = new List<Sprite>();
            var apply = new List<Action>();
            int selected = -1;
            for (int i = 0; i < GameModel.PlayerAvatarResources.Length; i++)
            {
                int choice = i;
                Sprite sprite = PlayerAvatarSprite(i);
                if (sprite == null) continue;
                faces.Add(sprite);
                apply.Add(() => model.SetPlayerAvatarPreset(choice));
                if (model.PlayerAvatarMemberId == null && model.PlayerAvatar == i) selected = faces.Count - 1;
            }
            foreach (string memberId in model.AvatarMemberChoices())
            {
                string id = memberId;
                int index = GameModel.MemberIndexFor1101(id);
                if (index < 0) continue;
                Sprite art = Resources.Load<Sprite>(GameModel.Members[index].ResourcePath);
                Sprite face = PortraitFace1101.CropSprite(id, art, 1f, PortraitFace1101.Framing.Head);
                if (face == null) continue;
                faces.Add(face);
                apply.Add(() => model.SetPlayerAvatarMember(id));
                if (model.PlayerAvatarMemberId == id) selected = faces.Count - 1;
            }

            const int columns = 4;
            const float gap = 16f;
            float tile = (width - (columns - 1) * gap) / columns;
            int rows = Mathf.Max(1, (faces.Count + columns - 1) / columns);
            float contentHeight = rows * tile + (rows - 1) * gap + 8f;
            RectTransform content = AvatarScrollContent1102(frame, width, contentHeight, out Func<bool> canPick);
            for (int i = 0; i < faces.Count; i++)
            {
                int slot = i;
                bool on = i == selected;
                GameObject ring = NewImage("Pick-" + i, content, DotSprite(),
                    on ? (Color)Brand1025.Teal : new Color(1f, 1f, 1f, .14f));
                PlaceTop(ring.GetComponent<RectTransform>(), i % columns * (tile + gap), i / columns * (tile + gap), tile, tile);
                ring.GetComponent<Image>().raycastTarget = true;
                GameObject mask = NewImage("Mask", ring.transform, DotSprite(), Color.white);
                Stretch(mask.GetComponent<RectTransform>(), 5f, 5f, -5f, -5f);
                mask.AddComponent<Mask>().showMaskGraphic = false;
                Image picture = NewImage("Face", mask.transform, faces[i], Color.white).GetComponent<Image>();
                Stretch(picture.rectTransform);
                picture.preserveAspect = true;
                if (on)
                {
                    GameObject check = NewImage("Check", ring.transform, DotSprite(), Brand1025.Teal);
                    RectTransform checkRect = check.GetComponent<RectTransform>();
                    checkRect.anchorMin = checkRect.anchorMax = checkRect.pivot = new Vector2(1f, 0f);
                    checkRect.sizeDelta = new Vector2(tile * .28f, tile * .28f);
                    checkRect.anchoredPosition = new Vector2(-tile * .04f, tile * .04f);
                }
                Button pick = ring.AddComponent<Button>();
                pick.transition = Selectable.Transition.None;
                pick.targetGraphic = ring.GetComponent<Image>();
                pick.onClick.AddListener(() =>
                {
                    if (!canPick()) return;
                    apply[slot]?.Invoke();
                    RefreshPlayerIdentity1028();
                    gameAudio?.PlaySuccess();
                    Destroy(overlay);
                    Toast("已换头像");
                });
            }
        }

        // Keep the existing four-column faces inside the modal; the header and close button
        // stay outside the scrolling body. Both avatar entry points use the same clipping.
        private RectTransform AvatarScrollContent1102(ModalPolish038Frame frame, float width,
            float contentHeight, out Func<bool> canPick)
        {
            ModalPolish038Chrome.FitHeight(frame, contentHeight);
            GameObject view = NewImage("AvatarScroll", frame.Body, null, Color.clear);
            Stretch(view.GetComponent<RectTransform>());
            view.GetComponent<Image>().raycastTarget = false;
            GameObject viewport = NewImage("Viewport", view.transform, null, new Color(1f, 1f, 1f, .004f));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            PlaceTop(content, 0f, 0f, width, contentHeight);
            ScrollRect scroll = view.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = .135f;
            scroll.scrollSensitivity = 36f;

            // ScrollRect cancels normal pointer clicks during a drag; also protect the release
            // frame, where a touch release must never select the face now under the finger.
            bool dragging = false;
            float releasedAt = float.NegativeInfinity;
            EventTrigger gesture = view.AddComponent<EventTrigger>();
            var begin = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            begin.callback.AddListener(_ => dragging = true);
            gesture.triggers.Add(begin);
            var end = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            end.callback.AddListener(_ => { dragging = false; releasedAt = Time.unscaledTime; });
            gesture.triggers.Add(end);
            canPick = () => !dragging && Time.unscaledTime - releasedAt > .12f;
            return content;
        }
    }
}
