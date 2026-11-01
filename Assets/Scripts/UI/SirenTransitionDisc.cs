using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>Transparent glass record with a separate moving reflection, never an input surface.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SirenTransitionDisc : UnityEngine.UI.MaskableGraphic
    {
        public bool Animate = true;
        public float ReflectionAngle = 26f;

        public static SirenTransitionDisc Add(Transform parent, string name)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SirenTransitionDisc));
            node.transform.SetParent(parent, false);
            var disc = node.GetComponent<SirenTransitionDisc>();
            disc.raycastTarget = false;
            return disc;
        }

        private void Update()
        {
            if (Animate && Application.isFocused && !BattlePresentationOptions.ReduceMotion) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float radius = Mathf.Min(r.width, r.height) * .48f;
            if (radius <= 0f) return;
            Vector2 c = r.center;
            AlbumGraphicGeometry929.Ring(vh, c, radius, radius * .13f, 0, 360,
                new Color32(12, 10, 22, 180), 96);
            AlbumGraphicGeometry929.Ring(vh, c, radius, radius * .985f, 0, 360,
                new Color32(235, 231, 255, 145), 96);
            for (int i = 0; i < 4; i++)
                AlbumGraphicGeometry929.Ring(vh, c, radius * (.78f + i * .047f),
                    radius * (.777f + i * .047f), 0, 360, new Color32(213, 201, 255, 35), 80);
            AlbumGraphicGeometry929.Ring(vh, c, radius * .27f, radius * .13f, 0, 360,
                new Color32(118, 43, 255, 155), 64);
            float angle = ReflectionAngle + (Animate && !BattlePresentationOptions.ReduceMotion
                ? Mathf.Sin(Time.unscaledTime * .75f) * 12f : 0f);
            AlbumGraphicGeometry929.Ring(vh, c, radius * .96f, radius * .90f, angle, 44,
                new Color32(247, 245, 255, 190), 28);
            AlbumGraphicGeometry929.Ring(vh, c, radius * .88f, radius * .86f, angle + 184, 30,
                new Color32(63, 240, 255, 120), 20);
        }
    }
}
