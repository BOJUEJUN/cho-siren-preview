using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Ultimate-ready cue (owner 2026-10-20): golden fire rising above the album disc.
    /// 2026-10-23 review #44 ("特效还是不好看"): the soft particle tongues are replaced by the same cel-banded
    /// procedural flame as the approved battle VFX (Fx1023Flame shader) in a gold palette, so the card cue
    /// speaks the same language as the skills.
    /// 2026-10-25 (少爷: "形状能不能更像我给你的PSD设计的形状"): the flame takes the PSD's ultimate-marker silhouette
    /// (大招特效标志 — a tall spire, purple at the base to lime at the needle tip, ^^ chevrons inside; the tip now burns brand teal) as the
    /// Fx1023Spire shader, still licking and swaying like fire.
    /// </summary>
    public sealed class UltReadyRing1020 : MonoBehaviour
    {
        static Shader flameShader;
        static readonly int T_ = Shader.PropertyToID("_T"), Time_ = Shader.PropertyToID("_FxTime"), Alpha_ = Shader.PropertyToID("_Alpha"),
            Seed_ = Shader.PropertyToID("_Seed"), Shape_ = Shader.PropertyToID("_Shape"), Hole_ = Shader.PropertyToID("_Hole"),
            Src_ = Shader.PropertyToID("_SrcBlend"), Dst_ = Shader.PropertyToID("_DstBlend"),
            Aspect_ = Shader.PropertyToID("_Aspect"), DiscY_ = Shader.PropertyToID("_DiscY"), DiscR_ = Shader.PropertyToID("_DiscR"),
            Front_ = Shader.PropertyToID("_Front");
        // The fire quad is 1.3 records wide and 2.5 tall; the record's centre sits .5 quad-widths above its foot.
        const float QuadW = 1.3f, QuadH = 2.5f, DiscY = .5f, DiscR = .5f / QuadW;
        // deep, main, hot, light, accent, ink — sRGB numbers sampled from the PSD spire; the shader converts them
        static readonly Vector4[] Gold =
        {
            new Vector4(.36f, .16f, .66f, 1f), new Vector4(.2f, .42f, 1f, 1f), new Vector4(.25f, .94f, 1f, 1f),
            new Vector4(.11f, .95f, .93f, 1f), new Vector4(.8f, 1f, .98f, 1f), new Vector4(.05f, .03f, .1f, 1f),
        };
        static readonly int[] PalIds =
        {
            Shader.PropertyToID("_Deep"), Shader.PropertyToID("_Main"), Shader.PropertyToID("_Hot"),
            Shader.PropertyToID("_Light"), Shader.PropertyToID("_Acc"), Shader.PropertyToID("_Ink"),
        };

        RectTransform area, disc, rimArea;
        RawImage flame, rim;
        Material material, rimMaterial;
        bool ready;
        float amount, clock, seed, charge;

        public static UltReadyRing1020 Attach(RectTransform disc)
        {
            // The flame lives on the disc's parent so it does not spin with the record.
            var go = new GameObject("UltReadyFlame", typeof(RectTransform));
            go.transform.SetParent(disc.parent, false);
            go.transform.SetSiblingIndex(disc.GetSiblingIndex() + 1);
            var ring = go.AddComponent<UltReadyRing1020>();
            ring.Build(disc);
            return ring;
        }

        void Build(RectTransform target)
        {
            disc = target;
            area = (RectTransform)transform;
            area.anchorMin = area.anchorMax = area.pivot = new Vector2(.5f, .5f);
            if (flameShader == null) flameShader = Resources.Load<Shader>("Shaders/Fx1023Spire");
            if (flameShader == null) return;
            material = new Material(flameShader) { hideFlags = HideFlags.DontSave };
            material.SetFloat(Src_, 1f); material.SetFloat(Dst_, 10f);   // premultiplied
            material.SetFloat(Shape_, 1f); material.SetFloat(Hole_, 0f);
            seed = Random.value * 20f;
            material.SetFloat(Seed_, seed);
            for (int i = 0; i < PalIds.Length; i++) material.SetVector(PalIds[i], Gold[i]);
            var go = new GameObject("CelFlame", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(transform, false);
            flame = go.GetComponent<RawImage>();
            flame.raycastTarget = false; flame.material = material; flame.color = Color.white;
            // pivot on the record's centre: the bowl wraps it, the spire climbs from its top
            var r = flame.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.pivot = new Vector2(.5f, DiscY * QuadW / QuadH);
            flame.enabled = false;
            // The burning rim sits in front of the record (a sibling right after it), so the fire reads as coming
            // out of the record instead of standing behind it.
            rimMaterial = new Material(flameShader) { hideFlags = HideFlags.DontSave };
            rimMaterial.SetFloat(Src_, 1f); rimMaterial.SetFloat(Dst_, 1f);   // additive glow
            rimMaterial.SetFloat(Seed_, seed + 7f);
            for (int i = 0; i < PalIds.Length; i++) rimMaterial.SetVector(PalIds[i], Gold[i]);
            var rimGo = new GameObject("UltReadyDiscRim", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            rimGo.transform.SetParent(target.parent, false);
            rimArea = (RectTransform)rimGo.transform;
            rim = rimGo.GetComponent<RawImage>();
            rim.raycastTarget = false; rim.material = rimMaterial; rim.color = Color.white;
            rimArea.anchorMin = rimArea.anchorMax = rimArea.pivot = new Vector2(.5f, .5f);
            rim.enabled = false;
        }

        public void SetReady(bool value) => ready = value;
        /// <summary>0..1 while the record is being swiped out: the fire runs faster and taller.</summary>
        public void SetCharge(float value) => charge = Mathf.Clamp01(value);

        void Update()
        {
            if (disc == null || flame == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            amount = Mathf.MoveTowards(amount, ready && disc.gameObject.activeInHierarchy ? 1f : 0f, dt / .3f);
            if (!BattlePresentationOptions.ReduceMotion) clock += dt * (1f + 1.6f * charge);
            flame.enabled = amount > 0f;
            if (rim != null) rim.enabled = flame.enabled;
            if (!flame.enabled) return;
            // Base of the fire on the disc's upper rim (the disc pivots at its centre; it slides up when ready).
            Vector2 centre = (Vector2)disc.localPosition + Vector2.Scale(new Vector2(.5f, .5f) - disc.pivot, disc.rect.size);
            // base at the record's centre: the record (drawn over the flame) hides where the fire starts
            area.localPosition = centre;
            float w = Mathf.Max(1f, disc.rect.width);
            flame.rectTransform.sizeDelta = new Vector2(w * QuadW, w * QuadH * (1f + .22f * charge));
            flame.rectTransform.anchoredPosition = Vector2.zero;
            if (rim != null)
            {
                rimArea.localPosition = centre;
                rimArea.sizeDelta = new Vector2(w * QuadW, w * QuadW);
                int front = disc.GetSiblingIndex() + 1;
                if (rimArea.GetSiblingIndex() != front && front < disc.parent.childCount) rimArea.SetSiblingIndex(front);
            }
            // _T .3 holds the shader's grown, not-yet-fading pose; ready fades the whole flame in and out.
            Apply(material, flame, false);
            if (rim != null) Apply(rimMaterial, rim, true);
        }

        void Apply(Material m, RawImage image, bool front)
        {
            foreach (Material target in new[] { m, image.materialForRendering })
            {
                if (target == null) continue;
                target.SetFloat(T_, .3f); target.SetFloat(Time_, clock + seed); target.SetFloat(Alpha_, amount);
                target.SetFloat(Aspect_, front ? 1f : QuadH / QuadW); target.SetFloat(DiscY_, front ? .5f : DiscY);
                target.SetFloat(DiscR_, DiscR); target.SetFloat(Front_, front ? 1f : 0f);
                // a mask's stencil copy does not carry the undeclared palette uniforms
                if (target != m) for (int i = 0; i < PalIds.Length; i++) target.SetVector(PalIds[i], Gold[i]);
            }
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (rimMaterial != null) Destroy(rimMaterial);
            if (rimArea != null) Destroy(rimArea.gameObject);
        }
    }
}
