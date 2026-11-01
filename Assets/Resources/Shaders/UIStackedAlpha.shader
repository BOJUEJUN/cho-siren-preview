// UI video with its alpha stored beside the colour (left half = colour, right half = alpha as grey), so a
// plain H.264 MP4 can carry a cut-out character on every platform (lobby idols, 2026-10-25). Same
// stencil/clip contract as UI/Default so it still works under Mask and RectMask2D.
Shader "ChoSiren/UIStackedAlpha"
{
    Properties
    {
        [PerRendererData] _MainTex ("Video Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        // 1 when the alpha half was linearised on its way into an sRGB render texture (linear projects).
        _AlphaFromSrgb ("Alpha From sRGB", Float) = 0
        // 1 = Catmull-Rom colour reconstruction (2026-11-01): the clips are magnified ~1.6-1.9x on phones and
        // Retina screens, where bilinear filtering visibly softens the face. 0 = plain bilinear.
        _Bicubic ("Bicubic Colour", Float) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _AlphaFromSrgb;
            float _Bicubic;

            // Colour half only: every tap is clamped inside [0, .5] so the alpha half never bleeds in.
            half3 Colour(float2 uv)
            {
                float maxU = .5 - _MainTex_TexelSize.x * .5;
                uv.x = clamp(uv.x, _MainTex_TexelSize.x * .5, maxU);
                return tex2Dlod(_MainTex, float4(uv, 0, 0)).rgb;
            }

            // Catmull-Rom in 9 bilinear taps (weights folded into the middle pair of each axis).
            half3 ColourBicubic(float2 uv)
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 p = uv * size;
                float2 t1 = floor(p - .5) + .5;
                float2 f = p - t1;
                float2 w0 = f * (-.5 + f * (1. - .5 * f));
                float2 w1 = 1. + f * f * (-2.5 + 1.5 * f);
                float2 w2 = f * (.5 + f * (2. - 1.5 * f));
                float2 w3 = f * f * (-.5 + .5 * f);
                float2 w12 = w1 + w2;
                float2 t12 = (t1 + w2 / w12) / size;
                float2 t0 = (t1 - 1.) / size;
                float2 t3 = (t1 + 2.) / size;
                half3 c = 0;
                c += Colour(float2(t0.x, t0.y)) * (w0.x * w0.y);
                c += Colour(float2(t12.x, t0.y)) * (w12.x * w0.y);
                c += Colour(float2(t3.x, t0.y)) * (w3.x * w0.y);
                c += Colour(float2(t0.x, t12.y)) * (w0.x * w12.y);
                c += Colour(float2(t12.x, t12.y)) * (w12.x * w12.y);
                c += Colour(float2(t3.x, t12.y)) * (w3.x * w12.y);
                c += Colour(float2(t0.x, t3.y)) * (w0.x * w3.y);
                c += Colour(float2(t12.x, t3.y)) * (w12.x * w3.y);
                c += Colour(float2(t3.x, t3.y)) * (w3.x * w3.y);
                return saturate(c);
            }
            float4 _ClipRect;
            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 colourUv = float2(uv.x * .5, uv.y);
                half3 rgb = _Bicubic > .5 ? ColourBicubic(colourUv) : Colour(colourUv);
                // Clamped inside the alpha half: at the clip's left edge a bilinear tap would otherwise mix in
                // the colour half's white backdrop as a faint line.
                float alphaU = clamp(.5 + uv.x * .5, .5 + _MainTex_TexelSize.x * .5, 1. - _MainTex_TexelSize.x * .5);
                float a = tex2D(_MainTex, float2(alphaU, uv.y)).g;
                a = lerp(a, LinearToGammaSpaceExact(a), _AlphaFromSrgb);
                // Codec noise in the black of the matte must not show as faint blocks.
                a = saturate((a - .03) / .94);
                half4 c = half4(rgb, a) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
