// Transparent effect video from an ordinary H.264 file (Tools/produce_move_fx.py): the top half of each frame is the
// premultiplied colour, the bottom half the matte. Android decodes H.264 in hardware, which VP8-with-alpha lacks.
Shader "AdamsHaven/StackedAlpha"
{
    Properties
    {
        _MainTex ("Stacked video", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off ZTest Always Cull Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 rgb = tex2D(_MainTex, float2(i.uv.x, 0.5 + i.uv.y * 0.5)).rgb;
                float a = tex2D(_MainTex, float2(i.uv.x, i.uv.y * 0.5)).r;
                return fixed4(rgb * _Color.rgb * _Color.a, a * _Color.a);
            }
            ENDCG
        }
    }
}
