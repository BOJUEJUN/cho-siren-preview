// 2026-11-09 (少爷: "只有换装之类的参数，类似炫彩皮肤"): recolours a character into a chroma variant. Saturated,
// non-skin pixels take the target hue (their saturation / value kept, a little boosted); skin tones and
// near-greys are left alone. Same stencil/clip contract as UI/Default so it works under Mask and RectMask2D.
Shader "ChoSiren/UIChroma1109"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Hue ("Target Hue (0-1)", Range(0,1)) = 0
        _Strength ("Strength", Range(0,1)) = 0
        _SatBoost ("Saturation Boost", Range(0,2)) = 1
        _ValGain ("Value Gain", Range(0,2)) = 1
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
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Hue, _Strength, _SatBoost, _ValGain;
            fixed4 _TextureSampleAdd;
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
            float3 rgb2hsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-5;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }
            float3 hsv2rgb(float3 c)
            {
                float3 p = abs(frac(c.xxx + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
                return c.z * lerp(float3(1, 1, 1), saturate(p - 1.0), c.y);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                half4 c = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd);
                float3 hsv = rgb2hsv(c.rgb);
                // skin: warm hue (≈0–45°), moderate saturation, fairly bright
                float warm = 1.0 - smoothstep(0.10, 0.14, hsv.x) * (1.0 - smoothstep(0.96, 0.99, hsv.x));
                float skin = warm * (1.0 - smoothstep(0.50, 0.66, hsv.y)) * smoothstep(0.42, 0.58, hsv.z);
                float colourful = smoothstep(0.16, 0.32, hsv.y);
                float w = saturate(_Strength) * colourful * (1.0 - skin);
                float3 recol = hsv2rgb(float3(_Hue, saturate(hsv.y * _SatBoost), saturate(hsv.z * _ValGain)));
                c.rgb = lerp(c.rgb, recol, w);
                c *= i.color;
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
