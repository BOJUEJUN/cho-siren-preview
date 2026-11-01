// 2026-10-31 (赵金): the 「开始演出」 green brush edge, wrapped around any button silhouette.
// _MainTex (per button, baked once by InkFlameEdge1031): R = 1 on/inside the silhouette, falling to 0 at the
// padding edge (a distance field); G = position along the outline (mirrored, seamless).
// _InkTex: the approved R02 brush's ragged top edge for all 48 frames (Tools/lobby1031/gen_lobby1031_art.py).
// Vertex colour: rgb = ink tint, a = reach (0 = hidden inside the button, 1 = full flame).
// The frames advance with _Time on the GPU, so a lit edge costs no CPU work per frame.
Shader "ChoSiren/UIInkEdge1031"
{
    Properties
    {
        [PerRendererData] _MainTex ("Edge Field", 2D) = "black" {}
        _InkTex ("Ink Edge Strip", 2D) = "white" {}
        _Frozen ("Frozen Frame (-1 = animate)", Float) = -1
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
            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; };
            sampler2D _MainTex;
            sampler2D _InkTex;
            float _Frozen;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 field = tex2D(_MainTex, i.texcoord).rgb;
                // Only the open air round the silhouette burns; the art covers its own body and holes stay clean.
                if (field.b < 0.5) discard;
                half reach = i.color.a;
                // 0 on the silhouette, 1 at the padding edge; the flame grows out as reach rises.
                half d = (1.0 - field.r) / max(reach, 0.001);
                if (d >= 1.0) discard;
                float frame = _Frozen >= 0 ? _Frozen : floor(fmod(_Time.y * 30.0, 48.0));
                // Strip rows: 0 = outside the paint, 31 = solid paint; 32 rows per frame, 48 frames.
                float row = frame * 32.0 + (1.0 - d) * 31.0 + 0.5;
                float2 inkUv = float2(lerp(0.5 / 256.0, 1.0 - 0.5 / 256.0, field.g), 1.0 - row / 1536.0);
                half4 ink = tex2D(_InkTex, inkUv);
                half a = smoothstep(0.36, 0.64, ink.a) * saturate(reach * 5.0);
                half3 rgb = i.color.rgb * lerp(0.80, 1.04, ink.r);
                fixed4 c = fixed4(rgb, a);
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
