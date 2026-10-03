// Imported effect videos (media library): drawn as light over the battle, so a black background is see-through.
Shader "AdamsHaven/ScreenAdd"
{
    Properties
    {
        _MainTex ("Effect video", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off ZTest Always Cull Off
        Blend One One
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
                fixed3 rgb = tex2D(_MainTex, i.uv).rgb * _Color.rgb * _Color.a;
                return fixed4(rgb, 1);
            }
            ENDCG
        }
    }
}
