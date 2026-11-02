using UnityEngine;
using UnityEngine.UI;
namespace ChoSiren.UI
{
    /// <summary>The same cel flame as an ultimate-ready record, above the scoring dice and outside their pips.</summary>
    public sealed class DiceScoreFire1102 : MonoBehaviour
    {
        Material flameMaterial, rimMaterial;
        RawImage flame, rim;
        float seed;
        static readonly string[] Palette = { "_Deep", "_Main", "_Hot", "_Light", "_Acc", "_Ink" };
        public bool Visible => rim != null && rim.enabled;
        public static DiceScoreFire1102 Create(RectTransform host, Vector2 dieSize)
        {
            var effect = host.gameObject.AddComponent<DiceScoreFire1102>();
            effect.Build(host, dieSize); return effect;
        }
        void Build(RectTransform host, Vector2 size)
        {
            Shader shader = Resources.Load<Shader>("Shaders/Fx1023Spire");
            if (shader == null) return;
            seed = Random.value * 20f;
            flameMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            rimMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            flameMaterial.SetFloat("_SrcBlend", 1); flameMaterial.SetFloat("_DstBlend", 10);
            rimMaterial.SetFloat("_SrcBlend", 1); rimMaterial.SetFloat("_DstBlend", 1);
            flame = Image(host, "ScoringSpire", flameMaterial);
            // Show only the spire above the bowl. It begins at the die's upper edge, leaving every pip clear.
            flame.uvRect = new Rect(0, .48f, 1, .52f);
            flame.rectTransform.pivot = new Vector2(.5f, 0f);
            flame.rectTransform.anchoredPosition = new Vector2(0, size.y * .39f);
            flame.rectTransform.sizeDelta = new Vector2(size.x * 1.12f, size.x * .88f);
            rim = Image(host, "ScoringRim", rimMaterial);
            rim.rectTransform.sizeDelta = size * 1.3f;
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
            if (flame == null) return;
            flame.enabled = rim.enabled = amount > .001f;
            if (!rim.enabled) return;
            Color c = style.Color;
            Color[] palette = { c * .26f, c * .65f, c, Color.Lerp(c, Color.white, .35f), Color.Lerp(c, Color.white, .8f), new Color(.04f,.02f,.08f) };
            for (int layer = 0; layer < 2; layer++)
            {
                RawImage image = layer == 0 ? flame : rim;
                Material original = layer == 0 ? flameMaterial : rimMaterial;
                foreach (Material m in new[] { original, image.materialForRendering })
                {
                    if (m == null) continue;
                    for (int i = 0; i < Palette.Length; i++) m.SetVector(Palette[i], palette[i]);
                    m.SetFloat("_T", .3f); m.SetFloat("_FxTime", clock + seed); m.SetFloat("_Seed", seed);
                    m.SetFloat("_Alpha", amount * (layer == 0 ? .55f : .9f) * style.Scale);
                    m.SetFloat("_Aspect", layer == 0 ? 1.5f : 1f); m.SetFloat("_DiscY", .5f);
                    m.SetFloat("_DiscR", .39f); m.SetFloat("_Front", layer);
                }
            }
        }
        void OnDestroy() { if (flameMaterial != null) Destroy(flameMaterial); if (rimMaterial != null) Destroy(rimMaterial); }
    }
}
