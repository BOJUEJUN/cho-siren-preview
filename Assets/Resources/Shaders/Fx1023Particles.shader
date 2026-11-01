// Procedural battle particles for Fx1023: one quad per particle, the shape is drawn in the shader.
// uv0 = quad corner (-1..1), uv1.x = kind (0 spark, 1 heart, 2 glint, 3 water drop, 4 petal, 5 shard),
// vertex colour = tint, vertex alpha = fade. Sparks and glints are meant for an additive material,
// the inked shapes (1, 3–5) for a premultiplied one; the material's blend factors decide.
Shader "ChoSiren/Fx1023Particles"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Ink, _Light;
            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 l : TEXCOORD0; float k : TEXCOORD1; };
            v2f vert(appdata_t v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.l = float2(v.uv0.x, -v.uv0.y); o.k = v.uv1.x; return o; }
            float aastep(float th, float x) { float w = max(fwidth(x) * .75, 1e-4); return smoothstep(th - w, th + w, x); }
            float dot2(float2 v) { return dot(v, v); }
            float sdHeart(float2 p)
            {
                p.x = abs(p.x);
                if (p.y + p.x > 1.) return sqrt(dot2(p - float2(.25, .75))) - sqrt(2.) / 4.;
                return sqrt(min(dot2(p - float2(0., 1.)), dot2(p - .5 * max(p.x + p.y, 0.)))) * sign(p.x - p.y);
            }
            fixed4 cel(float d, float hl, fixed4 c)
            {
                float fill = 1. - aastep(0., d), inner = 1. - aastep(-.13, d);
                // vertex colours arrive linear from the canvas; the palette uniforms are sRGB numbers
                float3 ink = _Ink.rgb, light = _Light.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                ink = GammaToLinearSpace(ink); light = GammaToLinearSpace(light);
                #endif
                float3 col = lerp(ink, c.rgb, inner); col = lerp(col, light, hl * inner);
                return float4(col, 1.) * fill * c.a;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 l = i.l; float k = i.k;
                if (k < .5) { float d = length(l), g = pow(max(0., 1. - d), 2.), core = 1. - smoothstep(0., .5, d); return float4(lerp(i.color.rgb, 1., core) * g * i.color.a, 0.); }
                if (k < 1.5) { float2 p = float2(l.x, -l.y) * 1.15 + float2(0., .55); return cel(sdHeart(p), 1. - aastep(.13, length((p - float2(-.2, .72)) * float2(1., 1.5))), i.color); }
                if (k < 2.5) { float2 q = abs(l); float s = min(q.x * 7. + q.y, q.y * 7. + q.x); return float4((i.color.rgb * max(0., 1. - s) + (1. - smoothstep(0., .3, length(l)))) * i.color.a, 0.); }
                if (k < 3.5) { float2 p = l * float2(1., .8); float d = min(length(p + float2(0., .2)) - .5, max(abs(p.x) - (.5 - (p.y + .2) * .5), max(-(p.y + .2), p.y - .75))); return cel(d, 1. - aastep(.12, length(p - float2(-.18, -.32))), i.color); }
                if (k < 4.5) { float d = length(l * float2(1., 2.2)) - .8; return cel(d, 1. - aastep(.1, length(l - float2(-.25, -.1))), i.color); }
                float d5 = max(-l.y - .85, abs(l.x) * 1.4 + l.y * .7 - .5); return cel(d5, 0., i.color);
            }
            ENDCG
        }
    }
}
