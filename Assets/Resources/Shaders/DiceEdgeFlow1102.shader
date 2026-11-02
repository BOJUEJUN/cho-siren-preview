Shader "ChoSiren/UI/DiceEdgeFlow1102"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _EdgeColor ("Edge Colour", Color) = (.5, 1, 1, 1)
        _FxTime ("Time", Float) = 0
        _Seed ("Seed", Float) = 0
        _Alpha ("Opacity", Float) = 0
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
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; };
            fixed4 _EdgeColor;
            float4 _ClipRect;
            float _FxTime, _Seed, _Alpha;
            v2f vert(appdata v)
            {
                v2f o; o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o;
            }
            float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise21(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3.-2.*f);
                return lerp(lerp(hash21(cell),hash21(cell+float2(1,0)),f.x),
                    lerp(hash21(cell+float2(0,1)),hash21(cell+1.),f.x),f.y);
            }
            float roundBox(float2 p)
            {
                // 0.40 of this padded quad matches the outer metal rim of the square face.
                float2 q=abs(p)-float2(.323,.323);
                return length(max(q,0.))+min(max(q.x,q.y),0.)-.077;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.uv-.5;
                float d=roundBox(p);
                // No effect may enter the pip area or reach beyond a few pixels of the rim.
                if(d<-.025 || d>.065) discard;
                float t=_FxTime*.7;
                float along=atan2(p.y,p.x)*.15915494+.5;
                // Evaluate noise spatially so there is no seam where the angular parameter wraps.
                float wave=noise21(p*23.+float2(t*.8+_Seed,-t*.65));
                float grain=noise21(p*65.+float2(-t*1.7,t*2.1+_Seed));
                float edge=d-(wave-.5)*.012;
                float run=.5+.5*sin(along*12.56637-t*2.7+_Seed);
                float broken=smoothstep(.38,.75,wave*.7+grain*.3);
                float flow=smoothstep(.35,.9,run)*(.32+.68*broken);
                float filament=exp(-abs(edge)/.0055)*flow;
                float soft=exp(-abs(edge)/.018)*flow*.20;
                // Short, uneven tongues detach outward from the four edges, never a tall plume.
                float fringe=smoothstep(.55,.8,grain)*smoothstep(.04,.009,d)
                    *smoothstep(-.001,.009,d)*broken*.32;
                float glint=pow(saturate(grain*1.6-.55),8.)
                    *smoothstep(.05,.016,d)*smoothstep(.006,.022,d)*flow*.65;
                float a=saturate(filament*.86+soft+fringe+glint)*_Alpha*i.color.a;
                a*=smoothstep(-.025,-.012,d)*(1.-smoothstep(.045,.065,d));
                #ifdef UNITY_UI_CLIP_RECT
                    a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(a-.001);
                #endif
                float3 col=lerp(_EdgeColor.rgb,float3(1,1,1),saturate(filament*.45+glint));
                return fixed4(col*i.color.rgb,a);
            }
            ENDCG
        }
    }
}
