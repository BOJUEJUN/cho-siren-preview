// Hard-edged cel UI shader (2026-10-21 research §3): a hard alpha cutoff (cel shapes instead of soft
// gradients) and a noise erosion with a bright rim, so effect art breaks apart instead of fading.
// Same stencil/clip contract as UI/Default so it works under Mask and RectMask2D.
Shader "ChoSiren/UICelErode"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 1.6
        _Cutoff ("Alpha Cutoff (0 = soft)", Range(0,1)) = 0
        _Erode ("Erode", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0,0.3)) = 0.08
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
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
            sampler2D _NoiseTex;
            fixed4 _Color;
            fixed4 _EdgeColor;
            float _NoiseScale, _Cutoff, _Erode, _EdgeWidth;
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
            fixed4 frag(v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.texcoord) + _TextureSampleAdd;
                half4 c = tex * i.color;
                // Cel cutoff: fading shrinks the shape a little (not a ghost), and it stays solid inside.
                if (_Cutoff > 0.001) c.a = smoothstep(_Cutoff - 0.02, _Cutoff + 0.02, tex.a * (0.45 + 0.55 * i.color.a)) * step(0.02, i.color.a);
                if (_Erode > 0.001)
                {
                    float n = tex2D(_NoiseTex, i.texcoord * _NoiseScale).r;
                    float keep = step(_Erode, n);
                    float rim = keep * (1.0 - step(_Erode + _EdgeWidth, n));
                    c.rgb = lerp(c.rgb, _EdgeColor.rgb, rim);
                    c.a *= keep;
                }
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
