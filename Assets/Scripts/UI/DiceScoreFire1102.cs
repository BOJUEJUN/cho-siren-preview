using UnityEngine;
using UnityEngine.UI;
namespace ChoSiren.UI
{
    /// <summary>Small broken streams follow the square die's rim, leaving the face and pips clear.</summary>
    public sealed class DiceScoreFire1102 : MonoBehaviour
    {
        Material rimMaterial;
        RawImage rim;
        RectTransform restingRig;
        float seed;
        public bool Visible => rim != null && rim.enabled;
        public static DiceScoreFire1102 Create(RectTransform host, Vector2 dieSize)
        {
            var effect = host.gameObject.AddComponent<DiceScoreFire1102>();
            effect.Build(host, dieSize); return effect;
        }
        void Build(RectTransform host, Vector2 size)
        {
            Shader shader = Resources.Load<Shader>("Shaders/DiceEdgeFlow1102");
            if (shader == null) return;
            seed = Random.value * 13f;
            rimMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            rim = Image(host, "ScoringEdgeFlow", rimMaterial);
            // The PSD hit target is taller than its square face. Use the face's fitted square,
            // not that rectangular target; the spare margin only holds a few escaping embers.
            float side = Mathf.Min(size.x, size.y);
            rim.rectTransform.sizeDelta = Vector2.one * side * 1.16f;
            if (host.parent != null)
                foreach (UnityEngine.UI.Image face in host.parent.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    if (face.name.StartsWith("DiceFace-"))
                    {
                        restingRig = face.transform.parent as RectTransform;
                        break;
                    }
        }
        static RawImage Image(Transform parent, string name, Material material)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            node.transform.SetParent(parent, false);
            var image = node.GetComponent<RawImage>(); image.material = material; image.raycastTarget = false; image.enabled = false;
            var r = image.rectTransform; r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f;
            return image;
        }
        public void Pose(float amount, DiceFxStyle style, float clock)
        {
            if (rim == null) return;
            rim.enabled = amount > .001f;
            if (!rim.enabled) return;
            SetPose(rimMaterial, amount, style, clock);
            Material rendered = rim.materialForRendering;
            if (rendered != rimMaterial) SetPose(rendered, amount, style, clock);
        }
        void SetPose(Material material, float amount, DiceFxStyle style, float clock)
        {
            if (material == null) return;
            material.SetColor("_EdgeColor", Color.Lerp(style.Color, Color.white, .12f));
            material.SetFloat("_FxTime", clock);
            material.SetFloat("_Seed", seed);
            material.SetFloat("_Alpha", amount * Mathf.Clamp(.72f * style.Scale, .55f, .9f));
        }
        void LateUpdate()
        {
            if (!Visible || restingRig == null) return;
            // Keep the narrow edge attached to the tiny idle bob/tilt of the authored die.
            rim.rectTransform.anchoredPosition = restingRig.anchoredPosition;
            rim.rectTransform.localRotation = restingRig.localRotation;
            rim.rectTransform.localScale = restingRig.localScale;
        }
        void OnDestroy() { if (rimMaterial != null) Destroy(rimMaterial); }
    }
}
