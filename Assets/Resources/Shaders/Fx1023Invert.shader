// P5-style impact frame: turns whatever is already on screen into its negative, tinted by the vertex colour
// (result = (1 - destination) * colour). Drawn as a full-screen Image for two frames on a big hit.
Shader "ChoSiren/Fx1023Invert"
{
    Properties { [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend OneMinusDstColor Zero
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };
            v2f vert(appdata_t v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target { return fixed4(i.color.rgb, 1.); }
            ENDCG
        }
    }
}
