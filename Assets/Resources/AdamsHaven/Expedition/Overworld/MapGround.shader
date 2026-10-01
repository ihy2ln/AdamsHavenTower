// Ground of the layered expedition map: ten tileable ground textures blended by soft per-cell weights
// (three RGBA weight maps, noise-warped so no squares show), with the worn-road layer painted over them.
Shader "AdamsHaven/MapGround"
{
    Properties
    {
        _W0 ("Weights 0-3", 2D) = "black" {}
        _W1 ("Weights 4-7", 2D) = "black" {}
        _W2 ("Weights 8-9", 2D) = "black" {}
        _Road ("Worn road", 2D) = "black" {}
        _G0 ("Meadow", 2D) = "gray" {}
        _G1 ("Forest floor", 2D) = "gray" {}
        _G2 ("Deep moss", 2D) = "gray" {}
        _G3 ("Dirt road", 2D) = "gray" {}
        _G4 ("Scree", 2D) = "gray" {}
        _G5 ("Ruin", 2D) = "gray" {}
        _G6 ("Blight", 2D) = "gray" {}
        _G7 ("Marsh", 2D) = "gray" {}
        _G8 ("Deep water", 2D) = "gray" {}
        _G9 ("Shallow water", 2D) = "gray" {}
        _Cells ("Grid size (xy) and cells per tile (z)", Vector) = (64, 40, 6, 0)
        _Warp ("Edge warp in cells", Float) = 0.45
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _W0, _W1, _W2, _Road, _G0, _G1, _G2, _G3, _G4, _G5, _G6, _G7, _G8, _G9;
            float4 _Cells;
            float _Warp;
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
            float fbm(float2 p) { return vnoise(p) * 0.6 + vnoise(p * 2.3 + 7.1) * 0.3 + vnoise(p * 5.1 + 3.3) * 0.1; }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 cell = i.uv * _Cells.xy;
                float2 warp = float2(fbm(cell * 0.7), fbm(cell * 0.7 + 19.3)) - 0.5;
                float2 wuv = i.uv + warp * _Warp * 2 / _Cells.xy;
                float4 w0 = tex2D(_W0, wuv), w1 = tex2D(_W1, wuv), w2 = tex2D(_W2, wuv);
                // Sharpen the blend a little so materials read as patches, not mush.
                w0 = pow(w0, 1.6); w1 = pow(w1, 1.6); w2 = pow(w2, 1.6);
                float total = dot(w0, 1) + dot(w1, 1) + w2.x + w2.y + 1e-4;
                float2 t = cell / _Cells.z;
                float3 col = tex2D(_G0, t).rgb * w0.x + tex2D(_G1, t).rgb * w0.y + tex2D(_G2, t).rgb * w0.z + tex2D(_G3, t).rgb * w0.w
                           + tex2D(_G4, t).rgb * w1.x + tex2D(_G5, t).rgb * w1.y + tex2D(_G6, t).rgb * w1.z + tex2D(_G7, t).rgb * w1.w
                           + tex2D(_G8, t * 0.7 + _Time.x * 0.15).rgb * w2.x + tex2D(_G9, t).rgb * w2.y;
                col /= total;
                // Walked ground worn into road: ragged-edged dirt over whatever is there.
                float road = tex2D(_Road, i.uv + warp * 0.25 / _Cells.xy).r;
                float worn = smoothstep(0.14, 0.42, road + (fbm(cell * 2.7) - 0.5) * 0.22);
                float3 dirt = tex2D(_G3, t * 1.3).rgb;
                col = lerp(col, dirt * 1.04, worn * 0.9);
                // Soft vignette toward the map edge.
                float2 e = min(i.uv, 1 - i.uv) * _Cells.xy;
                col *= lerp(0.7, 1, saturate(min(e.x, e.y) / 3));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
