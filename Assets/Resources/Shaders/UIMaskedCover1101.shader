// 2026-11-01 唱片收藏: a song cover shown INSIDE one of the board's torn frames. The cover is
// centre-cropped (_CoverST, like CSS background-size: cover), multiplied by the frame's own soft
// window mask (_MaskTex, alpha), optionally darkened toward the bottom for the caption (_CapHeight
// as a fraction of the window, _CapColor) and drained to grey for a locked song (_Gray).
// Same stencil/clip contract as UI/Default so it works under Mask and RectMask2D.
Shader "ChoSiren/UIMaskedCover1101"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cover", 2D) = "white" {}
        _MaskTex ("Window Mask", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CoverST ("Cover Scale/Offset", Vector) = (1,1,0,0)
        _CapHeight ("Caption Height", Float) = 0
        _CapColor ("Caption Color", Color) = (0.03,0.012,0.08,0.92)
        _Gray ("Gray", Range(0,1)) = 0
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
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
            sampler2D _MaskTex;
            fixed4 _Color;
            float4 _CoverST;
            float _CapHeight;
            fixed4 _CapColor;
            float _Gray;
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
                float2 uv = i.texcoord;
                half4 c = (tex2D(_MainTex, uv * _CoverST.xy + _CoverST.zw) + _TextureSampleAdd);
                half grey = dot(c.rgb, half3(.299, .587, .114)) * .42;
                c.rgb = lerp(c.rgb, half3(grey, grey, grey), saturate(_Gray));
                if (_CapHeight > 0.0001)
                {
                    half k = saturate(1.0 - uv.y / _CapHeight);
                    c.rgb = lerp(c.rgb, _CapColor.rgb, k * _CapColor.a);
                }
                c *= i.color;
                c.a *= tex2D(_MaskTex, uv).a;
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
