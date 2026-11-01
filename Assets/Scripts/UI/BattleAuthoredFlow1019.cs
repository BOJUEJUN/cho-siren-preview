using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Animates the supplied alpha artwork itself; never changes a control's hit rect.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class BattleAuthoredFlow1019 : BaseMeshEffect
    {
        const int Columns = 16, Rows = 20;
        Func<bool> paused;
        bool pillar, showing;
        float clock;
        readonly UIVertex[] corners = new UIVertex[4];
        public float VisualClock => clock;
        protected override void OnEnable()
        {
            base.OnEnable();
            BattlePresentationOptions.Changed += InvalidatePose;
        }
        protected override void OnDisable()
        {
            BattlePresentationOptions.Changed -= InvalidatePose;
            base.OnDisable();
        }
        void InvalidatePose() { if (graphic != null) graphic.SetVerticesDirty(); }
        public void Configure(bool upwardPillar, Func<bool> pause)
        {
            pillar = upwardPillar; paused = pause;
            graphic.raycastTarget = false;
        }
        public void SetShowing(bool value)
        {
            if (showing == value) return;
            showing = value;
            if (!value) clock = 0;
            graphic.SetVerticesDirty();
        }
        void Update() => Advance(Mathf.Min(Time.unscaledDeltaTime, .1f));
        public void Advance(float seconds)
        {
            if (!showing || BattlePresentationOptions.ReduceMotion || (paused?.Invoke() ?? false) ||
                seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            clock += seconds;
            graphic.SetVerticesDirty();
        }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || !showing || BattlePresentationOptions.ReduceMotion || mesh.currentVertCount != 4) return;
            for (int i = 0; i < 4; i++) mesh.PopulateUIVertex(ref corners[i], i);
            mesh.Clear();
            var rect = ((RectTransform)transform).rect;
            for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Columns; x++)
            {
                float u = x / (float)Columns, v = y / (float)Rows;
                var a = Blend(corners[0], corners[3], u);
                var b = Blend(corners[1], corners[2], u);
                var vertex = Blend(a, b, v);
                // Coherent moving noise disturbs the existing torn cyan edge. The PNG's
                // own alpha remains the cutout; no rectangular glow is introduced.
                if (!pillar)
                {
                    float n = Mathf.PerlinNoise(u * 4.6f + clock * 1.6f, v * 3.2f - clock * .7f) - .5f;
                    float n2 = Mathf.PerlinNoise(u * 6f - clock * .9f, v * 4f + clock * 1.2f) - .5f;
                    vertex.position += new Vector3(n * Mathf.Min(11f, rect.width * .035f),
                        n2 * Mathf.Min(12f, rect.height * .08f), 0);
                }
                else
                {
                    // Light travels upward through the native yellow arrow and beam.
                    // A soft taper breathes locally instead of bouncing the whole card.
                    float wave = Mathf.Sin(v * 11f - clock * 5.5f);
                    float spread = 1 + .055f * wave * Mathf.Sin(v * Mathf.PI);
                    vertex.position.x = rect.center.x + (vertex.position.x - rect.center.x) * spread;
                    vertex.position.y += Mathf.Sin(v * 8f - clock * 4f) * 2.4f * Mathf.Sin(v * Mathf.PI);
                    Color tint = vertex.color;
                    tint.a *= .58f + .42f * Mathf.Pow(.5f + .5f * wave, 2);
                    vertex.color = tint;
                }
                mesh.AddVert(vertex);
            }
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                int a = y * (Columns + 1) + x, b = a + Columns + 1;
                mesh.AddTriangle(a, b, b + 1); mesh.AddTriangle(b + 1, a + 1, a);
            }
        }
        static UIVertex Blend(UIVertex a, UIVertex b, float t)
        {
            var v = a; v.position = Vector3.Lerp(a.position, b.position, t);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, t); v.color = Color32.Lerp(a.color, b.color, t);
            return v;
        }
    }
}
