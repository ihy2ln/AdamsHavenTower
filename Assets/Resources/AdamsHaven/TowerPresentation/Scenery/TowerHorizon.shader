Shader "AdamsHaven/Tower Horizon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Landscape", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FadeTop ("Sky blend", Range(0.001,1)) = 0.28
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Varying { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _FadeTop;
            Varying vert(Input v)
            {
                Varying o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(Varying i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.a *= smoothstep(0, _FadeTop, 1 - i.uv.y);
                return c;
            }
            ENDCG
        }
    }
}
