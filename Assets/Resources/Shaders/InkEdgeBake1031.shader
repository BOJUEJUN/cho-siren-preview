// 2026-10-31: copies one sprite's alpha into a padded square (zero outside the sprite's own rect, so atlas
// neighbours and cropped captions never leak in). InkFlameEdge1031 reads it back once to build the edge field.
Shader "Hidden/ChoSiren/InkEdgeBake1031"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Src ("Sprite UV rect (xy min, zw size)", Vector) = (0,0,1,1)
        _Inner ("Sprite rect inside the padded quad", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Src, _Inner;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 q = (i.uv - _Inner.xy) / _Inner.zw;
                if (q.x < 0.0 || q.y < 0.0 || q.x > 1.0 || q.y > 1.0) return fixed4(0, 0, 0, 0);
                fixed a = tex2D(_MainTex, _Src.xy + q * _Src.zw).a;
                return fixed4(a, a, a, a);
            }
            ENDCG
        }
    }
}
