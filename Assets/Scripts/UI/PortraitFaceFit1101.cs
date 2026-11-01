using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 便捷组件：挂在 Image / RawImage 上，记住原立绘与构图，尺寸变化时自动重新裁切。
    /// Image 模式用 <see cref="PortraitFace1101.CropSprite"/>（无需 Mask）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortraitFaceFit1101 : MonoBehaviour
    {
        public string MemberId;
        public Sprite Source;
        public PortraitFace1101.Framing Framing = PortraitFace1101.Framing.Head;
        private Vector2 lastSize = new Vector2(-1f, -1f);

        public static PortraitFaceFit1101 Attach(Graphic target, string memberId, Sprite source,
            PortraitFace1101.Framing framing = PortraitFace1101.Framing.Head)
        {
            if (target == null) return null;
            PortraitFaceFit1101 fit = UnityNull.Real(target.GetComponent<PortraitFaceFit1101>())
                ?? target.gameObject.AddComponent<PortraitFaceFit1101>();
            fit.MemberId = memberId;
            fit.Source = source;
            fit.Framing = framing;
            fit.lastSize = new Vector2(-1f, -1f);
            fit.Refit();
            return fit;
        }

        /// <summary>Image 当前 sprite 作为原图。</summary>
        public static PortraitFaceFit1101 Attach(Image target, string memberId,
            PortraitFace1101.Framing framing = PortraitFace1101.Framing.Head)
        {
            if (target == null) return null;
            PortraitFaceFit1101 existing = UnityNull.Real(target.GetComponent<PortraitFaceFit1101>());
            Sprite source = existing != null && existing.Source != null && target.sprite != null
                            && target.sprite.name.StartsWith(existing.Source.name + "@", StringComparison.Ordinal)
                ? existing.Source : target.sprite;
            return Attach((Graphic)target, memberId, source, framing);
        }

        private void OnEnable() => Refit();
        private void OnRectTransformDimensionsChange() => Refit();

        public void Refit()
        {
            if (Source == null) return;
            RectTransform rt = transform as RectTransform;
            if (rt == null) return;
            Vector2 size = rt.rect.size;
            if (size.x <= 0f || size.y <= 0f) size = new Vector2(1f, 1f);
            if ((size - lastSize).sqrMagnitude < .25f) return;
            lastSize = size;
            float aspect = size.x / size.y;
            if (TryGetComponent(out Image image))
            {
                image.sprite = PortraitFace1101.CropSprite(MemberId, Source, aspect, Framing);
                image.preserveAspect = false;
            }
            else if (TryGetComponent(out RawImage raw))
            {
                PortraitFace1101.Apply(raw, MemberId, Source, Framing);
            }
        }
    }
}
