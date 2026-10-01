// Fog of the layered expedition map: drifting cloud over everything the party has not seen,
// with a ragged, breathing edge where it thins out.
Shader "AdamsHaven/MapFog"
{
    Properties
    {
        _Seen ("Seen mask", 2D) = "black" {}
        _Cloud ("Cloud tile (optional)", 2D) = "white" {}
        _UseCloud ("Use cloud tile", Float) = 0
        _Color ("Fog colour", Color) = (0.11, 0.14, 0.19, 1)
        _Cells ("Grid size", Vector) = (64, 40, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _Seen, _Cloud;
            float _UseCloud;
            fixed4 _Color;
            float4 _Cells;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            float fbm(float2 p) { return vnoise(p) * 0.55 + vnoise(p * 2.1 + 5.2) * 0.3 + vnoise(p * 4.7 + 1.7) * 0.15; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 cell = i.uv * _Cells.xy;
                float2 drift = float2(_Time.y * 0.12, _Time.y * 0.05);
                float cloud = _UseCloud > 0.5 ? tex2D(_Cloud, cell / 16 + drift * 0.05).r : fbm(cell * 0.35 + drift);
                float seen = tex2D(_Seen, i.uv).r;
                float edge = seen + (fbm(cell * 0.9 - drift * 0.6) - 0.5) * 0.5;
                float fog = 1 - smoothstep(0.25, 0.65, edge);
                float a = fog * lerp(0.9, 1.0, cloud);
                float3 col = _Color.rgb * lerp(0.8, 1.3, cloud);
                // A faint haze lingers just inside the edge.
                a = max(a, (1 - smoothstep(0.45, 0.9, edge)) * 0.16 * cloud);
                return fixed4(col, saturate(a));
            }
            ENDCG
        }
    }
}
